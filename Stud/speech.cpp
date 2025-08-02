#define _USE_MATH_DEFINES
#define WIN32_LEAN_AND_MEAN
#include <Windows.h>
#ifdef max
#undef max
#endif
#ifdef min
#undef min
#endif
#include "speech.h"
#include <algorithm>
#include <sstream>
#include <regex>
#include <cmath>
SpeechInput::~SpeechInput(){
	stopTranscription();
	unloadModel();
}
void SpeechInput::log(const std::string& message){
	std::lock_guard<std::mutex> lock(_callbackMutex);
	//OutputDebugStringA((message + "\n").c_str());
	if(_logCallback){
		_logCallback(message.c_str());
	}
}
void SpeechInput::normalizeAudio(std::vector<float>& data){
	if(data.empty()) return;
	const auto [min_it, max_it] = std::minmax_element(data.begin(), data.end());
	const float max_abs = std::max(std::abs(*min_it), std::abs(*max_it));
	if(max_abs > 0.0001f){
		// Avoid division by very small numbers
		const float scale = 1.0f / max_abs;
		std::transform(data.begin(), data.end(), data.begin(), [scale](float sample){ return sample * scale; });
	}
}
bool SpeechInput::vadSimple(std::vector<float>& pcmf32, int sampleRate, int lastMs, float vadThold, float freqThold){
	const int nSamples = static_cast<int>(pcmf32.size());
	const int nSamplesLast = (sampleRate * lastMs) / 1000;
	if(nSamplesLast >= nSamples || nSamples == 0){ return false; }
	// Apply high-pass filter if threshold is set
	if(freqThold > 0.0f){ _highPassFilter.process(pcmf32, freqThold, static_cast<float>(sampleRate)); }
	// Calculate energy
	float energyAll = 0.0f;
	float energyLast = 0.0f;
	for(int i = 0; i < nSamples; i++){
		const float abs_sample = std::abs(pcmf32[i]);
		energyAll += abs_sample;
		if(i >= nSamples - nSamplesLast){ energyLast += abs_sample; }
	}
	energyAll /= nSamples;
	energyLast /= nSamplesLast;
	// Voice activity detected if recent energy is significantly higher
	return energyLast > vadThold*energyAll;
}
float SpeechInput::calculateSimilarity(const std::string& s0, const std::string& s1){
	if(s0.empty() && s1.empty()) return 1.0f;
	if(s0.empty() || s1.empty()) return 0.0f;
	const size_t len0 = s0.size() + 1;
	const size_t len1 = s1.size() + 1;
	std::vector<int> col(len1, 0);
	std::vector<int> prevCol(len1, 0);
	for(size_t i = 0; i < len1; i++){ prevCol[i] = static_cast<int>(i); }
	for(size_t i = 0; i < len0; i++){
		col[0] = static_cast<int>(i);
		for(size_t j = 1; j < len1; j++){
			const int cost = (i > 0 && s0[i - 1] == s1[j - 1]) ? 0 : 1;
			col[j] = std::min({
				1 + col[j - 1],
				// insertion
				1 + prevCol[j],
				// deletion
				prevCol[j - 1] + cost // substitution
			});
		}
		col.swap(prevCol);
	}
	const float dist = static_cast<float>(prevCol[len1 - 1]);
	const float maxLen = static_cast<float>(std::max(s0.size(), s1.size()));
	return 1.0f - (dist / maxLen);
}
std::vector<std::string> SpeechInput::getWords(const std::string& s){
	std::istringstream iss(s);
	std::vector<std::string> words;
	words.reserve(16); // Reserve some space to avoid reallocations
	std::string word;
	while(iss >> word){ words.push_back(std::move(word)); }
	return words;
}
std::string SpeechInput::getWakeCommand(){
	std::lock_guard<std::mutex> lock(_configMutex);
	return _wakeCommand;
}
bool SpeechInput::loadModel(const char* modelPath, int nThreads, bool useGPU){
	if(!modelPath){
		log("Error: Model path is null");
		return false;
	}
	unloadModel();
	try{
		whisper_context_params cparams = whisper_context_default_params();
		cparams.use_gpu = useGPU;
		auto* ctx = whisper_init_from_file_with_params(modelPath, cparams);
		if(!ctx){
			log("Error: Failed to load whisper model from: " + std::string(modelPath));
			return false;
		}
		_whisperCtx.reset(ctx);
		// Configure whisper parameters for real-time processing
		_wparams = whisper_full_default_params(WHISPER_SAMPLING_GREEDY);
		_wparams.n_threads = nThreads;
		_wparams.tdrz_enable = true;
		_wparams.print_progress = false;
		_wparams.print_timestamps = false;
		_wparams.print_realtime = false;
		_wparams.translate = false;
		_wparams.no_context = false;
		_wparams.single_segment = false;
		_wparams.suppress_blank = true;
		_wparams.suppress_nst = true;
		// Initialize audio capture
		constexpr int capture_length_ms = 30000;
		_audioCapture = std::make_unique<audio_async>(capture_length_ms);
		if(!_audioCapture->init(-1, DEFAULT_SAMPLE_RATE)){
			log("Error: Failed to initialize audio capture");
			_whisperCtx.reset();
			return false;
		}
		log("Whisper model loaded successfully");
		return true;
	} catch(const std::exception& e){
		log("Exception while loading model: " + std::string(e.what()));
		return false;
	}
}
void SpeechInput::unloadModel(){
	stopTranscription();
	if(_audioCapture){
		_audioCapture->pause();
		_audioCapture.reset();
	}
	_whisperCtx.reset();
	_highPassFilter.reset();
	log("Whisper model unloaded");
}
void SpeechInput::transcriptionLoop(){
	log("Transcription thread started");
	// Pre-allocate buffers
	std::vector<float> pcmDataShort;
	std::vector<float> pcmDataLong;
	pcmDataShort.reserve(DEFAULT_SAMPLE_RATE * VAD_CHECK_DURATION / 1000);
	_audioCapture->resume();
	while(_transcriptionRunning.load()){
		try{
			std::this_thread::sleep_for(std::chrono::milliseconds(THREAD_SLEEP_MS));
			// Get current configuration values
			const int voiceDuration = _voiceDuration.load();
			const float vadThreshold = _vadThreshold.load();
			const float freqThreshold = _freqThreshold.load();
			const float wakeWordThreshold = _wakeWordThreshold.load();
			const std::string wakeCommand = getWakeCommand();
			// Resize buffers if needed
			const int nShortSamples = DEFAULT_SAMPLE_RATE * VAD_CHECK_DURATION / 1000;
			const int nLongSamples = DEFAULT_SAMPLE_RATE * voiceDuration / 1000;
			pcmDataShort.resize(nShortSamples);
			pcmDataLong.resize(nLongSamples);
			// Check for voice activity
			_audioCapture->get(VAD_CHECK_DURATION, pcmDataShort);
			if(!vadSimple(pcmDataShort, DEFAULT_SAMPLE_RATE, VAD_LAST_MS, vadThreshold, freqThreshold)){ continue; }
			log("Voice activity detected, capturing audio...");
			// Capture longer audio segment
			_audioCapture->get(voiceDuration, pcmDataLong);
			// Normalize audio
			normalizeAudio(pcmDataLong);
			// Perform transcription
			const int result = whisper_full(_whisperCtx.get(), _wparams, pcmDataLong.data(), static_cast<int>(pcmDataLong.size()));
			if(result != 0){
				log("Whisper transcription failed with code: " + std::to_string(result));
				continue;
			}
			// Extract transcription result
			std::string transcriptionResult;
			transcriptionResult.reserve(256);
			const int nSegs = whisper_full_n_segments(_whisperCtx.get());
			for(int i = 0; i < nSegs; i++){
				const char* text = whisper_full_get_segment_text(_whisperCtx.get(), i);
				if(text){ transcriptionResult += text; }
			}
			if(transcriptionResult.empty()){ continue; }
			log("Raw transcription: " + transcriptionResult);
			// Process wake command if configured
			if(!wakeCommand.empty()){
				const auto wakeWords = getWords(wakeCommand);
				const auto transWords = getWords(transcriptionResult);
				if(transWords.size() < wakeWords.size()){ continue; }
				// Extract potential wake words and remaining text
				std::string heardWake;
				std::string remaining;
				for(size_t i = 0; i < transWords.size(); ++i){
					if(i < wakeWords.size()){
						if(!heardWake.empty()) heardWake += " ";
						heardWake += transWords[i];
					} else{
						if(!remaining.empty()) remaining += " ";
						remaining += transWords[i];
					}
				}
				const float similarity = calculateSimilarity(heardWake, wakeCommand);
				log("Wake word similarity: " + std::to_string(similarity) + " (threshold: " + std::to_string(wakeWordThreshold) + ")");
				if(similarity < wakeWordThreshold || remaining.empty()){ continue; }
				transcriptionResult = remaining;
			}
			// Fire callback if transcription is new and non-empty
			if(!transcriptionResult.empty()){
				log("Final transcription: " + transcriptionResult);
				std::lock_guard<std::mutex> lock(_callbackMutex);
				if(_whisperCallback){ _whisperCallback(transcriptionResult.c_str()); }
				_audioCapture->clear();
			}
		} catch(const std::exception& e){
			log("Exception in transcription loop: " + std::string(e.what()));
			std::this_thread::sleep_for(std::chrono::milliseconds(1000));
		}
	}
	_audioCapture->pause();
	log("Transcription thread stopped");
}
bool SpeechInput::startTranscription(){
	if(!_whisperCtx || !_audioCapture){
		log("Error: Model not loaded or audio capture not initialized");
		return false;
	}
	if(_transcriptionRunning.load()){
		log("Warning: Transcription already running");
		return true;
	}
	_transcriptionRunning.store(true);
	_transcriptionThread = std::thread(&SpeechInput::transcriptionLoop, this);
	return true;
}
void SpeechInput::stopTranscription(){
	_transcriptionRunning.store(false);
	if(_transcriptionThread.joinable()){ _transcriptionThread.join(); }
}
void SpeechInput::setCallback(WhisperCallbackFn cb){
	std::lock_guard<std::mutex> lock(_callbackMutex);
	_whisperCallback = cb;
}
void SpeechInput::setLogCallback(LogCallbackFn cb){
	std::lock_guard<std::mutex> lock(_callbackMutex);
	_logCallback = cb;
}
void SpeechInput::setWakeCommand(const char* wakeCmd){
	std::lock_guard<std::mutex> lock(_configMutex);
	if(wakeCmd){
		_wakeCommand = wakeCmd;
		log("Wake command set to: " + _wakeCommand);
	} else{
		_wakeCommand.clear();
		log("Wake command cleared");
	}
}
void SpeechInput::setVADThresholds(float vadThreshold, float freqThreshold){
	_vadThreshold.store(vadThreshold);
	_freqThreshold.store(freqThreshold);
	log("VAD thresholds updated - VAD: " + std::to_string(vadThreshold) + ", Freq: " + std::to_string(freqThreshold));
}
void SpeechInput::setWakeWordThreshold(float threshold){
	_wakeWordThreshold.store(threshold);
	log("Wake word threshold set to: " + std::to_string(threshold));
}
void SpeechInput::setVoiceDuration(int voiceDuration){
	_voiceDuration.store(voiceDuration);
	log("Voice duration set to: " + std::to_string(voiceDuration) + "ms");
}
// C API Implementation
void SetWhisperCallback(WhisperCallbackFn cb){
	if(!g_speechRecognizer){ g_speechRecognizer = std::make_unique<SpeechInput>(); }
	g_speechRecognizer->setCallback(cb);
}
void SetLogCallback(LogCallbackFn cb){
	if(!g_speechRecognizer){ g_speechRecognizer = std::make_unique<SpeechInput>(); }
	g_speechRecognizer->setLogCallback(cb);
}
bool LoadWhisperModel(const char* modelPath, int nThreads, bool useGPU){
	if(!g_speechRecognizer){ g_speechRecognizer = std::make_unique<SpeechInput>(); }
	return g_speechRecognizer->loadModel(modelPath, nThreads, useGPU);
}
bool StartSpeechTranscription(){
	if(!g_speechRecognizer){ return false; }
	return g_speechRecognizer->startTranscription();
}
void StopSpeechTranscription(){ if(g_speechRecognizer){ g_speechRecognizer->stopTranscription(); } }
void UnloadWhisperModel(){ if(g_speechRecognizer){ g_speechRecognizer->unloadModel(); } }
void SetWakeCommand(const char* wakeCmd){
	if(!g_speechRecognizer){ g_speechRecognizer = std::make_unique<SpeechInput>(); }
	g_speechRecognizer->setWakeCommand(wakeCmd);
}
void SetVADThresholds(float vadThreshold, float freqThreshold){
	if(!g_speechRecognizer){ g_speechRecognizer = std::make_unique<SpeechInput>(); }
	g_speechRecognizer->setVADThresholds(vadThreshold, freqThreshold);
}
void SetWakeWordThreshold(float threshold){
	if(!g_speechRecognizer){ g_speechRecognizer = std::make_unique<SpeechInput>(); }
	g_speechRecognizer->setWakeWordThreshold(threshold);
}
void SetVoiceDuration(int voiceDuration){
	if(!g_speechRecognizer){ g_speechRecognizer = std::make_unique<SpeechInput>(); }
	g_speechRecognizer->setVoiceDuration(voiceDuration);
}
bool IsTranscriptionRunning(){
	if(!g_speechRecognizer){ return false; }
	return g_speechRecognizer->isRunning();
}
#define _USE_MATH_DEFINES
#include "speech.h"
#include <thread>
#include <atomic>
#include <vector>
#include <string>
#include <regex>
#include <sstream>
static std::string trim(const std::string& s){
	const size_t start = s.find_first_not_of(" \t\n\r");
	if(start == std::string::npos) return "";
	const size_t end = s.find_last_not_of(" \t\n\r");
	return s.substr(start, end - start + 1);
}
bool LoadWhisperModel(const char* modelPath, const int nThreads, const bool useGPU, const bool useVAD, const char* vadModel){
	UnloadWhisperModel();
	whisper_context_params cparams = whisper_context_default_params();
	cparams.use_gpu = useGPU;
	_whisperCtx = whisper_init_from_file_with_params(modelPath, cparams);
	if(!_whisperCtx) return false;
	_wparams = whisper_full_default_params(WHISPER_SAMPLING_GREEDY);
	_wparams.n_threads = nThreads;
	_wparams.tdrz_enable = true;
	_wparams.temperature = _temp;
	_wparams.print_progress = false;
	_wparams.print_timestamps = false;
	_wparams.no_timestamps = true;
	_wparams.suppress_nst = true;
	if(useVAD && vadModel != nullptr && vadModel != ""){
		_wparams.vad = useVAD;
		_vadModel = vadModel;
		_wparams.vad_model_path = _vadModel.c_str();
		_wparams.vad_params.max_speech_duration_s = _voiceDuration;
		_wparams.vad_params.threshold = _vadThreshold;
		whisper_vad_context_params vadCParams = whisper_vad_default_context_params();
		vadCParams.n_threads = nThreads;
		vadCParams.use_gpu = useGPU;
		_vadCtx = whisper_vad_init_from_file_with_params(_vadModel.c_str(), vadCParams);
		if(!_vadCtx) return false;
	}
	_audioCapture = new audio_async(_voiceDuration);
	if(!_audioCapture->init(-1, WHISPER_SAMPLE_RATE)){ return false; }
	return true;
}
void UnloadWhisperModel(){
	if(_audioCapture){
		_audioCapture->pause();
		delete _audioCapture;
		_audioCapture = nullptr;
	}
	if(_whisperCtx){
		whisper_vad_free(_vadCtx);
		_vadCtx = nullptr;
		whisper_free(_whisperCtx);
		_whisperCtx = nullptr;
	}
}
void HighPassFilter(std::vector<float>& data, const float cutoff, const float sampleRate){
	const float rc = 1.0f / (2.0f * M_PI * cutoff);
	const float dt = 1.0f / sampleRate;
	const float alpha = dt / (rc + dt);
	float y = data[0];
	for(size_t i = 1; i < data.size(); i++){
		y = alpha * (y + data[i] - data[i - 1]);
		data[i] = y;
	}
}
bool VadSimple(std::vector<float>& pcmf32, const int sampleRate, const int lastMs, const float vadThold, const float freqThold){
	const int nSamples = pcmf32.size();
	const int nSamplesLast = (sampleRate*lastMs)/1000;
	if(nSamplesLast >= nSamples){ return false; }
	if(freqThold > 0.0f){ HighPassFilter(pcmf32, freqThold, sampleRate); }
	float energyAll = 0.0f;
	float energyLast = 0.0f;
	for(int i = 0; i < nSamples; i++){
		energyAll += fabsf(pcmf32[i]);
		if(i >= nSamples - nSamplesLast){ energyLast += fabsf(pcmf32[i]); }
	}
	energyAll /= nSamples;
	energyLast /= nSamplesLast;
	if(energyLast > vadThold * energyAll){ return false; }
	return true;
}
float similarity(const std::string& s0, const std::string& s1){
	const size_t len0 = s0.size() + 1;
	const size_t len1 = s1.size() + 1;
	std::vector<int> col(len1, 0);
	std::vector<int> prevCol(len1, 0);
	for(size_t i = 0; i < len1; i++){ prevCol[i] = i; }
	for(size_t i = 0; i < len0; i++){
		col[0] = i;
		for(size_t j = 1; j < len1; j++){ col[j] = std::min(std::min(1 + col[j - 1], 1 + prevCol[j]), prevCol[j - 1] + (i > 0 && s0[i - 1] == s1[j - 1] ? 0 : 1)); }
		col.swap(prevCol);
	}
	const float dist = prevCol[len1 - 1];
	return 1.0f - (dist / std::max(s0.size(), s1.size()));
}
bool StartSpeechTranscription(){
	if(!_whisperCtx || !_audioCapture){ return false; }
	_transcriptionRunning.store(true);
	_transcriptionThread = std::thread([](){
		constexpr int nShortSamples = WHISPER_SAMPLE_RATE*2;
		std::vector<float> pcmDataShort(nShortSamples, 0.0f);
		const auto voiceDuration = _voiceDuration;
		const int nInitialSamples = (WHISPER_SAMPLE_RATE*voiceDuration)/1000;
		std::vector<float> pcmDataLong(nInitialSamples, 0.0f);
		// Helper lambda to split a string into words.
		auto getWords = [](const std::string& s) -> std::vector<std::string>{
			std::istringstream iss(s);
			std::vector<std::string> words;
			std::string word;
			while(iss >> word){ words.push_back(word); }
			return words;
		};
		// Variable to track last output and prevent duplicate transcriptions.
		std::string lastOutput;
		_audioCapture->clear();
		_audioCapture->resume();
		while(_transcriptionRunning.load()){
			std::this_thread::sleep_for(std::chrono::milliseconds(100));
			_audioCapture->get(2000, pcmDataShort);
			if(_vadCtx && !whisper_vad_detect_speech(_vadCtx, pcmDataShort.data(), pcmDataShort.size()/sizeof(float))) continue;
			else if(!VadSimple(pcmDataShort, WHISPER_SAMPLE_RATE, 1250, _vadThreshold, _freqThreshold)) continue;
			_audioCapture->get(voiceDuration, pcmDataLong);
			if(whisper_full(_whisperCtx, _wparams, pcmDataLong.data(), static_cast<int>(pcmDataLong.size())) != 0){
				pcmDataLong.clear();
				pcmDataLong.resize(nInitialSamples, 0.0f);
				continue;
			}
			std::string transcriptionResult;
			const int nSegs = whisper_full_n_segments(_whisperCtx);
			if(nSegs > 0){
				// Use the final segment to avoid duplicates.
				transcriptionResult = std::string(whisper_full_get_segment_text(_whisperCtx, nSegs - 1));
			}
			// Process wake command if configured.
			if(!_wakeCommand.empty()){
				auto wakeWords = getWords(_wakeCommand);
				const int wakeWordCount = wakeWords.size();
				auto transWords = getWords(transcriptionResult);
				if(transWords.size() < static_cast<size_t>(wakeWordCount)){
					// Clear the buffer before the next iteration.
					pcmDataLong.clear();
					pcmDataLong.resize(nInitialSamples, 0.0f);
					continue;
				}
				std::string heardWake;
				std::string remaining;
				for(size_t i = 0; i < transWords.size(); i++){ if(i < static_cast<size_t>(wakeWordCount)){ heardWake += transWords[i] + " "; } else{ remaining += transWords[i] + " "; } }
				heardWake = trim(heardWake);
				remaining = trim(remaining);
				const float sim = similarity(heardWake, _wakeCommand);
				// Only proceed if similarity is acceptable and there is text remaining.
				if(sim < _wakeWordSim || remaining.empty()){
					pcmDataLong.clear();
					pcmDataLong.resize(nInitialSamples, 0.0f);
					continue;
				}
				transcriptionResult = remaining;
			}
			// Only fire the callback if the transcription is non-empty and different from the last output.
			if(_whisperCallback && !transcriptionResult.empty() && transcriptionResult != lastOutput){
				lastOutput = transcriptionResult;
				_whisperCallback(transcriptionResult.c_str());
				_audioCapture->clear();
			}
			// Reinitialize the long audio buffer before next iteration.
			pcmDataLong.clear();
			pcmDataLong.resize(nInitialSamples, 0.0f);
		}
		_audioCapture->pause();
		});
	return true;
}
void StopSpeechTranscription(){
	_transcriptionRunning.store(false);
	if(_transcriptionThread.joinable()){ _transcriptionThread.join(); }
}
void SetWhisperCallback(WhisperCallbackFn cb){ _whisperCallback = cb; }
void SetWakeCommand(const char* wakeCmd){ if(wakeCmd){ _wakeCommand = wakeCmd; } else{ _wakeCommand.clear(); } }
void SetVADThresholds(const float vad, const float freq){
	_vadThreshold = vad;
	_freqThreshold = freq;
}
void SetVoiceDuration(const int voiceDuration){
	_voiceDuration = voiceDuration;
}
void SetWakeWordSimilarity(float similarity){
	_wakeWordSim = similarity;
}
void SetWhisperTemp(float temp){
	_temp = temp;
}
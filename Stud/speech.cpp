#define _USE_MATH_DEFINES
#include "speech.h"
#include "StudError.h"
#include <windows.h>
#include <thread>
#include <atomic>
#include <llama.h>
#include <vector>
#include <string>
#include <regex>
#include <sstream>
#include <cstring>
#include <cctype>
static std::mutex _committedMutex;
static std::atomic<bool> _wakeWordDetected{false};
static bool _gpuOomSpeech = false;
static void WhisperLogDisable(ggml_log_level level, const char* text, void* userData){}
static void GPUOomLogCallbackSpeech(ggml_log_level level, const char* text, void* userData){
	fputs(text, stderr);
	fflush(stderr);
	if(level == GGML_LOG_LEVEL_ERROR || level == GGML_LOG_LEVEL_WARN){
		const std::string_view msg(text);
		if(msg.find("out of memory") != std::string_view::npos) _gpuOomSpeech = true;
		if(msg.find("failed to allocate") != std::string_view::npos) _gpuOomSpeech = true;
	}
}
LONG WINAPI ExceptionHandler(EXCEPTION_POINTERS* exceptionInfo){
	if(exceptionInfo->ExceptionRecord->ExceptionCode == EXCEPTION_ACCESS_VIOLATION){
		_gpuOomSpeech = true;
		return EXCEPTION_EXECUTE_HANDLER;
	}
	return EXCEPTION_CONTINUE_SEARCH;
}
StudError LoadWhisperModel(const char* modelPath, const int nThreads, const bool useGPU, const bool useVAD, const char* vadModel){
	UnloadWhisperModel();
	whisper_context_params cparams = whisper_context_default_params();
	cparams.use_gpu = useGPU;
	_gpuOomSpeech = false;
	const auto oldHandler = SetUnhandledExceptionFilter(ExceptionHandler);
	ggml_log_set(GPUOomLogCallbackSpeech, nullptr);
	_whisperCtx = whisper_init_from_file_with_params(modelPath, cparams);
	ggml_log_set(nullptr, nullptr);
	SetUnhandledExceptionFilter(oldHandler);
	if(!_whisperCtx) return _gpuOomSpeech ? StudError::GpuOutOfMemory : StudError::CantLoadWhisperModel;
	_wparams = whisper_full_default_params(WHISPER_SAMPLING_GREEDY);
	_wparams.n_threads = nThreads;
	_wparams.tdrz_enable = false;
	_wparams.temperature = _temp;
	_wparams.print_progress = false;
	_wparams.print_timestamps = false;
	_wparams.no_timestamps = true;
	_wparams.suppress_nst = true;
	_wparams.single_segment = true;
	if(useVAD && vadModel && std::strlen(vadModel) > 0){
		_wparams.vad = useVAD;
		_vadModel = vadModel;
		_wparams.vad_model_path = _vadModel.c_str();
		_wparams.vad_params.max_speech_duration_s = _voiceDurationMS / 1000.0f;
		_wparams.vad_params.threshold = _vadThreshold;
		whisper_vad_context_params vadCParams = whisper_vad_default_context_params();
		vadCParams.n_threads = nThreads;
		vadCParams.use_gpu = false;
		_vadCtx = whisper_vad_init_from_file_with_params(_vadModel.c_str(), vadCParams);
		if(!_vadCtx) return StudError::CantLoadVADModel;
	}
	_audioCapture = new audio_async(_voiceDurationMS);
	if(!_audioCapture->init(-1, WHISPER_SAMPLE_RATE)){
		delete _audioCapture;
		_audioCapture = nullptr;
		return StudError::CantInitAudioCapture;
	}
	whisper_log_set(WhisperLogDisable, nullptr);
	return StudError::Success;
}
void UnloadWhisperModel(){
	if(_audioCapture){
		_audioCapture->pause();
		delete _audioCapture;
		_audioCapture = nullptr;
	}
	if(_vadCtx){
		whisper_vad_free(_vadCtx);
		_vadCtx = nullptr;
	}
	if(_whisperCtx){
		whisper_free(_whisperCtx);
		_whisperCtx = nullptr;
	}
}
void HighPassFilter(std::vector<float>& data, const float cutoff, const float sampleRate){
	const float rc = 1.0f / (2.0f*M_PI*cutoff);
	const float dt = 1.0f / sampleRate;
	const float alpha = dt / (rc + dt);
	float y = data[0];
	for(size_t i = 1; i < data.size(); i++){
		y = alpha*(y + data[i] - data[i - 1]);
		data[i] = y;
	}
}
float _vadEnergy;
bool VadSimple(std::vector<float>& pcmf32, const int sampleRate){
	const int nSamples = pcmf32.size();
	if(nSamples == 0){ _vadEnergy = 0.0f; return false; }
	if(_freqThreshold > 0.0f){ HighPassFilter(pcmf32, _freqThreshold, sampleRate); }
	float energy = 0.0f;
	for(int i = 0; i < nSamples; ++i){
		energy += fabsf(pcmf32[i]);
	}
	energy /= nSamples;
	static float energyAvg = 0.0f;
	if(energyAvg <= 0.0f) energyAvg = energy;
	_vadEnergy = energy;
	const float ratio = energyAvg > 0.0f ? energy / energyAvg : 0.0f;
	const float prob = ratio / (ratio + 1.0f);
	OutputDebugStringA(("VadSimple prob: " + std::to_string(prob) + "\n").c_str());
	const bool speech = prob >= std::clamp(_vadThreshold, 0.0f, 1.0f);
	energyAvg = 0.95f*energyAvg + 0.05f*energy;
	return speech;
}
float Similarity(const std::string& s0, const std::string& s1){
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
	return 1.0f - dist / std::max(s0.size(), s1.size());
}
bool StartSpeechTranscription(){
	if(!_whisperCtx || !_audioCapture || _transcriptionThread.joinable()){ return false; }
	_wakeWordDetected.store(false);
	_transcriptionRunning.store(true);
	_transcriptionThread = std::thread([]{
		const int stepMs = 500;
		const int nSamplesStep = 1e-3*stepMs*WHISPER_SAMPLE_RATE;
		std::vector<float> pcmBuffer;
		std::vector<float> pcmStep;
		auto getWords = [](const std::string& s) -> std::vector<std::string>{
			std::istringstream iss(s);
			std::vector<std::string> words;
			std::string w;
			while(iss >> w) words.push_back(w);
			return words;
		};
		auto joinRange = [](const std::vector<std::string>& v, size_t a, size_t b) -> std::string{
			std::string out;
			for(size_t i = a; i < b; ++i){
				if(i > a) out += ' ';
				out += v[i];
			}
			return out;
		};
		auto trimWakeWord = [](std::string& text){
			if(_wakeCommand.empty()) return;
			size_t pos = 0;
			while(pos < text.size() && std::isspace(static_cast<unsigned char>(text[pos]))) ++pos;
			std::string lowerText = text.substr(pos);
			std::transform(lowerText.begin(), lowerText.end(), lowerText.begin(),
				[](unsigned char c){ return static_cast<char>(std::tolower(c)); });
			std::string lowerWake = _wakeCommand;
			std::transform(lowerWake.begin(), lowerWake.end(), lowerWake.begin(),
				[](unsigned char c){ return static_cast<char>(std::tolower(c)); });
			if(lowerText.rfind(lowerWake, 0) == 0){
				pos += lowerWake.size();
				if(pos < text.size() && text[pos] == ',') ++pos;
				while(pos < text.size() && std::isspace(static_cast<unsigned char>(text[pos]))) ++pos;
				text.erase(0, pos);
				if(!text.empty()) text[0] = static_cast<char>(std::toupper(static_cast<unsigned char>(text[0])));
			}
		};
		std::string pending;
		std::string lastOutput;
		bool speaking = false;
		auto lastSpeech = std::chrono::steady_clock::now();
		_audioCapture->resume();
		while(_transcriptionRunning.load()){
			while(_transcriptionRunning.load()){
				_audioCapture->get(stepMs, pcmStep);
				if(static_cast<int>(pcmStep.size()) > 2*nSamplesStep){
					_audioCapture->clear();
					continue;
				}
				if(static_cast<int>(pcmStep.size()) >= nSamplesStep){
					_audioCapture->clear();
					break;
				}
				std::this_thread::sleep_for(std::chrono::milliseconds(1));
			}
			if(!_transcriptionRunning.load()) break;
			pcmBuffer.insert(pcmBuffer.end(), pcmStep.begin(), pcmStep.end());
			bool hasSpeech = false;
			if(_vadCtx){
				if(whisper_vad_detect_speech(_vadCtx, pcmStep.data(), pcmStep.size())){
					const int nProbs = whisper_vad_n_probs(_vadCtx);
					const float* probs = whisper_vad_probs(_vadCtx);
					for(int i = 0; i < nProbs; ++i){
						if(probs[i] > 0.1f) OutputDebugStringA(("VAD prob " + std::to_string(probs[i]) + "/" + std::to_string(_wparams.vad_params.threshold) + "\n").c_str());
						if(probs[i] > _wparams.vad_params.threshold){
							hasSpeech = true;
							break;
						}
					}
				}
			} else{ hasSpeech = VadSimple(pcmStep, WHISPER_SAMPLE_RATE); }
			auto now = std::chrono::steady_clock::now();
			if(!hasSpeech){
				if(speaking && now - lastSpeech > std::chrono::milliseconds(std::min(1000, _silenceTimeoutMs.load()))){
					OutputDebugStringA("Commit and clear\n");
					speaking = false;
					{
						std::lock_guard<std::mutex> lock(_committedMutex);
						_committed += pending;
					}
					pending.clear();
					lastOutput.clear();
					pcmBuffer.clear();
					if(_whisperCallback) _whisperCallback(_committed.c_str());
				}
				if(!speaking && _wakeWordDetected.load() && now - lastSpeech > std::chrono::milliseconds(_silenceTimeoutMs.load())){
					_wakeWordDetected.exchange(false);
					std::lock_guard<std::mutex> lock(_committedMutex);
					if(!_committed.empty()){
						if(_speechEndCallback) _speechEndCallback();
						_committed.clear();
					}
				}
				continue;
			}
			speaking = true;
			lastSpeech = now;
			_wparams.prompt_tokens = nullptr;
			_wparams.prompt_n_tokens = 0;
			if(whisper_full(_whisperCtx, _wparams, pcmBuffer.data(), static_cast<int>(pcmBuffer.size())) != 0){ continue; }
			std::string transcriptionResult;
			const int nSegs = whisper_full_n_segments(_whisperCtx);
			for(int i = 0; i < nSegs; ++i){
				const char* seg = whisper_full_get_segment_text(_whisperCtx, i);
				if(seg) transcriptionResult += seg;
			}
			if(!_wakeCommand.empty() && !_wakeWordDetected.load()){
				auto normalize = [](const std::string& in) -> std::string{
					std::string out;
					out.reserve(in.size());
					for(const char c : in){ if(!std::ispunct(static_cast<unsigned char>(c))){ out.push_back(static_cast<char>(std::tolower(static_cast<unsigned char>(c)))); } }
					return out;
				};
				const std::string normTrans = normalize(transcriptionResult);
				const std::string normWake = normalize(_wakeCommand);
				const auto transWords = getWords(normTrans);
				const auto wakeWords = getWords(normWake);
				float bestSim = -1.0f;
				size_t bestStart = 0;
				size_t bestEnd = 0;
				for(size_t start = 0; start < transWords.size(); ++start){
					const size_t maxEnd = std::min(transWords.size(), start + wakeWords.size() + static_cast<size_t>(2));
					for(size_t end = start + 1; end <= maxEnd; ++end){
						const std::string probe = joinRange(transWords, start, end);
						const float sim = Similarity(probe, normWake);
						if(sim > bestSim){
							bestSim = sim;
							bestStart = start;
							bestEnd = end;
						}
					}
				}
				const std::string bestPhrase = joinRange(transWords, bestStart, bestEnd);
				{
					std::string msg = "Wake word probe: \"" + bestPhrase + "\" sim=" + std::to_string(bestSim) + " threshold=" + std::to_string(_wakeWordSim) + "\n";
					OutputDebugStringA(msg.c_str());
				}
				if(bestSim < _wakeWordSim){
					OutputDebugStringA("Wake word not detected\n");
					continue;
				}
				OutputDebugStringA("Wake word detected\n");
				const auto origWords = getWords(transcriptionResult);
				if(bestEnd <= origWords.size()){ transcriptionResult = joinRange(origWords, bestEnd, origWords.size()); } else{ transcriptionResult.clear(); }
				_wakeWordDetected.store(true);
			} else if(_wakeCommand.empty()) _wakeWordDetected.store(true);
			if(_wakeWordDetected.load()){
				if(_committed.empty()) trimWakeWord(transcriptionResult);
				pending = transcriptionResult;
				std::string combined = _committed + pending;
				if(combined != lastOutput){
					lastOutput = combined;
					if(_whisperCallback) _whisperCallback(combined.c_str());
				}
			}
		}
		_audioCapture->pause();
	});
	return true;
}
void StopSpeechTranscription(){
	_transcriptionRunning.store(false);
	if(_transcriptionThread.joinable()){ _transcriptionThread.join(); }
	_wakeWordDetected.store(false);
	std::lock_guard<std::mutex> lock(_committedMutex);
	_committed.clear();
}
void SetWhisperCallback(const WhisperCallbackFn cb){ _whisperCallback = cb; }
void SetSpeechEndCallback(const SpeechEndCallbackFn cb){ _speechEndCallback = cb; }
void SetWakeCommand(const char* wakeCmd){ if(wakeCmd){ _wakeCommand = wakeCmd; } else{ _wakeCommand.clear(); } }
void SetVADThresholds(const float vad, const float freq){
	_wparams.vad_params.threshold = _vadThreshold = vad;
	_freqThreshold = freq;
}
void SetWakeWordSimilarity(const float similarity){ _wakeWordSim = similarity; }
void SetWhisperTemp(const float temp){ _temp = temp; }
void SetSilenceTimeout(const int ms){ _silenceTimeoutMs.store(ms); }
void SetCommittedText(const char* text){
	std::lock_guard<std::mutex> lock(_committedMutex);
	_committed = text;
}
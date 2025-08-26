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
static bool _gpuOomSpeech = false;
static std::atomic<bool> _wakeWordDetected{false};
static void GPUOomLogCallbackSpeech(ggml_log_level level, const char* text, void* userData){
	if(level == GGML_LOG_LEVEL_ERROR || level == GGML_LOG_LEVEL_WARN){
		const std::string_view msg(text);
		if(msg.find("out of memory") != std::string_view::npos) _gpuOomSpeech = true;
	}
}
StudError LoadWhisperModel(const char* modelPath, const int nThreads, const bool useGPU, const bool useVAD, const char* vadModel){
	UnloadWhisperModel();
	whisper_context_params cparams = whisper_context_default_params();
	cparams.use_gpu = useGPU;
	_gpuOomSpeech = false;
	whisper_log_set(GPUOomLogCallbackSpeech, nullptr);
	_whisperCtx = whisper_init_from_file_with_params(modelPath, cparams);
	whisper_log_set(nullptr, nullptr);
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
bool VadSimple(std::vector<float>& pcmf32, const int sampleRate, const int lastMs){
	const int nSamples = pcmf32.size();
	const int nSamplesLast = sampleRate*lastMs / 1000;
	if(nSamplesLast >= nSamples){ return false; }
	if(_freqThreshold > 0.0f){ HighPassFilter(pcmf32, _freqThreshold, sampleRate); }
	float energyAll = 0.0f;
	float energyLast = 0.0f;
	for(int i = 0; i < nSamples; i++){
		energyAll += fabsf(pcmf32[i]);
		if(i >= nSamples - nSamplesLast){ energyLast += fabsf(pcmf32[i]); }
	}
	energyAll /= nSamples;
	energyLast /= nSamplesLast;
	if(energyLast > _vadThreshold*energyAll){ return false; }
	return true;
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
	if(!_whisperCtx || !_audioCapture){ return false; }
	_wakeWordDetected.store(false);
	_transcriptionRunning.store(true);
	_transcriptionThread = std::thread([]{
		const int stepMs = 2000;
		const int lengthMs = _voiceDurationMS;
		const int keepMs = 200;
		const int nSamplesStep = 1e-3*stepMs*WHISPER_SAMPLE_RATE;
		const int nSamplesLen = 1e-3*lengthMs*WHISPER_SAMPLE_RATE;
		const int nSamplesKeep = 1e-3*keepMs*WHISPER_SAMPLE_RATE;
		std::vector<float> pcmf32(nSamplesLen, 0.0f);
		std::vector<float> pcmf32Old;
		std::vector<float> pcmf32New;
		std::vector<whisper_token> promptTokens;
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
		std::string lastOutput;
		bool speaking = false;
		auto lastSpeech = std::chrono::steady_clock::now();
		_audioCapture->clear();
		_audioCapture->resume();
		while(_transcriptionRunning.load()){
			while(_transcriptionRunning.load()){
				_audioCapture->get(stepMs, pcmf32New);
				if(static_cast<int>(pcmf32New.size()) > 2*nSamplesStep){
					_audioCapture->clear();
					continue;
				}
				if(static_cast<int>(pcmf32New.size()) >= nSamplesStep){
					_audioCapture->clear();
					break;
				}
				std::this_thread::sleep_for(std::chrono::milliseconds(1));
			}
			if(!_transcriptionRunning.load()) break;
			bool hasSpeech = false;
			if(_vadCtx){
				if(whisper_vad_detect_speech(_vadCtx, pcmf32New.data(), pcmf32New.size())){
					const int nProbs = whisper_vad_n_probs(_vadCtx);
					const float* probs = whisper_vad_probs(_vadCtx);
					for(int i = 0; i < nProbs; ++i){
						if(probs[i] > _wparams.vad_params.threshold){
							hasSpeech = true;
							break;
						}
					}
				}
			} else{ hasSpeech = !VadSimple(pcmf32New, WHISPER_SAMPLE_RATE, 1250); }
			auto now = std::chrono::steady_clock::now();
			if(!hasSpeech){
				if(speaking && now - lastSpeech > std::chrono::milliseconds(_silenceTimeoutMs.load())){
					speaking = false;
					lastOutput.clear();
					pcmf32Old.clear();
					promptTokens.clear();
					const bool wasWake = _wakeWordDetected.exchange(false);
					if(wasWake && _speechEndCallback) _speechEndCallback();
				}
				continue;
			}
			speaking = true;
			lastSpeech = now;
			const int nSamplesNew = pcmf32New.size();
			const int nSamplesTake = std::min(static_cast<int>(pcmf32Old.size()), std::max(0, nSamplesKeep + nSamplesLen - nSamplesNew));
			pcmf32.resize(nSamplesNew + nSamplesTake);
			for(int i = 0; i < nSamplesTake; ++i){ pcmf32[i] = pcmf32Old[pcmf32Old.size() - nSamplesTake + i]; }
			memcpy(pcmf32.data() + nSamplesTake, pcmf32New.data(), nSamplesNew*sizeof(float));
			pcmf32Old = pcmf32;
			_wparams.prompt_tokens = promptTokens.data();
			_wparams.prompt_n_tokens = promptTokens.size();
			if(whisper_full(_whisperCtx, _wparams, pcmf32.data(), static_cast<int>(pcmf32.size())) != 0){ continue; }
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
					for(char c : in){ if(!std::ispunct(static_cast<unsigned char>(c))){ out.push_back(static_cast<char>(std::tolower(static_cast<unsigned char>(c)))); } }
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
					pcmf32Old.clear();
					continue;
				}
				OutputDebugStringA("Wake word detected\n");
				const auto origWords = getWords(transcriptionResult);
				if(bestEnd <= origWords.size()){ transcriptionResult = joinRange(origWords, bestEnd, origWords.size()); } else{ transcriptionResult.clear(); }
				_wakeWordDetected.store(true);
			}
			std::string newText = transcriptionResult;
			if(!lastOutput.empty() && transcriptionResult.rfind(lastOutput, 0) == 0){ newText = transcriptionResult.substr(lastOutput.size()); }
			if(!newText.empty()){
				lastOutput = transcriptionResult;
				if(_whisperCallback) _whisperCallback(newText.c_str());
			}
			pcmf32Old = std::vector<float>(pcmf32.end() - std::min(nSamplesKeep, static_cast<int>(pcmf32.size())), pcmf32.end());
			promptTokens.clear();
			for(int i = 0; i < nSegs; ++i){
				const int tokenCount = whisper_full_n_tokens(_whisperCtx, i);
				for(int j = 0; j < tokenCount; ++j){ promptTokens.push_back(whisper_full_get_token_id(_whisperCtx, i, j)); }
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
}
void SetWhisperCallback(WhisperCallbackFn cb){ _whisperCallback = cb; }
void SetSpeechEndCallback(SpeechEndCallbackFn cb){ _speechEndCallback = cb; }
void SetWakeCommand(const char* wakeCmd){ if(wakeCmd){ _wakeCommand = wakeCmd; } else{ _wakeCommand.clear(); } }
void SetVADThresholds(const float vad, const float freq){
	_vadThreshold = vad;
	_freqThreshold = freq;
}
void SetVoiceDuration(const int voiceDuration){ _voiceDurationMS = voiceDuration; }
void SetWakeWordSimilarity(float similarity){ _wakeWordSim = similarity; }
void SetWhisperTemp(float temp){ _temp = temp; }
void SetSilenceTimeout(int ms){ _silenceTimeoutMs.store(ms); }
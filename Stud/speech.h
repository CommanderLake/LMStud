#pragma once
#define SDL_MAIN_HANDLED
#include "common-sdl.h"
#include <whisper.h>
#include <memory>
#include <atomic>
#include <thread>
#include <mutex>
#include <condition_variable>
#include <chrono>
#include <functional>
#pragma comment(lib, "winmm.lib")
#pragma comment(lib, "imm32.lib")
#pragma comment(lib, "version.lib")
#pragma comment(lib, "setupapi.lib")
#pragma comment(lib, "cfgmgr32.lib")
#ifdef NDEBUG
#pragma comment(lib, "manual-link\\SDL2main.lib")
#pragma comment(lib, "SDL2-static.lib")
#else
#pragma comment(lib, "manual-link\\SDL2maind.lib")
#pragma comment(lib, "SDL2-staticd.lib")
#endif
#pragma comment(lib, "whisper.lib")
#define EXPORT __declspec(dllexport)
// Constants
constexpr int DEFAULT_VOICE_DURATION = 60000;
constexpr float DEFAULT_VAD_THRESHOLD = 0.6f;
constexpr float DEFAULT_FREQ_THRESHOLD = 100.0f;
constexpr float DEFAULT_WAKE_WORD_THRESHOLD = 0.8f;
constexpr int DEFAULT_SAMPLE_RATE = WHISPER_SAMPLE_RATE;
constexpr int VAD_CHECK_DURATION = 2000;
constexpr int VAD_LAST_MS = 1250;
constexpr int THREAD_SLEEP_MS = 100;
constexpr int MAX_TRANSCRIPTION_TIMEOUT_S = 30;
using WhisperCallbackFn = void(*)(const char* transcription);
using LogCallbackFn = void(*)(const char* message);
// High-pass filter with state
class HighPassFilter{
	float _y = 0.0f;
	bool _initialized = false;
	float _lastInput = 0.0f;
public:
	void reset(){
		_y = 0.0f;
		_initialized = false;
		_lastInput = 0.0f;
	}
	void process(std::vector<float>& data, float cutoff, float sampleRate){
		if(data.empty()) return;
		const float rc = 1.0f / (2.0f * M_PI * cutoff);
		const float dt = 1.0f / sampleRate;
		const float alpha = dt / (rc + dt);
		if(!_initialized){
			_y = data[0];
			_lastInput = data[0];
			_initialized = true;
		}
		for(size_t i = 0; i < data.size(); i++){
			const float currentInput = data[i];
			_y = alpha * (_y + currentInput - _lastInput);
			data[i] = _y;
			_lastInput = currentInput;
		}
	}
};
// Thread safe speech recognizer class
class SpeechInput{
	// Thread safety
	std::mutex _configMutex;
	std::mutex _callbackMutex;
	std::atomic<bool> _transcriptionRunning{false};
	std::thread _transcriptionThread;
	std::condition_variable _configChanged;
	// Whisper context and parameters
	std::unique_ptr<whisper_context, decltype(&whisper_free)> _whisperCtx{nullptr, whisper_free};
	whisper_full_params _wparams{};
	// Audio capture
	std::unique_ptr<audio_async> _audioCapture;
	// Configuration (atomic for lock-free access)
	std::atomic<int> _voiceDuration{DEFAULT_VOICE_DURATION};
	std::atomic<float> _vadThreshold{DEFAULT_VAD_THRESHOLD};
	std::atomic<float> _freqThreshold{DEFAULT_FREQ_THRESHOLD};
	std::atomic<float> _wakeWordThreshold{DEFAULT_WAKE_WORD_THRESHOLD};
	// Thread-safe string configuration
	std::string _wakeCommand;
	// Callbacks
	WhisperCallbackFn _whisperCallback = nullptr;
	LogCallbackFn _logCallback = nullptr;
	// Audio processing
	HighPassFilter _highPassFilter;
	// Helper methods
	void log(const std::string& message);
	bool vadSimple(std::vector<float>& pcmf32, int sampleRate, int lastMs, float vadThold, float freqThold);
	void normalizeAudio(std::vector<float>& data);
	float calculateSimilarity(const std::string& s0, const std::string& s1);
	std::vector<std::string> getWords(const std::string& s);
	void transcriptionLoop();
	std::string getWakeCommand();
public:
	SpeechInput() = default;
	~SpeechInput();
	// Non-copyable, non-movable
	SpeechInput(const SpeechInput&) = delete;
	SpeechInput& operator=(const SpeechInput&) = delete;
	SpeechInput(SpeechInput&&) = delete;
	SpeechInput& operator=(SpeechInput&&) = delete;
	bool loadModel(const char* modelPath, int nThreads, bool useGPU);
	void unloadModel();
	bool startTranscription();
	void stopTranscription();
	void setCallback(WhisperCallbackFn cb);
	void setLogCallback(LogCallbackFn cb);
	void setWakeCommand(const char* wakeCmd);
	void setVADThresholds(float vadThreshold, float freqThreshold);
	void setWakeWordThreshold(float threshold);
	void setVoiceDuration(int voiceDuration);
	bool isRunning() const{ return _transcriptionRunning.load(); }
};
// Global instance
inline std::unique_ptr<SpeechInput> g_speechRecognizer;
// C API
extern "C" {
EXPORT void SetWhisperCallback(WhisperCallbackFn cb);
EXPORT void SetLogCallback(LogCallbackFn cb);
EXPORT bool LoadWhisperModel(const char* modelPath, int nThreads, bool useGPU);
EXPORT bool StartSpeechTranscription();
EXPORT void StopSpeechTranscription();
EXPORT void UnloadWhisperModel();
EXPORT void SetWakeCommand(const char* wakeCmd);
EXPORT void SetVADThresholds(float vadThreshold, float freqThreshold);
EXPORT void SetWakeWordThreshold(float threshold);
EXPORT void SetVoiceDuration(int voiceDuration);
EXPORT bool IsTranscriptionRunning();
}
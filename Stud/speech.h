#pragma once
#define SDL_MAIN_HANDLED
#include "common-sdl.h"
#include <whisper.h>
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
inline int _gVoiceDuration = 60000;
inline std::atomic<bool> _transcriptionRunning(false);
inline std::thread _transcriptionThread;
typedef void(*WhisperCallbackFn)(const char* transcription);
inline WhisperCallbackFn _whisperCallback = nullptr;
inline whisper_context* _whisperCtx = nullptr;
inline whisper_full_params _wparams = {};
inline audio_async* _audioCapture = nullptr;
inline std::string _gWakeCommand = "";
inline float _gVadThreshold = 0.5f;
inline float _gFreqThreshold = 100.0f;
extern "C"{
	EXPORT void SetWhisperCallback(WhisperCallbackFn cb);
	EXPORT bool LoadWhisperModel(const char* modelPath, int nThreads, bool useGPU);
	EXPORT bool StartSpeechTranscription();
	EXPORT void StopSpeechTranscription();
	EXPORT void UnloadWhisperModel();
	EXPORT void SetWakeCommand(const char* wakeCmd);
	EXPORT void SetVADThresholds(float vadThreshold, float freqThreshold);
	EXPORT inline void SetVoiceDuration(const int voiceDuration){
		_gVoiceDuration = voiceDuration;
	}
}
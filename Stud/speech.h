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
inline int gVoiceDuration = 10000;
inline std::atomic<bool> transcriptionRunning(false);
inline std::thread transcriptionThread;
typedef void(*WhisperCallbackFn)(const char* transcription);
inline WhisperCallbackFn whisperCallback = nullptr;
inline whisper_context* whisperCtx = nullptr;
inline whisper_full_params wparams = {};
inline audio_async* audioCapture = nullptr;
inline std::string gWakeCommand = "";
inline float gVadThreshold = 0.8f;
inline float gFreqThreshold = 80.0f;
extern "C"{
	EXPORT void SetWhisperCallback(WhisperCallbackFn cb);
	EXPORT bool LoadWhisperModel(const char* modelPath, int nThreads, bool useGPU);
	EXPORT bool StartSpeechTranscription();
	EXPORT void StopSpeechTranscription();
	EXPORT void UnloadWhisperModel();
	EXPORT void SetWakeCommand(const char* wakeCmd);
	EXPORT void SetVADThresholds(float vadThreshold, float freqThreshold);
	EXPORT inline void SetVoiceDuration(const int ms){
		gVoiceDuration = ms;
	}
}
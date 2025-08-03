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
inline int _voiceDuration = 10000;
inline std::string _wakeCommand = "";
inline float _vadThreshold = 0.5f;
inline float _freqThreshold = 100.0f;
inline float _wakeWordSim = 0.8f;
inline float _temp = 0.2f;
inline std::string _vadModel;
inline std::atomic<bool> _transcriptionRunning(false);
inline std::thread _transcriptionThread;
typedef void(*WhisperCallbackFn)(const char* transcription);
inline WhisperCallbackFn _whisperCallback = nullptr;
inline whisper_context* _whisperCtx = nullptr;
inline whisper_full_params _wparams = {};
inline whisper_vad_context* _vadCtx = nullptr;
inline whisper_vad_context_params _vadParams = {};
inline audio_async* _audioCapture = nullptr;
extern "C"{
	EXPORT void SetWhisperCallback(WhisperCallbackFn cb);
	EXPORT bool LoadWhisperModel(const char* modelPath, int nThreads, bool useGPU, bool useVAD, const char* vadModel);
	EXPORT bool StartSpeechTranscription();
	EXPORT void StopSpeechTranscription();
	EXPORT void UnloadWhisperModel();
	EXPORT void SetWakeCommand(const char* wakeCmd);
	EXPORT void SetVADThresholds(float vad, float freq);
	EXPORT void SetVoiceDuration(int voiceDuration);
	EXPORT void SetWakeWordSimilarity(float similarity);
	EXPORT void SetWhisperTemp(float temp);
}
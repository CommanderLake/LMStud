#pragma once
#include "stud.h"
#include "common-sdl.h"
#include <whisper.h>
inline int gVoiceDuration = 5000;
inline std::atomic<bool> transcriptionRunning(false);
inline std::thread transcriptionThread;
typedef void(*WhisperCallbackFn)(const char* transcription);
static WhisperCallbackFn whisperCallback = nullptr;
inline whisper_context* whisperCtx = nullptr;
inline whisper_full_params wparams = {};
inline audio_async* audioCapture = nullptr;
inline std::string gWakeCommand = "";
inline float gVadThreshold = 0.5f;
inline float gFreqThreshold = 100.0f;
extern "C"{
	EXPORT void SetWhisperCallback(WhisperCallbackFn cb);
	EXPORT bool LoadWhisperModel(const char* modelPath, const char* language, int nThreads, bool useGPU);
	EXPORT bool StartSpeechTranscription();
	EXPORT void StopSpeechTranscription();
	EXPORT void UnloadWhisperModel();
	EXPORT void SetWakeCommand(const char* wakeCmd);
	EXPORT void SetVADThresholds(float vadThreshold, float freqThreshold);
	EXPORT inline void SetVoiceDuration(const int ms){
		gVoiceDuration = ms;
	}
}
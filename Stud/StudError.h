#pragma once
enum class StudError{
	Success = 0,
	CantLoadModel = -1,
	ModelNotLoaded = -2,
	CantCreateContext = -3,
	CantCreateSampler = -4,
	CantApplyTemplate = -5,
	ConvTooLong = -6,
	LlamaDecodeError = -7,
	IndexOutOfRange = -8,
	CantTokenizePrompt = -9,
	CantConvertToken = -10,
	ChatParseError = -11,
	GpuOutOfMemory = -12,
	CantLoadWhisperModel = -13,
	CantLoadVADModel = -14,
	CantInitAudioCapture = -15,
	Generic = -16
};
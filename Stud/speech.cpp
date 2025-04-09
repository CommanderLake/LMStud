#define _USE_MATH_DEFINES
#include "speech.h"
#include <thread>
#include <atomic>
#include <vector>
#include <cstring>
#include <string>
#include <regex>
static std::string trim(const std::string& s){
	const size_t start = s.find_first_not_of(" \t\n\r");
	if(start==std::string::npos) return "";
	const size_t end = s.find_last_not_of(" \t\n\r");
	return s.substr(start, end-start+1);
}
bool LoadWhisperModel(const char* modelPath, const char* language, const int nThreads, const bool useGPU){
	whisper_context_params cparams = whisper_context_default_params();
	cparams.use_gpu = useGPU;
	whisperCtx = whisper_init_from_file_with_params(modelPath, cparams);
	if(!whisperCtx){ return false; }
	wparams = whisper_full_default_params(WHISPER_SAMPLING_GREEDY);
	wparams.print_realtime = false;
	wparams.single_segment = true;
	if(!whisper_is_multilingual(whisperCtx)){
		wparams.language = "en";
		wparams.translate = false;
	} else{ if(language&&std::strlen(language)>0){ wparams.language = language; } }
	wparams.n_threads = nThreads;
	audioCapture = new audio_async(30000);
	if(!audioCapture->init(-1, WHISPER_SAMPLE_RATE)){ return false; }
	audioCapture->resume();
	return true;
}
void HighPassFilter(std::vector<float>& data, const float cutoff, const float sampleRate){
	const float rc = 1.0f/(2.0f*M_PI*cutoff);
	const float dt = 1.0f/sampleRate;
	const float alpha = dt/(rc+dt);
	float y = data[0];
	for(size_t i = 1; i<data.size(); i++){
		y = alpha*(y+data[i]-data[i-1]);
		data[i] = y;
	}
}
bool VadSimple(std::vector<float>& pcmf32, const int sampleRate, const int lastMs, const float vadThold, const float freqThold){
	const int nSamples = pcmf32.size();
	const int nSamplesLast = (sampleRate*lastMs)/1000;
	if(nSamplesLast>=nSamples){ return false; }
	if(freqThold>0.0f){ HighPassFilter(pcmf32, freqThold, sampleRate); }
	float energyAll = 0.0f;
	float energyLast = 0.0f;
	for(int i = 0; i<nSamples; i++){
		energyAll += fabsf(pcmf32[i]);
		if(i>=nSamples-nSamplesLast){ energyLast += fabsf(pcmf32[i]); }
	}
	energyAll /= nSamples;
	energyLast /= nSamplesLast;
	if(energyLast>vadThold*energyAll){ return false; }
	return true;
}
float similarity(const std::string& s0, const std::string& s1){
	const size_t len0 = s0.size()+1;
	const size_t len1 = s1.size()+1;
	std::vector<int> col(len1, 0);
	std::vector<int> prevCol(len1, 0);
	for(size_t i = 0; i<len1; i++){ prevCol[i] = i; }
	for(size_t i = 0; i<len0; i++){
		col[0] = i;
		for(size_t j = 1; j<len1; j++){ col[j] = std::min(std::min(1+col[j-1], 1+prevCol[j]), prevCol[j-1]+(i>0&&s0[i-1]==s1[j-1] ? 0 : 1)); }
		col.swap(prevCol);
	}
	const float dist = prevCol[len1-1];
	return 1.0f-(dist/std::max(s0.size(), s1.size()));
}
bool StartSpeechTranscription(){
	if(!whisperCtx||!audioCapture){ return false; }
	transcriptionRunning.store(true);
	transcriptionThread = std::thread([](){
		constexpr int nShortSamples = WHISPER_SAMPLE_RATE*2;
		std::vector<float> pcmDataShort(nShortSamples, 0.0f);
		const int nLongSamples = (WHISPER_SAMPLE_RATE*gVoiceDuration)/1000;
		std::vector<float> pcmDataLong(nLongSamples, 0.0f);
		// Helper lambda to split a string into words.
		auto get_words = [](const std::string& s) -> std::vector<std::string>{
			std::istringstream iss(s);
			std::vector<std::string> words;
			std::string word;
			while(iss>>word){ words.push_back(word); }
			return words;
		};
		// static variable to track the last output and avoid duplicates.
		std::string lastOutput;
		while(transcriptionRunning.load()){
			std::this_thread::sleep_for(std::chrono::milliseconds(100));
			audioCapture->get(2000, pcmDataShort);
			if(!VadSimple(pcmDataShort, WHISPER_SAMPLE_RATE, 1250, gVadThreshold, gFreqThreshold)){ continue; }
			audioCapture->get(gVoiceDuration, pcmDataLong);
			if(whisper_full(whisperCtx, wparams, pcmDataLong.data(), static_cast<int>(pcmDataLong.size()))!=0){ continue; }
			std::string transcriptionResult;
			int nSegs = whisper_full_n_segments(whisperCtx);
			if(nSegs>0){
				// Use the final segment only to avoid concatenating older segments.
				transcriptionResult = std::string(whisper_full_get_segment_text(whisperCtx, nSegs-1));
			}
			transcriptionResult = trim(transcriptionResult);
			{
				// Remove bracketed or parenthesized text.
				std::regex reBracket("\\[.*?\\]");
				transcriptionResult = std::regex_replace(transcriptionResult, reBracket, "");
				std::regex reParen("\\(.*?\\)");
				transcriptionResult = std::regex_replace(transcriptionResult, reParen, "");
				transcriptionResult = trim(transcriptionResult);
			}
			// If a wake command is specified, process the transcription accordingly.
			if(!gWakeCommand.empty()){
				auto wakeWords = get_words(gWakeCommand);
				int wakeWordCount = wakeWords.size();
				auto transWords = get_words(transcriptionResult);
				if(transWords.size()<static_cast<size_t>(wakeWordCount)){ continue; }
				std::string heardWake;
				std::string remaining;
				for(size_t i = 0; i<transWords.size(); i++){ if(i<static_cast<size_t>(wakeWordCount)){ heardWake += transWords[i]+" "; } else{ remaining += transWords[i]+" "; } }
				heardWake = trim(heardWake);
				remaining = trim(remaining);
				float sim = similarity(heardWake, gWakeCommand);
				// Only proceed if similarity is high enough and some text remains.
				if(sim<0.5f||remaining.empty()){ continue; }
				transcriptionResult = remaining;
			}
			// Only trigger the callback if the new transcription is non-empty and different from the last output.
			if(whisperCallback&&!transcriptionResult.empty()&&transcriptionResult!=lastOutput){
				lastOutput = transcriptionResult;
				whisperCallback(transcriptionResult.c_str());
				// Clear the audio buffer to avoid reprocessing the same utterance.
				audioCapture->clear();
			}
		}
	});
	return true;
}
void StopSpeechTranscription(){
	transcriptionRunning.store(false);
	if(transcriptionThread.joinable()){ transcriptionThread.join(); }
}
void UnloadWhisperModel(){
	if(audioCapture){
		audioCapture->pause();
		delete audioCapture;
		audioCapture = nullptr;
	}
	if(whisperCtx){
		whisper_free(whisperCtx);
		whisperCtx = nullptr;
	}
}
void SetWhisperCallback(WhisperCallbackFn cb){ whisperCallback = cb; }
void SetWakeCommand(const char* wakeCmd){ if(wakeCmd){ gWakeCommand = wakeCmd; } else{ gWakeCommand.clear(); } }
void SetVADThresholds(const float vadThreshold, const float freqThreshold){
	gVadThreshold = vadThreshold;
	gFreqThreshold = freqThreshold;
}
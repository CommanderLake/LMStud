#include "StudInternal.h"
#include <algorithm>
#include <cctype>
#include <cstdlib>
#include <mtmd-helper.h>
#pragma comment(lib, "llama.lib")
#pragma comment(lib, "llama-common.lib")
#pragma comment(lib, "ggml.lib")
#pragma comment(lib, "ggml-base.lib")
#pragma comment(lib, "mtmd.lib")
static bool gpuOomDetected = false;
static std::string lastErrorMessage;
namespace Stud::Internal{
	std::string& LastErrorMessage(){ return lastErrorMessage; }
	bool& GpuOomDetected(){ return gpuOomDetected; }
	void AppendLastErrorLogMessage(std::string_view message){
		while(!message.empty() && (message.back() == '\r' || message.back() == '\n')) message.remove_suffix(1);
		if(message.empty()) return;
		if(!lastErrorMessage.empty()) lastErrorMessage += "\r\n";
		lastErrorMessage.append(message.data(), message.size());
	}
	void BackendLogCallback(const ggml_log_level level, const char* text, void* userData){
		(void)userData;
		if(level == GGML_LOG_LEVEL_ERROR || level == GGML_LOG_LEVEL_WARN){
			const std::string_view message(text ? text : "");
			if(message.find("out of memory") != std::string_view::npos || message.find("cudaMalloc failed") != std::string_view::npos) gpuOomDetected = true;
		}
		if(level == GGML_LOG_LEVEL_ERROR) AppendLastErrorLogMessage(text ? text : "");
	}
	ScopedBackendErrorCapture::ScopedBackendErrorCapture() : _logLock(llamaLogMutex){
		mtmd_helper_log_set(BackendLogCallback, nullptr);
		llama_log_set(BackendLogCallback, nullptr);
	}
	ScopedBackendErrorCapture::~ScopedBackendErrorCapture(){
		llama_log_set(nullptr, nullptr);
		mtmd_helper_log_set(nullptr, nullptr);
	}
	std::string NormalizeSlotName(const char* slotName){
		std::string name = slotName ? slotName : "";
		name.erase(name.begin(), std::find_if(name.begin(), name.end(), [](unsigned char ch){ return !std::isspace(ch); }));
		name.erase(std::find_if(name.rbegin(), name.rend(), [](unsigned char ch){ return !std::isspace(ch); }).base(), name.end());
		if(name.empty()) name = "main";
		std::transform(name.begin(), name.end(), name.begin(), [](unsigned char ch){ return static_cast<char>(std::tolower(ch)); });
		return name;
	}
	StudModel* FindModel(const std::string& slotName){
		std::lock_guard<std::mutex> lock(modelsMutex);
		const auto it = models.find(slotName);
		return it == models.end() ? nullptr : it->second.get();
	}
	static StudModel& GetOrCreateModel(const std::string& slotName){
		std::lock_guard<std::mutex> lock(modelsMutex);
		auto& model = models[slotName];
		if(!model){
			model = std::make_unique<StudModel>();
			model->slotName = slotName;
		}
		return *model;
	}
}
extern "C" EXPORT const char* GetLastErrorMessage(){ return Stud::Internal::LastErrorMessage().c_str(); }
extern "C" EXPORT void ClearLastErrorMessage(){ Stud::Internal::LastErrorMessage().clear(); }
void SetHWnd(const HWND hWnd){ Stud::hWnd = hWnd; }
void BackendInit(){
	_putenv("OMP_PROC_BIND=close");
	ggml_backend_load("ggml-cpu.dll");
	const HMODULE hModule = LoadLibraryA("nvcuda.dll");
	if(hModule != nullptr) ggml_backend_load("ggml-cuda.dll");
	llama_backend_init();
}
Stud::StudModel* GetModel(const char* slotName){ return &Stud::Internal::GetOrCreateModel(Stud::Internal::NormalizeSlotName(slotName)); }
void SetTokenCallback(const Stud::TokenCallbackFn callback){ Stud::tokenCb = callback; }
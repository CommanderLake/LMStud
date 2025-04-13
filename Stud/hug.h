#pragma once
#define EXPORT __declspec(dllexport)
#include <curl\system.h>
typedef int(*NativeProgressCallback)(curl_off_t /*dltotal*/, curl_off_t /*dlnow*/);
extern "C"{
	EXPORT char* PerformHttpGet(const char* url);
	EXPORT int DownloadFile(const char* url, const char* targetPath);
	EXPORT void FreeMemory(char* ptr);
	EXPORT void CurlGlobalInit();
	EXPORT void CurlGlobalCleanup();
	EXPORT int DownloadFileWithProgress(const char* url, const char* targetPath, NativeProgressCallback progressCallback);
}
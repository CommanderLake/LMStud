#pragma once
#define EXPORT __declspec(dllexport)
#include <string>
#include <curl\system.h>
#pragma comment(lib, "Ws2_32.lib")
#pragma comment(lib, "Crypt32.lib")
#pragma comment(lib, "libcrypto.lib")
#pragma comment(lib, "libssl.lib")
#ifdef NDEBUG
#pragma comment(lib, "zlib.lib")
#pragma comment(lib, "libcurl.lib")
#else
#pragma comment(lib, "zlibd.lib")
#pragma comment(lib, "libcurl-d.lib")
#endif
typedef int(*NativeProgressCallback)(curl_off_t /*dltotal*/, curl_off_t /*dlnow*/);
extern "C"{
	EXPORT const char* PerformHttpGet(const char* url);
	EXPORT int DownloadFile(const char* url, const char* targetPath);
	EXPORT void FreeMemory(char* ptr);
	EXPORT void CurlGlobalInit();
	EXPORT void CurlGlobalCleanup();
	EXPORT int DownloadFileWithProgress(const char* url, const char* targetPath, NativeProgressCallback progressCallback);
}
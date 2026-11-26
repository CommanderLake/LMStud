#define CURL_STATICLIB
#include "hug.h"
#include <curl\curl.h>
#include <stdexcept>
#include <string>
#include <vector>
#include <Windows.h>
#include <winhttp.h>
#pragma comment(lib, "winhttp.lib")
static std::wstring Utf8ToWide(const std::string& utf8Str){
	if(utf8Str.empty()){ return std::wstring(); }
	const int size_needed = MultiByteToWideChar(CP_UTF8, 0, &utf8Str[0], static_cast<int>(utf8Str.size()), nullptr, 0);
	std::wstring wstrTo(size_needed, 0);
	MultiByteToWideChar(CP_UTF8, 0, &utf8Str[0], static_cast<int>(utf8Str.size()), &wstrTo[0], size_needed);
	return wstrTo;
}
static bool ResolveSystemProxyForUrl(const char* url, std::string& outProxy){
	WINHTTP_CURRENT_USER_IE_PROXY_CONFIG ie = {0};
	if(!WinHttpGetIEProxyConfigForCurrentUser(&ie)){ return false; }
	BOOL ok = FALSE;
	WINHTTP_PROXY_INFO pi = {0};
	const bool has_auto_proxy = ie.fAutoDetect || ie.lpszAutoConfigUrl;
	if(has_auto_proxy){
		const HINTERNET hSession = WinHttpOpen(L"LMStud/1.0", WINHTTP_ACCESS_TYPE_NO_PROXY, nullptr, nullptr, 0);
		if(hSession){
			WINHTTP_AUTOPROXY_OPTIONS ao = {0};
			ao.dwFlags = (ie.fAutoDetect ? WINHTTP_AUTOPROXY_AUTO_DETECT : 0) | (ie.lpszAutoConfigUrl ? WINHTTP_AUTOPROXY_CONFIG_URL : 0);
			ao.dwAutoDetectFlags = WINHTTP_AUTO_DETECT_TYPE_DHCP | WINHTTP_AUTO_DETECT_TYPE_DNS_A;
			ao.lpszAutoConfigUrl = ie.lpszAutoConfigUrl;
			const std::wstring w_url = Utf8ToWide(url);
			ok = WinHttpGetProxyForUrl(hSession, w_url.c_str(), &ao, &pi);
			WinHttpCloseHandle(hSession);
			if(ok && (!pi.lpszProxy || !pi.lpszProxy[0])){
				if(pi.lpszProxy){ GlobalFree(pi.lpszProxy); pi.lpszProxy = nullptr; }
				if(pi.lpszProxyBypass){ GlobalFree(pi.lpszProxyBypass); pi.lpszProxyBypass = nullptr; }
				ok = FALSE;
			}
		}
	}
	if(!ok && ie.lpszProxy){
		if(pi.lpszProxy){ GlobalFree(pi.lpszProxy); pi.lpszProxy = nullptr; }
		if(pi.lpszProxyBypass){ GlobalFree(pi.lpszProxyBypass); pi.lpszProxyBypass = nullptr; }
		pi.dwAccessType = WINHTTP_ACCESS_TYPE_NAMED_PROXY;
		pi.lpszProxy = ie.lpszProxy;
		ie.lpszProxy = nullptr;
		ok = TRUE;
	}
	if(ie.lpszAutoConfigUrl) GlobalFree(ie.lpszAutoConfigUrl);
	if(ie.lpszProxy) GlobalFree(ie.lpszProxy);
	if(ie.lpszProxyBypass) GlobalFree(ie.lpszProxyBypass);
	if(pi.lpszProxy) GlobalFree(pi.lpszProxy);
	if(pi.lpszProxyBypass) GlobalFree(pi.lpszProxyBypass);
	return ok && !outProxy.empty();
}
static size_t WriteCallback(void* ptr, size_t size, size_t nmemb, void* userdata){
	const auto response = static_cast<std::string*>(userdata);
	response->append(static_cast<char*>(ptr), size * nmemb);
	return size * nmemb;
}
void FreeMemory(char* ptr){ if(ptr) std::free(ptr); }
static CURLcode PerformWithRetry(CURL* curl, const char* url){
	std::string proxy;
	bool proxy_was_set = false;
	if(ResolveSystemProxyForUrl(url, proxy)){
		curl_easy_setopt(curl, CURLOPT_PROXY, proxy.c_str());
		curl_easy_setopt(curl, CURLOPT_PROXYAUTH, CURLAUTH_NEGOTIATE | CURLAUTH_NTLM | CURLAUTH_BASIC);
		curl_easy_setopt(curl, CURLOPT_PROXYUSERPWD, ":");
		proxy_was_set = true;
	}
	CURLcode res = curl_easy_perform(curl);
	if(proxy_was_set && (res == CURLE_COULDNT_RESOLVE_PROXY || res == CURLE_COULDNT_CONNECT)){
		const CURLcode original_proxy_error = res;
		curl_easy_setopt(curl, CURLOPT_PROXY, NULL);
		curl_easy_setopt(curl, CURLOPT_PROXYUSERPWD, NULL);
		res = curl_easy_perform(curl);
		if(res != CURLE_OK){ return original_proxy_error; }
	}
	return res;
}
char* PerformHttpGet(const char* url){
	if(!url) return nullptr;
	try{
		CURL* curl = curl_easy_init();
		if(!curl) throw std::runtime_error("Failed to initialize libcurl.");
		std::string response;
		curl_easy_setopt(curl, CURLOPT_URL, url);
		curl_easy_setopt(curl, CURLOPT_SSL_OPTIONS, CURLSSLOPT_NATIVE_CA);
		curl_easy_setopt(curl, CURLOPT_WRITEFUNCTION, WriteCallback);
		curl_easy_setopt(curl, CURLOPT_WRITEDATA, &response);
		curl_easy_setopt(curl, CURLOPT_FOLLOWLOCATION, 1L);
		curl_easy_setopt(curl, CURLOPT_USERAGENT, "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36 (LM Stud; +https://github.com/CommanderLake/LMStud)");
		const CURLcode res = PerformWithRetry(curl, url);
		if(res != CURLE_OK){
			std::string err = "HTTP GET failed: ";
			err += curl_easy_strerror(res);
			curl_easy_cleanup(curl);
			throw std::runtime_error(err);
		}
		curl_easy_cleanup(curl);
		const auto out = static_cast<char*>(std::malloc(response.size() + 1));
		if(out){ std::memcpy(out, response.c_str(), response.size() + 1); }
		return out;
	} catch(const std::exception& ex){
		std::string err = "Error: ";
		err += ex.what();
		const auto out = static_cast<char*>(std::malloc(err.size() + 1));
		if(out){ std::memcpy(out, err.c_str(), err.size() + 1); }
		return out;
	}
}
static int InternalProgressCallback(void* clientp, curl_off_t dltotal, curl_off_t dlnow, curl_off_t, curl_off_t){
	const auto progressCallback = static_cast<NativeProgressCallback>(clientp);
	if(progressCallback){ return progressCallback(dltotal, dlnow); }
	return 0;
}
static int DownloadCore(const char* url, const char* targetPath, NativeProgressCallback progressCallback){
	if(!url || !targetPath) return -1;
	const auto destination = Utf8ToWide(targetPath);
	const auto separator = destination.find_last_of(L"\\/");
	const auto directory = separator == std::wstring::npos ? L"." : destination.substr(0, separator + 1);
	wchar_t temporaryPath[MAX_PATH];
	if(destination.empty() || !GetTempFileNameW(directory.c_str(), L"lms", 0, temporaryPath)) return -2;
	FILE* fp = nullptr;
	_wfopen_s(&fp, temporaryPath, L"wb");
	if(!fp){
		DeleteFileW(temporaryPath);
		return -2;
	}
	CURL* curl = curl_easy_init();
	if(!curl){
		std::fclose(fp);
		DeleteFileW(temporaryPath);
		return -3;
	}
	curl_easy_setopt(curl, CURLOPT_URL, url);
	curl_easy_setopt(curl, CURLOPT_SSL_OPTIONS, CURLSSLOPT_NATIVE_CA);
	curl_easy_setopt(curl, CURLOPT_WRITEFUNCTION, fwrite);
	curl_easy_setopt(curl, CURLOPT_WRITEDATA, fp);
	curl_easy_setopt(curl, CURLOPT_FOLLOWLOCATION, 1L);
	curl_easy_setopt(curl, CURLOPT_FAILONERROR, 1L);
	curl_easy_setopt(curl, CURLOPT_BUFFERSIZE, 1048576);
	if(progressCallback != nullptr){
		curl_easy_setopt(curl, CURLOPT_XFERINFOFUNCTION, InternalProgressCallback);
		curl_easy_setopt(curl, CURLOPT_XFERINFODATA, (void*)progressCallback);
		curl_easy_setopt(curl, CURLOPT_NOPROGRESS, 0L);
	}
	CURLcode result = PerformWithRetry(curl, url);
	curl_off_t total = 0;
	curl_off_t received = 0;
	if(result == CURLE_OK && progressCallback){
		curl_easy_getinfo(curl, CURLINFO_CONTENT_LENGTH_DOWNLOAD_T, &total);
		curl_easy_getinfo(curl, CURLINFO_SIZE_DOWNLOAD_T, &received);
	}
	curl_easy_cleanup(curl);
	if(std::fclose(fp) != 0 && result == CURLE_OK) result = CURLE_WRITE_ERROR;
	if(result == CURLE_OK && progressCallback && progressCallback(total > 0 ? total : 0, received)) result = CURLE_ABORTED_BY_CALLBACK;
	if(result == CURLE_OK && !MoveFileExW(temporaryPath, destination.c_str(), MOVEFILE_REPLACE_EXISTING | MOVEFILE_WRITE_THROUGH)) result = CURLE_WRITE_ERROR;
	if(result != CURLE_OK) DeleteFileW(temporaryPath);
	return result;
}
int DownloadFile(const char* url, const char* targetPath){ return DownloadCore(url, targetPath, nullptr); }
int DownloadFileWithProgress(const char* url, const char* targetPath, NativeProgressCallback progressCallback){ return DownloadCore(url, targetPath, progressCallback); }
void CurlGlobalInit(){ curl_global_init(CURL_GLOBAL_ALL); }
void CurlGlobalCleanup(){ curl_global_cleanup(); }

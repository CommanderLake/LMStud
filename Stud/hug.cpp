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
	if(ie.fAutoDetect || ie.lpszAutoConfigUrl){
		WINHTTP_AUTOPROXY_OPTIONS ao = {0};
		ao.dwFlags = (ie.fAutoDetect ? WINHTTP_AUTOPROXY_AUTO_DETECT : 0) | (ie.lpszAutoConfigUrl ? WINHTTP_AUTOPROXY_CONFIG_URL : 0);
		ao.dwAutoDetectFlags = WINHTTP_AUTO_DETECT_TYPE_DHCP | WINHTTP_AUTO_DETECT_TYPE_DNS_A;
		ao.lpszAutoConfigUrl = ie.lpszAutoConfigUrl;
		const std::wstring w_url = Utf8ToWide(url);
		ok = WinHttpGetProxyForUrl(nullptr, w_url.c_str(), &ao, &pi);
	}
	if(!ok && ie.lpszProxy){
		pi.dwAccessType = WINHTTP_ACCESS_TYPE_NAMED_PROXY;
		pi.lpszProxy = ie.lpszProxy;
		ie.lpszProxy = nullptr;
		ok = TRUE;
	}
	if(ok && pi.lpszProxy){
		const int len = WideCharToMultiByte(CP_UTF8, 0, pi.lpszProxy, -1, nullptr, 0, nullptr, nullptr);
		if(len > 1){
			std::string s(len - 1, '\0');
			WideCharToMultiByte(CP_UTF8, 0, pi.lpszProxy, -1, &s[0], len, nullptr, nullptr);
			outProxy = s;
		}
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
	FILE* fp = nullptr;
	fopen_s(&fp, targetPath, "wb");
	if(!fp) return -2;
	CURL* curl = curl_easy_init();
	if(!curl){
		std::fclose(fp);
		return -3;
	}
	curl_easy_setopt(curl, CURLOPT_URL, url);
	curl_easy_setopt(curl, CURLOPT_SSL_OPTIONS, CURLSSLOPT_NATIVE_CA);
	curl_easy_setopt(curl, CURLOPT_WRITEFUNCTION, fwrite);
	curl_easy_setopt(curl, CURLOPT_WRITEDATA, fp);
	curl_easy_setopt(curl, CURLOPT_FOLLOWLOCATION, 1L);
	curl_easy_setopt(curl, CURLOPT_BUFFERSIZE, 1048576);
	if(progressCallback != nullptr){
		curl_easy_setopt(curl, CURLOPT_XFERINFOFUNCTION, InternalProgressCallback);
		curl_easy_setopt(curl, CURLOPT_XFERINFODATA, (void*)progressCallback);
		curl_easy_setopt(curl, CURLOPT_NOPROGRESS, 0L);
	}
	const CURLcode res = PerformWithRetry(curl, url);
	curl_easy_cleanup(curl);
	std::fclose(fp);
	if(res != CURLE_OK){
		std::remove(targetPath);
		return res;
	}
	return 0;
}
int DownloadFile(const char* url, const char* targetPath){ return DownloadCore(url, targetPath, nullptr); }
int DownloadFileWithProgress(const char* url, const char* targetPath, NativeProgressCallback progressCallback){ return DownloadCore(url, targetPath, progressCallback); }
void CurlGlobalInit(){ curl_global_init(CURL_GLOBAL_ALL); }
void CurlGlobalCleanup(){ curl_global_cleanup(); }
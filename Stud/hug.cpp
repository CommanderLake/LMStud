#define CURL_STATICLIB
#include "hug.h"
#include <curl\curl.h>
#include <stdexcept>
#include <string>
static size_t WriteCallback(void* ptr, size_t size, size_t nmemb, void* userdata){
	const auto response = static_cast<std::string*>(userdata);
	response->append(static_cast<char*>(ptr), size*nmemb);
	return size*nmemb;
}
const char* PerformHttpGet(const char* url){
	if(!url) return nullptr;
	try{
		CURL* curl = curl_easy_init();
		if(!curl) throw std::runtime_error("Failed to initialize libcurl.");
		std::string response;
		curl_easy_setopt(curl, CURLOPT_URL, url);
		curl_easy_setopt(curl, CURLOPT_SSLVERSION, CURL_SSLVERSION_TLSv1_3);
		curl_easy_setopt(curl, CURLOPT_CAINFO, "cacert.pem");
		curl_easy_setopt(curl, CURLOPT_WRITEFUNCTION, WriteCallback);
		curl_easy_setopt(curl, CURLOPT_WRITEDATA, &response);
		curl_easy_setopt(curl, CURLOPT_FOLLOWLOCATION, 1L);
		const CURLcode res = curl_easy_perform(curl);
		if(res!=CURLE_OK){
			std::string err = "HTTP GET failed: ";
			err += curl_easy_strerror(res);
			curl_easy_cleanup(curl);
			throw std::runtime_error(err);
		}
		curl_easy_cleanup(curl);
		return response.c_str();
	} catch(const std::exception& ex){
		std::string err = "Error: ";
		err += ex.what();
		return err.c_str();
	}
}
int DownloadFile(const char* url, const char* targetPath){
	if(!url||!targetPath) return -1;
	FILE* fp = std::fopen(targetPath, "wb");
	if(!fp) return -2;
	CURL* curl = curl_easy_init();
	if(!curl){
		std::fclose(fp);
		return -3;
	}
	curl_easy_setopt(curl, CURLOPT_URL, url);
	curl_easy_setopt(curl, CURLOPT_SSLVERSION, CURL_SSLVERSION_TLSv1_3);
	curl_easy_setopt(curl, CURLOPT_CAINFO, "cacert.pem");
	curl_easy_setopt(curl, CURLOPT_WRITEFUNCTION, fwrite);
	curl_easy_setopt(curl, CURLOPT_WRITEDATA, fp);
	curl_easy_setopt(curl, CURLOPT_FOLLOWLOCATION, 1L);
	const CURLcode res = curl_easy_perform(curl);
	curl_easy_cleanup(curl);
	std::fclose(fp);
	if(res!=CURLE_OK){
		std::remove(targetPath);
		return -4;
	}
	return 0;
}
void CurlGlobalInit(){ curl_global_init(3); }
void CurlGlobalCleanup(){ curl_global_cleanup(); }
static int InternalProgressCallback(void* clientp, curl_off_t dltotal, curl_off_t dlnow, curl_off_t, curl_off_t){
	const auto progressCallback = static_cast<NativeProgressCallback>(clientp);
	if(progressCallback){ return progressCallback(dltotal, dlnow); }
	return 0;
}
int DownloadFileWithProgress(const char* url, const char* targetPath, NativeProgressCallback progressCallback){
	if(!url||!targetPath) return -1;
	FILE* fp = std::fopen(targetPath, "wb");
	if(!fp) return -2;
	CURL* curl = curl_easy_init();
	if(!curl){
		std::fclose(fp);
		return -3;
	}
	curl_easy_setopt(curl, CURLOPT_URL, url);
	curl_easy_setopt(curl, CURLOPT_SSLVERSION, CURL_SSLVERSION_TLSv1_3);
	curl_easy_setopt(curl, CURLOPT_CAINFO, "cacert.pem");
	if(progressCallback!=nullptr){
		curl_easy_setopt(curl, CURLOPT_XFERINFOFUNCTION, InternalProgressCallback);
		curl_easy_setopt(curl, CURLOPT_XFERINFODATA, (void*)progressCallback);
		curl_easy_setopt(curl, CURLOPT_NOPROGRESS, 0L);
	}
	curl_easy_setopt(curl, CURLOPT_WRITEFUNCTION, fwrite);
	curl_easy_setopt(curl, CURLOPT_WRITEDATA, fp);
	curl_easy_setopt(curl, CURLOPT_FOLLOWLOCATION, 1L);
	curl_easy_setopt(curl, CURLOPT_BUFFERSIZE, 1048576);
	const CURLcode res = curl_easy_perform(curl);
	curl_easy_cleanup(curl);
	std::fclose(fp);
	if(res!=CURLE_OK){
		std::remove(targetPath);
		return -4;
	}
	return 0;
}
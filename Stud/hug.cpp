#define CURL_STATICLIB
#include "hug.h"
#include <curl\curl.h>
#include <cstdlib>
#include <cstring>
#include <stdexcept>
#include <string>
std::string GetCACertBundlePath(){
	// Get temp folder from environment (fallback to "C:\temp" if not set)
	const char* tempDir = std::getenv("TEMP");
	if(!tempDir){
		tempDir = "C:\\temp";
	}
	std::string caPath = std::string(tempDir) + "\\cacert.pem";

	// Check if the file already exists
	FILE* file = std::fopen(caPath.c_str(), "rb");
	if(file){
		std::fclose(file);
		return caPath;
	}

	// File does not exist: download it using libcurl.
	CURL* curl = curl_easy_init();
	if(curl){
		FILE* fp = std::fopen(caPath.c_str(), "wb");
		if(fp){
			curl_easy_setopt(curl, CURLOPT_URL, "https://curl.se/ca/cacert.pem");
			// Disable verification temporarily to allow downloading the CA bundle.
			curl_easy_setopt(curl, CURLOPT_SSL_VERIFYPEER, 0L);
			curl_easy_setopt(curl, CURLOPT_SSL_VERIFYHOST, 0L);
			// Write callback directly writes downloaded data to file.
			curl_easy_setopt(curl, CURLOPT_WRITEFUNCTION, fwrite);
			curl_easy_setopt(curl, CURLOPT_WRITEDATA, fp);
			CURLcode res = curl_easy_perform(curl);
			std::fclose(fp);
			if(res != CURLE_OK){
				// On failure, remove the file and return an empty path.
				std::remove(caPath.c_str());
				caPath = "";
			}
		}
		curl_easy_cleanup(curl);
	}
	return caPath;
}

//------------------------------------------------------------------------------
// Callback that accumulates HTTP response data into a std::string.
static size_t WriteCallback(void* ptr, size_t size, size_t nmemb, void* userdata){
	auto response = reinterpret_cast<std::string*>(userdata);
	response->append(reinterpret_cast<char*>(ptr), size * nmemb);
	return size * nmemb;
}

// Helper: Allocates memory (using malloc) and copies the content of a std::string.
// The caller is expected to free the returned pointer via FreeMemory.
static char* AllocateAndCopy(const std::string& str){
	auto buffer = static_cast<char*>(std::malloc(str.size() + 1));
	if(buffer) std::memcpy(buffer, str.c_str(), str.size() + 1);
	return buffer;
}

//------------------------------------------------------------------------------
// Performs an HTTPS GET for the provided URL using libcurl with TLS 1.3 enabled.
// The function sets the CA bundle (downloaded to %TEMP% if needed) so that
// certificate verification succeeds.
char* PerformHttpGet(const char* url){
	if(!url) return nullptr;
	try{
		CURL* curl = curl_easy_init();
		if(!curl) throw std::runtime_error("Failed to initialize libcurl.");
		std::string response;

		curl_easy_setopt(curl, CURLOPT_URL, url);
		// Enforce TLS 1.3.
		curl_easy_setopt(curl, CURLOPT_SSLVERSION, CURL_SSLVERSION_TLSv1_3);

		// Get the CA bundle; if available, set it for certificate verification.
		std::string caPath = GetCACertBundlePath();
		if(!caPath.empty()){
			curl_easy_setopt(curl, CURLOPT_CAINFO, caPath.c_str());
		}

		curl_easy_setopt(curl, CURLOPT_WRITEFUNCTION, WriteCallback);
		curl_easy_setopt(curl, CURLOPT_WRITEDATA, &response);
		curl_easy_setopt(curl, CURLOPT_FOLLOWLOCATION, 1L);

		CURLcode res = curl_easy_perform(curl);
		if(res != CURLE_OK){
			std::string err = "HTTP GET failed: ";
			err += curl_easy_strerror(res);
			curl_easy_cleanup(curl);
			throw std::runtime_error(err);
		}
		curl_easy_cleanup(curl);
		return AllocateAndCopy(response);
	} catch(const std::exception& ex){
		std::string err = "Error: ";
		err += ex.what();
		return AllocateAndCopy(err);
	}
}

//------------------------------------------------------------------------------
// Downloads a file from the provided URL and saves it at the targetPath.
// The function sets the CA bundle (downloaded to %TEMP% if needed) so that
// certificate verification succeeds.
int DownloadFile(const char* url, const char* targetPath){
	if(!url || !targetPath) return -1;
	FILE* fp = std::fopen(targetPath, "wb");
	if(!fp) return -2;
	CURL* curl = curl_easy_init();
	if(!curl){
		std::fclose(fp);
		return -3;
	}

	curl_easy_setopt(curl, CURLOPT_URL, url);
	curl_easy_setopt(curl, CURLOPT_SSLVERSION, CURL_SSLVERSION_TLSv1_3);

	// Get the CA bundle and set it for certificate verification.
	std::string caPath = GetCACertBundlePath();
	if(!caPath.empty()){
		curl_easy_setopt(curl, CURLOPT_CAINFO, caPath.c_str());
	}

	// Use fwrite directly as the write callback.
	curl_easy_setopt(curl, CURLOPT_WRITEFUNCTION, fwrite);
	curl_easy_setopt(curl, CURLOPT_WRITEDATA, fp);
	curl_easy_setopt(curl, CURLOPT_FOLLOWLOCATION, 1L);

	CURLcode res = curl_easy_perform(curl);
	curl_easy_cleanup(curl);
	std::fclose(fp);
	if(res != CURLE_OK){
		// Clean up any incomplete file on error.
		std::remove(targetPath);
		return -4;
	}
	return 0;
}

//------------------------------------------------------------------------------
// Frees memory allocated by the PerformHttpGet function.
void FreeMemory(char* ptr){
	if(ptr)
		std::free(ptr);
}

//------------------------------------------------------------------------------
// Global initialization and cleanup functions for libcurl.
void CurlGlobalInit(){
	curl_global_init(3);
}
void CurlGlobalCleanup(){
	curl_global_cleanup();
}

// This is the function that libcurl will call. It adapts the CURLOPT_XFERINFOFUNCTION prototype.
static int InternalProgressCallback(void* clientp, curl_off_t dltotal, curl_off_t dlnow, curl_off_t /*ultotal*/, curl_off_t /*ulnow*/){
	// Cast the user pointer to our NativeProgressCallback.
	auto progressCallback = static_cast<NativeProgressCallback>(clientp);
	if(progressCallback){
		// Call the user-supplied callback.
		return progressCallback(dltotal, dlnow);
	}
	return 0;
}

// New exported function that downloads a file with progress updates.
// It returns 0 on success or a negative error code on failure.
int DownloadFileWithProgress(const char* url, const char* targetPath, NativeProgressCallback progressCallback){
	if(!url || !targetPath)
		return -1;
	FILE* fp = std::fopen(targetPath, "wb");
	if(!fp)
		return -2;
	CURL* curl = curl_easy_init();
	if(!curl){
		std::fclose(fp);
		return -3;
	}
	curl_easy_setopt(curl, CURLOPT_URL, url);
	curl_easy_setopt(curl, CURLOPT_SSLVERSION, CURL_SSLVERSION_TLSv1_3);

	// Set the CA bundle (downloaded to %TEMP% if needed).
	std::string caPath = GetCACertBundlePath();
	if(!caPath.empty()){
		curl_easy_setopt(curl, CURLOPT_CAINFO, caPath.c_str());
	}

	// Set the progress callback if provided.
	if(progressCallback != nullptr){
		curl_easy_setopt(curl, CURLOPT_XFERINFOFUNCTION, InternalProgressCallback);
		curl_easy_setopt(curl, CURLOPT_XFERINFODATA, (void*)progressCallback);
		// Enable the progress callback.
		curl_easy_setopt(curl, CURLOPT_NOPROGRESS, 0L);
	}

	// Setup writing the file.
	curl_easy_setopt(curl, CURLOPT_WRITEFUNCTION, fwrite);
	curl_easy_setopt(curl, CURLOPT_WRITEDATA, fp);
	curl_easy_setopt(curl, CURLOPT_FOLLOWLOCATION, 1L);

	CURLcode res = curl_easy_perform(curl);
	curl_easy_cleanup(curl);
	std::fclose(fp);
	if(res != CURLE_OK){
		std::remove(targetPath);
		return -4;
	}
	return 0;
}
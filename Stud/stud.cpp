#include "stud.h"
#include "hug.h"
#include <Windows.h>
#include <filesystem>
#include <chrono>
#include <regex>
#include <unordered_map>
#include <curl\curl.h>
using hr_clock = std::chrono::high_resolution_clock;
struct WebSection{ std::string tag; std::string text; };
struct CachedPage{ std::vector<WebSection> sections; };
static std::unordered_map<std::string, CachedPage> _webCache;
static std::string url_encode(const char* text){
	CURL* curl = curl_easy_init();
	char* enc = curl_easy_escape(curl, text, 0);
	std::string out = enc ? enc : "";
	curl_free(enc);
	curl_easy_cleanup(curl);
	return out;
}
void ClearTools(){
	_tools.clear();
	_toolHandlers.clear();
}
void AddTool(const char* name, const char* description, const char* parameters, char*(*handler)(const char* args)){
	if(!name) return;
	common_chat_tool t;
	t.name = name;
	if(description) t.description = description;
	if(parameters) t.parameters = parameters;
	_tools.push_back(t);
	if(handler) _toolHandlers[name] = handler;
}
const char* GoogleSearch(const char* argsJson){
	const char* queryStart = nullptr;
	const char* queryEnd = nullptr;
	if(argsJson){
		const char* p = strstr(argsJson, "\"query\"");
		if(p){
			p = strchr(p, ':');
			if(p){
				++p;
				while(*p==' '||*p=='\t') ++p;
				if(*p=='\"'){
					queryStart = ++p;
					queryEnd = strchr(queryStart, '\"');
				}
			}
		}
	}
	std::string query;
	if(queryStart&&queryEnd&&queryEnd>queryStart){ query.assign(queryStart, queryEnd); } else{ query = argsJson ? argsJson : ""; }
	return PerformHttpGet(("https://customsearch.googleapis.com/customsearch/v1?key="+googleAPIKey+"&cx="+googleSearchID+"&num=5&fields=items(title,link,snippet)&prettyPrint=false&q="+url_encode(query.c_str())).c_str());
}
void SetGoogle(const char* apiKey, const char* searchEngineId){
	googleAPIKey = apiKey;
	googleSearchID = searchEngineId;
}
void BackendInit(){
	_putenv("OMP_PROC_BIND=close");
	ggml_backend_load("ggml-cpu.dll");
	const HMODULE hModule = LoadLibraryA("nvcuda.dll");
	if(hModule!=nullptr) ggml_backend_load("ggml-cuda.dll");
	llama_backend_init();
}
void ResetChat(){
	_chatMsgs.clear();
	RetokenizeChat();
}
void FreeModel(){
	if(_smpl){
		common_sampler_free(_smpl);
		_smpl = nullptr;
	}
	if(_ctx){
		llama_free(_ctx);
		_ctx = nullptr;
	}
	if(_llModel){
		llama_model_free(_llModel);
		_llModel = nullptr;
	}
	llama_backend_free();
}
bool LoadModel(const char* filename, const char* systemPrompt, const int nCtx, const float temp, const float repeatPenalty, const int topK, const int topP, const int nThreads, const bool strictCPU, const int nThreadsBatch, const bool strictCPUBatch,
				const int nGPULayers, const int nBatch, const bool mMap, const bool mLock, const ggml_numa_strategy numaStrategy, const bool flashAttn){
	FreeModel();
	_params.numa = numaStrategy;
	llama_numa_init(_params.numa);
	_params.warmup = false;
	_params.model.path = filename;
	std::string sysPrompt(systemPrompt);
	if(sysPrompt.empty()) sysPrompt = std::string("Assist the user to the best of your ability.");
	_params.prompt = sysPrompt;
	_params.n_ctx = nCtx;
	_params.sampling.temp = temp;
	_params.sampling.penalty_repeat = repeatPenalty;
	_params.sampling.top_k = topK;
	_params.sampling.top_p = topP;
	_params.use_mmap = mMap;
	_params.use_mlock = mLock;
	_params.flash_attn = flashAttn;
	_params.cpuparams.n_threads = nThreads;
	_params.cpuparams.strict_cpu = strictCPU;
	_params.cpuparams_batch.n_threads = nThreadsBatch;
	_params.cpuparams_batch.strict_cpu = strictCPUBatch;
	_params.n_gpu_layers = nGPULayers;
	_params.n_ubatch = nBatch;
	_params.enable_chat_template = true;
	_params.use_jinja = true;
	auto llamaInit = common_init_from_params(_params);
	if(!llamaInit.model||!llamaInit.context) return false;
	_llModel = llamaInit.model.release();
	_ctx = llamaInit.context.release();
	_vocab = llama_model_get_vocab(_llModel);
	_chatTemplates = common_chat_templates_init(_llModel, _params.chat_template);
	if(!llama_model_has_encoder(_llModel)&&llama_vocab_get_add_eos(_vocab)) return false;
	_smpl = common_sampler_init(_llModel, _params.sampling);
	if(!_smpl) return false;
	return true;
}
void SetTokenCallback(const TokenCallbackFn cb){ _tokenCb = cb; }
void SetThreadCount(int n, int nBatch){ if(_ctx) llama_set_n_threads(_ctx, n, nBatch); }
int AddMessage(std::string role, std::string message){
	if(!_vocab||role.empty()) return 0;
	const size_t prev = _tokens.size();
	common_chat_msg newMsg;
	newMsg.role = role;
	newMsg.content = message;
	const auto formatted = common_chat_format_single(_chatTemplates.get(), _chatMsgs, newMsg, !role._Equal("assistant"), _params.use_jinja&&role._Equal("assistant"));
	_chatMsgs.push_back(newMsg);
	std::vector<llama_token> toks = common_tokenize(_vocab, formatted, false, true);
	_tokens.insert(_tokens.end(), toks.begin(), toks.end());
	return static_cast<int>(_tokens.size()-prev);
}
int AddMessage(const bool user, const char* message){ return AddMessage(std::string(user ? "user" : "assistant"), std::string(message)); }
void RetokenizeChat(){
	if(!_ctx || !_vocab) return;
	llama_kv_self_clear(_ctx);
	_tokens.clear();
	std::vector<common_chat_msg> msgs;
	if(!_params.prompt.empty()){
		common_chat_msg sys;
		sys.role = "system";
		sys.content = _params.prompt;
		msgs.push_back(sys);
	}
	if(!_chatMsgs.empty()) msgs.insert(msgs.end(), _chatMsgs.begin(), _chatMsgs.end());
	common_chat_templates_inputs inputs;
	inputs.use_jinja = _params.use_jinja;
	inputs.messages = msgs;
	inputs.add_generation_prompt = true;
	inputs.tools = _tools;
	inputs.tool_choice = COMMON_CHAT_TOOL_CHOICE_AUTO;
	const auto chatData = common_chat_templates_apply(_chatTemplates.get(), inputs);
	_chatFormat = chatData.format;
	_tokens = common_tokenize(_vocab, chatData.prompt, true, true);
	_nConsumed = 0;
}
void SetSystemPrompt(const char* prompt){
	_params.prompt = std::string(prompt);
	RetokenizeChat();
}
void SetMessageAt(int index, const char* message){
	if(index<0||index>=static_cast<int>(_chatMsgs.size())) return;
	_chatMsgs[index].content = std::string(message);
	RetokenizeChat();
}
void RemoveMessageAt(const int index){
	if(index<0||index>=static_cast<int>(_chatMsgs.size())) return;
	_chatMsgs.erase(_chatMsgs.begin()+index);
	RetokenizeChat();
}
void RemoveMessagesStartingAt(int index){
	if(index<0) index = 0;
	if(index>static_cast<int>(_chatMsgs.size())) index = static_cast<int>(_chatMsgs.size());
	_chatMsgs.erase(_chatMsgs.begin()+index, _chatMsgs.end());
	RetokenizeChat();
}
int Generate(const unsigned int nPredict, const bool callback){
	const auto prepStart = hr_clock::now();
	_stop.store(false);
	const TokenCallbackFn cb = _tokenCb;
	std::vector<llama_token> embd;
	std::ostringstream assMsg;
	double ftTime = 0.0;
	unsigned i = 0;
	while(i<nPredict&&!_stop.load()){
		bool sampled = false;
		if(_nConsumed>=static_cast<int>(_tokens.size())){
			llama_token id = common_sampler_sample(_smpl, _ctx, -1);
			common_sampler_accept(_smpl, id, true);
			embd.push_back(id);
			sampled = true;
		} else{
			while(_nConsumed<static_cast<int>(_tokens.size())&&embd.size()<_params.n_batch){
				embd.push_back(_tokens[_nConsumed]);
				common_sampler_accept(_smpl, _tokens[_nConsumed], false);
				++_nConsumed;
			}
		}
		for(int j = 0; j<static_cast<int>(embd.size()); j += _params.n_batch){
			int nEval = static_cast<int>(embd.size())-j;
			if(nEval>_params.n_batch) nEval = _params.n_batch;
			if(llama_decode(_ctx, llama_batch_get_one(&embd[j], nEval))) return i;
		}
		if(sampled){
			++i;
			const llama_token id = common_sampler_last(_smpl);
			std::string tokenStr = common_token_to_piece(_ctx, id, false);
			if(ftTime==0.0) ftTime = std::chrono::duration<double, std::ratio<1, 1>>(hr_clock::now()-prepStart).count();
			if(!tokenStr.empty()){
				assMsg<<tokenStr;
				if(cb&&callback) cb(tokenStr.c_str(), static_cast<int>(tokenStr.length()), 1, static_cast<int>(_tokens.size()+i), ftTime, false);
			}
			if(llama_vocab_is_eog(_vocab, id)) break;
		}
		embd.clear();
	}
	AddMessage(false, assMsg.str().c_str());
	if(cb&&!callback) cb(assMsg.str().c_str(), assMsg.str().length(), i, static_cast<int>(_tokens.size()), ftTime, false);
	return i;
}
int GenerateWithTools(const unsigned int nPredict, const bool callback){
	int res;
	bool toolCalled;
	const TokenCallbackFn cb = _tokenCb;
	do{
		toolCalled = false;
		res = Generate(nPredict, callback);
		if(_chatMsgs.empty()) return res;
		const auto& last = _chatMsgs.back();
		const auto parsed = common_chat_parse(last.content, _chatFormat);
		for(const auto& tc : parsed.tool_calls){
			auto it = _toolHandlers.find(tc.name);
			if(it!=_toolHandlers.end()){
				const auto result = it->second(tc.arguments.c_str());
				const bool gotResult = result != nullptr;
				std::string resultStr = gotResult ? result : "";
				FreeMemory(result);
				AddMessage(std::string("tool"), resultStr);
				if(cb&&callback) cb(resultStr.c_str(), static_cast<int>(resultStr.length()), 0, static_cast<int>(_tokens.size()), 0, true);
				if(gotResult){ toolCalled = true; }
			}
		}
	} while(toolCalled);
	return res;
}
void StopGeneration(){ _stop.store(true); }

static std::string json_escape(const std::string& in){
        std::string out; out.reserve(in.size());
        for(const char c : in){
                switch(c){
                        case '"': out += "\\\""; break;
                        case '\\': out += "\\\\"; break;
                        case '\n': out += "\\n"; break;
                        case '\r': out += "\\r"; break;
                        case '\t': out += "\\t"; break;
                        default: out += c; break;
                }
        }
        return out;
}

static std::string get_json_value(const char* json, const char* key){
        if(!json||!key) return "";
        const char* p = strstr(json, key);
        if(!p) return "";
        p = strchr(p, ':');
        if(!p) return "";
        ++p;
        while(*p==' '||*p=='\t') ++p;
        if(*p=='\"'){
                ++p;
                const char* end = strchr(p, '\"');
                if(end&&end>p) return std::string(p, end);
        } else{
                const char* end = p;
                while(*end&&*end!=','&&*end!='}'&&*end!=' ') ++end;
                if(end>p) return std::string(p, end);
        }
        return "";
}

static void parse_sections(const std::string& html, CachedPage& page){
        std::string cleaned = std::regex_replace(html, std::regex("<(script|style)[^>]*?>.*?</\\s*\\1\\s*>", std::regex::icase|std::regex::dotall), " ");
        std::regex section_re("<(p|h[1-6]|li|title|article|section)[^>]*>(.*?)</\\s*\\1\\s*>", std::regex::icase|std::regex::dotall);
        std::regex tag_re("<[^>]+>");
        std::regex ws_re("\\s+");
        auto it = cleaned.cbegin();
        std::smatch m;
        while(std::regex_search(it, cleaned.cend(), m, section_re)){
                std::string tag = m[1].str();
                std::string txt = std::regex_replace(m[2].str(), tag_re, " ");
                txt = std::regex_replace(txt, ws_re, " ");
                size_t s = txt.find_first_not_of(' ');
                size_t e = txt.find_last_not_of(' ');
                if(s!=std::string::npos&&e!=std::string::npos){
                        txt = txt.substr(s, e-s+1);
                        if(!txt.empty()) page.sections.push_back({tag, txt});
                }
                it = m.suffix().first;
        }
}

static char* make_json(const std::string& s){
        char* out = static_cast<char*>(std::malloc(s.size()+1));
        if(out) std::memcpy(out, s.c_str(), s.size()+1);
        return out;
}

const char* FetchWebpage(const char* argsJson){
        std::string url = get_json_value(argsJson, "url");
        if(url.empty()) url = argsJson ? argsJson : "";
        char* res = PerformHttpGet(url.c_str());
        if(!res) return nullptr;
        std::string html(res);
        FreeMemory(res);
        CachedPage page; parse_sections(html, page);
        _webCache[url] = page;
        std::string json = "{\"url\":\""+json_escape(url)+"\",\"sections\":";
        json += "[";
        for(size_t i=0;i<page.sections.size();++i){
                auto& sec = page.sections[i];
                std::string snippet = sec.text.substr(0,80);
                if(sec.text.size()>80) snippet += "...";
                json += "{\"id\":"+std::to_string(i)+",\"tag\":\""+sec.tag+"\",\"snippet\":\""+json_escape(snippet)+"\"}";
                if(i+1<page.sections.size()) json += ",";
        }
        json += "]}";
        return make_json(json);
}

const char* BrowseWebCache(const char* argsJson){
        std::string url = get_json_value(argsJson, "url");
        if(url.empty()) url = argsJson ? argsJson : "";
        auto it = _webCache.find(url);
        if(it==_webCache.end()) return make_json("{\"error\":\"not cached\"}");
        const auto& page = it->second;
        std::string json = "{\"url\":\""+json_escape(url)+"\",\"sections\":";
        json += "[";
        for(size_t i=0;i<page.sections.size();++i){
                auto& sec = page.sections[i];
                std::string snippet = sec.text.substr(0,80);
                if(sec.text.size()>80) snippet += "...";
                json += "{\"id\":"+std::to_string(i)+",\"tag\":\""+sec.tag+"\",\"snippet\":\""+json_escape(snippet)+"\"}";
                if(i+1<page.sections.size()) json += ",";
        }
        json += "]}";
        return make_json(json);
}

const char* GetWebSection(const char* argsJson){
        std::string url = get_json_value(argsJson, "url");
        std::string idStr = get_json_value(argsJson, "id");
        if(url.empty()) url = argsJson ? argsJson : "";
        int id = idStr.empty() ? -1 : std::stoi(idStr);
        auto it = _webCache.find(url);
        if(it==_webCache.end()||id<0||id>=static_cast<int>(it->second.sections.size()))
                return make_json("{\"error\":\"not found\"}");
        return make_json(json_escape(it->second.sections[id].text));
}

void ClearWebCache(){ _webCache.clear(); }

#include "stud.h"
#include "hug.h"
#include <filesystem>
#include <chrono>
#include <regex>
#include <unordered_map>
#include <minja\minja.hpp>
#include <minja\chat-template.hpp>
using HrClock = std::chrono::high_resolution_clock;
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
		llama_sampler_free(_smpl);
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
	_cachedTokens.clear();
}
bool LoadModel(const HWND hWnd, const char* filename, const int nCtx, const float temp, const float repeatPenalty, const int topK, const int topP, const int nThreads, const int nThreadsBatch, const int nGPULayers, const int nBatch, const bool mMap, const bool mLock, const ggml_numa_strategy numaStrategy, const bool flashAttn){
	auto params = llama_model_default_params();
	params.n_gpu_layers = nGPULayers;
	params.use_mlock = mLock;
	params.use_mmap = mMap;
	llama_numa_init(numaStrategy);
	_useJinja = true;
	_llModel = llama_model_load_from_file(filename, params);
	if(!_llModel){
		MessageBoxA(hWnd, (std::string("Unable to load model:\n\n") + filename).c_str(), "LM Stud", MB_ICONERROR);
		return false;
	}
	_vocab = llama_model_get_vocab(_llModel);
	auto ctxParams = llama_context_default_params();
	ctxParams.n_ctx = nCtx;
	ctxParams.n_batch = _nBatch = nBatch;
	ctxParams.flash_attn = flashAttn;
	ctxParams.n_threads = nThreads;
	ctxParams.n_threads_batch = nThreadsBatch;
	_ctx = llama_init_from_model(_llModel, ctxParams);
	if(!_ctx){
		MessageBoxA(hWnd, (std::string("Failed to create the llama context for model:\n\n") + filename).c_str(), "LM Stud", MB_ICONERROR);
		return false;
	}
	_smpl = llama_sampler_chain_init(llama_sampler_chain_default_params());
	llama_sampler_chain_add(_smpl, llama_sampler_init_min_p(0.05f, 1));
	llama_sampler_chain_add(_smpl, llama_sampler_init_top_p(topP, 1));
	llama_sampler_chain_add(_smpl, llama_sampler_init_top_k(topK));
	llama_sampler_chain_add(_smpl, llama_sampler_init_temp(temp));
	llama_sampler_chain_add(_smpl, llama_sampler_init_penalties(64, repeatPenalty, 0.0f, 0.0f));
	llama_sampler_chain_add(_smpl, llama_sampler_init_dist(LLAMA_DEFAULT_SEED));
	_chatTemplates = common_chat_templates_init(_llModel, "");
	const auto bosStr = llama_vocab_get_text(_vocab, llama_vocab_bos(_vocab));
	const auto eosStr = llama_vocab_get_text(_vocab, llama_vocab_eos(_vocab));
	const char* tmplSrc = llama_model_chat_template(_llModel, nullptr);
	if(tmplSrc){
		const minja::chat_template tmpl(std::string(tmplSrc), bosStr, eosStr);
		_hasTools = tmpl.original_caps().supports_tools;
	}
	return true;
}
void AddTool(const char* name, const char* description, const char* parameters, std::string(*handler)(const char* args)){
	if(!name || !_hasTools) return;
	common_chat_tool t;
	t.name = name;
	if(description) t.description = description;
	if(parameters) t.parameters = parameters;
	_tools.push_back(t);
	if(handler) _toolHandlers[name] = handler;
}
void ClearTools(){
	_tools.clear();
	_toolHandlers.clear();
}
bool HasTool(const char* name){
	for(const auto & tool : _tools){
		if(tool.name._Equal(name)) return true;
	}
	return false;
}
void SetTokenCallback(const TokenCallbackFn cb){ _tokenCb = cb; }
void SetThreadCount(const int n, const int nBatch){ if(_ctx) llama_set_n_threads(_ctx, n, nBatch); }
void AddMessage(const std::string role, const std::string message){
	if(!_vocab||role.empty()) return;
	common_chat_msg newMsg;
	newMsg.role = role;
	newMsg.content = message;
	_chatMsgs.push_back(newMsg);
}
void RetokenizeChat(){
	if(!_ctx||!_vocab) return;
	std::vector<common_chat_msg> msgs;
	if(!_prompt.empty()){ msgs.push_back({"system", _prompt}); }
	msgs.insert(msgs.end(), _chatMsgs.begin(), _chatMsgs.end());
	common_chat_templates_inputs in;
	in.use_jinja = _useJinja;
	in.messages = msgs;
	in.add_generation_prompt = true;
	in.tools = _tools;
	in.tool_choice = COMMON_CHAT_TOOL_CHOICE_AUTO;
	auto chatData = common_chat_templates_apply(_chatTemplates.get(), in);
	_chatFormat = chatData.format;
	const int nPrompt = -llama_tokenize(_vocab, chatData.prompt.c_str(), chatData.prompt.size(), nullptr, 0, true, true);
	std::vector<llama_token> promptTokens(nPrompt);
	llama_tokenize(_vocab, chatData.prompt.c_str(), chatData.prompt.size(), promptTokens.data(), promptTokens.size(), true, true);
	size_t prefix = 0;
	while(prefix<_cachedTokens.size()&&prefix<promptTokens.size()&&_cachedTokens[prefix]==promptTokens[prefix]){ ++prefix; }
	llama_memory_t mem = llama_get_memory(_ctx);
	if(prefix<_cachedTokens.size()){ llama_memory_seq_rm(mem, 0, prefix, -1); } else if(prefix==0){ llama_memory_clear(mem, true); }
	llama_sampler_reset(_smpl);
	for(size_t i = 0; i<prefix; ++i){ llama_sampler_accept(_smpl, promptTokens[i]); }
	for(size_t i = prefix; i<promptTokens.size(); i += _nBatch){
		const int nEval = std::min<int>(_nBatch, promptTokens.size()-i);
		auto batch = llama_batch_get_one(&promptTokens[i], nEval);
		if(llama_decode(_ctx, batch)!=0){ return; }
		for(int j = 0; j<nEval; ++j){ llama_sampler_accept(_smpl, promptTokens[i+j]); }
	}
	_cachedTokens = std::move(promptTokens);
	OutputDebugStringA((std::string(GetContextAsText()) + "\n\n---\n\n").c_str());
}
void SetSystemPrompt(const char* prompt){
	_prompt = std::string(prompt);
	RetokenizeChat();
}
void SetMessageAt(const int index, const char* message){
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
std::string Generate(const HWND hWnd, const std::string role, const std::string& prompt, const unsigned int nPredict, const bool callback){
	const auto prepStart = HrClock::now();
	_stop.store(false);
	const TokenCallbackFn cb = _tokenCb;
	std::string response;
	common_chat_msg newMsg;
	newMsg.role = role;
	newMsg.content = prompt;
	const auto formatted = common_chat_format_single(_chatTemplates.get(), _chatMsgs, newMsg, !role._Equal("assistant"), _useJinja&&role._Equal("assistant"));
	const int nPromptTokens = -llama_tokenize(_vocab, formatted.c_str(), formatted.size(), nullptr, 0, false, true);
	std::vector<llama_token> promptTokens(nPromptTokens);
	if(llama_tokenize(_vocab, formatted.c_str(), formatted.size(), promptTokens.data(), promptTokens.size(), false, true)<0){
		MessageBoxA(hWnd, "Failed to tokenize the prompt", "LM Stud", MB_ICONERROR);
		return std::string();
	}
	AddMessage(role, prompt);
	llama_token newTokenId;
	std::vector<llama_token> newTokens;
	const auto llMem = llama_get_memory(_ctx);
	double ftTime = 0.0;
	int i = 0;
	size_t p = 0;
	while(p < promptTokens.size()){
		const int nEval = std::min<int>(_nBatch, promptTokens.size()-p);
		llama_batch batch = llama_batch_get_one(&promptTokens[p], nEval);
		const int nCtx = llama_n_ctx(_ctx);
		const int nCtxUsed = llama_memory_seq_pos_max(llMem, 0);
		if(nCtxUsed + batch.n_tokens > nCtx){
			MessageBoxA(hWnd, "Context size exceeded", "LM Stud", MB_ICONEXCLAMATION);
			return std::string();
		}
		auto result = llama_decode(_ctx, batch);
		if(result != 0){
			MessageBoxA(hWnd, (std::string("llama_decode failed with error number: ") + std::to_string(result)).c_str(), "LM Stud", MB_ICONERROR);
			return std::string();
		}
		for(int j = 0; j<nEval; ++j){ llama_sampler_accept(_smpl, promptTokens[p+j]); }
		p += nEval;
	}
	while(i<nPredict&&!_stop.load()){
		newTokenId = llama_sampler_sample(_smpl, _ctx, -1);
		if(llama_vocab_is_eog(_vocab, newTokenId)){ break; }
		char buf[256];
		const int n = llama_token_to_piece(_vocab, newTokenId, buf, sizeof buf, 0, true);
		if(n<0){
			MessageBoxA(hWnd, "Failed to convert token to piece", "LM Stud", MB_ICONERROR);
			return std::string();
		}
		newTokens.push_back(newTokenId);
		if(ftTime==0.0) ftTime = std::chrono::duration<double, std::ratio<1, 1>>(HrClock::now()-prepStart).count();
		std::string tokenStr(buf, n);
		response += tokenStr;
		++i;
		if(cb&&callback) cb(tokenStr.c_str(), static_cast<int>(tokenStr.length()), 1, llama_memory_seq_pos_max(llMem, 0), ftTime, 0);
		llama_batch batch = llama_batch_get_one(&newTokenId, 1);
		const int nCtx = llama_n_ctx(_ctx);
		const int nCtxUsed = llama_memory_seq_pos_max(llMem, 0);
		if(nCtxUsed + batch.n_tokens > nCtx){
			MessageBoxA(hWnd, "Context size exceeded", "LM Stud", MB_ICONEXCLAMATION);
			return std::string();
		}
		auto result = llama_decode(_ctx, batch);
		if(result != 0){
			MessageBoxA(hWnd, (std::string("llama_decode failed with error number: ") + std::to_string(result)).c_str(), "LM Stud", MB_ICONERROR);
			return std::string();
		}
		llama_sampler_accept(_smpl, newTokenId);
	}
	AddMessage("assistant", response);
	_cachedTokens.insert(_cachedTokens.end(), promptTokens.begin(), promptTokens.end());
	_cachedTokens.insert(_cachedTokens.end(), newTokens.begin(), newTokens.end());
	if(cb&&!callback) cb(response.c_str(), response.length(), i, llama_memory_seq_pos_max(llMem, 0), ftTime, 0);
	return response;
}
int GenerateWithTools(const HWND hWnd, const MessageRole role, char* prompt, const unsigned int nGen, const bool callback){
	auto promptStr = std::string(prompt);
	if(!_hasTools) return Generate(hWnd, "user", promptStr, nGen, callback).length();
	std::string response;
	bool toolCalled = role == MessageRole::Tool;
	const TokenCallbackFn cb = _tokenCb;
	const auto llMem = llama_get_memory(_ctx);
	auto StripThink = [](const std::string& text){
		std::string out;
		out.reserve(text.size());
		bool inThink = false;
		for(size_t i = 0; i<text.size(); ++i){
			if(!inThink && _strnicmp(text.c_str()+i, "<think>", 7)==0){
				inThink = true;
				i += 6;
				continue;
			}
			if(inThink && _strnicmp(text.c_str()+i, "</think>", 8)==0){
				inThink = false;
				i += 7;
				continue;
			}
			if(!inThink) out.push_back(text[i]);
		}
		return out;
	};
	do{
		response = Generate(hWnd, toolCalled ? "tool" : "user", promptStr, nGen, callback);
		toolCalled = false;
		if(_chatMsgs.empty()) return response.length();
		common_chat_syntax syntax;
		syntax.format = _chatFormat;
		syntax.parse_tool_calls = true;
		try{
			const auto parsed = common_chat_parse(StripThink(response), false, syntax);
			for(const auto& tc : parsed.tool_calls){
				auto it = _toolHandlers.find(tc.name);
				if(it!=_toolHandlers.end()){
					promptStr = it->second(tc.arguments.c_str());
					if(cb&&callback) cb(promptStr.c_str(), static_cast<int>(promptStr.length()), 0, llama_memory_seq_pos_max(llMem, 0), 0, 1);
					toolCalled = true;
				}
			}
		} catch(...){ break; }
	} while(toolCalled);
	return response.length();
}
void StopGeneration(){ _stop.store(true); }
char* GetContextAsText(){
	if(!_ctx) return nullptr;
	std::string outStr;
	outStr.reserve(_cachedTokens.size()*4);
	for(const llama_token tok : _cachedTokens){ outStr += common_token_to_piece(_ctx, tok, true); }
	auto* out = static_cast<char*>(std::malloc(outStr.size()+1));
	if(out) std::memcpy(out, outStr.c_str(), outStr.size()+1);
	return out;
}

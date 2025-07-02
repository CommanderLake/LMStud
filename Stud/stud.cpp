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
static ChatSession* CurrSession(){
	const auto it = _sessions.find(_activeSession);
	if(it==_sessions.end()) return nullptr;
	return &it->second;
}
int CreateSession(const int nCtx, const float temp, const float repeatPenalty, const int topK, const int topP, const int nThreads, const int nThreadsBatch, const int nBatch, const bool flashAttn){
	if(!_llModel) return -1;
	ChatSession sess;
	auto ctxParams = llama_context_default_params();
	ctxParams.n_ctx = nCtx;
	ctxParams.n_batch = nBatch;
	ctxParams.flash_attn = flashAttn;
	ctxParams.n_threads = nThreads;
	ctxParams.n_threads_batch = nThreadsBatch;
	sess.ctx = llama_init_from_model(_llModel, ctxParams);
	if(!sess.ctx) return -1;
	sess.nBatch = nBatch;
	sess.smpl = llama_sampler_chain_init(llama_sampler_chain_default_params());
	llama_sampler_chain_add(sess.smpl, llama_sampler_init_min_p(0.05f, 1));
	llama_sampler_chain_add(sess.smpl, llama_sampler_init_top_p(topP, 1));
	llama_sampler_chain_add(sess.smpl, llama_sampler_init_top_k(topK));
	llama_sampler_chain_add(sess.smpl, llama_sampler_init_temp(temp));
	llama_sampler_chain_add(sess.smpl, llama_sampler_init_penalties(64, repeatPenalty, 0.0f, 0.0f));
	llama_sampler_chain_add(sess.smpl, llama_sampler_init_dist(LLAMA_DEFAULT_SEED));
	const int id = _nextSessionId++;
	_sessions[id] = std::move(sess);
	_activeSession = id;
	return id;
}
void DestroySession(const int id){
	const auto it = _sessions.find(id);
	if(it==_sessions.end()) return;
	if(it->second.smpl) llama_sampler_free(it->second.smpl);
	if(it->second.ctx) llama_free(it->second.ctx);
	_sessions.erase(it);
	if(_activeSession==id) _activeSession = _sessions.empty() ? -1 : _sessions.begin()->first;
}
bool SetActiveSession(const int id){
	if(_sessions.find(id)==_sessions.end()) return false;
	_activeSession = id;
	return true;
}
int GetActiveSession(){ return _activeSession; }
void ResetChat(){
	auto* s = CurrSession();
	if(!s) return;
	s->chatMsgs.clear();
	RetokenizeChat();
}
void FreeModel(){
	for(auto& it : _sessions){
		if(it.second.smpl){
			llama_sampler_free(it.second.smpl);
			it.second.smpl = nullptr;
		}
		if(it.second.ctx){
			llama_free(it.second.ctx);
			it.second.ctx = nullptr;
		}
		it.second.chatMsgs.clear();
		it.second.cachedTokens.clear();
	}
	_sessions.clear();
	_activeSession = -1;
	if(_llModel){
		llama_model_free(_llModel);
		_llModel = nullptr;
	}
}
int LoadModel(const HWND hWnd, const char* filename, const int nGPULayers, const bool mMap, const bool mLock, const ggml_numa_strategy numaStrategy){
	auto params = llama_model_default_params();
	params.n_gpu_layers = nGPULayers;
	params.use_mlock = mLock;
	params.use_mmap = mMap;
	llama_numa_init(numaStrategy);
	_llModel = llama_model_load_from_file(filename, params);
	if(!_llModel){
		MessageBoxA(hWnd, (std::string("Unable to load model:\n\n")+filename).c_str(), "LM Stud", MB_ICONERROR);
		return -1;
	}
	_vocab = llama_model_get_vocab(_llModel);
	_chatTemplates = common_chat_templates_init(_llModel, "");
	const auto bosStr = llama_vocab_get_text(_vocab, llama_vocab_bos(_vocab));
	const auto eosStr = llama_vocab_get_text(_vocab, llama_vocab_eos(_vocab));
	const char* tmplSrc = llama_model_chat_template(_llModel, nullptr);
	if(tmplSrc){
		const minja::chat_template tmpl(std::string(tmplSrc), bosStr, eosStr);
		_hasTools = tmpl.original_caps().supports_tools;
	}
	_sessions.clear();
	_activeSession = -1;
	return 0;
}
void AddTool(const char* name, const char* description, const char* parameters, std::string (*handler)(const char* args)){
	if(!name||!_hasTools) return;
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
	for(const auto& tool : _tools){ if(tool.name._Equal(name)) return true; }
	return false;
}
void SetTokenCallback(const TokenCallbackFn cb){ _tokenCb = cb; }
void SetThreadCount(const int n, const int nBatch){
	const auto* s = CurrSession();
	if(s&&s->ctx) llama_set_n_threads(s->ctx, n, nBatch);
}
void AddMessage(const std::string role, const std::string message){
	if(!_vocab||role.empty()) return;
	auto* s = CurrSession();
	if(!s) return;
	common_chat_msg newMsg;
	newMsg.role = role;
	newMsg.content = message;
	s->chatMsgs.push_back(newMsg);
}
void RetokenizeChat(){
	auto* s = CurrSession();
	if(!s||!s->ctx||!_vocab) return;
	std::vector<common_chat_msg> msgs;
	if(!s->prompt.empty()){ msgs.push_back({"system", s->prompt}); }
	msgs.insert(msgs.end(), s->chatMsgs.begin(), s->chatMsgs.end());
	common_chat_templates_inputs in;
	in.use_jinja = s->useJinja;
	in.messages = msgs;
	in.add_generation_prompt = true;
	in.tools = _tools;
	in.tool_choice = COMMON_CHAT_TOOL_CHOICE_AUTO;
	auto chatData = common_chat_templates_apply(_chatTemplates.get(), in);
	s->chatFormat = chatData.format;
	const int nPrompt = -llama_tokenize(_vocab, chatData.prompt.c_str(), chatData.prompt.size(), nullptr, 0, true, true);
	std::vector<llama_token> promptTokens(nPrompt);
	llama_tokenize(_vocab, chatData.prompt.c_str(), chatData.prompt.size(), promptTokens.data(), promptTokens.size(), true, true);
	size_t prefix = 0;
	while(prefix<s->cachedTokens.size()&&prefix<promptTokens.size()&&s->cachedTokens[prefix]==promptTokens[prefix]){ ++prefix; }
	llama_memory_t mem = llama_get_memory(s->ctx);
	if(prefix<s->cachedTokens.size()){ llama_memory_seq_rm(mem, 0, prefix, -1); } else if(prefix==0){ llama_memory_clear(mem, true); }
	llama_sampler_reset(s->smpl);
	for(size_t i = 0; i<prefix; ++i){ llama_sampler_accept(s->smpl, promptTokens[i]); }
	for(size_t i = prefix; i<promptTokens.size(); i += s->nBatch){
		const int nEval = std::min<int>(s->nBatch, promptTokens.size()-i);
		auto batch = llama_batch_get_one(&promptTokens[i], nEval);
		if(llama_decode(s->ctx, batch)!=0){ return; }
		for(int j = 0; j<nEval; ++j){ llama_sampler_accept(s->smpl, promptTokens[i+j]); }
	}
	s->cachedTokens = std::move(promptTokens);
	//OutputDebugStringA((std::string(GetContextAsText()) + "\n\n---\n\n").c_str());
}
void SetSystemPrompt(const char* prompt){
	auto* s = CurrSession();
	if(!s) return;
	s->prompt = std::string(prompt);
	RetokenizeChat();
}
void SetMessageAt(const int index, const char* message){
	auto* s = CurrSession();
	if(!s) return;
	if(index<0||index>=static_cast<int>(s->chatMsgs.size())) return;
	s->chatMsgs[index].content = std::string(message);
	RetokenizeChat();
}
void RemoveMessageAt(const int index){
	auto* s = CurrSession();
	if(!s) return;
	if(index<0||index>=static_cast<int>(s->chatMsgs.size())) return;
	s->chatMsgs.erase(s->chatMsgs.begin()+index);
	RetokenizeChat();
}
void RemoveMessagesStartingAt(int index){
	auto* s = CurrSession();
	if(!s) return;
	if(index<0) index = 0;
	if(index>static_cast<int>(s->chatMsgs.size())) index = static_cast<int>(s->chatMsgs.size());
	s->chatMsgs.erase(s->chatMsgs.begin()+index, s->chatMsgs.end());
	RetokenizeChat();
}
std::string Generate(const HWND hWnd, const std::string role, const std::string& prompt, const unsigned int nPredict, const bool callback){
	auto* s = CurrSession();
	if(!s) return std::string();
	const auto prepStart = HrClock::now();
	_stop.store(false);
	const TokenCallbackFn cb = _tokenCb;
	std::string response;
	common_chat_msg newMsg;
	newMsg.role = role;
	newMsg.content = prompt;
	const auto formatted = common_chat_format_single(_chatTemplates.get(), s->chatMsgs, newMsg, !role._Equal("assistant"), s->useJinja&&role._Equal("assistant"));
	const int nPromptTokens = -llama_tokenize(_vocab, formatted.c_str(), formatted.size(), nullptr, 0, false, true);
	std::vector<llama_token> promptTokens(nPromptTokens);
	if(llama_tokenize(_vocab, formatted.c_str(), formatted.size(), promptTokens.data(), promptTokens.size(), false, true)<0){
		MessageBoxA(hWnd, "Failed to tokenize the prompt", "LM Stud", MB_ICONERROR);
		return std::string();
	}
	AddMessage(role, prompt);
	llama_token newTokenId;
	std::vector<llama_token> newTokens;
	const auto llMem = llama_get_memory(s->ctx);
	double ftTime = 0.0;
	int i = 0;
	size_t p = 0;
	while(p<promptTokens.size()){
		const int nEval = std::min<int>(s->nBatch, promptTokens.size()-p);
		llama_batch batch = llama_batch_get_one(&promptTokens[p], nEval);
		const int nCtx = llama_n_ctx(s->ctx);
		const int nCtxUsed = llama_memory_seq_pos_max(llMem, 0);
		if(nCtxUsed+batch.n_tokens>nCtx){
			MessageBoxA(hWnd, "Context size exceeded", "LM Stud", MB_ICONEXCLAMATION);
			return std::string();
		}
		auto result = llama_decode(s->ctx, batch);
		if(result!=0){
			MessageBoxA(hWnd, (std::string("llama_decode failed with error number: ")+std::to_string(result)).c_str(), "LM Stud", MB_ICONERROR);
			return std::string();
		}
		for(int j = 0; j<nEval; ++j){ llama_sampler_accept(s->smpl, promptTokens[p+j]); }
		p += nEval;
	}
	while(i<nPredict&&!_stop.load()){
		newTokenId = llama_sampler_sample(s->smpl, s->ctx, -1);
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
		const int nCtx = llama_n_ctx(s->ctx);
		const int nCtxUsed = llama_memory_seq_pos_max(llMem, 0);
		if(nCtxUsed+batch.n_tokens>nCtx){
			MessageBoxA(hWnd, "Context size exceeded", "LM Stud", MB_ICONEXCLAMATION);
			return std::string();
		}
		auto result = llama_decode(s->ctx, batch);
		if(result!=0){
			MessageBoxA(hWnd, (std::string("llama_decode failed with error number: ")+std::to_string(result)).c_str(), "LM Stud", MB_ICONERROR);
			return std::string();
		}
		llama_sampler_accept(s->smpl, newTokenId);
	}
	AddMessage("assistant", response);
	s->cachedTokens.insert(s->cachedTokens.end(), promptTokens.begin(), promptTokens.end());
	s->cachedTokens.insert(s->cachedTokens.end(), newTokens.begin(), newTokens.end());
	if(cb&&!callback) cb(response.c_str(), response.length(), i, llama_memory_seq_pos_max(llMem, 0), ftTime, 0);
	return response;
}
int GenerateWithTools(const HWND hWnd, const MessageRole role, char* prompt, const unsigned int nGen, const bool callback){
	const auto* s = CurrSession();
	if(!s) return 0;
	auto promptStr = std::string(prompt);
	if(!_hasTools) return Generate(hWnd, "user", promptStr, nGen, callback).length();
	std::string response;
	bool toolCalled = role==MessageRole::Tool;
	const TokenCallbackFn cb = _tokenCb;
	const auto llMem = llama_get_memory(s->ctx);
	auto StripThink = [](const std::string& text){
		std::string out;
		out.reserve(text.size());
		bool inThink = false;
		for(size_t i = 0; i<text.size(); ++i){
			if(!inThink&&_strnicmp(text.c_str()+i, "<think>", 7)==0){
				inThink = true;
				i += 6;
				continue;
			}
			if(inThink&&_strnicmp(text.c_str()+i, "</think>", 8)==0){
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
		if(!s->chatMsgs.size()) return response.length();
		common_chat_syntax syntax;
		syntax.format = s->chatFormat;
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
//char* GetContextAsText(){
//	if(!_ctx) return nullptr;
//	std::string outStr;
//	outStr.reserve(_cachedTokens.size()*4);
//	for(const llama_token tok : _cachedTokens){ outStr += common_token_to_piece(_ctx, tok, true); }
//	auto* out = static_cast<char*>(std::malloc(outStr.size()+1));
//	if(out) std::memcpy(out, outStr.c_str(), outStr.size()+1);
//	return out;
//}
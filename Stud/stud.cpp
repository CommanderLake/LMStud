#include "stud.h"
#include "hug.h"
#include <filesystem>
#include <chrono>
#include <regex>
#include <unordered_map>
#include <minja\minja.hpp>
#include <minja\chat-template.hpp>
using HrClock = std::chrono::high_resolution_clock;
void SetHWnd(HWND hWnd){ _hWnd = hWnd; }
void BackendInit(){
	_putenv("OMP_PROC_BIND=close");
	ggml_backend_load("ggml-cpu.dll");
	const HMODULE hModule = LoadLibraryA("nvcuda.dll");
	if(hModule != nullptr) ggml_backend_load("ggml-cuda.dll");
	llama_backend_init();
}
int CreateContext(const int nCtx, const int nBatch, const bool flashAttn, const int nThreads, const int nThreadsBatch){
	if(_session.ctx){
		llama_free(_session.ctx);
		_session.ctx = nullptr;
	}
	auto ctxParams = llama_context_default_params();
	ctxParams.n_ctx = nCtx;
	ctxParams.n_batch = nBatch;
	ctxParams.flash_attn = flashAttn;
	ctxParams.n_threads = nThreads;
	ctxParams.n_threads_batch = nThreadsBatch;
	_session.ctx = llama_init_from_model(_llModel, ctxParams);
	if(!_session.ctx) MessageBoxA(_hWnd, "Unable to create context", "LM Stud", MB_ICONERROR);
	RetokenizeChat(true);
	return _session.ctx ? 0 : -1;
}
int CreateSampler(const float minP, const float topP, const int topK, const float temp, const float repeatPenalty){
	if(_session.smpl){
		llama_sampler_free(_session.smpl);
		_session.smpl = nullptr;
	}
	_session.smpl = llama_sampler_chain_init(llama_sampler_chain_default_params());
	if(!_session.smpl){
		MessageBoxA(_hWnd, "Unable to create sampler", "LM Stud", MB_ICONERROR);
		return -2;
	}
	llama_sampler_chain_add(_session.smpl, llama_sampler_init_penalties(128, repeatPenalty, 0.0f, 0.0f));
	// Optional: DRY (sequence) penalty immediately after penalties
	// llama_sampler_chain_add(_session.smpl, llama_sampler_init_dry(0.8f, 1.8f, -1));
	if(topK > 0) llama_sampler_chain_add(_session.smpl, llama_sampler_init_top_k(topK));
	if(topP < 1.0f) llama_sampler_chain_add(_session.smpl, llama_sampler_init_top_p(topP, 1));
	if(minP > 0.0f) llama_sampler_chain_add(_session.smpl, llama_sampler_init_min_p(minP, 1));
	llama_sampler_chain_add(_session.smpl, llama_sampler_init_temp(temp));
	llama_sampler_chain_add(_session.smpl, llama_sampler_init_dist(LLAMA_DEFAULT_SEED));

	return 0;
}
int CreateSession(const int nCtx, const int nBatch, const bool flashAttn, const int nThreads, const int nThreadsBatch, const float minP, const float topP, const int topK, const float temp, const float repeatPenalty){
	if(!_llModel) return -1;
	_session.syntax.reasoning_format = COMMON_REASONING_FORMAT_DEEPSEEK;
	auto result = CreateContext(nCtx, nBatch, flashAttn, nThreads, nThreadsBatch);
	if(result != 0) return result;
	_session.nBatch = nBatch;
	result = CreateSampler(minP, topP, topK, temp, repeatPenalty);
	if(result != 0) return result;
	return 0;
}
void DestroySession(){
	if(_session.smpl){
		llama_sampler_free(_session.smpl);
		_session.smpl = nullptr;
	}
	if(_session.ctx){
		llama_free(_session.ctx);
		_session.ctx = nullptr;
	}
}
void FreeModel(){
	if(_session.smpl){
		llama_sampler_free(_session.smpl);
		_session.smpl = nullptr;
	}
	if(_session.ctx){
		llama_free(_session.ctx);
		_session.ctx = nullptr;
	}
	_session.cachedTokens.clear();
	if(_llModel){
		llama_model_free(_llModel);
		_llModel = nullptr;
	}
}
int LoadModel(const char* filename, const int nGPULayers, const bool mMap, const bool mLock, const ggml_numa_strategy numaStrategy){
	auto params = llama_model_default_params();
	params.n_gpu_layers = nGPULayers;
	params.use_mlock = mLock;
	params.use_mmap = mMap;
	llama_numa_init(numaStrategy);
	_llModel = llama_model_load_from_file(filename, params);
	if(!_llModel){
		MessageBoxA(_hWnd, (std::string("Unable to load model:\n\n") + filename).c_str(), "LM Stud", MB_ICONERROR);
		return -1;
	}
	_vocab = llama_model_get_vocab(_llModel);
	_chatTemplates = common_chat_templates_init(_llModel, "");
	const auto bosStr = llama_vocab_get_text(_vocab, llama_vocab_bos(_vocab));
	const auto eosStr = llama_vocab_get_text(_vocab, llama_vocab_eos(_vocab));
	const std::string tmplSrc = llama_model_chat_template(_llModel, nullptr);
	if(!tmplSrc.empty()){
		const minja::chat_template tmpl(tmplSrc, bosStr, eosStr);
		_hasTools = tmpl.original_caps().supports_tools;
	}
	return 0;
}
bool HasTool(const char* name){
	for(const auto& tool : _tools){ if(tool.name._Equal(name)) return true; }
	return false;
}
void SetTokenCallback(const TokenCallbackFn cb){ _tokenCb = cb; }
void SetThreadCount(const int n, const int nBatch){
	if(_session.ctx) llama_set_n_threads(_session.ctx, n, nBatch);
}
void RetokenizeChat(bool rebuildMemory = false){
	if(!_session.ctx || !_session.smpl || !_vocab) return;
	std::vector<common_chat_msg> msgs;
	std::string prompt(_session.prompt);
	if(_hasTools && !_session.toolsPrompt.empty()) prompt += _session.toolsPrompt;
	msgs.push_back({"system", prompt});
	msgs.insert(msgs.end(), _session.chatMsgs.begin(), _session.chatMsgs.end());
	for(auto& msg : msgs) if(msg.content.empty()) msg.content = " ";
	common_chat_templates_inputs in;
	in.use_jinja = _session.useJinja;
	in.messages = msgs;
	in.add_generation_prompt = false;
	in.tools = _tools;
	in.tool_choice = COMMON_CHAT_TOOL_CHOICE_AUTO;
	in.parallel_tool_calls = true;
	in.reasoning_format = COMMON_REASONING_FORMAT_DEEPSEEK;
	common_chat_params chatData;
	try{
		chatData = common_chat_templates_apply(_chatTemplates.get(), in);
	} catch(std::exception& e){
		MessageBoxA(_hWnd, ("Failed to apply template, chat state inconsistent!\n\n" + std::string(e.what())).c_str(), "LM Stud", MB_ICONERROR);
		return;
	}
	_session.syntax.format = chatData.format;
	const int nPrompt = -llama_tokenize(_vocab, chatData.prompt.c_str(), chatData.prompt.size(), nullptr, 0, true, true);
	std::vector<llama_token> promptTokens(nPrompt);
	llama_tokenize(_vocab, chatData.prompt.c_str(), chatData.prompt.size(), promptTokens.data(), promptTokens.size(), true, true);
	size_t prefix = 0;
	while(prefix < _session.cachedTokens.size() && prefix < promptTokens.size() && _session.cachedTokens[prefix] == promptTokens[prefix]){
		++prefix;
	}
	llama_memory_t mem = llama_get_memory(_session.ctx);
	const bool canShift = llama_memory_can_shift(mem);
	if(rebuildMemory){
		if(llama_memory_seq_pos_max(mem, 0) < static_cast<llama_pos>(prefix - 1)){
			prefix = 0;
			llama_memory_clear(mem, true);
		}
	}
	size_t suffix = 0;
	if(canShift){
		const size_t oldSz = _session.cachedTokens.size();
		const size_t newSz = promptTokens.size();
		while(suffix + prefix < oldSz && suffix + prefix < newSz &&
			_session.cachedTokens[oldSz - 1 - suffix] == promptTokens[newSz - 1 - suffix]){
			++suffix;
		}
	}
	const size_t oldSize = _session.cachedTokens.size();
	const size_t newSize = promptTokens.size();
	if(canShift && suffix > 0){
		if(prefix < oldSize - suffix){
			llama_memory_seq_rm(mem, 0, prefix, oldSize - suffix);
		}
		if(oldSize != newSize){
			const int delta = static_cast<int>(newSize) - static_cast<int>(oldSize);
			llama_memory_seq_add(mem, 0, oldSize - suffix, -1, delta);
		}
	} else{
		suffix = 0;
		if(prefix < oldSize){
			llama_memory_seq_rm(mem, 0, prefix, -1);
		} else if(prefix == 0){
			llama_memory_clear(mem, true);
		}
	}
	llama_sampler_reset(_session.smpl);
	for(size_t i = 0; i < prefix; ++i){ llama_sampler_accept(_session.smpl, promptTokens[i]); }
	const size_t decodeEnd = newSize - suffix;
	for(size_t i = prefix; i < decodeEnd; i += _session.nBatch){
		const int nEval = std::min<int>(_session.nBatch, decodeEnd - i);
		auto batch = llama_batch_get_one(&promptTokens[i], nEval);
		if(llama_decode(_session.ctx, batch) != 0){ return; }
		for(int j = 0; j < nEval; ++j){ llama_sampler_accept(_session.smpl, promptTokens[i + j]); }
	}
	for(size_t i = decodeEnd; i < newSize; ++i){ llama_sampler_accept(_session.smpl, promptTokens[i]); }
	_session.cachedTokens = std::move(promptTokens);
}
void ResetChat(){
	_session.chatMsgs.clear();
	RetokenizeChat(true);
}
void SetSystemPrompt(const char* prompt, const char* toolsPrompt){
	_session.prompt = std::string(prompt);
	_session.toolsPrompt = std::string(toolsPrompt);
	RetokenizeChat();
}
void SetMessageAt(const int index, const char* think, const char* message){
	if(index < 0 || index >= static_cast<int>(_session.chatMsgs.size())) return;
	_session.chatMsgs[index].reasoning_content = think;
	_session.chatMsgs[index].content = std::string(message);
	RetokenizeChat();
}
void RemoveMessageAt(const int index){
	if(index < 0 || index >= static_cast<int>(_session.chatMsgs.size())) return;
	_session.chatMsgs.erase(_session.chatMsgs.begin() + index);
	RetokenizeChat();
}
void RemoveMessagesStartingAt(int index){
	if(index < 0) index = 0;
	if(index > static_cast<int>(_session.chatMsgs.size())) index = static_cast<int>(_session.chatMsgs.size());
	_session.chatMsgs.erase(_session.chatMsgs.begin() + index, _session.chatMsgs.end());
	RetokenizeChat();
}
static std::string OpenToolResponseTag(){
	switch(_session.syntax.format){
		case COMMON_CHAT_FORMAT_DEEPSEEK_R1: return "<｜tool▁outputs▁begin｜>";
		case COMMON_CHAT_FORMAT_COMMAND_R7B: return "<|START_RESPONSE|>";
		case COMMON_CHAT_FORMAT_LLAMA_3_X:
		case COMMON_CHAT_FORMAT_LLAMA_3_X_WITH_BUILTIN_TOOLS: return "<|start_header_id|>ipython<|end_header_id|>";
		default: return "<tool_response>";
	}
}
static std::string CloseToolResponseTag(){
	switch(_session.syntax.format){
		case COMMON_CHAT_FORMAT_DEEPSEEK_R1: return "<｜tool▁outputs▁end｜>";
		case COMMON_CHAT_FORMAT_COMMAND_R7B: return "<|END_RESPONSE|>";
		case COMMON_CHAT_FORMAT_LLAMA_3_X:
		case COMMON_CHAT_FORMAT_LLAMA_3_X_WITH_BUILTIN_TOOLS: return "<|eot_id|>";
		default: return "</tool_response>";
	}
}

static std::string OpenToolCallTag(){
	switch(_session.syntax.format){
		case COMMON_CHAT_FORMAT_DEEPSEEK_R1: return "<｜tool▁call▁begin｜>";
		case COMMON_CHAT_FORMAT_COMMAND_R7B: return "<|START_ACTION|>";
		case COMMON_CHAT_FORMAT_MISTRAL_NEMO: return "[TOOL_CALLS]";
		case COMMON_CHAT_FORMAT_FIREFUNCTION_V2: return "functools[";
		case COMMON_CHAT_FORMAT_FUNCTIONARY_V3_1_LLAMA_3_1: return "<function=";
		default: return "<tool_call>";
	}
}
static std::string CloseToolCallTag(){
	switch(_session.syntax.format){
		case COMMON_CHAT_FORMAT_DEEPSEEK_R1: return "<｜tool▁call▁end｜>";
		case COMMON_CHAT_FORMAT_COMMAND_R7B: return "<|END_ACTION|>";
		case COMMON_CHAT_FORMAT_FIREFUNCTION_V2: return "]";
		case COMMON_CHAT_FORMAT_FUNCTIONARY_V3_1_LLAMA_3_1: return "</function>";
		case COMMON_CHAT_FORMAT_MISTRAL_NEMO: return "";
		default: return "</tool_call>";
	}
}

static std::string OpenThinkTag(){
	switch(_session.syntax.format){
		case COMMON_CHAT_FORMAT_COMMAND_R7B: return "<|START_THINKING|>";
		case COMMON_CHAT_FORMAT_LLAMA_3_X:
		case COMMON_CHAT_FORMAT_LLAMA_3_X_WITH_BUILTIN_TOOLS: return "<|start_header_id|>analysis<|end_header_id|>";
		default: return "<think>";
	}
}
static std::string CloseThinkTag(){
	switch(_session.syntax.format){
		case COMMON_CHAT_FORMAT_COMMAND_R7B: return "<|END_THINKING|>";
		case COMMON_CHAT_FORMAT_LLAMA_3_X:
		case COMMON_CHAT_FORMAT_LLAMA_3_X_WITH_BUILTIN_TOOLS: return "<|eot_id|>";
		default: return "</think>";
	}
}
static bool doTool(std::string_view tok, ToolCtx& s, const bool cbOn, double& ftTime, const HrClock::time_point& t0, llama_memory_t llMem, std::string& response, std::vector<llama_token>& newTokens, const TokenCallbackFn cb) {
	if(tok == OpenThinkTag()){
		s.inThink = true;
		return true;
	}
	if(tok == CloseThinkTag()){
		s.inThink = false;
		return true;
	}
	if(!_hasTools || !s.inThink) return false;
	if(tok == OpenToolCallTag()){
		s.inCall = true;
		s.buf = tok;
		return true;
	}
	if(s.inCall){
		if(tok != CloseToolCallTag() && tok != OpenToolResponseTag()){
			s.buf += tok;
			return true;
		}
		if(tok == CloseToolCallTag()) s.buf += CloseToolCallTag();
		s.inCall = false;
		_session.syntax.parse_tool_calls = true;
		auto p = common_chat_parse(s.buf, true, _session.syntax);
		s.buf.clear();
		if(p.tool_calls.empty()) return true;
		auto& c = p.tool_calls.back();
		if(auto h = _toolHandlers.find(c.name); h != _toolHandlers.end()){
			if(tok == CloseToolCallTag()){
				std::string open = "\n" + OpenToolResponseTag();
				if(!open.empty()){
					int m = -llama_tokenize(_vocab, open.c_str(), open.size(), nullptr, 0, true, true);
					std::vector<llama_token> v2(m);
					llama_tokenize(_vocab, open.c_str(), open.size(), v2.data(), m, true, true);
					llama_batch lb = llama_batch_get_one(v2.data(), m);
					llama_decode(_session.ctx, lb);
					for(auto t2 : v2) llama_sampler_accept(_session.smpl, t2);
					newTokens.insert(newTokens.end(), v2.begin(), v2.end());
					response += open;
				}
			}
			std::string out = "\n" + h->second(c.arguments.c_str());
			if(!out.empty()){
				int n = -llama_tokenize(_vocab, out.c_str(), out.size(), nullptr, 0, true, true);
				std::vector<llama_token> v(n);
				llama_tokenize(_vocab, out.c_str(), out.size(), v.data(), n, true, true);
				for(size_t i = 0; i < v.size();){
					int b = std::min<int>(_session.nBatch, v.size() - i);
					llama_batch lb = llama_batch_get_one(&v[i], b);
					llama_decode(_session.ctx, lb);
					for(int k = 0; k < b; ++k) llama_sampler_accept(_session.smpl, v[i + k]);
					i += b;
				}
				newTokens.insert(newTokens.end(), v.begin(), v.end());
				response += out;
			}
			std::string close = "\n" + CloseToolResponseTag();
			if(!close.empty()){
				int m = -llama_tokenize(_vocab, close.c_str(), close.size(), nullptr, 0, true, true);
				std::vector<llama_token> v2(m);
				llama_tokenize(_vocab, close.c_str(), close.size(), v2.data(), m, true, true);
				llama_batch lb = llama_batch_get_one(v2.data(), m);
				llama_decode(_session.ctx, lb);
				for(auto t2 : v2) llama_sampler_accept(_session.smpl, t2);
				newTokens.insert(newTokens.end(), v2.begin(), v2.end());
				response += close;
			}
		}
		if(_tokenCb && cbOn){
			_session.syntax.parse_tool_calls = false;
			auto msg = common_chat_parse(response, true, _session.syntax);
			cb(msg.reasoning_content.c_str(), static_cast<int>(msg.reasoning_content.length()), msg.content.c_str(), static_cast<int>(msg.content.length()), 1, llama_memory_seq_pos_max(llMem, 0), ftTime, 0);
			return true;
		}
	}
	return false;
}
common_chat_msg Generate(const std::vector<common_chat_msg> messages, const int nPredict, const bool callback){
	const auto prepStart = HrClock::now();
	_stop.store(false);
	const TokenCallbackFn cb = _tokenCb;
	const auto llMem = llama_get_memory(_session.ctx);
	const size_t chatStart = _session.chatMsgs.size();
	for(auto& message : messages){
		const auto formatted = common_chat_format_single(_chatTemplates.get(), _session.chatMsgs, message, !message.role._Equal("assistant"), _session.useJinja && message.role._Equal("assistant"));
		const int nPromptTokens = -llama_tokenize(_vocab, formatted.c_str(), formatted.size(), nullptr, 0, true, true);
		std::vector<llama_token> promptTokens(nPromptTokens);
		if(llama_tokenize(_vocab, formatted.c_str(), formatted.size(), promptTokens.data(), promptTokens.size(), true, true) < 0){
			MessageBoxA(_hWnd, "Failed to tokenize the prompt", "LM Stud", MB_ICONERROR);
			return common_chat_msg();
		}
		_session.chatMsgs.push_back(message);
		size_t p = 0;
		while(p < promptTokens.size() && !_stop.load()){
			const int nEval = std::min<int>(_session.nBatch, promptTokens.size() - p);
			llama_batch batch = llama_batch_get_one(&promptTokens[p], nEval);
			const int nCtx = llama_n_ctx(_session.ctx);
			const int nCtxUsed = llama_memory_seq_pos_max(llMem, 0);
			if(nCtxUsed + batch.n_tokens > nCtx){
				MessageBoxA(_hWnd, "Context size exceeded", "LM Stud", MB_ICONEXCLAMATION);
				_session.chatMsgs.pop_back();
				RetokenizeChat(true);
				return common_chat_msg();
			}
			auto result = llama_decode(_session.ctx, batch);
			if(result != 0){
				MessageBoxA(_hWnd, (std::string("llama_decode failed with error number: ") + std::to_string(result)).c_str(), "LM Stud", MB_ICONERROR);
				_session.chatMsgs.pop_back();
				RetokenizeChat(true);
				return common_chat_msg();
			}
			for(int j = 0; j < nEval; ++j){ llama_sampler_accept(_session.smpl, promptTokens[p + j]); }
			p += nEval;
		}
		_session.cachedTokens.insert(_session.cachedTokens.end(), promptTokens.begin(), promptTokens.end());
	}
	llama_token newTokenId;
	std::vector<llama_token> newTokens;
	std::string response;
	common_chat_msg msg;
	bool insideThink = false, insideCall = false;
	std::string pendingJson;
	ToolCtx tool;
	double ftTime = 0.0;
	int i = 0;
	while((nPredict < 0 || i < nPredict) && !_stop.load()){
		newTokenId = llama_sampler_sample(_session.smpl, _session.ctx, -1);
		if(llama_vocab_is_eog(_vocab, newTokenId)){
			_stop.store(true);
		}
		char buf[256];
		const int n = llama_token_to_piece(_vocab, newTokenId, buf, sizeof buf, 0, false);
		if(n < 0){
			MessageBoxA(_hWnd, "Failed to convert token to piece", "LM Stud", MB_ICONERROR);
			_session.chatMsgs.resize(chatStart + messages.size());
			RetokenizeChat(true);
			return msg;
		}
		newTokens.push_back(newTokenId);
		if(ftTime == 0.0) ftTime = std::chrono::duration<double, std::ratio<1, 1>>(HrClock::now() - prepStart).count();
		std::string tokenStr(buf, n);
		response += tokenStr;
		++i;
		if(cb && callback && !tokenStr.empty()){
			_session.syntax.parse_tool_calls = false;
			msg = common_chat_parse(response, true, _session.syntax);
			cb(msg.reasoning_content.c_str(), static_cast<int>(msg.reasoning_content.length()), msg.content.c_str(), static_cast<int>(msg.content.length()), 1, llama_memory_seq_pos_max(llMem, 0), ftTime, 0);
		}
		auto batch = llama_batch_get_one(&newTokenId, 1);
		const int nCtx = llama_n_ctx(_session.ctx);
		const int nCtxUsed = llama_memory_seq_pos_max(llMem, 0);
		if(nCtxUsed + batch.n_tokens > nCtx){
			MessageBoxA(_hWnd, "Context size exceeded", "LM Stud", MB_ICONEXCLAMATION);
			_session.chatMsgs.resize(chatStart + messages.size());
			RetokenizeChat(true);
			return msg;
		}
		auto result = llama_decode(_session.ctx, batch);
		if(result != 0){
			if(result == 1) MessageBoxA(_hWnd, "Context full", "LM Stud", MB_ICONEXCLAMATION);
			else MessageBoxA(_hWnd, (std::string("llama_decode failed with error number: ") + std::to_string(result)).c_str(), "LM Stud", MB_ICONERROR);
			_session.chatMsgs.resize(chatStart + messages.size());
			RetokenizeChat(true);
			return msg;
		}
		llama_sampler_accept(_session.smpl, newTokenId);
		if(_hasTools) doTool(tokenStr, tool, callback, ftTime, prepStart, llMem, response, newTokens, cb);
	}
	_session.syntax.parse_tool_calls = false;
	msg = common_chat_parse(response, false, _session.syntax);
	_session.chatMsgs.push_back(msg);
	_session.cachedTokens.insert(_session.cachedTokens.end(), newTokens.begin(), newTokens.end());
	if(cb && !callback) cb(msg.reasoning_content.c_str(), static_cast<int>(msg.reasoning_content.length()), msg.content.c_str(), static_cast<int>(msg.content.length()), i, llama_memory_seq_pos_max(llMem, 0), ftTime, 0);
	//OutputDebugStringA(("\n---\n" + std::string(GetContextAsText()) + "\n---\n").c_str());
	return msg;
}
void GenerateWithTools(const MessageRole role, const char* prompt, const int nPredict, const bool callback){
	common_chat_msg msg;
	switch(role){
		case MessageRole::User: msg.role = "user"; break;
		case MessageRole::Assistant: msg.role = "assistant"; break;
		case MessageRole::Tool: msg.role = "tool"; break;
	}
	msg.content = std::string(prompt);
	std::vector<common_chat_msg> msgs{msg};
	if(!_hasTools){
		Generate(msgs, nPredict, callback);
		return;
	}
	bool toolCalled = role == MessageRole::Tool;
	const TokenCallbackFn cb = _tokenCb;
	const auto llMem = llama_get_memory(_session.ctx);
	do{
		msg = Generate(msgs, nPredict, callback);
		toolCalled = false;
		if(!_session.chatMsgs.size()) return;
		msgs.clear();
		_session.syntax.parse_tool_calls = true;
		try{
			std::string rest = msg.content;
			while(true){
				const auto parsed = common_chat_parse(rest, false, _session.syntax);
				rest = parsed.content;
				msg.tool_calls.insert(msg.tool_calls.end(), parsed.tool_calls.begin(), parsed.tool_calls.end());
				if(parsed.tool_calls.empty()) break;
			}
			msg.content = "";
			msg.reasoning_content = "";
			for(auto& tool : msg.tool_calls){
				auto it = _toolHandlers.find(tool.name);
				if(it != _toolHandlers.end()){
					auto toolMsg = it->second(tool.arguments.c_str());
					if(cb) cb(nullptr, 0, toolMsg.c_str(), static_cast<int>(toolMsg.length()), 0, llama_memory_seq_pos_max(llMem, 0), 0, 1);
					msgs.push_back(common_chat_msg());
					msgs.back().role = "tool";
					msgs.back().content = toolMsg;
					toolCalled = true;
				}
			}
		} catch(std::exception& e){ MessageBoxA(nullptr, e.what(), "LM Stud", MB_ICONEXCLAMATION); }
	} while(toolCalled);
}
void StopGeneration(){ _stop.store(true); }
char* GetContextAsText(){
	if(!_session.ctx) return nullptr;
	std::string outStr;
	outStr.reserve(_session.cachedTokens.size()*4);
	for(const llama_token tok : _session.cachedTokens){ outStr += common_token_to_piece(_session.ctx, tok, true); }
	auto* out = static_cast<char*>(std::malloc(outStr.size()+1));
	if(out) std::memcpy(out, outStr.c_str(), outStr.size()+1);
	return out;
}
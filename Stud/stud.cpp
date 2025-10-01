#include "stud.h"
#include "hug.h"
#include <filesystem>
#include <chrono>
#include <regex>
#include <unordered_map>
#include <minja\minja.hpp>
#include <minja\chat-template.hpp>
#include <algorithm>
using HrClock = std::chrono::high_resolution_clock;
static bool _gpuOomStud = false;
static std::string _lastErrorMessage;
extern "C" EXPORT const char* GetLastErrorMessage(){ return _lastErrorMessage.c_str(); }
extern "C" EXPORT void ClearLastErrorMessage(){ _lastErrorMessage.clear(); }
static void GPUOomLogCallbackStud(ggml_log_level level, const char* text, void* userData){
	if(level == GGML_LOG_LEVEL_ERROR || level == GGML_LOG_LEVEL_WARN){
		const std::string_view msg(text);
		if(msg.find("out of memory") != std::string_view::npos) _gpuOomStud = true;
	}
}
void SetHWnd(HWND hWnd){ _hWnd = hWnd; }
void BackendInit(){
	_putenv("OMP_PROC_BIND=close");
	ggml_backend_load("ggml-cpu.dll");
	const HMODULE hModule = LoadLibraryA("nvcuda.dll");
	if(hModule != nullptr) ggml_backend_load("ggml-cuda.dll");
	llama_backend_init();
}
void AddTool(const char* name, const char* description, const char* parameters, std::string(*handler)(const char* args)){
	if(!name || !_hasTools) return;
	common_chat_tool tool;
	tool.name = name;
	if(description) tool.description = description;
	if(parameters) tool.parameters = parameters;
	_tools.push_back(tool);
	if(handler) _toolHandlers[name] = handler;
}
void ClearTools(){
	_tools.clear();
	_toolHandlers.clear();
}
StudError CreateContext(const int nCtx, const int nBatch, const bool flashAttn, const int nThreads, const int nThreadsBatch){
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
	_gpuOomStud = false;
	llama_log_set(GPUOomLogCallbackStud, nullptr);
	_session.ctx = llama_init_from_model(_llModel, ctxParams);
	llama_log_set(nullptr, nullptr);
	if(!_session.ctx){ return _gpuOomStud ? StudError::GpuOutOfMemory : StudError::CantCreateContext; }
	_session.llMem = llama_get_memory(_session.ctx);
	auto result = StudError::Success;
	if(_session.smpl[0]) result = RetokenizeChat(true);
	return result;
}
StudError CreateSamplerInternal(const float minP, const float topP, const int topK, const float temp, const float repeatPenalty, llama_sampler* & smpl){
	if(smpl){
		llama_sampler_free(smpl);
		smpl = nullptr;
	}
	smpl = llama_sampler_chain_init(llama_sampler_chain_default_params());
	if(!smpl){ return StudError::CantCreateSampler; }
	llama_sampler_chain_add(smpl, llama_sampler_init_penalties(128, repeatPenalty, 0.0f, 0.0f));
	// Optional: DRY (sequence) penalty immediately after penalties
	// llama_sampler_chain_add(_session.smpl, llama_sampler_init_dry(0.8f, 1.8f, -1));
	if(topK > 0) llama_sampler_chain_add(smpl, llama_sampler_init_top_k(topK));
	if(topP < 1.0f) llama_sampler_chain_add(smpl, llama_sampler_init_top_p(topP, 1));
	llama_sampler_chain_add(smpl, llama_sampler_init_temp(temp));
	llama_sampler_chain_add(smpl, llama_sampler_init_dist(LLAMA_DEFAULT_SEED));
	if(minP > 0.0f) llama_sampler_chain_add(smpl, llama_sampler_init_min_p(minP, 1));
	return StudError::Success;
}
StudError CreateSampler(const float minP, const float topP, const int topK, const float temp, const float repeatPenalty){
	const auto result = CreateSamplerInternal(minP, topP, topK, temp, repeatPenalty, _session.smpl[0]);
	if(result != StudError::Success) return result;
	return CreateSamplerInternal(minP, topP, topK, temp, repeatPenalty, _session.smpl[1]);
}
StudError CreateSession(const int nCtx, const int nBatch, const bool flashAttn, const int nThreads, const int nThreadsBatch, const float minP, const float topP, const int topK, const float temp, const float repeatPenalty){
	if(!_llModel) return StudError::ModelNotLoaded;
	_session.syntax.reasoning_format = COMMON_REASONING_FORMAT_DEEPSEEK;
	auto result = CreateContext(nCtx, nBatch, flashAttn, nThreads, nThreadsBatch);
	if(result != StudError::Success) return result;
	_session.nBatch = nBatch;
	result = CreateSampler(minP, topP, topK, temp, repeatPenalty);
	if(result != StudError::Success) return result;
	return StudError::Success;
}
void DestroySession(){
	if(_session.smpl[0]){
		llama_sampler_free(_session.smpl[0]);
		_session.smpl[0] = nullptr;
	}
	if(_session.smpl[1]){
		llama_sampler_free(_session.smpl[1]);
		_session.smpl[1] = nullptr;
	}
	if(_session.ctx){
		llama_free(_session.ctx);
		_session.ctx = nullptr;
	}
	DialecticFree();
}
void FreeModel(){
	DestroySession();
	_session.cachedTokens[0].clear();
	_session.cachedTokens[1].clear();
	if(_llModel){
		llama_model_free(_llModel);
		_llModel = nullptr;
	}
}
StudError LoadModel(const char* filename, const char* jinjaTemplate, const int nGPULayers, const bool mMap, const bool mLock, const ggml_numa_strategy numaStrategy){
	auto params = llama_model_default_params();
	params.n_gpu_layers = nGPULayers;
	params.use_mlock = mLock;
	params.use_mmap = mMap;
	llama_numa_init(numaStrategy);
	_gpuOomStud = false;
	llama_log_set(GPUOomLogCallbackStud, nullptr);
	_llModel = llama_model_load_from_file(filename, params);
	llama_log_set(nullptr, nullptr);
	if(!_llModel){ return _gpuOomStud ? StudError::GpuOutOfMemory : StudError::CantLoadModel; }
	_vocab = llama_model_get_vocab(_llModel);
	const auto bosStr = llama_vocab_get_text(_vocab, llama_vocab_bos(_vocab));
	const auto eosStr = llama_vocab_get_text(_vocab, llama_vocab_eos(_vocab));
	std::string tmplSrc;
	if(jinjaTemplate && jinjaTemplate[0] != '\0'){
		_chatTemplates = common_chat_templates_init(_llModel, jinjaTemplate, bosStr, eosStr);
		tmplSrc = jinjaTemplate;
	} else{
		_chatTemplates = common_chat_templates_init(_llModel, "");
		tmplSrc = llama_model_chat_template(_llModel, nullptr);
	}
	_hasTools = false;
	if(!tmplSrc.empty()){
		try{
			const minja::chat_template tmpl(tmplSrc, bosStr, eosStr);
			_hasTools = tmpl.original_caps().supports_tools;
			_session.useJinja = true;
		} catch(...){ _session.useJinja = false; }
	} else{ _session.useJinja = false; }
	return StudError::Success;
}
bool HasTool(const char* name){
	for(const auto& tool : _tools){ if(tool.name._Equal(name)) return true; }
	return false;
}
void SetTokenCallback(const TokenCallbackFn cb){ _tokenCb = cb; }
void SetThreadCount(const int n, const int nBatch){ if(_session.ctx) llama_set_n_threads(_session.ctx, n, nBatch); }
int LlamaMemSize(){
	const int nCtxPosMin = llama_memory_seq_pos_min(_session.llMem, 0);
	const int nCtxPosMax = llama_memory_seq_pos_max(_session.llMem, 0);
	return nCtxPosMax - nCtxPosMin + 1;
}
int GetStateSize(){
	if(!_session.ctx) return 0;
	return static_cast<int>(llama_state_get_size(_session.ctx));
}
void GetStateData(unsigned char* dst, int size){ if(_session.ctx) llama_state_get_data(_session.ctx, dst, size); }
void SetStateData(const unsigned char* src, int size){ if(_session.ctx) llama_state_set_data(_session.ctx, src, size); }
void DialecticInit(){
	const int size = GetStateSize();
	if(size <= 0) return;
	_session.dialState[0].assign(size, 0);
	_session.dialState[1].assign(size, 0);
	GetStateData(_session.dialState[0].data(), size);
	GetStateData(_session.dialState[1].data(), size);
	_session.dId = 0;
}
void DialecticStart(){
	if(_session.dialState[0].empty()) return;
	const int size = static_cast<int>(_session.dialState[0].size());
	SetStateData(_session.dialState[_session.dId].data(), size);
}
StudError DialecticSwap(){
	if(_session.dialState[0].empty()) return StudError::Success;
	const int size = static_cast<int>(_session.dialState[0].size());
	GetStateData(_session.dialState[_session.dId].data(), size);
	_session.dId = 1 - _session.dId;
	SetStateData(_session.dialState[_session.dId].data(), size);
	return RetokenizeChat(true);
}
void DialecticFree(){
	_session.dialState[0].clear();
	_session.dialState[1].clear();
}
std::string RoleString(const MessageRole role){
	switch(role){
		case MessageRole::User: return std::string("user");
		case MessageRole::Assistant: return std::string("assistant");
		case MessageRole::Tool: return std::string("tool");
		default: return std::string();
	}
}
StudError RetokenizeChat(bool rebuildMemory = false){
	if(!_session.ctx || !_session.smpl[_session.dId] || !_vocab) return StudError::ModelNotLoaded;
	std::vector<common_chat_msg> msgs;
	std::string prompt(_session.prompt);
	if(_hasTools && !_session.toolsPrompt.empty()) prompt += _session.toolsPrompt;
	msgs.push_back({"system", prompt});
	msgs.insert(msgs.end(), _session.chatMsgs[_session.dId].begin(), _session.chatMsgs[_session.dId].end());
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
	try{ chatData = common_chat_templates_apply(_chatTemplates.get(), in); } catch(std::exception& e){
		_lastErrorMessage = e.what();
		OutputDebugStringA((std::string("EXCEPTION:\r\n") + e.what()).c_str());
		return StudError::CantApplyTemplate;
	}
	_session.syntax.format = chatData.format;
	const int nPrompt = -llama_tokenize(_vocab, chatData.prompt.c_str(), chatData.prompt.size(), nullptr, 0, true, true);
	std::vector<llama_token> promptTokens(nPrompt);
	llama_tokenize(_vocab, chatData.prompt.c_str(), chatData.prompt.size(), promptTokens.data(), promptTokens.size(), true, true);
	size_t prefix = 0;
	while(prefix < _session.cachedTokens[_session.dId].size() && prefix < promptTokens.size() && _session.cachedTokens[_session.dId][prefix] == promptTokens[prefix]){ ++prefix; }
	const bool canShift = llama_memory_can_shift(_session.llMem);
	if(rebuildMemory){
		if(prefix == 0 || LlamaMemSize() < static_cast<llama_pos>(prefix - 1)){
			prefix = 0;
			llama_memory_clear(_session.llMem, true);
		}
	}
	const size_t oldSz = _session.cachedTokens[_session.dId].size();
	const size_t newSz = promptTokens.size();
	size_t suffix = 0;
	if(canShift && oldSz > 0 && newSz > 0){ while(suffix + prefix < oldSz && suffix + prefix < newSz && suffix < oldSz && suffix < newSz && _session.cachedTokens[_session.dId][oldSz - 1 - suffix] == promptTokens[newSz - 1 - suffix]){ ++suffix; } }
	const size_t oldSize = _session.cachedTokens[_session.dId].size();
	const size_t newSize = promptTokens.size();
	if(newSize > static_cast<size_t>(llama_n_ctx(_session.ctx))){ return StudError::ConvTooLong; }
	if(prefix == oldSize && oldSize == newSize) return StudError::Success;
	if(canShift && suffix > 0){
		if(prefix < oldSize - suffix){ llama_memory_seq_rm(_session.llMem, 0, prefix, oldSize - suffix); }
		if(oldSize != newSize){
			const int delta = static_cast<int>(newSize) - static_cast<int>(oldSize);
			llama_memory_seq_add(_session.llMem, 0, oldSize - suffix, -1, delta);
		}
	} else{
		suffix = 0;
		if(prefix < oldSize){ llama_memory_seq_rm(_session.llMem, 0, prefix, -1); }
	}
	llama_sampler_reset(_session.smpl[_session.dId]);
	for(size_t i = 0; i < prefix; ++i){ llama_sampler_accept(_session.smpl[_session.dId], promptTokens[i]); }
	const size_t decodeEnd = newSize - suffix;
	const int batchSize = std::min(_session.nBatch, static_cast<int>(decodeEnd - prefix));
	if(batchSize <= 0) return StudError::Success;
	for(size_t i = prefix; i < decodeEnd; i += _session.nBatch){
		const int nEval = std::min<int>(_session.nBatch, decodeEnd - i);
		auto batch = llama_batch_get_one(&promptTokens[i], nEval);
		if(llama_decode(_session.ctx, batch) != 0) return StudError::LlamaDecodeError;
		for(int j = 0; j < nEval; ++j){ llama_sampler_accept(_session.smpl[_session.dId], promptTokens[i + j]); }
	}
	for(size_t i = decodeEnd; i < newSize; ++i){ llama_sampler_accept(_session.smpl[_session.dId], promptTokens[i]); }
	_session.cachedTokens[_session.dId] = std::move(promptTokens);
	return StudError::Success;
}
static void AlignChatStates(){
	auto& a = _session.chatMsgs[0];
	auto& b = _session.chatMsgs[1];
	if(a.size() == b.size()) return;
	auto* longer = &a;
	auto* shorter = &b;
	if(a.size() < b.size()){
		longer = &b;
		shorter = &a;
	}
	for(size_t i = shorter->size(); i < longer->size(); ++i){
		auto msg = (*longer)[i];
		if(msg.role._Equal("assistant")){
			msg.role = "user";
			msg.reasoning_content.clear();
		} else if(msg.role._Equal("user")){ msg.role = "assistant"; }
		shorter->push_back(std::move(msg));
	}
}
StudError ResetChat(){
	_session.chatMsgs[0].clear();
	_session.chatMsgs[1].clear();
	_session.cachedTokens[0].clear();
	_session.cachedTokens[1].clear();
	if(!_session.ctx || !_session.smpl[_session.dId] || !_vocab){
		DialecticFree();
		return StudError::Success;
	}
	auto err = RetokenizeChat(true);
	if(err != StudError::Success || _session.dialState[_session.dId == 0 ? 1 : 0].empty()) return err;
	err = DialecticSwap();
	if(err != StudError::Success) return err;
	auto err2 = RetokenizeChat(true);
	auto err3 = DialecticSwap();
	return err2 != StudError::Success ? err2 : err3;
}
StudError SetSystemPrompt(const char* prompt, const char* toolsPrompt){
	_session.prompt = std::string(prompt);
	_session.toolsPrompt = std::string(toolsPrompt);
	_session.cachedTokens[0].clear();
	_session.cachedTokens[1].clear();
	if(!_session.ctx || !_session.smpl[_session.dId] || !_vocab) return StudError::Success;
	auto err = RetokenizeChat();
	if(err != StudError::Success || _session.dialState[_session.dId == 0 ? 1 : 0].empty()) return err;
	err = DialecticSwap();
	if(err != StudError::Success) return err;
	auto err2 = RetokenizeChat();
	auto err3 = DialecticSwap();
	return err2 != StudError::Success ? err2 : err3;
}
StudError SetMessageAt(const int index, const char* think, const char* message){
	AlignChatStates();
	if(index < 0 || index >= static_cast<int>(_session.chatMsgs[_session.dId].size())) return StudError::IndexOutOfRange;
	if(!_session.ctx || !_session.smpl[_session.dId] || !_vocab){
		_session.chatMsgs[0][index].reasoning_content = think;
		_session.chatMsgs[0][index].content = std::string(message);
		_session.chatMsgs[1][index].reasoning_content = think;
		_session.chatMsgs[1][index].content = std::string(message);
		_session.cachedTokens[0].clear();
		_session.cachedTokens[1].clear();
		return StudError::Success;
	}
	_session.chatMsgs[_session.dId][index].reasoning_content = think;
	_session.chatMsgs[_session.dId][index].content = std::string(message);
	auto err = RetokenizeChat();
	const auto dId = _session.dId == 0 ? 1 : 0;
	if(err != StudError::Success || _session.dialState[dId].empty() || _session.chatMsgs[dId].size() <= index) return err;
	err = DialecticSwap();
	if(err != StudError::Success) return err;
	_session.chatMsgs[_session.dId][index].reasoning_content = think;
	_session.chatMsgs[_session.dId][index].content = std::string(message);
	auto err2 = RetokenizeChat();
	auto err3 = DialecticSwap();
	return err2 != StudError::Success ? err2 : err3;
}
StudError RemoveMessageAt(const int index){
	AlignChatStates();
	if(index < 0 || index >= static_cast<int>(_session.chatMsgs[_session.dId].size())) return StudError::IndexOutOfRange;
	if(!_session.ctx || !_session.smpl[_session.dId] || !_vocab){
		_session.chatMsgs[0].erase(_session.chatMsgs[0].begin() + index);
		_session.chatMsgs[1].erase(_session.chatMsgs[1].begin() + index);
		_session.cachedTokens[0].clear();
		_session.cachedTokens[1].clear();
		return StudError::Success;
	}
	_session.chatMsgs[_session.dId].erase(_session.chatMsgs[_session.dId].begin() + index);
	auto err = RetokenizeChat();
	const auto dId = _session.dId == 0 ? 1 : 0;
	if(err != StudError::Success || _session.dialState[dId].empty() || _session.chatMsgs[dId].size() <= index) return err;
	err = DialecticSwap();
	if(err != StudError::Success) return err;
	_session.chatMsgs[_session.dId].erase(_session.chatMsgs[_session.dId].begin() + index);
	auto err2 = RetokenizeChat();
	auto err3 = DialecticSwap();
	return err2 != StudError::Success ? err2 : err3;
}
StudError RemoveMessagesStartingAt(int index){
	AlignChatStates();
	if(index < 0) index = 0;
	if(index > static_cast<int>(_session.chatMsgs[_session.dId].size())) index = static_cast<int>(_session.chatMsgs[_session.dId].size());
	if(!_session.ctx || !_session.smpl[_session.dId] || !_vocab){
		_session.chatMsgs[0].erase(_session.chatMsgs[0].begin() + index, _session.chatMsgs[0].end());
		_session.chatMsgs[1].erase(_session.chatMsgs[1].begin() + index, _session.chatMsgs[1].end());
		_session.cachedTokens[0].clear();
		_session.cachedTokens[1].clear();
		return StudError::Success;
	}
	_session.chatMsgs[_session.dId].erase(_session.chatMsgs[_session.dId].begin() + index, _session.chatMsgs[_session.dId].end());
	auto err = RetokenizeChat();
	const auto dId = _session.dId == 0 ? 1 : 0;
	if(err != StudError::Success || _session.dialState[dId].empty() || _session.chatMsgs[dId].size() <= index) return err;
	err = DialecticSwap();
	if(err != StudError::Success) return err;
	_session.chatMsgs[_session.dId].erase(_session.chatMsgs[_session.dId].begin() + index, _session.chatMsgs[_session.dId].end());
	auto err2 = RetokenizeChat();
	auto err3 = DialecticSwap();
	return err2 != StudError::Success ? err2 : err3;
}
StudError AddMessage(const MessageRole role, const char* message){
	common_chat_msg msg;
	msg.role = RoleString(role);
	msg.content = std::string(message);
	_session.chatMsgs[_session.dId].push_back(msg);
	return RetokenizeChat();
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
static StudError doTool(std::string_view tok, ToolCtx& s, const bool cbOn, double& ftTime, const HrClock::time_point& t0, llama_memory_t llMem, std::string& response, std::vector<llama_token>& newTokens, const TokenCallbackFn cb){
	if(tok == OpenThinkTag()){
		s.inThink = true;
		return StudError::Success;
	}
	if(tok == CloseThinkTag()){
		s.inThink = false;
		return StudError::Success;
	}
	if(!_hasTools || !s.inThink) return StudError::Success;
	if(tok == OpenToolCallTag()){
		s.inCall = true;
		s.buf = tok;
		return StudError::Success;
	}
	if(s.inCall){
		if(tok != CloseToolCallTag() && tok != OpenToolResponseTag()){
			s.buf += tok;
			return StudError::Success;
		}
		if(tok == CloseToolCallTag()) s.buf += CloseToolCallTag();
		s.inCall = false;
		_session.syntax.parse_tool_calls = true;
		auto p = common_chat_parse(s.buf, true, _session.syntax);
		s.buf.clear();
		if(p.tool_calls.empty()) return StudError::Success;
		auto& c = p.tool_calls.back();
		auto tokenizeAndRun = [&](const std::string& text){
			if(text.empty()) return StudError::Success;
			const int n = -llama_tokenize(_vocab, text.c_str(), text.size(), nullptr, 0, true, true);
			std::vector<llama_token> v(n);
			llama_tokenize(_vocab, text.c_str(), text.size(), v.data(), n, true, true);
			for(size_t i = 0; i < v.size();){
				const int b = std::min<int>(_session.nBatch, v.size() - i);
				const llama_batch lb = llama_batch_get_one(&v[i], b);
				if(LlamaMemSize() + lb.n_tokens > llama_n_ctx(_session.ctx)) return StudError::ConvTooLong;
				if(llama_decode(_session.ctx, lb) != 0) return StudError::LlamaDecodeError;
				for(int k = 0; k < b; ++k) llama_sampler_accept(_session.smpl[_session.dId], v[i + k]);
				i += b;
			}
			newTokens.insert(newTokens.end(), v.begin(), v.end());
			response += text;
			return StudError::Success;
		};
		if(auto h = _toolHandlers.find(c.name); h != _toolHandlers.end()){
			if(tok == CloseToolCallTag()){
				std::string open = "\n" + OpenToolResponseTag();
				auto err = tokenizeAndRun(open);
				if(err != StudError::Success) return err;
			}
			std::string out = "\n" + h->second(c.arguments.c_str());
			auto err = tokenizeAndRun(out);
			if(err != StudError::Success) return err;
			std::string close = "\n" + CloseToolResponseTag();
			err = tokenizeAndRun(close);
			if(err != StudError::Success) return err;
		}
		if(_tokenCb && cbOn){
			_session.syntax.parse_tool_calls = false;
			auto msg = common_chat_parse(response, true, _session.syntax);
			cb(msg.reasoning_content.c_str(), static_cast<int>(msg.reasoning_content.length()), msg.content.c_str(), static_cast<int>(msg.content.length()), 1, LlamaMemSize(), ftTime, 0);
		}
	}
	return StudError::Success;
}
StudError Generate(const std::vector<common_chat_msg>& messages, const int nPredict, const bool callback, common_chat_msg& outMsg){
	const auto prepStart = HrClock::now();
	_stop.store(false);
	const TokenCallbackFn cb = _tokenCb;
	const size_t chatStart = _session.chatMsgs[_session.dId].size();
	for(const auto& message : messages){
		const auto formatted = common_chat_format_single(_chatTemplates.get(), _session.chatMsgs[_session.dId], message, !message.role._Equal("assistant"), _session.useJinja && message.role._Equal("assistant"));
		const int nPromptTokens = -llama_tokenize(_vocab, formatted.c_str(), formatted.size(), nullptr, 0, true, true);
		std::vector<llama_token> promptTokens(nPromptTokens);
		if(llama_tokenize(_vocab, formatted.c_str(), formatted.size(), promptTokens.data(), promptTokens.size(), true, true) < 0){
			outMsg = common_chat_msg();
			return StudError::CantTokenizePrompt;
		}
		_session.chatMsgs[_session.dId].push_back(message);
		size_t p = 0;
		while(p < promptTokens.size() && !_stop.load()){
			const int nEval = std::min<int>(_session.nBatch, promptTokens.size() - p);
			llama_batch batch = llama_batch_get_one(&promptTokens[p], nEval);
			const int nCtx = llama_n_ctx(_session.ctx);
			const int nCtxUsed = LlamaMemSize();
			if(nCtxUsed + batch.n_tokens > nCtx){
				_session.chatMsgs[_session.dId].pop_back();
				auto rtErr = RetokenizeChat(true);
				outMsg = common_chat_msg();
				return rtErr != StudError::Success ? rtErr : StudError::ConvTooLong;
			}
			auto result = llama_decode(_session.ctx, batch);
			if(result != 0){
				_session.chatMsgs[_session.dId].pop_back();
				auto rtErr = RetokenizeChat(true);
				outMsg = common_chat_msg();
				if(result == 1) return rtErr != StudError::Success ? rtErr : StudError::ConvTooLong;
				return rtErr != StudError::Success ? rtErr : StudError::LlamaDecodeError;
			}
			for(int j = 0; j < nEval; ++j){ llama_sampler_accept(_session.smpl[_session.dId], promptTokens[p + j]); }
			p += nEval;
		}
		_session.cachedTokens[_session.dId].insert(_session.cachedTokens[_session.dId].end(), promptTokens.begin(), promptTokens.end());
	}
	auto status = StudError::Success;
	llama_token newTokenId;
	std::vector<llama_token> newTokens;
	std::string response;
	common_chat_msg msg;
	ToolCtx tool;
	double ftTime = 0.0;
	int i = 0;
	while((nPredict < 0 || i < nPredict) && !_stop.load()){
		newTokenId = llama_sampler_sample(_session.smpl[_session.dId], _session.ctx, -1);
		if(llama_vocab_is_eog(_vocab, newTokenId)) _stop.store(true);
		char buf[256];
		const int n = llama_token_to_piece(_vocab, newTokenId, buf, sizeof buf, 0, false);
		if(n < 0){
			_session.chatMsgs[_session.dId].resize(chatStart + messages.size());
			auto rtErr = RetokenizeChat(true);
			outMsg = common_chat_msg();
			return rtErr != StudError::Success ? rtErr : StudError::CantConvertToken;
		}
		newTokens.push_back(newTokenId);
		if(ftTime == 0.0) ftTime = std::chrono::duration<double>(HrClock::now() - prepStart).count();
		std::string tokenStr(buf, n);
		response += tokenStr;
		++i;
		if(cb && callback && !tokenStr.empty()){
			_session.syntax.parse_tool_calls = false;
			msg = common_chat_parse(response, true, _session.syntax);
			cb(msg.reasoning_content.c_str(), static_cast<int>(msg.reasoning_content.length()), msg.content.c_str(), static_cast<int>(msg.content.length()), 1, LlamaMemSize(), ftTime, 0);
		}
		auto batch = llama_batch_get_one(&newTokenId, 1);
		const int nCtx = llama_n_ctx(_session.ctx);
		const int nCtxUsed = LlamaMemSize();
		if(nCtxUsed + batch.n_tokens > nCtx){
			_session.chatMsgs[_session.dId].resize(chatStart + messages.size());
			auto rtErr = RetokenizeChat(true);
			outMsg = common_chat_msg();
			return rtErr != StudError::Success ? rtErr : StudError::ConvTooLong;
		}
		auto decodeErr = llama_decode(_session.ctx, batch);
		if(decodeErr != 0){
			_session.chatMsgs[_session.dId].resize(chatStart + messages.size());
			auto rtErr = RetokenizeChat(true);
			outMsg = common_chat_msg();
			if(decodeErr == 1) return rtErr != StudError::Success ? rtErr : StudError::ConvTooLong;
			return rtErr != StudError::Success ? rtErr : StudError::LlamaDecodeError;
		}
		llama_sampler_accept(_session.smpl[_session.dId], newTokenId);
		if(_hasTools){
			auto toolErr = doTool(tokenStr, tool, callback, ftTime, prepStart, _session.llMem, response, newTokens, cb);
			if(toolErr != StudError::Success){
				status = toolErr;
				_stop.store(true);
			}
		}
	}
	_session.syntax.parse_tool_calls = false;
	msg = common_chat_parse(response, false, _session.syntax);
	_session.chatMsgs[_session.dId].push_back(msg);
	_session.cachedTokens[_session.dId].insert(_session.cachedTokens[_session.dId].end(), newTokens.begin(), newTokens.end());
	if(cb && !callback) cb(msg.reasoning_content.c_str(), static_cast<int>(msg.reasoning_content.length()), msg.content.c_str(), static_cast<int>(msg.content.length()), i, LlamaMemSize(), ftTime, 0);
	outMsg = std::move(msg);
	//OutputDebugStringA(("\n---\n" + std::string(GetContextAsText()) + "\n---\n").c_str());
	return status;
}
StudError GenerateWithTools(const MessageRole role, const char* prompt, const int nPredict, const bool callback){
	common_chat_msg msg;
	msg.role = RoleString(role);
	msg.content = std::string(prompt);
	std::vector<common_chat_msg> msgs{msg};
	if(!_hasTools){ return Generate(msgs, nPredict, callback, msg); }
	const TokenCallbackFn cb = _tokenCb;
	auto err = StudError::Success;
	bool toolCalled = false;
	do{
		err = Generate(msgs, nPredict, callback, msg);
		if(err != StudError::Success) return err;
		if(!_session.chatMsgs[_session.dId].size()) return StudError::Success;
		msgs.clear();
		_session.syntax.parse_tool_calls = true;
		try{
			auto parsed = common_chat_parse(msg.content, false, _session.syntax);
			msg.content.clear();
			msg.reasoning_content.clear();
			msg.tool_calls = parsed.tool_calls;
			toolCalled = false;
			for(common_chat_tool_call& toolCall : parsed.tool_calls){
				auto it = _toolHandlers.find(toolCall.name);
				if(it != _toolHandlers.end()){
					auto toolMsg = it->second(toolCall.arguments.c_str());
					if(cb) cb(nullptr, 0, toolMsg.c_str(), static_cast<int>(toolMsg.length()), 0, LlamaMemSize(), 0, 1);
					msgs.push_back(common_chat_msg());
					msgs.back().role = "tool";
					msgs.back().content = toolMsg;
					toolCalled = true;
				}
			}
		} catch(std::exception& e){
			_lastErrorMessage = e.what();
			return StudError::ChatParseError;
		}
	} while(toolCalled);
	return StudError::Success;
}
void StopGeneration(){ _stop.store(true); }
char* GetContextAsText(){
	if(!_session.ctx) return nullptr;
	std::string outStr;
	outStr.reserve(_session.cachedTokens[_session.dId].size() * 4);
	for(const llama_token tok : _session.cachedTokens[_session.dId]){ outStr += common_token_to_piece(_session.ctx, tok, true); }
	auto* out = static_cast<char*>(std::malloc(outStr.size() + 1));
	if(out) std::memcpy(out, outStr.c_str(), outStr.size() + 1);
	return out;
}
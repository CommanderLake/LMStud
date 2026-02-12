#include "stud.h"
#include "hug.h"
#include <filesystem>
#include <chrono>
#include <regex>
#include <unordered_map>
#include <minja\minja.hpp>
#include <minja\chat-template.hpp>
#include <algorithm>
#include <mutex>
#include <thread>
using HrClock = std::chrono::high_resolution_clock;
extern "C" void CloseCommandPrompt();
extern "C" void StopCMDOutput();
extern "C" void MarkToolsJsonDirty();
static bool _gpuOomStud = false;
static std::string _lastErrorMessage;
namespace Stud::Backend{
	BackendState& state(){
		static BackendState instance;
		return instance;
	}
}
namespace{
	Stud::Backend::BackendState& backend = Stud::Backend::state();
	auto& _hWnd = backend.hWnd;
	auto& _llModel = backend.model;
	auto& _session = backend.session;
	auto& _stop = backend.stop;
	auto& _chatTemplates = backend.chatTemplates;
	auto& _tokenCb = backend.tokenCallback;
	auto& _tools = backend.tools;
	auto& _toolHandlers = backend.toolHandlers;
	auto& _hasTools = backend.hasTools;
}
using Stud::MessageRole;
using Stud::ToolCtx;
using Stud::ToolHandlerFn;
using Stud::TokenCallbackFn;
struct ChatStateSnapshot{
	std::vector<common_chat_msg> chatMsgs[2];
	std::vector<llama_token> cachedTokens[2];
	std::vector<unsigned char> dialState[2];
	int dId = 0;
	std::string prompt;
	std::string toolsPrompt;
	common_chat_syntax syntax{};
	bool useJinja = true;
	int nBatch = 1;
};
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
void AddTool(const char* name, const char* description, const char* parameters, const ToolHandlerFn handler){
	if(!name) return;
	common_chat_tool tool;
	tool.name = name;
	if(description) tool.description = description;
	if(parameters) tool.parameters = parameters;
	_tools.push_back(tool);
	if(handler) _toolHandlers[name] = handler;
	MarkToolsJsonDirty();
}
void ClearTools(){
	CloseCommandPrompt();
	_tools.clear();
	_toolHandlers.clear();
	MarkToolsJsonDirty();
}
static char* CopyCString(const std::string& text){
	const auto size = text.size();
	auto* buffer = static_cast<char*>(std::malloc(size + 1));
	if(!buffer) return nullptr;
	std::memcpy(buffer, text.data(), size);
	buffer[size] = '\0';
	return buffer;
}
extern "C" EXPORT char* ExecuteTool(const char* name, const char* argsJson){
	if(!name || name[0] == '\0') return CopyCString("{\"error\":\"missing tool name\"}");
	const auto it = _toolHandlers.find(name);
	if(it == _toolHandlers.end() || !it->second) return CopyCString("{\"error\":\"unknown tool\"}");
	try{
		const std::string response = it->second(argsJson ? argsJson : "");
		return CopyCString(response);
	} catch(const std::exception& ex){ return CopyCString(std::string("{\"error\":\"") + ex.what() + "\"}"); } catch(...){ return CopyCString("{\"error\":\"tool execution failed\"}"); }
}
StudError CreateContext(const int nCtx, const int nBatch, const unsigned int flashAttn, const int nThreads, const int nThreadsBatch){
	if(_session.ctx){
		llama_free(_session.ctx);
		_session.ctx = nullptr;
	}
	auto ctxParams = llama_context_default_params();
	ctxParams.n_ctx = nCtx;
	ctxParams.n_batch = nBatch;
	//ctxParams.flash_attn = flashAttn > 0;
	if(flashAttn == 0) ctxParams.flash_attn_type = LLAMA_FLASH_ATTN_TYPE_DISABLED;
	else if(flashAttn == 1) ctxParams.flash_attn_type = LLAMA_FLASH_ATTN_TYPE_ENABLED;
	else if(flashAttn == 2) ctxParams.flash_attn_type = LLAMA_FLASH_ATTN_TYPE_AUTO;
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
StudError CreateSession(const int nCtx, const int nBatch, const unsigned int flashAttn, const int nThreads, const int nThreadsBatch, const float minP, const float topP, const int topK, const float temp, const float repeatPenalty){
	if(!_llModel) return StudError::ModelNotLoaded;
	_session.syntax.reasoning_format = COMMON_REASONING_FORMAT_AUTO;
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
	_session._vocab = llama_model_get_vocab(_llModel);
	const auto bosStr = llama_vocab_get_text(_session._vocab, llama_vocab_bos(_session._vocab));
	const auto eosStr = llama_vocab_get_text(_session._vocab, llama_vocab_eos(_session._vocab));
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
int LlamaMemSize(){ return static_cast<int>(_session.cachedTokens[_session.dId].size()); }
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
	AlignChatStates();
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
	if(!_session.ctx || !_session.smpl[_session.dId] || !_session._vocab) return StudError::ModelNotLoaded;
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
	in.reasoning_format = COMMON_REASONING_FORMAT_AUTO;
	common_chat_params chatData;
	try{ chatData = common_chat_templates_apply(_chatTemplates.get(), in); } catch(std::exception& e){
		_lastErrorMessage = e.what();
		OutputDebugStringA((std::string("EXCEPTION:\r\n") + e.what()).c_str());
		return StudError::CantApplyTemplate;
	}
	_session.syntax.format = chatData.format;
	const int nPrompt = -llama_tokenize(_session._vocab, chatData.prompt.c_str(), chatData.prompt.size(), nullptr, 0, true, true);
	std::vector<llama_token> promptTokens(nPrompt);
	llama_tokenize(_session._vocab, chatData.prompt.c_str(), chatData.prompt.size(), promptTokens.data(), promptTokens.size(), true, true);
	size_t prefix = 0;
	while(prefix < _session.cachedTokens[_session.dId].size() && prefix < promptTokens.size() && _session.cachedTokens[_session.dId][prefix] == promptTokens[prefix]){ ++prefix; }
	const bool canShift = llama_memory_can_shift(_session.llMem);
	const size_t oldSz = _session.cachedTokens[_session.dId].size();
	const size_t newSz = promptTokens.size();
	size_t suffix = 0;
	if(canShift && oldSz > 0 && newSz > 0){ while(suffix + prefix < oldSz && suffix + prefix < newSz && suffix < oldSz && suffix < newSz && _session.cachedTokens[_session.dId][oldSz - 1 - suffix] == promptTokens[newSz - 1 - suffix]){ ++suffix; } }
	const size_t oldSize = _session.cachedTokens[_session.dId].size();
	const size_t newSize = promptTokens.size();
	if(prefix == oldSize && oldSize == newSize) return StudError::Success;
	if(newSize > static_cast<size_t>(llama_n_ctx(_session.ctx))){ return StudError::ConvTooLong; }
	if(rebuildMemory || !canShift){
		if(prefix == 0 || LlamaMemSize() < static_cast<llama_pos>(prefix - 1)){
			prefix = 0;
			llama_memory_clear(_session.llMem, true);
		}
	}
	if(!canShift && newSize < oldSize){
		prefix = 0;
		llama_memory_clear(_session.llMem, true);
	}
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
	if(batchSize > 0){
		for(size_t i = prefix; i < decodeEnd; i += _session.nBatch){
			const int nEval = std::min<int>(_session.nBatch, decodeEnd - i);
			auto batch = llama_batch_get_one(&promptTokens[i], nEval);
			if(llama_decode(_session.ctx, batch) != 0){
				if(_session.llMem) llama_memory_clear(_session.llMem, true);
				_session.cachedTokens[_session.dId].clear();
				if(_session.smpl[_session.dId]) llama_sampler_reset(_session.smpl[_session.dId]);
				return StudError::LlamaDecodeError;
			}
			for(int j = 0; j < nEval; ++j){ llama_sampler_accept(_session.smpl[_session.dId], promptTokens[i + j]); }
		}
	}
	for(size_t i = decodeEnd; i < newSize; ++i){ llama_sampler_accept(_session.smpl[_session.dId], promptTokens[i]); }
	_session.cachedTokens[_session.dId] = std::move(promptTokens);
	return StudError::Success;
}
static void AlignChatStates(){
	auto& a = _session.chatMsgs[0];
	auto& b = _session.chatMsgs[1];
	if(a.size() == b.size()) return;
	const auto* longer = &a;
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
	if(!_session.ctx || !_session.smpl[_session.dId] || !_session._vocab){
		DialecticFree();
		return StudError::Success;
	}
	auto err = RetokenizeChat(true);
	if(err != StudError::Success || _session.dialState[_session.dId == 0 ? 1 : 0].empty()) return err;
	err = DialecticSwap();
	if(err != StudError::Success) return err;
	const auto err2 = RetokenizeChat(true);
	const auto err3 = DialecticSwap();
	return err2 != StudError::Success ? err2 : err3;
}
StudError SetSystemPrompt(const char* prompt, const char* toolsPrompt){
	_session.prompt = std::string(prompt);
	_session.toolsPrompt = std::string(toolsPrompt);
	if(!_session.ctx || !_session.smpl[_session.dId] || !_session._vocab){
		_session.cachedTokens[0].clear();
		_session.cachedTokens[1].clear();
		return StudError::Success;
	}
	auto err = RetokenizeChat();
	if(err != StudError::Success || _session.dialState[_session.dId == 0 ? 1 : 0].empty()) return err;
	err = DialecticSwap();
	if(err != StudError::Success) return err;
	const auto err2 = RetokenizeChat();
	const auto err3 = DialecticSwap();
	return err2 != StudError::Success ? err2 : err3;
}
StudError SetMessageAt(const int index, const char* think, const char* message){
	AlignChatStates();
	if(index < 0 || index >= static_cast<int>(_session.chatMsgs[_session.dId].size())) return StudError::IndexOutOfRange;
	const auto applyMessageUpdate = [&](std::vector<common_chat_msg>& msgs){
		msgs[index].reasoning_content = msgs[index].role._Equal("assistant") ? think : std::string();
		msgs[index].content = std::string(message);
	};
	if(!_session.ctx || !_session.smpl[_session.dId] || !_session._vocab){
		applyMessageUpdate(_session.chatMsgs[0]);
		applyMessageUpdate(_session.chatMsgs[1]);
		_session.cachedTokens[0].clear();
		_session.cachedTokens[1].clear();
		return StudError::Success;
	}
	applyMessageUpdate(_session.chatMsgs[_session.dId]);
	auto err = RetokenizeChat();
	const auto dId = _session.dId == 0 ? 1 : 0;
	if(err != StudError::Success || _session.dialState[dId].empty() || _session.chatMsgs[dId].size() <= index) return err;
	err = DialecticSwap();
	if(err != StudError::Success) return err;
	applyMessageUpdate(_session.chatMsgs[_session.dId]);
	const auto err2 = RetokenizeChat();
	const auto err3 = DialecticSwap();
	return err2 != StudError::Success ? err2 : err3;
}
StudError RemoveMessageAt(const int index){
	AlignChatStates();
	if(index < 0 || index >= static_cast<int>(_session.chatMsgs[_session.dId].size())) return StudError::IndexOutOfRange;
	if(!_session.ctx || !_session.smpl[_session.dId] || !_session._vocab){
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
	const auto err2 = RetokenizeChat();
	const auto err3 = DialecticSwap();
	return err2 != StudError::Success ? err2 : err3;
}
StudError RemoveMessagesStartingAt(int index){
	AlignChatStates();
	if(index < 0) index = 0;
	if(index > static_cast<int>(_session.chatMsgs[_session.dId].size())) index = static_cast<int>(_session.chatMsgs[_session.dId].size());
	if(!_session.ctx || !_session.smpl[_session.dId] || !_session._vocab){
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
	const auto err2 = RetokenizeChat();
	const auto err3 = DialecticSwap();
	return err2 != StudError::Success ? err2 : err3;
}
StudError AddMessage(const MessageRole role, const char* message){
	common_chat_msg msg;
	msg.role = RoleString(role);
	msg.content = std::string(message);
	_session.chatMsgs[_session.dId].push_back(msg);
	return RetokenizeChat();
}
StudError SyncChatMessages(const int* roles, const char** thinks, const char** messages, int count){
	std::vector<common_chat_msg> msgs;
	if(count > 0){
		msgs.reserve(static_cast<size_t>(count));
		for(int i = 0; i < count; ++i){
			const auto role = static_cast<MessageRole>(roles ? roles[i] : 0);
			common_chat_msg msg;
			msg.role = RoleString(role);
			msg.content = messages && messages[i] ? std::string(messages[i]) : std::string();
			if(role == MessageRole::Assistant) msg.reasoning_content = thinks && thinks[i] ? std::string(thinks[i]) : std::string();
			msgs.push_back(std::move(msg));
		}
	}
	_session.chatMsgs[_session.dId] = std::move(msgs);
	_session.chatMsgs[_session.dId == 0 ? 1 : 0].clear();
	AlignChatStates();
	_session.cachedTokens[0].clear();
	_session.cachedTokens[1].clear();
	if(!_session.ctx || !_session.smpl[_session.dId] || !_session._vocab) return StudError::Success;
	auto err = RetokenizeChat(true);
	const auto dId = _session.dId == 0 ? 1 : 0;
	if(err != StudError::Success || _session.dialState[dId].empty()) return err;
	err = DialecticSwap();
	if(err != StudError::Success) return err;
	const auto err2 = RetokenizeChat(true);
	const auto err3 = DialecticSwap();
	return err2 != StudError::Success ? err2 : err3;
}
struct PendingToken{
	llama_token token;
	int memSize;
};
class AsyncTokenPostProcessor{
public:
	AsyncTokenPostProcessor(const TokenCallbackFn callbackFn, const bool streamCallback, const common_chat_syntax& chatSyntax, const HrClock::time_point& prepStart, std::string& responseText, common_chat_msg& parsedMsg, double& firstTokenTime) : _callbackFn(callbackFn),
		_streamCallback(streamCallback), _chatSyntax(chatSyntax), _prepStart(prepStart), _responseText(responseText), _parsedMsg(parsedMsg), _firstTokenTime(firstTokenTime), _queue(kQueueCapacity){ _worker = std::thread([this](){ WorkerLoop(); }); }
	~AsyncTokenPostProcessor(){ Close(); }
	StudError Error() const{ return _asyncError.load(std::memory_order_acquire); }
	void Close(){
		const bool wasClosed = _queueClosed.exchange(true, std::memory_order_acq_rel);
		if(!wasClosed) _queueCv.notify_all();
		if(_worker.joinable()) _worker.join();
	}
	StudError Enqueue(const llama_token token, const int memSize){
		for(;;){
			const StudError error = Error();
			if(error != StudError::Success) return error;
			const size_t head = _queueHead.load(std::memory_order_acquire);
			const size_t tail = _queueTail.load(std::memory_order_relaxed);
			if((tail - head) < kQueueCapacity){
				_queue[tail % kQueueCapacity] = PendingToken{token, memSize};
				_queueTail.store(tail + 1, std::memory_order_release);
				if(tail == head) _queueCv.notify_one();
				return StudError::Success;
			}
			std::this_thread::yield();
		}
	}
private:
	void EmitStreamingCallback(const int memSize){
		if(!_callbackFn || !_streamCallback || _pendingCallbackTokens <= 0) return;
		_parsedMsg = common_chat_parse(_responseText, true, _chatSyntax);
		_callbackFn(_parsedMsg.reasoning_content.c_str(), static_cast<int>(_parsedMsg.reasoning_content.length()), _parsedMsg.content.c_str(), static_cast<int>(_parsedMsg.content.length()), _pendingCallbackTokens, memSize, _firstTokenTime, 0);
		_pendingCallbackTokens = 0;
		_lastCallbackTime = std::chrono::steady_clock::now();
	}
	void WorkerLoop(){
		constexpr auto kMinCallbackInterval = std::chrono::milliseconds(16);
		constexpr int kMaxTokensPerCallback = 8;
		int lastMemSize = 0;
		for(;;){
			const size_t head = _queueHead.load(std::memory_order_relaxed);
			const size_t tail = _queueTail.load(std::memory_order_acquire);
			if(head == tail){
				if(_queueClosed.load(std::memory_order_acquire)){
					EmitStreamingCallback(lastMemSize);
					return;
				}
				if(_pendingCallbackTokens > 0 && std::chrono::steady_clock::now() - _lastCallbackTime >= kMinCallbackInterval){ EmitStreamingCallback(lastMemSize); }
				std::unique_lock<std::mutex> lock(_queueWaitMutex);
				_queueCv.wait_for(lock, std::chrono::milliseconds(1), [&](){ return _queueClosed.load(std::memory_order_acquire) || _queueHead.load(std::memory_order_relaxed) != _queueTail.load(std::memory_order_acquire); });
				continue;
			}
			const PendingToken pending = _queue[head % kQueueCapacity];
			_queueHead.store(head + 1, std::memory_order_release);
			char buf[256];
			const int n = llama_token_to_piece(_session._vocab, pending.token, buf, sizeof buf, 0, false);
			if(n < 0){
				_asyncError.store(StudError::CantConvertToken, std::memory_order_release);
				_queueClosed.store(true, std::memory_order_release);
				_queueCv.notify_all();
				return;
			}
			if(_firstTokenTime == 0.0) _firstTokenTime = std::chrono::duration<double>(HrClock::now() - _prepStart).count();
			_responseText.append(buf, static_cast<size_t>(n));
			if(n > 0){
				lastMemSize = pending.memSize;
				++_pendingCallbackTokens;
				const bool reachedBatchSize = _pendingCallbackTokens >= kMaxTokensPerCallback;
				const bool reachedRateLimit = std::chrono::steady_clock::now() - _lastCallbackTime >= kMinCallbackInterval;
				if(reachedBatchSize || reachedRateLimit) EmitStreamingCallback(lastMemSize);
			}
		}
	}
	static constexpr size_t kQueueCapacity = 1024;
	TokenCallbackFn _callbackFn;
	bool _streamCallback;
	common_chat_syntax _chatSyntax;
	HrClock::time_point _prepStart;
	std::string& _responseText;
	common_chat_msg& _parsedMsg;
	double& _firstTokenTime;
	std::vector<PendingToken> _queue;
	std::atomic<size_t> _queueHead{0};
	std::atomic<size_t> _queueTail{0};
	std::atomic<bool> _queueClosed{false};
	std::atomic<StudError> _asyncError{StudError::Success};
	std::mutex _queueWaitMutex;
	std::condition_variable _queueCv;
	std::thread _worker;
	std::chrono::steady_clock::time_point _lastCallbackTime = std::chrono::steady_clock::now();
	int _pendingCallbackTokens = 0;
};
static StudError RollbackGenerate(const size_t chatStart, const size_t newMessageCount, common_chat_msg& outMsg, const StudError error){
	_session.chatMsgs[_session.dId].resize(chatStart + newMessageCount);
	const auto rtErr = RetokenizeChat(true);
	outMsg = common_chat_msg();
	return rtErr != StudError::Success ? rtErr : error;
}
static StudError DecodePromptMessages(const std::vector<common_chat_msg>& messages, common_chat_msg& outMsg){
	for(const auto& message : messages){
		const auto formatted = common_chat_format_single(_chatTemplates.get(), _session.chatMsgs[_session.dId], message, !message.role._Equal("assistant"), _session.useJinja && message.role._Equal("assistant"));
		const int nPromptTokens = -llama_tokenize(_session._vocab, formatted.c_str(), formatted.size(), nullptr, 0, true, true);
		std::vector<llama_token> promptTokens(nPromptTokens);
		if(llama_tokenize(_session._vocab, formatted.c_str(), formatted.size(), promptTokens.data(), promptTokens.size(), true, true) < 0){
			outMsg = common_chat_msg();
			return StudError::CantTokenizePrompt;
		}
		_session.chatMsgs[_session.dId].push_back(message);
		size_t p = 0;
		while(p < promptTokens.size() && !_stop.load()){
			const int nEval = std::min<int>(_session.nBatch, promptTokens.size() - p);
			const llama_batch batch = llama_batch_get_one(&promptTokens[p], nEval);
			const int nCtx = llama_n_ctx(_session.ctx);
			const int nCtxUsed = LlamaMemSize();
			if(nCtxUsed + batch.n_tokens > nCtx){
				_session.chatMsgs[_session.dId].pop_back();
				const auto rtErr = RetokenizeChat(true);
				outMsg = common_chat_msg();
				return rtErr != StudError::Success ? rtErr : StudError::ConvTooLong;
			}
			const auto result = llama_decode(_session.ctx, batch);
			if(result != 0){
				_session.chatMsgs[_session.dId].pop_back();
				const auto rtErr = RetokenizeChat(true);
				outMsg = common_chat_msg();
				if(result == 1) return rtErr != StudError::Success ? rtErr : StudError::ConvTooLong;
				return rtErr != StudError::Success ? rtErr : StudError::LlamaDecodeError;
			}
			for(int j = 0; j < nEval; ++j){ llama_sampler_accept(_session.smpl[_session.dId], promptTokens[p + j]); }
			_session.cachedTokens[_session.dId].insert(_session.cachedTokens[_session.dId].end(), promptTokens.begin() + p, promptTokens.begin() + p + nEval);
			p += nEval;
		}
	}
	return StudError::Success;
}
StudError Generate(const std::vector<common_chat_msg>& messages, const int nPredict, const bool callback, common_chat_msg& outMsg){
	const auto prepStart = HrClock::now();
	_stop.store(false);
	const TokenCallbackFn cb = _tokenCb;
	const size_t chatStart = _session.chatMsgs[_session.dId].size();
	const auto promptErr = DecodePromptMessages(messages, outMsg);
	if(promptErr != StudError::Success) return promptErr;
	std::string response;
	common_chat_msg msg;
	double ftTime = 0.0;
	AsyncTokenPostProcessor postProcessor(cb, callback, _session.syntax, prepStart, response, msg, ftTime);
	auto failWith = [&](StudError error){
		postProcessor.Close();
		return RollbackGenerate(chatStart, messages.size(), outMsg, error);
	};
	int i = 0;
	while((nPredict < 0 || i < nPredict) && !_stop.load()){
		const StudError pendingError = postProcessor.Error();
		if(pendingError != StudError::Success) return failWith(pendingError);
		if(LlamaMemSize() + 1 > llama_n_ctx(_session.ctx)) return failWith(StudError::ConvTooLong);
		auto newTokenId = llama_sampler_sample(_session.smpl[_session.dId], _session.ctx, -1);
		const auto isEog = llama_vocab_is_eog(_session._vocab, newTokenId);
		const auto decodeErr = llama_decode(_session.ctx, llama_batch_get_one(&newTokenId, 1));
		if(decodeErr != 0) return failWith(decodeErr == 1 ? StudError::ConvTooLong : StudError::LlamaDecodeError);
		llama_sampler_accept(_session.smpl[_session.dId], newTokenId);
		_session.cachedTokens[_session.dId].push_back(newTokenId);
		if(isEog) break;
		const auto enqueueErr = postProcessor.Enqueue(newTokenId, LlamaMemSize());
		if(enqueueErr != StudError::Success) return failWith(enqueueErr);
		++i;
	}
	postProcessor.Close();
	if(postProcessor.Error() != StudError::Success) return RollbackGenerate(chatStart, messages.size(), outMsg, postProcessor.Error());
	msg = common_chat_parse(response, false, _session.syntax);
	_session.chatMsgs[_session.dId].push_back(msg);
	if(cb && !callback) cb(msg.reasoning_content.c_str(), static_cast<int>(msg.reasoning_content.length()), msg.content.c_str(), static_cast<int>(msg.content.length()), i, LlamaMemSize(), ftTime, 0);
	outMsg = std::move(msg);
	return StudError::Success;
}
StudError GenerateWithTools(const MessageRole role, const char* prompt, const int nPredict, const bool callback){
	common_chat_msg msg;
	msg.role = RoleString(role);
	msg.content = std::string(prompt);
	std::vector<common_chat_msg> msgs{msg};
	if(!_hasTools){ return Generate(msgs, nPredict, callback, msg); }
	const TokenCallbackFn cb = _tokenCb;
	bool toolCalled;
	do{
		const auto err = Generate(msgs, nPredict, callback, msg);
		if(err != StudError::Success) return err;
		if(!_session.chatMsgs[_session.dId].size()) return StudError::Success;
		msgs.clear();
		try{
			toolCalled = false;
			for(common_chat_tool_call& toolCall : msg.tool_calls){
				if(_stop.load()) return StudError::Success;
				auto toolCallMsg = "Tool name: " + toolCall.name + "\r\nTool ID: " + toolCall.id + "\r\nTool arguments: " + toolCall.arguments;
				if(cb) cb(nullptr, 0, toolCallMsg.c_str(), static_cast<int>(toolCallMsg.length()), 0, LlamaMemSize(), 0, 3);
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
void StopGeneration(){
	_stop.store(true);
	StopCMDOutput();
}
char* GetContextAsText(){
	if(!_session.ctx) return nullptr;
	std::string outStr;
	outStr.reserve(_session.cachedTokens[_session.dId].size() * 4);
	for(const llama_token tok : _session.cachedTokens[_session.dId]){ outStr += common_token_to_piece(_session.ctx, tok, true); }
	auto* out = static_cast<char*>(std::malloc(outStr.size() + 1));
	if(out) std::memcpy(out, outStr.c_str(), outStr.size() + 1);
	return out;
}
extern "C" EXPORT void* CaptureChatState(){
	auto* snapshot = new(std::nothrow) ChatStateSnapshot();
	if(!snapshot) return nullptr;
	snapshot->chatMsgs[0] = _session.chatMsgs[0];
	snapshot->chatMsgs[1] = _session.chatMsgs[1];
	snapshot->cachedTokens[0] = _session.cachedTokens[0];
	snapshot->cachedTokens[1] = _session.cachedTokens[1];
	snapshot->dialState[0] = _session.dialState[0];
	snapshot->dialState[1] = _session.dialState[1];
	snapshot->dId = _session.dId;
	snapshot->prompt = _session.prompt;
	snapshot->toolsPrompt = _session.toolsPrompt;
	snapshot->syntax = _session.syntax;
	snapshot->useJinja = _session.useJinja;
	snapshot->nBatch = _session.nBatch;
	return snapshot;
}
extern "C" EXPORT void RestoreChatState(void* state){
	if(!state) return;
	const auto* snapshot = static_cast<ChatStateSnapshot*>(state);
	_session.chatMsgs[0] = snapshot->chatMsgs[0];
	_session.chatMsgs[1] = snapshot->chatMsgs[1];
	_session.cachedTokens[0] = snapshot->cachedTokens[0];
	_session.cachedTokens[1] = snapshot->cachedTokens[1];
	_session.dialState[0] = snapshot->dialState[0];
	_session.dialState[1] = snapshot->dialState[1];
	_session.dId = snapshot->dId;
	_session.prompt = snapshot->prompt;
	_session.toolsPrompt = snapshot->toolsPrompt;
	_session.syntax = snapshot->syntax;
	_session.useJinja = snapshot->useJinja;
	_session.nBatch = snapshot->nBatch;
}
extern "C" EXPORT void FreeChatState(void* state){ delete static_cast<ChatStateSnapshot*>(state); }
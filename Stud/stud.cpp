#include "stud.h"
#include "hug.h"
#include <Windows.h>
#include <filesystem>
#include <chrono>
#include <regex>
#include <unordered_map>
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
void AddTool(const char* name, const char* description, const char* parameters, char*(*handler)(const char* args)){
	if(!name) return;
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
	if(!_ctx||!_vocab) return;
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
	const auto prepStart = HrClock::now();
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
			if(ftTime==0.0) ftTime = std::chrono::duration<double, std::ratio<1, 1>>(HrClock::now()-prepStart).count();
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
				const bool gotResult = result!=nullptr;
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
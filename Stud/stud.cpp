#include "stud.h"
#include <fstream>
#pragma comment(lib, "llama.lib")
#pragma comment(lib, "common.lib")
#pragma comment(lib, "ggml-base.lib")
void SetOMPEnv(){
	_putenv("OMP_PROC_BIND=close");
}
void InitTokens(){
	if(!_vocab) return;
	_tokens.clear();
	if(!_params.prompt.empty()){
		common_chat_msg newMsg;
		newMsg.role = "system";
		newMsg.content = _params.prompt;
		const auto systemPrompt = common_chat_format_single(_chatTemplates.get(), _chatMsgs, newMsg, false, _params.use_jinja);
		_tokens = common_tokenize(_ctx, systemPrompt, true, true);
	}
	if(_tokens.empty() && llama_vocab_get_add_bos(_vocab) && !_params.use_jinja){
		_tokens.push_back(llama_vocab_bos(_vocab));
	}
	_params.n_keep = _tokens.size();
}
void ResetChat(const bool msgs){
	if(_ctx) llama_kv_self_clear(_ctx);
	_tokens.erase(_tokens.begin() + _params.n_keep, _tokens.end());
	if(msgs) _chatMsgs.clear();
	_nPast = 0;
	_nConsumed = 0;
	_gaI = 0;
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
bool LoadModel(const char* filename, const char* system, int contextSize, const float temp, const float repeatPenalty, const int topK, const int topP, const int nThreads, const bool strictCPU, const int nGPULayers, const int nBatch, const ggml_numa_strategy numaStrategy){
	FreeModel();
	llama_backend_init();
	_params.numa = numaStrategy;
	llama_numa_init(_params.numa);
	_params.warmup = false;
	_params.model = filename;
	_params.prompt = system;
	_params.n_ctx = contextSize;
	_params.sampling.temp = temp;
	_params.sampling.penalty_repeat = repeatPenalty;
	_params.sampling.top_k = topK;
	_params.sampling.top_p = topP;
	_params.cpuparams.n_threads = nThreads;
	_params.cpuparams.strict_cpu = strictCPU;
	_params.cpuparams_batch.n_threads = nThreads;
	_params.cpuparams_batch.strict_cpu = strictCPU;
	_params.n_gpu_layers = nGPULayers;
	_params.n_ubatch = nBatch;
	_params.enable_chat_template = true;
	auto llamaInit = common_init_from_params(_params);
	if(!llamaInit.model||!llamaInit.context) return false;
	_llModel = llamaInit.model.release();
	_ctx = llamaInit.context.release();
	_vocab = llama_model_get_vocab(_llModel);
	_chatTemplates = common_chat_templates_init(_llModel, _params.chat_template);
	if(!llama_model_has_encoder(_llModel) && llama_vocab_get_add_eos(_vocab)) return false;
	_smpl = common_sampler_init(_llModel, _params.sampling);
	if(!_smpl) return false;
	_gaI = 0;
	_gaN = _params.grp_attn_n;
	_gaW = _params.grp_attn_w;
	if(_gaN!=1&&(_gaN<0||_gaW%_gaN!=0)) return false;
	RetokenizeChat();
	return true;
}
void SetTokenCallback(const TokenCallbackFn cb){
	_tokenCb = cb;
}
void SetThreadCount(int n){
	if(_ctx) llama_set_n_threads(_ctx, n, n);
}
int AddMessage(const bool user, const char* message){
	if(!_vocab) return 0;
	common_chat_msg newMsg;
	newMsg.role = user ? "user" : "assistant";
	newMsg.content = message;
	const auto formatted = common_chat_format_single(_chatTemplates.get(), _chatMsgs, newMsg, newMsg.role=="user", _params.use_jinja);
	_chatMsgs.push_back(newMsg);
	auto tokens = common_tokenize(_vocab, formatted, false, true);
	_tokens.insert(_tokens.end(), tokens.begin(), tokens.end());
	return tokens.size();
}
void RetokenizeChat(){
	ResetChat(false);
	InitTokens();
	for(auto i = 0; i < _chatMsgs.size(); ++i){
		const auto formatted = common_chat_format_single(_chatTemplates.get(), _chatMsgs, _chatMsgs[i], i == _chatMsgs.size() - 1 && _chatMsgs[i].role == "user", _params.use_jinja);
		auto tokens = common_tokenize(_vocab, formatted, false, true);
		_tokens.insert(_tokens.end(), tokens.begin(), tokens.end());
	}
}
void SetSystemPrompt(const char* prompt){
	_params.prompt = prompt;
	RetokenizeChat();
}
void RemoveMessageAt(const int index){
	if(index < 0 || index >= static_cast<int>(_chatMsgs.size())) return;
	_chatMsgs.erase(_chatMsgs.begin() + index);
	RetokenizeChat();
}
void RemoveMessagesStartingAt(int index){
	if(index < 0) index = 0;
	if(index > static_cast<int>(_chatMsgs.size())) index = static_cast<int>(_chatMsgs.size());
	_chatMsgs.erase(_chatMsgs.begin() + index, _chatMsgs.end());
	RetokenizeChat();
}
void Generate(const unsigned int nPredict){
	_stop = false;
	std::vector<llama_token> embd;
	std::ostringstream assMsg;
	for(auto i = 0; i < nPredict && !_stop; ++i){
		if(_gaN==1){
			if(_nPast+static_cast<int>(embd.size())>=_params.n_ctx){
				if(!_params.ctx_shift){
					break;         
				}
				const int nLeft = _nPast-_params.n_keep;
				const int nDiscard = nLeft/2;
				llama_kv_self_seq_rm(_ctx, 0, _params.n_keep, _params.n_keep+nDiscard);
				llama_kv_self_seq_add(_ctx, 0, _params.n_keep+nDiscard, _nPast, -nDiscard);
				_nPast -= nDiscard;
			}
		} else{
			while(_nPast>=_gaI+_gaW){
				const int ib = (_gaN*_gaI)/_gaW;
				const int bd = (_gaW/_gaN)*(_gaN-1);
				const int dd = (_gaW/_gaN)-ib*bd-_gaW;
				llama_kv_self_seq_add(_ctx, 0, _gaI, _nPast, ib*bd);
				llama_kv_self_seq_div(_ctx, 0, _gaI+ib*bd, _gaI+ib*bd+_gaW, _gaN);
				llama_kv_self_seq_add(_ctx, 0, _gaI+ib*bd+_gaW, _nPast+ib*bd, dd);
				_nPast -= bd;
				_gaI += _gaW/_gaN;
			}
		}
		if(static_cast<int>(_tokens.size())<=_nConsumed){
			llama_token id = common_sampler_sample(_smpl, _ctx, -1);
			common_sampler_accept(_smpl, id, true);
			embd.push_back(id);
		} else{
			while(_nConsumed<static_cast<int>(_tokens.size())&&embd.size()<_params.n_batch){
				embd.push_back(_tokens[_nConsumed]);
				common_sampler_accept(_smpl, _tokens[_nConsumed], false);
				++_nConsumed;
				if(static_cast<int>(embd.size()) >= _params.n_batch){
					break;
				}
			}
		}
		for(int i = 0; i<static_cast<int>(embd.size()); i += _params.n_batch){
			int nEval = static_cast<int>(embd.size())-i;
			if(nEval>_params.n_batch) nEval = _params.n_batch;
			if(llama_decode(_ctx, llama_batch_get_one(&embd[i], nEval))){
				return;
			}
			_nPast += nEval;
		}
		const auto id = common_sampler_last(_smpl);
		std::string tokenStr = common_token_to_piece(_ctx, id, false);
		assMsg << tokenStr;
		if(_tokenCb) _tokenCb(tokenStr.c_str(), tokenStr.length(), _tokens.size() + i);
		if(!embd.empty() && llama_vocab_is_eog(_vocab, embd.back())) break;
		embd.clear();
	}
	AddMessage(false, assMsg.str().c_str());
}
void StopGeneration(){ _stop = true; }
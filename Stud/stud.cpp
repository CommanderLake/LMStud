#include "stud.h"
#include <fstream>
#pragma comment(lib, "llama.lib")
#pragma comment(lib, "common.lib")
//#pragma comment(lib, "ggml.lib")
#pragma comment(lib, "ggml-base.lib")
//#pragma comment(lib, "ggml-cpu.lib")
//#pragma comment(lib, "ggml-cuda.lib")
void SetOMPEnv(){
	_putenv("OMP_PROC_BIND=close");
}
void ResetChat(const bool msgs){
	std::lock_guard<std::mutex> lock(tokensMutex);
	if(ctx) llama_kv_cache_clear(ctx);
	embdInp.erase(embdInp.begin() + params.n_keep, embdInp.end());
	if(msgs) chatMsgs.clear();
	nPast = 0;
	nConsumed = 0;
	gaI = 0;
}
void FreeModel(){
	if(ctx){
		llama_free(ctx);
		ctx = nullptr;
	}
	if(llModel){
		llama_model_free(llModel);
		llModel = nullptr;
	}
	if(smpl){
		common_sampler_free(smpl);
		smpl = nullptr;
	}
	llama_backend_free();
}
//void LoadGGUFMetadata(const char* filename){
//	metadata.clear();
//	std::ifstream fin(filename, std::ios::binary);
//	if(!fin){ return; }
//	uint32_t magic = 0;
//	fin.read(reinterpret_cast<char*>(&magic), sizeof magic);
//	if(magic!=0x46554747){
//		return;
//	}
//	uint32_t version = 0;
//	fin.read(reinterpret_cast<char*>(&version), sizeof version);
//	uint64_t tensorCount = 0, metadataCount = 0;
//	fin.read(reinterpret_cast<char*>(&tensorCount), sizeof tensorCount);
//	fin.read(reinterpret_cast<char*>(&metadataCount), sizeof metadataCount);
//	metadata.reserve(metadataCount);
//	for(uint64_t i = 0; i<metadataCount; ++i){
//		GGUFMetadataEntry entry = readMetadataEntry(fin);
//		metadata.push_back(std::move(entry));
//	}
//	fin.close();
//}
bool LoadModel(const char* filename, const char* system, int contextSize, const float temp, const float repeatPenalty, const int topK, const int topP, const int nThreads, const bool strictCPU, const int nGPULayers, const int nBatch, const ggml_numa_strategy numaStrategy){
	FreeModel();
	llama_backend_init();
	params.numa = numaStrategy;
	llama_numa_init(params.numa);
	params.warmup = false;
	params.model = filename;
	params.prompt = system;
	params.n_ctx = contextSize;
	params.sampling.temp = temp;
	params.sampling.penalty_repeat = repeatPenalty;
	params.sampling.top_k = topK;
	params.sampling.top_p = topP;
	params.cpuparams.n_threads = nThreads;
	params.cpuparams.strict_cpu = strictCPU;
	params.cpuparams_batch.n_threads = nThreads;
	params.cpuparams_batch.strict_cpu = strictCPU;
	params.n_gpu_layers = nGPULayers;
	params.n_ubatch = nBatch;
	params.enable_chat_template = true;
	auto llamaInit = common_init_from_params(params);
	if(!llamaInit.model||!llamaInit.context) return false;
	llModel = llamaInit.model.release();
	ctx = llamaInit.context.release();
	vocab = llama_model_get_vocab(llModel);
	chatTemplates = common_chat_templates_init(llModel, params.chat_template);
	if(!llama_model_has_encoder(llModel)){ if(llama_vocab_get_add_eos(vocab)) return false; }
	if(!params.prompt.empty()){
		common_chat_msg newMsg;
		newMsg.role = "system";
		newMsg.content = params.prompt;
		const auto systemPrompt = common_chat_format_single(chatTemplates.get(), chatMsgs, newMsg, false, params.use_jinja);
		embdInp = common_tokenize(ctx, systemPrompt, true, true);
		params.n_keep = embdInp.size();
	}
	const auto addBos = llama_vocab_get_add_bos(vocab)&&!params.use_jinja;
	if(embdInp.empty() && addBos){
		embdInp.push_back(llama_vocab_bos(vocab));
		params.n_keep = 1;
	}
	if(static_cast<int>(embdInp.size())>contextSize-4) return false;
	if(params.n_keep<=0 || params.n_keep>embdInp.size()) params.n_keep = embdInp.size();
	smpl = common_sampler_init(llModel, params.sampling);
	if(!smpl) return false;
	gaI = 0;
	gaN = params.grp_attn_n;
	gaW = params.grp_attn_w;
	if(gaN!=1&&(gaN<0||gaW%gaN!=0)) return false;
	if(!chatMsgs.empty()){
		ResetChat(false);
		RetokenizeChat();
	}
	return true;
}
void SetTokenCallback(const TokenCallbackFn cb){
	tokenCb = cb;
}
void SetThreadCount(int n){
	if(ctx) llama_set_n_threads(ctx, n, n);
}
int AddMessage(const bool user, const char* message){
	if(!vocab) return 0;
	common_chat_msg newMsg;
	newMsg.role = user ? "user" : "assistant";
	newMsg.content = message;
	const auto formatted = common_chat_format_single(chatTemplates.get(), chatMsgs, newMsg, newMsg.role=="user", params.use_jinja);
	chatMsgs.push_back(newMsg);
	auto tokens = common_tokenize(vocab, formatted, false, true);
	std::lock_guard<std::mutex> lock(tokensMutex);
	embdInp.insert(embdInp.end(), tokens.begin(), tokens.end());
	return embdInp.size();
}
void RetokenizeChat(){
	std::lock_guard<std::mutex> lock(tokensMutex);
	for(auto i = 0; i < chatMsgs.size(); ++i){
		const auto formatted = common_chat_format_single(chatTemplates.get(), chatMsgs, chatMsgs[i], i == chatMsgs.size() - 1 && chatMsgs[i].role == "user", params.use_jinja);
		auto tokens = common_tokenize(vocab, formatted, false, true);
		embdInp.insert(embdInp.end(), tokens.begin(), tokens.end());
	}
}
void RemoveMessageAt(const int index){
	if(index < 0 || index >= static_cast<int>(chatMsgs.size())) return;
	ResetChat(false);
	chatMsgs.erase(chatMsgs.begin() + index);
	RetokenizeChat();
}
void RemoveMessagesStartingAt(int index){
	if(index < 0) index = 0;
	if(index > static_cast<int>(chatMsgs.size())) index = static_cast<int>(chatMsgs.size());
	ResetChat(false);
	chatMsgs.erase(chatMsgs.begin() + index, chatMsgs.end());
	RetokenizeChat();
}
void Generate(const unsigned int nPredict){
	stop = false;
	std::vector<llama_token> embd;
	std::ostringstream assMsg;
	for(auto i = 0; i < nPredict && !stop; ++i){
		if(gaN==1){
			if(nPast+static_cast<int>(embd.size())>=params.n_ctx){
				if(!params.ctx_shift){
					break;         
				}
				const int nLeft = nPast-params.n_keep;
				const int nDiscard = nLeft/2;
				llama_kv_cache_seq_rm(ctx, 0, params.n_keep, params.n_keep+nDiscard);
				llama_kv_cache_seq_add(ctx, 0, params.n_keep+nDiscard, nPast, -nDiscard);
				nPast -= nDiscard;
			}
		} else{
			while(nPast>=gaI+gaW){
				const int ib = (gaN*gaI)/gaW;
				const int bd = (gaW/gaN)*(gaN-1);
				const int dd = (gaW/gaN)-ib*bd-gaW;
				llama_kv_cache_seq_add(ctx, 0, gaI, nPast, ib*bd);
				llama_kv_cache_seq_div(ctx, 0, gaI+ib*bd, gaI+ib*bd+gaW, gaN);
				llama_kv_cache_seq_add(ctx, 0, gaI+ib*bd+gaW, nPast+ib*bd, dd);
				nPast -= bd;
				gaI += gaW/gaN;
			}
		}
		if(static_cast<int>(embdInp.size())<=nConsumed){
			llama_token id = common_sampler_sample(smpl, ctx, -1);
			common_sampler_accept(smpl, id, true);
			embd.push_back(id);
		} else{
			while(nConsumed<static_cast<int>(embdInp.size())&&embd.size()<params.n_batch){
				embd.push_back(embdInp[nConsumed]);
				common_sampler_accept(smpl, embdInp[nConsumed], false);
				++nConsumed;
				if(static_cast<int>(embd.size()) >= params.n_batch){
					break;
				}
			}
		}
		for(int i = 0; i<static_cast<int>(embd.size()); i += params.n_batch){
			int nEval = static_cast<int>(embd.size())-i;
			if(nEval>params.n_batch) nEval = params.n_batch;
			if(llama_decode(ctx, llama_batch_get_one(&embd[i], nEval))){
				return;
			}
			nPast += nEval;
		}
		const auto id = common_sampler_last(smpl);
		std::string tokenStr = common_token_to_piece(ctx, id, false);
		assMsg << tokenStr;
		if(tokenCb) tokenCb(tokenStr.c_str(), embdInp.size() + i);
		if(!embd.empty() && llama_vocab_is_eog(vocab, embd.back())) break;
		embd.clear();
	}
	AddMessage(false, assMsg.str().c_str());
}
void StopGeneration(){ stop = true; }
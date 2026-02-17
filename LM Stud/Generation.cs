using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using LMStud.Properties;
namespace LMStud {
	internal class Generation{
		private static Form1 _form;
		internal static readonly SemaphoreSlim GenerationLock = new SemaphoreSlim(1, 1);
		internal static volatile bool Generating;
		internal static volatile bool APIServerGenerating;
		private static Action<string> _apiTokenCallback;
		private static readonly Stopwatch SwRate = new Stopwatch();
		private static readonly Stopwatch SwTot = new Stopwatch();
		private static ChatMessageControl _cntAssMsg;
		private static ChatMessageControl _cntToolMsg;
		private static int _msgTokenCount;
		internal static bool DialecticStarted;
		internal static bool DialecticPaused;
		internal static bool FirstToken = true;
		private static int _genTokenTotal;
		internal Generation(Form1 form){
			_form = form;
		}
		internal static void Generate(){
			var prompt = _form.textInput.Text;
			Generate(MessageRole.User, prompt, true);
		}
		internal static void Generate(MessageRole role, string prompt, bool addToChat){
			var useRemote = Common.APIClientEnable;
			if((!useRemote && !Common.LlModelLoaded) || string.IsNullOrWhiteSpace(prompt)) return;
			if(!GenerationLock.Wait(0)) return;
			if(Generating || APIServerGenerating){
				GenerationLock.Release();
				return;
			}
			if(_form.checkVoiceInput.CheckState == CheckState.Checked) NativeMethods.StopSpeechTranscription();
			DialecticPaused = false;
			Generating = true;
			foreach(var msg in _form.ChatMessages.Where(msg => msg.Editing)) _form.MsgButEditCancelOnClick(msg);
			_form.butGen.Text = Resources.Stop;
			_form.butReset.Enabled = _form.butApply.Enabled = false;
			foreach(var message in _form.ChatMessages) message.Generating = true;
			var newMsg = prompt.Trim();
			if(addToChat) _form.AddMessage(role, "", newMsg);
			_cntAssMsg = null;
			_cntToolMsg = null;
			_form.TTS.CancelPendingSpeech();
			FirstToken = true;
			if(role == MessageRole.User){
				NativeMethods.SetCommittedText("");
				if(addToChat) _form.textInput.Text = "";
			}
			if(!useRemote && _form.checkDialectic.Checked && !DialecticStarted && role == MessageRole.User){
				NativeMethods.DialecticStart();
				DialecticStarted = true;
			}
			if(useRemote){
				ThreadPool.QueueUserWorkItem(o => {GenerateWithApiClient(role, newMsg, addToChat);});
				return;
			}
			ThreadPool.QueueUserWorkItem(o => {
				var lockHeld = true;
				_msgTokenCount = 0;
				_genTokenTotal = 0;
				SwTot.Restart();
				SwRate.Restart();
				var generationError = NativeMethods.GenerateWithTools(role, newMsg, Common.NGen, _form.checkStream.Checked);
				SwTot.Stop();
				SwRate.Stop();
				if(_form.TTS.Pending.Length > 0){
					var remainingText = _form.TTS.Pending.ToString().Trim();
					if(!string.IsNullOrWhiteSpace(remainingText)) _form.TTS.QueueSpeech(remainingText);
					_form.TTS.Pending.Clear();
				}
				try{
					_form.Invoke(new MethodInvoker(() => {
						string followupPrompt = null;
						if(generationError != NativeMethods.StudError.Success){
							if(generationError == NativeMethods.StudError.ContextFull)
								MessageBox.Show(_form, Resources.Context_full, Resources.LM_Stud, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
							else MessageBox.Show(_form, generationError.ToString(), Resources.LM_Stud, MessageBoxButtons.OK, MessageBoxIcon.Error);
						}
						var elapsed = SwTot.Elapsed.TotalSeconds;
						if(_genTokenTotal > 0 && elapsed > 0.0){
							var callsPerSecond = _genTokenTotal/elapsed;
							_form.labelTPS.Text = string.Format(Resources._0_F2__Tok_s, callsPerSecond);
							SwTot.Reset();
							SwRate.Reset();
						}
						_form.FinishedGenerating();
						if(generationError != NativeMethods.StudError.Success) return;
						if(_form.checkDialectic.Checked && !DialecticPaused){
							var last = _form.ChatMessages.LastOrDefault();
							if(last != null){
								var dialecticError = NativeMethods.DialecticSwap();
								if(dialecticError == NativeMethods.StudError.Success) followupPrompt = last.Message;
								else{
									if(dialecticError == NativeMethods.StudError.ContextFull)
										MessageBox.Show(_form, Resources.Context_full, Resources.LM_Stud, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
									else MessageBox.Show(_form, dialecticError.ToString(), Resources.LM_Stud, MessageBoxButtons.OK, MessageBoxIcon.Error);
									return;
								}
							}
						}
						if(string.IsNullOrWhiteSpace(followupPrompt)) return;
						GenerationLock.Release();
						lockHeld = false;
						Generate(MessageRole.User, followupPrompt, false);
					}));
				} catch(ObjectDisposedException){} finally{ if(lockHeld) GenerationLock.Release(); }
			});
		}
		private static string RoleToApiRole(MessageRole role){
			switch(role){
				case MessageRole.Assistant: return "assistant";
				case MessageRole.Tool: return "tool";
				default: return "user";
			}
		}
		private static List<ApiClient.ChatMessage> BuildApiMessages(MessageRole role, string prompt, bool addToChat){
			var messages = new List<ApiClient.ChatMessage>();
			foreach(var msg in _form.ChatMessages){
				var hasToolCalls = msg.ApiToolCalls != null && msg.ApiToolCalls.Count > 0;
				if(msg.Role == MessageRole.Tool && string.IsNullOrWhiteSpace(msg.ApiToolCallId)) continue;
				if(string.IsNullOrWhiteSpace(msg.Message) && !hasToolCalls) continue;
				var apiMessage = new ApiClient.ChatMessage(RoleToApiRole(msg.Role), msg.Message);
				if(hasToolCalls) apiMessage.ToolCalls = msg.ApiToolCalls;
				if(!string.IsNullOrWhiteSpace(msg.ApiToolCallId)) apiMessage.ToolCallId = msg.ApiToolCallId;
				messages.Add(apiMessage);
			}
			if(!addToChat && !string.IsNullOrWhiteSpace(prompt)) messages.Add(new ApiClient.ChatMessage(RoleToApiRole(role), prompt));
			return messages;
		}
		private static NativeMethods.StudError SyncNativeChatMessages(){
			if(_form.IsDisposed) return NativeMethods.StudError.Success;
			var roles = Array.Empty<MessageRole>();
			var thinks = Array.Empty<string>();
			var messages = Array.Empty<string>();
			_form.RunOnUiThread(() => {
				var count = _form.ChatMessages.Count;
				roles = new MessageRole[count];
				thinks = new string[count];
				messages = new string[count];
				for(var i = 0; i < count; i++){
					var msg = _form.ChatMessages[i];
					roles[i] = msg.Role;
					thinks[i] = msg.Think ?? "";
					messages[i] = msg.Message ?? "";
				}
			});
			var result = NativeMethods.ResetChat();
			if(result != NativeMethods.StudError.Success && result != NativeMethods.StudError.ModelNotLoaded) return result;
			for(var i = 0; i < roles.Length; i++){
				result = NativeMethods.AddMessage(roles[i], messages[i]);
				if(result != NativeMethods.StudError.Success && result != NativeMethods.StudError.ModelNotLoaded) return result;
				if(roles[i] != MessageRole.Assistant || string.IsNullOrEmpty(thinks[i])) continue;
				result = NativeMethods.SetMessageAt(i, thinks[i], messages[i]);
				if(result != NativeMethods.StudError.Success && result != NativeMethods.StudError.ModelNotLoaded) return result;
			}
			return NativeMethods.StudError.Success;
		}
		private static void GenerateWithApiClient(MessageRole role, string prompt, bool addToChat){
			Exception error = null;
			var syncError = NativeMethods.StudError.Success;
			try{
				var messages = BuildApiMessages(role, prompt, addToChat);
				var history = ApiClient.BuildInputItems(messages);
				var toolsJson = Tools.BuildApiToolsJson();
				var client = new ApiClient(Common.APIClientUrl, Common.APIClientKey, Common.APIClientModel, Common.APIClientStore, Common.SystemPrompt);
				string lastToolSignature = null;
				while(true){
					var result = client.CreateChatCompletion(history, Common.Temp, Common.NGen, toolsJson, null, CancellationToken.None);
					ApiClient.AppendOutputItems(history, result);
					var content = result.Content;
					var reasoning = result.Reasoning;
					var toolCalls = result.ToolCalls;
					if(!string.IsNullOrWhiteSpace(content) || (toolCalls != null && toolCalls.Count > 0) || !string.IsNullOrWhiteSpace(reasoning)){
						if(toolCalls != null && toolCalls.Count > 0)
							content = toolCalls.Aggregate(content, (current, toolCall) => current + Resources.__Tool_name_ + toolCall.Name + Resources.__Tool_ID_ + toolCall.Id + Resources.__Tool_arguments_ + toolCall.Arguments);
						try{
							_form.Invoke(new MethodInvoker(() => {
								var message = _form.AddMessage(MessageRole.Assistant, reasoning ?? "", content ?? "", toolCalls);
								if(toolCalls != null && toolCalls.Count > 0) message.SetRoleText(Resources.Tool_Call);
								if(Common.Speak && !string.IsNullOrWhiteSpace(content)) _form.TTS.QueueSpeech(content);
							}));
						} catch(ObjectDisposedException){}
					}
					if(toolCalls == null || toolCalls.Count == 0) break;
					var toolSignature = string.Join("|", toolCalls.Select(call => string.Format(Resources._0___1___2_, call.Id, call.Name, call.Arguments)));
					if(toolSignature == lastToolSignature) throw new InvalidOperationException(Resources.Repeated_tool_calls_detected);
					lastToolSignature = toolSignature;
					foreach(var toolCall in toolCalls){
						var toolResult = Tools.ExecuteToolCall(toolCall);
						var toolMessage = new ApiClient.ChatMessage("tool", toolResult){ ToolCallId = toolCall.Id, ToolName = toolCall.Name };
						history.Add(ApiClient.BuildInputMessagePayload(toolMessage));
						if(string.IsNullOrWhiteSpace(toolResult)) continue;
						try{
							_form.Invoke(new MethodInvoker(() => {
								var toolMessageControl = _form.AddMessage(MessageRole.Tool, "", toolResult, null, toolCall.Id);
								toolMessageControl.SetRoleText(Resources.Tool_Output);
							}));
						} catch(ObjectDisposedException){}
					}
				}
				syncError = SyncNativeChatMessages();
			} catch(Exception ex){ error = ex; }
			try{
				_form.Invoke(new MethodInvoker(() => {
					if(error != null) _form.ShowApiClientError(Resources.API_Client, error);
					if(error == null && syncError != NativeMethods.StudError.Success && syncError != NativeMethods.StudError.ContextFull) _form.ShowError(Resources.API_Client, syncError.ToString(), true);
					_form.FinishedGenerating();
				}));
			} catch(ObjectDisposedException){} finally{ GenerationLock.Release(); }
		}
		private byte[] GetState(){
			var size = NativeMethods.GetStateSize();
			var data = new byte[size];
			NativeMethods.StudError err;
			unsafe{
				fixed(byte* p = data){ err = NativeMethods.GetStateData((IntPtr)p, size); }
			}
			if(err != NativeMethods.StudError.Success) _form.ShowError(Resources.API_Server, "GetStateData", true);
			return data;
		}
		private void SetState(byte[] state){
			if(state == null || state.Length == 0){
				_form.ShowError("Api server", "SetState\r\n\r\nstate is null", false);
				return;
			}
			NativeMethods.StudError err;
			unsafe{
				fixed(byte* p = state){ err = NativeMethods.SetStateData((IntPtr)p, state.Length); }
			}
			if(err != NativeMethods.StudError.Success) _form.ShowError(Resources.API_Server, "SetStateData", true);
		}
		internal bool GenerateForApiServer(byte[] state, string prompt, Action<string> onToken, out byte[] newState, out int tokenCount){
			newState = null;
			tokenCount = 0;
			if(string.IsNullOrWhiteSpace(prompt)) return false;
			if(!GenerationLock.Wait(300000)) return false;
			try{
				if(!Common.LlModelLoaded || Generating || APIServerGenerating) return false;
				APIServerGenerating = true;
				_apiTokenCallback = onToken;
				var originalState = GetState();
				var chatSnapshot = NativeMethods.CaptureChatState();
				try{
					if(state != null && state.Length > 0) SetState(state);
					else{
						var resetResult = NativeMethods.ResetChat();
						if(resetResult != NativeMethods.StudError.Success) return false;
					}
					NativeMethods.GenerateWithTools(MessageRole.User, prompt, Common.NGen, false);
					newState = GetState();
					tokenCount = NativeMethods.LlamaMemSize();
					return true;
				} finally{
					SetState(originalState);
					if(chatSnapshot != IntPtr.Zero) try{ NativeMethods.RestoreChatState(chatSnapshot); } finally{ NativeMethods.FreeChatState(chatSnapshot); }
					_apiTokenCallback = null;
					APIServerGenerating = false;
				}
			} finally{ GenerationLock.Release(); }
		}
		private static int FindSentenceEnd(StringBuilder sb){
			for(var i = 0; i < sb.Length - 1; i++) if((sb[i] == '.' || sb[i] == '!' || sb[i] == '?') && char.IsWhiteSpace(sb[i + 1])) return i;
			return -1;
		}
		internal static unsafe void TokenCallback(byte* thinkPtr, int thinkLen, byte* messagePtr, int messageLen, int tokenCount, int tokensTotal, double ftTime, int tool){
			if(APIServerGenerating){
				if(messageLen <= 0) return;
				var msg = Encoding.UTF8.GetString(messagePtr, messageLen);
				_apiTokenCallback?.Invoke(msg);
				return;
			}
			var elapsed = SwRate.Elapsed.TotalSeconds;
			if(elapsed >= 1.0) SwRate.Restart();
			var think = "";
			if(thinkLen > 0) think = Encoding.UTF8.GetString(thinkPtr, thinkLen);
			var message = "";
			if(messageLen > 0) message = Encoding.UTF8.GetString(messagePtr, messageLen);
			if(tool > 0){// 1 == tool output, 2 = CMD output stream, 3 = tool call details
				try{
					_form.BeginInvoke(new MethodInvoker(() => {
						if(_cntToolMsg == null){
							_cntToolMsg = _form.AddMessage(MessageRole.Tool, "", message);
							switch(tool){
								case 1:
								case 2: _cntToolMsg.SetRoleText(Resources.Tool_Output);
									break;
								case 3: _cntToolMsg.SetRoleText(Resources.Tool_Call);
									break;
							}
						}
						else{ _cntToolMsg.UpdateText("", message, true); }
						_cntAssMsg = null;
						_form.labelTokens.Text = string.Format(Resources._0___1__2_, tokensTotal, Common.CntCtxMax, Resources._Tokens);
						if(tool == 1 || tool == 3) _cntToolMsg = null;
					}));
				} catch(ObjectDisposedException){}
				return;
			}
			_msgTokenCount += tokenCount;
			_genTokenTotal += tokenCount;
			var renderToken = !_form.Rendering;
			try{
				_form.BeginInvoke((MethodInvoker)(() => {
					try{
						_form.Rendering = true;
						if(FirstToken){
							_form.labelPreGen.Text = Resources.First_token_time__ + ftTime + Resources._s;
							FirstToken = false;
						}
						if(elapsed >= 1.0){
							var callsPerSecond = _msgTokenCount/elapsed;
							_form.labelTPS.Text = string.Format(Resources._0_F2__Tok_s, callsPerSecond);
							_msgTokenCount = 0;
						}
						if(_cntAssMsg == null) _cntAssMsg = _form.AddMessage(MessageRole.Assistant, "", "");
						var lastThink = _cntAssMsg.checkThink.Checked;
						_cntAssMsg.UpdateText(think, message, renderToken);
						if(Common.Speak && !_cntAssMsg.checkThink.Checked && !lastThink && !string.IsNullOrWhiteSpace(message)){
							var i = _cntAssMsg.TTSPosition;
							for(; i < _cntAssMsg.Message.Length; i++){
								var ch = _cntAssMsg.Message[i];
								if(ch != '`' && ch != '*' && ch != '_' && ch != '#') _form.TTS.Pending.Append(ch);
							}
							_cntAssMsg.TTSPosition = i;
							while((i = FindSentenceEnd(_form.TTS.Pending)) >= 0){
								var sentence = _form.TTS.Pending.ToString(0, i + 1).Trim();
								if(!string.IsNullOrWhiteSpace(sentence)) _form.TTS.QueueSpeech(sentence);
								_form.TTS.Pending.Remove(0, i + 1);
								while(_form.TTS.Pending.Length > 0 && char.IsWhiteSpace(_form.TTS.Pending[0])) _form.TTS.Pending.Remove(0, 1);
							}
						}
						_form.labelTokens.Text = string.Format(Resources._0___1__2_, tokensTotal, Common.CntCtxMax, Resources._Tokens);
					} catch(ObjectDisposedException){} finally{ _form.Rendering = false; }
				}));
			} catch(ObjectDisposedException){}
		}
	}
}

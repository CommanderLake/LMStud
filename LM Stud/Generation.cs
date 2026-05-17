using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using LMStud.Parsers;
using LMStud.Properties;
namespace LMStud {
	internal static class Generation{
		internal static Form1 MainForm;
		internal static readonly NativeMethods.TokenCallback TokenCallbackFn = TokenCallback;
		private static readonly SemaphoreSlim MsgRenderLock = new SemaphoreSlim(1, 1);
		private static int _uiGenerationActive;
		internal static bool Generating{
			get => Volatile.Read(ref _uiGenerationActive) > 0;
			set => Interlocked.Exchange(ref _uiGenerationActive, value ? 1 : 0);
		}
		private static int _apiServerGenCount;
		internal static bool APIServerGenerating{
			get => Volatile.Read(ref _apiServerGenCount) > 0;
			set => Interlocked.Exchange(ref _apiServerGenCount, value ? 1 : 0);
		}
		private static readonly object APICancelSync = new object();
		private static CancellationTokenSource _apiGenCts;
		private static readonly object ApiTokenCallbacksSync = new object();
		private static readonly Dictionary<string, ApiTokenStream> ApiTokenStreams = new Dictionary<string, ApiTokenStream>(StringComparer.OrdinalIgnoreCase);
		private static readonly Stopwatch SwRate = new Stopwatch();
		private static readonly Stopwatch SwTot = new Stopwatch();
		private static ChatMessageControl _cntAssMsg;
		private static ChatMessageControl _cntToolMsg;
		private static int _msgTokenCount;
		internal static bool DialPaused;
		internal static string DialPriSlotName;
		internal static string DialSecSlotName;
		internal static string CntDialSlotName;
		internal static bool DialecticRelayEnabled => !string.IsNullOrWhiteSpace(DialPriSlotName) && !string.IsNullOrWhiteSpace(DialSecSlotName) && !string.Equals(DialPriSlotName, DialSecSlotName, StringComparison.OrdinalIgnoreCase);
		internal static bool FirstToken = true;
		private static int _genTokenTotal;
		internal static void ConfigureDialecticSlots(IList<ModelSlot> slots){
			DialPriSlotName = slots != null && slots.Count > 0 ? slots[0].Name : null;
			DialSecSlotName = slots != null && slots.Count > 1 ? slots[1].Name : null;
			CntDialSlotName = DialPriSlotName;
		}
		internal static void ClearDialecticSlots(){
			DialPriSlotName = null;
			DialSecSlotName = null;
			CntDialSlotName = null;
		}
		internal static string[] GetDialecticSlotNames(){
			return DialecticRelayEnabled ? new[]{ DialPriSlotName, DialSecSlotName } : new string[0];
		}
		private static string GetNextDialecticSlotName(string currentSlotName){
			if(!DialecticRelayEnabled) return null;
			return string.Equals(currentSlotName, DialSecSlotName, StringComparison.OrdinalIgnoreCase) ? DialPriSlotName : DialSecSlotName;
		}
		internal static void Generate(){
			var prompt = MainForm.textInput.Text;
			Generate(MessageRole.User, prompt, true);
		}
		internal static void Generate(MessageRole role, string prompt, bool addToChat){
			Generate(role, prompt, addToChat, null);
		}
		private static void Generate(MessageRole role, string prompt, bool addToChat, List<ModelSlotLockLease> slotLeases){
			var useDialecticLocalSlot = MainForm.checkDialectic.Checked && DialecticRelayEnabled && !string.IsNullOrWhiteSpace(CntDialSlotName);
			var activeSlot = useDialecticLocalSlot ? ModelSlotManager.GetSlot(CntDialSlotName) : ModelSlotManager.GetActiveChatSlot();
			var useRemote = activeSlot?.Source == ModelSlotSource.Api && !useDialecticLocalSlot;
			var slotName = activeSlot?.Name;
			if((useRemote && !ModelSlotManager.CanServeApiSlot(activeSlot)) || (!useRemote && !ModelSlotManager.CanServeLocalSlot(activeSlot)) || string.IsNullOrWhiteSpace(prompt)){
				ModelSlotManager.ReleaseSlots(slotLeases);
				return;
			}
			if(slotLeases == null) slotLeases = ModelSlotManager.TryEnterSlots(GetGenerationSlotNames(useDialecticLocalSlot, slotName), 0);
			if(slotLeases == null || !TryBeginUiGeneration()){
				ModelSlotManager.ReleaseSlots(slotLeases);
				return;
			}
			if(MainForm.checkVoiceInput.CheckState == CheckState.Checked) NativeMethods.StopSpeechTranscription();
			DialPaused = false;
			foreach(var msg in MainForm.ChatMessages.Where(msg => msg.Editing)) MainForm.MsgButEditCancelOnClick(msg);
			MainForm.butGen.Text = Resources.Stop;
			MainForm.butReset.Enabled = MainForm.butApply.Enabled = false;
			foreach(var message in MainForm.ChatMessages) message.Generating = true;
			var newMsg = prompt.Trim();
			if(addToChat) MainForm.AddMessage(role, "", newMsg);
			_cntAssMsg = null;
			_cntToolMsg = null;
			TTS.CancelPendingSpeech();
			FirstToken = true;
			if(role == MessageRole.User){
				NativeMethods.SetCommittedText("");
				if(addToChat) MainForm.textInput.Text = "";
			}
			if(useRemote){
				SetApiGenerationCancellation(new CancellationTokenSource());
				ThreadPool.QueueUserWorkItem(o => {GenerateWithApiClient(activeSlot, role, newMsg, addToChat, slotLeases);});
				return;
			}
			ThreadPool.QueueUserWorkItem(o => {
				var slotLocksHeld = true;
				_msgTokenCount = 0;
				_genTokenTotal = 0;
				SwTot.Restart();
				SwRate.Restart();
				var genErr = NativeMethods.GenerateWithTools(slotName, role, newMsg, Common.NGen, MainForm.checkStream.Checked);
				SwTot.Stop();
				SwRate.Stop();
				if(TTS.Pending.Length > 0){
					var remainingText = TTS.Pending.ToString().Trim();
					if(!string.IsNullOrWhiteSpace(remainingText)) TTS.QueueSpeech(remainingText);
					TTS.Pending.Clear();
				}
				try{
					MainForm.Invoke(new MethodInvoker(() => {
						if(genErr != NativeMethods.StudError.Success){
							if(genErr == NativeMethods.StudError.ContextFull)
								MessageBox.Show(MainForm, Resources.Context_full, Resources.LM_Stud, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
							else MainForm.ShowError(Resources.Generation, genErr);
						}
						var elapsed = SwTot.Elapsed.TotalSeconds;
						if(_genTokenTotal > 0 && elapsed > 0.0){
							var callsPerSecond = _genTokenTotal/elapsed;
							MainForm.labelTPS.Text = string.Format(Resources._0_F2__Tok_s, callsPerSecond);
							SwTot.Reset();
							SwRate.Reset();
						}
						MainForm.FinishedGenerating();
						if(genErr != NativeMethods.StudError.Success) return;
						if(!MainForm.checkDialectic.Checked || DialPaused) return;
						var last = MainForm.ChatMessages.LastOrDefault();
						if(last == null) return;
						var nextSlotName = GetNextDialecticSlotName(slotName);
						var err = string.IsNullOrWhiteSpace(slotName) || string.IsNullOrWhiteSpace(nextSlotName) ? NativeMethods.StudError.Generic : NativeMethods.DialecticRelaySwap(slotName, slotName, nextSlotName);
						if(err == NativeMethods.StudError.Success) CntDialSlotName = nextSlotName;
						switch(err){
							case NativeMethods.StudError.Success:
								slotLocksHeld = false;
								Generate(MessageRole.User, last.Message, false, slotLeases);
								break;
							case NativeMethods.StudError.ContextFull: MessageBox.Show(MainForm, Resources.Context_full, Resources.LM_Stud, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
								break;
							default: MainForm.ShowError(Resources.Generation, err);
								break;
						}
					}));
				} catch(ObjectDisposedException){} finally{
					if(slotLocksHeld) ModelSlotManager.ReleaseSlots(slotLeases);
				}
			});
		}
		private static bool TryBeginUiGeneration(){return Interlocked.CompareExchange(ref _uiGenerationActive, 1, 0) == 0;}
		internal static void EndUiGeneration(){Interlocked.Exchange(ref _uiGenerationActive, 0);}
		private static ChatMessageControl AddGeneratedAssistantMessage(string slotName, string think, string message, List<APIClient.ToolCall> toolCalls = null){
			var control = MainForm.AddMessage(MessageRole.Assistant, think, message, toolCalls);
			if(toolCalls != null && toolCalls.Count > 0) control.Markdown = false;
			if(!string.IsNullOrWhiteSpace(slotName)) control.SetRoleText(slotName);
			return control;
		}
		private static IEnumerable<string> GetGenerationSlotNames(bool useDialecticLocalSlot, string slotName){
			var slotNames = useDialecticLocalSlot ? GetDialecticSlotNames() : null;
			return slotNames != null && slotNames.Length > 0 ? slotNames : new[]{ slotName };
		}
		private static string RoleToApiRole(MessageRole role){
			switch(role){
				case MessageRole.Assistant: return "assistant";
				case MessageRole.Tool: return "tool";
				default: return "user";
			}
		}
		private static List<APIClient.ChatMessage> BuildApiMessages(MessageRole role, string prompt, bool addToChat){
			var messages = new List<APIClient.ChatMessage>();
			foreach(var msg in MainForm.ChatMessages){
				var hasToolCalls = msg.ApiToolCalls != null && msg.ApiToolCalls.Count > 0;
				if(msg.Role == MessageRole.Tool && string.IsNullOrWhiteSpace(msg.ApiToolCallId)) continue;
				var content = msg.ApiMessageContent;
				if(string.IsNullOrWhiteSpace(content) && !hasToolCalls) continue;
				var apiMessage = new APIClient.ChatMessage(RoleToApiRole(msg.Role), content);
				if(hasToolCalls) apiMessage.ToolCalls = msg.ApiToolCalls;
				if(!string.IsNullOrWhiteSpace(msg.ApiToolCallId)) apiMessage.ToolCallId = msg.ApiToolCallId;
				messages.Add(apiMessage);
			}
			if(!addToChat && !string.IsNullOrWhiteSpace(prompt)) messages.Add(new APIClient.ChatMessage(RoleToApiRole(role), prompt));
			return messages;
		}
		private static NativeMethods.StudError SyncNativeChatMessages(string slotName){
			if(MainForm.IsDisposed) return NativeMethods.StudError.Success;
			if(string.IsNullOrWhiteSpace(slotName)) slotName = ModelSlotManager.MainSlotName;
			var count = MainForm.ChatMessages.Count;
			var roles = new MessageRole[count];
			var thinks = new string[count];
			var messages = new string[count];
			MainForm.RunOnUiThread(() => {
				for(var i = 0; i < count; i++){
					var msg = MainForm.ChatMessages[i];
					roles[i] = msg.Role;
					thinks[i] = msg.Think ?? "";
					messages[i] = msg.ApiMessageContent;
				}
			});
			var result = NativeMethods.ResetChat(slotName);
			if(result != NativeMethods.StudError.Success && result != NativeMethods.StudError.ModelNotLoaded) return result;
			for(var i = 0; i < count; ++i){
				result = NativeMethods.AddMessage(slotName, roles[i], thinks[i], messages[i]);
				if(result != NativeMethods.StudError.Success && result != NativeMethods.StudError.ModelNotLoaded) return result;
			}
			return NativeMethods.StudError.Success;
		}
		private static void GenerateWithApiClient(ModelSlot slot, MessageRole role, string prompt, bool addToChat, List<ModelSlotLockLease> slotLeases){
			Exception error = null;
			var syncError = NativeMethods.StudError.Success;
			var cancellationToken = GetApiGenerationCancellationToken();
			try{
				var messages = BuildApiMessages(role, prompt, addToChat);
				var history = APIClient.BuildInputItems(messages);
				var toolsJson = Tools.BuildApiToolsJson(slot.Name);
				if(!ModelSlotManager.CanServeApiSlot(slot)) throw new InvalidOperationException(Resources.The_active_API_model_slot_is_incomplete_);
				using(var client = new APIClient(slot.ApiBaseUrl, slot.ApiKey, slot.ApiModel, slot.ApiStore, slot.GetInstructionsOrDefault(),
					slot.ApiReasoningEffort, slot.ApiReasoningSummary)){
					string lastToolSignature = null;
					while(true){
						var streamOutput = false;
						MainForm.RunOnUiThread(() => { streamOutput = MainForm.checkStream.Checked; });
						var streamedContent = new StringBuilder();
						ChatMessageControl streamedMessage = null;
						Action<string> streamCallback = null;
						if(streamOutput)
							streamCallback = delta => {
								if(string.IsNullOrEmpty(delta)) return;
								streamedContent.Append(delta);
								var snapshot = streamedContent.ToString();
								try{
									if(!MsgRenderLock.Wait(0)) return;
									MainForm.BeginInvoke(new MethodInvoker(() => {
										try {
											if(streamedMessage == null) streamedMessage = AddGeneratedAssistantMessage(slot.Name, "", "");
											streamedMessage.UpdateText("", snapshot, true);
										} finally { MsgRenderLock.Release(); }
									}));
								} catch(ObjectDisposedException){ MsgRenderLock.Release(); } catch(InvalidOperationException){ MsgRenderLock.Release(); }
							};
						var result = client.CreateChatCompletion(history, Common.Temp, Common.NGen, toolsJson, null, cancellationToken, streamCallback);
						UpdateApiTotalTokensLabel(result.TotalTokens);
						APIClient.AppendOutputItems(history, result);
						var content = result.Content;
						var reasoning = result.Reasoning;
						var toolCalls = result.ToolCalls;
						if(!string.IsNullOrWhiteSpace(content) || (toolCalls != null && toolCalls.Count > 0) || !string.IsNullOrWhiteSpace(reasoning)){
							var displayContent = toolCalls != null && toolCalls.Count > 0 ? Tools.FormatToolCallDisplayText(content, toolCalls) : content;
							try{
								MainForm.Invoke(new MethodInvoker(() => {
									var message = streamedMessage ?? AddGeneratedAssistantMessage(slot.Name, reasoning ?? "", displayContent ?? "", toolCalls);
									if(streamedMessage != null){
										message.ApiToolCalls = toolCalls;
										message.UpdateText(reasoning ?? "", displayContent ?? "", true);
									}
									if(toolCalls != null && toolCalls.Count > 0){
										message.ApiContent = content ?? "";
										message.Markdown = false;
										message.SetRoleText(Resources.Tool_Call);
									}
									if(Common.Speak && !string.IsNullOrWhiteSpace(content)) TTS.QueueSpeech(content);
								}));
							} catch(ObjectDisposedException){}
						}
						if(toolCalls == null || toolCalls.Count == 0) break;
						var toolSignature = string.Join("|", toolCalls.Select(call => string.Format("{0}:{1}:{2}", call.Id, call.Name, call.Arguments)));
						if(toolSignature == lastToolSignature) throw new InvalidOperationException(Resources.Repeated_tool_calls_detected);
						lastToolSignature = toolSignature;
						foreach(var toolCall in toolCalls){
							var toolStream = streamOutput ? new ToolStreamRenderer(toolCall.Id) : null;
							var toolResult = Tools.ExecuteToolCall(toolCall, slot.Name, toolStream == null ? (Action<string>)null : toolStream.Append);
							var toolMessage = new APIClient.ChatMessage("tool", toolResult){ ToolCallId = toolCall.Id };
							history.Add(APIClient.BuildInputMessagePayload(toolMessage));
							if(toolStream != null){
								if(!string.IsNullOrWhiteSpace(toolResult) || toolStream.HasContent) toolStream.Complete(toolResult);
								continue;
							}
							if(!string.IsNullOrWhiteSpace(toolResult)) Tools.AddToolOutputMessage(toolCall.Id, toolResult);
						}
					}
				}
				syncError = SyncNativeChatMessages(slot.Name);
			} catch(OperationCanceledException){
				error = null;
			} catch(Exception ex){ error = ex; }
			try{
				MainForm.Invoke(new MethodInvoker(() => {
					if(error != null) APIClient.ShowApiClientError(Resources.API_Client, error);
					if(error == null && syncError != NativeMethods.StudError.Success && syncError != NativeMethods.StudError.ContextFull) MainForm.ShowError(Resources.API_Client, syncError.ToString(), true);
					MainForm.FinishedGenerating();
				}));
			} catch(ObjectDisposedException){} finally{
				ClearApiGenerationCancellation();
				ModelSlotManager.ReleaseSlots(slotLeases);
			}
		}
		private static string FormatTokenLabel(int tokenCount){
			return Common.CntCtxMax > 0 ? string.Format(Resources._0___1__2_, tokenCount, Common.CntCtxMax, Resources._Tokens) : tokenCount + Resources._Tokens;
		}
		private static void UpdateApiTotalTokensLabel(int? totalTokens){
			if(!totalTokens.HasValue || MainForm == null || MainForm.IsDisposed) return;
			try{
				MainForm.BeginInvoke(new MethodInvoker(() => { MainForm.labelTokens.Text = FormatTokenLabel(totalTokens.Value); }));
			} catch(ObjectDisposedException){} catch(InvalidOperationException){}
		}
		internal static void StopActiveGeneration(){
			CancelApiGeneration();
			var slotName = !string.IsNullOrWhiteSpace(CntDialSlotName) ? CntDialSlotName : ModelSlotManager.GetActiveChatSlot()?.Name ?? Common.ActiveModelSlotName ?? ModelSlotManager.MainSlotName;
			NativeMethods.StopGeneration(slotName);
		}
		private static void SetApiGenerationCancellation(CancellationTokenSource cts){
			lock(APICancelSync){
				_apiGenCts?.Dispose();
				_apiGenCts = cts;
			}
		}
		private static CancellationToken GetApiGenerationCancellationToken(){
			lock(APICancelSync){ return _apiGenCts?.Token ?? CancellationToken.None; }
		}
		private static void CancelApiGeneration(){
			lock(APICancelSync){
				if(_apiGenCts == null) return;
				if(!_apiGenCts.IsCancellationRequested) _apiGenCts.Cancel();
			}
		}
		private static void ClearApiGenerationCancellation(){
			lock(APICancelSync){
				_apiGenCts?.Dispose();
				_apiGenCts = null;
			}
		}
		private static byte[] GetState(string slotName){
			var size = NativeMethods.GetStateSize(slotName);
			var data = new byte[size];
			NativeMethods.StudError err;
			unsafe{
				fixed(byte* p = data){ err = NativeMethods.GetStateData(slotName, (IntPtr)p, size); }
			}
			if(err != NativeMethods.StudError.Success) MainForm.ShowError(Resources.API_Server, Resources.GetStateData, true);
			return data;
		}
		private static void SetState(string slotName, byte[] state){
			if(state == null || state.Length == 0){
				MainForm.ShowError(Resources.API_Server, Resources.SetState__State_is_null, false);
				return;
			}
			NativeMethods.StudError err;
			unsafe{
				fixed(byte* p = state){ err = NativeMethods.SetStateData(slotName, (IntPtr)p, state.Length); }
			}
			if(err != NativeMethods.StudError.Success) MainForm.ShowError(Resources.API_Server, Resources.SetStateData, true);
		}
		internal static bool GenerateForApiServer(string slotName, byte[] state, IntPtr chatState, string historyJson, MessageRole role, string prompt, string toolsJson, out APIClient.ChatCompletionResult result, out byte[] newState, out IntPtr newChatState, out int tokenCount, int? maxTokens = null, string instructions = null, Action<string> streamCallback = null){
			result = null;
			newState = null;
			newChatState = IntPtr.Zero;
			tokenCount = 0;
			if(prompt == null) return false;
			if(string.IsNullOrWhiteSpace(slotName) || !NativeMethods.IsModelSlotLoaded(slotName)) return false;
			Interlocked.Increment(ref _apiServerGenCount);
			byte[] originalState = null;
			var chatSnapshot = IntPtr.Zero;
			try{
				SetApiTokenCallback(slotName, streamCallback);
				originalState = GetState(slotName);
				chatSnapshot = NativeMethods.CaptureChatState(slotName);
				if(!string.IsNullOrWhiteSpace(instructions)){
					var promptResult = NativeMethods.SetSystemPrompt(slotName, instructions, "");
					if(promptResult != NativeMethods.StudError.Success) return false;
				}
				// API server requests borrow a loaded slot; restore llama state plus Stud chat metadata so other chats keep their place.
				if(!string.IsNullOrWhiteSpace(historyJson)){
					var resetResult = NativeMethods.ResetChat(slotName);
					if(resetResult != NativeMethods.StudError.Success) return false;
					var syncResult = NativeMethods.SyncChatMessagesJson(slotName, historyJson);
					if(syncResult != NativeMethods.StudError.Success) return false;
				}
				else if(state != null && state.Length > 0 && chatState != IntPtr.Zero){
					SetState(slotName, state);
					NativeMethods.RestoreChatState(slotName, chatState);
				}
				else{
					var resetResult = NativeMethods.ResetChat(slotName);
					if(resetResult != NativeMethods.StudError.Success) return false;
				}
				var generationError = NativeMethods.GenerateForAPI(slotName, role, prompt, toolsJson, maxTokens ?? Common.NGen, streamCallback != null, out var responsePtr);
				if(generationError != NativeMethods.StudError.Success) return false;
				if(responsePtr == IntPtr.Zero) return false;
				try{ result = APIResponseParser.ParseResponseBody(ReadNativeUtf8(responsePtr)); }
				finally{ NativeMethods.FreeMemory(responsePtr); }
				newState = GetState(slotName);
				newChatState = NativeMethods.CaptureChatState(slotName);
				if(newChatState == IntPtr.Zero) return false;
				tokenCount = NativeMethods.LlamaMemSize(slotName);
				return true;
			} finally{
				if(originalState != null) SetState(slotName, originalState);
				if(chatSnapshot != IntPtr.Zero) try{ NativeMethods.RestoreChatState(slotName, chatSnapshot); } finally{ NativeMethods.FreeChatState(chatSnapshot); }
				SetApiTokenCallback(slotName, null);
				Interlocked.Decrement(ref _apiServerGenCount);
				if(MainForm != null) try{ MainForm.Invoke((MethodInvoker)(() => STT.RetryStart())); } catch(ObjectDisposedException){}
			}
		}
		private static void SetApiTokenCallback(string slotName, Action<string> callback){
			if(string.IsNullOrWhiteSpace(slotName)) return;
			lock(ApiTokenCallbacksSync){
				if(callback == null) ApiTokenStreams.Remove(slotName.Trim());
				else ApiTokenStreams[slotName.Trim()] = new ApiTokenStream(callback);
			}
		}
		private static ApiTokenStream GetApiTokenStream(string slotName){
			if(string.IsNullOrWhiteSpace(slotName)) return null;
			lock(ApiTokenCallbacksSync){
				ApiTokenStreams.TryGetValue(slotName.Trim(), out var stream);
				return stream;
			}
		}
		private sealed class ApiTokenStream{
			private readonly object _sync = new object();
			private readonly Action<string> _callback;
			private string _lastMessage = "";
			internal ApiTokenStream(Action<string> callback){ _callback = callback; }
			internal void Emit(string message){
				if(string.IsNullOrEmpty(message)) return;
				string delta;
				lock(_sync){
					delta = message.StartsWith(_lastMessage, StringComparison.Ordinal) ? message.Substring(_lastMessage.Length) : message;
					_lastMessage = message;
				}
				if(!string.IsNullOrEmpty(delta)) _callback(delta);
			}
		}
		private static string ReadNativeUtf8(IntPtr ptr){
			if(ptr == IntPtr.Zero) return null;
			var length = 0;
			while(Marshal.ReadByte(ptr, length) != 0) length++;
			var buffer = new byte[length];
			Marshal.Copy(ptr, buffer, 0, length);
			return Encoding.UTF8.GetString(buffer);
		}
		private static int FindSentenceEnd(StringBuilder sb){
			for(var i = 0; i < sb.Length - 1; i++) if((sb[i] == '.' || sb[i] == '!' || sb[i] == '?') && char.IsWhiteSpace(sb[i + 1])) return i;
			return -1;
		}
		private static void TokenCallback(string slotName, string think, string message, int tokenCount, int tokensTotal, double ftTime, int tool){
			var apiTokenStream = tool == 0 ? GetApiTokenStream(slotName) : null;
			if(apiTokenStream != null){
				try{ apiTokenStream.Emit(message); } catch{}
				return;
			}
			var elapsed = SwRate.Elapsed.TotalSeconds;
			if(elapsed >= 1.0) SwRate.Restart();
			if(tool > 0){// 1 == tool output, 2 = CMD output stream, 3 = tool call details
				try{
					MainForm.BeginInvoke(new MethodInvoker(() => {
						var displayMessage = Tools.FormatToolMessageForDisplay(tool, message);
						if(_cntToolMsg == null){
							_cntToolMsg = MainForm.AddMessage(MessageRole.Tool, "", displayMessage);
							switch(tool){
								case 1:
								case 2: _cntToolMsg.SetRoleText(Resources.Tool_Output);
									break;
								case 3: _cntToolMsg.SetRoleText(Resources.Tool_Call);
									break;
							}
						}
						else{ _cntToolMsg.UpdateText("", displayMessage, true); }
						if(tool == 1) _cntToolMsg.ApiContent = message ?? "";
						_cntAssMsg = null;
						MainForm.labelTokens.Text = FormatTokenLabel(tokensTotal);
						if(tool == 1 || tool == 3) _cntToolMsg = null;
					}));
				} catch(ObjectDisposedException){}
				return;
			}
			_msgTokenCount += tokenCount;
			_genTokenTotal += tokenCount;
			var renderToken = !MainForm.Rendering;
			try{
				MainForm.BeginInvoke((MethodInvoker)(() => {
					try{
						MainForm.Rendering = true;
						if(FirstToken){
							MainForm.labelPreGen.Text = Resources.First_token_time__ + ftTime + Resources._s;
							FirstToken = false;
						}
						if(elapsed >= 1.0){
							var callsPerSecond = _msgTokenCount/elapsed;
							MainForm.labelTPS.Text = string.Format(Resources._0_F2__Tok_s, callsPerSecond);
							_msgTokenCount = 0;
						}
						if(_cntAssMsg == null) _cntAssMsg = AddGeneratedAssistantMessage(slotName, "", "");
						var lastThink = _cntAssMsg.checkThink.Checked;
						_cntAssMsg.UpdateText(think, message, renderToken);
						if(Common.Speak && !_cntAssMsg.checkThink.Checked && !lastThink && !string.IsNullOrWhiteSpace(message)){
							var i = _cntAssMsg.TTSPosition;
							for(; i < _cntAssMsg.Message.Length; i++){
								var ch = _cntAssMsg.Message[i];
								if(ch != '`' && ch != '*' && ch != '_' && ch != '#') TTS.Pending.Append(ch);
							}
							_cntAssMsg.TTSPosition = i;
							while((i = FindSentenceEnd(TTS.Pending)) >= 0){
								var sentence = TTS.Pending.ToString(0, i + 1).Trim();
								if(!string.IsNullOrWhiteSpace(sentence)) TTS.QueueSpeech(sentence);
								TTS.Pending.Remove(0, i + 1);
								while(TTS.Pending.Length > 0 && char.IsWhiteSpace(TTS.Pending[0])) TTS.Pending.Remove(0, 1);
							}
						}
						MainForm.labelTokens.Text = FormatTokenLabel(tokensTotal);
					} catch(ObjectDisposedException){} finally{ MainForm.Rendering = false; }
				}));
			} catch(ObjectDisposedException){}
		}
	}
}

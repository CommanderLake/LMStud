using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using LMStud.Properties;
namespace LMStud{
	[Flags]
	internal enum ModelSlotUse{
		None = 0,
		Chat = 1,
		Dialectic = 2,
		Tool = 4,
		Server = 8
	}
	internal enum ModelSlotSource{
		Local,
		Api,
		Mcp
	}
	internal enum McpSlotTransport{
		Stdio,
		Http
	}
	internal sealed class ModelSlot{
		public string ApiBaseUrl { get; set; }
		public string ApiKey { get; set; }
		public string ApiModel { get; set; }
		public int ApiReasoningEffort { get; set; }
		public int ApiReasoningSummary { get; set; }
		public bool ApiStore { get; set; }
		public string Instructions { get; set; }
		public string LocalPath { get; set; }
		public string McpAuthHeader { get; set; }
		public string McpCommandLine { get; set; }
		public int McpTimeoutMs { get; set; }
		public McpSlotTransport McpTransport { get; set; }
		public string McpUrl { get; set; }
		public string McpWorkingDirectory { get; set; }
		public string Name { get; set; }
		public bool OverrideSystemPrompt { get; set; }
		public ModelSlotSource Source { get; set; }
		public string ToolName { get; set; }
		public ModelSlotUse Use { get; set; }
		internal ModelSlot Clone(){
			return new ModelSlot{
				ApiBaseUrl = ApiBaseUrl, ApiKey = ApiKey, ApiModel = ApiModel, ApiReasoningEffort = ApiReasoningEffort, ApiReasoningSummary = ApiReasoningSummary,
				ApiStore = ApiStore, Instructions = Instructions, LocalPath = LocalPath, McpAuthHeader = McpAuthHeader, McpCommandLine = McpCommandLine, McpTimeoutMs = McpTimeoutMs,
				McpTransport = McpTransport, McpUrl = McpUrl, McpWorkingDirectory = McpWorkingDirectory, Name = Name, OverrideSystemPrompt = OverrideSystemPrompt,
				Source = Source, ToolName = ToolName, Use = Use
			};
		}
		internal bool HasUse(ModelSlotUse use){return (Use & use) == use;}
		internal string GetInstructionsOrDefault(){return OverrideSystemPrompt ? Instructions ?? "" : Common.SystemPrompt ?? "";}
		internal string GetMcpEndpoint(){ return McpTransport == McpSlotTransport.Http ? McpUrl ?? "" : McpCommandLine ?? ""; }
		internal string DisplayModel(){
			if(Source == ModelSlotSource.Mcp){
				var endpoint = GetMcpEndpoint();
				return string.IsNullOrWhiteSpace(endpoint) ? Resources._MCP_server_ : endpoint;
			}
			if(Source == ModelSlotSource.Api) return string.IsNullOrWhiteSpace(ApiModel) ? Resources._API_model_ : ApiModel;
			if(string.IsNullOrWhiteSpace(LocalPath)) return Resources._local_model_;
			return Path.GetFileNameWithoutExtension(LocalPath.TrimEnd('\\', '/'));
		}
		internal string ResolveLocalPath(){
			if(string.IsNullOrWhiteSpace(LocalPath)) return "";
			if(Path.IsPathRooted(LocalPath)) return LocalPath;
			return Path.Combine(Common.ModelsDir ?? "", LocalPath);
		}
	}
	internal sealed class ModelSlotLockLease : IDisposable{
		private SemaphoreSlim _semaphore;
		internal ModelSlotLockLease(SemaphoreSlim semaphore){_semaphore = semaphore;}
		public void Dispose(){
			var semaphore = _semaphore;
			if(semaphore == null) return;
			_semaphore = null;
			semaphore.Release();
		}
	}
	internal static class ModelSlotManager{
		internal const string MainSlotName = "main";
		private static readonly Regex InvalidToolChars = new Regex("[^a-zA-Z0-9_]", RegexOptions.Compiled);
		private static readonly object Sync = new object();
		private static readonly object SlotLocksSync = new object();
		private static readonly string SlotsFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LM Stud");
		private static readonly string SlotsFile = Path.Combine(SlotsFolder, "ModelSlots.json");
		private static readonly List<ModelSlot> SlotsInternal = new List<ModelSlot>();
		private static readonly Dictionary<string, SemaphoreSlim> SlotLocks = new Dictionary<string, SemaphoreSlim>(StringComparer.OrdinalIgnoreCase);
		private static string ActiveChatSlotName { get; set; } = MainSlotName;
		internal static IEnumerable<ModelSlot> Slots{
			get{
				lock(Sync) return SlotsInternal.Select(slot => slot.Clone()).ToList();
			}
		}
		internal static void Load(){
			lock(Sync){
				SlotsInternal.Clear();
				var config = LoadConfigFile();
				if(config?.Slots != null) SlotsInternal.AddRange(config.Slots.Where(IsUsableSlot).Select(NormalizeSlot));
				ActiveChatSlotName = string.IsNullOrWhiteSpace(config?.ActiveChatSlot) ? MainSlotName : config.ActiveChatSlot.Trim();
				EnsureMainSlot();
				EnsureSingleChatSlot();
			}
			Common.ActiveModelSlotName = ActiveChatSlotName;
		}
		private static void Save(){
			lock(Sync){
				EnsureMainSlot();
				EnsureSingleChatSlot();
				var config = new ModelSlotConfig{ ActiveChatSlot = ActiveChatSlotName, Slots = SlotsInternal.Select(slot => slot.Clone()).ToList() };
				if(!Directory.Exists(SlotsFolder)) Directory.CreateDirectory(SlotsFolder);
				File.WriteAllText(SlotsFile, SerializeConfig(config));
			}
			Common.ActiveModelSlotName = ActiveChatSlotName;
			Tools.InvalidateToolsJsonCache();
		}
		internal static ModelSlot GetSlot(string name){
			lock(Sync) return FindSlotInternal(name)?.Clone();
		}
		internal static ModelSlot GetActiveChatSlot(){
			lock(Sync){
				var active = FindActiveChatSlotInternal();
				return active?.Clone();
			}
		}
		internal static ModelSlot GetLoadedLocalSlot(){
			lock(Sync){
				if(Common.LoadedLocalSlots.Count == 0) return null;
				var active = FindSlotInternal(ActiveChatSlotName);
				if(active != null && Common.LoadedLocalSlots.TryGetValue(active.Name, out var activeItem) && SamePath(active.ResolveLocalPath(), activeItem.SubItems[1].Text) && NativeMethods.IsModelSlotLoaded(active.Name)) return active.Clone();
				var loaded = Common.LoadedLocalSlots.FirstOrDefault(kv => NativeMethods.IsModelSlotLoaded(kv.Key));
				var loadedPath = loaded.Value?.SubItems[1].Text;
				if(string.IsNullOrWhiteSpace(loadedPath)) return null;
				return SlotsInternal.FirstOrDefault(slot => slot.Source == ModelSlotSource.Local && SamePath(slot.ResolveLocalPath(), loadedPath))?.Clone();
			}
		}
		internal static List<ModelSlot> ResolveDialecticLocalSlots(){
			ModelSlot active;
			List<ModelSlot> dialecticSlots;
			lock(Sync){
				active = (FindSlotInternal(ActiveChatSlotName) ?? SlotsInternal.FirstOrDefault(slot => slot.HasUse(ModelSlotUse.Chat)) ?? FindSlotInternal(MainSlotName))?.Clone();
				dialecticSlots = SlotsInternal.Where(slot => slot.Source == ModelSlotSource.Local && slot.HasUse(ModelSlotUse.Dialectic)).Select(slot => slot.Clone()).ToList();
			}
			if(!CanServeLocalSlot(active)) return new List<ModelSlot>();
			var slots = new List<ModelSlot>{ active };
			var partner = dialecticSlots.FirstOrDefault(slot => !string.Equals(slot.Name, active?.Name, StringComparison.OrdinalIgnoreCase) && CanServeLocalSlot(slot));
			if(partner != null) slots.Add(partner);
			return slots;
		}
		internal static ModelSlotLockLease TryEnterSlot(string slotName, int millisecondsTimeout){
			var slotLock = GetSlotLock(slotName);
			return slotLock.Wait(millisecondsTimeout) ? new ModelSlotLockLease(slotLock) : null;
		}
		internal static ModelSlotLockLease EnterSlot(string slotName){
			var slotLock = GetSlotLock(slotName);
			slotLock.Wait(-1);
			return new ModelSlotLockLease(slotLock);
		}
		internal static List<ModelSlotLockLease> TryEnterSlots(IEnumerable<string> slotNames, int millisecondsTimeout){
			var leases = new List<ModelSlotLockLease>();
			foreach(var lease in NormalizeSlotNames(slotNames).Select(slotName => TryEnterSlot(slotName, millisecondsTimeout))){
				if(lease == null){
					ReleaseSlots(leases);
					return null;
				}
				leases.Add(lease);
			}
			return leases;
		}
		internal static List<ModelSlotLockLease> EnterSlots(IEnumerable<string> slotNames){
			var leases = new List<ModelSlotLockLease>();
			foreach(var slotName in NormalizeSlotNames(slotNames)) leases.Add(EnterSlot(slotName));
			return leases;
		}
		internal static void ReleaseSlots(IEnumerable<ModelSlotLockLease> leases){
			if(leases == null) return;
			foreach(var lease in leases) lease?.Dispose();
		}
		internal static void AddOrUpdate(ModelSlot slot, string oldName = null){
			if(slot == null) return;
			lock(Sync){
				slot = NormalizeSlot(slot);
				var existing = FindSlotInternal(oldName ?? slot.Name);
				if(existing == null) SlotsInternal.Add(slot);
				else{
					existing.ApiBaseUrl = slot.ApiBaseUrl;
					existing.ApiKey = slot.ApiKey;
					existing.ApiModel = slot.ApiModel;
					existing.ApiReasoningEffort = slot.ApiReasoningEffort;
					existing.ApiReasoningSummary = slot.ApiReasoningSummary;
					existing.ApiStore = slot.ApiStore;
					existing.Instructions = slot.Instructions;
					existing.LocalPath = slot.LocalPath;
					existing.McpAuthHeader = slot.McpAuthHeader;
					existing.McpCommandLine = slot.McpCommandLine;
					existing.McpTimeoutMs = slot.McpTimeoutMs;
					existing.McpTransport = slot.McpTransport;
					existing.McpUrl = slot.McpUrl;
					existing.McpWorkingDirectory = slot.McpWorkingDirectory;
					existing.Name = slot.Name;
					existing.OverrideSystemPrompt = slot.OverrideSystemPrompt;
					existing.Source = slot.Source;
					existing.ToolName = slot.ToolName;
					existing.Use = slot.Use;
				}
				if(slot.HasUse(ModelSlotUse.Chat) && slot.Source != ModelSlotSource.Mcp) ActiveChatSlotName = slot.Name;
				EnsureMainSlot();
				EnsureSingleChatSlot();
			}
			Save();
		}
		internal static bool SetActiveChatSlot(string name){
			lock(Sync){
				var slot = FindSlotInternal(name);
				if(slot == null || slot.Source == ModelSlotSource.Mcp) return false;
				ActiveChatSlotName = slot.Name;
				slot.Use |= ModelSlotUse.Chat;
				EnsureSingleChatSlot();
			}
			Save();
			return true;
		}
		internal static bool Remove(string name){
			lock(Sync){
				if(string.Equals(name, MainSlotName, StringComparison.OrdinalIgnoreCase)) return false;
				var slot = FindSlotInternal(name);
				if(slot == null) return false;
				SlotsInternal.Remove(slot);
				if(string.Equals(ActiveChatSlotName, name, StringComparison.OrdinalIgnoreCase)){
					ActiveChatSlotName = MainSlotName;
					var main = FindSlotInternal(MainSlotName);
					if(main != null) main.Use |= ModelSlotUse.Chat;
				}
			}
			Save();
			return true;
		}
		internal static void LoadLocalIntoSlot(string slotName, string modelPath, bool makeChat){
			if(string.IsNullOrWhiteSpace(modelPath)) return;
			var requestedSlotName = string.IsNullOrWhiteSpace(slotName) ? MainSlotName : slotName.Trim();
			var activeChatSlot = GetActiveChatSlot();
			var loadedDifferentChatSlot = activeChatSlot?.Source == ModelSlotSource.Local && !string.Equals(activeChatSlot.Name, requestedSlotName, StringComparison.OrdinalIgnoreCase) && CanServeLocalSlot(activeChatSlot);
			lock(Sync){
				var slot = FindSlotInternal(requestedSlotName) ?? new ModelSlot{ Name = requestedSlotName, Use = ModelSlotUse.Server };
				if(!SlotsInternal.Contains(slot)) SlotsInternal.Add(slot);
				slot.Source = ModelSlotSource.Local;
				slot.LocalPath = Common.GetPathRelativeToModelsDir(modelPath);
				if(string.IsNullOrWhiteSpace(slot.ToolName)) slot.ToolName = BuildToolName(slot.Name);
				var promoteToChat = makeChat || string.Equals(slot.Name, MainSlotName, StringComparison.OrdinalIgnoreCase);
				if(loadedDifferentChatSlot && !string.Equals(slot.Name, MainSlotName, StringComparison.OrdinalIgnoreCase)) promoteToChat = false;
				if(promoteToChat){
					slot.Use |= ModelSlotUse.Chat;
					ActiveChatSlotName = slot.Name;
				}
				EnsureSingleChatSlot();
			}
			Save();
		}
		internal static void SyncMainFromLoadedLocal(){
			if(!Common.LlModelLoaded || Common.LoadedModel == null) return;
			LoadLocalIntoSlot(MainSlotName, Common.LoadedModel.SubItems[1].Text, true);
		}
		internal static ModelSlot ResolveServerSlot(string requestedModel){
			if(string.IsNullOrWhiteSpace(requestedModel)) return ResolveDefaultServerSlot();
			lock(Sync){
				var normalized = NormalizeServerModelName(requestedModel);
				var exact = SlotsInternal.FirstOrDefault(slot => IsServerRoutableSlot(slot) && string.Equals(slot.Name, normalized, StringComparison.OrdinalIgnoreCase));
				if(exact != null) return exact.Clone();
				var modelMatch = SlotsInternal.FirstOrDefault(slot => IsServerRoutableSlot(slot) && (string.Equals(slot.DisplayModel(), requestedModel, StringComparison.OrdinalIgnoreCase) ||
					string.Equals(slot.ApiModel, requestedModel, StringComparison.OrdinalIgnoreCase)));
				return modelMatch?.Clone();
			}
		}
		private static ModelSlot ResolveDefaultServerSlot(){
			List<ModelSlot> candidates;
			lock(Sync) candidates = SlotsInternal.Where(IsServerRoutableSlot).Select(slot => slot.Clone()).ToList();
			if(candidates.Count == 0){
				var fallback = GetFallbackSlot();
				if(fallback != null) candidates.Add(fallback);
			}
			return candidates.OrderByDescending(slot => slot.HasUse(ModelSlotUse.Server))
				.ThenByDescending(CanServeServerSlot)
				.ThenByDescending(IsSlotAvailable)
				.ThenBy(slot => slot.HasUse(ModelSlotUse.Chat))
				.FirstOrDefault();
		}
		internal static string GetServerModelId(ModelSlot slot){return slot == null ? "lmstud/main" : "lmstud/" + slot.Name;}
		internal static JsonNode BuildServerModels(){
			var array = Json.ArrayBuilder();
			List<ModelSlot> slots;
			lock(Sync) slots = SlotsInternal.Where(IsServerRoutableSlot).Select(slot => slot.Clone()).ToList();
			if(slots.Count == 0){
				var fallback = GetFallbackSlot();
				if(fallback != null) slots.Add(fallback);
			}
			foreach(var slot in slots){
				array.Add(Json.Object(Json.P("id", GetServerModelId(slot)), Json.P("object", "model"), Json.P("created", 0), Json.P("owned_by", "lmstud"),
					Json.P("source", slot.Source == ModelSlotSource.Api ? "api" : "local"), Json.P("display_name", slot.DisplayModel())));
			}
			return array.ToNode();
		}
		internal static bool CanServeLocalSlot(ModelSlot slot){
			if(slot == null || slot.Source != ModelSlotSource.Local) return false;
			if(!Common.LoadedLocalSlots.TryGetValue(slot.Name, out var loadedModel) || loadedModel == null) return false;
			return SamePath(slot.ResolveLocalPath(), loadedModel.SubItems[1].Text) && NativeMethods.IsModelSlotLoaded(slot.Name);
		}
		internal static List<string> GetLoadedLocalSlotNames(){
			return (from loaded in Common.LoadedLocalSlots let loadedModel = loaded.Value where loadedModel != null && loadedModel.SubItems.Count >= 2 select loaded.Key).ToList();
		}
		internal static List<string> GetLoadedLocalSlotNamesForPath(string modelPath){
			var slots = new List<string>();
			if(string.IsNullOrWhiteSpace(modelPath)) return slots;
			slots.AddRange(from loaded in Common.LoadedLocalSlots let loadedModel = loaded.Value where loadedModel != null && loadedModel.SubItems.Count >= 2 where SamePath(loadedModel.SubItems[1].Text, modelPath) select loaded.Key);
			return slots;
		}
		internal static bool CanServeApiSlot(ModelSlot slot){
			return slot != null && slot.Source == ModelSlotSource.Api && !string.IsNullOrWhiteSpace(slot.ApiBaseUrl) && !string.IsNullOrWhiteSpace(slot.ApiModel);
		}
		internal static bool CanServeMcpSlot(ModelSlot slot){
			return slot != null && slot.Source == ModelSlotSource.Mcp && McpServerManager.IsConnected(slot.Name);
		}
		private static bool CanServeServerSlot(ModelSlot slot){
			return slot != null && (slot.Source == ModelSlotSource.Api ? CanServeApiSlot(slot) : slot.Source == ModelSlotSource.Local && CanServeLocalSlot(slot));
		}
		private static bool IsSlotAvailable(ModelSlot slot){
			if(slot == null || string.IsNullOrWhiteSpace(slot.Name)) return false;
			var lease = TryEnterSlot(slot.Name, 0);
			if(lease == null) return false;
			lease.Dispose();
			return true;
		}
		internal static string GetSlotState(ModelSlot slot){
			if(slot == null) return "";
			if(slot.Source == ModelSlotSource.Mcp){
				if(string.IsNullOrWhiteSpace(slot.GetMcpEndpoint())) return Resources.Incomplete;
				return CanServeMcpSlot(slot) ? Resources.Connected : Resources.Disconnected;
			}
			if(slot.Source == ModelSlotSource.Api) return CanServeApiSlot(slot) ? Resources.Ready : Resources.Incomplete;
			var path = slot.ResolveLocalPath();
			if(string.IsNullOrWhiteSpace(path)) return Resources.Empty;
			if(!File.Exists(path)) return Resources.Missing;
			if(CanServeLocalSlot(slot)) return Resources.Loaded;
			return Resources.Cold;
		}
		internal static string FormatUse(ModelSlot slot){
			if(slot == null || slot.Use == ModelSlotUse.None) return "";
			var parts = new List<string>();
			if(slot.HasUse(ModelSlotUse.Chat)) parts.Add(Resources.Chat);
			if(slot.HasUse(ModelSlotUse.Dialectic)) parts.Add(Resources.Dialectic);
			if(slot.HasUse(ModelSlotUse.Tool)) parts.Add(Resources.Tool);
			if(slot.HasUse(ModelSlotUse.Server)) parts.Add(Resources.Server);
			return string.Join(", ", parts);
		}
		internal static JsonNode BuildModelCallTools(string callerSlotName = null){
			var tools = Json.ArrayBuilder();
			List<ModelSlot> slots;
			lock(Sync)
				slots = SlotsInternal.Where(slot => slot.HasUse(ModelSlotUse.Tool) && !IsCallerSlot(slot, callerSlotName) &&
					(slot.Source == ModelSlotSource.Api && !string.IsNullOrWhiteSpace(slot.ApiBaseUrl) && !string.IsNullOrWhiteSpace(slot.ApiModel) ||
					 slot.Source == ModelSlotSource.Local && CanServeLocalSlot(slot))).Select(slot => slot.Clone()).ToList();
			foreach(var slot in slots){
				var source = slot.Source == ModelSlotSource.Local ? "local" : "API";
				var function = Json.Object(
					Json.P("name", GetToolName(slot)),
					Json.P("description", "Ask the " + slot.Name + " " + source + " model slot for a second opinion or a specialised answer."),
					Json.P("parameters", Json.Object(
						Json.P("type", "object"),
						Json.P("properties", Json.Object(
							Json.P("prompt", Json.Object(Json.P("type", "string"), Json.P("description", "The question or task to send to the model slot."))),
							Json.P("instructions", Json.Object(Json.P("type", "string"), Json.P("description", "Optional system instructions for this call."))),
							Json.P("max_tokens", Json.Object(Json.P("type", "integer"), Json.P("description", "Optional response token limit.")))
						)),
						Json.P("required", Json.Array("prompt"))
					))
				);
				tools.Add(Json.Object(Json.P("type", "function"), Json.P("function", function)));
			}
			return tools.ToNode();
		}
		internal static bool TryExecuteToolCall(APIClient.ToolCall toolCall, out string result){
			return TryExecuteToolCall(toolCall, null, null, out result);
		}
		internal static bool TryExecuteToolCall(APIClient.ToolCall toolCall, string callerSlotName, out string result){
			return TryExecuteToolCall(toolCall, callerSlotName, null, out result);
		}
		internal static bool TryExecuteToolCall(APIClient.ToolCall toolCall, string callerSlotName, Action<string> streamCallback, out string result){
			result = null;
			if(toolCall == null || string.IsNullOrWhiteSpace(toolCall.Name)) return false;
			ModelSlot slot;
			lock(Sync)
				slot = SlotsInternal.FirstOrDefault(candidate => candidate.HasUse(ModelSlotUse.Tool) &&
					string.Equals(GetToolName(candidate), toolCall.Name, StringComparison.OrdinalIgnoreCase))?.Clone();
			if(slot == null) return false;
			if(IsCallerSlot(slot, callerSlotName)){
				result = ErrorJson(Resources.A_model_slot_cannot_call_itself_as_a_tool_);
				return true;
			}
			if(slot.Source == ModelSlotSource.Api && !CanServeApiSlot(slot)){
				result = ErrorJson(Resources.API_model_tool_slot_is_missing_an_API_URL_or_model_);
				return true;
			}
			if(slot.Source == ModelSlotSource.Local && !CanServeLocalSlot(slot)){
				result = ErrorJson(Resources.Local_model_tool_slot_is_not_loaded__ + slot.Name);
				return true;
			}
			if(slot.Source != ModelSlotSource.Api && slot.Source != ModelSlotSource.Local) return false;
			ModelSlotLockLease slotLock = null;
			try{
				var args = string.IsNullOrWhiteSpace(toolCall.Arguments) ? Json.Object() : Json.Parse(toolCall.Arguments);
				var prompt = args.GetString("prompt");
				if(string.IsNullOrWhiteSpace(prompt)){
					result = ErrorJson(Resources.prompt_is_required);
					return true;
				}
				var instructions = args.GetString("instructions");
				var maxTokens = args.GetInt("max_tokens") ?? Common.NGen;
				slotLock = TryEnterSlot(slot.Name, 300000);
				if(slotLock == null){
					result = ErrorJson(Resources.Model_tool_slot_is_busy__ + slot.Name);
					return true;
				}
				result = slot.Source == ModelSlotSource.Api ? ExecuteApiModelTool(slot, prompt, instructions, maxTokens, streamCallback) : ExecuteLocalModelTool(slot, prompt, instructions, maxTokens, streamCallback);
			} catch(Exception ex){ result = ErrorJson(ex.Message); }
			finally{ slotLock?.Dispose(); }
			return true;
		}
		internal static string BuildToolName(string slotName){
			var name = InvalidToolChars.Replace((slotName ?? "").Trim().ToLowerInvariant(), "_");
			name = name.Trim('_');
			if(string.IsNullOrWhiteSpace(name)) name = "model";
			if(char.IsDigit(name[0])) name = "model_" + name;
			return "ask_" + name;
		}
		private static string GetToolName(ModelSlot slot){return string.IsNullOrWhiteSpace(slot.ToolName) ? BuildToolName(slot.Name) : slot.ToolName.Trim();}
		private static string ErrorJson(string error){return Json.Object(Json.P("error", error)).ToJson();}
		private static string GetToolInstructions(ModelSlot slot, string instructions){return string.IsNullOrWhiteSpace(instructions) ? slot.GetInstructionsOrDefault() : instructions;}
		private static string GetLocalToolInstructions(ModelSlot slot, string instructions){
			if(!string.IsNullOrWhiteSpace(instructions)) return instructions;
			return slot.OverrideSystemPrompt ? slot.Instructions ?? "" : null;
		}
		private static string ExecuteApiModelTool(ModelSlot slot, string prompt, string instructions, int maxTokens, Action<string> streamCallback){
			var history = APIClient.BuildInputItems(new[]{ new APIClient.ChatMessage("user", prompt) });
			using(var client = new APIClient(slot.ApiBaseUrl, slot.ApiKey, slot.ApiModel, slot.ApiStore, GetToolInstructions(slot, instructions), slot.ApiReasoningEffort, slot.ApiReasoningSummary)){
				var response = client.CreateChatCompletion(history, Common.Temp, maxTokens, null, null, CancellationToken.None, streamCallback);
				return Json.Object(Json.P("slot", slot.Name), Json.P("source", "api"), Json.P("model", slot.ApiModel), Json.P("content", response.Content ?? ""),
					Json.P("reasoning", response.Reasoning ?? ""), Json.P("total_tokens", response.TotalTokens)).ToJson(JsonFormat.Indented);
			}
		}
		private static string ExecuteLocalModelTool(ModelSlot slot, string prompt, string instructions, int maxTokens, Action<string> streamCallback){
			var chatState = IntPtr.Zero;
			try{
				byte[] ignoredState;
				int tokenCount;
				APIClient.ChatCompletionResult response;
				if(!Generation.GenerateForApiServer(slot.Name, null, IntPtr.Zero, null, MessageRole.User, prompt, null, out response, out ignoredState, out chatState, out tokenCount, maxTokens, GetLocalToolInstructions(slot, instructions), streamCallback))
					return ErrorJson(Resources.Local_model_tool_slot_could_not_generate_a_response__ + slot.Name);
				return Json.Object(Json.P("slot", slot.Name), Json.P("source", "local"), Json.P("model", slot.DisplayModel()), Json.P("content", response?.Content ?? ""),
					Json.P("reasoning", response?.Reasoning ?? ""), Json.P("total_tokens", response?.TotalTokens), Json.P("tokens", tokenCount)).ToJson(JsonFormat.Indented);
			} finally{
				if(chatState != IntPtr.Zero) NativeMethods.FreeChatState(chatState);
			}
		}
		private static bool IsCallerSlot(ModelSlot slot, string callerSlotName){
			return slot != null && !string.IsNullOrWhiteSpace(callerSlotName) && string.Equals(slot.Name, callerSlotName.Trim(), StringComparison.OrdinalIgnoreCase);
		}
		private static ModelSlotConfig LoadConfigFile(){
			if(!File.Exists(SlotsFile)) return null;
			try{
				var json = File.ReadAllText(SlotsFile);
				try{ return ParseConfig(json); }
				catch{
					var repaired = RepairLegacyMalformedSlotsJson(json);
					return string.Equals(repaired, json, StringComparison.Ordinal) ? null : ParseConfig(repaired);
				}
			} catch{ return null; }
		}
		private static string RepairLegacyMalformedSlotsJson(string json){
			if(string.IsNullOrWhiteSpace(json)) return json;
			var repaired = Regex.Replace(json, "(\"Use\"\\s*:\\s*[^,}\\]\\r\\n]+)\\s*,\\s*(\\{)", "$1\r\n    },\r\n    $2");
			return AppendMissingJsonClosers(repaired);
		}
		private static string AppendMissingJsonClosers(string json){
			var stack = new Stack<char>();
			var inString = false;
			var escaping = false;
			foreach(var ch in json){
				if(inString){
					if(escaping) escaping = false;
					else if(ch == '\\') escaping = true;
					else if(ch == '"') inString = false;
					continue;
				}
				if(ch == '"'){
					inString = true;
					continue;
				}
				if(ch == '{' || ch == '[') stack.Push(ch);
				else if(ch == '}' && stack.Count > 0 && stack.Peek() == '{') stack.Pop();
				else if(ch == ']' && stack.Count > 0 && stack.Peek() == '[') stack.Pop();
			}
			if(stack.Count == 0) return json;
			var repaired = new StringBuilder(json.TrimEnd());
			while(stack.Count > 0) repaired.Append(stack.Pop() == '{' ? '}' : ']');
			return repaired.ToString();
		}
		private static string SerializeConfig(ModelSlotConfig config){
			var slots = Json.ArrayBuilder();
			foreach(var slot in config.Slots ?? new List<ModelSlot>()) slots.Add(SerializeSlot(slot));
			return Json.Object(Json.P("ActiveChatSlot", config.ActiveChatSlot), Json.P("Slots", slots)).ToJson(JsonFormat.Indented);
		}
		private static JsonNode SerializeSlot(ModelSlot slot){
			return Json.Object(
				Json.P("ApiBaseUrl", slot.ApiBaseUrl), Json.P("ApiKey", slot.ApiKey), Json.P("ApiModel", slot.ApiModel),
				Json.P("ApiReasoningEffort", slot.ApiReasoningEffort), Json.P("ApiReasoningSummary", slot.ApiReasoningSummary), Json.P("ApiStore", slot.ApiStore),
				Json.P("Instructions", slot.Instructions), Json.P("LocalPath", slot.LocalPath), Json.P("McpAuthHeader", slot.McpAuthHeader),
				Json.P("McpCommandLine", slot.McpCommandLine), Json.P("McpTimeoutMs", slot.McpTimeoutMs), Json.P("McpTransport", (int)slot.McpTransport),
				Json.P("McpUrl", slot.McpUrl), Json.P("McpWorkingDirectory", slot.McpWorkingDirectory), Json.P("Name", slot.Name),
				Json.P("OverrideSystemPrompt", slot.OverrideSystemPrompt), Json.P("Source", (int)slot.Source), Json.P("ToolName", slot.ToolName), Json.P("Use", (int)slot.Use)
			);
		}
		private static ModelSlotConfig ParseConfig(string json){
			var root = Json.Parse(json);
			if(!root.IsObject) return null;
			var config = new ModelSlotConfig{ ActiveChatSlot = root.GetString("ActiveChatSlot"), Slots = new List<ModelSlot>() };
			var slots = root["Slots"];
			if(slots.IsArray)
				foreach(var slotNode in slots){
					var slot = ParseSlot(slotNode);
					if(slot != null) config.Slots.Add(slot);
				}
			return config;
		}
		private static ModelSlot ParseSlot(JsonNode node){
			if(!node.IsObject) return null;
			return new ModelSlot{
				ApiBaseUrl = node.GetString("ApiBaseUrl"),
				ApiKey = node.GetString("ApiKey"),
				ApiModel = node.GetString("ApiModel"),
				ApiReasoningEffort = node.GetInt("ApiReasoningEffort") ?? 0,
				ApiReasoningSummary = node.GetInt("ApiReasoningSummary") ?? 0,
				ApiStore = node.GetBool("ApiStore") ?? false,
				Instructions = node.GetString("Instructions"),
				LocalPath = node.GetString("LocalPath"),
				McpAuthHeader = node.GetString("McpAuthHeader"),
				McpCommandLine = node.GetString("McpCommandLine"),
				McpTimeoutMs = node.GetInt("McpTimeoutMs") ?? 0,
				McpTransport = (McpSlotTransport)ReadEnumValue(node["McpTransport"], 0, typeof(McpSlotTransport)),
				McpUrl = node.GetString("McpUrl"),
				McpWorkingDirectory = node.GetString("McpWorkingDirectory"),
				Name = node.GetString("Name"),
				OverrideSystemPrompt = node.GetBool("OverrideSystemPrompt") ?? false,
				Source = (ModelSlotSource)ReadEnumValue(node["Source"], 0, typeof(ModelSlotSource)),
				ToolName = node.GetString("ToolName"),
				Use = (ModelSlotUse)ReadEnumValue(node["Use"], 0, typeof(ModelSlotUse))
			};
		}
		private static int ReadEnumValue(JsonNode token, int fallback, Type enumType = null){
			var numeric = token.AsInt();
			if(numeric.HasValue) return numeric.Value;
			var text = token.AsString();
			if(int.TryParse(text, out var parsed)) return parsed;
			if(enumType != null && !string.IsNullOrWhiteSpace(text)){
				try{ return Convert.ToInt32(Enum.Parse(enumType, text.Replace("|", ","), true)); }
				catch{}
			}
			return fallback;
		}
		private static bool IsUsableSlot(ModelSlot slot){return !string.IsNullOrWhiteSpace(slot?.Name);}
		private static ModelSlot NormalizeSlot(ModelSlot slot){
			slot = slot.Clone();
			slot.Name = string.IsNullOrWhiteSpace(slot.Name) ? MainSlotName : slot.Name.Trim();
			slot.ApiBaseUrl = slot.ApiBaseUrl?.Trim() ?? "";
			slot.ApiKey = slot.ApiKey?.Trim() ?? "";
			slot.ApiModel = slot.ApiModel?.Trim() ?? "";
			slot.ApiReasoningEffort = NormalizeReasoningIndex(slot.ApiReasoningEffort, Common.ReasoningEffortValues.Length);
			slot.ApiReasoningSummary = NormalizeReasoningIndex(slot.ApiReasoningSummary, Common.ReasoningSummaryValues.Length);
			slot.LocalPath = slot.LocalPath?.Trim() ?? "";
			slot.McpAuthHeader = slot.McpAuthHeader?.Trim() ?? "";
			slot.McpCommandLine = slot.McpCommandLine?.Trim() ?? "";
			slot.McpUrl = slot.McpUrl?.Trim() ?? "";
			slot.McpWorkingDirectory = slot.McpWorkingDirectory?.Trim() ?? "";
			slot.Instructions = slot.Instructions ?? "";
			if(!slot.OverrideSystemPrompt && !string.IsNullOrWhiteSpace(slot.Instructions)) slot.OverrideSystemPrompt = true;
			if(slot.Source == ModelSlotSource.Mcp && slot.McpTransport == McpSlotTransport.Http && string.IsNullOrWhiteSpace(slot.McpUrl) && LooksLikeHttpUrl(slot.McpCommandLine)){
				slot.McpUrl = slot.McpCommandLine;
				slot.McpCommandLine = "";
			}
			if(slot.McpTimeoutMs <= 0) slot.McpTimeoutMs = McpServerManager.DefaultTimeoutMs;
			if(slot.Source == ModelSlotSource.Mcp){
				slot.Use &= ModelSlotUse.Tool;
				slot.OverrideSystemPrompt = false;
				slot.Instructions = "";
			}
			slot.ToolName = string.IsNullOrWhiteSpace(slot.ToolName) ? BuildToolName(slot.Name) : slot.ToolName.Trim();
			return slot;
		}
		private static bool LooksLikeHttpUrl(string value){
			return !string.IsNullOrWhiteSpace(value) && (value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || value.StartsWith("https://", StringComparison.OrdinalIgnoreCase));
		}
		private static void EnsureMainSlot(){
			if(FindSlotInternal(MainSlotName) != null) return;
			SlotsInternal.Insert(0, CreateDefaultMainSlot());
		}
		private static void EnsureSingleChatSlot(){
			var active = FindActiveChatSlotInternal();
			if(active == null) return;
			ActiveChatSlotName = active.Name;
			foreach(var slot in SlotsInternal){
				if(ReferenceEquals(slot, active)) slot.Use |= ModelSlotUse.Chat;
				else slot.Use &= ~ModelSlotUse.Chat;
			}
		}
		private static ModelSlot CreateDefaultMainSlot(){
			return new ModelSlot{
				Name = MainSlotName, Source = ModelSlotSource.Local, LocalPath = Settings.Default.LastModel ?? "", ToolName = BuildToolName(MainSlotName),
				Use = ModelSlotUse.Chat | ModelSlotUse.Server
			};
		}
		private static ModelSlot FindSlotInternal(string name){
			if(string.IsNullOrWhiteSpace(name)) return null;
			return SlotsInternal.FirstOrDefault(slot => string.Equals(slot.Name, name.Trim(), StringComparison.OrdinalIgnoreCase));
		}
		private static ModelSlot FindActiveChatSlotInternal(){
			var active = FindSlotInternal(ActiveChatSlotName);
			if(active != null && active.Source != ModelSlotSource.Mcp) return active;
			var marked = SlotsInternal.FirstOrDefault(slot => slot.Source != ModelSlotSource.Mcp && slot.HasUse(ModelSlotUse.Chat));
			if(marked != null) return marked;
			var main = FindSlotInternal(MainSlotName);
			if(main != null && main.Source != ModelSlotSource.Mcp) return main;
			return SlotsInternal.FirstOrDefault(slot => slot.Source != ModelSlotSource.Mcp);
		}
		private static bool IsServerRoutableSlot(ModelSlot slot){
			return slot != null && slot.Source != ModelSlotSource.Mcp && (slot.HasUse(ModelSlotUse.Server) || slot.HasUse(ModelSlotUse.Chat));
		}
		private static string NormalizeServerModelName(string requestedModel){
			var model = requestedModel.Trim();
			if(model.StartsWith("lmstud/", StringComparison.OrdinalIgnoreCase)) model = model.Substring("lmstud/".Length);
			return model;
		}
		private static int NormalizeReasoningIndex(int index, int valueCount){
			return index > 0 && index < valueCount ? index : 0;
		}
		private static bool SamePath(string a, string b){
			if(string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b)) return false;
			try{
				a = Path.GetFullPath(a).TrimEnd('\\', '/');
				b = Path.GetFullPath(b).TrimEnd('\\', '/');
			} catch{}
			return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
		}
		private static ModelSlot GetFallbackSlot(){
			if(Common.LlModelLoaded && Common.LoadedModel != null)
				return new ModelSlot{ Name = MainSlotName, Source = ModelSlotSource.Local, LocalPath = Common.LoadedModel.SubItems[1].Text };
			foreach(var loaded in Common.LoadedLocalSlots)
				if(loaded.Value != null && NativeMethods.IsModelSlotLoaded(loaded.Key)) return new ModelSlot{ Name = loaded.Key, Source = ModelSlotSource.Local, LocalPath = loaded.Value.SubItems[1].Text };
			return null;
		}
		private static SemaphoreSlim GetSlotLock(string slotName){
			slotName = NormalizeSlotName(slotName);
			lock(SlotLocksSync){
				if(!SlotLocks.TryGetValue(slotName, out var slotLock)){
					slotLock = new SemaphoreSlim(1, 1);
					SlotLocks[slotName] = slotLock;
				}
				return slotLock;
			}
		}
		private static string NormalizeSlotName(string slotName){
			return string.IsNullOrWhiteSpace(slotName) ? MainSlotName : slotName.Trim();
		}
		private static IEnumerable<string> NormalizeSlotNames(IEnumerable<string> slotNames){
			return (slotNames ?? new[]{ MainSlotName }).Select(NormalizeSlotName).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToList();
		}
		private sealed class ModelSlotConfig{
			public string ActiveChatSlot { get; set; }
			public List<ModelSlot> Slots { get; set; }
		}
	}
}

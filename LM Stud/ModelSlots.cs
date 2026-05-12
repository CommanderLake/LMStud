using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using LMStud.Properties;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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
		Api
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
		public string Name { get; set; }
		public ModelSlotSource Source { get; set; }
		public string ToolName { get; set; }
		public ModelSlotUse Use { get; set; }
		internal ModelSlot Clone(){
			return new ModelSlot{
				ApiBaseUrl = ApiBaseUrl, ApiKey = ApiKey, ApiModel = ApiModel, ApiReasoningEffort = ApiReasoningEffort, ApiReasoningSummary = ApiReasoningSummary,
				ApiStore = ApiStore, Instructions = Instructions, LocalPath = LocalPath, Name = Name, Source = Source, ToolName = ToolName, Use = Use
			};
		}
		internal bool HasUse(ModelSlotUse use){return (Use & use) == use;}
		internal string GetInstructionsOrDefault(){return string.IsNullOrWhiteSpace(Instructions) ? Common.SystemPrompt : Instructions;}
		internal string DisplayModel(){
			if(Source == ModelSlotSource.Api) return string.IsNullOrWhiteSpace(ApiModel) ? "(API model)" : ApiModel;
			if(string.IsNullOrWhiteSpace(LocalPath)) return "(local model)";
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
		private const string MainSlotName = "main";
		private static readonly Regex InvalidToolChars = new Regex("[^a-zA-Z0-9_]", RegexOptions.Compiled);
		private static readonly object Sync = new object();
		private static readonly object SlotLocksSync = new object();
		private static readonly string SlotsFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LM Stud");
		private static readonly string SlotsFile = Path.Combine(SlotsFolder, "ModelSlots.json");
		private static readonly List<ModelSlot> SlotsInternal = new List<ModelSlot>();
		private static readonly Dictionary<string, SemaphoreSlim> SlotLocks = new Dictionary<string, SemaphoreSlim>(StringComparer.OrdinalIgnoreCase);
		internal static string ActiveChatSlotName { get; private set; } = MainSlotName;
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
		internal static void Save(){
			lock(Sync){
				EnsureMainSlot();
				EnsureSingleChatSlot();
				var config = new ModelSlotConfig{ ActiveChatSlot = ActiveChatSlotName, Slots = SlotsInternal.Select(slot => slot.Clone()).ToList() };
				if(!Directory.Exists(SlotsFolder)) Directory.CreateDirectory(SlotsFolder);
				File.WriteAllText(SlotsFile, JsonConvert.SerializeObject(config, Formatting.Indented));
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
			var partner = dialecticSlots.FirstOrDefault(slot => !string.Equals(slot.Name, active.Name, StringComparison.OrdinalIgnoreCase) && CanServeLocalSlot(slot));
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
			foreach(var slotName in NormalizeSlotNames(slotNames)){
				var lease = TryEnterSlot(slotName, millisecondsTimeout);
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
					existing.Name = slot.Name;
					existing.Source = slot.Source;
					existing.ToolName = slot.ToolName;
					existing.Use = slot.Use;
				}
				if(slot.HasUse(ModelSlotUse.Chat)) ActiveChatSlotName = slot.Name;
				EnsureMainSlot();
				EnsureSingleChatSlot();
			}
			Save();
		}
		internal static bool SetActiveChatSlot(string name){
			lock(Sync){
				var slot = FindSlotInternal(name);
				if(slot == null) return false;
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
				slot.LocalPath = ToStoredLocalPath(modelPath);
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
				var fallback = GetFallbackRuntimeSlot();
				if(fallback != null) candidates.Add(fallback);
			}
			return candidates.OrderByDescending(slot => slot.HasUse(ModelSlotUse.Server))
				.ThenByDescending(CanServeServerSlot)
				.ThenByDescending(IsSlotAvailable)
				.ThenBy(slot => slot.HasUse(ModelSlotUse.Chat))
				.FirstOrDefault();
		}
		internal static string GetServerModelId(ModelSlot slot){return slot == null ? "lmstud/main" : "lmstud/" + slot.Name;}
		internal static JArray BuildServerModels(){
			var array = new JArray();
			List<ModelSlot> slots;
			lock(Sync) slots = SlotsInternal.Where(IsServerRoutableSlot).Select(slot => slot.Clone()).ToList();
			if(slots.Count == 0){
				var fallback = GetFallbackRuntimeSlot();
				if(fallback != null) slots.Add(fallback);
			}
			foreach(var slot in slots){
				array.Add(new JObject{
					["id"] = GetServerModelId(slot), ["object"] = "model", ["created"] = 0, ["owned_by"] = "lmstud", ["source"] = slot.Source == ModelSlotSource.Api ? "api" : "local",
					["display_name"] = slot.DisplayModel()
				});
			}
			return array;
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
		private static bool CanServeServerSlot(ModelSlot slot){
			return slot != null && (slot.Source == ModelSlotSource.Api ? CanServeApiSlot(slot) : CanServeLocalSlot(slot));
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
			if(slot.Source == ModelSlotSource.Api) return CanServeApiSlot(slot) ? "Ready" : "Incomplete";
			var path = slot.ResolveLocalPath();
			if(string.IsNullOrWhiteSpace(path)) return "Empty";
			if(!File.Exists(path)) return "Missing";
			if(CanServeLocalSlot(slot)) return "Loaded";
			return "Cold";
		}
		internal static string FormatUse(ModelSlot slot){
			if(slot == null || slot.Use == ModelSlotUse.None) return "";
			var parts = new List<string>();
			if(slot.HasUse(ModelSlotUse.Chat)) parts.Add("Chat");
			if(slot.HasUse(ModelSlotUse.Dialectic)) parts.Add("Dialectic");
			if(slot.HasUse(ModelSlotUse.Tool)) parts.Add("Tool");
			if(slot.HasUse(ModelSlotUse.Server)) parts.Add("Server");
			return string.Join(", ", parts);
		}
		internal static JArray BuildModelCallTools(){
			var tools = new JArray();
			List<ModelSlot> slots;
			lock(Sync)
				slots = SlotsInternal.Where(slot => slot.HasUse(ModelSlotUse.Tool) && slot.Source == ModelSlotSource.Api && !string.IsNullOrWhiteSpace(slot.ApiBaseUrl) &&
					!string.IsNullOrWhiteSpace(slot.ApiModel)).Select(slot => slot.Clone()).ToList();
			foreach(var slot in slots){
				var function = new JObject{
					["name"] = GetToolName(slot),
					["description"] = "Ask the " + slot.Name + " model slot for a second opinion or a specialised answer.",
					["parameters"] = new JObject{
						["type"] = "object",
						["properties"] = new JObject{
							["prompt"] = new JObject{ ["type"] = "string", ["description"] = "The question or task to send to the model slot." },
							["instructions"] = new JObject{ ["type"] = "string", ["description"] = "Optional system instructions for this call." },
							["max_tokens"] = new JObject{ ["type"] = "integer", ["description"] = "Optional response token limit." }
						},
						["required"] = new JArray("prompt")
					}
				};
				tools.Add(new JObject{ ["type"] = "function", ["function"] = function });
			}
			return tools;
		}
		internal static bool TryExecuteToolCall(APIClient.ToolCall toolCall, out string result){
			result = null;
			if(toolCall == null || string.IsNullOrWhiteSpace(toolCall.Name)) return false;
			ModelSlot slot;
			lock(Sync)
				slot = SlotsInternal.FirstOrDefault(candidate => candidate.HasUse(ModelSlotUse.Tool) &&
					string.Equals(GetToolName(candidate), toolCall.Name, StringComparison.OrdinalIgnoreCase))?.Clone();
			if(slot == null) return false;
			if(slot.Source != ModelSlotSource.Api){
				result = JsonConvert.SerializeObject(new{ error = "Only API-backed model slots can be called as tools during generation." });
				return true;
			}
			if(!CanServeApiSlot(slot)){
				result = JsonConvert.SerializeObject(new{ error = "API model tool slot is missing an API URL or model." });
				return true;
			}
			try{
				var args = string.IsNullOrWhiteSpace(toolCall.Arguments) ? new JObject() : JObject.Parse(toolCall.Arguments);
				var prompt = args.Value<string>("prompt");
				if(string.IsNullOrWhiteSpace(prompt)){
					result = JsonConvert.SerializeObject(new{ error = "prompt is required" });
					return true;
				}
				var instructions = args.Value<string>("instructions");
				var maxTokens = args.Value<int?>("max_tokens") ?? Common.NGen;
				var history = APIClient.BuildInputItems(new[]{ new APIClient.ChatMessage("user", prompt) });
				using(var client = new APIClient(slot.ApiBaseUrl, slot.ApiKey, slot.ApiModel, slot.ApiStore, string.IsNullOrWhiteSpace(instructions) ? slot.GetInstructionsOrDefault() : instructions,
					slot.ApiReasoningEffort, slot.ApiReasoningSummary)){
					var response = client.CreateChatCompletion(history, Common.Temp, maxTokens, null, null, System.Threading.CancellationToken.None);
					result = JsonConvert.SerializeObject(new{
						slot = slot.Name, model = slot.ApiModel, content = response.Content ?? "", reasoning = response.Reasoning ?? "", total_tokens = response.TotalTokens
					}, Formatting.Indented);
				}
			} catch(Exception ex){ result = JsonConvert.SerializeObject(new{ error = ex.Message }); }
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
		private static ModelSlotConfig LoadConfigFile(){
			if(!File.Exists(SlotsFile)) return null;
			try{ return JsonConvert.DeserializeObject<ModelSlotConfig>(File.ReadAllText(SlotsFile)); }
			catch{ return null; }
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
			slot.ToolName = string.IsNullOrWhiteSpace(slot.ToolName) ? BuildToolName(slot.Name) : slot.ToolName.Trim();
			return slot;
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
			return FindSlotInternal(ActiveChatSlotName) ?? SlotsInternal.FirstOrDefault(slot => slot.HasUse(ModelSlotUse.Chat)) ?? FindSlotInternal(MainSlotName);
		}
		private static bool IsServerRoutableSlot(ModelSlot slot){
			return slot != null && (slot.HasUse(ModelSlotUse.Server) || slot.HasUse(ModelSlotUse.Chat));
		}
		private static string NormalizeServerModelName(string requestedModel){
			var model = requestedModel.Trim();
			if(model.StartsWith("lmstud/", StringComparison.OrdinalIgnoreCase)) model = model.Substring("lmstud/".Length);
			return model;
		}
		private static string ToStoredLocalPath(string modelPath){
			if(string.IsNullOrWhiteSpace(modelPath)) return "";
			var modelsDir = Common.ModelsDir ?? "";
			if(!string.IsNullOrWhiteSpace(modelsDir) && modelPath.StartsWith(modelsDir, StringComparison.OrdinalIgnoreCase)) return modelPath.Substring(modelsDir.Length);
			return modelPath;
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
		private static ModelSlot GetFallbackRuntimeSlot(){
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

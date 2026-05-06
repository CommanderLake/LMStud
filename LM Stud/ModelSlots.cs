using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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
		public bool ApiStore { get; set; }
		public string Instructions { get; set; }
		public string LocalPath { get; set; }
		public string Name { get; set; }
		public ModelSlotSource Source { get; set; }
		public string ToolName { get; set; }
		public ModelSlotUse Use { get; set; }
		internal ModelSlot Clone(){
			return new ModelSlot{
				ApiBaseUrl = ApiBaseUrl, ApiKey = ApiKey, ApiModel = ApiModel, ApiStore = ApiStore, Instructions = Instructions, LocalPath = LocalPath, Name = Name, Source = Source,
				ToolName = ToolName, Use = Use
			};
		}
		internal bool HasUse(ModelSlotUse use){return (Use & use) == use;}
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
	internal static class ModelSlotManager{
		private const string MainSlotName = "main";
		private static readonly Regex InvalidToolChars = new Regex("[^a-zA-Z0-9_]", RegexOptions.Compiled);
		private static readonly object Sync = new object();
		private static readonly string SlotsFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LM Stud");
		private static readonly string SlotsFile = Path.Combine(SlotsFolder, "ModelSlots.json");
		private static readonly List<ModelSlot> SlotsInternal = new List<ModelSlot>();
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
				var active = FindSlotInternal(ActiveChatSlotName) ?? SlotsInternal.FirstOrDefault(slot => slot.HasUse(ModelSlotUse.Chat)) ?? FindSlotInternal(MainSlotName);
				return active?.Clone();
			}
		}
		internal static ModelSlot GetLoadedLocalSlot(){
			lock(Sync){
				if(!Common.LlModelLoaded || Common.LoadedModel == null) return null;
				var loadedPath = Common.LoadedModel.SubItems[1].Text;
				return SlotsInternal.FirstOrDefault(slot => slot.Source == ModelSlotSource.Local && SamePath(slot.ResolveLocalPath(), loadedPath))?.Clone();
			}
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
			lock(Sync){
				var slot = FindSlotInternal(slotName) ?? new ModelSlot{ Name = string.IsNullOrWhiteSpace(slotName) ? MainSlotName : slotName.Trim(), Use = ModelSlotUse.Server };
				if(!SlotsInternal.Contains(slot)) SlotsInternal.Add(slot);
				slot.Source = ModelSlotSource.Local;
				slot.LocalPath = ToStoredLocalPath(modelPath);
				if(string.IsNullOrWhiteSpace(slot.ToolName)) slot.ToolName = BuildToolName(slot.Name);
				if(makeChat || string.Equals(slot.Name, MainSlotName, StringComparison.OrdinalIgnoreCase)){
					slot.Use |= ModelSlotUse.Chat | ModelSlotUse.Server;
					ActiveChatSlotName = slot.Name;
				}
				EnsureSingleChatSlot();
			}
			Save();
		}
		internal static void SyncMainFromApiSettings(){
			lock(Sync){
				var main = FindSlotInternal(MainSlotName) ?? CreateDefaultMainSlot();
				if(!SlotsInternal.Contains(main)) SlotsInternal.Add(main);
				main.Source = ModelSlotSource.Api;
				main.ApiBaseUrl = Common.APIClientUrl;
				main.ApiKey = Common.APIClientKey;
				main.ApiModel = Common.APIClientModel;
				main.ApiStore = Common.APIClientStore;
				main.Use |= ModelSlotUse.Chat | ModelSlotUse.Server;
				ActiveChatSlotName = main.Name;
				EnsureSingleChatSlot();
			}
			Save();
		}
		internal static void SyncMainFromLoadedLocal(){
			if(!Common.LlModelLoaded || Common.LoadedModel == null) return;
			LoadLocalIntoSlot(MainSlotName, Common.LoadedModel.SubItems[1].Text, true);
		}
		internal static ModelSlot ResolveServerSlot(string requestedModel){
			lock(Sync){
				if(string.IsNullOrWhiteSpace(requestedModel)) return GetActiveChatSlot();
				var normalized = NormalizeServerModelName(requestedModel);
				var exact = SlotsInternal.FirstOrDefault(slot => string.Equals(slot.Name, normalized, StringComparison.OrdinalIgnoreCase));
				if(exact != null) return exact.Clone();
				var modelMatch = SlotsInternal.FirstOrDefault(slot => string.Equals(slot.DisplayModel(), requestedModel, StringComparison.OrdinalIgnoreCase) ||
					string.Equals(slot.ApiModel, requestedModel, StringComparison.OrdinalIgnoreCase));
				return modelMatch?.Clone();
			}
		}
		internal static string GetServerModelId(ModelSlot slot){return slot == null ? "lmstud/main" : "lmstud/" + slot.Name;}
		internal static JArray BuildServerModels(){
			var array = new JArray();
			List<ModelSlot> slots;
			lock(Sync) slots = SlotsInternal.Where(slot => slot.HasUse(ModelSlotUse.Server) || slot.HasUse(ModelSlotUse.Chat)).Select(slot => slot.Clone()).ToList();
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
			if(slot == null || slot.Source != ModelSlotSource.Local || !Common.LlModelLoaded || Common.LoadedModel == null) return false;
			return SamePath(slot.ResolveLocalPath(), Common.LoadedModel.SubItems[1].Text);
		}
		internal static string GetSlotState(ModelSlot slot){
			if(slot == null) return "";
			if(slot.Source == ModelSlotSource.Api) return string.IsNullOrWhiteSpace(slot.ApiBaseUrl) || string.IsNullOrWhiteSpace(slot.ApiModel) ? "Incomplete" : "Ready";
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
			lock(Sync) slot = SlotsInternal.FirstOrDefault(candidate => string.Equals(GetToolName(candidate), toolCall.Name, StringComparison.OrdinalIgnoreCase))?.Clone();
			if(slot == null) return false;
			if(slot.Source != ModelSlotSource.Api){
				result = JsonConvert.SerializeObject(new{ error = "Only API-backed model slots can be called as tools during generation." });
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
				using(var client = new APIClient(slot.ApiBaseUrl, slot.ApiKey, slot.ApiModel, false, string.IsNullOrWhiteSpace(instructions) ? slot.Instructions ?? Common.SystemPrompt : instructions)){
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
			slot.LocalPath = slot.LocalPath?.Trim() ?? "";
			slot.ToolName = string.IsNullOrWhiteSpace(slot.ToolName) ? BuildToolName(slot.Name) : slot.ToolName.Trim();
			return slot;
		}
		private static void EnsureMainSlot(){
			if(FindSlotInternal(MainSlotName) != null) return;
			SlotsInternal.Insert(0, CreateDefaultMainSlot());
		}
		private static void EnsureSingleChatSlot(){
			var active = FindSlotInternal(ActiveChatSlotName) ?? SlotsInternal.FirstOrDefault(slot => slot.HasUse(ModelSlotUse.Chat)) ?? FindSlotInternal(MainSlotName);
			if(active == null) return;
			ActiveChatSlotName = active.Name;
			foreach(var slot in SlotsInternal){
				if(ReferenceEquals(slot, active)) slot.Use |= ModelSlotUse.Chat;
				else slot.Use &= ~ModelSlotUse.Chat;
			}
		}
		private static ModelSlot CreateDefaultMainSlot(){
			if(Common.APIClientEnable && !string.IsNullOrWhiteSpace(Common.APIClientModel))
				return new ModelSlot{
					Name = MainSlotName, Source = ModelSlotSource.Api, ApiBaseUrl = Common.APIClientUrl, ApiKey = Common.APIClientKey, ApiModel = Common.APIClientModel,
					ApiStore = Common.APIClientStore, ToolName = BuildToolName(MainSlotName), Use = ModelSlotUse.Chat | ModelSlotUse.Server
				};
			return new ModelSlot{
				Name = MainSlotName, Source = ModelSlotSource.Local, LocalPath = Settings.Default.LastModel ?? "", ToolName = BuildToolName(MainSlotName),
				Use = ModelSlotUse.Chat | ModelSlotUse.Server
			};
		}
		private static ModelSlot FindSlotInternal(string name){
			if(string.IsNullOrWhiteSpace(name)) return null;
			return SlotsInternal.FirstOrDefault(slot => string.Equals(slot.Name, name.Trim(), StringComparison.OrdinalIgnoreCase));
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
		private static bool SamePath(string a, string b){
			if(string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b)) return false;
			try{
				a = Path.GetFullPath(a).TrimEnd('\\', '/');
				b = Path.GetFullPath(b).TrimEnd('\\', '/');
			} catch{}
			return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
		}
		private static ModelSlot GetFallbackRuntimeSlot(){
			if(Common.APIClientEnable && !string.IsNullOrWhiteSpace(Common.APIClientModel))
				return new ModelSlot{ Name = MainSlotName, Source = ModelSlotSource.Api, ApiBaseUrl = Common.APIClientUrl, ApiKey = Common.APIClientKey, ApiModel = Common.APIClientModel };
			if(Common.LlModelLoaded && Common.LoadedModel != null)
				return new ModelSlot{ Name = MainSlotName, Source = ModelSlotSource.Local, LocalPath = Common.LoadedModel.SubItems[1].Text };
			return null;
		}
		private sealed class ModelSlotConfig{
			public string ActiveChatSlot { get; set; }
			public List<ModelSlot> Slots { get; set; }
		}
	}
}

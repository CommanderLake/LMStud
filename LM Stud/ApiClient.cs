using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
namespace LMStud{
	internal sealed class ApiClient{
		private static readonly HttpClient HttpClient = new HttpClient();
		private readonly string _apiBaseUrl;
		private readonly string _apiKey;
		private readonly string _model;
		internal ApiClient(string apiBaseUrl, string apiKey, string model){
			_apiBaseUrl = apiBaseUrl?.Trim() ?? "";
			_apiKey = apiKey?.Trim() ?? "";
			_model = model?.Trim() ?? "";
		}
		internal string CreateChatCompletion(IReadOnlyList<ChatMessage> messages, float temperature, int maxTokens, CancellationToken cancellationToken){
			if(string.IsNullOrWhiteSpace(_apiBaseUrl)) throw new InvalidOperationException("API base URL is not configured.");
			if(string.IsNullOrWhiteSpace(_apiKey)) throw new InvalidOperationException("API key is not configured.");
			if(string.IsNullOrWhiteSpace(_model)) throw new InvalidOperationException("API model is not configured.");
			var payload = new JObject{
				["model"] = _model, ["messages"] = new JArray(messages.Select(m => new JObject{ ["role"] = m.Role, ["content"] = m.Content ?? "" })), ["temperature"] = temperature
			};
			if(maxTokens > 0) payload["max_tokens"] = maxTokens;
			using(var request = new HttpRequestMessage(HttpMethod.Post, BuildChatEndpoint(_apiBaseUrl))){
				request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
				request.Content = new StringContent(payload.ToString(Formatting.None), Encoding.UTF8, "application/json");
				using(var response = HttpClient.SendAsync(request, cancellationToken).GetAwaiter().GetResult()){
					var body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
					if(!response.IsSuccessStatusCode) throw new InvalidOperationException($"API error ({(int)response.StatusCode}): {body}");
					var json = JObject.Parse(body);
					var content = json["choices"]?.FirstOrDefault()?["message"]?["content"]?.ToString();
					if(string.IsNullOrWhiteSpace(content)) throw new InvalidOperationException("API response did not contain any content.");
					return content;
				}
			}
		}
		internal List<string> ListModels(CancellationToken cancellationToken){
			if(string.IsNullOrWhiteSpace(_apiBaseUrl)) throw new InvalidOperationException("API base URL is not configured.");
			using(var request = new HttpRequestMessage(HttpMethod.Get, BuildModelsEndpoint(_apiBaseUrl))){
				if(!string.IsNullOrWhiteSpace(_apiKey)) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
				using(var response = HttpClient.SendAsync(request, cancellationToken).GetAwaiter().GetResult()){
					var body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
					if(!response.IsSuccessStatusCode) throw new InvalidOperationException($"API error ({(int)response.StatusCode}): {body}");
					var json = JObject.Parse(body);
					var models = new List<string>();
					if(!(json["data"] is JArray data)) return models;
					models.AddRange(data.Select(item => item?["id"]?.ToString()).Where(id => !string.IsNullOrWhiteSpace(id)));
					return models;
				}
			}
		}
		private static string BuildChatEndpoint(string apiBaseUrl){
			var normalized = apiBaseUrl.EndsWith("/") ? apiBaseUrl : apiBaseUrl + "/";
			if(normalized.EndsWith("/v1/")) return normalized + "chat/completions";
			if(normalized.EndsWith("/v1")) return normalized + "/chat/completions";
			return normalized + "v1/chat/completions";
		}
		private static string BuildModelsEndpoint(string apiBaseUrl){
			var normalized = apiBaseUrl.EndsWith("/") ? apiBaseUrl : apiBaseUrl + "/";
			if(normalized.EndsWith("/v1/")) return normalized + "models";
			if(normalized.EndsWith("/v1")) return normalized + "/models";
			return normalized + "v1/models";
		}
		internal sealed class ChatMessage{
			public string Content;
			public string Role;
			public ChatMessage(string role, string content){
				Role = role;
				Content = content;
			}
		}
	}
}
using System;
using LMStud.Properties;
namespace LMStud.Parsers{
	internal interface IAPIResponseFormatParser{
		bool TryParse(JsonNode root, string responseId, out APIClient.ChatCompletionResult result);
	}
	internal static class APIResponseParser{
		private static readonly IAPIResponseFormatParser[] Parsers ={
			new ResponsesApiResponseParser(), new ChatCompletionsResponseParser(), new AnthropicResponseParser(), new OllamaResponseParser(), new GeminiResponseParser()
		};
		internal static APIClient.ChatCompletionResult ParseResponseBody(string jsonText){
			if(string.IsNullOrWhiteSpace(jsonText)) throw new InvalidOperationException(Resources.API_response_did_not_contain_any_message_);
			var root = Json.Parse(jsonText);
			if(root.GetString("status") == "failed" || !root["error"].IsNull)
				throw new InvalidOperationException(root["error"].GetString("message") ?? root["error"].AsString() ?? "The API response failed.");
			var responseId = root.GetString("id");
			foreach(var parser in Parsers){
				if(!parser.TryParse(root, responseId, out var parsed)) continue;
				parsed.ReadUsage(root["usage"]);
				parsed.FinishReason = parsed.FinishReason ?? root.GetString("finish_reason");
				if(root.GetString("status") == "incomplete") parsed.IncompleteReason = root["incomplete_details"].GetString("reason") ?? "max_output_tokens";
				if(parsed.Incomplete) parsed.ToolCalls = null;
				return parsed;
			}
			throw new InvalidOperationException(Resources.API_response_did_not_contain_any_message_);
		}
	}
}

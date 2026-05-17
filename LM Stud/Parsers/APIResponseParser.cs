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
			var responseId = root.GetString("id");
			foreach(var parser in Parsers)
				if(parser.TryParse(root, responseId, out var parsed))
					return parsed;
			throw new InvalidOperationException(Resources.API_response_did_not_contain_any_message_);
		}
	}
}

using System;
using LMStud.Properties;
using Newtonsoft.Json.Linq;
namespace LMStud.Parsers{
	internal interface IAPIResponseFormatParser{
		bool TryParse(JObject root, string responseId, out APIClient.ChatCompletionResult result);
	}
	internal static class APIResponseParser{
		private static readonly IAPIResponseFormatParser[] Parsers ={
			new ResponsesApiResponseParser(), new ChatCompletionsResponseParser(), new AnthropicResponseParser(), new OllamaResponseParser(), new GeminiResponseParser()
		};
		internal static APIClient.ChatCompletionResult ParseResponseBody(string jsonText){
			if(string.IsNullOrWhiteSpace(jsonText)) throw new InvalidOperationException(Resources.API_response_did_not_contain_any_message_);
			var root = JObject.Parse(jsonText);
			var responseId = root.Value<string>("id");
			foreach(var parser in Parsers)
				if(parser.TryParse(root, responseId, out var parsed))
					return parsed;
			throw new InvalidOperationException(Resources.API_response_did_not_contain_any_message_);
		}
	}
}
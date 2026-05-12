using System.Collections.Generic;
using Newtonsoft.Json.Linq;
namespace LMStud.Parsers{
	internal sealed class ChatCompletionsResponseParser : IAPIResponseFormatParser{
		public bool TryParse(JObject root, string responseId, out APIClient.ChatCompletionResult result){
			result = null;
			if(!(root["choices"] is JArray choices) || choices.Count == 0) return false;
			var message = (choices[0] as JObject)?["message"] as JObject;
			if(message == null) return false;
			var messageContent = APIResponseParserCommon.ExtractContentText(message["content"]);
			var reasoning = message.Value<string>("reasoning_content") ?? APIResponseParserCommon.ExtractReasoningText(message["reasoning"]) ?? APIResponseParserCommon.ExtractReasoningTextFromContent(message["content"]);
			var toolCalls = new List<APIClient.ToolCall>();
			var directToolCalls = APIResponseParserCommon.ParseToolCalls(message["tool_calls"]);
			if(directToolCalls != null) toolCalls.AddRange(directToolCalls);
			var contentToolCalls = APIResponseParserCommon.ParseToolCallsFromContent(message["content"]);
			if(contentToolCalls != null) toolCalls.AddRange(contentToolCalls);
			var finalToolCalls = toolCalls.Count > 0 ? toolCalls : null;
			if(!APIResponseParserCommon.HasResultContent(messageContent, reasoning, finalToolCalls)) return false;
			result = new APIClient.ChatCompletionResult(messageContent ?? "", reasoning, finalToolCalls, responseId, null, APIResponseParserCommon.ExtractTotalTokens(root));
			return true;
		}
	}
}

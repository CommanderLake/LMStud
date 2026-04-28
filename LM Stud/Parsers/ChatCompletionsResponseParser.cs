using Newtonsoft.Json.Linq;
namespace LMStud{
	internal sealed class ChatCompletionsResponseParser : IAPIResponseFormatParser{
		public bool TryParse(JObject root, string responseId, out APIClient.ChatCompletionResult result){
			result = null;
			if(!(root["choices"] is JArray choices) || choices.Count == 0) return false;
			var message = (choices[0] as JObject)?["message"] as JObject;
			if(message == null) return false;
			var messageContent = APIResponseParserCommon.ExtractContentText(message["content"]);
			var reasoning = APIResponseParserCommon.ExtractReasoningTextFromContent(message["content"]);
			var toolCalls = APIResponseParserCommon.ParseToolCalls(message["tool_calls"]);
			if(string.IsNullOrWhiteSpace(messageContent) && (toolCalls == null || toolCalls.Count == 0)) return false;
			result = new APIClient.ChatCompletionResult(messageContent, reasoning, toolCalls, responseId, null);
			return true;
		}
	}
}
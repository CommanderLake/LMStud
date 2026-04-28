using Newtonsoft.Json.Linq;
namespace LMStud{
	internal sealed class AnthropicResponseParser : IAPIResponseFormatParser{
		public bool TryParse(JObject root, string responseId, out APIClient.ChatCompletionResult result){
			result = null;
			if(!(root["content"] is JArray contentArray)) return false;
			var content = APIResponseParserCommon.ExtractContentText(contentArray);
			var reasoning = APIResponseParserCommon.ExtractReasoningTextFromContent(contentArray);
			var toolCalls = APIResponseParserCommon.ParseToolCallsFromContent(contentArray);
			if(string.IsNullOrWhiteSpace(content) && (toolCalls == null || toolCalls.Count == 0)) return false;
			result = new APIClient.ChatCompletionResult(content ?? "", reasoning, toolCalls, responseId, null);
			return true;
		}
	}
}
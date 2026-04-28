using Newtonsoft.Json.Linq;
namespace LMStud{
	internal sealed class OllamaResponseParser : IAPIResponseFormatParser{
		public bool TryParse(JObject root, string responseId, out APIClient.ChatCompletionResult result){
			result = null;
			if(!(root["message"] is JObject messageObj)) return false;
			var content = APIResponseParserCommon.ExtractContentText(messageObj["content"]);
			var toolCalls = APIResponseParserCommon.ParseToolCalls(messageObj["tool_calls"]);
			if(string.IsNullOrWhiteSpace(content) && (toolCalls == null || toolCalls.Count == 0)) return false;
			result = new APIClient.ChatCompletionResult(content ?? "", null, toolCalls, responseId, null);
			return true;
		}
	}
}
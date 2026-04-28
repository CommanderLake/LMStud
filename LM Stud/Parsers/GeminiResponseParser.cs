using Newtonsoft.Json.Linq;
namespace LMStud.Parsers{
	internal sealed class GeminiResponseParser : IAPIResponseFormatParser{
		public bool TryParse(JObject root, string responseId, out APIClient.ChatCompletionResult result){
			result = null;
			if(!(root["candidates"] is JArray candidates) || candidates.Count == 0) return false;
			var firstCandidate = candidates[0] as JObject;
			var contentToken = firstCandidate?["content"]?["parts"];
			var content = APIResponseParserCommon.ExtractContentText(contentToken);
			var toolCalls = APIResponseParserCommon.ParseToolCallsFromContent(contentToken);
			if((toolCalls == null || toolCalls.Count == 0) && firstCandidate?["content"] is JObject contentObj && contentObj["tool_calls"] is JArray directToolCalls)
				toolCalls = APIResponseParserCommon.ParseToolCalls(directToolCalls);
			if(string.IsNullOrWhiteSpace(content) && (toolCalls == null || toolCalls.Count == 0)) return false;
			result = new APIClient.ChatCompletionResult(content ?? "", null, toolCalls, responseId, null);
			return true;
		}
	}
}
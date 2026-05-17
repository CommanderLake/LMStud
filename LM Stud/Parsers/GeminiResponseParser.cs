namespace LMStud.Parsers{
	internal sealed class GeminiResponseParser : IAPIResponseFormatParser{
		public bool TryParse(JsonNode root, string responseId, out APIClient.ChatCompletionResult result){
			result = null;
			var candidates = root["candidates"];
			if(!candidates.IsArray || candidates.Count == 0) return false;
			var firstCandidate = candidates[0];
			var contentToken = firstCandidate["content"]["parts"];
			var content = APIResponseParserCommon.ExtractContentText(contentToken);
			var toolCalls = APIResponseParserCommon.ParseToolCallsFromContent(contentToken);
			if((toolCalls == null || toolCalls.Count == 0) && firstCandidate["content"].IsObject && firstCandidate["content"]["tool_calls"].IsArray)
				toolCalls = APIResponseParserCommon.ParseToolCalls(firstCandidate["content"]["tool_calls"]);
			if(!APIResponseParserCommon.HasResultContent(content, null, toolCalls)) return false;
			result = new APIClient.ChatCompletionResult(content ?? "", null, toolCalls, responseId, JsonNode.Missing);
			return true;
		}
	}
}

namespace LMStud.Parsers{
	internal sealed class AnthropicResponseParser : IAPIResponseFormatParser{
		public bool TryParse(JsonNode root, string responseId, out APIClient.ChatCompletionResult result){
			result = null;
			var contentArray = root["content"];
			if(!contentArray.IsArray) return false;
			var content = APIResponseParserCommon.ExtractContentText(contentArray);
			var reasoning = APIResponseParserCommon.ExtractReasoningTextFromContent(contentArray);
			var toolCalls = APIResponseParserCommon.ParseToolCallsFromContent(contentArray);
			if(!APIResponseParserCommon.HasResultContent(content, reasoning, toolCalls)) return false;
			result = new APIClient.ChatCompletionResult(content ?? "", reasoning, toolCalls, responseId, JsonNode.Missing);
			return true;
		}
	}
}

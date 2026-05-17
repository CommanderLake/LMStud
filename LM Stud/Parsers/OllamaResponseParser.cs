namespace LMStud.Parsers{
	internal sealed class OllamaResponseParser : IAPIResponseFormatParser{
		public bool TryParse(JsonNode root, string responseId, out APIClient.ChatCompletionResult result){
			result = null;
			var messageObj = root["message"];
			if(!messageObj.IsObject) return false;
			var content = APIResponseParserCommon.ExtractContentText(messageObj["content"]);
			var toolCalls = APIResponseParserCommon.ParseToolCalls(messageObj["tool_calls"]);
			if(!APIResponseParserCommon.HasResultContent(content, null, toolCalls)) return false;
			result = new APIClient.ChatCompletionResult(content ?? "", null, toolCalls, responseId, JsonNode.Missing);
			return true;
		}
	}
}

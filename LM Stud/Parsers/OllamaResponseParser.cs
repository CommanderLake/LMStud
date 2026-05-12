using Newtonsoft.Json.Linq;
namespace LMStud.Parsers{
	internal sealed class OllamaResponseParser : IAPIResponseFormatParser{
		public bool TryParse(JObject root, string responseId, out APIClient.ChatCompletionResult result){
			result = null;
			if(!(root["message"] is JObject messageObj)) return false;
			var content = APIResponseParserCommon.ExtractContentText(messageObj["content"]);
			var toolCalls = APIResponseParserCommon.ParseToolCalls(messageObj["tool_calls"]);
			if(!APIResponseParserCommon.HasResultContent(content, null, toolCalls)) return false;
			result = new APIClient.ChatCompletionResult(content ?? "", null, toolCalls, responseId, null);
			return true;
		}
	}
}

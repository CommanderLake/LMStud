using Newtonsoft.Json.Linq;
namespace LMStud{
	internal sealed class GeminiResponseParser : IAPIResponseFormatParser{
		public bool TryParse(JObject root, string responseId, out APIClient.ChatCompletionResult result){
			result = null;
			if(!(root["candidates"] is JArray candidates) || candidates.Count == 0) return false;
			var firstCandidate = candidates[0] as JObject;
			var contentToken = firstCandidate?["content"]?["parts"];
			var content = APIResponseParserCommon.ExtractContentText(contentToken);
			if(string.IsNullOrWhiteSpace(content)) return false;
			result = new APIClient.ChatCompletionResult(content, null, null, responseId, null);
			return true;
		}
	}
}
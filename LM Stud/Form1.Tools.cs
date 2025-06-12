using System;
namespace LMStud{
	internal partial class Form1{
		private NativeMethods.ToolHandler _googleSearchHandler;
		private NativeMethods.ToolHandler _getWebpageHandler;
		private NativeMethods.ToolHandler _getWebTagHandler;
		private NativeMethods.ToolHandler _listWebTagsHandler;
		private static IntPtr GoogleSearchHandler(string args){return NativeMethods.GoogleSearch(args);}
		private static IntPtr GetWebpageHandler(string args){return NativeMethods.GetWebpage(args);}
		private static IntPtr GetWebTagHandler(string args){return NativeMethods.GetWebTag(args);}
		private static IntPtr ListWebTagsHandler(string args){return NativeMethods.ListWebTags(args);}
		private void RegisterTools(){
			NativeMethods.ClearTools();
			_googleSearchHandler = GoogleSearchHandler;
			_getWebpageHandler = GetWebpageHandler;
			_getWebTagHandler = GetWebTagHandler;
			_listWebTagsHandler = ListWebTagsHandler;
			if(_googleSearchEnable){
				NativeMethods.AddTool("web_search", "Search Google and return the top results in JSON format.",
					"{\"type\":\"object\",\"properties\":{\"query\":{\"type\":\"string\"}},\"required\":[\"query\"]}", _googleSearchHandler);
			}
			if(_webpageFetchEnable){
				NativeMethods.AddTool("browse_webpage", "Download the webpage at url and preview the text in all <p>, <article> and <section> tags.",
					"{\"type\":\"object\",\"properties\":{\"url\":{\"type\":\"string\"}},\"required\":[\"url\"]}", _getWebpageHandler);
				NativeMethods.AddTool("expand_tag", "Expand a tag preview listed by the browse_webpage tool.",
					"{\"type\":\"object\",\"properties\":{\"url\":{\"type\":\"string\"},\"id\":{\"type\":\"string\"}},\"required\":[\"url\",\"id\"]}", _getWebTagHandler);
				//NativeMethods.AddTool("list_tags", "List the tags of a previously fetched webpage.",
				//	"{\"type\":\"object\",\"properties\":{\"url\":{\"type\":\"string\"}},\"required\":[\"url\"]}", _listWebTagsHandler);
			}
		}
		private void ClearRegisteredTools(){
			NativeMethods.ClearTools();
			NativeMethods.ClearWebCache();
			_googleSearchHandler = null;
			_getWebpageHandler = null;
			_getWebTagHandler = null;
			_listWebTagsHandler = null;
		}
	}
}
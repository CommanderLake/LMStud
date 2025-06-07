using System;
namespace LMStud{
	internal partial class Form1{
		private NativeMethods.ToolHandler _googleHandler;
		private NativeMethods.ToolHandler _fetchPageHandler;
		private NativeMethods.ToolHandler _browseCacheHandler;
		private NativeMethods.ToolHandler _getSectionHandler;
		private static IntPtr GoogleSearchHandler(string args){return NativeMethods.GoogleSearch(args);}
		private static IntPtr FetchPageHandler(string args){return NativeMethods.FetchWebpage(args);}
		private static IntPtr BrowseCacheHandler(string args){return NativeMethods.BrowseWebCache(args);}
		private static IntPtr GetSectionHandler(string args){return NativeMethods.GetWebSection(args);}
		private void RegisterTools(){
			NativeMethods.ClearTools();
			_googleHandler = GoogleSearchHandler;
			_fetchPageHandler = FetchPageHandler;
			_browseCacheHandler = BrowseCacheHandler;
			_getSectionHandler = GetSectionHandler;
			if(_googleSearchEnable)
				NativeMethods.AddTool("google_search", "Search Google and return the top results", "{\"type\":\"object\",\"properties\":{\"query\":{\"type\":\"string\"}},\"required\":[\"query\"]}", _googleHandler);
			if(_webpageFetchEnable){
				NativeMethods.AddTool("fetch_webpage_to_cache", "Fetch a webpage and cache its sections for retrieval using fetch_webpage_section_from_cache", "{\"type\":\"object\",\"properties\":{\"url\":{\"type\":\"string\"}},\"required\":[\"url\"]}", _fetchPageHandler);
				NativeMethods.AddTool("list_webpage_cache", "List sections for a webpage cached after using fetch_webpage", "{\"type\":\"object\",\"properties\":{\"url\":{\"type\":\"string\"}},\"required\":[\"url\"]}", _browseCacheHandler);
				NativeMethods.AddTool("fetch_webpage_section_from_cache", "Get text from a section of a webpage cached after using fetch_webpage",
					"{\"type\":\"object\",\"properties\":{\"url\":{\"type\":\"string\"},\"id\":{\"type\":\"integer\"}},\"required\":[\"url\",\"id\"]}", _getSectionHandler);
			}
		}
		private void ClearRegisteredTools(){
			NativeMethods.ClearTools();
			NativeMethods.ClearWebCache();
			_googleHandler = null;
			_fetchPageHandler = null;
			_browseCacheHandler = null;
			_getSectionHandler = null;
		}
	}
}
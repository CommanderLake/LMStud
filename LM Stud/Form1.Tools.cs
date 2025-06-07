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
                        if(_googleSearchEnable) NativeMethods.AddTool("google_search", "Search Google and return the top results", "{\"type\":\"object\",\"properties\":{\"query\":{\"type\":\"string\"}},\"required\":[\"query\"]}", _googleHandler);
                        NativeMethods.AddTool("fetch_webpage", "Fetch a webpage and cache its sections", "{\"type\":\"object\",\"properties\":{\"url\":{\"type\":\"string\"}},\"required\":[\"url\"]}", _fetchPageHandler);
                        NativeMethods.AddTool("browse_web_cache", "List cached sections for a webpage", "{\"type\":\"object\",\"properties\":{\"url\":{\"type\":\"string\"}},\"required\":[\"url\"]}", _browseCacheHandler);
                        NativeMethods.AddTool("fetch_web_section", "Get text from a cached section", "{\"type\":\"object\",\"properties\":{\"url\":{\"type\":\"string\"},\"id\":{\"type\":\"integer\"}},\"required\":[\"url\",\"id\"]}", _getSectionHandler);
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
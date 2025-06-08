using System;
namespace LMStud{
	internal partial class Form1{
		private NativeMethods.ToolHandler _googleHandler;
		private NativeMethods.ToolHandler _addPageHandler;
		private NativeMethods.ToolHandler _getSectionHandler;
		private NativeMethods.ToolHandler _listSectionsHandler;
		private static IntPtr GoogleSearchHandler(string args){return NativeMethods.GoogleSearch(args);}
		private static IntPtr GetPageHandler(string args){return NativeMethods.GetWebpage(args);}
		private static IntPtr GetSectionHandler(string args){return NativeMethods.GetWebSection(args);}
		private static IntPtr ListSectionsHandler(string args){return NativeMethods.ListSections(args);}
		private void RegisterTools(){
			NativeMethods.ClearTools();
			_googleHandler = GoogleSearchHandler;
			_addPageHandler = GetPageHandler;
			_getSectionHandler = GetSectionHandler;
			_listSectionsHandler = ListSectionsHandler;
			if(_googleSearchEnable){
				NativeMethods.AddTool("web_search", "Search Google and return the top results in JSON format.",
					"{\"type\":\"object\",\"properties\":{\"query\":{\"type\":\"string\"}},\"required\":[\"query\"]}", _googleHandler);
			}
			if(_webpageFetchEnable){
				NativeMethods.AddTool("download_webpage", "Download the webpage at url and store sections of text in a local cache, after using this tool you must call page_get_section with a url and section id.",
					"{\"type\":\"object\",\"properties\":{\"url\":{\"type\":\"string\"}},\"required\":[\"url\"]}", _addPageHandler);
				NativeMethods.AddTool("get_text_from_downloaded_webpage", "Return the full text of one section of a webpage from the local cache.",
					"{\"type\":\"object\",\"properties\":{\"url\":{\"type\":\"string\"},\"id\":{\"type\":\"string\"}},\"required\":[\"url\",\"id\"]}", _getSectionHandler);
				//NativeMethods.AddTool("page_list_sections", "List the section cache contents of a previously fetched webpage.",
				//	"{\"type\":\"object\",\"properties\":{\"url\":{\"type\":\"string\"}},\"required\":[\"url\"]}", _listSectionsHandler);
			}
		}
		private void ClearRegisteredTools(){
			NativeMethods.ClearTools();
			NativeMethods.ClearWebCache();
			_googleHandler = null;
			_addPageHandler = null;
			_getSectionHandler = null;
			_listSectionsHandler = null;
		}
	}
}
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
				NativeMethods.AddTool("web_search", "Search Google and give me the top results so I can immediately feed one to cache_add_page.",
					"{\"type\":\"object\",\"properties\":{\"query\":{\"type\":\"string\"}},\"required\":[\"query\"]}", _googleHandler);
			}
			if(_webpageFetchEnable){
				NativeMethods.AddTool("cache_add_page", "Download url, store all text blocks locally and return a JSON array 'sections' with {id, preview} objects so I can get a section of text using cache_get_section.",
					"{\"type\":\"object\",\"properties\":{\"url\":{\"type\":\"string\"}},\"required\":[\"url\"]}", _addPageHandler);
				NativeMethods.AddTool("cache_get_section", "Give me the full text of one cached section by url and id that I saw in cache_add_page.",
					"{\"type\":\"object\",\"properties\":{\"url\":{\"type\":\"string\"},\"id\":{\"type\":\"string\"}},\"required\":[\"url\",\"id\"]}", _getSectionHandler);
				//NativeMethods.AddTool("cache_list_sections", "If I lost track of the previews from cache_add_page, show them again for this url.",
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
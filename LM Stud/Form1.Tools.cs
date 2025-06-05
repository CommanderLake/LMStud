using System;
namespace LMStud{
	internal partial class Form1{
		private NativeMethods.ToolHandler _googleHandler;
		private static IntPtr GoogleSearchHandler(string args){return NativeMethods.GoogleSearch(args);}
		private void RegisterTools(){
			NativeMethods.ClearTools();
			_googleHandler = GoogleSearchHandler;
			NativeMethods.AddTool("google_search", "Search Google and return the top results", "{\"type\":\"object\",\"properties\":{\"query\":{\"type\":\"string\"}},\"required\":[\"query\"]}", _googleHandler);
		}
		private void ClearRegisteredTools(){
			NativeMethods.ClearTools();
			_googleHandler = null;
		}
	}
}
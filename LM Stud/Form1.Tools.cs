namespace LMStud{
	internal partial class Form1{
		private void RegisterTools(){NativeMethods.RegisterTools(_googleSearchEnable, _webpageFetchEnable);}
		private void ClearRegisteredTools(){
			NativeMethods.ClearTools();
			NativeMethods.ClearWebCache();
		}
	}
}
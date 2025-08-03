namespace LMStud{
	internal partial class Form1{
		private void RegisterTools(){NativeMethods.RegisterTools(_dateTimeEnable, _googleSearchEnable, _webpageFetchEnable, _fileListEnable, _fileCreateEnable, _fileReadEnable, _fileWriteEnable);}
		private static void ClearRegisteredTools(){
			NativeMethods.ClearTools();
			NativeMethods.ClearWebCache();
		}
	}
}
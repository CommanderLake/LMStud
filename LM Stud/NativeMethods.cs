using System;
using System.Runtime.InteropServices;
namespace LMStud{
	internal static class NativeMethods{
		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		public delegate int ProgressCallback(long totalBytes, long downloadedBytes);
		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		public unsafe delegate void TokenCallback(byte* thinkPtr, int thinkLen, byte* messagePtr, int messageLen, int tokenCount, int tokensTotal, double ftTime, int tool);
		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		public delegate IntPtr ToolHandler([MarshalAs(UnmanagedType.LPUTF8Str)] string args);
		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		public delegate void WhisperCallback(string transcription);
		public enum GgmlNumaStrategy{
			Disabled = 0,
			Distribute = 1,
			Isolate = 2,
			Numactl = 3,
			Mirror = 4,
			Count
		}
		private const string DLLName = "stud";
		private const int WmUser = 0x400;
		internal const int EmSetscrollpos = WmUser + 222;
		internal const int WmVscroll = 0x115;
		internal const int SbBottom = 7;
		internal const int EmSetsel = 0xB1;
		internal const int WmSetfocus = 0x0007;
		internal const int WmKillfocus = 0x0008;
		internal const int WmLbuttondown = 0x0201;
		[DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
		public static extern void SetHWnd(IntPtr hWnd);
		[DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
		public static extern void BackendInit();
		[DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
		public static extern int LoadModel(string filename, int nGPULayers, bool mMap, bool mLock, GgmlNumaStrategy numaStrategy);
		[DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
		public static extern void FreeModel();
		[DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ResetChat();
		[DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
		public static extern void SetTokenCallback(TokenCallback cb);
		[DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
		public static extern void SetThreadCount(int n, int nBatch);
		[DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
		public static extern void RetokenizeChat(bool rebuildMemory);
		[DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
		public static extern void SetSystemPrompt([MarshalAs(UnmanagedType.LPUTF8Str)] string prompt, [MarshalAs(UnmanagedType.LPUTF8Str)] string toolsPrompt);
		[DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
		public static extern void SetMessageAt(int index, [MarshalAs(UnmanagedType.LPUTF8Str)] string think, [MarshalAs(UnmanagedType.LPUTF8Str)] string message);
		[DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
		public static extern void RemoveMessageAt(int index);
		[DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
		public static extern void RemoveMessagesStartingAt(int index);
		[DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
		public static extern void GenerateWithTools(MessageRole role, [MarshalAs(UnmanagedType.LPUTF8Str)] string prompt, int nPredict, bool callback);
		[DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
		public static extern void SetGoogle(string apiKey, string searchEngineID, int resultCount);
		[DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
		public static extern void SetFileBaseDir(string dir);
		[DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ClearTools();
		[DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
		public static extern void RegisterTools(bool googleSearch, bool webpageFetch, bool fileList, bool fileCreate, bool fileRead, bool fileWrite);
		[DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
		public static extern void StopGeneration();
		[DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
		public static extern int CreateContext(int nCtx, int nBatch, bool flashAttn, int nThreads, int nThreadsBatch);
		[DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
		public static extern int CreateSampler(float topP, int topK, float temp, float repeatPenalty);
		[DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
		public static extern int CreateSession(int nCtx, int nBatch, bool flashAttn, int nThreads, int nThreadsBatch, float topP, int topK, float temp, float repeatPenalty);
		[DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ClearWebCache();
		[DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
		public static extern unsafe void ConvertMarkdownToRtf([MarshalAs(UnmanagedType.LPUTF8Str)] string markdown, ref byte* rtfOut, ref int rtfLen);
		[DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
		public static extern void SetWhisperCallback(WhisperCallback cb);
		[DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool LoadWhisperModel(string modelPath, int nThreads, bool useGPU);
		[DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
		public static extern void UnloadWhisperModel();
		[DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool StartSpeechTranscription();
		[DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
		public static extern void StopSpeechTranscription();
		[DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
		public static extern void SetWakeCommand(string wakeCmd);
		[DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
		public static extern void SetVADThresholds(float vadThreshold, float freqThreshold);
		[DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
		public static extern void SetVoiceDuration(int voiceDuration);
		[DllImport(DLLName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
		public static extern IntPtr PerformHttpGet(string url);
		[DllImport(DLLName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
		public static extern int DownloadFile(string url, string targetPath);
		[DllImport(DLLName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
		public static extern int DownloadFileWithProgress(string url, string targetPath, ProgressCallback progressCallback);
		[DllImport(DLLName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
		public static extern void FreeMemory(IntPtr ptr);
		[DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
		public static extern void CurlGlobalInit();
		[DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
		public static extern void CurlGlobalCleanup();
		[DllImport("user32.dll")]
		public static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wp, IntPtr lp);
	}
}
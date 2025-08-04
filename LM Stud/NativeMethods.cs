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
		public static extern int LlamaMemSize();
		[DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
		public static extern bool RetokenizeChat(bool rebuildMemory);
		[DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
		public static extern bool SetSystemPrompt([MarshalAs(UnmanagedType.LPUTF8Str)] string prompt, [MarshalAs(UnmanagedType.LPUTF8Str)] string toolsPrompt);
		[DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
		public static extern bool SetMessageAt(int index, [MarshalAs(UnmanagedType.LPUTF8Str)] string think, [MarshalAs(UnmanagedType.LPUTF8Str)] string message);
		[DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
		public static extern bool RemoveMessageAt(int index);
		[DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
		public static extern bool RemoveMessagesStartingAt(int index);
		[DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
		public static extern void GenerateWithTools(MessageRole role, [MarshalAs(UnmanagedType.LPUTF8Str)] string prompt, int nPredict, bool callback);
		[DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
		public static extern void SetGoogle(string apiKey, string searchEngineID, int resultCount);
		[DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
		public static extern void SetFileBaseDir(string dir);
		[DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ClearTools();
		[DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
		public static extern void RegisterTools(bool dateTime, bool googleSearch, bool webpageFetch, bool fileList, bool fileCreate, bool fileRead, bool fileWrite);
		[DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
		public static extern void StopGeneration();
		[DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
		public static extern int CreateContext(int nCtx, int nBatch, bool flashAttn, int nThreads, int nThreadsBatch);
		[DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
		public static extern int CreateSampler(float minP, float topP, int topK, float temp, float repeatPenalty);
		[DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
		public static extern int CreateSession(int nCtx, int nBatch, bool flashAttn, int nThreads, int nThreadsBatch, float minP, float topP, int topK, float temp, float repeatPenalty);
		[DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ClearWebCache();
		[DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
		public static extern unsafe void ConvertMarkdownToRtf([MarshalAs(UnmanagedType.LPUTF8Str)] string markdown, ref byte* rtfOut, ref int rtfLen);
		[DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
		public static extern void SetWhisperCallback(WhisperCallback cb);
		[DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool LoadWhisperModel(string modelPath, int nThreads, bool useGPU, bool useVAD, string vadModel);
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
		public static extern void SetVADThresholds(float vad, float freq);
		[DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
		public static extern void SetWakeWordSimilarity(float similarity);
		[DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
		public static extern void SetVoiceDuration(int voiceDuration);
		[DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
		public static extern void SetWhisperTemp(float temp);
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
		[DllImport("user32.dll")]
		internal static extern bool EnableScrollBar(HandleRef hWnd, int wSBflags, int wArrows);
	}
}
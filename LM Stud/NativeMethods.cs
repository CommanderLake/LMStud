using System;
using System.Runtime.InteropServices;
namespace LMStud{
	internal static class NativeMethods{
		private const string DLLName = "stud";
		public const int WM_USER = 0x400;
		public const int EM_SETSCROLLPOS = WM_USER + 222;
		public const int WM_VSCROLL = 0x115;
		public const int SB_BOTTOM = 7;
		public const int EM_SETSEL = 0xB1;
		public enum GgmlNumaStrategy{
			Disabled = 0,
			Distribute = 1,
			Isolate = 2,
			Numactl = 3,
			Mirror = 4,
			Count
		}
		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		public unsafe delegate void TokenCallback(byte* tokenPtr, int strLen, int tokenCount);
		[DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
		public static extern void BackendInit();
		[DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
		public static extern bool LoadModel(string filename, string systemPrompt, int nCtx, float temp, float repeatPenalty, int topK, float topP, int nThreads, bool strictCPU, int nThreadsBatch,
			bool strictCPUBatch, int nGPULayers, int nBatch, bool mMap, bool mLock, GgmlNumaStrategy numaStrategy, bool flashAttn);
		[DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
		public static extern void FreeModel();
		[DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ResetChat();
		[DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
		public static extern void SetTokenCallback(TokenCallback cb);
		[DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
		public static extern void SetThreadCount(int n, int nBatch);
		[DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
		public static extern int AddMessage(bool user, string text);
		[DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
		public static extern void RetokenizeChat();
		[DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
		public static extern void SetSystemPrompt(string prompt);
		[DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
		public static extern void SetMessageAt(int index, string message);
		[DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
		public static extern void RemoveMessageAt(int index);
		[DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
		public static extern void RemoveMessagesStartingAt(int index);
		[DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
		public static extern void Generate(int nPredict);
		[DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
		public static extern void StopGeneration();
		[DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
		public static extern unsafe void ConvertMarkdownToRtf(string markdown, ref byte* rtfOut, ref int rtfLen);

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		public delegate void WhisperCallback(string transcription);
		[DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
		public static extern void SetWhisperCallback(WhisperCallback cb);
		[DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
		public static extern bool LoadWhisperModel(string modelPath, int nThreads, bool useGPU);
		[DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
		public static extern void UnloadWhisperModel();
		[DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
		public static extern bool StartSpeechTranscription();
		[DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
		public static extern void StopSpeechTranscription();
		[DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
		public static extern void SetWakeCommand(string wakeCmd);
		[DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
		public static extern void SetVADThresholds(float vadThreshold, float freqThreshold);
		[DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
		public static extern void SetVoiceDuration(int voiceDuration);

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		public delegate int ProgressCallback(long totalBytes, long downloadedBytes);
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
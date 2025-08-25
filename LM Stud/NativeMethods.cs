using System;
using System.Runtime.InteropServices;
using System.Text;
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
		public enum StudError{
			Success = 0,
			CantLoadModel = -1,
			ModelNotLoaded = -2,
			CantCreateContext = -3,
			CantCreateSampler = -4,
			CantApplyTemplate = -5,
			ConvTooLong = -6,
			LlamaDecodeError = -7,
			IndexOutOfRange = -8,
			CantTokenizePrompt = -9,
			CantConvertToken = -10,
			ChatParseError = -11,
			GpuOutOfMemory = -12,
			CantLoadWhisperModel = -13,
			CantLoadVADModel = -14,
			CantInitAudioCapture = -15
		};
		private const string DLLName = "stud";
		[DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
		public static extern void SetHWnd(IntPtr hWnd);
		[DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
		public static extern void BackendInit();
		[DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
		public static extern StudError CreateContext(int nCtx, int nBatch, bool flashAttn, int nThreads, int nThreadsBatch);
		[DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
		public static extern StudError CreateSampler(float minP, float topP, int topK, float temp, float repeatPenalty);
		[DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
		public static extern StudError CreateSession(int nCtx, int nBatch, bool flashAttn, int nThreads, int nThreadsBatch, float minP, float topP, int topK, float temp, float repeatPenalty);
		[DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
		public static extern StudError LoadModel(string filename, int nGPULayers, bool mMap, bool mLock, GgmlNumaStrategy numaStrategy);
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
		public static extern int GetStateSize();
		[DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
		public static extern void CopyStateData(IntPtr dst, int size);
		[DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
		public static extern void SetStateData(IntPtr src, int size);
		[DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
		public static extern StudError RetokenizeChat(bool rebuildMemory);
		[DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
		public static extern StudError SetSystemPrompt([MarshalAs(UnmanagedType.LPUTF8Str)] string prompt, [MarshalAs(UnmanagedType.LPUTF8Str)] string toolsPrompt);
		[DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
		public static extern StudError SetMessageAt(int index, [MarshalAs(UnmanagedType.LPUTF8Str)] string think, [MarshalAs(UnmanagedType.LPUTF8Str)] string message);
		[DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
		public static extern StudError RemoveMessageAt(int index);
		[DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
		public static extern StudError RemoveMessagesStartingAt(int index);
		[DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
		public static extern StudError GenerateWithTools(MessageRole role, [MarshalAs(UnmanagedType.LPUTF8Str)] string prompt, int nPredict, bool callback);
		[DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
		public static extern void SetGoogle(string apiKey, string searchEngineID, int resultCount);
		[DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
		public static extern void SetFileBaseDir(string dir);
		[DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ClearTools();
		[DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
		private static extern IntPtr GetLastErrorMessage();
		[DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ClearLastErrorMessage();
		private static string PtrToStringUtf8(IntPtr ptr){
			if(ptr == IntPtr.Zero) return string.Empty;
			var len = 0;
			while(Marshal.ReadByte(ptr, len) != 0) len++;
			var buffer = new byte[len];
			Marshal.Copy(ptr, buffer, 0, len);
			return Encoding.UTF8.GetString(buffer);
		}
		internal static string GetLastError(){
			var ptr = GetLastErrorMessage();
			return PtrToStringUtf8(ptr);
		}
		[DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
		public static extern void RegisterTools(bool dateTime, bool googleSearch, bool webpageFetch, bool fileList, bool fileCreate, bool fileRead, bool fileWrite);
		[DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
		public static extern void StopGeneration();
		[DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ClearWebCache();
		[DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
		public static extern unsafe void ConvertMarkdownToRtf([MarshalAs(UnmanagedType.LPUTF8Str)] string markdown, ref byte* rtfOut, ref int rtfLen);
		[DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
		public static extern void SetWhisperCallback(WhisperCallback cb);
		[DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
		public static extern StudError LoadWhisperModel(string modelPath, int nThreads, bool useGPU, bool useVAD, string vadModel);
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
using System;
using System.Runtime.InteropServices;
using System.Text;
namespace LMStud{
	internal static class NativeMethods{
		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		public delegate int ProgressCallback(long totalBytes, long downloadedBytes);
		public delegate void SpeechEndCallback();
		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		public unsafe delegate void TokenCallback(byte* thinkPtr, int thinkLen, byte* messagePtr, int messageLen, int tokenCount, int tokensTotal, double ftTime, int tool);
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
		public enum QuantType{
			Default = 0,
			TQ1 = 1,
			TQ2 = 2
		}
		public enum StudError{
			Success = 0,
			CantLoadModel = -1,
			ModelNotLoaded = -2,
			CantCreateContext = -3,
			CantCreateSampler = -4,
			CantApplyTemplate = -5,
			ContextFull = -6,
			LlamaDecodeError = -7,
			IndexOutOfRange = -8,
			CantTokenizePrompt = -9,
			CantConvertToken = -10,
			ChatParseError = -11,
			GpuOutOfMemory = -12,
			CantLoadWhisperModel = -13,
			CantLoadVADModel = -14,
			CantInitAudioCapture = -15,
			Generic = -16
		}
		public static string GetLastError(){
			var ptr = GetLastErrorMessage();
			if(ptr == IntPtr.Zero) return string.Empty;
			var length = 0;
			while(Marshal.ReadByte(ptr, length) != 0) length++;
			var buffer = new byte[length];
			Marshal.Copy(ptr, buffer, 0, length);
			return Encoding.UTF8.GetString(buffer);
		}
		private const string DllName = "Stud.dll";
		[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
		internal static extern void SetHWnd(IntPtr hWnd);
		[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
		internal static extern void BackendInit();
		[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
		internal static extern StudError CreateContext([MarshalAs(UnmanagedType.LPUTF8Str)] string slotName, int nCtx, int nBatch, uint flashAttn, int nThreads, int nThreadsBatch, int kType, int vType);
		[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
		internal static extern StudError CreateSampler([MarshalAs(UnmanagedType.LPUTF8Str)] string slotName, float minP, float topP, int topK, float temp, float repeatPenalty);
		[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
		internal static extern StudError CreateSession([MarshalAs(UnmanagedType.LPUTF8Str)] string slotName, int nCtx, int nBatch, uint flashAttn, int nThreads, int nThreadsBatch, float minP, float topP, int topK, float temp, float repeatPenalty, int kType, int vType);
		[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		internal static extern bool IsModelSlotLoaded([MarshalAs(UnmanagedType.LPUTF8Str)] string slotName);
		[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
		internal static extern void FreeModelSlot([MarshalAs(UnmanagedType.LPUTF8Str)] string slotName);
		[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
		internal static extern StudError LoadModel([MarshalAs(UnmanagedType.LPUTF8Str)] string slotName, [MarshalAs(UnmanagedType.LPUTF8Str)] string filename, [MarshalAs(UnmanagedType.LPUTF8Str)] string jinjaTemplate, int nGPULayers, bool mMap, bool mLock, GgmlNumaStrategy numaStrategy);
		[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
		internal static extern void FreeModel([MarshalAs(UnmanagedType.LPUTF8Str)] string slotName);
		[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
		internal static extern StudError ResetChat([MarshalAs(UnmanagedType.LPUTF8Str)] string slotName);
		[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
		internal static extern void SetTokenCallback(TokenCallback cb);
		[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
		internal static extern void SetThreadCount(int n, int nBatch);
		[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
		internal static extern int LlamaMemSize([MarshalAs(UnmanagedType.LPUTF8Str)] string slotName);
		[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
		internal static extern int GetStateSize([MarshalAs(UnmanagedType.LPUTF8Str)] string slotName);
		[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
		internal static extern StudError GetStateData([MarshalAs(UnmanagedType.LPUTF8Str)] string slotName, IntPtr dst, int size);
		[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
		internal static extern StudError SetStateData([MarshalAs(UnmanagedType.LPUTF8Str)] string slotName, IntPtr src, int size);
		[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
		internal static extern StudError DialecticInit([MarshalAs(UnmanagedType.LPUTF8Str)] string slotName);
		[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
		internal static extern StudError DialecticRelayInit([MarshalAs(UnmanagedType.LPUTF8Str)] string slotName);
		[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
		internal static extern StudError DialecticStart([MarshalAs(UnmanagedType.LPUTF8Str)] string slotName);
		[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
		internal static extern StudError DialecticSwap([MarshalAs(UnmanagedType.LPUTF8Str)] string slotName);
		[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
		internal static extern StudError DialecticRelaySwap([MarshalAs(UnmanagedType.LPUTF8Str)] string slotName, [MarshalAs(UnmanagedType.LPUTF8Str)] string fromSlotName, [MarshalAs(UnmanagedType.LPUTF8Str)] string toSlotName);
		[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
		internal static extern void DialecticFree([MarshalAs(UnmanagedType.LPUTF8Str)] string slotName);
		[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
		internal static extern StudError SetSystemPrompt([MarshalAs(UnmanagedType.LPUTF8Str)] string slotName, [MarshalAs(UnmanagedType.LPUTF8Str)] string prompt, [MarshalAs(UnmanagedType.LPUTF8Str)] string toolsPrompt);
		[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
		internal static extern StudError SetMessageAt([MarshalAs(UnmanagedType.LPUTF8Str)] string slotName, int index, [MarshalAs(UnmanagedType.LPUTF8Str)] string think, [MarshalAs(UnmanagedType.LPUTF8Str)] string message);
		[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
		internal static extern StudError RemoveMessageAt([MarshalAs(UnmanagedType.LPUTF8Str)] string slotName, int index);
		[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
		internal static extern StudError RemoveMessagesStartingAt([MarshalAs(UnmanagedType.LPUTF8Str)] string slotName, int index);
		[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
		internal static extern StudError AddMessage([MarshalAs(UnmanagedType.LPUTF8Str)] string slotName, MessageRole role, [MarshalAs(UnmanagedType.LPUTF8Str)] string think, [MarshalAs(UnmanagedType.LPUTF8Str)] string message);
		[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
		internal static extern StudError SyncChatMessages([MarshalAs(UnmanagedType.LPUTF8Str)] string slotName, [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.I4)] int[] roles, [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPStr)] string[] thinks, [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPStr)] string[] messages, int count);
		[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
		internal static extern StudError SyncChatMessagesJson([MarshalAs(UnmanagedType.LPUTF8Str)] string slotName, [MarshalAs(UnmanagedType.LPUTF8Str)] string messagesJson);
		[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
		internal static extern StudError GenerateWithTools([MarshalAs(UnmanagedType.LPUTF8Str)] string slotName, MessageRole role, [MarshalAs(UnmanagedType.LPUTF8Str)] string prompt, int nPredict, bool callback);
		[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
		internal static extern StudError GenerateForAPI([MarshalAs(UnmanagedType.LPUTF8Str)] string slotName, MessageRole role, [MarshalAs(UnmanagedType.LPUTF8Str)] string prompt, [MarshalAs(UnmanagedType.LPUTF8Str)] string toolsJson, int nPredict, out IntPtr responseJson);
		[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
		internal static extern void StopGeneration([MarshalAs(UnmanagedType.LPUTF8Str)] string slotName);
		[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
		internal static extern IntPtr ExecuteTool([MarshalAs(UnmanagedType.LPUTF8Str)] string slotName, [MarshalAs(UnmanagedType.LPUTF8Str)] string name, [MarshalAs(UnmanagedType.LPUTF8Str)] string argsJson);
		[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
		internal static extern IntPtr GetToolsJson([MarshalAs(UnmanagedType.LPUTF8Str)] string slotName, out int length);
		[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
		internal static extern void SetGoogle([MarshalAs(UnmanagedType.LPUTF8Str)] string apiKey, [MarshalAs(UnmanagedType.LPUTF8Str)] string searchEngineID, int resultCount);
		[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
		internal static extern void SetFileBaseDir([MarshalAs(UnmanagedType.LPUTF8Str)] string dir);
		[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
		internal static extern void RegisterTools([MarshalAs(UnmanagedType.LPUTF8Str)] string slotName, bool dateTime, bool googleSearch, bool webpageFetch, bool fileList, bool fileCreate, bool fileRead, bool fileWrite, bool commandPrompt);
		[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
		internal static extern void ClearTools([MarshalAs(UnmanagedType.LPUTF8Str)] string slotName);
		[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
		internal static extern IntPtr GetLastErrorMessage();
		[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
		internal static extern void ClearLastErrorMessage();
		[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
		internal static extern void CloseCommandPrompt();
		[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
		internal static extern void ClearWebCache();
		[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
		internal static extern unsafe void ConvertMarkdownToRtf([MarshalAs(UnmanagedType.LPUTF8Str)] string markdown, ref byte* rtfOut, ref int rtfLen);
		[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
		internal static extern void SetWhisperCallback(WhisperCallback cb);
		[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
		internal static extern void SetSpeechEndCallback(SpeechEndCallback cb);
		[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
		internal static extern StudError LoadWhisperModel([MarshalAs(UnmanagedType.LPUTF8Str)] string modelPath, int nThreads, bool useGPU, bool useVAD, [MarshalAs(UnmanagedType.LPUTF8Str)] string vadModel);
		[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
		internal static extern void UnloadWhisperModel();
		[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		internal static extern bool StartSpeechTranscription();
		[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
		internal static extern void StopSpeechTranscription();
		[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
		internal static extern void SetWakeCommand([MarshalAs(UnmanagedType.LPUTF8Str)] string wakeCmd);
		[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
		internal static extern void SetVADThresholds(float vad, float freq);
		[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
		internal static extern void SetWakeWordSimilarity(float similarity);
		[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
		internal static extern void SetWhisperTemp(float temp);
		[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
		internal static extern void SetSilenceTimeout(int milliseconds);
		[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
		internal static extern void SetCommandPromptTimeout(int milliseconds);
		[DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
		internal static extern void SetCommittedText([MarshalAs(UnmanagedType.LPUTF8Str)] string text);
		[DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
		internal static extern IntPtr PerformHttpGet([MarshalAs(UnmanagedType.LPUTF8Str)] string url);
		[DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
		internal static extern int DownloadFile([MarshalAs(UnmanagedType.LPUTF8Str)] string url, [MarshalAs(UnmanagedType.LPUTF8Str)] string targetPath);
		[DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
		internal static extern int DownloadFileWithProgress([MarshalAs(UnmanagedType.LPUTF8Str)] string url, [MarshalAs(UnmanagedType.LPUTF8Str)] string targetPath, ProgressCallback progressCallback);
		[DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
		internal static extern void FreeMemory(IntPtr ptr);
		[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
		internal static extern void CurlGlobalInit();
		[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
		internal static extern void CurlGlobalCleanup();
		[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
		internal static extern IntPtr CaptureChatState([MarshalAs(UnmanagedType.LPUTF8Str)] string slotName);
		[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
		internal static extern void RestoreChatState([MarshalAs(UnmanagedType.LPUTF8Str)] string slotName, IntPtr state);
		[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
		internal static extern void FreeChatState(IntPtr state);
		[DllImport("user32.dll")]
		internal static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wp, IntPtr lp);
		[DllImport("user32.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		internal static extern bool EnableScrollBar(HandleRef hWnd, int wSBflags, int wArrows);
	}
}

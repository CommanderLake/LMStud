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
		}
		private static INativeMethods _implementation = new DllImportNativeMethods();
		internal static INativeMethods Implementation{get => _implementation; set => _implementation = value ?? throw new ArgumentNullException(nameof(value));}
		public static void SetHWnd(IntPtr hWnd){Implementation.SetHWnd(hWnd);}
		public static void BackendInit(){Implementation.BackendInit();}
		public static StudError CreateContext(int nCtx, int nBatch, uint flashAttn, int nThreads, int nThreadsBatch){
			return Implementation.CreateContext(nCtx, nBatch, flashAttn, nThreads, nThreadsBatch);
		}
		public static StudError CreateSampler(float minP, float topP, int topK, float temp, float repeatPenalty){
			return Implementation.CreateSampler(minP, topP, topK, temp, repeatPenalty);
		}
		public static StudError CreateSession(int nCtx, int nBatch, uint flashAttn, int nThreads, int nThreadsBatch, float minP, float topP, int topK, float temp, float repeatPenalty){
			return Implementation.CreateSession(nCtx, nBatch, flashAttn, nThreads, nThreadsBatch, minP, topP, topK, temp, repeatPenalty);
		}
		public static StudError LoadModel(string filename, string jinjaTemplate, int nGPULayers, bool mMap, bool mLock, GgmlNumaStrategy numaStrategy){
			return Implementation.LoadModel(filename, jinjaTemplate, nGPULayers, mMap, mLock, numaStrategy);
		}
		public static void FreeModel(){Implementation.FreeModel();}
		public static StudError ResetChat(){return Implementation.ResetChat();}
		public static void SetTokenCallback(TokenCallback cb){Implementation.SetTokenCallback(cb);}
		public static void SetThreadCount(int n, int nBatch){Implementation.SetThreadCount(n, nBatch);}
		public static int LlamaMemSize(){return Implementation.LlamaMemSize();}
		public static int GetStateSize(){return Implementation.GetStateSize();}
		public static void GetStateData(IntPtr dst, int size){Implementation.GetStateData(dst, size);}
		public static void SetStateData(IntPtr src, int size){Implementation.SetStateData(src, size);}
		public static StudError SetSystemPrompt(string prompt, string toolsPrompt){return Implementation.SetSystemPrompt(prompt, toolsPrompt);}
		public static StudError SetMessageAt(int index, string think, string message){return Implementation.SetMessageAt(index, think, message);}
		public static void DialecticInit(){Implementation.DialecticInit();}
		public static void DialecticStart(){Implementation.DialecticStart();}
		public static StudError DialecticSwap(){return Implementation.DialecticSwap();}
		public static void DialecticFree(){Implementation.DialecticFree();}
		public static StudError RetokenizeChat(bool rebuildMemory){return Implementation.RetokenizeChat(rebuildMemory);}
		public static StudError RemoveMessageAt(int index){return Implementation.RemoveMessageAt(index);}
		public static StudError RemoveMessagesStartingAt(int index){return Implementation.RemoveMessagesStartingAt(index);}
		public static StudError AddMessage(MessageRole role, string message){return Implementation.AddMessage(role, message);}
		public static StudError GenerateWithTools(MessageRole role, string prompt, int nPredict, bool callback){return Implementation.GenerateWithTools(role, prompt, nPredict, callback);}
		public static void SetGoogle(string apiKey, string searchEngineID, int resultCount){Implementation.SetGoogle(apiKey, searchEngineID, resultCount);}
		public static void SetFileBaseDir(string dir){Implementation.SetFileBaseDir(dir);}
		public static void ClearTools(){Implementation.ClearTools();}
		public static void ClearLastErrorMessage(){Implementation.ClearLastErrorMessage();}
		public static string GetLastError(){return Implementation.GetLastError();}
		public static void RegisterTools(bool dateTime, bool googleSearch, bool webpageFetch, bool fileList, bool fileCreate, bool fileRead, bool fileWrite, bool commandPrompt){
			Implementation.RegisterTools(dateTime, googleSearch, webpageFetch, fileList, fileCreate, fileRead, fileWrite, commandPrompt);
		}
		public static void CloseCommandPrompt(){Implementation.CloseCommandPrompt();}
		public static void StopGeneration(){Implementation.StopGeneration();}
		public static void ClearWebCache(){Implementation.ClearWebCache();}
		public static unsafe void ConvertMarkdownToRtf(string markdown, ref byte* rtfOut, ref int rtfLen){Implementation.ConvertMarkdownToRtf(markdown, ref rtfOut, ref rtfLen);}
		public static void SetWhisperCallback(WhisperCallback cb){Implementation.SetWhisperCallback(cb);}
		public static void SetSpeechEndCallback(SpeechEndCallback cb){Implementation.SetSpeechEndCallback(cb);}
		public static StudError LoadWhisperModel(string modelPath, int nThreads, bool useGPU, bool useVAD, string vadModel){
			return Implementation.LoadWhisperModel(modelPath, nThreads, useGPU, useVAD, vadModel);
		}
		public static void UnloadWhisperModel(){Implementation.UnloadWhisperModel();}
		public static bool StartSpeechTranscription(){return Implementation.StartSpeechTranscription();}
		public static void StopSpeechTranscription(){Implementation.StopSpeechTranscription();}
		public static void SetWakeCommand(string wakeCmd){Implementation.SetWakeCommand(wakeCmd);}
		public static void SetVADThresholds(float vad, float freq){Implementation.SetVADThresholds(vad, freq);}
		public static void SetWakeWordSimilarity(float similarity){Implementation.SetWakeWordSimilarity(similarity);}
		public static void SetWhisperTemp(float temp){Implementation.SetWhisperTemp(temp);}
		public static void SetSilenceTimeout(int milliseconds){Implementation.SetSilenceTimeout(milliseconds);}
		public static void SetCommandPromptTimeout(int milliseconds){Implementation.SetCommandPromptTimeout(milliseconds);}
		public static void SetCommittedText(string text){Implementation.SetCommittedText(text);}
		public static IntPtr PerformHttpGet(string url){return Implementation.PerformHttpGet(url);}
		public static int DownloadFile(string url, string targetPath){return Implementation.DownloadFile(url, targetPath);}
		public static int DownloadFileWithProgress(string url, string targetPath, ProgressCallback progressCallback){return Implementation.DownloadFileWithProgress(url, targetPath, progressCallback);}
		public static void FreeMemory(IntPtr ptr){Implementation.FreeMemory(ptr);}
		public static void CurlGlobalInit(){Implementation.CurlGlobalInit();}
		public static void CurlGlobalCleanup(){Implementation.CurlGlobalCleanup();}
		public static IntPtr CaptureChatState(){return Implementation.CaptureChatState();}
		public static void RestoreChatState(IntPtr state){Implementation.RestoreChatState(state);}
		public static void FreeChatState(IntPtr state){Implementation.FreeChatState(state);}
		public static IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wp, IntPtr lp){return Implementation.SendMessage(hWnd, msg, wp, lp);}
		internal static bool EnableScrollBar(HandleRef hWnd, int wSBflags, int wArrows){return Implementation.EnableScrollBar(hWnd, wSBflags, wArrows);}
		internal interface INativeMethods{
			void SetHWnd(IntPtr hWnd);
			void BackendInit();
			StudError CreateContext(int nCtx, int nBatch, uint flashAttn, int nThreads, int nThreadsBatch);
			StudError CreateSampler(float minP, float topP, int topK, float temp, float repeatPenalty);
			StudError CreateSession(int nCtx, int nBatch, uint flashAttn, int nThreads, int nThreadsBatch, float minP, float topP, int topK, float temp, float repeatPenalty);
			StudError LoadModel(string filename, string jinjaTemplate, int nGPULayers, bool mMap, bool mLock, GgmlNumaStrategy numaStrategy);
			void FreeModel();
			StudError ResetChat();
			void SetTokenCallback(TokenCallback cb);
			void SetThreadCount(int n, int nBatch);
			int LlamaMemSize();
			int GetStateSize();
			void GetStateData(IntPtr dst, int size);
			void SetStateData(IntPtr src, int size);
			StudError SetSystemPrompt(string prompt, string toolsPrompt);
			StudError SetMessageAt(int index, string think, string message);
			void DialecticInit();
			void DialecticStart();
			StudError DialecticSwap();
			void DialecticFree();
			StudError RetokenizeChat(bool rebuildMemory);
			StudError RemoveMessageAt(int index);
			StudError RemoveMessagesStartingAt(int index);
			StudError AddMessage(MessageRole role, string message);
			StudError GenerateWithTools(MessageRole role, string prompt, int nPredict, bool callback);
			void SetGoogle(string apiKey, string searchEngineID, int resultCount);
			void SetFileBaseDir(string dir);
			void ClearTools();
			void ClearLastErrorMessage();
			string GetLastError();
			void RegisterTools(bool dateTime, bool googleSearch, bool webpageFetch, bool fileList, bool fileCreate, bool fileRead, bool fileWrite, bool commandPrompt);
			void CloseCommandPrompt();
			void StopGeneration();
			void ClearWebCache();
			void SetWhisperCallback(WhisperCallback cb);
			void SetSpeechEndCallback(SpeechEndCallback cb);
			StudError LoadWhisperModel(string modelPath, int nThreads, bool useGPU, bool useVAD, string vadModel);
			void UnloadWhisperModel();
			bool StartSpeechTranscription();
			void StopSpeechTranscription();
			void SetWakeCommand(string wakeCmd);
			void SetVADThresholds(float vad, float freq);
			void SetWakeWordSimilarity(float similarity);
			void SetWhisperTemp(float temp);
			void SetSilenceTimeout(int milliseconds);
			void SetCommandPromptTimeout(int milliseconds);
			void SetCommittedText(string text);
			unsafe void ConvertMarkdownToRtf(string markdown, ref byte* rtfOut, ref int rtfLen);
			IntPtr PerformHttpGet(string url);
			int DownloadFile(string url, string targetPath);
			int DownloadFileWithProgress(string url, string targetPath, ProgressCallback progressCallback);
			void FreeMemory(IntPtr ptr);
			void CurlGlobalInit();
			void CurlGlobalCleanup();
			IntPtr CaptureChatState();
			void RestoreChatState(IntPtr state);
			void FreeChatState(IntPtr state);
			IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wp, IntPtr lp);
			bool EnableScrollBar(HandleRef hWnd, int wSBflags, int wArrows);
		}
		private sealed class DllImportNativeMethods : INativeMethods{
			public void SetHWnd(IntPtr hWnd){NativeExports.SetHWnd(hWnd);}
			public void BackendInit(){NativeExports.BackendInit();}
			public StudError CreateContext(int nCtx, int nBatch, uint flashAttn, int nThreads, int nThreadsBatch){return NativeExports.CreateContext(nCtx, nBatch, flashAttn, nThreads, nThreadsBatch);}
			public StudError CreateSampler(float minP, float topP, int topK, float temp, float repeatPenalty){return NativeExports.CreateSampler(minP, topP, topK, temp, repeatPenalty);}
			public StudError CreateSession(int nCtx, int nBatch, uint flashAttn, int nThreads, int nThreadsBatch, float minP, float topP, int topK, float temp, float repeatPenalty){
				return NativeExports.CreateSession(nCtx, nBatch, flashAttn, nThreads, nThreadsBatch, minP, topP, topK, temp, repeatPenalty);
			}
			public StudError LoadModel(string filename, string jinjaTemplate, int nGPULayers, bool mMap, bool mLock, GgmlNumaStrategy numaStrategy){
				return NativeExports.LoadModel(filename, jinjaTemplate, nGPULayers, mMap, mLock, numaStrategy);
			}
			public void FreeModel(){NativeExports.FreeModel();}
			public StudError ResetChat(){return NativeExports.ResetChat();}
			public void SetTokenCallback(TokenCallback cb){NativeExports.SetTokenCallback(cb);}
			public void SetThreadCount(int n, int nBatch){NativeExports.SetThreadCount(n, nBatch);}
			public int LlamaMemSize(){return NativeExports.LlamaMemSize();}
			public int GetStateSize(){return NativeExports.GetStateSize();}
			public void GetStateData(IntPtr dst, int size){NativeExports.GetStateData(dst, size);}
			public void SetStateData(IntPtr src, int size){NativeExports.SetStateData(src, size);}
			public StudError SetSystemPrompt(string prompt, string toolsPrompt){return NativeExports.SetSystemPrompt(prompt, toolsPrompt);}
			public StudError SetMessageAt(int index, string think, string message){return NativeExports.SetMessageAt(index, think, message);}
			public void DialecticInit(){NativeExports.DialecticInit();}
			public void DialecticStart(){NativeExports.DialecticStart();}
			public StudError DialecticSwap(){return NativeExports.DialecticSwap();}
			public void DialecticFree(){NativeExports.DialecticFree();}
			public StudError RetokenizeChat(bool rebuildMemory){return NativeExports.RetokenizeChat(rebuildMemory);}
			public StudError RemoveMessageAt(int index){return NativeExports.RemoveMessageAt(index);}
			public StudError RemoveMessagesStartingAt(int index){return NativeExports.RemoveMessagesStartingAt(index);}
			public StudError AddMessage(MessageRole role, string message){return NativeExports.AddMessage(role, message);}
			public StudError GenerateWithTools(MessageRole role, string prompt, int nPredict, bool callback){return NativeExports.GenerateWithTools(role, prompt, nPredict, callback);}
			public void SetGoogle(string apiKey, string searchEngineID, int resultCount){NativeExports.SetGoogle(apiKey, searchEngineID, resultCount);}
			public void SetFileBaseDir(string dir){NativeExports.SetFileBaseDir(dir);}
			public void ClearTools(){NativeExports.ClearTools();}
			public void ClearLastErrorMessage(){NativeExports.ClearLastErrorMessage();}
			public string GetLastError(){
				var ptr = NativeExports.GetLastErrorMessage();
				if(ptr == IntPtr.Zero) return string.Empty;
				var length = 0;
				while(Marshal.ReadByte(ptr, length) != 0) length++;
				var buffer = new byte[length];
				Marshal.Copy(ptr, buffer, 0, length);
				return Encoding.UTF8.GetString(buffer);
			}
			public void RegisterTools(bool dateTime, bool googleSearch, bool webpageFetch, bool fileList, bool fileCreate, bool fileRead, bool fileWrite, bool commandPrompt){
				NativeExports.RegisterTools(dateTime, googleSearch, webpageFetch, fileList, fileCreate, fileRead, fileWrite, commandPrompt);
			}
			public void CloseCommandPrompt(){NativeExports.CloseCommandPrompt();}
			public void StopGeneration(){NativeExports.StopGeneration();}
			public void ClearWebCache(){NativeExports.ClearWebCache();}
			public unsafe void ConvertMarkdownToRtf(string markdown, ref byte* rtfOut, ref int rtfLen){NativeExports.ConvertMarkdownToRtf(markdown, ref rtfOut, ref rtfLen);}
			public void SetWhisperCallback(WhisperCallback cb){NativeExports.SetWhisperCallback(cb);}
			public void SetSpeechEndCallback(SpeechEndCallback cb){NativeExports.SetSpeechEndCallback(cb);}
			public StudError LoadWhisperModel(string modelPath, int nThreads, bool useGPU, bool useVAD, string vadModel){
				return NativeExports.LoadWhisperModel(modelPath, nThreads, useGPU, useVAD, vadModel);
			}
			public void UnloadWhisperModel(){NativeExports.UnloadWhisperModel();}
			public bool StartSpeechTranscription(){return NativeExports.StartSpeechTranscription();}
			public void StopSpeechTranscription(){NativeExports.StopSpeechTranscription();}
			public void SetWakeCommand(string wakeCmd){NativeExports.SetWakeCommand(wakeCmd);}
			public void SetVADThresholds(float vad, float freq){NativeExports.SetVADThresholds(vad, freq);}
			public void SetWakeWordSimilarity(float similarity){NativeExports.SetWakeWordSimilarity(similarity);}
			public void SetWhisperTemp(float temp){NativeExports.SetWhisperTemp(temp);}
			public void SetSilenceTimeout(int milliseconds){NativeExports.SetSilenceTimeout(milliseconds);}
			public void SetCommandPromptTimeout(int milliseconds){NativeExports.SetCommandPromptTimeout(milliseconds);}
			public void SetCommittedText(string text){NativeExports.SetCommittedText(text);}
			public IntPtr PerformHttpGet(string url){return NativeExports.PerformHttpGet(url);}
			public int DownloadFile(string url, string targetPath){return NativeExports.DownloadFile(url, targetPath);}
			public int DownloadFileWithProgress(string url, string targetPath, ProgressCallback progressCallback){return NativeExports.DownloadFileWithProgress(url, targetPath, progressCallback);}
			public void FreeMemory(IntPtr ptr){NativeExports.FreeMemory(ptr);}
			public void CurlGlobalInit(){NativeExports.CurlGlobalInit();}
			public void CurlGlobalCleanup(){NativeExports.CurlGlobalCleanup();}
			public IntPtr CaptureChatState(){return NativeExports.CaptureChatState();}
			public void RestoreChatState(IntPtr state){NativeExports.RestoreChatState(state);}
			public void FreeChatState(IntPtr state){NativeExports.FreeChatState(state);}
			public IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wp, IntPtr lp){return NativeExports.SendMessage(hWnd, msg, wp, lp);}
			public bool EnableScrollBar(HandleRef hWnd, int wSBflags, int wArrows){return NativeExports.EnableScrollBar(hWnd, wSBflags, wArrows);}
		}
		private static class NativeExports{
			private const string DllName = "stud";
			[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
			internal static extern void SetHWnd(IntPtr hWnd);
			[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
			internal static extern void BackendInit();
			[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
			internal static extern StudError CreateContext(int nCtx, int nBatch, uint flashAttn, int nThreads, int nThreadsBatch);
			[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
			internal static extern StudError CreateSampler(float minP, float topP, int topK, float temp, float repeatPenalty);
			[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
			internal static extern StudError CreateSession(int nCtx, int nBatch, uint flashAttn, int nThreads, int nThreadsBatch, float minP, float topP, int topK, float temp, float repeatPenalty);
			[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
			internal static extern StudError LoadModel(string filename, string jinjaTemplate, int nGPULayers, bool mMap, bool mLock, GgmlNumaStrategy numaStrategy);
			[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
			internal static extern void FreeModel();
			[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
			internal static extern StudError ResetChat();
			[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
			internal static extern void SetTokenCallback(TokenCallback cb);
			[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
			internal static extern void SetThreadCount(int n, int nBatch);
			[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
			internal static extern int LlamaMemSize();
			[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
			internal static extern int GetStateSize();
			[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
			internal static extern void GetStateData(IntPtr dst, int size);
			[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
			internal static extern void SetStateData(IntPtr src, int size);
			[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
			internal static extern void DialecticInit();
			[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
			internal static extern void DialecticStart();
			[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
			internal static extern StudError DialecticSwap();
			[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
			internal static extern void DialecticFree();
			[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
			internal static extern StudError RetokenizeChat(bool rebuildMemory);
			[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
			internal static extern StudError SetSystemPrompt([MarshalAs(UnmanagedType.LPUTF8Str)] string prompt, [MarshalAs(UnmanagedType.LPUTF8Str)] string toolsPrompt);
			[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
			internal static extern StudError SetMessageAt(int index, [MarshalAs(UnmanagedType.LPUTF8Str)] string think, [MarshalAs(UnmanagedType.LPUTF8Str)] string message);
			[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
			internal static extern StudError RemoveMessageAt(int index);
			[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
			internal static extern StudError RemoveMessagesStartingAt(int index);
			[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
			internal static extern StudError AddMessage(MessageRole role, string message);
			[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
			internal static extern StudError GenerateWithTools(MessageRole role, [MarshalAs(UnmanagedType.LPUTF8Str)] string prompt, int nPredict, bool callback);
			[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
			internal static extern void SetGoogle(string apiKey, string searchEngineID, int resultCount);
			[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
			internal static extern void SetFileBaseDir(string dir);
			[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
			internal static extern void ClearTools();
			[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
			internal static extern IntPtr GetLastErrorMessage();
			[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
			internal static extern void ClearLastErrorMessage();
			[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
			internal static extern void RegisterTools(bool dateTime, bool googleSearch, bool webpageFetch, bool fileList, bool fileCreate, bool fileRead, bool fileWrite, bool commandPrompt);
			[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
			internal static extern void CloseCommandPrompt();
			[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
			internal static extern void StopGeneration();
			[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
			internal static extern void ClearWebCache();
			[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
			internal static extern unsafe void ConvertMarkdownToRtf([MarshalAs(UnmanagedType.LPUTF8Str)] string markdown, ref byte* rtfOut, ref int rtfLen);
			[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
			internal static extern void SetWhisperCallback(WhisperCallback cb);
			[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
			internal static extern void SetSpeechEndCallback(SpeechEndCallback cb);
			[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
			internal static extern StudError LoadWhisperModel(string modelPath, int nThreads, bool useGPU, bool useVAD, string vadModel);
			[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
			internal static extern void UnloadWhisperModel();
			[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
			[return: MarshalAs(UnmanagedType.I1)]
			internal static extern bool StartSpeechTranscription();
			[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
			internal static extern void StopSpeechTranscription();
			[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
			internal static extern void SetWakeCommand(string wakeCmd);
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
			internal static extern void SetCommittedText(string text);
			[DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
			internal static extern IntPtr PerformHttpGet(string url);
			[DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
			internal static extern int DownloadFile(string url, string targetPath);
			[DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
			internal static extern int DownloadFileWithProgress(string url, string targetPath, ProgressCallback progressCallback);
			[DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
			internal static extern void FreeMemory(IntPtr ptr);
			[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
			internal static extern void CurlGlobalInit();
			[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
			internal static extern void CurlGlobalCleanup();
			[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
			internal static extern IntPtr CaptureChatState();
			[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
			internal static extern void RestoreChatState(IntPtr state);
			[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
			internal static extern void FreeChatState(IntPtr state);
			[DllImport("user32.dll")]
			internal static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wp, IntPtr lp);
			[DllImport("user32.dll")]
			[return: MarshalAs(UnmanagedType.Bool)]
			internal static extern bool EnableScrollBar(HandleRef hWnd, int wSBflags, int wArrows);
		}
	}
}
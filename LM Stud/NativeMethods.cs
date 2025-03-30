using System;
using System.Runtime.InteropServices;
namespace LMStud{
	internal static class NativeMethods{
		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		public unsafe delegate void TokenCallback(byte* tokenPtr, int strLen, int tokenCount);
		private const string DLLName = "stud";
		public const int WM_USER = 0x400;
		public const int EM_SETSCROLLPOS = WM_USER + 222;
		public const int WM_VSCROLL = 0x115;
		public const int SB_BOTTOM = 7;
		public const int EM_SETSEL = 0xB1;
		[DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
		public static extern void SetOMPEnv();
		[DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
		public static extern bool LoadModel(string filename, string system, int nCtx, float temp, float repeatPenalty, int topK, float topP, int nThreads, bool strictCPU, int nGPULayers,
			int batchSize, GgmlNumaStrategy numaStrategy);
		[DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
		public static extern void FreeModel();
		[DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ResetChat();
		[DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
		public static extern void SetTokenCallback(TokenCallback cb);
		[DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
		public static extern void SetThreadCount(int n);
		[DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
		public static extern int AddMessage(bool user, string text);
		[DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
		public static extern void RetokenizeChat();
		[DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
		public static extern void SetSystemPrompt(string prompt);
		[DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
		public static extern void RemoveMessageAt(int index);
		[DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
		public static extern void RemoveMessagesStartingAt(int index);
		[DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
		public static extern void Generate(int nPredict);
		[DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
		public static extern void StopGeneration();
		[DllImport(DLLName, CallingConvention = CallingConvention.Cdecl)]
		public unsafe static extern void ConvertMarkdownToRtf(string markdown, ref byte* rtfOut, ref int rtfLen);
		[DllImport("user32.dll")]
		public static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wp, IntPtr lp);
		public enum GgmlNumaStrategy{
			GgmlNumaStrategyDisabled = 0,
			GgmlNumaStrategyDistribute = 1,
			GgmlNumaStrategyIsolate = 2,
			GgmlNumaStrategyNumactl = 3,
			GgmlNumaStrategyMirror = 4,
			GgmlNumaStrategyCount
		}
	}
}
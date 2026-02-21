using System;
using System.Speech.Synthesis;
using System.Text;
using System.Threading;
using System.Windows.Forms;
namespace LMStud {
	internal static class TTS{
		internal static Form1 MainForm;
		private static readonly SpeechSynthesizer SS = new SpeechSynthesizer();
		internal static readonly StringBuilder Pending = new StringBuilder();
		internal static volatile bool TTSSpeaking;
		internal static int TTSPendingCount;
		internal static void SetHandlers(){
			SS.SpeakStarted += TtsOnSpeakStarted;
			SS.SpeakCompleted += TtsOnSpeakCompleted;
		}
		internal static void CancelPendingSpeech(){
			SS.SpeakAsyncCancelAll();
			Interlocked.Exchange(ref TTSPendingCount, 0);
			TTSSpeaking = false;
			STT.RequestStart(false);
		}
		internal static void QueueSpeech(string text){
			if(string.IsNullOrWhiteSpace(text)) return;
			Interlocked.Increment(ref TTSPendingCount);
			SS.SpeakAsync(text);
		}
		private static void TtsOnSpeakStarted(object sender, SpeakStartedEventArgs e){
			if(MainForm.IsDisposed) return;
			MainForm.BeginInvoke((MethodInvoker)(() => {
				if(MainForm.IsDisposed) return;
				TTSSpeaking = true;
				if(MainForm.checkVoiceInput.CheckState == CheckState.Checked) NativeMethods.StopSpeechTranscription();
			}));
		}
		private static void TtsOnSpeakCompleted(object sender, SpeakCompletedEventArgs e){
			var pending = Interlocked.Decrement(ref TTSPendingCount);
			if(pending < 0){
				Interlocked.Exchange(ref TTSPendingCount, 0);
				pending = 0;
			}
			if(MainForm.IsDisposed) return;
			MainForm.BeginInvoke((MethodInvoker)(() => {
				if(MainForm.IsDisposed) return;
				if(pending > 0) return;
				TTSSpeaking = false;
				STT.RequestStart(false);
			}));
		}
	}
}

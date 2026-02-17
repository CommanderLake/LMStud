using System;
using System.Speech.Synthesis;
using System.Text;
using System.Threading;
using System.Windows.Forms;
namespace LMStud {
	internal class TTS : IDisposable{
		private readonly Form1 _form;
		private readonly SpeechSynthesizer _ss;
		internal readonly StringBuilder Pending = new StringBuilder();
		internal volatile bool TTSSpeaking;
		internal int TTSPendingCount;
		internal TTS(Form1 form){
			_form = form;
			_ss = new SpeechSynthesizer();
			_ss.SpeakStarted += TtsOnSpeakStarted;
			_ss.SpeakCompleted += TtsOnSpeakCompleted;
		}
		public void Dispose(){
			_ss?.Dispose();
		}
		internal void CancelPendingSpeech(){
			_ss.SpeakAsyncCancelAll();
			Interlocked.Exchange(ref TTSPendingCount, 0);
			TTSSpeaking = false;
			STT.TryStartSpeechTranscription(false);
		}
		internal void QueueSpeech(string text){
			if(string.IsNullOrWhiteSpace(text)) return;
			Interlocked.Increment(ref TTSPendingCount);
			_ss.SpeakAsync(text);
		}
		private void TtsOnSpeakStarted(object sender, SpeakStartedEventArgs e){
			if(_form.IsDisposed) return;
			_form.BeginInvoke((MethodInvoker)(() => {
				if(_form.IsDisposed) return;
				TTSSpeaking = true;
				if(_form.checkVoiceInput.CheckState == CheckState.Checked) NativeMethods.StopSpeechTranscription();
			}));
		}
		private void TtsOnSpeakCompleted(object sender, SpeakCompletedEventArgs e){
			var pending = Interlocked.Decrement(ref TTSPendingCount);
			if(pending < 0){
				Interlocked.Exchange(ref TTSPendingCount, 0);
				pending = 0;
			}
			if(_form.IsDisposed) return;
			_form.BeginInvoke((MethodInvoker)(() => {
				if(_form.IsDisposed) return;
				if(pending > 0) return;
				TTSSpeaking = false;
				STT.TryStartSpeechTranscription(false);
			}));
		}
	}
}

using System.Threading;
using System.Windows.Forms;
using LMStud.Properties;
namespace LMStud {
	internal static class STT {
		internal static Form1 MainForm;
		private static int _restartPending;
		internal static void RequestStart(bool showErrorOnFailure){
			Interlocked.Exchange(ref _restartPending, 1);
			RetryStart(showErrorOnFailure);
		}
		internal static void RetryStart(bool showErrorOnFailure = false){
			if(Volatile.Read(ref _restartPending) == 0) return;
			if(MainForm.checkVoiceInput.CheckState != CheckState.Checked){
				Interlocked.Exchange(ref _restartPending, 0);
				return;
			}
			if(MainForm.IsEditing || Generation.Generating || Generation.APIServerGenerating || TTS.TTSSpeaking || Volatile.Read(ref TTS.TTSPendingCount) > 0) return;
			if(NativeMethods.StartSpeechTranscription()){
				Interlocked.Exchange(ref _restartPending, 0);
				return;
			}
			if(!showErrorOnFailure) return;
			MessageBox.Show(MainForm, Resources.Error_starting_voice_input, Resources.LM_Stud, MessageBoxButtons.OK, MessageBoxIcon.Error);
			MainForm.checkVoiceInput.Checked = false;
			MainForm.CheckVoiceInputLast = MainForm.checkVoiceInput.CheckState;
			Interlocked.Exchange(ref _restartPending, 0);
		}
		internal static void SpeechEndCallback(){
			if(MainForm.IsDisposed) return;
			MainForm.BeginInvoke((MethodInvoker)(() => {
				if(MainForm.IsDisposed || MainForm.IsEditing) return;
				if(Generation.Generating || Generation.APIServerGenerating) return;
				if(MainForm.checkVoiceInput.CheckState != CheckState.Checked) return;
				var prompt = MainForm.textInput.Text;
				if(string.IsNullOrWhiteSpace(prompt)) return;
				if(!Common.APIClientEnable && !Common.LlModelLoaded) return;
				NativeMethods.StopSpeechTranscription();
				Generation.Generate();
			}));
		}
		internal static void WhisperCallback(string transcription){
			if(MainForm.IsDisposed) return;
			MainForm.BeginInvoke((MethodInvoker)(() => {
				if(MainForm.IsDisposed || MainForm.IsEditing) return;
				MainForm.textInput.Text = transcription;
				MainForm.textInput.SelectionStart = MainForm.textInput.Text.Length;
			}));
		}
	}
}

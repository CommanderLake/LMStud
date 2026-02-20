using System.Threading;
using System.Windows.Forms;
using LMStud.Properties;
namespace LMStud {
	internal static class STT {
		internal static Form1 MainForm;
		internal static void TryStartSpeechTranscription(bool showErrorOnFailure){
			if(MainForm.checkVoiceInput.CheckState != CheckState.Checked) return;
			if(MainForm.IsEditing || Generation.Generating || Generation.APIServerGenerating || TTS.TTSSpeaking || Volatile.Read(ref TTS.TTSPendingCount) > 0) return;
			if(NativeMethods.StartSpeechTranscription()) return;
			if(!showErrorOnFailure) return;
			MessageBox.Show(MainForm, Resources.Error_starting_voice_input, Resources.LM_Stud, MessageBoxButtons.OK, MessageBoxIcon.Error);
			MainForm.checkVoiceInput.Checked = false;
			MainForm.CheckVoiceInputLast = MainForm.checkVoiceInput.CheckState;
		}
		internal static void SpeechEndCallback(){
			if(MainForm.IsDisposed) return;
			MainForm.BeginInvoke((MethodInvoker)(() => {
				if(MainForm.IsDisposed || MainForm.IsEditing) return;
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

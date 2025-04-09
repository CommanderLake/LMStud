using System;
using System.IO;
using System.Windows.Forms;
using LMStud.Properties;
namespace LMStud{
	internal partial class Form1{
		private string _instruction = "";
		private string _modelsPath = "";
		private int _ctxSize = 4096;
		private int _gpuLayers = 32;
		private float _temp = 0.7f;
		private int _nGen = -1;
		private NativeMethods.GgmlNumaStrategy _numaStrat = 0;
		private float _repPen = 1.1f;
		private int _topK = 40;
		private float _topP = 0.95f;
		private int _batchSize = 512;
		private bool _mMap = true;
		private bool _mLock;
		private int _nThreads = 8;
		private bool _strictCPU;
		private int _nThreadsBatch = 8;
		private bool _strictCPUBatch;
		private int _whisperModel;
		private string _wakeWord;
		private bool _whisperUseGPU;
		private void LoadConfig(){
			textInstruction.Text = Settings.Default.Instruction;
			textModelsPath.Text = Settings.Default.ModelsDir;
			numCtxSize.Value = Settings.Default.CtxSize;
			numGPULayers.Value = Settings.Default.GPULayers;
			numTemp.Value = Settings.Default.Temp;
			numNGen.Value = Settings.Default.NGen;
			comboNUMAStrat.SelectedIndex = Settings.Default.NUMAStrat;
			numRepPen.Value = Settings.Default.RepPen;
			numTopK.Value = Settings.Default.TopK;
			numTopP.Value = Settings.Default.TopP;
			numBatchSize.Value = Settings.Default.BatchSize;
			checkMMap.Checked = Settings.Default.MMap;
			checkMLock.Checked = Settings.Default.MLock;
			numThreads.Value = Settings.Default.Threads;
			checkStrictCPU.Checked = Settings.Default.StrictCPU;
			numThreadsBatch.Value = Settings.Default.ThreadsBatch;
			checkStrictCPUBatch.Checked = Settings.Default.StrictCPUBatch;
			textWakeWord.Text = Settings.Default.WakeWord;
			checkWhisperUseGPU.Checked = Settings.Default.whisperUseGPU;
		}
		private void SetConfig(){
			_instruction = textInstruction.Text;
			_modelsPath = textModelsPath.Text;
			_ctxSize = (int)numCtxSize.Value;
			_gpuLayers = (int)numGPULayers.Value;
			_temp = (float)numTemp.Value;
			_nGen = (int)numNGen.Value;
			_numaStrat = (NativeMethods.GgmlNumaStrategy)comboNUMAStrat.SelectedIndex;
			_repPen = (float)numRepPen.Value;
			_topK = (int)numTopK.Value;
			_topP = (float)numTopP.Value;
			_batchSize = (int)numBatchSize.Value;
			_mMap = checkMMap.Checked;
			_mLock = checkMLock.Checked;
			_nThreads = (int)numThreads.Value;
			_strictCPU = checkStrictCPU.Checked;
			_nThreadsBatch = (int)numThreadsBatch.Value;
			_strictCPUBatch = checkStrictCPUBatch.Checked;
			_wakeWord = textWakeWord.Text;
			_whisperUseGPU = checkWhisperUseGPU.Checked;
			NativeMethods.SetSystemPrompt(textInstruction.Text);
			NativeMethods.SetThreadCount((int)numThreads.Value, (int)numThreadsBatch.Value);
			NativeMethods.SetWakeCommand(_wakeWord);
		}
		private void ButApply_Click(object sender, EventArgs e){
			if(Settings.Default.ModelsDir != textModelsPath.Text) PopulateModels();
			Settings.Default.Instruction = textInstruction.Text;
			Settings.Default.ModelsDir = textModelsPath.Text;
			Settings.Default.CtxSize = numCtxSize.Value;
			Settings.Default.GPULayers = numGPULayers.Value;
			Settings.Default.Temp = numTemp.Value;
			Settings.Default.NGen = numNGen.Value;
			Settings.Default.NUMAStrat = comboNUMAStrat.SelectedIndex;
			Settings.Default.RepPen = numRepPen.Value;
			Settings.Default.TopK = numTopK.Value;
			Settings.Default.TopP = numTopP.Value;
			Settings.Default.BatchSize = numBatchSize.Value;
			Settings.Default.MMap = checkMMap.Checked;
			Settings.Default.MLock = checkMLock.Checked;
			Settings.Default.Threads = numThreads.Value;
			Settings.Default.StrictCPU = checkStrictCPU.Checked;
			Settings.Default.ThreadsBatch = numThreadsBatch.Value;
			Settings.Default.StrictCPUBatch = checkStrictCPUBatch.Checked;
			Settings.Default.WhisperModel = comboWhisperModel.Text;
			Settings.Default.WakeWord = textWakeWord.Text;
			Settings.Default.whisperUseGPU = checkWhisperUseGPU.Checked;
			Settings.Default.Save();
			SetConfig();
		}
		private void ButBrowse_Click(object sender, EventArgs e){
			if(folderBrowserDialog1.ShowDialog(this) == DialogResult.OK){
				textModelsPath.Text = folderBrowserDialog1.SelectedPath;
			}
		}
		private void TextModelsPath_KeyDown(object sender, KeyEventArgs e){
			if(e.KeyCode != Keys.Enter) return;
			MessageBox.Show(this, "Folder not found", "LM Stud Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
		}
		private void ComboWhisperModel_DropDown(object sender, EventArgs e){
			if(!Directory.Exists(_modelsPath)) return;
			PopulateWhisperModels();
		}
		private void ButWhispDown_Click(object sender, EventArgs e) {
			HugLoadFiles("ggerganov", "whisper.cpp", ".bin");
			tabControl1.SelectTab(3);
		}
		private void PopulateWhisperModels(){
			UseWaitCursor = true;
			try{
				_whisperModels.Clear();
				comboWhisperModel.Items.Clear();
				var files = Directory.GetFiles(_modelsPath, "*.bin", SearchOption.AllDirectories);
				foreach(var file in files){
					using(var fs = new FileStream(file, FileMode.Open, FileAccess.Read))
					using(var br = new BinaryReader(fs)){
						var magic = br.ReadUInt32();
						if(magic != 0x67676D6C)// "lmgg" in little-endian
							return;
					}
					_whisperModels.Add(file);
					comboWhisperModel.Items.Add(Path.GetFileName(file));
				}
				comboWhisperModel.SelectedIndex = comboWhisperModel.Items.IndexOf(Settings.Default.WhisperModel);
				_whisperModel = comboWhisperModel.SelectedIndex;
			} finally{ UseWaitCursor = false; }
		}
	}
}
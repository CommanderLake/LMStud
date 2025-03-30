using System;
using System.IO;
using System.Windows.Forms;
using LMStud.Properties;
namespace LMStud{
	internal partial class Form1{
		private string _instruction = "";
		private string _modelsPath = "";
		private int _nThreads = 8;
		private int _ctxSize = 4096;
		private int _gpuLayers = 32;
		private float _temp = 0.7f;
		private int _nGen = -1;
		private NativeMethods.GgmlNumaStrategy _numaStrat = 0;
		private float _repPen = 1.1f;
		private int _topK = 40;
		private float _topP = 0.95f;
		private int _batchSize = 512;
		private bool _strictCPU;
		private void LoadConfig(){
			textInstruction.Text = Settings.Default.Instruction;
			textModelsPath.Text = Settings.Default.ModelsDir;
			numThreads.Value = Settings.Default.Threads;
			numCtxSize.Value = Settings.Default.CtxSize;
			numGPULayers.Value = Settings.Default.GPULayers;
			numTemp.Value = Settings.Default.Temp;
			numNGen.Value = Settings.Default.NGen;
			comboNUMAStrat.SelectedIndex = Settings.Default.NUMAStrat;
			numRepPen.Value = Settings.Default.RepPen;
			numTopK.Value = Settings.Default.TopK;
			numTopP.Value = Settings.Default.TopP;
			numBatchSize.Value = Settings.Default.BatchSize;
			checkStrictCPU.Checked = Settings.Default.strictCPU;
		}
		private void SetConfig(){
			_instruction = textInstruction.Text;
			_modelsPath = textModelsPath.Text;
			_nThreads = (int)numThreads.Value;
			_ctxSize = (int)numCtxSize.Value;
			_gpuLayers = (int)numGPULayers.Value;
			_temp = (float)numTemp.Value;
			_nGen = (int)numNGen.Value;
			_numaStrat = (NativeMethods.GgmlNumaStrategy)comboNUMAStrat.SelectedIndex;
			_repPen = (float)numRepPen.Value;
			_topK = (int)numTopK.Value;
			_topP = (float)numTopP.Value;
			_batchSize = (int)numBatchSize.Value;
			_strictCPU = checkStrictCPU.Checked;
		}
		private void ButApply_Click(object sender, EventArgs e){
			if(Settings.Default.Instruction != textInstruction.Text) {
				NativeMethods.SetSystemPrompt(textInstruction.Text);
				NativeMethods.RetokenizeChat();
			}
			if(Settings.Default.ModelsDir != textModelsPath.Text) PopulateModels();
			if(Settings.Default.Threads != numThreads.Value) NativeMethods.SetThreadCount((int)numThreads.Value);
			Settings.Default.Instruction = textInstruction.Text;
			Settings.Default.ModelsDir = textModelsPath.Text;
			Settings.Default.Threads = numThreads.Value;
			Settings.Default.CtxSize = numCtxSize.Value;
			Settings.Default.GPULayers = numGPULayers.Value;
			Settings.Default.Temp = numTemp.Value;
			Settings.Default.NGen = numNGen.Value;
			Settings.Default.NUMAStrat = comboNUMAStrat.SelectedIndex;
			Settings.Default.RepPen = numRepPen.Value;
			Settings.Default.TopK = numTopK.Value;
			Settings.Default.TopP = numTopP.Value;
			Settings.Default.BatchSize = numBatchSize.Value;
			Settings.Default.strictCPU = checkStrictCPU.Checked;
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
	}
}
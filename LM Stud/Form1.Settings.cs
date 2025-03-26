using System;
using System.Windows.Forms;
using LMStud.Properties;
namespace LMStud{
	internal partial class Form1{
		private string _instruction = "";
		private int _batchSize = 512;
		private int _ctxSize = 4096;
		private int _nGen = -1;
		private int _nThreads = 8;
		private int _gpuLayers = 32;
		private NativeMethods.GgmlNumaStrategy _numaStrat = 0;
		private bool _strictCPU;
		private float _temp = 0.7f;
		private float _repPen = 1.05f;
		private int _topK = 40;
		private float _topP = 0.95f;
		private void SetConfig(){
			_instruction = textInstruction.Text;
			_numaStrat = (NativeMethods.GgmlNumaStrategy)comboNUMAStrat.SelectedIndex;
			_nThreads = (int)numThreads.Value;
			_temp = (float)numTemp.Value;
			_topK = (int)numTopK.Value;
			_topP = (float)numTopP.Value;
			_repPen = (float)numRepPen.Value;
			_nGen = (int)numNGen.Value;
			_ctxSize = (int)numCtxSize.Value;
			_gpuLayers = (int)numGPULayers.Value;
			_batchSize = (int)numBatchSize.Value;
			_strictCPU = checkStrictCPU.Checked;
		}
		private void ButApply_Click(object sender, EventArgs e){
			if(Settings.Default.Threads != numThreads.Value) NativeMethods.SetThreadCount((int)numThreads.Value);
			Settings.Default.Instruction = textInstruction.Text;
			Settings.Default.ModelsDir = textModelsPath.Text;
			Settings.Default.NUMAStrat = comboNUMAStrat.SelectedIndex;
			Settings.Default.Threads = numThreads.Value;
			Settings.Default.Temp = numTemp.Value;
			Settings.Default.TopK = numTopK.Value;
			Settings.Default.TopP = numTopP.Value;
			Settings.Default.RepPen = numRepPen.Value;
			Settings.Default.NGen = numNGen.Value;
			Settings.Default.CtxSize = numCtxSize.Value;
			Settings.Default.GPULayers = numGPULayers.Value;
			Settings.Default.BatchSize = numBatchSize.Value;
			Settings.Default.strictCPU = checkStrictCPU.Checked;
			Settings.Default.Save();
			SetConfig();
		}
		private void ButBrowse_Click(object sender, EventArgs e){
			if(folderBrowserDialog1.ShowDialog(this) == DialogResult.OK){
				textModelsPath.Text = folderBrowserDialog1.SelectedPath;
				PopulateModels();
			}
		}
		private void TextModelsPath_KeyDown(object sender, KeyEventArgs e){
			if(e.KeyCode != Keys.Enter) return;
			PopulateModels();
		}
	}
}
using System.Windows.Forms;
using LMStud.Properties;
namespace LMStud{
	internal static class Dialectics{
		internal static bool InitializeMode(Form1 owner){
			var slots = ModelSlotManager.ResolveDialecticLocalSlots();
			if(slots.Count < 2){
				MessageBox.Show(owner, "Load a local chat slot and a separate loaded local slot marked Dialectic before enabling dialectic mode.", Resources.LM_Stud, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
				return false;
			}
			var err = NativeMethods.ResetChat(slots[1].Name);
			if(err != NativeMethods.StudError.Success){
				owner.ShowError(Resources.Dialectic_enable, err);
				return false;
			}
			Generation.ConfigureDialecticSlots(slots);
			return true;
		}
	}
}

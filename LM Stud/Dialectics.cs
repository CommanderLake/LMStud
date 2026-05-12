using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using LMStud.Properties;
namespace LMStud{
	internal static class Dialectics{
		internal static bool InitializeMode(Form1 owner, bool confirmRelayReset = true){
			var slots = ModelSlotManager.ResolveDialecticLocalSlots();
			if(slots.Count == 0){
				MessageBox.Show(owner, Resources.Load_a_model_first_, Resources.LM_Stud, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
				return false;
			}
			if(slots.Count > 1 && confirmRelayReset &&
				MessageBox.Show(owner, "Dialectic relay will clear the current native chat state for slot \"" + slots[1].Name + "\" before using it as the reply partner.", Resources.LM_Stud,
					MessageBoxButtons.OKCancel, MessageBoxIcon.Exclamation) != DialogResult.OK)
				return false;
			var err = slots.Count > 1 ? InitializeRelay(slots[0], slots[1]) : NativeMethods.DialecticInit(slots[0].Name);
			if(err != NativeMethods.StudError.Success){
				FreeStateForSlots(slots);
				owner.ShowError(Resources.Dialectic_enable, err);
				return false;
			}
			Generation.ConfigureDialecticSlots(slots);
			return true;
		}
		internal static void FreeState(){
			var slotNames = Generation.GetDialecticSlotNames();
			if(slotNames.Length == 0) NativeMethods.DialecticFree("main");
			else foreach(var slotName in DistinctSlotNames(slotNames)) NativeMethods.DialecticFree(slotName);
		}
		private static NativeMethods.StudError InitializeRelay(ModelSlot primary, ModelSlot secondary){
			var err = NativeMethods.DialecticRelayInit(primary.Name);
			if(err != NativeMethods.StudError.Success) return err;
			err = NativeMethods.ResetChat(secondary.Name);
			if(err != NativeMethods.StudError.Success) return err;
			return NativeMethods.DialecticRelayInit(secondary.Name);
		}
		private static void FreeStateForSlots(IEnumerable<ModelSlot> slots){
			if(slots == null) return;
			foreach(var slot in slots.Where(slot => slot != null).GroupBy(slot => slot.Name, StringComparer.OrdinalIgnoreCase).Select(group => group.First()).Where(slot => !string.IsNullOrWhiteSpace(slot.Name))) NativeMethods.DialecticFree(slot.Name);
		}
		private static IEnumerable<string> DistinctSlotNames(IEnumerable<string> slotNames){
			return slotNames.Where(name => !string.IsNullOrWhiteSpace(name)).Distinct(StringComparer.OrdinalIgnoreCase);
		}
	}
}

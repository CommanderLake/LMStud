using System;
using System.Collections.Generic;
using System.Linq;
namespace LMStud{
	internal static class NativeChat{
		internal static string GetActiveSlotName(){
			if(!string.IsNullOrWhiteSpace(Generation.CntDialSlotName)) return Generation.CntDialSlotName;
			return ModelSlotManager.GetActiveChatSlot()?.Name ?? Common.ActiveModelSlotName ?? ModelSlotManager.MainSlotName;
		}
		internal static IEnumerable<string> GetActiveSlotNames(){
			var slotNames = Generation.GetDialecticSlotNames();
			return slotNames.Length > 0 ? slotNames : new[]{ GetActiveSlotName() };
		}
		internal static NativeMethods.StudError ResetState(){return RunForActiveSlots(NativeMethods.ResetChat);}
		internal static NativeMethods.StudError RemoveMessageAt(int index){return RunForActiveSlots(slotName => NativeMethods.RemoveMessageAt(slotName, index));}
		internal static NativeMethods.StudError RemoveMessagesStartingAt(int index){return RunForActiveSlots(slotName => NativeMethods.RemoveMessagesStartingAt(slotName, index));}
		internal static NativeMethods.StudError SetMessageAt(int index, MessageRole role, string think, string message){
			var hasImages = false;
			var contentJson = role == MessageRole.User ? MarkdownImages.BuildNativeContentJson(message, out hasImages) : null;
			return RunForActiveSlots(slotName => hasImages ? NativeMethods.SetMessageAtJson(slotName, index, think, contentJson) : NativeMethods.SetMessageAt(slotName, index, think, message));
		}
		internal static NativeMethods.StudError SetSystemPrompt(string slotName, string prompt, string toolsPrompt){
			return NativeMethods.SetSystemPrompt(string.IsNullOrWhiteSpace(slotName) ? GetActiveSlotName() : slotName, prompt, toolsPrompt);
		}
		private static NativeMethods.StudError RunForActiveSlots(Func<string, NativeMethods.StudError> action){
			if(!Generation.DialecticRelayEnabled) return action(GetActiveSlotName());
			var result = NativeMethods.StudError.Success;
			var applied = false;
			foreach(var slotName in Generation.GetDialecticSlotNames().Where(name => !string.IsNullOrWhiteSpace(name)).Distinct(StringComparer.OrdinalIgnoreCase)){
				result = action(slotName);
				if(result == NativeMethods.StudError.Success){
					applied = true;
					continue;
				}
				if(result == NativeMethods.StudError.IndexOutOfRange) continue;
				break;
			}
			return applied && result == NativeMethods.StudError.IndexOutOfRange ? NativeMethods.StudError.Success : result;
		}
	}
}

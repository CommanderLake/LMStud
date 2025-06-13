using System;
using System.Drawing;
using System.Windows.Forms;
namespace LMStud{
	internal class MyFlowLayoutPanel : FlowLayoutPanel{
		private readonly IntPtr _sbBottom = (IntPtr)7;
		private readonly IntPtr _sbEndscroll = (IntPtr)8;
		private bool _scrollable = true;
		internal void ScrollToEnd(){
			if(!_scrollable || Handle == IntPtr.Zero) return;
			var m = Message.Create(Handle, NativeMethods.WmVscroll, _sbBottom, IntPtr.Zero);
			base.WndProc(ref m);
		}
		protected override void WndProc(ref Message m){
			if(m.Msg == NativeMethods.WmLbuttondown || m.Msg == NativeMethods.WmVscroll && m.WParam != _sbBottom) _scrollable = false;
			switch(m.Msg){
				case NativeMethods.WmVscroll when m.WParam == _sbEndscroll:
					_scrollable = true;
					break;
			}
			base.WndProc(ref m);
		}
		protected override Point ScrollToControl(Control activeControl){
			//return base.ScrollToControl(activeControl);
			return AutoScrollPosition;
		}
	}
}
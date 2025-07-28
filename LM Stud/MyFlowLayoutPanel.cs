using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
namespace LMStud{
	internal class MyFlowLayoutPanel : FlowLayoutPanel{
		private const int WmVscroll = 0x0115;
		private const int WsHscroll = 0x00100000;
		private const int WsVscroll = 0x00200000;
		private const int SbVert = 1;
		private const int EsbEnableBoth = 0x0000;
		private const int EsbDisableBoth = 0x0003;
		private const int WmLbuttondown = 0x0201;
		private readonly IntPtr _sbBottom = (IntPtr)7;
		private readonly IntPtr _sbEndscroll = (IntPtr)8;
		private bool _scrollable = true;
		private bool _userScrolling;
		protected override CreateParams CreateParams{
			get{
				var cp = base.CreateParams;
				cp.Style &= ~(WsHscroll | WsVscroll);
				cp.Style |= WsVscroll;
				return cp;
			}
		}
		protected override void OnHandleCreated(EventArgs e){
			base.OnHandleCreated(e);
			UpdateScrollState();
		}
		protected override void OnLayout(LayoutEventArgs e){
			base.OnLayout(e);
			UpdateScrollState();
		}
		protected override void OnSizeChanged(EventArgs e){
			base.OnSizeChanged(e);
			UpdateScrollState();
		}
		protected override void OnControlAdded(ControlEventArgs e){
			base.OnControlAdded(e);
			UpdateScrollState();
		}
		protected override void OnControlRemoved(ControlEventArgs e){
			base.OnControlRemoved(e);
			UpdateScrollState();
		}
		private void UpdateScrollState(){
			if(!IsHandleCreated) return;
			var canScrollNow = DisplayRectangle.Height > ClientSize.Height;
			var desiredScrollable = canScrollNow && !_userScrolling;
			if(_scrollable != desiredScrollable){
				_scrollable = desiredScrollable;
				if(!_scrollable && AutoScrollPosition != Point.Empty) AutoScrollPosition = new Point(0, 0);
			}
			NativeMethods.EnableScrollBar(new HandleRef(this, Handle), SbVert, _scrollable ? EsbEnableBoth : EsbDisableBoth);
		}
		protected override void WndProc(ref Message m){
			if(m.Msg == WmLbuttondown || (m.Msg == WmVscroll && m.WParam != _sbBottom)){
				_userScrolling = true;
				_scrollable = false;
			}
			switch(m.Msg){
				case WmVscroll when m.WParam == _sbEndscroll:
					_userScrolling = false;
					UpdateScrollState();
					break;
			}
			base.WndProc(ref m);
		}
		internal void ScrollToEnd(){
			if(!_scrollable || Handle == IntPtr.Zero) return;
			var m = Message.Create(Handle, WmVscroll, _sbBottom, IntPtr.Zero);
			base.WndProc(ref m);
		}
	}
}
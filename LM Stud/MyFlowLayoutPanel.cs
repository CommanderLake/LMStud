using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
namespace LMStud{
	public class MyFlowLayoutPanel : FlowLayoutPanel{
		private const int WmVscroll = 0x0115;
		private const int WsHscroll = 0x00100000;
		private const int WsVscroll = 0x00200000;
		private const int SbVert = 1;
		private const int EsbEnableBoth = 0x0000;
		private const int EsbDisableBoth = 0x0003;
		private const int SbThumbtrack = 5;
		private const int SbEndscroll = 8;
		private readonly IntPtr _sbBottom = (IntPtr)7;
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
			var desiredScrollable = canScrollNow;
			if(_scrollable != desiredScrollable){
				_scrollable = desiredScrollable;
				if(!_scrollable && AutoScrollPosition != Point.Empty) AutoScrollPosition = new Point(0, 0);
			}
			NativeMethods.EnableScrollBar(new HandleRef(this, Handle), SbVert, _scrollable ? EsbEnableBoth : EsbDisableBoth);
		}
		protected override void WndProc(ref Message m){
			if(m.Msg == WmVscroll){
				var code = (int)m.WParam & 0xFFFF;
				if(code == SbThumbtrack){ _userScrolling = true; } else if(code == SbEndscroll){
					_userScrolling = false;
					UpdateScrollState();
				}
			}
			base.WndProc(ref m);
		}
		internal void ScrollToEnd(){
			if(!_scrollable || Handle == IntPtr.Zero || !Form1.This.checkAutoScroll.Checked) return;
			if(_userScrolling) return;
			var m = Message.Create(Handle, WmVscroll, _sbBottom, IntPtr.Zero);
			base.WndProc(ref m);
		}
		protected override Point ScrollToControl(Control activeControl){return AutoScrollPosition;}
	}
}
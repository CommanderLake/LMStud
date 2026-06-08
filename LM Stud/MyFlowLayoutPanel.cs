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
		private const int SbThumbtrack = 5;
		private const int SbEndscroll = 8;
		private const int WheelDelta = 120;
		private const int WheelPageScroll = -1;
		private readonly IntPtr _sbBottom = (IntPtr)7;
		private bool _scrollable = true;
		private bool _scrollToEndPending;
		private bool _userScrolling;
		private int _wheelDeltaRemainder;
		internal bool AutoScrollEnable = true;
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
		protected override void OnMouseWheel(MouseEventArgs e){
			ScrollByWheelDelta(e.Delta, SystemInformation.MouseWheelScrollLines);
			if(e is HandledMouseEventArgs handled) handled.Handled = true;
		}
		internal void ScrollByWheelDelta(int delta, int scrollLines){
			if(delta == 0 || scrollLines == 0 || !AutoScroll || DisplayRectangle.Height <= ClientSize.Height) return;
			_wheelDeltaRemainder += delta;
			var detents = _wheelDeltaRemainder/WheelDelta;
			_wheelDeltaRemainder %= WheelDelta;
			if(detents == 0) return;
			var distance = scrollLines == WheelPageScroll
				? Math.Max(1, ClientSize.Height)
				: Math.Max(1, Font.Height)*Math.Max(1, scrollLines);
			var current = -AutoScrollPosition.Y;
			var maximum = Math.Max(0, DisplayRectangle.Height - ClientSize.Height);
			var target = Math.Max(0, Math.Min(maximum, current - detents*distance));
			if(target != current) AutoScrollPosition = new Point(0, target);
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
			if(!AutoScrollEnable || _userScrolling || !IsHandleCreated || IsDisposed || _scrollToEndPending) return;
			_scrollToEndPending = true;
			try{
				BeginInvoke(new MethodInvoker(() => {
					_scrollToEndPending = false;
					if(!AutoScrollEnable || _userScrolling || IsDisposed || !IsHandleCreated) return;
					PerformLayout();
					UpdateScrollState();
					if(!_scrollable) return;
					var m = Message.Create(Handle, WmVscroll, _sbBottom, IntPtr.Zero);
					base.WndProc(ref m);
				}));
			} catch(ObjectDisposedException){ _scrollToEndPending = false; }
			catch(InvalidOperationException){ _scrollToEndPending = false; }
		}
		protected override Point ScrollToControl(Control activeControl){return AutoScrollPosition;}
	}
}

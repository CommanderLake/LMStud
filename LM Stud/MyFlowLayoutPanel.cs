using System.Windows.Forms;

namespace LMStud {
	class MyFlowLayoutPanel : FlowLayoutPanel {
        protected override System.Drawing.Point ScrollToControl(Control activeControl) {
            //return base.ScrollToControl(activeControl);
            return AutoScrollPosition;
        }
    }
}

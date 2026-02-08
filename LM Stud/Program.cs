using System;
using System.Windows.Forms;
namespace LMStud{
	internal static class Program{
		internal static Form1 MainForm;
		/// <summary>
		///     The main entry point for the application.
		/// </summary>
		[STAThread]
		public static void Main(){
			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(false);
			MainForm = new Form1();
			Application.Run(MainForm);
		}
	}
}
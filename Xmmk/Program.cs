using System;
using Xwt;

namespace Xmmk
{
	class MainClass
	{
        [STAThread]
		public static void Main (string[] args)
		{
			Application.Initialize ();

			var w = new MainWindow ();
			w.Closed += (o, e) => Application.Exit ();
			w.Show ();
			
			Application.Run ();
		}
	}
}

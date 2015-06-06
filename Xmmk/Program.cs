using System;
using Xwt;

namespace Xmmk
{
	class MainClass
	{
		public static void Main (string[] args)
		{
			Application.Initialize (ToolkitType.Gtk);

			var w = new MainWindow ();
			w.Closed += (o, e) => Application.Exit ();
			w.Show ();
			
			Application.Run ();
		}
	}
}

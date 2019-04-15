using System;
using System.IO;
using System.IO.IsolatedStorage;
using System.Xml;

namespace Xmmk
{
	class UserSettings
	{
		public int OutputChannel { get; set; }
		public string DefaultOutputDevice { get; set; }
		public string DefaultInputDevice { get; set; }
		public string KeyMapLow { get; set; }
		public string KeyMapHigh { get; set; }

		const string settings_file = "xmmk.settings.txt";
		
		public void Load ()
		{
			using (var store = IsolatedStorageFile.GetUserStoreForAssembly ()) {
				if (!store.FileExists (settings_file))
					return;
				using (var stream = store.OpenFile (settings_file, FileMode.Open)) {
					var doc = new XmlDocument ();
					doc.Load (stream);
					foreach (var pi in GetType ().GetProperties ()) {
						var v = doc.SelectSingleNode ("/settings/" + pi.Name)?.InnerText;
						if (v != null)
							pi.SetValue (this,Convert.ChangeType (v, pi.PropertyType));
					}
				}
			}
		}

		public void Save ()
		{
			using (var store = IsolatedStorageFile.GetUserStoreForAssembly ()) {
				using (var stream = store.CreateFile (settings_file)) {
					using (var xw = XmlWriter.Create (stream)) {
						xw.WriteStartElement ("settings");
						foreach (var pi in GetType ().GetProperties ())
							xw.WriteElementString (pi.Name, pi.GetValue (this)?.ToString ());
						xw.WriteEndElement ();
					}
				}
			}
		}
	}
}
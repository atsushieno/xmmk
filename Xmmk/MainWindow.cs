using System;
using System.Collections.Generic;
using System.IO;
using Xwt;
using Xwt.Drawing;
using Commons.Music.Midi;

using Keys = Xwt.Key;

namespace Xmmk
{
	public partial class MainWindow : Window
	{
		MidiController midi = new MidiController ();

		public MainWindow ()
		{
			midi.SetupMidiDevices ();
			midi.OutputDeviceChanged += (o, e) => UpdateBankSelectorMenu ();
			
			SetupMenu ();
			
			SetupKeyboard ();
		}
		
		void SetupMenu ()
		{
			var fileMenu = new Menu ();
			var exit = new MenuItem ("E_xit");
			exit.Clicked += delegate {
				Close ();
			};
			fileMenu.Items.Add (exit);

			var viewMenu = new Menu ();
			var changeLayout = new MenuItem ("_Change Keyboard Layout");
			var layoutPiano = new MenuItem ("Piano");
			layoutPiano.Clicked += KeyboardLayoutChanged;
			var layoutChromaTone = new MenuItem ("Chromatone");
			layoutChromaTone.Clicked += KeyboardLayoutChanged;
			var changeLayoutSubMenu = new Menu ();
			changeLayout.SubMenu = changeLayoutSubMenu;
			changeLayoutSubMenu.Items.Add (layoutPiano);
			changeLayoutSubMenu.Items.Add (layoutChromaTone);
			viewMenu.Items.Add (changeLayout);

			var toneMenu = new Menu ();
			for (int i = 0; i < GeneralMidi.InstrumentCategories.Length; i++) {
				var item = new MenuItem (GeneralMidi.InstrumentCategories [i]);
				var catMenu = new Menu ();
				for (int j = 0; j < 8; j++) {
					var tone = new MenuItem (GeneralMidi.InstrumentNames [i * 8 + j]);
					tone.Clicked += ProgramSelected;
					catMenu.Items.Add (tone);
				}
				item.SubMenu = catMenu;
				toneMenu.Items.Add (item);
			}

			var deviceMenu = new Menu ();
			var outputItem = new MenuItem ("_Output");
			device_output_menu = new Menu ();
			outputItem.SubMenu = device_output_menu;
			SetupDeviceSelectorMenu (true);
			//output.Clicked += delegate { SetupDeviceSelector (true); };
			deviceMenu.Items.Add (outputItem);
			var inputItem = new MenuItem ("_Input");
			device_input_menu = new Menu ();
			inputItem.SubMenu = device_input_menu;
			SetupDeviceSelectorMenu (false);
			//input.Clicked += delegate { SetupDeviceSelector (false); };
			deviceMenu.Items.Add (inputItem);
			
			MainMenu = new Menu ();
			MainMenu.Items.Add (new MenuItem ("_File") { SubMenu = fileMenu });
			MainMenu.Items.Add (new MenuItem ("_View") { SubMenu = viewMenu });
			MainMenu.Items.Add (new MenuItem ("_Tone") { SubMenu = toneMenu });
			MainMenu.Items.Add (new MenuItem ("_Device") { SubMenu = deviceMenu });
		}

		private void KeyboardLayoutChanged (object sender, EventArgs e)
		{
			var menuItem = (MenuItem) sender;
			if (menuItem.Label == "Piano") {
				ChromaTone = false;
				key_labels = key_labels_piano;
			} else {
				ChromaTone = true;
				key_labels = key_labels_chromatone;
			}
			SetupKeyboard ();
		}

		void ProgramSelected (object sender, EventArgs e)
		{
			midi.ChangeProgram (Array.IndexOf (GeneralMidi.InstrumentNames, ((MenuItem)sender).Label));
		}
	
		protected void OnQuitActionActivated (object sender, EventArgs e)
		{
			Application.Exit ();
		}

		#region MIDI configuration

		Menu device_output_menu;
		Menu device_input_menu;

		void SetupDeviceSelectorMenu (bool isOutput)
		{
			var menu = isOutput ? device_output_menu : device_input_menu;
			int i = 0;
			menu.Items.Clear ();
			foreach (var dev in isOutput ? midi.MidiAccess.Outputs : midi.MidiAccess.Inputs) {
				var devItem = new MenuItem ("_" + i++ + ": " + dev.Name);
				devItem.Clicked += delegate {
					if (isOutput)
						midi.ChangeOutputDevice (dev.Id);
					else
						midi.ChangeInputDevice (dev.Id);
				};
				menu.Items.Add (devItem);
			}

			// bring back focus to the keyboard panel.
			if (Content != null)
				Content.SetFocus ();
		}

		void UpdateBankSelectorMenu ()
		{
			var db = midi.CurrentOutputMidiModule;
			if (db != null && db.Instrument != null && db.Instrument.Maps.Count > 0) {
				var map = db.Instrument.Maps [0];
				foreach (var prog in map.Programs) {
					/*
					var mcat = tone_menu.MenuItems [prog.Index / 8];
					var mprg = mcat.MenuItems [prog.Index % 8];
					mprg.MenuItems.Clear ();
					foreach (var bank in prog.Banks) {
						var mi = new MenuItem (String.Format ("{0}:{1} {2}", bank.Msb, bank.Lsb, bank.Name)) { Tag = bank };
						mi.Select += delegate {
							var mbank = (MidiBankDefinition) mi.Tag;
							output.Write (0, new MidiMessage (SmfEvent.CC + channel, SmfCC.BankSelect, mbank.Msb));
							output.Write (0, new MidiMessage (SmfEvent.CC + channel, SmfCC.BankSelectLsb, mbank.Lsb));
							output.Write (0, new MidiMessage (SmfEvent.Program + channel, (int) mi.Parent.Tag, 0));
						};
						mprg.MenuItems.Add (mi);
					}
				*/
				}
			}
		}

		#endregion

		#region Keyboard

		static readonly string [] key_labels_chromatone = {"c", "c+", "d", "d+", "e", "f", "f+", "g", "g+", "a", "a+", "b"};
		static readonly string [] key_labels_piano = {"c", "c+", "d", "d+", "e", "", "f", "f+", "g", "g+", "a", "a+", "b", ""};
		static string [] key_labels = key_labels_piano;

		public bool ChromaTone = false;

		void SetupKeyboard ()
		{
			var panel = new VBox () { CanGetFocus = true };
			HBox keys1 = new HBox (), keys2 = new HBox (), keys3 = new HBox (), keys4 = new HBox ();

			var keyRows = new List<Tuple<string, List<Button>, HBox, HBox, Action<Button[]>>> ();
			// (JP106) offset 4, 10, 18 are not mapped, so skip those numbers
			var hl = new List<Button> ();
			keyRows.Add (Tuple.Create (keymap.HighKeys, hl, keys1, keys2, new Action<Button[]> (a => high_buttons = a)));
			var ll = new List<Button> ();
			keyRows.Add (Tuple.Create (keymap.LowKeys, ll, keys3, keys4, new Action<Button []> (a => low_buttons = a)));

			foreach (var keyRow in keyRows) {
				int labelStringIndex = key_labels.Length - 5;
				for (int i = 0; i < keyRow.Item1.Length; i++) {
					var b = new NoteButton ();
					b.Label = key_labels [labelStringIndex % key_labels.Length];
					labelStringIndex++;
					if (!IsNotableIndex (i)) {
						b.CanGetFocus = false;
						b.Sensitive = false;
						// this seems to only work fine on GTK3...
						b.BackgroundColor = new Color (0, 0, 0, 255);
					}
					keyRow.Item2.Add (b);
					(i % 2 == 0 ? keyRow.Item3 : keyRow.Item4).PackStart (b);
				}
				keyRow.Item5 (keyRow.Item2.ToArray ());
			}

			high_button_states = new bool [high_buttons.Length];
			low_button_states = new bool [low_buttons.Length];

			panel.PackStart (CreatePlacement (keys1, 0));
			panel.PackStart (CreatePlacement (keys2, 0.33));
			panel.PackStart (CreatePlacement (keys3, 0.66));
			panel.PackStart (CreatePlacement (keys4, 1.0));

			panel.KeyPressed += (o, e) => ProcessKey (true, e);
			panel.KeyReleased += (o, e) => ProcessKey (false, e);			
			
			this.Content = panel;

			panel.SetFocus (); // it is not focused when layout is changed.
		}

		Placement CreatePlacement (Widget c, double xAlign)
		{
			var p = new Placement () { Child = c, XAlign = xAlign };
			return p;
		}

		const int btSize = 40;
		Button [] high_buttons;
		Button [] low_buttons;
		bool [] high_button_states;
		bool [] low_button_states;

		class NoteButton : Button
		{
			public NoteButton ()
			{
				WidthRequest = btSize;
				//HeightRequest = btSize;
				CanGetFocus = false;
			}
		}

		// check if the key is a notable key (in mmk).
		bool IsNotableIndex (int i)
		{
			if (ChromaTone)
				return true;

			switch (i) {
			case 4:
			case 10:
			case 18:
				return false;
			}
			return true;
		}

		class KeyMap
		{
			// note that those arrays do not contain non-mapped notes: index at 4, 10, 18

			// keyboard map for JP106
			// [1][2][3][4][5][6][7][8][9][0][-][^][\]
			//  [Q][W][E][R][T][Y][U][I][O][P][@][{]
			//  [A][S][D][F][G][H][J][K][L][;][:][}]
			//   [Z][X][C][V][B][N][M][<][>][?][_]
			// [UP] - octave up
			// [DOWN] - octave down
			// [LEFT] - <del>transpose decrease</del>
			// [RIGHT] - <del>transpose increase</del>

			public static readonly KeyMap JP106 = new KeyMap ("AZSXDCFVGBHNJMK\xbcL\xbe\xbb\xbf\xba\xe2\xdd ", "1Q2W3E4R5T6Y7U8I9O0P\xbd\xc0\xde\xdb\xdc");

			public KeyMap (string lowKeys, string highKeys)
			{
				LowKeys = lowKeys;
				HighKeys = highKeys;
			}

			public readonly string LowKeys;
			public readonly string HighKeys;
		}
		
		KeyMap keymap = KeyMap.JP106; // FIXME: make it adjustable

		#endregion

		#region Key Events

		int octave = 4; // lowest
		int transpose = 0;

		void ProcessKey (bool down, KeyEventArgs e)
		{
			var key = e.Key;
			switch (key) {
			case Keys.Up:
				if (!down && octave < 7)
					octave++;
				break;
			case Keys.Down:
				if (!down && octave > 0)
					octave--;
				break;
			case Keys.Left:
				transpose--;
				break;
			case Keys.Right:
				transpose++;
				break;
			default:
				var ch = char.ToUpper ((char) key);
				var idx = keymap.LowKeys.IndexOf (ch);
				if (!IsNotableIndex (idx))
					return;

				if (idx >= 0)
					ProcessNoteKey (down, true, idx);
				else {
					idx = keymap.HighKeys.IndexOf (ch);
					if (!IsNotableIndex (idx))
						return;
					if (idx >= 0)
						ProcessNoteKey (down, false, idx);
					else
						return;
				}
				break;
			}
		}

		void ProcessNoteKey (bool down, bool low, int idx)
		{
			var fl = low ? low_button_states : high_button_states;
			if (fl [idx] == down)
				return; // no need to process repeated keys.

			var b = low ? low_buttons [idx] : high_buttons [idx];
			// since BackgroundColor does not work on GTK2, hack label instead.
			if (down)
				//b.BackgroundColor = Colors.Gray;
				b.Label = "*" + b.Label;
			else
				//b.BackgroundColor = Colors.White;
				b.Label = b.Label.Substring (1);
			fl [idx] = down;

			int nid = idx;
			if (!ChromaTone) {
				if (idx < 4)
					nid = idx;
				else if (idx < 10)
					nid = idx - 1;
				else if (idx < 18)
					nid = idx - 2;
				else
					nid = idx - 3;
			}

			int note;
			if (ChromaTone)
				note = octave * 12 - 5 + nid + (low ? 2 : 0) + transpose;
			else
				note = (octave + (low ? 0 : 1)) * 12 - 4 + nid + transpose;

			if (0 <= note && note <= 128)
				midi.NoteOn ((byte) note, (byte) (down ? 100 : 0));
		}
		
		#endregion
	}
}

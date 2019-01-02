using System;
using System.Collections.Generic;
using System.Linq;
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
			this.Closed += (o, e) => Application.Exit ();

			midi.SetupMidiDevices ();
			midi.OutputDeviceChanged += (o, e) => SetupToneMenu ();
			
			SetupMenu ();

			SetupWindowContent ();
		}

		Menu tone_menu;

		void SetupMenu ()
		{
			var fileMenu = new Menu ();
			var exit = new MenuItem ("E_xit");
			exit.Clicked += delegate {
				Close ();
			};
			fileMenu.Items.Add (exit);

			tone_menu = new Menu ();
			SetupToneMenu ();

			MainMenu = new Menu ();
			MainMenu.Items.Add (new MenuItem ("_File") { SubMenu = fileMenu });
			MainMenu.Items.Add (new MenuItem ("_Tone") { SubMenu = tone_menu });
		}

		void SetupToneMenu ()
		{
			tone_menu.Items.Clear ();

			var moduleDB = midi.CurrentOutputMidiModule;
			var instMapUnordered = (moduleDB == null || moduleDB.Instrument == null || moduleDB.Instrument.Maps.Count == 0) ? null : moduleDB.Instrument.Maps.First ();
			var progs = instMapUnordered?.Programs?.OrderBy (p => p.Index);
			int progsSearchFrom = 0;
			for (int i = 0; i < GeneralMidi.InstrumentCategories.Length; i++) {
				var item = new MenuItem (GeneralMidi.InstrumentCategories [i]);
				var catMenu = new Menu ();
				for (int j = 0; j < 8; j++) {
					int index = i * 8 + j;
					var prog = progs?.Skip (progsSearchFrom)?.First (p => p.Index == index);
					var name = prog != null ? prog.Name : GeneralMidi.InstrumentNames [index];
					var tone = new MenuItem (name);
					if (prog != null && prog.Banks != null && prog.Banks.Any ()) {
						var bankMenu = new Menu ();
						foreach (var bank in prog.Banks) {
							var bankItem = new MenuItem (bank.Name);
							bankItem.Tag = new Tuple<int,MidiBankDefinition> (index,bank);
							bankItem.Clicked += ProgramSelected;
							bankMenu.Items.Add (bankItem);
						}
						tone.SubMenu = bankMenu;
					} else {
						tone.Tag = index;
						tone.Clicked += ProgramSelected;
					}
					catMenu.Items.Add (tone);
				}
				item.SubMenu = catMenu;
				tone_menu.Items.Add (item);
			}
		}

		private void KeyboardLayoutChanged (object sender, EventArgs e)
		{
			var menuItem = (MenuItem) sender;
			if (menuItem.Label == "Piano") {
				ChromaTone = false;
			} else {
				ChromaTone = true;
			}
			SetupWindowContent ();
		}

		void ProgramSelected (object sender, EventArgs e)
		{
			var mi = (MenuItem) sender;
			var bank = mi.Tag as Tuple<int, MidiBankDefinition>;
			if (bank != null)
				midi.ChangeProgram (bank.Item1, (byte) bank.Item2.Msb, (byte) bank.Item2.Lsb);
			else
				midi.ChangeProgram ((byte) (int) mi.Tag, 0, 0);
		}

		#region Keyboard

		static readonly string [] note_names = { "c", "c+", "d", "d+", "e", "f", "f+", "g", "g+", "a", "a+", "b" };
		static readonly string [] key_labels_chromatone = note_names;
		static readonly string [] key_labels_piano = {"c", "c+", "d", "d+", "e", "", "f", "f+", "g", "g+", "a", "a+", "b", ""};
		static string [] key_labels = key_labels_piano;

		int octave;
		int transpose;

		Label octave_label;
		Label transpose_label;

		public int Octave {
			get => octave;
			set {
				octave = value;
				octave_label.Text = octave.ToString ();
			}
		}

		public int Transpose {
			get => transpose;
			set {
				transpose = value;
				transpose_label.Text = transpose.ToString ();
			}
		}

		bool chroma_tone;
		public bool ChromaTone {
			get => chroma_tone;
			set {
				chroma_tone = value;
				key_labels = value ? key_labels_chromatone : key_labels_piano;
				SetupWindowContent ();
			}
		}

		TextEntry notepad;

		void SetupWindowContent ()
		{
			var entireContentBox = new VBox ();

			var headToolBox = SetupHeadToolBox ();
			entireContentBox.PackStart (headToolBox);

			var keyboard = SetupKeyboard ();
			entireContentBox.PackStart (keyboard);

			notepad = new TextEntry () {
				MultiLine = true,
				HeightRequest = 200,
				VerticalPlacement = WidgetPlacement.Start,		 
				CursorPosition = 0 };
			entireContentBox.PackStart (notepad);

			this.Content = entireContentBox;

			keyboard.SetFocus (); // it is not focused when layout is changed.
		}

		int current_device = 0;
		int current_layout = 0;

		HBox SetupHeadToolBox ()
		{
			var headToolBox = new HBox ();

			// device selector
			var deviceSelectorBox = new ComboBox ();
			foreach (var output in midi.MidiAccess.Outputs)
				deviceSelectorBox.Items.Add (output, output.Name);
			deviceSelectorBox.SelectedIndex = current_device;
			deviceSelectorBox.SelectionChanged += (sender, e) => {
				current_device = deviceSelectorBox.SelectedIndex;
				midi.ChangeOutputDevice (((IMidiPortDetails)deviceSelectorBox.SelectedItem).Id);
			};
			headToolBox.PackStart (deviceSelectorBox);

			// keyboard layout
			var layoutSelectorBox = new ComboBox ();
			layoutSelectorBox.Items.Add (false, "Piano");
			layoutSelectorBox.Items.Add (true, "ChromaTone");
			layoutSelectorBox.SelectedIndex = current_layout;
			layoutSelectorBox.SelectionChanged += (sender, e) => {
				current_layout = layoutSelectorBox.SelectedIndex;
				ChromaTone = (bool) layoutSelectorBox.SelectedItem;
			};
			headToolBox.PackStart (layoutSelectorBox);

			// octave and transpose
			this.octave_label = new Label ();
			this.transpose_label = new Label ();
			Octave = 4;
			Transpose = 0;
			headToolBox.PackStart (new Label { Text = "Octave:", TooltipText = "SHIFT + UP/DOWN to change it" });
			headToolBox.PackStart (octave_label);
			headToolBox.PackStart (new Label { Text = "Transpose:", TooltipText = "SHIFT + LEFT/RIGHT to change it" });
			headToolBox.PackStart (transpose_label);

			return headToolBox;
		}

		VBox SetupKeyboard ()
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

			return panel;
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

		void ProcessKey (bool down, KeyEventArgs e)
		{
			var key = e.Key;
			switch (key) {
			case Keys.Up:
				if (e.Modifiers == ModifierKeys.Shift && !down && octave < 7)
					Octave++;
				break;
			case Keys.Down:
				if (e.Modifiers == ModifierKeys.Shift && !down && octave > 0)
					Octave--;
				break;
			case Keys.Left:
				if (e.Modifiers == ModifierKeys.Shift)
					Transpose--;
				break;
			case Keys.Right:
				if (e.Modifiers == ModifierKeys.Shift)
					Transpose++;
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
				note = (octave + (low ? 0 : 1)) * 12 - 5 + nid + (low ? 2 : 0) + transpose;
			else
				note = (octave + (low ? 0 : 1)) * 12 - 4 + nid + transpose;

			if (0 <= note && note <= 128) {
				if (down) {
					var mml = NoteNumberToName (note);
					int newPosition = notepad.CursorPosition + mml.Length;
					notepad.Text = notepad.Text.Insert (notepad.CursorPosition, mml);
					if (notepad.Text.Length - notepad.Text.LastIndexOf ('\n') > notepad_wrap_line_at) {
						notepad.Text += "\r\n";
						newPosition++;
						newPosition++;
					}
					notepad.CursorPosition = newPosition;
				}
				midi.NoteOn ((byte)note, (byte)(down ? 100 : 0));
			}
		}

		const int notepad_wrap_line_at = 80;
		int last_octave = 4;
		DateTime last_note_on_time = DateTime.MinValue;

		string NoteNumberToName (int note)
		{
			string mml = high_button_states.Sum (v => v ? 1 : 0) <= 1 && low_button_states.Sum (v => v ? 1 : 0) <= 1 ? " " :
				DateTime.Now - last_note_on_time < TimeSpan.FromMilliseconds (100) ? "0" : "&";
			last_note_on_time = DateTime.Now;
			int newOctave = note / 12;
			for (int diff = newOctave - last_octave; diff > 0; diff--)
				mml += '>';
			for (int diff = newOctave - last_octave; diff < 0; diff++)
				mml += '<';
			mml += note_names [note % 12];
			last_octave = newOctave;
			return mml;
		}

		#endregion
	}
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using Xwt;
using Xwt.Drawing;
using Commons.Music.Midi;
using Xwt.Backends;
using Keys = Xwt.Key;
using Label = Xwt.Label;

namespace Xmmk
{
	public partial class MainWindow : Window
	{
		MidiController midi = new MidiController ();

		public MainWindow ()
		{
			this.Closed += (o, e) => Application.Exit ();

			this.Title = "Virtual MIDI keyboard Xmmk";
			Icon = Image.FromResource (GetType ().Assembly, "xmmk.png");

			midi.SetupMidiDevices ();
			midi.OutputDeviceChanged += (o, e) => SetupToneMenu ();
			
			SetupMenu ();

			SetupWindowContent ();
		}

		Menu tone_menu, keyboard_menu;

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

			keyboard_menu = new Menu ();
			foreach (var km in available_keymaps) {
				var mi = new MenuItem (km.Name);
				mi.Clicked += (sender, e) => {
					keyboard.Clear ();
					this.keymap = km;
					FillKeyboard (keyboard);
				};
				keyboard_menu.Items.Add (mi);
			}
			var shortcut_menu = new Menu ();
			shortcut_menu.Items.Add (new MenuItem ("_Keyboard") { SubMenu = keyboard_menu });

			MainMenu = new Menu ();
			MainMenu.Items.Add (new MenuItem ("_File") { SubMenu = fileMenu });
			MainMenu.Items.Add (new MenuItem ("_Tone") { SubMenu = tone_menu });
			MainMenu.Items.Add (new MenuItem ("_Shortcuts") { SubMenu = shortcut_menu });
			
			var devices = new MenuItem ("_Devices");
			devices.SubMenu = new Menu ();
			var outputDevices = new MenuItem ("_Output");
			var inputDevices = new MenuItem ("_Input");
			devices.SubMenu.Items.Add (outputDevices);
			devices.SubMenu.Items.Add (inputDevices);
			var outputDevMenu = new Menu ();
			outputDevices.Clicked += delegate {
				outputDevMenu.Items.Clear ();
				foreach (var output in midi.MidiAccess.Outputs) {
					var item = new CheckBoxMenuItem (output.Name)
						{ Tag = output.Id, Checked = output.Id == midi.CurrentDeviceId };
					item.Clicked += delegate { midi.ChangeOutputDevice ((string) item.Tag); };
					outputDevMenu.Items.Add (item);
				};
			};
			outputDevices.SubMenu = outputDevMenu;
			var inputDevMenu = new Menu ();
			inputDevices.Clicked += delegate {
				inputDevMenu.Items.Clear ();
				foreach (var input in midi.MidiAccess.Inputs.Where (d => d.Id != midi.VirtualPort.Details.Id)) {
					var item = new CheckBoxMenuItem (input.Name)
						{ Tag = input.Id, Checked = input.Id == midi.CurrentDeviceId };
					item.Clicked += delegate { midi.ChangeInputDevice ((string) item.Tag); };
					inputDevMenu.Items.Add (item);
				};
			};
			inputDevices.SubMenu = inputDevMenu;
			MainMenu.Items.Add (devices);			
		}

		void SetupToneMenu ()
		{
			tone_menu.Items.Clear ();
			
			var overrideDB = new MenuItem ("_Override MIDI module database");
			overrideDB.SubMenu = new Menu ();
			foreach (var db in MidiModuleDatabase.Default.All ()) {
				var module = new MenuItem (db.Name);
				module.SubMenu = new Menu ();
				foreach (var map in db.Instrument.Maps) {
					var mapItem = new MenuItem ("[Inst] " + map.Name);
					mapItem.Clicked += delegate {
						midi.MidiInstrumentMapOverride = map;
						SetupToneMenu ();
					};
					module.SubMenu.Items.Add (mapItem);
				}
				foreach (var map in db.Instrument.DrumMaps) {
					var mapItem = new MenuItem ("[Drum] " + map.Name);
					mapItem.Clicked += delegate {
						midi.MidiDrumMapOverride = map;
						SetupToneMenu ();
					};
					module.SubMenu.Items.Add (mapItem);
				}
				overrideDB.SubMenu.Items.Add (module);
			}
			tone_menu.Items.Add (overrideDB);
			tone_menu.Items.Add (new SeparatorMenuItem ());

			var instMap = midi.Channel == 9 ? midi.CurrentDrumMap : midi.CurrentInstrumentMap;
			var progs = instMap?.Programs?.OrderBy (p => p.Index);
			int progsSearchFrom = 0;
			for (int i = 0; i < GeneralMidi.InstrumentCategories.Length; i++) {
				var item = new MenuItem (midi.Channel == 9 ? "(DRUM)" : GeneralMidi.InstrumentCategories [i]);
				var catMenu = new Menu ();
				for (int j = 0; j < 8; j++) {
					int index = i * 8 + j;
					var prog = progs?.Skip (progsSearchFrom)?.FirstOrDefault (p => p.Index == index);
					var name = prog != null ? prog.Name : midi.Channel == 9 ? GeneralMidi.DrumKitsGM2.Length > index ? GeneralMidi.DrumKitsGM2 [index] : "" : GeneralMidi.InstrumentNames [index];
					var tone = new MenuItem ($"{index}: {name}") { Sensitive = name != "" };
					if (prog != null && prog.Banks != null && prog.Banks.Skip (1).Any ()) { // no need for only one bank
						var bankMenu = new Menu ();
						foreach (var bank in prog.Banks) {
							var bankItem = new MenuItem ($"{bank.Msb}, {bank.Lsb}: {bank.Name}");
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

		void KeyboardLayoutChanged (object sender, EventArgs e)
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

		TextEntry mml_record_pad;
		TextEntry mml_exec_pad;
		VBox keyboard;
		HBox entire_content_box;
		VBox keyboard_content_box;
		VBox player_list;
		ComboBox player_list_selector;

		void SetupWindowContent ()
		{
			var tabpage = new Notebook ();
			tabpage.Add (SetupPrimaryPageContent (), "Main");
			tabpage.Add (SetupKeyConfigurationPages (), "Key Configs");
			this.Content = tabpage;
		}

		Widget SetupKeyConfigurationPages ()
		{
			var entire_config_box = new VBox ();
			
			var panel = new Frame () { Label = "PC Keyboard Type" };
			var keyConfigs = new VBox ();
			
			var entryLabel = new Label {
				Text = "Enter sequence of Keycodes, for high keys and low keys respectively. For non-alphanumeric keys, enter '\\unnnn' (4 hexadecimal numbers). Spaces are ignored.",
				WidthRequest = 500,
				Wrap = WrapMode.Word
			};
			keyConfigs.PackStart (entryLabel, true);

			var entryControls = new HBox ();
			var entries = new VBox ();
			var highLabel = new Label {Text = "High keys"};
			var lowLabel = new Label {Text = "Low keys"};
			var highRow = new TextEntry () { WidthRequest = 500 };
			var lowRow = new TextEntry () { WidthRequest = 500 };
			entries.PackStart (highLabel, true);
			entries.PackStart (highRow, true);
			entries.PackStart (lowLabel, true);
			entries.PackStart (lowRow, true);
			entryControls.PackStart (entries);
			
			var buttons = new VBox ();
			var us101 = new Button ("US101");
			Func<string, string> escape = s => string.Join (" ", s.Select (c =>
				'0' <= c && c <= '9' || 'A' <= c && c <= 'Z' || 'a' <= c && c <= 'z' || @"!""#$%&'()=-~^|\@`[{+;*:}]<,>.?/_".IndexOf (c) >= 0
					? c.ToString ()
					: string.Format ("\\u{0:x04}", (int) c)));
			Func<string, string> unescape = s =>
				new System.Text.RegularExpressions.Regex (@"(\\u[0-9A-Za-z]4)").Replace (s, "\\1")
					.Replace (" ", "");
			Action<KeyMap> applyKeyMap = m => {
				highRow.Text = escape (m.HighKeys);
				lowRow.Text = escape (m.LowKeys);
			};
			us101.Clicked += (sender, args) => applyKeyMap (KeyMap.US101);
			var jp106 = new Button ("JP106");
			jp106.Clicked += (sender, args) => applyKeyMap (KeyMap.JP106);
			buttons.PackStart (us101);
			buttons.PackStart (jp106);
			
			applyKeyMap (keymap);
			
			entryControls.PackStart (buttons);
			keyConfigs.PackStart (entryControls);

			var applyButton = new Button ("Apply");
			applyButton.Clicked += (sender, args) => {
				keymap = new KeyMap (null, unescape (lowRow.Text), unescape (highRow.Text));
			};
			keyConfigs.PackStart (applyButton);

			panel.Content = keyConfigs;
			entire_config_box.PackStart (panel);
			
			return entire_config_box;
		}
		
		Widget SetupPrimaryPageContent ()
		{
			entire_content_box = new HBox ();
			keyboard_content_box = new VBox ();

			var headToolBox = SetupHeadToolBox ();
			keyboard_content_box.PackStart (headToolBox);
			
			entire_content_box.PackStart (keyboard_content_box);

			if (keyboard != null)
				entire_content_box.Remove (keyboard);
			keyboard = SetupKeyboard ();
			keyboard_content_box.PackStart (keyboard);

			// MML runner
			var mml_exec_box = new HBox ();
			var mml_exec_button = new Button {
				Label = "Run",
				TooltipText = "Compile and run MML"
			};
			mml_exec_button.Clicked += delegate { midi.ExecuteMml (mml_exec_pad.Text, player_list_selector.SelectedIndex); };
			mml_exec_box.PackStart (mml_exec_button, false);
			mml_exec_pad = new TextEntry () {
				MultiLine = true,
				HeightRequest = 50,
				VerticalPlacement = WidgetPlacement.Start,
				CursorPosition = 0,
				TooltipText = "You can send arbitrary MIDI messages in mugene MML syntax (track and channel (CH) are automatically prepended)",
			};
			mml_exec_box.PackStart (mml_exec_pad, true);
			keyboard_content_box.PackStart (mml_exec_box);

			// MML recording pad
			mml_record_pad = new TextEntry () {
				MultiLine = true,
				HeightRequest = 200,
				VerticalPlacement = WidgetPlacement.Start,		 
				CursorPosition = 0,
				TooltipText = "Your note on/off inputs are recorded here",
			};
			keyboard_content_box.PackStart (mml_record_pad);

			// Player list
			player_list = new VBox ();
			var playerListHeader = new HBox ();
			var playerListLabel = new Label ("Player: ");
			playerListHeader.PackStart (playerListLabel);
			player_list_selector = new ComboBox ();
			player_list_selector.Items.Add ("--new--");
			player_list_selector.SelectedIndex = 0;
			playerListHeader.PackStart (player_list_selector);
			player_list.PackStart (playerListHeader);
			int playerStartIndex = player_list.Children.Count ();

			midi.MusicListingChanged += (o, e) => {
				Application.InvokeAsync (() => {
					var widgetIndex = e.Index + playerStartIndex;
					var items = player_list.Children.ToList ();
					if (widgetIndex < player_list.Children.Count ())
						items.RemoveAt (widgetIndex);

					player_list.Clear ();
					var hbox = new HBox ();
					var label = new Label (e.Index.ToString ());
					hbox.PackStart (label);
					var button = new Button ("STOP");
					button.Clicked += (_, __) => midi.StopPlayer (e.Index);
					hbox.PackStart (button);
					items.Insert (widgetIndex, hbox);

					foreach (var item in items)
						player_list.PackStart (item);

					if (player_list_selector.Items.Count - 1 <= e.Index)
						player_list_selector.Items.Insert (e.Index, e.Index);
				});
			};
			
			entire_content_box.PackStart (player_list);
			
			keyboard.SetFocus (); // it is not focused when layout is changed.
			
			return entire_content_box;
		}

		int current_layout = 0;

		HBox SetupHeadToolBox ()
		{
			var headToolBox = new HBox ();

			// keyboard layout
			var layoutSelectorBox = new ComboBox () { TooltipText = "Select virtual keyboard layout" };
			layoutSelectorBox.Items.Add (false, "Piano Layout");
			layoutSelectorBox.Items.Add (true, "ChromaTone Layout");
			layoutSelectorBox.SelectedIndex = current_layout;
			layoutSelectorBox.SelectionChanged += (sender, e) => {
				current_layout = layoutSelectorBox.SelectedIndex;
				ChromaTone = (bool) layoutSelectorBox.SelectedItem;
			};
			headToolBox.PackStart (layoutSelectorBox);

			// channel selector
			headToolBox.PackStart (new Label {Text = "Ch."});
			var channelSelectorBox = new ComboBox { TooltipText = "Set MIDI output channel. 10 for drums."};
			foreach (var n in Enumerable.Range(0, 15))
				channelSelectorBox.Items.Add ((n + 1).ToString ());
			channelSelectorBox.SelectedIndex = midi.Channel;
			channelSelectorBox.SelectionChanged += delegate {
				midi.Channel = channelSelectorBox.SelectedIndex;
				SetupToneMenu ();
			};
			headToolBox.PackStart (channelSelectorBox);

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
			var panel = new VBox () { Name = "keyboard", CanGetFocus = true };
			panel.KeyPressed += (o, e) => ProcessKey (true, e);
			panel.KeyReleased += (o, e) => ProcessKey (false, e);

			FillKeyboard (panel);
			return panel;
		}

		VBox FillKeyboard (VBox panel)
		{
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
					var b = new Button () { WidthRequest = btSize, CanGetFocus = false };
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

			public static readonly KeyMap US101 = new KeyMap ("US101", "AZSXDCFVGBHNJMK\xbcL\xbe\xbb\xbf\xba\xe2\xdd ", "1Q2W3E4R5T6Y7U8I9O0P\xbd\xc0\xde\xdb\xdc");

			public static readonly KeyMap JP106 = new KeyMap ("JP106", "AZSXDCFVGBHNJMK,L.;/:\\", "1Q2W3E4R5T6Y7U8I9O0P-@^");

			public KeyMap (string name, string lowKeys, string highKeys)
			{
				Name = name;
				LowKeys = lowKeys;
				HighKeys = highKeys;
			}

			public string Name { get; private set; }
			public string LowKeys { get; private set; }
			public string HighKeys { get; private set; }
		}

		readonly KeyMap [] available_keymaps = { KeyMap.US101, KeyMap.JP106 };

		KeyMap keymap = KeyMap.US101;

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
				note = (octave + (low ? 0 : 1)) * 12 - 5 + nid + transpose;
			else
				note = (octave + (low ? 0 : 1)) * 12 - 4 + nid + transpose;

			if (0 <= note && note <= 128) {
				if (down) {
					var mml = NoteNumberToName (note);
					int newPosition = mml_record_pad.CursorPosition + mml.Length;
					mml_record_pad.Text = mml_record_pad.Text.Insert (mml_record_pad.CursorPosition, mml);
					if (mml_record_pad.Text.Length - mml_record_pad.Text.LastIndexOf ('\n') > notepad_wrap_line_at) {
						mml_record_pad.Text += "\r\n";
						newPosition++;
						newPosition++;
					}
					mml_record_pad.CursorPosition = newPosition;
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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xwt;
using Commons.Music.Midi;
using Commons.Music.Midi.Mml;
using Commons.Music.Musicality;

namespace Xmmk
{
	public class MidiController : IDisposable
	{
		static IMidiOutput empty_output = MidiAccessManager.Empty
			.OpenOutputAsync (MidiAccessManager.Empty.Outputs.First ().Id).Result; 
		public IMidiOutput Output { get; private set; } = empty_output;
		public IMidiInput Input { get; private set; }
		public int Channel { get; set; } = 1;
		public int Program { get; private set; } = 0; // grand piano
		public int BankMsb { get; private set; } = 0;
		public int BankLsb { get; private set; } = 0;

		public IMidiAccess MidiAccess => MidiAccessManager.Default;
		
		public string CurrentDeviceId { get; set; }

		public MidiInstrumentMap CurrentInstrumentMap => MidiInstrumentMapOverride ?? MidiModuleDatabase.Default.Resolve (Output?.Details?.Name)?.Instrument?.Maps?.FirstOrDefault ();
		public MidiInstrumentMap CurrentDrumMap => MidiDrumMapOverride ?? MidiModuleDatabase.Default.Resolve (Output?.Details?.Name)?.Instrument?.DrumMaps?.FirstOrDefault ();

		public event EventHandler<NoteOnOffEventArgs> NoteOnOffReceived;
		
		public MidiInstrumentMap MidiInstrumentMapOverride { get; set; }
		public MidiInstrumentMap MidiDrumMapOverride { get; set; }
		
		MidiMachine machine = new MidiMachine ();

		public void Dispose ()
		{
			if (Input != null)
				Input.Dispose ();
			Input = null;
			if (Output != null)
				Output.Dispose ();
			Output = empty_output;
			DisableVirtualOutput ();
		}
		
		void Send (byte [] buffer, int offset, int length, long timestamp)
		{
			Output.Send (buffer, offset, length, timestamp);
			if (virtual_port != null)
				virtual_port.Send (buffer, offset, length, timestamp);
		}

		public void SetupMidiDevices ()
		{
			AppDomain.CurrentDomain.DomainUnload += delegate {
				Dispose ();
			};

			if (MidiAccessManager.Default.Outputs.Count () == 0) {
				MessageDialog.ShowError ("No MIDI device was found.");
				Output = empty_output;
			}
			else
				ChangeOutputDevice (MidiAccessManager.Default.Outputs.First ().Id);

			EnableVirtualOutput ();
		}

		public void ChangeInputDevice (string deviceID)
		{
			if (Input != null) {
				Input.Dispose ();
				Input = null;
			}

			Input = MidiAccessManager.Default.OpenInputAsync (deviceID).Result;
			Input.MessageReceived += (o, e) =>
				Send (e.Data, e.Start, e.Length, e.Timestamp);
		}

		public void ChangeOutputDevice (string deviceID)
		{
			if (Output != null) {
				Output.Dispose ();
				Output = empty_output;
			}

			Output = MidiAccessManager.Default.OpenOutputAsync (deviceID).Result;
			Send (new byte [] { (byte) (MidiEvent.Program + Channel), (byte) Program }, 0, 2, 0);

			CurrentDeviceId = deviceID;
		}

		public void ChangeProgram (int newProgram, byte bankMsb, byte bankLsb)
		{
			Program = newProgram;
			BankMsb = bankMsb;
			BankLsb = bankLsb;
			Send (new byte [] { (byte) (MidiEvent.CC + Channel), MidiCC.BankSelect, bankMsb }, 0, 3, 0);
			Send (new byte [] { (byte) (MidiEvent.CC + Channel), MidiCC.BankSelectLsb, bankLsb }, 0, 3, 0);
			Send (new byte [] { (byte) (MidiEvent.Program + Channel), (byte) Program }, 0, 2, 0);
		}

		public void NoteOnOff (byte note, byte velocity)
		{
			machine.Channels [Channel].NoteVelocity [note] = velocity;
			Send (new byte [] { (byte) (0x90 + Channel), note, velocity }, 0, 3, 0);
			if (NoteOnOffReceived != null)
				NoteOnOffReceived (this, new NoteOnOffEventArgs { Note = note, Velocity = velocity });
		}

		public class NoteOnOffEventArgs
		{
			public int Note { get; set; }
			public int Velocity { get; set; }
		}

		public void StopPlayer (int playerIndex)
		{
			Task.Run (() => DoStopPlayer (playerIndex));
		}

		void DoStopPlayer (int playerIndex)
		{
			if (players.Count > playerIndex)
				players [playerIndex].Stop ();
		}

		public void ExecuteMml (string mml, int playerIndex)
		{
			Task.Run (() => DoExecuteMml (mml, playerIndex));
		}

		void DoExecuteMml (string mml, int playerIndex)
		{
			try {
				var music = CompileMmlToSong (mml);
				StartNewSong (playerIndex, music);
			} catch (Exception ex) {
				Console.Error.WriteLine ("[error] " + ex);
			}
		}
		
		readonly List<MidiPlayer> players = new List<MidiPlayer> ();

		public event EventHandler<PlayerListingChangedEventArgs> MusicListingChanged;

		public class PlayerListingChangedEventArgs : EventArgs
		{
			public PlayerListingChangedEventArgs (int index, MidiMusic music)
			{
				Index = index;
				Music = music;
			}
			
			public int Index { get; private set; }
			public MidiMusic Music { get; private set; }
		}

		MidiMusic CompileMmlToSong (string mml)
		{
			mml = $"1 CH{Channel + 1} t200r1t120 {mml}";
			
			var compiler = new MmlCompiler ();
			var midiStream = new MemoryStream ();
			var source = new MmlInputSource ("", new StringReader (mml));
			compiler.Compile (false, Enumerable.Repeat (source, 1).ToArray (), null, midiStream, false);
			return MidiMusic.Read (new MemoryStream (midiStream.ToArray ()));
		}

		void StartNewSong (int playerIndex, MidiMusic music)
		{
			MidiPlayer mml_music_player = null;
		
			if (players.Count > playerIndex)
				players [playerIndex].Dispose ();
			
			mml_music_player = new MidiPlayer (music, Output);
			if (virtual_port != null)
			{
				var buffer = new byte[1024];
				mml_music_player.EventReceived += (evt) => {
					if (MidiEvent.FixedDataSize(evt.StatusByte) == 0) {
						buffer[0] = evt.StatusByte;
						if (buffer.Length < evt.ExtraDataLength - evt.ExtraDataOffset)
							buffer = new byte[evt.ExtraDataLength - evt.ExtraDataOffset + 1];
						Array.Copy (evt.ExtraData, evt.ExtraDataOffset, buffer, 1, evt.ExtraDataLength);
						virtual_port.Send (buffer, 0, evt.ExtraDataLength + 1, 0);
					} else {
						buffer[0] = evt.EventType;
						buffer[1] = evt.Msb;
						buffer[2] = evt.Lsb;
						virtual_port.Send (buffer, 0, 1 + MidiEvent.FixedDataSize (evt.StatusByte), 0);
					}
				};
			}

			mml_music_player.PlayAsync ();
			if (MusicListingChanged != null)
				MusicListingChanged (this, new PlayerListingChangedEventArgs (playerIndex, music));
			if (players.Count > playerIndex)
				players [playerIndex] = mml_music_player;
			else
				players.Add (mml_music_player);
		}

		IMidiOutput virtual_port;

		public bool EnableVirtualOutput ()
		{
			IMidiAccess2 m2 = MidiAccess as IMidiAccess2;
			if (m2 == null)
				return false;
			var pc = m2.ExtensionManager.GetInstance<MidiPortCreatorExtension> ();
			if (pc == null)
				return false;
			virtual_port = pc.CreateVirtualInputSender (new MidiPortCreatorExtension.PortCreatorContext { Manufacturer = "managed-midi project", ApplicationName = "Xmmk", PortName = "Xmmk Input Port"});
			return true;
		}

		public bool DisableVirtualOutput ()
		{
			if (virtual_port == null)
				return false;
			virtual_port.CloseAsync ();
			virtual_port = null;
			return true;
		}

		public IMidiOutput VirtualPort => virtual_port;

		public string GuessChords ()
		{
			var candidates = new List<string> ();
			var noteStates = machine.Channels [Channel].NoteVelocity;
			bool[] components = new bool [12];
			var lowestNote = 0;
			for (int i = 0; i < noteStates.Length; i++) {
				if (noteStates [i] == 0)
					continue;
				lowestNote = Math.Min (lowestNote, i);
				components [i % 12] = true;
			}
			foreach (var chord in Chord.KnownChords)
				if (chord.Matches (components))
					candidates.Add (chord.Name);

			return string.Join (", ", candidates);
		}
	}
}

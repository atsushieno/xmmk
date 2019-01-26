using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xwt;
using Commons.Music.Midi;
using Commons.Music.Midi.Mml;

namespace Xmmk
{
	public class MidiController
	{
		public IMidiOutput Output { get; private set; }
		public IMidiInput Input { get; private set; }
		public int Channel { get; set; } = 1;
		public int Program { get; private set; } = 0; // grand piano

		public IMidiAccess MidiAccess => MidiAccessManager.Default;

		public MidiInstrumentMap CurrentInstrumentMap => MidiInstrumentMapOverride ?? MidiModuleDatabase.Default.Resolve (Output.Details.Name)?.Instrument?.Maps?.FirstOrDefault ();
		public MidiInstrumentMap CurrentDrumMap => MidiDrumMapOverride ?? MidiModuleDatabase.Default.Resolve (Output.Details.Name)?.Instrument?.DrumMaps?.FirstOrDefault ();

		public event EventHandler ProgramChanged;
		public event EventHandler InputDeviceChanged;
		public event EventHandler OutputDeviceChanged;
		public event EventHandler<NoteEventArgs> NoteOnReceived;
		
		public MidiInstrumentMap MidiInstrumentMapOverride { get; set; }
		public MidiInstrumentMap MidiDrumMapOverride { get; set; }

		public void SetupMidiDevices ()
		{
			if (MidiAccessManager.Default.Outputs.Count () == 0) {
				MessageDialog.ShowError ("No MIDI device was found.");
				Application.Exit ();
				return;
			}

			AppDomain.CurrentDomain.DomainUnload += delegate {
				if (Input != null)
					Input.Dispose ();
				if (Output != null)
					Output.Dispose ();
			};

			ChangeOutputDevice (MidiAccessManager.Default.Outputs.First ().Id);
		}

		public void ChangeInputDevice (string deviceID)
		{
			if (Input != null) {
				Input.Dispose ();
				Input = null;
			}

			Input = MidiAccessManager.Default.OpenInputAsync (deviceID).Result;
			InputDeviceChanged (this, EventArgs.Empty);
		}

		public void ChangeOutputDevice (string deviceID)
		{
			if (Output != null) {
				Output.Dispose ();
				Output = null;
			}

			Output = MidiAccessManager.Default.OpenOutputAsync (deviceID).Result;
			Output.Send (new byte [] { (byte)(MidiEvent.Program + Channel), (byte)Program }, 0, 2, 0);

			if (OutputDeviceChanged != null)
				OutputDeviceChanged (this, EventArgs.Empty);
		}

		public void ChangeProgram (int newProgram, byte bankMsb, byte bankLsb)
		{
			Program = newProgram;
			Output.Send (new byte [] { (byte) (MidiEvent.CC + Channel), MidiCC.BankSelect, bankMsb }, 0, 3, 0);
			Output.Send (new byte [] { (byte) (MidiEvent.CC + Channel), MidiCC.BankSelectLsb, bankLsb }, 0, 3, 0);
			Output.Send (new byte [] { (byte) (MidiEvent.Program + Channel), (byte)Program }, 0, 2, 0);

			if (ProgramChanged != null)
				ProgramChanged (this, EventArgs.Empty);
		}

		public void NoteOn (byte note, byte velocity)
		{
			Output.Send (new byte [] { (byte)(0x90 + Channel), note, velocity }, 0, 3, 0);
			if (NoteOnReceived != null)
				NoteOnReceived (this, new NoteEventArgs { Note = note, Velocity = velocity });
		}

		public class NoteEventArgs
		{
			public int Note { get; set; }
			public int Velocity { get; set; }
		}

		public void ExecuteMml (string mml)
		{
			Task.Run (() => DoExecuteMml (mml));
		}

		void DoExecuteMml (string mml)
		{
			StartNewSong (CompileMmlToSong (mml));
		}
		
		MidiMusic CompileMmlToSong (string mml)
		{
			mml += $"0 CH{Channel + 1} {mml}";
			
			var compiler = new MmlCompiler ();
			var midiStream = new MemoryStream ();
			var source = new MmlInputSource ("", new StringReader (mml));
			compiler.Compile (false, Enumerable.Repeat (source, 1).ToArray (), null, midiStream, false);
			return MidiMusic.Read (new MemoryStream (midiStream.ToArray ()));
		}

		MidiPlayer mml_music_player;
		
		void StartNewSong (MidiMusic music)
		{
			if (mml_music_player != null)
				mml_music_player.Dispose ();
			mml_music_player = new MidiPlayer (music, Output);
			mml_music_player.PlayAsync ();
		}
	}
}

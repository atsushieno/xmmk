using System;
using System.Linq;
using Xwt;
using Commons.Music.Midi;

namespace Xmmk
{
	public class MidiController
	{
		public IMidiOutput Output { get; private set; }
		public IMidiInput Input { get; private set; }
		public int Channel { get; set; } = 1;
		public int Program { get; private set; } = 0; // grand piano

		public IMidiAccess MidiAccess => MidiAccessManager.Default;

		public MidiModuleDefinition CurrentOutputMidiModule => MidiModuleDatabase.Default.Resolve (Output.Details.Name);

		public event EventHandler ProgramChanged;
		public event EventHandler InputDeviceChanged;
		public event EventHandler OutputDeviceChanged;
		public event EventHandler<NoteEventArgs> NoteOnReceived;

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
	}
}

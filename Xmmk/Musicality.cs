using System;
using System.Linq;

namespace Commons.Music.Musicality
{
	public static class Notes
	{
		public const byte
			C = 0,
			CSharp = 1,
			DFlat = 1,
			D = 2,
			DSharp = 3,
			EFlat = 3,
			E = 4,
			F = 5,
			FSharp = 6,
			GFlat = 6,
			G = 7,
			GSharp =8,
			AFlat = 8,
			A = 9,
			ASharp = 10,
			BFlat = 10,
			B = 11;
		
		static readonly string [] note_names_major_ascii = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
		static readonly string [] note_names_minor_ascii = { "C", "Db", "D", "Eb", "E", "F", "Gb", "G", "Ab", "A", "Bb", "B" };
		static readonly string [] note_names_major = { "C", "C♯", "D", "D♯", "E", "F", "F♯", "G", "G♯", "A", "A♯", "B" };
		static readonly string [] note_names_minor = { "C", "D♭", "D", "E♭", "E", "F", "G♭", "G", "A♭", "A", "B♭", "B" };
		
		public static string GetName (byte note, bool onlyAscii = false, bool isMinor = false)
		{
			var arr = onlyAscii ?
				(isMinor ? note_names_minor_ascii : note_names_major_ascii) :
				(isMinor ? note_names_minor : note_names_major);
			return arr [note % 12];
		}
	}
	
	public static class Intervals
	{
		public const int PerfectUnison = 0,
			Minor2nd = 1,
			Major2nd = 2,
			Minor3rd = 3,
			Major3rd = 4,
			Perfect4th = 5,
			Perfect5th = 7,
			Minor6th = 8,
			Major6th = 9,
			Minor7th = 10,
			Major7th = 11,
			PerfectOctave = 12,
			
			Diminished2nd = 0,
			AugmentedUnison = 1,
			Diminished3rd = 2,
			Augmented2nd = 3,
			Diminished4th = 4,
			Augmented3rd = 5,
			Diminished5th = 6,
			Augmented4th = 6,
			Diminished6th = 7,
			Augmented5th = 8,
			Diminished7th = 9,
			Augmented6th = 10,
			DiminishedOctave = 11,
			Augmented7th = 12;
	}
	
	public static class ScaleDegrees
	{
		public static readonly byte []
			Major = { 0, 2, 4, 5, 7, 9, 11 },
			NaturalMinor = { 0, 2, 3, 5, 7, 8, 10 },
			HarmonicMinor = { 0, 2, 3, 5, 7, 8, 11 },
			MelodicMinor = { 0, 2, 3, 5, 7, 9, 11 };
	}
	
	public class PitchClass
	{
		public enum FifthCategory
		{
			Perfect,
			Augmented,
			Diminished,
		}

		public enum SeventhCategory
		{
			None,
			Major,
			Minor,
			Diminished,
		}
		
		public PitchClass (string shortName, string name, byte [] intervals)
		{
			ShortName = shortName;
			Name = name;
			Intervals = intervals;
		}
		
		public string ShortName { get; private set; }
		public string Name { get; private set; }
		public byte [] Intervals { get; private set; }

		public bool IsThirdMinor => Intervals [1] == 3;

		public FifthCategory Fifth =>
			Intervals [2] == 7 ? FifthCategory.Perfect :
			Intervals [2] == 6 ? FifthCategory.Diminished :
			FifthCategory.Augmented;

		public SeventhCategory Seventh =>
			Intervals.Length < 4 ? SeventhCategory.None :
			Intervals [3] == 11 ? SeventhCategory.Major :
			Intervals [3] == 10 ? SeventhCategory.Minor :
			SeventhCategory.Diminished;
		
		public static readonly PitchClass
			MajorTriad = new PitchClass ("", "maj", new byte [] { 0 , 4 , 7  }),
			MinorTriad = new PitchClass ("m", "min", new byte [] { 0 , 3 , 7  }),
			AugmentedTriad = new PitchClass ("+", "aug", new byte [] { 0 , 4 , 8  }),
			DimishedTriad = new PitchClass ("°", "dim", new byte [] { 0 , 3 , 6  }),
			
			Major6th = new PitchClass ("6", "maj6", new byte [] { 0, 4, 7, 9 }),
			Minor6th = new PitchClass ("m6", "min6", new byte [] { 0, 3, 7, 9 }),
			
			Diminished7th = new PitchClass ("°7", "dim7", new byte [] { 0 , 3 , 6 , 9  }),
			HalfDiminished7th = new PitchClass ("CØ7", "cmin7dim5", new byte [] { 0 , 3 , 6 , 10  }),
			MinorMajor7th = new PitchClass ("mM7", "minmaj7", new byte [] { 0 , 3 , 7 , 11  }),
			Augmented7th = new PitchClass ("+7", "aug7", new byte [] { 0 , 4 , 8 , 10  }),
			Dominant7th = new PitchClass ("7", "7", new byte [] { 0 , 4 , 7 , 10  }),
			Major7th = new PitchClass ("M7", "maj7", new byte [] { 0 , 4 , 7 , 11  }),
			AugmentedMajor7th = new PitchClass ("+M7", "augmaj7", new byte [] { 0 , 4 , 8 , 11  }),

			Dominant7thFlatted5th = new PitchClass ("7b5", "7dim5", new byte [] { 0 , 4 , 6 , 10  }),
			
			MajorAdd9th = new PitchClass ("add9", "add9", new byte [] { 0, 2, 4, 7 }),
			MinorAdd9th = new PitchClass ("madd9", "minadd9", new byte [] { 0, 2, 3, 7 }),
			
			/*
			Sus4 = new PitchClass ("sus4", "sus4", new byte [] { 0, 5, 7 })
			Blah7Sus4...
			*/
			
			/*
			Dominant9th = new PitchClass ("M9", "maj9", new byte [] { 0, 4, 7, 11, 14 }),
			Dominant11th = new PitchClass ("11", "maj9", new byte [] { 0, 4, 7, 11, 14, 17 }),
			Dominant13th = new PitchClass ("M13", "min", new byte [] { 0, 4, 7, 11, 14, 17, 21 });
			*/
			__dummy__ = null;
	}
	
	public class Chord
	{
		static readonly PitchClass [] all_pitch_classes =
			typeof (PitchClass).GetFields ()
			.Where (f => f.FieldType == typeof (PitchClass))
			.Select (f => f.GetValue (null))
			.Cast<PitchClass> ().ToArray ();

		public readonly string Name;
		public readonly PitchClass PitchClass;
		public readonly byte Root;
		
		Chord (string name, byte root, PitchClass pitchClass)
		{
			Name = name;
			Root = root;
			PitchClass = pitchClass;
		}
		
		public static readonly Chord [] known_chords = Enumerable.Range (Notes.C, Notes.B)
			.Cast<byte> ()
			.SelectMany (root => all_pitch_classes.Select (pc => new Chord (Notes.GetName (root) + pc.ShortName, root, pc)))
			.ToArray ();
	}
}

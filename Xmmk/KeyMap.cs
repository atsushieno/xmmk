namespace Xmmk
{
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
}
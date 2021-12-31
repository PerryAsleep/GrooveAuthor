using System.Collections.Generic;

namespace StepManiaEditor
{
	class Utils
	{
		public static readonly Dictionary<int, string> ArrowTextureBySubdivision;

		static Utils()
		{
			ArrowTextureBySubdivision = new Dictionary<int, string>
			{
				{4, "1_4"},
				{8, "1_8"},
				{12, "1_12"},
				{16, "1_16"},
				{24, "1_24"},
				{32, "1_32"},
				{48, "1_48"},
				{64, "1_64"},
			};
		}

		public static string GetArrowTextureId(int measureSubDivision)
		{
			if (measureSubDivision == 1 || measureSubDivision == 2)
				measureSubDivision = 4;
			if (ArrowTextureBySubdivision.ContainsKey(measureSubDivision))
				return ArrowTextureBySubdivision[measureSubDivision];
			return ArrowTextureBySubdivision[64];
		}
	}
}

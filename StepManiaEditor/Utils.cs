using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using ImGuiNET;

namespace StepManiaEditor
{
	class Utils
	{
		public static readonly Dictionary<int, string> ArrowTextureBySubdivision;

		public const int DefaultArrowWidth = 128;
		public const int DefaultHoldCapHeight = 64;
		public const int DefaultHoldSegmentHeight = 64;

		private static readonly Dictionary<Type, string[]> EnumStringsCache = new Dictionary<Type, string[]>();

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
		
		public static void ComboFromEnum<T>(string name, ref T enumValue) where T : Enum
		{
			var typeOfT = typeof(T);
			if (!EnumStringsCache.ContainsKey(typeOfT))
			{
				var enumValues = Enum.GetValues(typeOfT);
				var numEnumValues = enumValues.Length;
				var enumStrings = new string[numEnumValues];
				for (var i = 0; i < numEnumValues; i++)
					enumStrings[i] = Regex.Replace(enumValues.GetValue(i).ToString(), "((?<!^)([A-Z][a-z]|(?<=[a-z])[A-Z]))", " $1").Trim();
				EnumStringsCache[typeOfT] = enumStrings;
			}

			var strings = EnumStringsCache[typeOfT];
			var intValue = (int)(object)enumValue;
			ImGui.Combo(name, ref intValue, strings, strings.Length);
			enumValue = (T)(object)intValue;
		}
	}
}

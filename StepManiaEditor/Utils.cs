using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Fumen;
using Fumen.Converters;
using ImGuiNET;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace StepManiaEditor
{
	class Utils
	{
		// TODO: Rename / Reorganize. Currently dumping a lot of rendering-related constants in here.

		public static readonly Dictionary<int, string> ArrowTextureByBeatSubdivision;

		private static readonly string[] ArrowTextureByRow;
		private static readonly uint[] ArrowColorABGRByRow;
		private static readonly ushort[] ArrowColorBGR565ByRow;
		private static readonly uint MineColorABGR;
		private static readonly ushort MineColorRBGR565;
		private static readonly uint HoldColorABGR;
		private static readonly ushort HoldColorRBGR565;
		private static readonly uint RollColorABGR;
		private static readonly ushort RollColorRBGR565;

		public const int DefaultArrowWidth = 128;
		public const int DefaultHoldCapHeight = 64;
		public const int DefaultHoldSegmentHeight = 64;

		public const float BeatMarkerScaleToStartingFading = 0.15f;
		public const float BeatMarkerMinScale = 0.10f;
		public const float MeasureMarkerScaleToStartingFading = 0.10f;
		public const float MeasureMarkerMinScale = 0.05f;
		public const float MeasureNumberScaleToStartFading = 0.20f;
		public const float MeasureNumberMinScale = 0.10f;

		public const int MaxMarkersToDraw = 256;
		public const int MaxEventsToDraw = 2048;

		public const int MiniMapMaxNotesToDraw = 6144;
		public const int MiniMapYPaddingFromTop = 52;		// This takes into account a 20 pixel padding for the main menu bar.
		public const int MiniMapYPaddingFromBottom = 32;
		public const int MiniMapXPadding = 32;

		public const string TextureIdReceptor = "receptor";
		public const string TextureIdReceptorFlash = "receptor_flash";
		public const string TextureIdReceptorGlow = "receptor_glow";
		public const string TextureIdHoldActive = "hold_active";
		public const string TextureIdHoldActiveCap = "hold_active_cap";
		public const string TextureIdHoldInactive = "hold_inactive";
		public const string TextureIdHoldInactiveCap = "hold_inactive_cap";
		public const string TextureIdRollActive = "roll_active";
		public const string TextureIdRollActiveCap = "roll_active_cap";
		public const string TextureIdRollInactive = "roll_inactive";
		public const string TextureIdRollInactiveCap = "roll_inactive_cap";
		public const string TextureIdMine = "mine";

		public const string TextureIdMeasureMarker = "measure_marker";
		public const string TextureIdBeatMarker = "beat_marker";

		private static readonly Dictionary<Type, string[]> EnumStringsCacheByType = new Dictionary<Type, string[]>();
		private static readonly Dictionary<string, string[]> EnumStringsCacheByCustomKey = new Dictionary<string, string[]>();

		public enum HorizontalAlignment
		{
			Left,
			Center,
			Right
		}

		public enum VerticalAlignment
		{
			Top,
			Center,
			Bottom
		}

		static Utils()
		{
			ArrowTextureByBeatSubdivision = new Dictionary<int, string>
			{
				{1, "1_4"},
				{2, "1_8"},
				{3, "1_12"},
				{4, "1_16"},
				{6, "1_24"},
				{8, "1_32"},
				{12, "1_48"},
				{16, "1_64"},
			};

			ArrowTextureByRow = new string[SMCommon.MaxValidDenominator];
			for (var i = 0; i < SMCommon.MaxValidDenominator; i++)
			{
				var key = new Fraction(i, SMCommon.MaxValidDenominator).Reduce().Denominator;
				if (!ArrowTextureByBeatSubdivision.ContainsKey(key))
					key = 16;
				ArrowTextureByRow[i] = ArrowTextureByBeatSubdivision[key];
			}

			var arrowColorABGRBySubdivision = new Dictionary<int, uint>
			{
				{1, 0xFF0000FF},	// Red
				{2, 0xFFFF0000},	// Blue
				{3, 0xFF00FF00},	// Green
				{4, 0xFF00FFFF},	// Yellow
				{6, 0xFFFF0080},	// Purple
				{8, 0xFFFFFF00},	// Cyan
				{12, 0xFFFF80FF},	// Pink
				{16, 0xFF99bf99},	// Pale Grey Green
			};
			ArrowColorABGRByRow = new uint[SMCommon.MaxValidDenominator];
			ArrowColorBGR565ByRow = new ushort[SMCommon.MaxValidDenominator];
			for (var i = 0; i < SMCommon.MaxValidDenominator; i++)
			{
				var key = new Fraction(i, SMCommon.MaxValidDenominator).Reduce().Denominator;
				
				if (!arrowColorABGRBySubdivision.ContainsKey(key))
					key = 16;
				ArrowColorABGRByRow[i] = arrowColorABGRBySubdivision[key];
				ArrowColorBGR565ByRow[i] = ToBGR565(ArrowColorABGRByRow[i]);
			}

			MineColorABGR = 0xFFDCDCDC; // Light Grey
			MineColorRBGR565 = ToBGR565(MineColorABGR);
			HoldColorABGR = 0xFF98B476; // Light Blue
			HoldColorRBGR565 = ToBGR565(HoldColorABGR);
			RollColorABGR = 0xFFAE8289; // Light Green
			RollColorRBGR565 = ToBGR565(RollColorABGR);
		}

		public static string GetArrowTextureId(int integerPosition)
		{
			return ArrowTextureByRow[integerPosition % SMCommon.MaxValidDenominator];
		}

		public static uint GetArrowColorABGR(int integerPosition)
		{
			return ArrowColorABGRByRow[integerPosition % SMCommon.MaxValidDenominator];
		}

		public static ushort GetArrowColorBGR565(int integerPosition)
		{
			return ArrowColorBGR565ByRow[integerPosition % SMCommon.MaxValidDenominator];
		}

		public static uint GetMineColorABGR()
		{
			return MineColorABGR;
		}

		public static ushort GetMineColorBGR565()
		{
			return MineColorRBGR565;
		}

		public static uint GetHoldColorABGR()
		{
			return HoldColorABGR;
		}

		public static ushort GetHoldColorBGR565()
		{
			return HoldColorRBGR565;
		}

		public static uint GetRollColorABGR()
		{
			return RollColorABGR;
		}

		public static ushort GetRollColorBGR565()
		{
			return RollColorRBGR565;
		}

		public static uint ColorABGRInterpolate(uint startColor, uint endColor, float endPercent)
		{
			var startPercent = 1.0f - endPercent;
			return (uint)((startColor & 0xFF) * startPercent + (endColor & 0xFF) * endPercent)
			       | ((uint)(((startColor >> 8) & 0xFF) * startPercent + ((endColor >> 8) & 0xFF) * endPercent) << 8)
			       | ((uint)(((startColor >> 16) & 0xFF) * startPercent + ((endColor >> 16) & 0xFF) * endPercent) << 16)
			       | ((uint)(((startColor >> 24) & 0xFF) * startPercent + ((endColor >> 24) & 0xFF) * endPercent) << 24);
		}

		public static uint ColorABGRInterpolateBGR(uint startColor, uint endColor, float endPercent)
		{
			var startPercent = 1.0f - endPercent;
			return (uint)((startColor & 0xFF) * startPercent + (endColor & 0xFF) * endPercent)
			       | ((uint)(((startColor >> 8) & 0xFF) * startPercent + ((endColor >> 8) & 0xFF) * endPercent) << 8)
			       | ((uint)(((startColor >> 16) & 0xFF) * startPercent + ((endColor >> 16) & 0xFF) * endPercent) << 16)
			       | (endColor & 0xFF000000);
		}

		public static ushort ToBGR565(float r, float g, float b)
		{
			return (ushort)(((ushort)(r * 31) << 11) + ((ushort)(g * 63) << 5) + (ushort)(b * 31));
		}

		public static ushort ToBGR565(Color c)
		{
			return ToBGR565((float)c.R / byte.MaxValue, (float)c.G / byte.MaxValue, (float)c.B / byte.MaxValue);
		}

		public static ushort ToBGR565(uint ABGR)
		{
			return ToBGR565(
				(byte)((ABGR | 0x00FF0000) >> 24) / (float)byte.MaxValue,
				(byte)((ABGR | 0x0000FF00) >> 16) / (float)byte.MaxValue,
				(byte)((ABGR | 0x000000FF) >> 8) / (float)byte.MaxValue);
		}

		public static bool ComboFromEnum<T>(string name, ref T enumValue) where T : Enum
		{
			var typeOfT = typeof(T);
			if (!EnumStringsCacheByType.ContainsKey(typeOfT))
			{
				var enumValues = Enum.GetValues(typeOfT);
				var numEnumValues = enumValues.Length;
				var enumStrings = new string[numEnumValues];
				for (var i = 0; i < numEnumValues; i++)
					enumStrings[i] = FormatEnumForUI(enumValues.GetValue(i).ToString());
				EnumStringsCacheByType[typeOfT] = enumStrings;
			}

			var strings = EnumStringsCacheByType[typeOfT];
			var intValue = (int)(object)enumValue;
			var result = ImGui.Combo(name, ref intValue, strings, strings.Length);
			enumValue = (T)(object)intValue;
			return result;
		}

		public static bool ComboFromEnum<T>(string name, ref T enumValue, T[] allowedValues, string cacheKey) where T : Enum
		{
			var typeOfT = typeof(T);
			if (!EnumStringsCacheByCustomKey.ContainsKey(cacheKey))
			{
				var numEnumValues = allowedValues.Length;
				var enumStrings = new string[numEnumValues];
				for (var i = 0; i < numEnumValues; i++)
					enumStrings[i] = FormatEnumForUI(allowedValues[i].ToString());
				EnumStringsCacheByCustomKey[cacheKey] = enumStrings;
			}

			var strings = EnumStringsCacheByCustomKey[cacheKey];
			var intValue = (int)(object)enumValue;
			var result = ImGui.Combo(name, ref intValue, strings, strings.Length);
			enumValue = (T)(object)intValue;
			return result;
		}

		private static string FormatEnumForUI(string enumValue)
		{
			return Regex.Replace(enumValue, "((?<!^)([A-Z][a-z]|(?<=[a-z])[A-Z]))", " $1").Trim();
		}

		public static void HelpMarker(string text)
		{
			ImGui.TextDisabled("(?)");
			if (ImGui.IsItemHovered())
			{
				ImGui.BeginTooltip();
				ImGui.PushTextWrapPos(ImGui.GetFontSize() * 80.0f);
				ImGui.TextUnformatted(text);
				ImGui.PopTextWrapPos();
				ImGui.EndTooltip();
			}
		}

		public static bool SliderUInt(string text, ref uint value, uint min, uint max, string format, ImGuiSliderFlags flags)
		{
			int iValue = (int)value;
			var ret = ImGui.SliderInt(text, ref iValue, (int)min, (int)max, format, flags);
			value = (uint)iValue;
			return ret;
		}

		public static Vector2 GetDrawPos(
			SpriteFont font,
			string text,
			Vector2 anchorPos,
			float scale,
			HorizontalAlignment hAlign = HorizontalAlignment.Left,
			VerticalAlignment vAlign = VerticalAlignment.Top)
		{
			var x = anchorPos.X;
			var y = anchorPos.Y;
			var size = font.MeasureString(text);
			switch (hAlign)
			{
				case HorizontalAlignment.Center:
					x -= size.X * 0.5f * scale;
					break;
				case HorizontalAlignment.Right:
					x -= size.X * scale;
					break;
			}
			switch (vAlign)
			{
				case VerticalAlignment.Center:
					y -= size.Y * 0.5f * scale;
					break;
				case VerticalAlignment.Bottom:
					y -= size.Y * scale;
					break;
			}
			return new Vector2(x, y);
		}
	}
}

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using ImGuiNET;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace StepManiaEditor
{
	class Utils
	{
		// TODO: Rename / Reorganize. Currently dumping a lot of rendering-related constants in here.

		public static readonly Dictionary<int, string> ArrowTextureBySubdivision;

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

		private static readonly Dictionary<Type, string[]> EnumStringsCache = new Dictionary<Type, string[]>();

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

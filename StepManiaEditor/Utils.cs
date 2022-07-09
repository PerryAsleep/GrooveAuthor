using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Fumen;
using Fumen.Converters;
using ImGuiNET;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace StepManiaEditor
{
	public class Utils
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

		public const uint UITempoColorABGR = 0x8A297A79;			// yellow
		public const uint UITimeSignatureColorABGR = 0x8A297A29;	// green
		public const uint UIStopColorABGR = 0x8A29297A;				// red
		public const uint UIDelayColorABGR = 0x8A295E7A;			// light orange
		public const uint UIWarpColorABGR = 0x8A7A7929;				// cyan
		public const uint UIScrollsColorABGR = 0x8A7A2929;			// blue
		public const uint UISpeedsColorABGR = 0x8A7A294D;			// purple

		public const uint UITicksColorABGR = 0x8A295E7A;			// orange
		public const uint UIMultipliersColorABGR = 0x8A297A63;		// lime
		public const uint UIFakesColorABGR = 0x8A29467A;			// dark orange
		public const uint UILabelColorABGR = 0x8A68297A;			// pink

		public const int DefaultArrowWidth = 128;
		public const int DefaultHoldCapHeight = 64;
		public const int DefaultHoldSegmentHeight = 64;

		public const int WaveFormTextureWidth = DefaultArrowWidth * 8;

		public const float BeatMarkerScaleToStartingFading = 0.15f;
		public const float BeatMarkerMinScale = 0.10f;
		public const float MeasureMarkerScaleToStartingFading = 0.10f;
		public const float MeasureMarkerMinScale = 0.05f;
		public const float MeasureNumberScaleToStartFading = 0.20f;
		public const float MeasureNumberMinScale = 0.10f;

		public const float HelpWidth = 18.0f;
		public const int CloseWidth = 18;

		public const int BackgroundWidth = 640;
		public const int BackgroundHeight = 480;
		public const int BannerWidth = 418;
		public const int BannerHeight = 164;
		public const int CDTitleWidth = 164;
		public const int CDTitleHeight = 164;

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

		public static readonly string[] ExpectedAudioFormats = { "mp3", "oga", "ogg", "wav" };
		public static readonly string[] ExpectedImageFormats = { "bmp", "gif", "jpeg", "jpg", "png", "tif", "tiff", "webp" };
		public static readonly string[] ExpectedVideoFormats = { "avi", "f4v", "flv", "mkv", "mp4", "mpeg", "mpg", "mov", "ogv", "webm", "wmv" };
		public static readonly string[] ExpectedLyricsFormats = { "lrc" };

		private static readonly Dictionary<Type, string[]> EnumStringsCacheByType = new Dictionary<Type, string[]>();
		private static readonly Dictionary<string, string[]> EnumStringsCacheByCustomKey = new Dictionary<string, string[]>();

		private static List<bool> EnabledStack = new List<bool>();

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

		public enum TextureLayoutMode
		{
			/// <summary>
			/// Draw the texture at its original size, centered in the destination area. If the texture is larger
			/// than the destination area then it will be cropped as needed to fit. If it is smaller then it will
			/// be rendered smaller.
			/// </summary>
			OriginalSize,

			/// <summary>
			/// The texture will fill the destination area exactly. It will shrink or grow as needed and the aspect
			/// ratio will change to match the destination.
			/// </summary>
			Stretch,

			/// <summary>
			/// Maintain the texture's original aspect ratio and fill the destination area. If the texture aspect
			/// ratio does not match the destination area's aspect ratio, then the texture will be cropped.
			/// </summary>
			Fill,

			/// <summary>
			/// Letterbox or pillarbox as needed such that texture's original aspect ratio is maintained and it fills
			/// the destination area as much as possible.
			/// </summary>
			Box
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

		#region ImGui Helpers

		[DllImport("msvcrt.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
		private static extern int _snwprintf_s([MarshalAs(UnmanagedType.LPWStr)] StringBuilder sb, IntPtr sizeOfBuffer, IntPtr count, string format, int p);

		public static string FormatImGuiInt(string fmt, int i, int sizeOfBuffer = 64, int count = 32)
		{
			StringBuilder sb = new StringBuilder(sizeOfBuffer);
			_snwprintf_s(sb, (IntPtr)sizeOfBuffer, (IntPtr)count, fmt, i);
			return sb.ToString();
		}

		[DllImport("msvcrt.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
		private static extern int _snwprintf_s([MarshalAs(UnmanagedType.LPWStr)] StringBuilder sb, IntPtr sizeOfBuffer, IntPtr count, string format, double p);

		public static string FormatImGuiDouble(string fmt, double d, int sizeOfBuffer = 64, int count = 32)
		{
			StringBuilder sb = new StringBuilder(sizeOfBuffer);
			_snwprintf_s(sb, (IntPtr)sizeOfBuffer, (IntPtr)count, fmt, d);
			return sb.ToString();
		}

		public static bool ComboFromEnum<T>(string name, ref T enumValue) where T : Enum
		{
			var strings = GetCachedEnumStrings<T>();
			var intValue = (int)(object)enumValue;
			var result = ImGui.Combo(name, ref intValue, strings, strings.Length);
			enumValue = (T)(object)intValue;
			return result;
		}

		public static bool ComboFromEnum<T>(string name, ref T enumValue, T[] allowedValues, string cacheKey) where T : Enum
		{
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

		/// <summary>
		/// Draws a TreeNode in ImGui with Selectable elements for each value in the
		/// Enum specified by the given type parameter T.
		/// </summary>
		/// <typeparam name="T">Enum type for drawing elements.</typeparam>
		/// <param name="label">Label for the TreeNode.</param>
		/// <param name="values">
		/// Array of booleans represented the selected state of each value in the Enum.
		/// Assumed to be the same length as the Enum type param.
		/// </param>
		/// <returns>
		/// Tuple. First value is whether any Selectable was changed.
		/// Second value is an array of bools represented the previous state. This is only
		/// set if the state changes. This is meant is a convenience for undo/redo so the
		/// caller can avoid creating a before state unnecessarily.
		/// </returns>
		public static (bool, bool[]) SelectableTree<T>(string label, ref bool[] values) where T : Enum
		{
			var strings = GetCachedEnumStrings<T>();
			var index = 0;
			var ret = false;
			bool[] originalValues = null;
			if (ImGui.TreeNode(label))
			{
				foreach (var enumString in strings)
				{
					if (ImGui.Selectable(enumString, values[index]))
					{
						if (!ret)
						{
							originalValues = (bool[])values.Clone();
						}
						ret = true;

						// Unset other selections if not holding control.
						if (!ImGui.GetIO().KeyCtrl)
						{
							for (var i = 0; i < values.Length; i++)
							{
								values[i] = false;
							}
						}

						// Toggle selected element.
						values[index] = !values[index];
					}

					index++;
				}

				ImGui.TreePop();
			}

			return (ret, originalValues);
		}

		private static string[] GetCachedEnumStrings<T>()
		{
			var typeOfT = typeof(T);
			if (EnumStringsCacheByType.ContainsKey(typeOfT))
				return EnumStringsCacheByType[typeOfT];
			
			var enumValues = Enum.GetValues(typeOfT);
			var numEnumValues = enumValues.Length;
			var enumStrings = new string[numEnumValues];
			for (var i = 0; i < numEnumValues; i++)
				enumStrings[i] = FormatEnumForUI(enumValues.GetValue(i).ToString());
			EnumStringsCacheByType[typeOfT] = enumStrings;
			return EnumStringsCacheByType[typeOfT];
		}

		/// <summary>
		/// Formats an enum string value for by returning a string value
		/// with space-separated capitalized words.
		/// </summary>
		/// <param name="enumValue">String representation of enum value.</param>
		/// <returns>Formatting string representation of enum value.</returns>
		private static string FormatEnumForUI(string enumValue)
		{
			StringBuilder sb = new StringBuilder(enumValue.Length * 2);
			var capitalizeNext = true;
			var previousWasCapital = false;
			var first = true;
			foreach (var character in enumValue)
			{
				// Treat dashes as spaces. Capitalize the letter after a space.
				if (character == '_' || character == '-')
				{
					sb.Append(' ');
					capitalizeNext = true;
					first = false;
					previousWasCapital = false;
					continue;
				}

				// Lowercase character. Use this character unless we are supposed to
				// capitalize it due to it following a space.
				if (char.IsLower(character))
				{
					if (capitalizeNext)
					{
						sb.Append(char.ToUpper(character));
						previousWasCapital = true;
					}
					else
					{
						sb.Append(character);
						previousWasCapital = false;
					}
				}

				// Uppercase character. Prepend a space, unless this followed another
				// capitalized character, in which case lowercase it. This is to support
				// formatting strings like "YES" to "Yes".
				else if (char.IsUpper(character))
				{
					if (!first && !previousWasCapital)
						sb.Append(' ');
					if (previousWasCapital)
						sb.Append(char.ToLower(character));
					else
						sb.Append(character);
					previousWasCapital = true;
				}

				// For any other character type, just record it as is.
				else
				{
					sb.Append(character);
					previousWasCapital = false;
				}

				first = false;
				capitalizeNext = false;
			}

			return sb.ToString();
		}

		public static void HelpMarker(string text)
		{
			PushEnabled();
			Text("(?)", HelpWidth, true);
			if (ImGui.IsItemHovered())
			{
				ImGui.BeginTooltip();
				ImGui.PushTextWrapPos(ImGui.GetFontSize() * 80.0f);
				ImGui.TextUnformatted(text);
				ImGui.PopTextWrapPos();
				ImGui.EndTooltip();
			}
			PopEnabled();
		}

		/// <summary>
		/// Draws an ImGUi Text element with a specified width.
		/// </summary>
		/// <param name="text">Text to display in the ImGUi Text element.</param>
		/// <param name="width">Width of the element.</param>
		/// <param name="disabled">Whether or not the element should be disabled.</param>
		public static void Text(string text, float width, bool disabled = false)
		{
			// Record original spacing and padding so we can edit it and restore it.
			var originalItemSpacingX = ImGui.GetStyle().ItemSpacing.X;
			var originalItemSpacingY = ImGui.GetStyle().ItemSpacing.Y;
			var originalFramePaddingX = ImGui.GetStyle().FramePadding.X;
			var originalFramePaddingY = ImGui.GetStyle().FramePadding.Y;

			// Set the padding and spacing so we can draw a table with precise dimensions.
			ImGui.GetStyle().ItemSpacing.X = 0;
			ImGui.GetStyle().ItemSpacing.Y = 0;
			ImGui.GetStyle().FramePadding.X = 0;
			ImGui.GetStyle().FramePadding.Y = 0;

			// Wrap the text in Table in order to control the size precisely.
			if (ImGui.BeginTable(text, 1, ImGuiTableFlags.None, new System.Numerics.Vector2(width, 0), width))
			{
				ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthStretch, 100.0f);
				ImGui.TableNextRow();
				ImGui.TableSetColumnIndex(0);
				if (disabled)
					ImGui.TextDisabled(text);
				else
					ImGui.Text(text);
				ImGui.EndTable();
			}

			// Restore the padding and spacing values.
			ImGui.GetStyle().FramePadding.X = originalFramePaddingX;
			ImGui.GetStyle().FramePadding.Y = originalFramePaddingY;
			ImGui.GetStyle().ItemSpacing.X = originalItemSpacingX;
			ImGui.GetStyle().ItemSpacing.Y = originalItemSpacingY;
		}

		public static bool SliderUInt(string text, ref uint value, uint min, uint max, string format, ImGuiSliderFlags flags)
		{
			int iValue = (int)value;
			var ret = ImGui.SliderInt(text, ref iValue, (int)min, (int)max, format, flags);
			value = (uint)iValue;
			if (value < min)
				value = min;
			if (value > max)
				value = max;
			return ret;
		}

		public static bool InputInt(string label, ref int value, int min = int.MinValue, int max = int.MaxValue)
		{
			var ret = ImGui.InputInt(label, ref value);
			if (ret)
			{
				value = Math.Max(min, value);
				value = Math.Min(max, value);
			}
			return ret;
		}

		public static bool DragInt(
			ref int value,
			string label,
			float speed,
			string format,
			int min = int.MinValue,
			int max = int.MaxValue)
		{
			var ret = ImGui.DragInt(label, ref value, speed, min, max, format);

			if (ret)
			{
				value = Math.Max(min, value);
				value = Math.Min(max, value);
			}

			return ret;
		}

		public static unsafe bool DragDouble(
			ref double value,
			string label,
			float speed,
			string format,
			double min = double.MinValue,
			double max = double.MaxValue)
		{
			var ret = false;
			fixed (double* p = &value)
			{
				IntPtr pData = new IntPtr(p);
				IntPtr pMin = new IntPtr(&min);
				IntPtr pMax = new IntPtr(&max);
				ret = ImGui.DragScalar(label, ImGuiDataType.Double, pData, speed, pMin, pMax, format);
			}

			if (ret)
			{
				value = Math.Max(min, value);
				value = Math.Min(max, value);
			}

			return ret;
		}

		public static unsafe bool DragDouble(ref double value, string label)
		{
			var ret = false;
			fixed (double* p = &value)
			{
				IntPtr pData = new IntPtr(p);
				ret = ImGui.DragScalar(label, ImGuiDataType.Double, pData);
			}
			return ret;
		}

		public static void PushEnabled()
		{
			var wasEnabled = EnabledStack.Count <= 0 || EnabledStack[^1];
			EnabledStack.Add(true);
			if (!wasEnabled)
				ImGui.EndDisabled();
		}

		public static void PushDisabled()
		{
			var wasEnabled = EnabledStack.Count <= 0 || EnabledStack[^1];
			EnabledStack.Add(false);
			if (wasEnabled)
				ImGui.BeginDisabled();
		}

		public static void PopEnabled()
		{
			Debug.Assert(EnabledStack.Count >= 0 && EnabledStack[^1]);
			PopEnabledOrDisabled();
		}

		public static void PopDisabled()
		{
			Debug.Assert(EnabledStack.Count >= 0 && !EnabledStack[^1]);
			PopEnabledOrDisabled();
		}

		private static void PopEnabledOrDisabled()
		{
			var wasEnabled = EnabledStack.Count <= 0 || EnabledStack[^1];
			EnabledStack.RemoveAt(EnabledStack.Count - 1);
			var isEnabled = EnabledStack.Count <= 0 || EnabledStack[^1];
			if (isEnabled && !wasEnabled)
				ImGui.EndDisabled();
			if (!isEnabled && wasEnabled)
				ImGui.BeginDisabled();
		}

		public static void DrawImage(
			string id,
			IntPtr textureImGui,
			Texture2D textureMonogame,
			uint width,
			uint height,
			TextureLayoutMode mode)
		{
			DrawImageInternal(id, textureImGui, textureMonogame, width, height, mode, false);
		}

		public static bool DrawButton(
			string id,
			IntPtr textureImGui,
			Texture2D textureMonogame,
			uint width,
			uint height,
			TextureLayoutMode mode)
		{
			return DrawImageInternal(id, textureImGui, textureMonogame, width, height, mode, true);
		}

		private static bool DrawImageInternal(
			string id,
			IntPtr textureImGui,
			Texture2D textureMonogame,
			uint width,
			uint height,
			TextureLayoutMode mode,
			bool button)
		{
			var result = false;

			// Record original spacing and padding so we can edit it and restore it.
			var originalItemSpacingX = ImGui.GetStyle().ItemSpacing.X;
			var originalItemSpacingY = ImGui.GetStyle().ItemSpacing.Y;
			var originalFramePaddingX = ImGui.GetStyle().FramePadding.X;
			var originalFramePaddingY = ImGui.GetStyle().FramePadding.Y;

			// The total dimensions to draw including the frame padding.
			var totalWidth = width + originalFramePaddingX * 2.0f;
			var totalHeight = height + originalFramePaddingY * 2.0f;

			// The offset in pixels from within the total dimensions to draw the image.
			var xOffset = 0.0f;
			var yOffset = 0.0f;

			// The size of the image to draw.
			var size = new System.Numerics.Vector2(width, height);
			
			// The UV coordinates for drawing the texture on the image.
			var uv0 = new System.Numerics.Vector2(0.0f, 0.0f);
			var uv1 = new System.Numerics.Vector2(1.0f, 1.0f);

			switch (mode)
			{
				// Maintain the original size of the texture.
				// Crop and offset as needed.
				case TextureLayoutMode.OriginalSize:
				{
					// If the texture is wider than the destination area then adjust the UV X values
					// so that we crop the texture.
					if (textureMonogame.Width > width)
					{
						xOffset = 0.0f;
						size.X = width;
						uv0.X = (textureMonogame.Width - width) * 0.5f / textureMonogame.Width;
						uv1.X = 1.0f - uv0.X;
					}
					// If the destination area is wider than the texture, then set the X offset value
					// so that we center the texture in X within the destination area.
					else if (textureMonogame.Width < width)
					{
						xOffset = (width - textureMonogame.Width) * 0.5f;
						size.X = textureMonogame.Width;
						uv0.X = 0.0f;
						uv1.X = 1.0f;
					}

					// If the texture is taller than the destination area then adjust the UV Y values
					// so that we crop the texture.
					if (textureMonogame.Height > height)
					{
						yOffset = 0.0f;
						size.Y = height;
						uv0.Y = (textureMonogame.Height - height) * 0.5f / textureMonogame.Height;
						uv1.Y = 1.0f - uv0.Y;
					}
					// If the destination area is taller than the texture, then set the Y offset value
					// so that we center the texture in Y within the destination area.
					else if (textureMonogame.Height < height)
					{
						yOffset = (height - textureMonogame.Height) * 0.5f;
						size.Y = textureMonogame.Height;
						uv0.Y = 0.0f;
						uv1.Y = 1.0f;
					}

					break;
				}
				
				// Stretch the texture to exactly fill the destination area.
				// The parameters are already set for rendering in this mode.
				case TextureLayoutMode.Stretch:
				{
					break;
				}

				// Scale the texture uniformly such that it fills the entire destination area.
				// Crop the dimension which goes beyond the destination area as needed.
				case TextureLayoutMode.Fill:
				{
					var textureAspectRatio = (float)textureMonogame.Width / textureMonogame.Height;
					var destinationAspectRatio = (float)width / height;

					// If the texture is wider than the destination area, crop the left and right.
					if (textureAspectRatio > destinationAspectRatio)
					{
						// Crop left and right.
						var scaledTextureW = textureMonogame.Width * ((float)height / textureMonogame.Height);
						uv0.X = (scaledTextureW - height) * 0.5f / scaledTextureW;
						uv1.X = 1.0f - uv0.X;

						// Fill Y.
						uv0.Y = 0.0f;
						uv1.Y = 1.0f;
					}

					// If the texture is taller than the destination area, crop the top and bottom.
					else if (textureAspectRatio < destinationAspectRatio)
					{
						// Fill X.
						uv0.X = 0.0f;
						uv1.X = 1.0f;

						// Crop top and bottom.
						var scaledTextureH = textureMonogame.Height * ((float)width / textureMonogame.Width);
						uv0.Y = (scaledTextureH - width) * 0.5f / scaledTextureH;
						uv1.Y = 1.0f - uv0.Y;
					}

					break;
				}

				// Scale the texture uniformly such that it fills the destination area without going over
				// in either dimension.
				case TextureLayoutMode.Box:
				{
					var textureAspectRatio = (float)textureMonogame.Width / textureMonogame.Height;
					var destinationAspectRatio = (float)width / height;

					// If the texture is wider than the destination area, letterbox.
					if (textureAspectRatio > destinationAspectRatio)
					{
						var scale = (float)width / textureMonogame.Width;
						size.X = textureMonogame.Width * scale;
						size.Y = textureMonogame.Height * scale;
						yOffset = (height - textureMonogame.Height * scale) * 0.5f;
					}

					// If the texture is taller than the destination area, pillarbox.
					else if (textureAspectRatio < destinationAspectRatio)
					{
						var scale = (float)height / textureMonogame.Height;
						size.X = textureMonogame.Width * scale;
						size.Y = textureMonogame.Height * scale;
						xOffset = (width - textureMonogame.Width * scale) * 0.5f;
					}

					break;
				}
			}

			// Set the padding and spacing so we can draw dummy boxes to offset the image.
			ImGui.GetStyle().ItemSpacing.X = 0;
			ImGui.GetStyle().ItemSpacing.Y = 0;
			ImGui.GetStyle().FramePadding.X = 0;
			ImGui.GetStyle().FramePadding.Y = 0;

			// Begin a child so we can add dummy items to offset the image.
			if (ImGui.BeginChild(id, new System.Numerics.Vector2(totalWidth, totalHeight)))
			{
				// Offset in Y.
				if (yOffset > 0.0f)
					ImGui.Dummy(new System.Numerics.Vector2(width, yOffset));
				
				// Offset in X.
				if (xOffset > 0.0f)
				{
					ImGui.Dummy(new System.Numerics.Vector2(xOffset, size.Y));
					ImGui.SameLine();
				}

				// Reset the padding now so it draws correctly on the image.
				ImGui.GetStyle().FramePadding.X = originalFramePaddingX;
				ImGui.GetStyle().FramePadding.Y = originalFramePaddingY;
				
				// Draw the image.
				if (button)
					result = ImGui.ImageButton(textureImGui, size, uv0, uv1);
				else
					ImGui.Image(textureImGui, size, uv0, uv1);
			}
			ImGui.EndChild();

			// Restore the padding and spacing values.
			ImGui.GetStyle().FramePadding.X = originalFramePaddingX;
			ImGui.GetStyle().FramePadding.Y = originalFramePaddingY;
			ImGui.GetStyle().ItemSpacing.X = originalItemSpacingX;
			ImGui.GetStyle().ItemSpacing.Y = originalItemSpacingY;

			return result;
		}

		#endregion ImGui Helpers

		#region File Open Helpers

		public static string FileOpenFilterForImages(string name, bool includeAllFiles)
		{
			var extenstionTypes = new List<string[]> { ExpectedImageFormats };
			return FileOpenFilter(name, extenstionTypes, includeAllFiles);
		}

		public static string FileOpenFilterForImagesAndVideos(string name, bool includeAllFiles)
		{
			var extenstionTypes = new List<string[]> { ExpectedImageFormats, ExpectedVideoFormats };
			return FileOpenFilter(name, extenstionTypes, includeAllFiles);
		}

		public static string FileOpenFilterForAudio(string name, bool includeAllFiles)
		{
			var extenstionTypes = new List<string[]> { ExpectedAudioFormats };
			return FileOpenFilter(name, extenstionTypes, includeAllFiles);
		}

		public static string FileOpenFilterForVideo(string name, bool includeAllFiles)
		{
			var extenstionTypes = new List<string[]> { ExpectedVideoFormats };
			return FileOpenFilter(name, extenstionTypes, includeAllFiles);
		}

		public static string FileOpenFilterForLyrics(string name, bool includeAllFiles)
		{
			var extenstionTypes = new List<string[]> { ExpectedLyricsFormats };
			return FileOpenFilter(name, extenstionTypes, includeAllFiles);
		}

		private static string FileOpenFilter(string name, List<string[]> extensionTypes, bool includeAllFiles)
		{
			var sb = new StringBuilder();
			sb.Append(name);
			sb.Append(" Files (");
			var first = true;
			foreach (var extensions in extensionTypes)
			{
				foreach (var extension in extensions)
				{
					if (!first)
						sb.Append(",");
					sb.Append("*.");
					sb.Append(extension);
					first = false;
				}
			}

			sb.Append(")|");
			first = true;
			foreach (var extensions in extensionTypes)
			{
				foreach (var extension in extensions)
				{
					if (!first)
						sb.Append(";");
					sb.Append("*.");
					sb.Append(extension);
					first = false;
				}
			}

			if (includeAllFiles)
			{
				sb.Append("|All files (*.*)|*.*");
			}

			return sb.ToString();
		}

		public static string BrowseFile(string name, string initialDirectory, string currentFileRelativePath, string filter)
		{
			string relativePath = null;
			using var openFileDialog = new OpenFileDialog();
			var startInitialDirectory = initialDirectory;
			if (!string.IsNullOrEmpty(currentFileRelativePath))
			{
				initialDirectory = Path.Combine(initialDirectory, currentFileRelativePath);
				initialDirectory = System.IO.Path.GetDirectoryName(initialDirectory);
			}

			openFileDialog.InitialDirectory = initialDirectory;
			openFileDialog.Filter = filter;
			openFileDialog.FilterIndex = 1;
			openFileDialog.Title = $"Open {name} File";

			if (openFileDialog.ShowDialog() == DialogResult.OK)
			{
				var fileName = openFileDialog.FileName;
				relativePath = Path.GetRelativePath(startInitialDirectory, fileName);
			}

			return relativePath;
		}

		#endregion File Open Helpers

		#region Application Focus

		public static bool IsApplicationFocused()
		{
			var activatedHandle = GetForegroundWindow();
			if (activatedHandle == IntPtr.Zero)
				return false;

			GetWindowThreadProcessId(activatedHandle, out var activeProcId);
			return activeProcId == Process.GetCurrentProcess().Id;
		}

		[DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
		private static extern IntPtr GetForegroundWindow();

		[DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
		private static extern int GetWindowThreadProcessId(IntPtr handle, out int processId);

		#endregion Application Focus
	}

	public static class EditorExtensions
	{
		public static bool FloatEquals(this float f, float other)
		{
			return f - float.Epsilon <= other && f + float.Epsilon >= other;
		}

		public static bool DoubleEquals(this double d, double other)
		{
			return d - double.Epsilon <= other && d + double.Epsilon >= other;
		}
	}
}

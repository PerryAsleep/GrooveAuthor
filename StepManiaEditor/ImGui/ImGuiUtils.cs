using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using Fumen;
using Fumen.Converters;
using ImGuiNET;
using Microsoft.Xna.Framework.Graphics;
using MonoGameExtensions;
using static StepManiaEditor.Utils;

namespace StepManiaEditor;

/// <summary>
/// This class offers helper methods for drawing ImGui controls with small additional behaviors.
/// </summary>
internal sealed class ImGuiUtils
{
	private static double DpiScale;

	// UI positioning values affected by DPI scaling.
	public const int HelpWidthDefaultDPI = 18;
	public const int CloseWidthDefaultDPI = 18;
	public const int MenuBarHeightDefaultDPI = 20;
	public const int ChartHeaderHeightDefaultDPI = 21;
	public const int CDTitleWidthDefaultDPI = 164;
	public const int CDTitleHeightDefaultDPI = 164;
	public const int BackgroundWidthDefaultDPI = 640;
	public const int BackgroundHeightDefaultDPI = 480;
	public const int BannerWidthDefaultDPI = 418;
	public const int BannerHeightDefaultDPI = 164;
	public const int SceneWidgetPaddingDefaultDPI = 10;
	public const int MiscEventLeftSideMarkerNumberAllowanceDefaultDPI = 10;
	public const int MeasureMarkerPaddingDefaultDPI = 40;
	public const int ActiveChartBoundaryWidthDefaultDPI = 1;
	public const int MeasureMarkerNumberPaddingDefaultDPI = 10;

	private static Editor Editor;
	private static readonly Dictionary<Type, string[]> EnumStringsCacheByType = new();

	private class EnumByAllowedValueCacheData
	{
		public Dictionary<int, int> AllowedValueToEnumValue;
		public Dictionary<int, int> EnumValueToAllowedValue;
		public string[] EnumStrings;
	}

	public static readonly string[] ValidNoteTypeStrings;
	public static readonly string[] ValidSnapLevelStrings;

	private static readonly Dictionary<string, EnumByAllowedValueCacheData> EnumDataCacheByCustomKey = new();
	private static readonly List<bool> EnabledStack = new();

	static ImGuiUtils()
	{
		var numStrings = SMCommon.ValidDenominators.Length;
		ValidNoteTypeStrings = new string[numStrings];
		ValidSnapLevelStrings = new string[numStrings + 1];
		ValidSnapLevelStrings[0] = "None";
		for (var i = 0; i < numStrings; i++)
		{
			ValidNoteTypeStrings[i] = $"1/{SMCommon.ValidDenominators[i] * SMCommon.NumBeatsPerMeasure}";
			ValidSnapLevelStrings[i + 1] = ValidNoteTypeStrings[i];
		}
	}

	public static void Init(Editor editor)
	{
		Editor = editor;
	}

	public static string GetSubdivisionTypeString(SubdivisionType subdivisionType)
	{
		return ValidNoteTypeStrings[(int)subdivisionType];
	}

	/// <summary>
	/// This struct corresponds to ImGuiTableColumnSortSpecs in Dear ImGui.
	/// In ImGuiNET, the corresponding struct does not use a correct layout.
	/// </summary>
	[StructLayout(LayoutKind.Explicit, Size = 12)]
	public struct NativeImGuiTableColumnSortSpecs
	{
		[FieldOffset(0)] public uint ColumnUserID;
		[FieldOffset(4)] public short ColumnIndex;
		[FieldOffset(6)] public short SortOrder;
		[FieldOffset(8)] public byte SortDirection;
	}

	public static string FormatImGuiInt(string fmt, int i)
	{
		return Printf.Sprintf(fmt, i);
	}

	public static string FormatImGuiDouble(string fmt, double d)
	{
		return Printf.Sprintf(fmt, d);
	}

	/// <summary>
	/// Draws an ImGui Combo element for the values of the enum of type T.
	/// </summary>
	/// <typeparam name="T">Enum type of values in the Combo element.</typeparam>
	/// <param name="name">Name of the element for ImGui.</param>
	/// <param name="enumValue">The current value.</param>
	/// <returns>Whether the Combo value has changed.</returns>
	public static bool ComboFromEnum<T>(string name, ref T enumValue) where T : Enum
	{
		var strings = GetCachedEnumStrings<T>();
		var intValue = (int)(object)enumValue;
		var result = ImGui.Combo(name, ref intValue, strings, strings.Length);
		enumValue = (T)(object)intValue;
		return result;
	}

	/// <summary>
	/// Draws an ImGui Combo element for the array of allowed values of type T.
	/// This assumes that allowedValues does not change across multiple calls for the same cacheKey.
	/// </summary>
	/// <typeparam name="T">Enum type of values in the Combo element.</typeparam>
	/// <param name="name">Name of the element for ImGui.</param>
	/// <param name="enumValue">The current value.</param>
	/// <param name="allowedValues">A sorted list of allowed values to use for the Combo, rather than the full enum.</param>
	/// <param name="cacheKey">A key to use for caching lookup data.</param>
	/// <returns>Whether the Combo value has changed.</returns>
	public static bool ComboFromEnum<T>(string name, ref T enumValue, T[] allowedValues, string cacheKey) where T : Enum
	{
		// Cache lookup data.
		if (!EnumDataCacheByCustomKey.ContainsKey(cacheKey))
		{
			var allowedValueToEnumValue = new Dictionary<int, int>();
			var enumValueToAllowedValue = new Dictionary<int, int>();
			var numEnumValues = allowedValues.Length;
			var enumStrings = new string[numEnumValues];
			for (var i = 0; i < numEnumValues; i++)
			{
				enumStrings[i] = FormatEnumForUI(allowedValues[i].ToString());
				allowedValueToEnumValue[i] = (int)(object)allowedValues[i];
				enumValueToAllowedValue[(int)(object)allowedValues[i]] = i;
			}

			EnumDataCacheByCustomKey[cacheKey] = new EnumByAllowedValueCacheData
			{
				AllowedValueToEnumValue = allowedValueToEnumValue,
				EnumValueToAllowedValue = enumValueToAllowedValue,
				EnumStrings = enumStrings,
			};
		}

		var cacheData = EnumDataCacheByCustomKey[cacheKey];
		var intValue = cacheData.EnumValueToAllowedValue[(int)(object)enumValue];
		var result = ImGui.Combo(name, ref intValue, cacheData.EnumStrings, cacheData.EnumStrings.Length);
		enumValue = (T)(object)cacheData.AllowedValueToEnumValue[intValue];
		return result;
	}

	public static bool ComboFromArray(string name, ref int currentIndex, string[] values)
	{
		return ImGui.Combo(name, ref currentIndex, values, values.Length);
	}

	/// <summary>
	/// Draws a TreeNode in ImGui with Selectable elements for each value in the
	/// Enum specified by the given type parameter T.
	/// </summary>
	/// <typeparam name="T">Enum type of choices in the tree.</typeparam>
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

	public static string GetPrettyEnumString<T>(T value)
	{
		var strings = GetCachedEnumStrings<T>();
		var intValue = (int)(object)value;
		return strings[intValue];
	}

	private static string[] GetCachedEnumStrings<T>()
	{
		var typeOfT = typeof(T);
		if (EnumStringsCacheByType.TryGetValue(typeOfT, out var strings))
			return strings;

		var enumValues = Enum.GetValues(typeOfT);
		var numEnumValues = enumValues.Length;
		var enumStrings = new string[numEnumValues];
		for (var i = 0; i < numEnumValues; i++)
			enumStrings[i] = FormatEnumForUI(enumValues.GetValue(i)!.ToString());
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
		var sb = new StringBuilder(enumValue.Length * 2);
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

	/// <summary>
	/// Draws "(?)" Text via ImGui with a Tooltip configured to show when the text is hovered.
	/// </summary>
	/// <param name="text">Text to draw in the Tooltip.</param>
	public static void HelpMarker(string text)
	{
		PushEnabled();
		Text("(?)", GetHelpWidth(), true);
		ToolTip(text);
		PopEnabled();
	}

	/// <summary>
	/// Draws an ImGui Tooltip with the given text if the current item is hovered.
	/// </summary>
	/// <param name="text">Text to draw in the Tooltip.</param>
	public static void ToolTip(string text)
	{
		if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
		{
			ImGui.BeginTooltip();
			ImGui.PushTextWrapPos(UiScaled(650));
			ImGui.TextUnformatted(text);
			ImGui.PopTextWrapPos();
			ImGui.EndTooltip();
		}
	}

	private static void SetMenuColor(uint color)
	{
		var min = ImGui.GetCursorScreenPos();
		min.X -= ImGui.GetStyle().FramePadding.X;
		min.Y -= (int)(ImGui.GetStyle().ItemSpacing.Y * 0.5);
		var max = new Vector2(
			min.X + ImGui.GetContentRegionAvail().X + ImGui.GetStyle().FramePadding.X * 2,
			min.Y + ImGui.GetFrameHeight() + ImGui.GetStyle().ItemSpacing.Y - ImGui.GetStyle().FramePadding.Y * 2);
		ImGui.GetWindowDrawList().AddRectFilled(min, max, color);
	}

	/// <summary>
	/// Draws an ImGui MenuItem with a given background color.
	/// </summary>
	/// <param name="label">Label text for the MenuItem.</param>
	/// <param name="enabled">Whether or not the MenuItem is enabled.</param>
	/// <param name="color">The color to draw behind the MenuItem.</param>
	/// <returns>Whether the MenuItem was selected.</returns>
	public static bool MenuItemWithColor(string label, bool enabled, uint color)
	{
		SetMenuColor(color);
		return ImGui.MenuItem(label, enabled);
	}

	/// <summary>
	/// Draws an ImGui MenuItem with a given background color.
	/// </summary>
	/// <param name="label">Label text for the MenuItem.</param>
	/// <param name="shortcut">Shortcut text for the MenuItem.</param>
	/// <param name="enabled">Whether or not the MenuItem is enabled.</param>
	/// <param name="color">The color to draw behind the MenuItem.</param>
	/// <returns>Whether the MenuItem was selected.</returns>
	public static bool MenuItemWithColor(string label, string shortcut, bool enabled, uint color)
	{
		SetMenuColor(color);
		return ImGui.MenuItem(label, shortcut, false, enabled);
	}

	/// <summary>
	/// Draws an ImGui BeginMenu with a given background color.
	/// </summary>
	/// <param name="label">Label text for the MenuItem.</param>
	/// <param name="enabled">Whether or not the MenuItem is enabled.</param>
	/// <param name="color">The color to draw behind the MenuItem.</param>
	/// <returns>Whether the MenuItem was selected.</returns>
	public static bool BeginMenuWithColor(string label, bool enabled, uint color)
	{
		SetMenuColor(color);
		return ImGui.BeginMenu(label, enabled);
	}

	/// <summary>
	/// ImGui Text methods take printf-style format strings.
	/// We need to escape the special printf characters otherwise ImGui will
	/// draw malformed text or crash.
	/// </summary>
	/// <param name="text">Unformatted text.</param>
	/// <returns>Text escaped for sending to ImGui.</returns>
	public static string EscapeTextForImGui(string text)
	{
		if (!text.Contains('%'))
			return text;
		return text.Replace("%", "%%");
	}

	/// <summary>
	/// Draws an ImGui Text element with a specified width.
	/// </summary>
	/// <param name="text">Text to display in the ImGUi Text element.</param>
	/// <param name="width">Width of the element.</param>
	/// <param name="disabled">Whether or not the element should be disabled.</param>
	public static void Text(string text, float width, bool disabled = false)
	{
		// The table below will render a line / border if the text is empty.
		if (string.IsNullOrEmpty(text))
		{
			ImGui.Dummy(new Vector2(width, 0));
			return;
		}

		// Wrap the text in Table in order to control the size precisely.
		if (ImGui.BeginTable(text, 1, ImGuiTableFlags.None, new Vector2(width, 0), width))
		{
			ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthStretch, 100.0f);
			ImGui.TableNextRow();
			ImGui.TableSetColumnIndex(0);
			if (disabled)
			{
				PushDisabled();
				ImGui.TextUnformatted(text);
				PopDisabled();
			}
			else
			{
				ImGui.TextUnformatted(text);
			}

			ImGui.EndTable();
		}
	}

	/// <summary>
	/// Draws a colored ImGui Text element with a specified width.
	/// </summary>
	/// <param name="color">The color of the text.</param>
	/// <param name="text">Text to display in the ImGUi Text element.</param>
	/// <param name="width">Width of the element.</param>
	public static void TextColored(Vector4 color, string text, float width)
	{
		// The table below will render a line / border if the text is empty.
		if (string.IsNullOrEmpty(text))
		{
			ImGui.Dummy(new Vector2(width, 0));
			return;
		}

		// Wrap the text in Table in order to control the size precisely.
		if (ImGui.BeginTable(text, 1, ImGuiTableFlags.None, new Vector2(width, 0), width))
		{
			ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthStretch, 100.0f);
			ImGui.TableNextRow();
			ImGui.TableSetColumnIndex(0);
			ImGui.PushStyleColor(ImGuiCol.Text, color);
			ImGui.TextUnformatted(text);
			ImGui.PopStyleColor();
			ImGui.EndTable();
		}
	}

	public static bool SliderUInt(string text, ref uint value, uint min, uint max, string format, ImGuiSliderFlags flags)
	{
		var iValue = (int)value;
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
		bool ret;
		fixed (double* p = &value)
		{
			var pData = new IntPtr(p);
			var pMin = new IntPtr(&min);
			var pMax = new IntPtr(&max);
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
		bool ret;
		fixed (double* p = &value)
		{
			var pData = new IntPtr(p);
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

	public static void PushErrorColor()
	{
		ImGui.PushStyleColor(ImGuiCol.FrameBg, UIFrameErrorColor);
	}

	public static void PopErrorColor()
	{
		ImGui.PopStyleColor(1);
	}

	public static void DrawImage(
		string id,
		IntPtr textureImGui,
		Texture2D textureMonogame,
		uint width,
		uint height,
		TextureUtils.TextureLayoutMode mode)
	{
		DrawImageInternal(id, textureImGui, textureMonogame, width, height, mode, false);
	}

	public static bool DrawButton(
		string id,
		IntPtr textureImGui,
		Texture2D textureMonogame,
		uint width,
		uint height,
		TextureUtils.TextureLayoutMode mode)
	{
		return DrawImageInternal(id, textureImGui, textureMonogame, width, height, mode, true);
	}

	private static bool DrawImageInternal(
		string id,
		IntPtr textureImGui,
		Texture2D textureMonogame,
		uint width,
		uint height,
		TextureUtils.TextureLayoutMode mode,
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

		var (xOffset, yOffset, size, uv0, uv1) = TextureUtils.GetTextureUVs(textureMonogame, width, height, mode);

		// Set the padding and spacing so we can draw dummy boxes to offset the image.
		ImGui.GetStyle().ItemSpacing.X = 0;
		ImGui.GetStyle().ItemSpacing.Y = 0;
		ImGui.GetStyle().FramePadding.X = 0;
		ImGui.GetStyle().FramePadding.Y = 0;

		// Begin a child so we can add dummy items to offset the image.
		if (ImGui.BeginChild(id, new Vector2(totalWidth, totalHeight)))
		{
			// Offset in Y.
			if (yOffset > 0.0f)
				ImGui.Dummy(new Vector2(width, yOffset));

			// Offset in X.
			if (xOffset > 0.0f)
			{
				ImGui.Dummy(new Vector2(xOffset, size.Y));
				ImGui.SameLine();
			}

			// Reset the padding now so it draws correctly on the image.
			ImGui.GetStyle().FramePadding.X = originalFramePaddingX;
			ImGui.GetStyle().FramePadding.Y = originalFramePaddingY;

			// Draw the image.
			if (button)
				result = ImGui.ImageButton($"##{id}Image", textureImGui, size, uv0, uv1);
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

	public static unsafe void PushAlpha(ImGuiCol col, float alpha)
	{
		var color = ImGui.GetStyleColorVec4(col);
		var newColor = ((uint)(byte)(alpha * color->W * byte.MaxValue) << 24)
		               | ((uint)(byte)(color->Z * byte.MaxValue) << 16)
		               | ((uint)(byte)(color->Y * byte.MaxValue) << 8)
		               | (byte)(color->X * byte.MaxValue);
		ImGui.PushStyleColor(col, newColor);
	}

	public static void PopAlpha()
	{
		ImGui.PopStyleColor();
	}

	public static void PopAlpha(int count)
	{
		ImGui.PopStyleColor(count);
	}

	#region Window Placement

	public static bool BeginWindow(string title, ref bool showWindow, float width, ImGuiWindowFlags flags = ImGuiWindowFlags.None)
	{
		var screenWidth = Editor.GetViewportWidth();
		var x = (screenWidth - width) * 0.5f;
		ImGui.SetNextWindowPos(new Vector2(x, 100.0f), ImGuiCond.FirstUseEver);
		ImGui.SetNextWindowSize(new Vector2(width, 0.0f), ImGuiCond.FirstUseEver);
		if ((flags & ImGuiWindowFlags.AlwaysVerticalScrollbar) == ImGuiWindowFlags.None)
			flags |= ImGuiWindowFlags.NoScrollbar;
		return ImGui.Begin(title, ref showWindow, flags);
	}

	public static bool BeginWindow(string title, ref bool showWindow, float width, float height,
		ImGuiWindowFlags flags = ImGuiWindowFlags.None)
	{
		return BeginWindow(title, ref showWindow, new Vector2(width, height), flags);
	}

	public static bool BeginWindow(string title, ref bool showWindow, Vector2 size,
		ImGuiWindowFlags flags = ImGuiWindowFlags.None)
	{
		var screenWidth = Editor.GetViewportWidth();
		var screenHeight = Editor.GetViewportHeight();
		var x = Math.Max(0.0f, (screenWidth - size.X) * 0.5f);
		var y = Math.Max(0.0f, (screenHeight - size.Y) * 0.5f);
		ImGui.SetNextWindowPos(new Vector2(x, y), ImGuiCond.FirstUseEver);
		ImGui.SetNextWindowSize(size, ImGuiCond.FirstUseEver);
		if ((flags & ImGuiWindowFlags.AlwaysVerticalScrollbar) == ImGuiWindowFlags.None)
			flags |= ImGuiWindowFlags.NoScrollbar;
		return ImGui.Begin(title, ref showWindow, flags);
	}

	#endregion Window Placement

	#region Table Headers

	public class ColumnData
	{
		public ColumnData(string name, string toolTip, ImGuiTableColumnFlags flags)
		{
			Name = name;
			ToolTipText = toolTip;
			Flags = flags;
			WidthOrWeight = 0.0f;
		}

		public ColumnData(string name, string toolTip, ImGuiTableColumnFlags flags, float widthOrWeight)
		{
			Name = name;
			ToolTipText = toolTip;
			Flags = flags;
			WidthOrWeight = widthOrWeight;
		}

		public readonly string Name;
		public readonly string ToolTipText;
		public readonly ImGuiTableColumnFlags Flags;
		public readonly float WidthOrWeight;
	}

	public static void BeginTable(ColumnData[] columnData)
	{
		ImGui.TableSetupScrollFreeze(0, 1);

		for (uint i = 0; i < columnData.Length; i++)
		{
			var colData = columnData[i];
			ImGui.TableSetupColumn(colData.Name, colData.Flags, colData.WidthOrWeight, i);
		}

		// Construct headers manually in order to add tool tips.
		ImGui.TableNextRow(ImGuiTableRowFlags.Headers);
		for (var i = 0; i < columnData.Length; i++)
		{
			if (!ImGui.TableSetColumnIndex(i))
				continue;
			var colData = columnData[i];
			ImGui.PushID(i);
			ImGui.TableHeader(colData.Name);
			if (!string.IsNullOrEmpty(colData.ToolTipText))
				ToolTip(colData.ToolTipText);
			ImGui.PopID();
		}
	}

	#endregion Table Headers

	#region UI Position and DPI Scaling

	public static double GetDpiScale()
	{
		// Cache the DPI scale value so queries to the value are consistent for the lifetime
		// of the application. Some DPI scaling parameters are used only in initialization and
		// others are used per frame.
		if (!DpiScale.DoubleEquals(0.0))
			return DpiScale;

		// Try to use the DPI scale specified in the preferences.
		if (Preferences.Instance.PreferencesOptions.UseCustomDpiScale)
			DpiScale = Preferences.Instance.PreferencesOptions.DpiScale;

		// If no DPI scale is specified in the preferences, use the system default DPI scale.
		if (DpiScale.DoubleEquals(0.0))
			DpiScale = Editor.GetMonitorDpiScale();

		// Ensure the DPI scale is set to a valid value.
		if (DpiScale <= 0.0)
			DpiScale = 1.0;

		return DpiScale;
	}

	public static int UiScaled(int value)
	{
		return (int)(value * GetDpiScale());
	}

	public static int GetHelpWidth()
	{
		return UiScaled(HelpWidthDefaultDPI);
	}

	public static int GetCloseWidth()
	{
		return UiScaled(CloseWidthDefaultDPI);
	}

	public static int GetMenuBarHeight()
	{
		return UiScaled(MenuBarHeightDefaultDPI);
	}

	public static int GetChartHeaderHeight()
	{
		return UiScaled(ChartHeaderHeightDefaultDPI);
	}

	public static int GetMiniMapYPaddingFromTopInScreenSpace()
	{
		return UiScaled(MenuBarHeightDefaultDPI + ChartHeaderHeightDefaultDPI + SceneWidgetPaddingDefaultDPI);
	}

	public static int GetMiniMapYPaddingFromTopInChartSpace()
	{
		return UiScaled(ChartHeaderHeightDefaultDPI + SceneWidgetPaddingDefaultDPI);
	}

	public static int GetMiniMapYPaddingFromBottom()
	{
		return UiScaled(SceneWidgetPaddingDefaultDPI);
	}

	public static int GetSceneWidgetPadding()
	{
		return UiScaled(SceneWidgetPaddingDefaultDPI);
	}

	public static int GetMiscEventLeftSideMarkerNumberAllowance()
	{
		return UiScaled(MiscEventLeftSideMarkerNumberAllowanceDefaultDPI);
	}

	public static int GetMeasureMarkerPadding()
	{
		return UiScaled(MeasureMarkerPaddingDefaultDPI);
	}

	public static int GetActiveChartBoundaryWidth()
	{
		return UiScaled(ActiveChartBoundaryWidthDefaultDPI);
	}

	public static int GetMeasureMarkerNumberPadding()
	{
		return UiScaled(MeasureMarkerNumberPaddingDefaultDPI);
	}

	public static int GetBackgroundWidth()
	{
		return UiScaled(BackgroundWidthDefaultDPI);
	}

	public static int GetBackgroundHeight()
	{
		return UiScaled(BackgroundHeightDefaultDPI);
	}

	public static int GetBannerWidth()
	{
		return UiScaled(BannerWidthDefaultDPI);
	}

	public static int GetBannerHeight()
	{
		return UiScaled(BannerHeightDefaultDPI);
	}

	public static int GetCDTitleWidth()
	{
		return UiScaled(CDTitleWidthDefaultDPI);
	}

	public static int GetCDTitleHeight()
	{
		return UiScaled(CDTitleHeightDefaultDPI);
	}

	#endregion UI Position and DPI Scaling
}

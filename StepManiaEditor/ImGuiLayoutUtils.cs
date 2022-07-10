using System;
using System.Collections.Generic;
using System.Reflection;
using ImGuiNET;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using Fumen;
using Fumen.ChartDefinition;
using Fumen.Converters;

namespace StepManiaEditor
{
	public class ImGuiLayoutUtils
	{
		/// <summary>
		/// Cache of values being edited through ImGui controls.
		/// We often need to edit a cached value instead of a real value
		///  Example: Text entry where we do not want to commit the text until editing is complete.
		/// We also often need to enqueue an undo action of a cached value from before editing began.
		///  Example: With a slider the value is changing continuously and we do not want to enqueue
		///  an event until the user releases the control and the before value in this case should be
		///  a previously cached value.
		/// </summary>
		private static readonly Dictionary<string, object> Cache = new Dictionary<string, object>();

		private static string CacheKeyPrefix = "";
		private const string DragHelpText = "\nShift+drag for large adjustments.\nAlt+drag for small adjustments.";

		private static ImFontPtr ImGuiFont;

		public static void SetFont(ImFontPtr font)
		{
			ImGuiFont = font;
		}

		public static bool BeginTable(string title, float titleColumnWidth)
		{
			CacheKeyPrefix = title ?? "";
			var ret = ImGui.BeginTable(title, 2, ImGuiTableFlags.None);
			if (ret)
			{
				ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, titleColumnWidth);
				ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthStretch, 100);
			}

			return ret;
		}

		public static void EndTable()
		{
			ImGui.EndTable();
		}

		public static void DrawRowTitleAndAdvanceColumn(string title)
		{
			ImGui.TableNextRow();

			ImGui.TableSetColumnIndex(0);
			if (!string.IsNullOrEmpty(title))
				ImGui.Text(title);

			ImGui.TableSetColumnIndex(1);
		}

		#region Checkbox

		public static bool DrawRowCheckbox(string title, ref bool value, string help = null)
		{
			DrawRowTitleAndAdvanceColumn(title);
			return DrawCheckbox(title, ref value, ImGui.GetContentRegionAvail().X, help);
		}

		public static bool DrawRowCheckbox(bool undoable, string title, object o, string fieldName, string help = null)
		{
			DrawRowTitleAndAdvanceColumn(title);
			return DrawCheckbox(undoable, title, o, fieldName, ImGui.GetContentRegionAvail().X, help);
		}

		private static bool DrawCheckbox(
			string title,
			ref bool value,
			float width,
			string help = null)
		{
			ImGui.SetNextItemWidth(DrawHelp(help, width));
			return ImGui.Checkbox(GetElementTitle(title), ref value);
		}

		private static bool DrawCheckbox(
			bool undoable,
			string title,
			object o,
			string fieldName,
			float width,
			string help = null)
		{
			ImGui.SetNextItemWidth(DrawHelp(help, width));

			var value = GetValueFromFieldOrProperty<bool>(o, fieldName);
			var ret = ImGui.Checkbox(GetElementTitle(title, fieldName), ref value);
			if (ret)
			{
				if (undoable)
				{
					ActionQueue.Instance.Do(new ActionSetObjectFieldOrPropertyValue<bool>(o, fieldName, value));
				}
				else
				{
					SetFieldOrPropertyToValue(o, fieldName, value);
				}
			}

			return ret;
		}

		#endregion Checkbox

		#region Button

		public static bool DrawRowButton(string title, string buttonText, string help = null)
		{
			DrawRowTitleAndAdvanceColumn(title);
			ImGui.SetNextItemWidth(DrawHelp(help, ImGui.GetContentRegionAvail().X));
			return ImGui.Button(buttonText);
		}

		#endregion Button

		#region Text Input

		public static void DrawRowTextInput(string title, ref string value, string help = null)
		{
			DrawRowTitleAndAdvanceColumn(title);
			DrawTextInput(title, ref value, ImGui.GetContentRegionAvail().X, help);
		}

		private static void DrawTextInput(string title, ref string value, float width, string help = null)
		{
			ImGui.SetNextItemWidth(DrawHelp(help, width));
			ImGui.InputTextWithHint(GetElementTitle(title), title, ref value, 256);
		}

		public static void DrawRowTextInput(bool undoable, string title, object o, string fieldName, string help = null)
		{
			DrawRowTitleAndAdvanceColumn(title);
			DrawTextInput(undoable, title, o, fieldName, ImGui.GetContentRegionAvail().X, help);
		}

		public static void DrawRowTextInputWithTransliteration(bool undoable, string title, object o, string fieldName,
			string transliterationFieldName, string help = null)
		{
			DrawRowTitleAndAdvanceColumn(title);
			DrawTextInput(undoable, title, o, fieldName, ImGui.GetContentRegionAvail().X * 0.5f, help);
			ImGui.SameLine();
			DrawTextInput(undoable, "Transliteration", o, transliterationFieldName, ImGui.GetContentRegionAvail().X,
				"Optional text to use when sorting by this value.\nStepMania sorts values lexicographically, preferring transliterations.");
		}

		private static void DrawTextInput(bool undoable, string title, object o, string fieldName, float width,
			string help = null)
		{
			(bool, string) Func(string v)
			{
				v ??= "";
				var r = ImGui.InputTextWithHint(GetElementTitle(title, fieldName), title, ref v, 256);
				return (r, v);
			}

			DrawCachedEditReference<string>(undoable, title, o, fieldName, width, Func, StringCompare, null, help);
		}

		#endregion Text Input

		#region Time Signature

		private static void DrawTimeSignatureInput(bool undoable, string title, object o, string fieldName, float width,
			string help = null)
		{
			(bool, string) Func(string v)
			{
				v ??= "";
				var r = ImGui.InputText(GetElementTitle(title, fieldName), ref v, 256);
				return (r, v);
			}

			bool ValidationFunc(string v)
			{
				var (valid, _) = EditorTimeSignatureEvent.IsValidTimeSignatureString(v);
				return valid;
			}

			DrawCachedEditReference<string>(undoable, title, o, fieldName, width, Func, StringCompare, ValidationFunc, help);
		}

		#endregion Time Signature

		#region Multipliers

		private static void DrawMultipliersInput(bool undoable, string title, object o, string fieldName, float width,
			string help = null)
		{
			(bool, string) Func(string v)
			{
				v ??= "";
				var r = ImGui.InputText(GetElementTitle(title, fieldName), ref v, 256);
				return (r, v);
			}

			bool ValidationFunc(string v)
			{
				var (valid, _, _) = EditorMultipliersEvent.IsValidMultipliersString(v);
				return valid;
			}

			DrawCachedEditReference<string>(undoable, title, o, fieldName, width, Func, StringCompare, ValidationFunc, help);
		}

		#endregion Multipliers

		#region Label

		private static void DrawLabelInput(bool undoable, string title, object o, string fieldName, float width,
			string help = null)
		{
			(bool, string) Func(string v)
			{
				v ??= "";
				var r = ImGui.InputText(GetElementTitle(title, fieldName), ref v, 256);
				return (r, v);
			}

			// Consider all input valid. Assume the EditorLabelEvent will sanitize input.
			bool ValidationFunc(string v)
			{
				return true;
			}

			DrawCachedEditReference<string>(undoable, title, o, fieldName, width, Func, StringCompare, ValidationFunc, help);
		}

		#endregion Label

		#region Scroll Rate Interpolation

		private static void DrawScrollRateInterpolationInput(bool undoable, string title, object o, string fieldName, float width,
			string help = null)
		{
			(bool, string) Func(string v)
			{
				v ??= "";
				var r = ImGui.InputText(GetElementTitle(title, fieldName), ref v, 256);
				return (r, v);
			}

			bool ValidationFunc(string v)
			{
				var (valid, _, _, _, _) = EditorInterpolatedRateAlteringEvent.IsValidScrollRateInterpolationString(v);
				return valid;
			}

			DrawCachedEditReference<string>(undoable, title, o, fieldName, width, Func, StringCompare, ValidationFunc, help);
		}

		#endregion Scroll Rate Interpolation

		#region File Browse

		public static void DrawRowFileBrowse(
			string title, object o, string fieldName, Action browseAction, Action clearAction, string help = null)
		{
			DrawRowTitleAndAdvanceColumn(title);

			// Display the text input but do not allow edits to it.
			Utils.PushDisabled();
			DrawTextInput(false, title, o, fieldName,
				Math.Max(1.0f, ImGui.GetContentRegionAvail().X - 70.0f - ImGui.GetStyle().ItemSpacing.X * 2), help);
			Utils.PopDisabled();

			ImGui.SameLine();
			if (ImGui.Button($"X{GetElementTitle(title, fieldName)}", new Vector2(20.0f, 0.0f)))
			{
				clearAction();
			}

			ImGui.SameLine();
			if (ImGui.Button($"Browse{GetElementTitle(title, fieldName)}", new Vector2(50.0f, 0.0f)))
			{
				browseAction();
			}
		}

		public static void DrawRowAutoFileBrowse(
			string title, object o, string fieldName, Action autoAction, Action browseAction, Action clearAction,
			string help = null)
		{
			DrawRowTitleAndAdvanceColumn(title);

			// Display the text input but do not allow edits to it.
			Utils.PushDisabled();
			DrawTextInput(false, title, o, fieldName,
				Math.Max(1.0f, ImGui.GetContentRegionAvail().X - 120.0f - ImGui.GetStyle().ItemSpacing.X * 3), help);
			Utils.PopDisabled();

			ImGui.SameLine();
			if (ImGui.Button($"X{GetElementTitle(title, fieldName)}", new Vector2(20.0f, 0.0f)))
			{
				clearAction();
			}

			ImGui.SameLine();
			if (ImGui.Button($"Auto{GetElementTitle(title, fieldName)}", new Vector2(50.0f, 0.0f)))
			{
				autoAction();
			}

			ImGui.SameLine();
			if (ImGui.Button($"Browse{GetElementTitle(title, fieldName)}", new Vector2(50.0f, 0.0f)))
			{
				browseAction();
			}
		}

		#endregion File Browse

		#region Input Int

		public static void DrawRowInputInt(
			bool undoable,
			string title,
			object o,
			string fieldName,
			string help = null,
			int min = int.MinValue,
			int max = int.MaxValue)
		{
			DrawRowTitleAndAdvanceColumn(title);
			DrawInputInt(undoable, title, o, fieldName, ImGui.GetContentRegionAvail().X, help, min, max);
		}

		private static void DrawInputInt(
			bool undoable,
			string title,
			object o,
			string fieldName,
			float width,
			string help = null,
			int min = int.MinValue,
			int max = int.MaxValue)
		{
			(bool, int) Func(int v)
			{
				var r = Utils.InputInt(GetElementTitle(title, fieldName), ref v, min, max);
				return (r, v);
			}

			DrawLiveEditValue<int>(undoable, title, o, fieldName, width, Func, IntCompare, help);
		}

		#endregion Input Int

		#region Drag Int

		public static bool DrawRowDragInt(
			string title,
			ref int value,
			string help = null,
			float speed = 1.0f,
			string format = "%i",
			int min = int.MinValue,
			int max = int.MaxValue)
		{
			DrawRowTitleAndAdvanceColumn(title);
			return DrawDragInt(title, ref value, ImGui.GetContentRegionAvail().X, help, speed, format, min, max);
		}

		private static bool DrawDragInt(
			string title,
			ref int value,
			float width,
			string help,
			float speed,
			string format,
			int min = int.MinValue,
			int max = int.MaxValue)
		{
			var helpText = string.IsNullOrEmpty(help) ? null : help + DragHelpText;
			var itemWidth = DrawHelp(helpText, width);
			ImGui.SetNextItemWidth(itemWidth);
			return Utils.DragInt(ref value, GetElementTitle(title), speed, format, min, max);
		}

		public static bool DrawRowDragInt(
			bool undoable,
			string title,
			object o,
			string fieldName,
			string help = null,
			float speed = 1.0f,
			string format = "%i",
			int min = int.MinValue,
			int max = int.MaxValue)
		{
			DrawRowTitleAndAdvanceColumn(title);
			return DrawDragInt(undoable, title, o, fieldName, ImGui.GetContentRegionAvail().X, help, speed, format, min, max);
		}

		private static bool DrawDragInt(
			bool undoable,
			string title,
			object o,
			string fieldName,
			float width,
			string help,
			float speed,
			string format,
			int min = int.MinValue,
			int max = int.MaxValue)
		{
			(bool, int) Func(int v)
			{
				var r = Utils.DragInt(ref v, GetElementTitle(title, fieldName), speed, format, min, max);
				return (r, v);
			}

			return DrawLiveEditValue<int>(undoable, title, o, fieldName, width, Func, IntCompare, help);
		}

		#endregion Drag Int

		#region Slider Int

		public static bool DrawRowSliderInt(
			bool undoable,
			string title,
			object o,
			string fieldName,
			int min,
			int max,
			string help = null)
		{
			DrawRowTitleAndAdvanceColumn(title);
			return DrawSliderInt(undoable, title, o, fieldName, ImGui.GetContentRegionAvail().X, min, max, help);
		}

		private static bool DrawSliderInt(
			bool undoable,
			string title,
			object o,
			string fieldName,
			float width,
			int min,
			int max,
			string help = null)
		{
			(bool, int) Func(int v)
			{
				var r = ImGui.SliderInt(GetElementTitle(title, fieldName), ref v, min, max);
				return (r, v);
			}

			return DrawLiveEditValue<int>(undoable, title, o, fieldName, width, Func, IntCompare, help);
		}

		#endregion Slider Int

		#region Slider UInt

		public static bool DrawRowSliderUInt(
			bool undoable,
			string title,
			object o,
			string fieldName,
			uint min,
			uint max,
			string format = null,
			ImGuiSliderFlags flags = ImGuiSliderFlags.None,
			string help = null)
		{
			DrawRowTitleAndAdvanceColumn(title);
			return DrawSliderUInt(undoable, title, o, fieldName, ImGui.GetContentRegionAvail().X, min, max, format, flags, help);
		}

		private static bool DrawSliderUInt(
			bool undoable,
			string title,
			object o,
			string fieldName,
			float width,
			uint min,
			uint max,
			string format = null,
			ImGuiSliderFlags flags = ImGuiSliderFlags.None,
			string help = null)
		{
			(bool, uint) Func(uint v)
			{
				var r = Utils.SliderUInt(GetElementTitle(title, fieldName), ref v, min, max, format, flags);
				return (r, v);
			}

			return DrawLiveEditValue<uint>(undoable, title, o, fieldName, width, Func, UIntCompare, help);
		}

		#endregion Slider UInt

		#region Slider Float

		public static bool DrawRowSliderFloat(
			bool undoable,
			string title,
			object o,
			string fieldName,
			float min,
			float max,
			string help = null,
			string format = "%.3f",
			ImGuiSliderFlags flags = ImGuiSliderFlags.None)
		{
			DrawRowTitleAndAdvanceColumn(title);
			return DrawSliderFloat(undoable, title, o, fieldName, ImGui.GetContentRegionAvail().X, min, max, help, format, flags);
		}

		private static bool DrawSliderFloat(
			bool undoable,
			string title,
			object o,
			string fieldName,
			float width,
			float min,
			float max,
			string help = null,
			string format = "%.3f",
			ImGuiSliderFlags flags = ImGuiSliderFlags.None)
		{
			(bool, float) Func(float v)
			{
				var r = ImGui.SliderFloat(GetElementTitle(title, fieldName), ref v, min, max, format, flags);
				return (r, v);
			}

			return DrawLiveEditValue<float>(undoable, title, o, fieldName, width, Func, FloatCompare, help);
		}

		public static void DrawRowSliderFloatWithReset(
			bool undoable,
			string title,
			object o,
			string fieldName,
			float min,
			float max,
			float resetValue,
			string help = null,
			string format = "%.3f",
			ImGuiSliderFlags flags = ImGuiSliderFlags.None)
		{
			DrawRowTitleAndAdvanceColumn(title);

			// Slider
			DrawSliderFloat(undoable, title, o, fieldName,
				ImGui.GetContentRegionAvail().X - 50.0f - ImGui.GetStyle().ItemSpacing.X, min, max, help, format, flags);

			// Reset
			ImGui.SameLine();
			if (ImGui.Button($"Reset{GetElementTitle(title, fieldName)}", new Vector2(50.0f, 0.0f)))
			{
				var value = GetValueFromFieldOrProperty<float>(o, fieldName);
				if (!resetValue.FloatEquals(value))
				{
					if (undoable)
						ActionQueue.Instance.Do(new ActionSetObjectFieldOrPropertyValue<float>(o, fieldName, resetValue));
					else
						SetFieldOrPropertyToValue(o, fieldName, resetValue);
				}
			}
		}

		#endregion Slider Float

		#region Drag Drouble

		public static bool DrawRowDragDouble(
			string title,
			ref double value,
			string help = null,
			float speed = 0.0001f,
			string format = "%.6f",
			double min = double.MinValue,
			double max = double.MaxValue)
		{
			DrawRowTitleAndAdvanceColumn(title);
			return DrawDragDouble(title, ref value, ImGui.GetContentRegionAvail().X, help, speed, format, min, max);
		}

		private static bool DrawDragDouble(
			string title,
			ref double value,
			float width,
			string help,
			float speed,
			string format,
			double min = double.MinValue,
			double max = double.MaxValue)
		{
			var helpText = string.IsNullOrEmpty(help) ? null : help + DragHelpText;
			var itemWidth = DrawHelp(helpText, width);
			ImGui.SetNextItemWidth(itemWidth);
			return Utils.DragDouble(ref value, GetElementTitle(title), speed, format, min, max);
		}

		public static bool DrawRowDragDouble(
			bool undoable,
			string title,
			object o,
			string fieldName,
			string help = null,
			float speed = 0.0001f,
			string format = "%.6f",
			double min = double.MinValue,
			double max = double.MaxValue)
		{
			DrawRowTitleAndAdvanceColumn(title);
			return DrawDragDouble(undoable, title, o, fieldName, ImGui.GetContentRegionAvail().X, help, speed, format, min, max);
		}

		public static void DrawRowDragDoubleWithEnabledCheckbox(
			bool undoable,
			string title,
			object o,
			string fieldName,
			string enabledFieldName,
			string help = null,
			float speed = 0.0001f,
			string format = "%.6f",
			double min = double.MinValue,
			double max = double.MaxValue)
		{
			title ??= "";

			DrawRowTitleAndAdvanceColumn(title);

			bool enabled;

			var controlWidth = ImGui.GetContentRegionAvail().X - 20.0f - ImGui.GetStyle().ItemSpacing.X;

			// Draw the checkbox for enabling the other control.
			if (DrawCheckbox(false, title + "check", o, enabledFieldName, 20.0f, help))
			{
				if (undoable)
				{
					enabled = GetValueFromFieldOrProperty<bool>(o, enabledFieldName);

					// If disabling the checkbox enqueue an action for undoing both the bool
					// and the setting of the double value to 0.
					if (!enabled)
					{
						ActionQueue.Instance.Do(new ActionSetObjectFieldOrPropertyValue<double, bool>(
							o,
							fieldName, 0.0, GetValueFromFieldOrProperty<double>(o, fieldName),
							enabledFieldName, enabled, !enabled));
					}

					// If enabling the checkbox we only need to enqueue an action for the checkbox bool.
					else
					{
						ActionQueue.Instance.Do(new ActionSetObjectFieldOrPropertyValue<bool>(
							o,
							enabledFieldName, enabled, !enabled));
					}
				}
			}

			enabled = GetValueFromFieldOrProperty<bool>(o, enabledFieldName);
			if (!enabled)
				Utils.PushDisabled();

			// Control for the double value.
			ImGui.SameLine();
			DrawDragDouble(undoable, title, o, fieldName, controlWidth, null, speed, format, min, max);

			if (!enabled)
				Utils.PopDisabled();
		}

		private static bool DrawDragDouble(
			bool undoable,
			string title,
			object o,
			string fieldName,
			float width,
			string help,
			float speed,
			string format,
			double min = double.MinValue,
			double max = double.MaxValue)
		{
			(bool, double) Func(double v)
			{
				var r = Utils.DragDouble(ref v, GetElementTitle(title, fieldName), speed, format, min, max);
				return (r, v);
			}

			return DrawLiveEditValue<double>(undoable, title, o, fieldName, width, Func, DoubleCompare, help);
		}

		#endregion Drag Double

		#region Enum

		public static bool DrawRowEnum<T>(bool undoable, string title, object o, string fieldName, string help = null)
			where T : struct, Enum
		{
			DrawRowTitleAndAdvanceColumn(title);
			return DrawEnum<T>(undoable, title, o, fieldName, ImGui.GetContentRegionAvail().X, null, help);
		}

		public static bool DrawRowEnum<T>(bool undoable, string title, object o, string fieldName, T[] allowedValues,
			string help = null) where T : struct, Enum
		{
			DrawRowTitleAndAdvanceColumn(title);
			return DrawEnum<T>(undoable, title, o, fieldName, ImGui.GetContentRegionAvail().X, allowedValues, help);
		}

		public static bool DrawEnum<T>(bool undoable, string title, object o, string fieldName, float width, T[] allowedValues,
			string help = null) where T : struct, Enum
		{
			var value = GetValueFromFieldOrProperty<T>(o, fieldName);

			var itemWidth = DrawHelp(help, width);
			ImGui.SetNextItemWidth(itemWidth);
			T newValue = value;
			var ret = false;
			if (allowedValues != null)
				ret = Utils.ComboFromEnum(GetElementTitle(title, fieldName), ref newValue, allowedValues,
					GetElementTitle(title, fieldName));
			else
				ret = Utils.ComboFromEnum(GetElementTitle(title, fieldName), ref newValue);
			if (ret)
			{
				if (!newValue.Equals(value))
				{
					if (undoable)
						ActionQueue.Instance.Do(new ActionSetObjectFieldOrPropertyValue<T>(o, fieldName, newValue, value));
					else
						SetFieldOrPropertyToValue<T>(o, fieldName, newValue);
				}
			}

			return ret;
		}

		#endregion Enum

		#region Selectable

		public static bool DrawRowSelectableTree<T>(bool undoable, string title, object o, string fieldName, string help = null)
			where T : Enum
		{
			DrawRowTitleAndAdvanceColumn(title);
			return DrawSelectableTree<T>(undoable, title, o, fieldName, ImGui.GetContentRegionAvail().X, help);
		}

		public static bool DrawSelectableTree<T>(bool undoable, string title, object o, string fieldName, float width,
			string help = null) where T : Enum
		{
			var value = GetValueFromFieldOrProperty<bool[]>(o, fieldName);

			var itemWidth = DrawHelp(help, width);
			ImGui.SetNextItemWidth(itemWidth);

			(var ret, var originalValues) = Utils.SelectableTree<T>(title, ref value);
			if (ret && undoable)
				ActionQueue.Instance.Do(
					new ActionSetObjectFieldOrPropertyReference<bool[]>(o, fieldName, (bool[])value.Clone(), originalValues));

			return ret;
		}

		#endregion Selectable

		#region Color Edit 3

		public static void DrawRowColorEdit3(
			bool undoable,
			string title,
			object o,
			string fieldName,
			ImGuiColorEditFlags flags,
			string help = null)
		{
			DrawRowTitleAndAdvanceColumn(title);
			DrawColorEdit3(undoable, title, o, fieldName, ImGui.GetContentRegionAvail().X, flags, help);
		}

		private static void DrawColorEdit3(
			bool undoable,
			string title,
			object o,
			string fieldName,
			float width,
			ImGuiColorEditFlags flags,
			string help = null)
		{
			(bool, Vector3) Func(Vector3 v)
			{
				var r = ImGui.ColorEdit3(GetElementTitle(title, fieldName), ref v, flags);
				return (r, v);
			}

			DrawLiveEditValue<Vector3>(undoable, title, o, fieldName, width, Func, Vector3Compare, help);
		}

		#endregion Color Edit 3

		#region Display Tempo

		/// <summary>
		/// Draws a row for a custom set of controls to edit a DisplayTempo object.
		/// </summary>
		/// <param name="undoable">Whether operations should be undoable or not.</param>
		/// <param name="displayTempo">DisplayTempo object to control.</param>
		/// <param name="actualMinTempo">Actual min tempo of the Chart.</param>
		/// <param name="actualMaxTempo">Actual max tempo of the Chart.</param>
		public static void DrawRowDisplayTempo(
			bool undoable,
			DisplayTempo displayTempo,
			double actualMinTempo,
			double actualMaxTempo)
		{
			DrawRowTitleAndAdvanceColumn("Display Tempo");

			const float enumWidth = 120.0f;
			const float toWidth = 14.0f;
			var spacing = ImGui.GetStyle().ItemSpacing.X;

			var tempoControlWidth = ImGui.GetContentRegionAvail().X - enumWidth - spacing;
			var splitTempoWidth = Math.Max(1.0f, (ImGui.GetContentRegionAvail().X - enumWidth - toWidth - spacing * 3.0f) * 0.5f);

			// Draw an enum for choosing the DisplayTempoMode.
			DrawEnum<DisplayTempoMode>(undoable, "", displayTempo, nameof(DisplayTempo.Mode), enumWidth, null,
				"How the tempo for this chart should be displayed." +
				"\nRandom:    The actual tempo will be hidden and replaced with a random display." +
				"\nSpecified: A specified tempo or tempo range will be displayed." +
				"\n           This is a good option when tempo gimmicks would result in a misleading actual tempo range." +
				"\nActual:    The actual tempo or tempo range will be displayed.");

			// The remainder of the row depends on the mode.
			switch (displayTempo.Mode)
			{
				// For a Random display, just draw a disabled InputText with "???".
				case DisplayTempoMode.Random:
				{
					Utils.PushDisabled();
					var text = "???";
					ImGui.SetNextItemWidth(Math.Max(1.0f, tempoControlWidth));
					ImGui.SameLine();
					ImGui.InputText("", ref text, 4);
					Utils.PopDisabled();
					break;
				}

				// For a Specified display, draw the specified range.
				case DisplayTempoMode.Specified:
				{
					// DragDouble for the min.
					ImGui.SameLine();
					ImGui.SetNextItemWidth(splitTempoWidth);
					DrawDragDouble(undoable, "", displayTempo, nameof(DisplayTempo.SpecifiedTempoMin), splitTempoWidth, null,
						0.001f, "%.6f");

					// "to" text to split the min and max.
					ImGui.SameLine();
					Utils.Text("to", toWidth);

					// Checkbox for whether or not to use a distinct max.
					ImGui.SameLine();
					if (DrawCheckbox(false, "", displayTempo, nameof(DisplayTempo.ShouldAllowEditsOfMax), 10.0f))
					{
						if (undoable)
						{
							// Enqueue a custom action so that the ShouldAllowEditsOfMax and previous max tempo can be undone together.
							ActionQueue.Instance.Do(
								new ActionSetDisplayTempoAllowEditsOfMax(displayTempo, displayTempo.ShouldAllowEditsOfMax));
						}
					}

					// If not using a distinct max, disable the max DragDouble and ensure that the max is set to the min.
					if (!displayTempo.ShouldAllowEditsOfMax)
					{
						Utils.PushDisabled();

						if (!displayTempo.SpecifiedTempoMin.DoubleEquals(displayTempo.SpecifiedTempoMax))
							displayTempo.SpecifiedTempoMax = displayTempo.SpecifiedTempoMin;
					}

					// DragDouble for the max.
					ImGui.SameLine();
					ImGui.SetNextItemWidth(splitTempoWidth);
					DrawDragDouble(undoable, "", displayTempo, nameof(DisplayTempo.SpecifiedTempoMax),
						ImGui.GetContentRegionAvail().X, null,
						0.001f, "%.6f");

					// Pop the disabled setting if we pushed it before.
					if (!displayTempo.ShouldAllowEditsOfMax)
					{
						Utils.PopDisabled();
					}

					break;
				}

				case DisplayTempoMode.Actual:
				{
					// The controls for the actual tempo are always disabled.
					Utils.PushDisabled();

					// If the actual tempo is one value then just draw one DragDouble.
					if (actualMinTempo.DoubleEquals(actualMaxTempo))
					{
						ImGui.SetNextItemWidth(Math.Max(1.0f, tempoControlWidth));
						ImGui.SameLine();
						Utils.DragDouble(ref actualMinTempo, "");
					}

					// If the actual tempo is a range then draw the min and max.
					else
					{
						// DragDouble for the min.
						ImGui.SetNextItemWidth(splitTempoWidth);
						ImGui.SameLine();
						Utils.DragDouble(ref actualMinTempo, "");

						// "to" text to split the min and max.
						ImGui.SameLine();
						ImGui.Text("to");

						// DragDouble for the max.
						ImGui.SetNextItemWidth(splitTempoWidth);
						ImGui.SameLine();
						Utils.DragDouble(ref actualMaxTempo, "");
					}

					Utils.PopDisabled();
					break;
				}
			}
		}

		#endregion Display Tempo

		#region Misc Editor Events

		public static double GetMiscEditorEventHeight(bool withBorder = false)
		{
			return ImGui.GetStyle().FramePadding.Y * 2 + ImGui.GetFontSize() + (withBorder ? 2 : 0);
		}

		public static double GetMiscEditorEventDragIntWidgetWidth(int i, string format)
		{
			return GetMiscEditorEventStringWidth(Utils.FormatImGuiInt(format, i));
		}

		public static void MiscEditorEventDragIntWidget(
			string id,
			EditorEvent e,
			string fieldName,
			int x,
			int y,
			int width,
			uint colorABGR,
			bool selected,
			bool canBeDeleted,
			float speed,
			string format,
			float alpha,
			string help,
			int min = int.MinValue,
			int max = int.MaxValue)
		{
			void Func(float elementWidth)
			{
				DrawDragInt(true, $"##{id}", e, fieldName, elementWidth, "", speed, format, min, max);
			}

			MiscEditorEventWidget(id, e, x, y, width, colorABGR, selected, canBeDeleted, alpha, help, Func);
		}

		public static double GetMiscEditorEventDragDoubleWidgetWidth(double d, string format)
		{
			return GetMiscEditorEventStringWidth(Utils.FormatImGuiDouble(format, d));
		}

		public static void MiscEditorEventDragDoubleWidget(
			string id,
			EditorEvent e,
			string fieldName,
			int x,
			int y,
			int width,
			uint colorABGR,
			bool selected,
			bool canBeDeleted,
			float speed,
			string format,
			float alpha,
			string help,
			double min=double.MinValue,
			double max=double.MaxValue)
		{
			void Func(float elementWidth)
			{
				DrawDragDouble(true, $"##{id}", e, fieldName, elementWidth, "", speed, format, min, max);
			}

			MiscEditorEventWidget(id, e, x, y, width, colorABGR, selected, canBeDeleted, alpha, help, Func);
		}

		public static double GetMiscEditorEventStringWidth(string s)
		{
			ImGui.PushFont(ImGuiFont);
			var width = ImGui.CalcTextSize(s).X + Utils.CloseWidth;
			ImGui.PopFont();
			return width;
		}

		public static void MiscEditorEventTimeSignatureWidget(
			string id,
			EditorEvent e,
			string fieldName,
			int x,
			int y,
			int width,
			uint colorABGR,
			bool selected,
			bool canBeDeleted,
			float alpha,
			string help)
		{
			void Func(float elementWidth)
			{
				DrawTimeSignatureInput(true, $"##{id}", e, fieldName, elementWidth);
			}

			MiscEditorEventWidget(id, e, x, y, width, colorABGR, selected, canBeDeleted, alpha, help, Func);
		}

		public static void MiscEditorEventMultipliersWidget(
			string id,
			EditorEvent e,
			string fieldName,
			int x,
			int y,
			int width,
			uint colorABGR,
			bool selected,
			bool canBeDeleted,
			float alpha,
			string help)
		{
			void Func(float elementWidth)
			{
				DrawMultipliersInput(true, $"##{id}", e, fieldName, elementWidth);
			}

			MiscEditorEventWidget(id, e, x, y, width, colorABGR, selected, canBeDeleted, alpha, help, Func);
		}

		public static void MiscEditorEventLabelWidget(
			string id,
			EditorEvent e,
			string fieldName,
			int x,
			int y,
			int width,
			uint colorABGR,
			bool selected,
			bool canBeDeleted,
			float alpha,
			string help)
		{
			void Func(float elementWidth)
			{
				DrawLabelInput(true, $"##{id}", e, fieldName, elementWidth);
			}

			MiscEditorEventWidget(id, e, x, y, width, colorABGR, selected, canBeDeleted, alpha, help, Func);
		}

		public static void MiscEditorEventScrollRateInterpolationInputWidget(
			string id,
			EditorEvent e,
			string fieldName,
			int x,
			int y,
			int width,
			uint colorABGR,
			bool selected,
			bool canBeDeleted,
			float alpha,
			string help)
		{
			void Func(float elementWidth)
			{
				DrawScrollRateInterpolationInput(true, $"##{id}", e, fieldName, elementWidth);
			}

			MiscEditorEventWidget(id, e, x, y, width, colorABGR, selected, canBeDeleted, alpha, help, Func);
		}

		private static void MiscEditorEventWidget(
			string id,
			EditorEvent e,
			int x,
			int y,
			int width,
			uint colorABGR,
			bool selected,
			bool canBeDeleted,
			float alpha,
			string help,
			Action<float> func)
		{
			var colorPushCount = 0;
			var drawHelp = false;
			
			// Selected coloring.
			if (selected)
			{
				ImGui.PushStyleColor(ImGuiCol.WindowBg, 0x484848FF);
				ImGui.PushStyleColor(ImGuiCol.Border, 0xFFFFFFFF);
				colorPushCount += 2;
			}

			// Color the frame background to help differentiate controls.
			ImGui.PushStyleColor(ImGuiCol.FrameBg, colorABGR);
			colorPushCount += 1;

			// If fading out, multiply key window elements by the alpha value.
			if (alpha < 1.0f)
			{
				Utils.PushAlpha(ImGuiCol.WindowBg, alpha);
				Utils.PushAlpha(ImGuiCol.Button, alpha);
				Utils.PushAlpha(ImGuiCol.FrameBg, alpha);
				Utils.PushAlpha(ImGuiCol.Text, alpha);
				Utils.PushAlpha(ImGuiCol.Border, alpha);
				colorPushCount += 5;
			}
			
			var height = (int)GetMiscEditorEventHeight(true);

			// Record window size and padding values so we can edit and restore them.
			var originalWindowPaddingX = ImGui.GetStyle().WindowPadding.X;
			var originalWindowPaddingY = ImGui.GetStyle().WindowPadding.Y;
			var originalMinWindowSize = ImGui.GetStyle().WindowMinSize;
			var originalItemSpacingX = ImGui.GetStyle().ItemSpacing.X;
			var originalInnerItemSpacingX = ImGui.GetStyle().ItemInnerSpacing.X;
			var originalFramePaddingX = ImGui.GetStyle().FramePadding.X;

			// Set the padding and spacing so we can draw a table with precise dimensions.
			ImGui.GetStyle().WindowPadding.X = 1;
			ImGui.GetStyle().WindowPadding.Y = 1;
			ImGui.GetStyle().WindowMinSize = new Vector2(width, height);
			ImGui.GetStyle().ItemSpacing.X = 0;
			ImGui.GetStyle().ItemInnerSpacing.X = 0;
			ImGui.GetStyle().FramePadding.X = 0;

			// Start the window.
			ImGui.SetNextWindowPos(new Vector2(x, y));
			ImGui.SetNextWindowSize(new Vector2(width, height));
			ImGui.Begin($"##Widget{id}",
				ImGuiWindowFlags.NoMove
				| ImGuiWindowFlags.NoDecoration
				| ImGuiWindowFlags.NoSavedSettings
				| ImGuiWindowFlags.NoDocking
				| ImGuiWindowFlags.NoBringToFrontOnFocus
				| ImGuiWindowFlags.NoFocusOnAppearing);

			var elementWidth = width - Utils.CloseWidth;

			// Draw the control.
			func(elementWidth);

			// Record whether or not we should draw help text.
			if (!string.IsNullOrEmpty(help) && ImGui.IsItemHovered())
				drawHelp = true;

			// Delete button
			ImGui.SameLine();
			if (!canBeDeleted)
				Utils.PushDisabled();
			if (ImGui.Button($"X##{id}", new Vector2(Utils.CloseWidth, 0.0f)))
				ActionQueue.Instance.Do(new ActionDeleteEditorEvent(e));
			if (!canBeDeleted)
				Utils.PopDisabled();

			ImGui.End();

			// Restore window size and padding values.
			ImGui.GetStyle().FramePadding.X = originalFramePaddingX;
			ImGui.GetStyle().ItemInnerSpacing.X = originalInnerItemSpacingX;
			ImGui.GetStyle().ItemSpacing.X = originalItemSpacingX;
			ImGui.GetStyle().WindowPadding.X = originalWindowPaddingX;
			ImGui.GetStyle().WindowPadding.Y = originalWindowPaddingY;
			ImGui.GetStyle().WindowMinSize = originalMinWindowSize;

			ImGui.PopStyleColor(colorPushCount);

			// Draw help after restoring style elements.
			if (drawHelp)
			{
				ImGui.BeginTooltip();
				ImGui.PushTextWrapPos(ImGui.GetFontSize() * 80.0f);
				ImGui.TextUnformatted(help);
				ImGui.PopTextWrapPos();
				ImGui.EndTooltip();
			}
		}

		#endregion Misc Editor Events

		#region Compare Functions

		private static bool IntCompare(int a, int b)
		{
			return a == b;
		}

		private static bool UIntCompare(uint a, uint b)
		{
			return a == b;
		}

		private static bool FloatCompare(float a, float b)
		{
			return a.FloatEquals(b);
		}

		private static bool DoubleCompare(double a, double b)
		{
			return a.DoubleEquals(b);
		}

		private static bool StringCompare(string a, string b)
		{
			return a == b;
		}

		private static bool Vector3Compare(Vector3 a, Vector3 b)
		{
			return a == b;
		}

		private static bool TimeSignatureCompare(TimeSignature a, TimeSignature b)
		{
			return a.Signature.Equals(b.Signature);
		}

		#endregion


		private static void SetFieldOrPropertyToValue<T>(object o, string fieldOrPropertyName, T value)
		{
			if (IsField(o, fieldOrPropertyName))
				o.GetType().GetField(fieldOrPropertyName, BindingFlags.Public | BindingFlags.Instance).SetValue(o, value);
			else
				o.GetType().GetProperty(fieldOrPropertyName, BindingFlags.Public | BindingFlags.Instance).SetValue(o, value);
		}

		private static T GetValueFromFieldOrProperty<T>(object o, string fieldOrPropertyName)
		{
			var value = default(T);
			if (o == null)
				return value;

			var isField = IsField(o, fieldOrPropertyName);
			if (isField)
				value = (T)o.GetType().GetField(fieldOrPropertyName)?.GetValue(o);
			else
				value = (T)o.GetType().GetProperty(fieldOrPropertyName)?.GetValue(o);
			return value;
		}

		private static bool IsField(object o, string fieldOrPropertyName)
		{
			return o.GetType().GetField(fieldOrPropertyName, BindingFlags.Public | BindingFlags.Instance) != null;
		}

		private static string GetElementTitle(string title, string fieldOrPropertyName)
		{
			return $"##{title}{fieldOrPropertyName}{CacheKeyPrefix}";
		}

		private static string GetElementTitle(string title)
		{
			return $"##{title}{CacheKeyPrefix}";
		}

		private static string GetCacheKey(string title, string fieldOrPropertyName)
		{
			return $"{CacheKeyPrefix}{title}{fieldOrPropertyName}";
		}

		private static T GetCachedValue<T>(string key)
		{
			return (T)Cache[key];
		}

		private static bool TryGetCachedValue<T>(string key, out T value)
		{
			value = default;
			var result = Cache.TryGetValue(key, out var outValue);
			if (result)
				value = (T)outValue;
			return result;
		}

		private static void SetCachedValue<T>(string key, T value)
		{
			Cache[key] = value;
		}

		public static float DrawHelp(string help, float width)
		{
			var hasHelp = !string.IsNullOrEmpty(help);
			var remainderWidth = hasHelp ? Math.Max(1.0f, width - Utils.HelpWidth - ImGui.GetStyle().ItemSpacing.X) : width;
			//var remainderWidth = Math.Max(1.0f, width - Utils.HelpWidth - ImGui.GetStyle().ItemSpacing.X);

			if (hasHelp)
			{
				Utils.HelpMarker(help);
				ImGui.SameLine();
			}
			//else
			//{
			//	ImGui.Dummy(new Vector2(Utils.HelpWidth, 1));
			//	ImGui.SameLine();
			//}

			return remainderWidth;
		}

		private static bool DrawCachedEditReference<T>(
			bool undoable,
			string title,
			object o,
			string fieldName,
			float width,
			Func<T, (bool, T)> imGuiFunc,
			Func<T, T, bool> compareFunc,
			Func<T, bool> validationFunc = null,
			string help = null) where T : class, ICloneable
		{
			// Get the cached value.
			var cacheKey = GetCacheKey(title, fieldName);
			if (!TryGetCachedValue(cacheKey, out T cachedValue))
			{
				cachedValue = default;
				SetCachedValue(cacheKey, cachedValue);
			}

			// Get the current value.
			var value = GetValueFromFieldOrProperty<T>(o, fieldName);

			// Draw the help marker and determine the remaining width.
			var textWidth = DrawHelp(help, width);
			ImGui.SetNextItemWidth(textWidth);

			// Draw the ImGui control using the cached value.
			// We do not want to see the effect of changing the value outside of the control
			// until after editing is complete.
			var (result, resultValue) = imGuiFunc(cachedValue);
			cachedValue = resultValue;
			SetCachedValue(cacheKey, cachedValue);

			// At the moment of releasing the control, enqueue an event to update the value to the
			// newly edited cached value.
			if (ImGui.IsItemDeactivatedAfterEdit()
			    && o != null
				&& (validationFunc == null || validationFunc(cachedValue))
				&& !compareFunc(cachedValue, value))
			{
				if (undoable)
					ActionQueue.Instance.Do(new ActionSetObjectFieldOrPropertyReference<T>(o, fieldName, (T)cachedValue.Clone()));
				else
					SetFieldOrPropertyToValue(o, fieldName, cachedValue);
				value = cachedValue;
			}

			// Always update the cached value if the control is not active.
			if (!ImGui.IsItemActive())
				SetCachedValue(cacheKey, value);

			return result;
		}

		/// <summary>
		/// Draws an ImGui control where the value bound to the control is edited directly
		/// as the user interacts with the control.
		/// </summary>
		/// <typeparam name="T">Type of value edited.</typeparam>
		/// <param name="undoable">
		/// Whether or not the edits to the value should be enqueued as action for undo or not.
		/// </param>
		/// <param name="title"></param>
		/// <param name="o"></param>
		/// <param name="fieldName"></param>
		/// <param name="width"></param>
		/// <param name="imGuiFunc">
		/// Function to wrap the ImGui control.
		/// Takes the value to be edited.
		/// Returns a tuple where the first entry is the return value of the ImGui control and
		/// the second entry is the new value after being edited. This returned because Funcs
		/// cannot take values by ref.
		/// </param>
		/// <param name="compareFunc">
		/// Function to compare to values of type T for equality.
		/// Returns true if equal and false otherwise.
		/// Used to avoid updating values or enqueueing actions when no changes occur.
		/// </param>
		/// <param name="help"></param>
		/// <returns></returns>
		private static bool DrawLiveEditValue<T>(
			bool undoable,
			string title,
			object o,
			string fieldName,
			float width,
			Func<T, (bool, T)> imGuiFunc,
			Func<T, T, bool> compareFunc,
			string help = null) where T : struct
		{
			var cacheKey = GetCacheKey(title, fieldName);

			// Get the current value.
			var value = GetValueFromFieldOrProperty<T>(o, fieldName);
			var beforeValue = value;

			// Draw the help marker and determine the remaining width.
			var itemWidth = DrawHelp(help, width);
			ImGui.SetNextItemWidth(itemWidth);

			// Draw a the ImGui control using the actual value, not the cached value.
			// We want to see the effect of changing this value immediately.
			// Do not however enqueue an EditorAction for this change yet.
			var (result, resultValue) = imGuiFunc(value);
			value = resultValue;
			if (result)
			{
				SetFieldOrPropertyToValue(o, fieldName, value);
			}

			// At the moment of activating the ImGui control, record the current value so we can undo to it later.
			if (ImGui.IsItemActivated())
			{
				SetCachedValue(cacheKey, beforeValue);
			}

			// At the moment of releasing the control, enqueue an event so we can undo to the previous value.
			if (undoable)
			{
				if (ImGui.IsItemDeactivatedAfterEdit() && o != null && !compareFunc(value, GetCachedValue<T>(cacheKey)))
				{
					ActionQueue.Instance.Do(
						new ActionSetObjectFieldOrPropertyValue<T>(o, fieldName, value, GetCachedValue<T>(cacheKey)));
				}
			}

			return result;
		}
	}
}

using System;
using System.Collections.Generic;
using System.Reflection;
using ImGuiNET;
using System.Numerics;

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
		private const string DragDoubleHelpText = "\nShift+drag for large adjustments.\nAlt+drag for small adjustments.";

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
		
		private static void DrawRowTitleAndAdvanceColumn(string title)
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

		public static bool DrawRowCheckbox(string title, object o, string fieldName, string help = null)
		{
			DrawRowTitleAndAdvanceColumn(title);
			return DrawCheckbox(title, o, fieldName, ImGui.GetContentRegionAvail().X, help);
		}

		public static bool DrawRowCheckboxUndoable(string title, object o, string fieldName, string help = null)
		{
			DrawRowTitleAndAdvanceColumn(title);
			return DrawCheckboxUndoable(title, o, fieldName, ImGui.GetContentRegionAvail().X, help);
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
				SetFieldOrPropertyToValue(o, fieldName, value);
			}
			return ret;
		}

		private static bool DrawCheckboxUndoable(
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
				EnqueueSetFieldOrPropertyAction(o, fieldName, value);
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

		public static void DrawRowTextInputWithTransliteration(bool undoable, string title, object o, string fieldName, string transliterationFieldName, string help = null)
		{
			DrawRowTitleAndAdvanceColumn(title);
			DrawTextInput(undoable, title, o, fieldName, ImGui.GetContentRegionAvail().X * 0.5f, help);
			ImGui.SameLine();
			DrawTextInput(undoable, "Transliteration", o, transliterationFieldName, ImGui.GetContentRegionAvail().X,
				"Optional text to use when sorting by this value.\nStepMania sorts values lexicographically, preferring transliterations.");
		}

		private static void DrawTextInput(bool undoable, string title, object o, string fieldName, float width, string help = null)
		{
			(bool, string) Func(string v)
			{
				v ??= "";
				var r = ImGui.InputTextWithHint(GetElementTitle(title, fieldName), title, ref v, 256);
				return (r, v);
			}
			DrawCachedEdit<string>(undoable, title, o, fieldName, width, Func, StringCompare, help);
		}

		#endregion Text Input

		#region File Browse

		public static void DrawRowFileBrowse(
			string title, object o, string fieldName, Action browseAction, Action clearAction, string help = null)
		{
			DrawRowTitleAndAdvanceColumn(title);

			// Display the text input but do not allow edits to it.
			Utils.PushDisabled();
			DrawTextInput(false, title, o, fieldName, Math.Max(1.0f, ImGui.GetContentRegionAvail().X - 70.0f - ImGui.GetStyle().ItemSpacing.X * 2), help);
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
			string title, object o, string fieldName, Action autoAction, Action browseAction, Action clearAction, string help = null)
		{
			DrawRowTitleAndAdvanceColumn(title);

			// Display the text input but do not allow edits to it.
			Utils.PushDisabled();
			DrawTextInput(false, title, o, fieldName, Math.Max(1.0f, ImGui.GetContentRegionAvail().X - 120.0f - ImGui.GetStyle().ItemSpacing.X * 3), help);
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
			bool useMin = false,
			int min = 0,
			bool useMax = false,
			int max = 0)
		{
			DrawRowTitleAndAdvanceColumn(title);
			DrawInputInt(undoable, title, o, fieldName, ImGui.GetContentRegionAvail().X, help, useMin, min, useMax, max);
		}

		private static void DrawInputInt(
			bool undoable,
			string title,
			object o,
			string fieldName,
			float width,
			string help = null,
			bool useMin = false,
			int min = 0,
			bool useMax = false,
			int max = 0)
		{
			(bool, int) Func(int v)
			{
				var r = Utils.InputInt(GetElementTitle(title, fieldName), ref v, useMin, min, useMax, max);
				return (r, v);
			}
			DrawLiveEdit<int>(undoable, title, o, fieldName, width, Func, IntCompare, help);
		}

		#endregion Input Int

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
			return DrawLiveEdit<int>(undoable, title, o, fieldName, width, Func, IntCompare, help);
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
			return DrawLiveEdit<uint>(undoable, title, o, fieldName, width, Func, UIntCompare, help);
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
			return DrawLiveEdit<float>(undoable, title, o, fieldName, width, Func, FloatCompare, help);
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
			DrawSliderFloat(undoable, title, o, fieldName, ImGui.GetContentRegionAvail().X - 50.0f - ImGui.GetStyle().ItemSpacing.X, min, max, help, format, flags);

			// Reset
			ImGui.SameLine();
			if (ImGui.Button($"Reset{GetElementTitle(title, fieldName)}", new Vector2(50.0f, 0.0f)))
			{
				var value = GetValueFromFieldOrProperty<float>(o, fieldName);
				if (!resetValue.FloatEquals(value))
				{
					if (undoable)
						EnqueueSetFieldOrPropertyAction(o, fieldName, resetValue);
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
			bool useMin = false,
			double min = 0.0,
			bool useMax = false,
			double max = 0.0)
		{
			DrawRowTitleAndAdvanceColumn(title);
			return DrawDragDouble(title, ref value, ImGui.GetContentRegionAvail().X, help, speed, format, useMin, min, useMax, max);
		}
		
		private static bool DrawDragDouble(
			string title,
			ref double value,
			float width,
			string help,
			float speed,
			string format,
			bool useMin = false,
			double min = 0.0,
			bool useMax = false,
			double max = 0.0)
		{
			var helpText = string.IsNullOrEmpty(help) ? null : help + DragDoubleHelpText;
			var itemWidth = DrawHelp(helpText, width);
			ImGui.SetNextItemWidth(itemWidth);
			return Utils.DragDouble(ref value, GetElementTitle(title), speed, format, useMin, min, useMax, max);
		}

		public static bool DrawRowDragDouble(
			bool undoable,
			string title,
			object o,
			string fieldName,
			string help = null,
			float speed = 0.0001f,
			string format = "%.6f",
			bool useMin = false,
			double min = 0.0,
			bool useMax = false,
			double max = 0.0)
		{
			DrawRowTitleAndAdvanceColumn(title);
			return DrawDragDouble(true, title, o, fieldName, ImGui.GetContentRegionAvail().X, help, speed, format, useMin, min, useMax, max);
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
			bool useMin = false,
			double min = 0.0,
			bool useMax = false,
			double max = 0.0)
		{
			(bool, double) Func(double v)
			{
				var r = Utils.DragDouble(ref v, GetElementTitle(title, fieldName), speed, format, useMin, min, useMax, max);
				return (r, v);
			}
			return DrawLiveEdit<double>(undoable, title, o, fieldName, width, Func, DoubleCompare, help);
		}

		#endregion Drag Double

		#region Enum

		public static bool DrawRowEnum<T>(bool undoable, string title, object o, string fieldName, string help = null) where T : Enum
		{
			DrawRowTitleAndAdvanceColumn(title);
			return DrawEnum<T>(undoable, title, o, fieldName, ImGui.GetContentRegionAvail().X, null, help);
		}

		public static bool DrawRowEnum<T>(bool undoable, string title, object o, string fieldName, T[] allowedValues, string help = null) where T : Enum
		{
			DrawRowTitleAndAdvanceColumn(title);
			return DrawEnum<T>(undoable, title, o, fieldName, ImGui.GetContentRegionAvail().X, allowedValues, help);
		}

		public static bool DrawEnum<T>(bool undoable, string title, object o, string fieldName, float width, T[] allowedValues, string help = null) where T : Enum
		{
			var value = GetValueFromFieldOrProperty<T>(o, fieldName);

			var itemWidth = DrawHelp(help, width);
			ImGui.SetNextItemWidth(itemWidth);
			T newValue = value;
			var ret = false;
			if (allowedValues != null)
				ret = Utils.ComboFromEnum(GetElementTitle(title, fieldName), ref newValue, allowedValues, GetElementTitle(title, fieldName));
			else
				ret = Utils.ComboFromEnum(GetElementTitle(title, fieldName), ref newValue);
			if (ret)
			{
				if (!newValue.Equals(value))
				{
					if (undoable)
						EnqueueSetFieldOrPropertyAction<T>(o, fieldName, newValue, value);
					else
						SetFieldOrPropertyToValue<T>(o, fieldName, newValue);
				}
			}
			return ret;
		}

		#endregion Enum

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
			DrawLiveEdit<Vector3>(undoable, title, o, fieldName, width, Func, Vector3Compare, help);
		}

		#endregion Color Edit 3

		#region Compare Functions

		private static bool IntCompare(int a, int b) { return a == b; }
		private static bool UIntCompare(uint a, uint b) { return a == b; }
		private static bool FloatCompare(float a, float b) { return a.FloatEquals(b); }
		private static bool DoubleCompare(double a, double b) { return a.DoubleEquals(b); }
		private static bool StringCompare(string a, string b) { return a == b; }
		private static bool Vector3Compare(Vector3 a, Vector3 b) { return a == b; }

		#endregion


		private static void SetFieldOrPropertyToValue<T>(object o, string fieldOrPropertyName, T value)
		{
			if (IsField(o, fieldOrPropertyName))
				o.GetType().GetField(fieldOrPropertyName, BindingFlags.Public | BindingFlags.Instance).SetValue(o, value);
			else
				o.GetType().GetProperty(fieldOrPropertyName, BindingFlags.Public | BindingFlags.Instance).SetValue(o, value);
		}

		private static void EnqueueSetFieldOrPropertyAction<T>(object o, string fieldOrPropertyName, T value, T previousValue)
		{
			if (IsField(o, fieldOrPropertyName))
				ActionQueue.Instance.Do(new ActionSetObjectField<T>(o, fieldOrPropertyName, value, previousValue));
			else
				ActionQueue.Instance.Do(new ActionSetObjectProperty<T>(o, fieldOrPropertyName, value, previousValue));
		}

		private static void EnqueueSetFieldOrPropertyAction<T>(object o, string fieldOrPropertyName, T value)
		{
			if (IsField(o, fieldOrPropertyName))
				ActionQueue.Instance.Do(new ActionSetObjectField<T>(o, fieldOrPropertyName, value));
			else
				ActionQueue.Instance.Do(new ActionSetObjectProperty<T>(o, fieldOrPropertyName, value));
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

		private static float DrawHelp(string help, float width)
		{
			var hasHelp = !string.IsNullOrEmpty(help);
			var remainderWidth = hasHelp ? Math.Max(1.0f, width - Utils.HelpWidth - ImGui.GetStyle().ItemSpacing.X) : width;
			
			if (hasHelp)
			{
				Utils.HelpMarker(help);
				ImGui.SameLine();
			}

			return remainderWidth;
		}

		private static bool DrawCachedEdit<T>(
			bool undoable,
			string title,
			object o,
			string fieldName,
			float width,
			Func<T, (bool, T)> imGuiFunc,
			Func<T, T, bool> compareFunc,
			string help = null)
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
			if (ImGui.IsItemDeactivatedAfterEdit() && o != null && !compareFunc(cachedValue, value))
			{
				if (undoable)
					EnqueueSetFieldOrPropertyAction(o, fieldName, cachedValue);
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
		private static bool DrawLiveEdit<T>(
			bool undoable,
			string title,
			object o,
			string fieldName,
			float width,
			Func<T, (bool, T)> imGuiFunc,
			Func<T, T, bool> compareFunc,
			string help = null)
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
					EnqueueSetFieldOrPropertyAction(o, fieldName, value, GetCachedValue<T>(cacheKey));
				}
			}

			return result;
		}
	}
}

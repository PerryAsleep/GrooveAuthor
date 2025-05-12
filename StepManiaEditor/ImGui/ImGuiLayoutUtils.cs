using System;
using System.Collections.Generic;
using System.Numerics;
using ImGuiNET;
using StepManiaEditor.AutogenConfig;
using StepManiaEditor.EditorActions;
using StepManiaLibrary.PerformedChart;
using static Fumen.FumenExtensions;
using static StepManiaEditor.ImGuiUtils;
using static StepManiaEditor.Utils;
using static Fumen.Converters.SMCommon;
using static StepManiaEditor.AutogenConfig.EditorPatternConfig;
using static StepManiaEditor.Editor;
using static System.Diagnostics.Debug;

namespace StepManiaEditor;

/// <summary>
/// This class offers methods for drawing complex elements using ImGui that typically use the following behavior:
///  - They can alter fields or properties on an object.
///  - They can be undoable, using EditorActions to perform edits.
///  - They can leverage cached state so that edits can occur in the UI before being committed to the underlying value.
///  - They are laid out using a table with a name on the left, an optional help tool tip marker, and a control on the right.
/// </summary>
internal sealed class ImGuiLayoutUtils
{
	/// <summary>
	/// Cache of values being edited through ImGui controls.
	/// We often need to edit a cached value instead of a real value
	///  Example: Text entry where we do not want to commit the text until editing is complete.
	/// We also often need to enqueue an undo action of a cached value from before editing began.
	///  Example: With a slider the value is changing continuously, and we do not want to enqueue
	///  an event until the user releases the control and the before value in this case should be
	///  a previously cached value.
	/// </summary>
	private static readonly Dictionary<string, object> Cache = new();

	private static string CacheKeyPrefix = "";
	private const string DragHelpText = "\n\nShift+drag for large adjustments.\nAlt+drag for small adjustments.";

	private static ImFontPtr ImGuiFont;

	private static readonly List<ChartType?> StartupStepGraphOptions;

	public static readonly float CheckBoxWidth = UiScaled(20);
	public static readonly float FileBrowseXWidth = UiScaled(20);
	public static readonly float FileBrowseBrowseWidth = UiScaled(50);
	public static readonly float FileBrowseAutoWidth = UiScaled(50);
	public static readonly float DisplayTempoEnumWidth = UiScaled(120);
	public static readonly float RangeToWidth = UiScaled(14);
	public static readonly float TextXWidth = UiScaled(8);
	public static readonly float TextYWidth = UiScaled(8);
	public static readonly float TextLatWidth = UiScaled(26);
	public static readonly float TextLongWidth = UiScaled(26);
	public static readonly float SliderResetWidth = UiScaled(50);
	public static readonly float ConfigFromListEditWidth = UiScaled(40);
	public static readonly float ConfigFromListViewAllWidth = UiScaled(60);
	public static readonly float ConfigFromListNewWidth = UiScaled(30);
	public static readonly float NoteTypeComboWidth = UiScaled(60);
	public static readonly float NotesFromTextWidth = UiScaled(60);
	public static readonly float BpmTextWidth = UiScaled(20);
	public static readonly float PatternConfigShortEnumWidth = UiScaled(160);
	public static readonly float ButtonGoWidth = UiScaled(20);
	public static readonly float ButtonUseCurrentRowWidth = UiScaled(80);
	public static readonly float TextInclusiveWidth = UiScaled(58);
	public static readonly float NewSeedButtonWidth = UiScaled(57);
	public static readonly float NewSeedAndRegenerateButtonWidth = UiScaled(152);
	public static readonly float ArrowIconWidth = UiScaled(16);
	public static readonly float ArrowIconHeight = UiScaled(16);
	public static readonly Vector2 ArrowIconSize = new(ArrowIconWidth, ArrowIconHeight);
	public static readonly float ButtonApplyWidth = UiScaled(60);
	public static readonly Vector2 ButtonCopySize = new(UiScaled(32), 0);
	public static readonly Vector2 ButtonSettingsSize = new(UiScaled(56), 0);
	public static readonly float SnapLimitTextWidth = UiScaled(30);
	public static readonly float TableRowRightPadding = UiScaled(1);

	static ImGuiLayoutUtils()
	{
		// Initialize the StartupStepGraphOptions List.
		// The size is 18 because we want 3 columns and the max rows per column is 6 from the dance types.
		const int stepGraphListSize = 18;
		StartupStepGraphOptions = new List<ChartType?>(stepGraphListSize);
		for (var i = 0; i < stepGraphListSize; i++)
			StartupStepGraphOptions.Add(null);

		var numAddedDanceTypes = 0;
		var numAddedPumpTypes = 0;
		var numAddedSmxTypes = 0;
		for (var i = 0; i < SupportedSinglePlayerChartTypes.Length; i++)
		{
			if (IsDanceType(SupportedSinglePlayerChartTypes[i]))
			{
				StartupStepGraphOptions[numAddedDanceTypes * 3] = SupportedSinglePlayerChartTypes[i];
				numAddedDanceTypes++;
			}
			else if (IsPumpType(SupportedSinglePlayerChartTypes[i]))
			{
				StartupStepGraphOptions[1 + numAddedPumpTypes * 3] = SupportedSinglePlayerChartTypes[i];
				numAddedPumpTypes++;
			}
			else if (IsSmxType(SupportedSinglePlayerChartTypes[i]))
			{
				StartupStepGraphOptions[2 + numAddedSmxTypes * 3] = SupportedSinglePlayerChartTypes[i];
				numAddedSmxTypes++;
			}
			else
			{
				Assert(false,
					$"Unexpected ChartType {SupportedSinglePlayerChartTypes[i]} in Editor.SupportedSinglePlayerChartTypes.");
			}
		}
	}

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

	public static bool BeginTable(string title, float titleColumnWidth, float contentColumnWidth)
	{
		CacheKeyPrefix = title ?? "";
		var ret = ImGui.BeginTable(title, 2, ImGuiTableFlags.None,
			new Vector2(titleColumnWidth + contentColumnWidth + ImGui.GetStyle().ItemSpacing.X, 0.0f));
		if (ret)
		{
			ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, titleColumnWidth);
			ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, contentColumnWidth);
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
			ImGui.TextUnformatted(title);

		ImGui.TableSetColumnIndex(1);
	}

	public static void DrawTitle(string title, string help = null)
	{
		ImGui.TableNextRow();

		ImGui.TableSetColumnIndex(0);
		if (!string.IsNullOrEmpty(title))
			ImGui.TextUnformatted(title);

		ImGui.TableSetColumnIndex(1);
		ImGui.SetNextItemWidth(DrawHelp(help, ImGui.GetContentRegionAvail().X));
	}

	public static void DrawRowTitleAndText(string title, string text, string help = null)
	{
		DrawRowTitleAndAdvanceColumn(title);
		ImGui.SetNextItemWidth(DrawHelp(help, ImGui.GetContentRegionAvail().X));
		ImGui.TextUnformatted(text);
	}

	public static void DrawRowTitleAndTextWithButton(string title, string text, Action buttonAction, string buttonText,
		float buttonWidth, string help = null)
	{
		DrawRowTitleAndAdvanceColumn(title);

		var remainingWidth = DrawHelp(help, ImGui.GetContentRegionAvail().X);
		var textWidth = remainingWidth - buttonWidth - ImGui.GetStyle().ItemSpacing.X;
		ImGui.SetNextItemWidth(textWidth);
		Text(text, textWidth);

		ImGui.SameLine();
		if (ImGui.Button($"{buttonText}{GetElementTitle(title, "Button")}", new Vector2(buttonWidth, 0.0f)))
		{
			buttonAction?.Invoke();
		}
	}

	private static string GetDragHelpText(string helpText)
	{
		return string.IsNullOrEmpty(helpText) ? null : helpText + DragHelpText;
	}

	public static float GetTableWidth()
	{
		// When drawing tables as the widget for a row they are cut off on the right by one pixel
		// and I am not sure why. This is a hack to compensate for it.
		return ImGui.GetContentRegionAvail().X - TableRowRightPadding;
	}

	#region Texture

	public static void DrawRowTexture(string title, EditorTexture texture, EmptyTexture fallbackTexture, string help = null)
	{
		DrawRowTitleAndAdvanceColumn(title);
		ImGui.SetNextItemWidth(DrawHelp(help, ImGui.GetContentRegionAvail().X));
		if (!(texture?.Draw() ?? false))
		{
			fallbackTexture?.Draw();
		}
	}

	#endregion Texture

	#region Checkbox

	public static bool DrawRowCheckbox(string title, ref bool value, string help = null)
	{
		DrawRowTitleAndAdvanceColumn(title);
		return DrawCheckbox(title, ref value, ImGui.GetContentRegionAvail().X, help);
	}

	public static bool DrawRowCheckboxWithButton(string title, ref bool value,
		string buttonText, Action buttonAction, string help = null)
	{
		DrawRowTitleAndAdvanceColumn(title);
		var buttonWidth = DrawHelp(help, ImGui.GetContentRegionAvail().X) - CheckBoxWidth - ImGui.GetStyle().ItemSpacing.X;
		ImGui.SetNextItemWidth(CheckBoxWidth);
		var ret = ImGui.Checkbox(GetElementTitle(title), ref value);
		ImGui.SameLine();
		if (ImGui.Button($"{buttonText}{GetElementTitle(title, "Button")}", new Vector2(buttonWidth, 0.0f)))
		{
			buttonAction();
		}

		return ret;
	}

	public static bool DrawRowCheckbox(bool undoable, string title, object o, string fieldName, bool affectsFile,
		string help = null)
	{
		DrawRowTitleAndAdvanceColumn(title);
		return DrawCheckbox(undoable, title, o, fieldName, ImGui.GetContentRegionAvail().X, affectsFile, help);
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
		bool affectsFile,
		string help = null)
	{
		ImGui.SetNextItemWidth(DrawHelp(help, width));

		var value = GetValueFromFieldOrProperty<bool>(o, fieldName);
		var ret = ImGui.Checkbox(GetElementTitle(title, fieldName), ref value);
		if (ret)
		{
			if (undoable)
			{
				ActionQueue.Instance.Do(new ActionSetObjectFieldOrPropertyValue<bool>(o, fieldName, value, affectsFile));
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

	public static void DrawRowTwoButtons(
		string title,
		string buttonText1,
		Action action1,
		string help = null)
	{
		DrawRowTitleAndAdvanceColumn(title);
		var remainingWidth = DrawHelp(help, ImGui.GetContentRegionAvail().X);

		var buttonWidth = (remainingWidth - ImGui.GetStyle().ItemSpacing.X) * 0.5f;
		var buttonWidthVec = new Vector2(buttonWidth, 0.0f);

		if (ImGui.Button(buttonText1, buttonWidthVec))
		{
			action1();
		}
	}

	public static void DrawRowTwoButtons(
		string title,
		string buttonText1,
		Action action1,
		string buttonText2,
		Action action2,
		string help = null)
	{
		DrawRowTitleAndAdvanceColumn(title);
		var remainingWidth = DrawHelp(help, ImGui.GetContentRegionAvail().X);

		var buttonWidth = (remainingWidth - ImGui.GetStyle().ItemSpacing.X) * 0.5f;
		var buttonWidthVec = new Vector2(buttonWidth, 0.0f);

		if (ImGui.Button(buttonText1, buttonWidthVec))
		{
			action1();
		}

		ImGui.SameLine();
		if (ImGui.Button(buttonText2, buttonWidthVec))
		{
			action2();
		}
	}

	public static void DrawRowTwoButtons(
		string title,
		string buttonText1,
		Action action1,
		bool action1Enabled,
		string buttonText2,
		Action action2,
		bool action2Enabled,
		string help = null)
	{
		DrawRowTitleAndAdvanceColumn(title);
		var remainingWidth = DrawHelp(help, ImGui.GetContentRegionAvail().X);

		var buttonWidth = (remainingWidth - ImGui.GetStyle().ItemSpacing.X) * 0.5f;
		var buttonWidthVec = new Vector2(buttonWidth, 0.0f);

		if (!action1Enabled)
			PushDisabled();
		if (ImGui.Button(buttonText1, buttonWidthVec))
		{
			action1();
		}

		if (!action1Enabled)
			PopDisabled();

		ImGui.SameLine();
		if (!action2Enabled)
			PushDisabled();
		if (ImGui.Button(buttonText2, buttonWidthVec))
		{
			action2();
		}

		if (!action2Enabled)
			PopDisabled();
	}

	public static void DrawRowThreeButtons(
		string title,
		string buttonText1,
		Action action1,
		bool action1Enabled,
		string buttonText2,
		Action action2,
		bool action2Enabled,
		string buttonText3,
		Action action3,
		bool action3Enabled,
		string help = null)
	{
		DrawRowTitleAndAdvanceColumn(title);
		var remainingWidth = DrawHelp(help, ImGui.GetContentRegionAvail().X);

		var buttonWidth = (remainingWidth - ImGui.GetStyle().ItemSpacing.X) / 3.0f;
		var buttonWidthVec = new Vector2(buttonWidth, 0.0f);

		if (!action1Enabled)
			PushDisabled();
		if (ImGui.Button(buttonText1, buttonWidthVec))
		{
			action1();
		}

		if (!action1Enabled)
			PopDisabled();

		ImGui.SameLine();
		if (!action2Enabled)
			PushDisabled();
		if (ImGui.Button(buttonText2, buttonWidthVec))
		{
			action2();
		}

		if (!action2Enabled)
			PopDisabled();

		ImGui.SameLine();
		if (!action3Enabled)
			PushDisabled();
		if (ImGui.Button(buttonText3, buttonWidthVec))
		{
			action3();
		}

		if (!action3Enabled)
			PopDisabled();
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

	public static void DrawRowTextInput(bool undoable, string title, object o, string fieldName, bool affectsFile,
		string help = null)
	{
		DrawRowTitleAndAdvanceColumn(title);
		DrawTextInput(undoable, title, o, fieldName, ImGui.GetContentRegionAvail().X, affectsFile, null, help);
	}

	public static void DrawRowTextInput(bool undoable, string title, object o, string fieldName, bool affectsFile,
		Func<string, bool> validationFunc, string help = null)
	{
		DrawRowTitleAndAdvanceColumn(title);
		DrawTextInput(undoable, title, o, fieldName, ImGui.GetContentRegionAvail().X, affectsFile, validationFunc, help);
	}

	public static void DrawRowTextInputWithOneButton(bool undoable, string title, object o, string fieldName, bool affectsFile,
		Action buttonAction, string buttonText, float buttonWidth, string help = null)
	{
		DrawRowTitleAndAdvanceColumn(title);

		var textInputWidth = ImGui.GetContentRegionAvail().X - buttonWidth - ImGui.GetStyle().ItemSpacing.X;
		DrawTextInput(undoable, title, o, fieldName, textInputWidth, affectsFile, null, help);

		ImGui.SameLine();
		if (ImGui.Button($"{buttonText}{GetElementTitle(title, fieldName)}", new Vector2(buttonWidth, 0.0f)))
		{
			buttonAction();
		}
	}

	public static void DrawRowTextInputWithTransliteration(bool undoable, string title, object o, string fieldName,
		string transliterationFieldName, bool affectsFile, bool currentValueError, string help = null)
	{
		DrawRowTitleAndAdvanceColumn(title);

		if (currentValueError)
			PushErrorColor();
		DrawTextInput(undoable, title, o, fieldName, ImGui.GetContentRegionAvail().X * 0.5f, affectsFile, null, help);
		if (currentValueError)
			PopErrorColor();
		ImGui.SameLine();
		DrawTextInput(undoable, "Transliteration", o, transliterationFieldName, ImGui.GetContentRegionAvail().X, affectsFile,
			null,
			"Optional text to use when sorting by this value.\nStepMania sorts values lexicographically, preferring transliterations.");
	}

	private static void DrawTextInput(bool undoable, string title, object o, string fieldName, float width,
		bool affectsFile, Func<string, bool> validationFunc = null, string help = null)
	{
		(bool, string) Func(string v)
		{
			v ??= "";
			bool r;
			if (string.IsNullOrEmpty(title) || title.StartsWith("##"))
				r = ImGui.InputText(GetElementTitle(title, fieldName), ref v, 256);
			else
				r = ImGui.InputTextWithHint(GetElementTitle(title, fieldName), title, ref v, 256);
			return (r, v);
		}

		DrawCachedEditReference(undoable, title, o, fieldName, width, affectsFile, Func, StringCompare, validationFunc, help);
	}

	public static void DrawRowCharacterInput(bool undoable, string title, object o, string fieldName, bool affectsFile,
		string help = null)
	{
		DrawRowTitleAndAdvanceColumn(title);
		DrawCharInput(undoable, title, o, fieldName, ImGui.GetContentRegionAvail().X, affectsFile, null, help);
	}

	private static void DrawCharInput(bool undoable, string title, object o, string fieldName, float width,
		bool affectsFile, Func<char, bool> validationFunc = null, string help = null)
	{
		(bool, char) Func(char v)
		{
			var s = v.ToString();
			var r = ImGui.InputText(GetElementTitle(title, fieldName), ref s, 1);
			if (s?.Length > 0)
				v = s[0];
			else
				v = ' ';
			return (r, v);
		}

		DrawCachedEditValue(undoable, title, o, fieldName, width, affectsFile, Func, CharCompare, validationFunc, help);
	}

	#endregion Text Input

	#region Time Signature

	private static void DrawTimeSignatureInput(bool undoable, string title, object o, string fieldName, float width,
		bool affectsFile, string help = null)
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

		DrawCachedEditReference<string>(undoable, title, o, fieldName, width, affectsFile, Func, StringCompare, ValidationFunc,
			help);
	}

	#endregion Time Signature

	#region Multipliers

	private static void DrawMultipliersInput(bool undoable, string title, object o, string fieldName, float width,
		bool affectsFile, string help = null)
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

		DrawCachedEditReference<string>(undoable, title, o, fieldName, width, affectsFile, Func, StringCompare, ValidationFunc,
			help);
	}

	#endregion Multipliers

	#region Label

	private static void DrawLabelInput(bool undoable, string title, object o, string fieldName, float width,
		bool affectsFile, string help = null)
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

		DrawCachedEditReference<string>(undoable, title, o, fieldName, width, affectsFile, Func, StringCompare, ValidationFunc,
			help);
	}

	#endregion Label

	#region Scroll Rate Interpolation

	private static void DrawScrollRateInterpolationInput(bool undoable, string title, object o, string fieldName, float width,
		bool affectsFile, string help = null)
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

		DrawCachedEditReference<string>(undoable, title, o, fieldName, width, affectsFile, Func, StringCompare, ValidationFunc,
			help);
	}

	#endregion Scroll Rate Interpolation

	#region File Browse

	public static void DrawRowFileBrowse(
		string title, object o, string fieldName, Action browseAction, Action clearAction, bool affectsFile, string help = null)
	{
		DrawRowTitleAndAdvanceColumn(title);

		// Display the text input but do not allow edits to it.
		PushDisabled();
		DrawTextInput(false, title, o, fieldName,
			Math.Max(1.0f,
				ImGui.GetContentRegionAvail().X - (FileBrowseXWidth + FileBrowseBrowseWidth) -
				ImGui.GetStyle().ItemSpacing.X * 2), affectsFile, null, help);
		PopDisabled();

		ImGui.SameLine();
		if (ImGui.Button($"X{GetElementTitle(title, fieldName)}", new Vector2(FileBrowseXWidth, 0.0f)))
		{
			clearAction();
		}

		ImGui.SameLine();
		if (ImGui.Button($"Browse{GetElementTitle(title, fieldName)}", new Vector2(FileBrowseBrowseWidth, 0.0f)))
		{
			browseAction();
		}
	}

	public static void DrawRowAutoFileBrowse(
		string title, object o, string fieldName, Action autoAction, Action browseAction, Action clearAction,
		bool affectsFile, string help = null)
	{
		DrawRowTitleAndAdvanceColumn(title);

		// Display the text input but do not allow edits to it.
		PushDisabled();
		DrawTextInput(false, title, o, fieldName,
			Math.Max(1.0f,
				ImGui.GetContentRegionAvail().X - (FileBrowseXWidth + FileBrowseAutoWidth + FileBrowseBrowseWidth) -
				ImGui.GetStyle().ItemSpacing.X * 3), affectsFile, null, help);
		PopDisabled();

		ImGui.SameLine();
		if (ImGui.Button($"X{GetElementTitle(title, fieldName)}", new Vector2(FileBrowseXWidth, 0.0f)))
		{
			clearAction();
		}

		ImGui.SameLine();
		if (ImGui.Button($"Auto{GetElementTitle(title, fieldName)}", new Vector2(FileBrowseAutoWidth, 0.0f)))
		{
			autoAction();
		}

		ImGui.SameLine();
		if (ImGui.Button($"Browse{GetElementTitle(title, fieldName)}", new Vector2(FileBrowseBrowseWidth, 0.0f)))
		{
			browseAction();
		}
	}

	#endregion File Browse

	#region Config From List

	public static bool DrawSelectableConfigFromList(
		string title,
		string elementName,
		ref int selectedIndex,
		string[] values,
		Action editAction,
		Action viewAllAction,
		Action newAction,
		string help = null)
	{
		DrawRowTitleAndAdvanceColumn(title);

		var elementTitle = GetElementTitle(title, elementName);

		var itemWidth = DrawHelp(help, ImGui.GetContentRegionAvail().X);
		var spacing = ImGui.GetStyle().ItemSpacing.X;
		var comboWidth = Math.Max(1.0f,
			itemWidth - ConfigFromListEditWidth - ConfigFromListViewAllWidth - ConfigFromListNewWidth - spacing * 3.0f);
		ImGui.SetNextItemWidth(comboWidth);

		var ret = ComboFromArray(elementTitle, ref selectedIndex, values);

		ImGui.SameLine();
		if (ImGui.Button($"Edit{elementTitle}", new Vector2(ConfigFromListEditWidth, 0.0f)))
		{
			editAction();
		}

		ImGui.SameLine();
		if (ImGui.Button($"View All{elementTitle}", new Vector2(ConfigFromListViewAllWidth, 0.0f)))
		{
			viewAllAction();
		}

		ImGui.SameLine();
		if (ImGui.Button($"New{elementTitle}", new Vector2(ConfigFromListNewWidth, 0.0f)))
		{
			newAction();
		}

		return ret;
	}

	public static bool DrawExpressedChartConfigCombo(EditorChart editorChart, string title, string help)
	{
		DrawRowTitleAndAdvanceColumn(title);

		var fieldName = nameof(EditorChart.ExpressedChartConfig);

		var elementTitle = GetElementTitle(title, fieldName);

		var itemWidth = DrawHelp(help, ImGui.GetContentRegionAvail().X);
		var spacing = ImGui.GetStyle().ItemSpacing.X;
		var comboWidth = Math.Max(1.0f,
			itemWidth - ConfigFromListEditWidth - ConfigFromListViewAllWidth - ConfigFromListNewWidth - spacing * 3.0f);
		ImGui.SetNextItemWidth(comboWidth);

		var currentValue = editorChart?.ExpressedChartConfig ??
		                   ExpressedChartConfigManager.DefaultExpressedChartDynamicConfigGuid;
		var configGuids = ExpressedChartConfigManager.Instance.GetSortedConfigGuids();
		var configNames = ExpressedChartConfigManager.Instance.GetSortedConfigNames();
		var selectedIndex = 0;
		for (var i = 0; i < configGuids.Length; i++)
		{
			if (configGuids[i].Equals(currentValue))
			{
				selectedIndex = i;
				break;
			}
		}

		var ret = ComboFromArray(elementTitle, ref selectedIndex, configNames);
		if (ret)
		{
			var newValue = configGuids[selectedIndex];
			if (!newValue.Equals(currentValue))
			{
				ActionQueue.Instance.Do(
					new ActionSetObjectFieldOrPropertyValue<Guid>(editorChart, fieldName, newValue, true));
			}
		}

		ImGui.SameLine();
		if (ImGui.Button($"Edit{elementTitle}", new Vector2(ConfigFromListEditWidth, 0.0f)))
		{
			if (editorChart != null)
				EditorExpressedChartConfig.ShowEditUI(editorChart.ExpressedChartConfig);
		}

		ImGui.SameLine();
		if (ImGui.Button($"View All{elementTitle}", new Vector2(ConfigFromListViewAllWidth, 0.0f)))
		{
			UIAutogenConfigs.Instance.Open(true);
		}

		ImGui.SameLine();
		if (ImGui.Button($"New{elementTitle}", new Vector2(ConfigFromListNewWidth, 0.0f)))
		{
			EditorExpressedChartConfig.CreateNewConfigAndShowEditUI(editorChart);
		}

		return ret;
	}

	public static void DrawPatternConfigCombo(bool undoable, string title, object o, string fieldName, string help)
	{
		DrawRowTitleAndAdvanceColumn(title);

		var elementTitle = GetElementTitle(title, fieldName);

		var itemWidth = DrawHelp(help, ImGui.GetContentRegionAvail().X);
		var spacing = ImGui.GetStyle().ItemSpacing.X;
		var comboWidth = Math.Max(1.0f,
			itemWidth - ConfigFromListViewAllWidth - ConfigFromListNewWidth - spacing * 2.0f);
		ImGui.SetNextItemWidth(comboWidth);

		var currentValue = GetValueFromFieldOrProperty<Guid>(o, fieldName);
		var configGuids = PatternConfigManager.Instance.GetSortedConfigGuids();
		var selectedIndex = 0;
		for (var i = 0; i < configGuids.Length; i++)
		{
			if (configGuids[i].Equals(currentValue))
			{
				selectedIndex = i;
				break;
			}
		}

		var selectedConfig = PatternConfigManager.Instance.GetConfig(configGuids[selectedIndex]);
		var newValue = currentValue;

		ImGui.PushStyleColor(ImGuiCol.Text, selectedConfig.GetStringColor());

		if (ImGui.BeginCombo($"{elementTitle}Combo", selectedConfig.ToString()))
		{
			ImGui.PopStyleColor();

			for (var i = 0; i < configGuids.Length; i++)
			{
				var config = PatternConfigManager.Instance.GetConfig(configGuids[i]);

				ImGui.PushStyleColor(ImGuiCol.Text, config.GetStringColor());

				var isSelected = i == selectedIndex;
				if (ImGui.Selectable(config.ToString(), isSelected))
				{
					newValue = configGuids[i];
				}

				if (isSelected)
				{
					ImGui.SetItemDefaultFocus();
				}

				ImGui.PopStyleColor();
			}

			ImGui.EndCombo();
		}
		else
		{
			ImGui.PopStyleColor();
		}

		if (!newValue.Equals(currentValue))
		{
			if (undoable)
			{
				ActionQueue.Instance.Do(
					new ActionSetObjectFieldOrPropertyValue<Guid>(o, fieldName, newValue, true));
			}
			else
			{
				SetFieldOrPropertyToValue(o, fieldName, newValue);
			}
		}

		ImGui.SameLine();
		if (ImGui.Button($"View All{elementTitle}", new Vector2(ConfigFromListViewAllWidth, 0.0f)))
		{
			UIAutogenConfigs.Instance.Open(true);
		}

		ImGui.SameLine();
		if (ImGui.Button($"New{elementTitle}", new Vector2(ConfigFromListNewWidth, 0.0f)))
		{
			var newConfigAction = GetCreateNewConfigAction();

			if (undoable)
			{
				var updateObjectAction =
					new ActionSetObjectFieldOrPropertyValue<Guid>(o, fieldName, newConfigAction.GetGuid(), true);

				var actionMultiple = new ActionMultiple();
				actionMultiple.EnqueueAndDo(newConfigAction);
				actionMultiple.EnqueueAndDo(updateObjectAction);
				ActionQueue.Instance.EnqueueWithoutDoing(actionMultiple);
			}
			else
			{
				ActionQueue.Instance.Do(newConfigAction);
				SetFieldOrPropertyToValue(o, fieldName, newConfigAction.GetGuid());
			}

			ShowEditUI(newConfigAction.GetGuid());
		}
	}

	public static bool DrawPerformedChartConfigCombo(bool undoable, string title, object o, string fieldName, string help)
	{
		DrawRowTitleAndAdvanceColumn(title);

		var elementTitle = GetElementTitle(title, fieldName);

		var itemWidth = DrawHelp(help, ImGui.GetContentRegionAvail().X);
		var spacing = ImGui.GetStyle().ItemSpacing.X;
		var comboWidth = Math.Max(1.0f,
			itemWidth - ConfigFromListViewAllWidth - ConfigFromListNewWidth - spacing * 2.0f);
		ImGui.SetNextItemWidth(comboWidth);

		var currentValue = GetValueFromFieldOrProperty<Guid>(o, fieldName);
		var configGuids = PerformedChartConfigManager.Instance.GetSortedConfigGuids();
		var configNames = PerformedChartConfigManager.Instance.GetSortedConfigNames();
		var selectedIndex = 0;
		for (var i = 0; i < configGuids.Length; i++)
		{
			if (configGuids[i].Equals(currentValue))
			{
				selectedIndex = i;
				break;
			}
		}

		var ret = ComboFromArray(elementTitle, ref selectedIndex, configNames);
		if (ret)
		{
			var newValue = configGuids[selectedIndex];
			if (!newValue.Equals(currentValue))
			{
				if (undoable)
				{
					ActionQueue.Instance.Do(
						new ActionSetObjectFieldOrPropertyValue<Guid>(o, fieldName, newValue, true));
				}
				else
				{
					SetFieldOrPropertyToValue(o, fieldName, newValue);
				}
			}
		}

		ImGui.SameLine();
		if (ImGui.Button($"View All{elementTitle}", new Vector2(ConfigFromListViewAllWidth, 0.0f)))
		{
			UIAutogenConfigs.Instance.Open(true);
		}

		ImGui.SameLine();
		if (ImGui.Button($"New{elementTitle}", new Vector2(ConfigFromListNewWidth, 0.0f)))
		{
			var newConfigAction = EditorPerformedChartConfig.GetCreateNewConfigAction();

			if (undoable)
			{
				var updateObjectAction =
					new ActionSetObjectFieldOrPropertyValue<Guid>(o, fieldName, newConfigAction.GetGuid(), true);

				var actionMultiple = new ActionMultiple();
				actionMultiple.EnqueueAndDo(newConfigAction);
				actionMultiple.EnqueueAndDo(updateObjectAction);
				ActionQueue.Instance.EnqueueWithoutDoing(actionMultiple);
			}
			else
			{
				ActionQueue.Instance.Do(newConfigAction);
				SetFieldOrPropertyToValue(o, fieldName, newConfigAction.GetGuid());
			}

			EditorPerformedChartConfig.ShowEditUI(newConfigAction.GetGuid());
		}

		return ret;
	}

	#endregion Config From List

	#region Input Int

	public static void DrawRowInputInt(
		bool undoable,
		string title,
		object o,
		string fieldName,
		bool affectsFile,
		string help = null,
		int min = int.MinValue,
		int max = int.MaxValue)
	{
		DrawRowTitleAndAdvanceColumn(title);
		DrawInputInt(undoable, title, o, fieldName, ImGui.GetContentRegionAvail().X, affectsFile, help, min, max);
	}

	private static void DrawInputInt(
		bool undoable,
		string title,
		object o,
		string fieldName,
		float width,
		bool affectsFile,
		string help = null,
		int min = int.MinValue,
		int max = int.MaxValue)
	{
		(bool, int) Func(int v)
		{
			var r = InputInt(GetElementTitle(title, fieldName), ref v, min, max);
			return (r, v);
		}

		DrawLiveEditValue<int>(undoable, title, o, fieldName, width, affectsFile, Func, IntCompare, help);
	}

	#endregion Input Int

	#region Drag Int

	public static void DrawRowDragInt2(
		bool undoable,
		string title,
		object o,
		string fieldName1,
		string fieldName2,
		bool affectsFile,
		bool field1Enabled = true,
		bool field2Enabled = true,
		string help = null,
		float speed = 1.0f,
		string format = "%i",
		int min1 = int.MinValue,
		int max1 = int.MaxValue,
		int min2 = int.MinValue,
		int max2 = int.MaxValue)
	{
		DrawRowTitleAndAdvanceColumn(title);
		if (!field1Enabled)
			PushDisabled();
		DrawDragInt(undoable, "", o, fieldName1, ImGui.GetContentRegionAvail().X * 0.5f, affectsFile, help, speed, format, min1,
			max1);
		if (!field1Enabled)
			PopDisabled();
		ImGui.SameLine();
		if (!field2Enabled)
			PushDisabled();
		DrawDragInt(undoable, "", o, fieldName2, ImGui.GetContentRegionAvail().X, affectsFile, "", speed, format, min2, max2);
		if (!field2Enabled)
			PopDisabled();
	}

	public static void DrawRowDragInt2(
		bool undoable,
		string title,
		object o,
		string fieldName1,
		string fieldName2,
		bool affectsFile,
		string field1Title,
		string field2Title,
		float fieldTitleWidth,
		string help = null,
		float speed = 1.0f,
		string format = "%i",
		int min1 = int.MinValue,
		int max1 = int.MaxValue,
		int min2 = int.MinValue,
		int max2 = int.MaxValue)
	{
		DrawRowTitleAndAdvanceColumn(title);

		var totalWidth = DrawHelp(help, ImGui.GetContentRegionAvail().X);
		var dragIntWidth = (totalWidth - 2 * fieldTitleWidth - ImGui.GetStyle().ItemSpacing.X * 3) * 0.5f;

		Text(field1Title, fieldTitleWidth);
		ImGui.SameLine();

		DrawDragInt(undoable, "", o, fieldName1, dragIntWidth, affectsFile, null, speed, format, min1, max1);
		ImGui.SameLine();

		Text(field2Title, fieldTitleWidth);
		ImGui.SameLine();

		DrawDragInt(undoable, "", o, fieldName2, dragIntWidth, affectsFile, null, speed, format, min2, max2);
	}

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
		var itemWidth = DrawHelp(GetDragHelpText(help), width);
		ImGui.SetNextItemWidth(itemWidth);
		return DragInt(ref value, GetElementTitle(title), speed, format, min, max);
	}

	public static bool DrawRowDragInt(
		bool undoable,
		string title,
		object o,
		string fieldName,
		bool affectsFile,
		string help = null,
		float speed = 1.0f,
		string format = "%i",
		int min = int.MinValue,
		int max = int.MaxValue)
	{
		DrawRowTitleAndAdvanceColumn(title);
		return DrawDragInt(undoable, title, o, fieldName, ImGui.GetContentRegionAvail().X, affectsFile, help, speed, format, min,
			max);
	}

	private static bool DrawDragInt(
		bool undoable,
		string title,
		object o,
		string fieldName,
		float width,
		bool affectsFile,
		string help,
		float speed,
		string format,
		int min = int.MinValue,
		int max = int.MaxValue)
	{
		(bool, int) Func(int v)
		{
			var r = DragInt(ref v, GetElementTitle(title, fieldName), speed, format, min, max);
			return (r, v);
		}

		return DrawLiveEditValue<int>(undoable, title, o, fieldName, width, affectsFile, Func, IntCompare, GetDragHelpText(help));
	}

	public static void DrawDragIntRange(
		bool undoable,
		string title,
		object o,
		string beginFieldName,
		string endFieldName,
		bool affectsFile,
		float width,
		string help = null,
		float speed = 1.0f,
		string format = "%i",
		int min = int.MinValue,
		int max = int.MaxValue)
	{
		var remainingWidth = DrawHelp(GetDragHelpText(help), width);
		var controlWidth = Math.Max(0.0f, 0.5f * (remainingWidth - ImGui.GetStyle().ItemSpacing.X * 2.0f - RangeToWidth));

		var currentBeginValue = GetValueFromFieldOrProperty<int>(o, beginFieldName);
		var currentEndValue = GetValueFromFieldOrProperty<int>(o, endFieldName);
		var maxForBegin = Math.Min(max, currentEndValue);
		var minForEnd = Math.Max(min, currentBeginValue);

		// Controls for the int values.
		ImGui.SameLine();
		DrawDragInt(undoable, title, o, beginFieldName, controlWidth, affectsFile, "", speed, format, min, maxForBegin);

		// "to" text
		ImGui.SameLine();
		Text("to", RangeToWidth);

		ImGui.SameLine();
		DrawDragInt(undoable, title, o, endFieldName, controlWidth, affectsFile, "", speed, format, minForEnd, max);
	}

	public static void DrawRowDragIntWithEnabledCheckbox(
		bool undoable,
		string title,
		object o,
		string fieldName,
		string enabledFieldName,
		bool affectsFile,
		string help = null,
		float speed = 1.0f,
		string format = "%i",
		int min = int.MinValue,
		int max = int.MaxValue)
	{
		title ??= "";

		DrawRowTitleAndAdvanceColumn(title);

		bool enabled;

		var controlWidth = ImGui.GetContentRegionAvail().X - CheckBoxWidth - ImGui.GetStyle().ItemSpacing.X;

		// Draw the checkbox for enabling the other control.
		if (DrawCheckbox(false, title + "check", o, enabledFieldName, CheckBoxWidth, affectsFile, GetDragHelpText(help)))
		{
			if (undoable)
			{
				enabled = GetValueFromFieldOrProperty<bool>(o, enabledFieldName);
				ActionQueue.Instance.Do(new ActionSetObjectFieldOrPropertyValue<bool>(
					o, enabledFieldName, enabled, !enabled, affectsFile));
			}
		}

		enabled = GetValueFromFieldOrProperty<bool>(o, enabledFieldName);
		if (!enabled)
			PushDisabled();

		// Control for the int value.
		ImGui.SameLine();
		DrawDragInt(undoable, title, o, fieldName, controlWidth, affectsFile, null, speed, format, min, max);

		if (!enabled)
			PopDisabled();
	}

	public static void DrawRowRandomSeed(
		bool undoable,
		string title,
		object o,
		string fieldName,
		bool affectsFile,
		EditorPatternEvent patternEvent,
		Editor editor,
		string help = null,
		string format = "%i")
	{
		title ??= "";

		DrawRowTitleAndAdvanceColumn(title);

		var remainingWidth = DrawHelp(help, ImGui.GetContentRegionAvail().X);

		var controlWidth = remainingWidth - NewSeedButtonWidth - NewSeedAndRegenerateButtonWidth -
		                   ImGui.GetStyle().ItemSpacing.X * 2;
		PushDisabled();
		DrawDragInt(undoable, title, o, fieldName, controlWidth, affectsFile, null, 1.0f, format);
		PopDisabled();

		ImGui.SameLine();
		if (ImGui.Button("New Seed", new Vector2(NewSeedButtonWidth, 0.0f)))
		{
			var newSeed = new Random().Next();
			if (undoable)
			{
				ActionQueue.Instance.Do(new ActionSetObjectFieldOrPropertyValue<int>(
					o, fieldName, newSeed, affectsFile));
			}
			else
			{
				SetFieldOrPropertyToValue(o, fieldName, newSeed);
			}
		}

		ImGui.SameLine();
		if (ImGui.Button("New Seed and Regenerate", new Vector2(NewSeedAndRegenerateButtonWidth, 0.0f)))
		{
			var newSeed = new Random().Next();
			if (undoable)
			{
				// Ideally these would be combined into an ActionMultiple, but ActionAutoGeneratePatterns
				// is async.
				ActionQueue.Instance.Do(new ActionSetObjectFieldOrPropertyValue<int>(
					o, fieldName, newSeed, affectsFile));
				ActionQueue.Instance.Do(new ActionAutoGeneratePatterns(
					editor,
					patternEvent!.GetEditorChart(),
					new List<EditorPatternEvent> { patternEvent }));
			}
			else
			{
				SetFieldOrPropertyToValue(o, fieldName, newSeed);
				ActionQueue.Instance.Do(new ActionAutoGeneratePatterns(
					editor,
					patternEvent!.GetEditorChart(),
					new List<EditorPatternEvent> { patternEvent }));
			}
		}
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
		bool affectsFile,
		string help = null)
	{
		DrawRowTitleAndAdvanceColumn(title);
		return DrawSliderInt(undoable, title, o, fieldName, ImGui.GetContentRegionAvail().X, min, max, affectsFile, help);
	}

	private static bool DrawSliderInt(
		bool undoable,
		string title,
		object o,
		string fieldName,
		float width,
		int min,
		int max,
		bool affectsFile,
		string help = null)
	{
		(bool, int) Func(int v)
		{
			var r = ImGui.SliderInt(GetElementTitle(title, fieldName), ref v, min, max);
			return (r, v);
		}

		return DrawLiveEditValue<int>(undoable, title, o, fieldName, width, affectsFile, Func, IntCompare, help);
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
		bool affectsFile,
		string format = null,
		ImGuiSliderFlags flags = ImGuiSliderFlags.None,
		string help = null)
	{
		DrawRowTitleAndAdvanceColumn(title);
		return DrawSliderUInt(undoable, title, o, fieldName, ImGui.GetContentRegionAvail().X, min, max, affectsFile, format,
			flags, help);
	}

	private static bool DrawSliderUInt(
		bool undoable,
		string title,
		object o,
		string fieldName,
		float width,
		uint min,
		uint max,
		bool affectsFile,
		string format = null,
		ImGuiSliderFlags flags = ImGuiSliderFlags.None,
		string help = null)
	{
		(bool, uint) Func(uint v)
		{
			var r = SliderUInt(GetElementTitle(title, fieldName), ref v, min, max, format, flags);
			return (r, v);
		}

		return DrawLiveEditValue<uint>(undoable, title, o, fieldName, width, affectsFile, Func, UIntCompare, help);
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
		bool affectsFile,
		string help = null,
		string format = "%.3f",
		ImGuiSliderFlags flags = ImGuiSliderFlags.None)
	{
		DrawRowTitleAndAdvanceColumn(title);
		return DrawSliderFloat(undoable, title, o, fieldName, ImGui.GetContentRegionAvail().X, min, max, affectsFile, help,
			format, flags);
	}

	private static bool DrawSliderFloat(
		bool undoable,
		string title,
		object o,
		string fieldName,
		float width,
		float min,
		float max,
		bool affectsFile,
		string help = null,
		string format = "%.3f",
		ImGuiSliderFlags flags = ImGuiSliderFlags.None)
	{
		(bool, float) Func(float v)
		{
			var r = ImGui.SliderFloat(GetElementTitle(title, fieldName), ref v, min, max, format, flags);
			return (r, v);
		}

		return DrawLiveEditValue<float>(undoable, title, o, fieldName, width, affectsFile, Func, FloatCompare, help);
	}

	public static void DrawRowSliderFloatWithReset(
		bool undoable,
		string title,
		object o,
		string fieldName,
		float min,
		float max,
		float resetValue,
		bool affectsFile,
		string help = null,
		string format = "%.3f",
		ImGuiSliderFlags flags = ImGuiSliderFlags.None)
	{
		DrawRowTitleAndAdvanceColumn(title);

		// Slider
		DrawSliderFloat(undoable, title, o, fieldName,
			ImGui.GetContentRegionAvail().X - SliderResetWidth - ImGui.GetStyle().ItemSpacing.X, min, max, affectsFile, help,
			format, flags);

		// Reset
		ImGui.SameLine();
		if (ImGui.Button($"Reset{GetElementTitle(title, fieldName)}", new Vector2(SliderResetWidth, 0.0f)))
		{
			var value = GetValueFromFieldOrProperty<float>(o, fieldName);
			if (!resetValue.FloatEquals(value))
			{
				if (undoable)
					ActionQueue.Instance.Do(
						new ActionSetObjectFieldOrPropertyValue<float>(o, fieldName, resetValue, affectsFile));
				else
					SetFieldOrPropertyToValue(o, fieldName, resetValue);
			}
		}
	}

	#endregion Slider Float

	#region Drag Float

	public static bool DrawRowDragFloat(
		string title,
		ref float value,
		string help = null,
		float speed = 0.0001f,
		string format = "%.6f",
		float min = float.MinValue,
		float max = float.MaxValue,
		ImGuiSliderFlags flags = ImGuiSliderFlags.None)
	{
		DrawRowTitleAndAdvanceColumn(title);
		return DrawDragFloat(title, ref value, ImGui.GetContentRegionAvail().X, help, speed, format, min, max, flags);
	}

	private static bool DrawDragFloat(
		string title,
		ref float value,
		float width,
		string help,
		float speed,
		string format,
		float min = float.MinValue,
		float max = float.MaxValue,
		ImGuiSliderFlags flags = ImGuiSliderFlags.None)
	{
		var itemWidth = DrawHelp(GetDragHelpText(help), width);
		ImGui.SetNextItemWidth(itemWidth);
		return ImGui.DragFloat(GetElementTitle(title), ref value, speed, min, max, format, flags);
	}

	public static bool DrawRowDragFloat(
		bool undoable,
		string title,
		object o,
		string fieldName,
		bool affectsFile,
		string help = null,
		float speed = 0.0001f,
		string format = "%.6f",
		float min = float.MinValue,
		float max = float.MaxValue,
		ImGuiSliderFlags flags = ImGuiSliderFlags.None)
	{
		DrawRowTitleAndAdvanceColumn(title);
		return DrawDragFloat(undoable, title, o, fieldName, ImGui.GetContentRegionAvail().X, help, speed, format, affectsFile,
			min, max, flags);
	}

	public static void DrawRowDragFloatWithEnabledCheckbox(
		bool undoable,
		string title,
		object o,
		string fieldName,
		string enabledFieldName,
		bool affectsFile,
		string help = null,
		float speed = 0.0001f,
		string format = "%.6f",
		float min = float.MinValue,
		float max = float.MaxValue,
		ImGuiSliderFlags flags = ImGuiSliderFlags.None)
	{
		title ??= "";

		DrawRowTitleAndAdvanceColumn(title);

		bool enabled;

		var controlWidth = ImGui.GetContentRegionAvail().X - CheckBoxWidth - ImGui.GetStyle().ItemSpacing.X;

		// Draw the checkbox for enabling the other control.
		if (DrawCheckbox(false, title + "check", o, enabledFieldName, CheckBoxWidth, affectsFile, GetDragHelpText(help)))
		{
			if (undoable)
			{
				enabled = GetValueFromFieldOrProperty<bool>(o, enabledFieldName);

				// If disabling the checkbox enqueue an action for undoing both the bool
				// and the setting of the float value to 0.
				if (!enabled)
				{
					var multiple = new ActionMultiple();
					multiple.EnqueueAndDo(new ActionSetObjectFieldOrPropertyValue<float>(o,
						fieldName, 0.0f, GetValueFromFieldOrProperty<float>(o, fieldName), affectsFile));
					multiple.EnqueueAndDo(new ActionSetObjectFieldOrPropertyValue<bool>(o,
						enabledFieldName, false, true, affectsFile));
					ActionQueue.Instance.EnqueueWithoutDoing(multiple);
				}

				// If enabling the checkbox we only need to enqueue an action for the checkbox bool.
				else
				{
					ActionQueue.Instance.Do(new ActionSetObjectFieldOrPropertyValue<bool>(
						o,
						enabledFieldName, true, false, affectsFile));
				}
			}
		}

		enabled = GetValueFromFieldOrProperty<bool>(o, enabledFieldName);
		if (!enabled)
			PushDisabled();

		// Control for the float value.
		ImGui.SameLine();
		DrawDragFloat(undoable, title, o, fieldName, controlWidth, null, speed, format, affectsFile, min, max, flags);

		if (!enabled)
			PopDisabled();
	}

	private static bool DrawDragFloat(
		bool undoable,
		string title,
		object o,
		string fieldName,
		float width,
		string help,
		float speed,
		string format,
		bool affectsFile,
		float min = float.MinValue,
		float max = float.MaxValue,
		ImGuiSliderFlags flags = ImGuiSliderFlags.None)
	{
		(bool, float) Func(float v)
		{
			var r = ImGui.DragFloat(GetElementTitle(title, fieldName), ref v, speed, min, max, format, flags);
			return (r, v);
		}

		return DrawLiveEditValue<float>(undoable, title, o, fieldName, width, affectsFile, Func, FloatCompare,
			GetDragHelpText(help));
	}

	#endregion Drag Float

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
		var itemWidth = DrawHelp(GetDragHelpText(help), width);
		ImGui.SetNextItemWidth(itemWidth);
		return DragDouble(ref value, GetElementTitle(title), speed, format, min, max);
	}

	public static bool DrawRowDragDouble(
		bool undoable,
		string title,
		object o,
		string fieldName,
		bool affectsFile,
		string help = null,
		float speed = 0.0001f,
		string format = "%.6f",
		double min = double.MinValue,
		double max = double.MaxValue)
	{
		DrawRowTitleAndAdvanceColumn(title);
		return DrawDragDouble(undoable, title, o, fieldName, ImGui.GetContentRegionAvail().X, help, speed, format, affectsFile,
			min, max);
	}

	public static void DrawRowDragDoubleWithEnabledCheckbox(
		bool undoable,
		string title,
		object o,
		string fieldName,
		string enabledFieldName,
		bool affectsFile,
		string help = null,
		float speed = 0.0001f,
		string format = "%.6f",
		double min = double.MinValue,
		double max = double.MaxValue)
	{
		title ??= "";

		DrawRowTitleAndAdvanceColumn(title);

		bool enabled;

		var controlWidth = ImGui.GetContentRegionAvail().X - CheckBoxWidth - ImGui.GetStyle().ItemSpacing.X;

		// Draw the checkbox for enabling the other control.
		if (DrawCheckbox(false, title + "check", o, enabledFieldName, CheckBoxWidth, affectsFile, GetDragHelpText(help)))
		{
			if (undoable)
			{
				enabled = GetValueFromFieldOrProperty<bool>(o, enabledFieldName);
				ActionQueue.Instance.Do(new ActionSetObjectFieldOrPropertyValue<bool>(
					o, enabledFieldName, enabled, !enabled, affectsFile));
			}
		}

		enabled = GetValueFromFieldOrProperty<bool>(o, enabledFieldName);
		if (!enabled)
			PushDisabled();

		// Control for the double value.
		ImGui.SameLine();
		DrawDragDouble(undoable, title, o, fieldName, controlWidth, null, speed, format, affectsFile, min, max);

		if (!enabled)
			PopDisabled();
	}

	public static void DrawRowDragDoubleWithOneButton(
		bool undoable,
		string title,
		object o,
		string fieldName,
		bool affectsFile,
		Action action,
		string text,
		float width,
		bool enabled,
		string help = null,
		float speed = 0.0001f,
		string format = "%.6f",
		double min = double.MinValue,
		double max = double.MaxValue)
	{
		DrawRowTitleAndAdvanceColumn(title);

		var dragDoubleWidth = ImGui.GetContentRegionAvail().X - width - ImGui.GetStyle().ItemSpacing.X;
		DrawDragDouble(undoable, title, o, fieldName, dragDoubleWidth, help, speed, format, affectsFile, min, max);

		ImGui.SameLine();
		if (!enabled)
			PushDisabled();
		if (ImGui.Button($"{text}{GetElementTitle(title, fieldName)}", new Vector2(width, 0.0f)))
		{
			action();
		}

		if (!enabled)
			PopDisabled();
	}

	public static void DrawRowDragDoubleWithTwoButtons(
		bool undoable,
		string title,
		object o,
		string fieldName,
		bool affectsFile,
		Action action1,
		string text1,
		float width1,
		Action action2,
		string text2,
		float width2,
		string help = null,
		float speed = 0.0001f,
		string format = "%.6f",
		double min = double.MinValue,
		double max = double.MaxValue)
	{
		DrawRowTitleAndAdvanceColumn(title);

		var dragDoubleWidth = ImGui.GetContentRegionAvail().X - width1 - width2 - ImGui.GetStyle().ItemSpacing.X * 2;
		DrawDragDouble(undoable, title, o, fieldName, dragDoubleWidth, help, speed, format, affectsFile, min, max);

		ImGui.SameLine();
		if (ImGui.Button($"{text1}{GetElementTitle(title, fieldName)}", new Vector2(width1, 0.0f)))
		{
			action1();
		}

		ImGui.SameLine();
		if (ImGui.Button($"{text2}{GetElementTitle(title, fieldName)}", new Vector2(width2, 0.0f)))
		{
			action2();
		}
	}

	public static void DrawRowDragDoubleWithThreeButtons(
		bool undoable,
		string title,
		object o,
		string fieldName,
		bool affectsFile,
		Action action1,
		string text1,
		float width1,
		Action action2,
		string text2,
		float width2,
		Action action3,
		string text3,
		float width3,
		string help = null,
		float speed = 0.0001f,
		string format = "%.6f",
		double min = double.MinValue,
		double max = double.MaxValue)
	{
		DrawRowTitleAndAdvanceColumn(title);

		var dragDoubleWidth = ImGui.GetContentRegionAvail().X - width1 - width2 - width3 - ImGui.GetStyle().ItemSpacing.X * 3;
		DrawDragDouble(undoable, title, o, fieldName, dragDoubleWidth, help, speed, format, affectsFile, min, max);

		ImGui.SameLine();
		if (ImGui.Button($"{text1}{GetElementTitle(title, fieldName)}", new Vector2(width1, 0.0f)))
		{
			action1();
		}

		ImGui.SameLine();
		if (ImGui.Button($"{text2}{GetElementTitle(title, fieldName)}", new Vector2(width2, 0.0f)))
		{
			action2();
		}

		ImGui.SameLine();
		if (ImGui.Button($"{text3}{GetElementTitle(title, fieldName)}", new Vector2(width3, 0.0f)))
		{
			action3();
		}
	}

	public static bool DrawRowDragDoubleWithThreeButtons(
		string title,
		ref double value,
		Action action1,
		string text1,
		float width1,
		Action action2,
		string text2,
		float width2,
		Action action3,
		string text3,
		float width3,
		string help = null,
		float speed = 0.0001f,
		string format = "%.6f",
		double min = double.MinValue,
		double max = double.MaxValue)
	{
		DrawRowTitleAndAdvanceColumn(title);

		var dragDoubleWidth = ImGui.GetContentRegionAvail().X - width1 - width2 - width3 - ImGui.GetStyle().ItemSpacing.X * 3;
		var ret = DrawDragDouble(title, ref value, dragDoubleWidth, help, speed, format, min, max);

		ImGui.SameLine();
		if (ImGui.Button($"{text1}{GetElementTitle(title)}", new Vector2(width1, 0.0f)))
		{
			action1();
		}

		ImGui.SameLine();
		if (ImGui.Button($"{text2}{GetElementTitle(title)}", new Vector2(width2, 0.0f)))
		{
			action2();
		}

		ImGui.SameLine();
		if (ImGui.Button($"{text3}{GetElementTitle(title)}", new Vector2(width3, 0.0f)))
		{
			action3();
		}

		return ret;
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
		bool affectsFile,
		double min = double.MinValue,
		double max = double.MaxValue)
	{
		(bool, double) Func(double v)
		{
			var r = DragDouble(ref v, GetElementTitle(title, fieldName), speed, format, min, max);
			return (r, v);
		}

		return DrawLiveEditValue<double>(undoable, title, o, fieldName, width, affectsFile, Func, DoubleCompare,
			GetDragHelpText(help));
	}

	// ReSharper disable once UnusedMethodReturnValue.Local
	private static bool DrawDragDoubleCached(
		bool undoable,
		string title,
		object o,
		string fieldName,
		float width,
		string help,
		float speed,
		string format,
		bool affectsFile,
		double min = double.MinValue,
		double max = double.MaxValue)
	{
		(bool, double) Func(double v)
		{
			var r = DragDouble(ref v, GetElementTitle(title, fieldName), speed, format, min, max);
			return (r, v);
		}

		bool ValidationFunc(double v)
		{
			return true;
		}

		return DrawCachedEditValue<double>(undoable, title, o, fieldName, width, affectsFile, Func, DoubleCompare, ValidationFunc,
			GetDragHelpText(help));
	}

	public static void DrawRowDragDoubleRange(
		bool undoable,
		string title,
		object o,
		string beginFieldName,
		string endFieldName,
		bool affectsFile,
		string help = null,
		float speed = 0.0001f,
		string format = "%.6f",
		double min = double.MinValue,
		double max = double.MaxValue)
	{
		title ??= "";

		DrawRowTitleAndAdvanceColumn(title);

		var remainingWidth = DrawHelp(GetDragHelpText(help), ImGui.GetContentRegionAvail().X);

		DrawDragDoubleRange(undoable, title, o, beginFieldName, endFieldName, affectsFile, remainingWidth, speed, format, min,
			max);
	}

	public static void DrawDragDoubleRange(
		bool undoable,
		string title,
		object o,
		string beginFieldName,
		string endFieldName,
		bool affectsFile,
		float width,
		float speed = 0.0001f,
		string format = "%.6f",
		double min = double.MinValue,
		double max = double.MaxValue)
	{
		var currentBeginValue = GetValueFromFieldOrProperty<double>(o, beginFieldName);
		var currentEndValue = GetValueFromFieldOrProperty<double>(o, endFieldName);
		var maxForBegin = Math.Min(max, currentEndValue);
		var minForEnd = Math.Max(min, currentBeginValue);

		var controlWidth = Math.Max(0.0f, 0.5f * (width - ImGui.GetStyle().ItemSpacing.X * 2.0f - RangeToWidth));

		// Controls for the double values.
		ImGui.SameLine();
		DrawDragDouble(undoable, title, o, beginFieldName, controlWidth, null, speed, format, affectsFile, min, maxForBegin);

		// "to" text
		ImGui.SameLine();
		Text("to", RangeToWidth);

		ImGui.SameLine();
		DrawDragDouble(undoable, title, o, endFieldName, controlWidth, null, speed, format, affectsFile, minForEnd, max);
	}

	public static void DrawRowDragDoubleXY(
		bool undoable,
		string title,
		object o,
		string xFieldName,
		string yFieldName,
		bool affectsFile,
		string help = null,
		float speed = 0.0001f,
		string format = "%.6f",
		double min = double.MinValue,
		double max = double.MaxValue)
	{
		title ??= "";

		DrawRowTitleAndAdvanceColumn(title);

		var remainingWidth = DrawHelp(GetDragHelpText(help), ImGui.GetContentRegionAvail().X);
		var controlWidth = Math.Max(0.0f,
			0.5f * (remainingWidth - ImGui.GetStyle().ItemSpacing.X * 3.0f - TextXWidth - TextYWidth));

		// X text
		ImGui.SameLine();
		Text("X", TextXWidth);

		// Control for X.
		ImGui.SameLine();
		DrawDragDouble(undoable, title, o, xFieldName, controlWidth, null, speed, format, affectsFile, min, max);

		// Y text
		ImGui.SameLine();
		Text("Y", TextYWidth);

		// Control for Y
		ImGui.SameLine();
		DrawDragDouble(undoable, title, o, yFieldName, controlWidth, null, speed, format, affectsFile, min, max);
	}

	public static void DrawRowDragDoubleLatitudeLongitude(
		bool undoable,
		string title,
		object o,
		string latFieldName,
		string longFieldName,
		bool affectsFile,
		string help = null,
		float speed = 0.0001f,
		string format = "%.6f",
		double min = double.MinValue,
		double max = double.MaxValue)
	{
		title ??= "";

		DrawRowTitleAndAdvanceColumn(title);

		var remainingWidth = DrawHelp(GetDragHelpText(help), ImGui.GetContentRegionAvail().X);
		var controlWidth = Math.Max(0.0f,
			0.5f * (remainingWidth - ImGui.GetStyle().ItemSpacing.X * 3.0f - TextLatWidth - TextLongWidth));

		// Latitude text
		ImGui.SameLine();
		Text("Lat", TextLatWidth);

		// Control for X.
		ImGui.SameLine();
		DrawDragDouble(undoable, title, o, latFieldName, controlWidth, null, speed, format, affectsFile, min, max);

		// Longitude text
		ImGui.SameLine();
		Text("Long", TextLongWidth);

		// Control for Y
		ImGui.SameLine();
		DrawDragDouble(undoable, title, o, longFieldName, controlWidth, null, speed, format, affectsFile, min, max);
	}

	#endregion Drag Double

	#region Enum

	public static bool DrawRowEnum<T>(string title, string elementName, ref T value, T[] allowedValues = null, string help = null)
		where T : struct, Enum
	{
		DrawRowTitleAndAdvanceColumn(title);
		ImGui.SetNextItemWidth(DrawHelp(help, ImGui.GetContentRegionAvail().X));

		if (allowedValues != null)
			return ComboFromEnum(GetElementTitle(title, elementName), ref value, allowedValues,
				GetElementTitle(title, elementName));
		return ComboFromEnum(GetElementTitle(title, elementName), ref value);
	}

	public static bool DrawRowEnum<T>(bool undoable, string title, object o, string fieldName, bool affectsFile,
		string help = null, T defaultValue = default)
		where T : struct, Enum
	{
		DrawRowTitleAndAdvanceColumn(title);
		return DrawEnum(undoable, title, o, fieldName, ImGui.GetContentRegionAvail().X, null, affectsFile, help, defaultValue);
	}

	public static bool DrawRowEnumWithButton<T>(bool undoable, string title, object o, string fieldName, bool affectsFile,
		string buttonText, Action buttonAction, float buttonWidth,
		string help = null, T defaultValue = default)
		where T : struct, Enum
	{
		DrawRowTitleAndAdvanceColumn(title);
		var enumWidth = ImGui.GetContentRegionAvail().X - ImGui.GetStyle().ItemSpacing.X - buttonWidth;

		var returnValue = DrawEnum(undoable, title, o, fieldName, enumWidth, null, affectsFile, help, defaultValue);

		ImGui.SameLine();
		if (ImGui.Button($"{buttonText}{GetElementTitle(title, "Button")}", new Vector2(buttonWidth, 0.0f)))
		{
			buttonAction();
		}

		return returnValue;
	}

	public static bool DrawRowEnum<T>(bool undoable, string title, object o, string fieldName, T[] allowedValues,
		bool affectsFile, string help = null, T defaultValue = default) where T : struct, Enum
	{
		DrawRowTitleAndAdvanceColumn(title);
		return DrawEnum(undoable, title, o, fieldName, ImGui.GetContentRegionAvail().X, allowedValues, affectsFile, help,
			defaultValue);
	}

	public static bool DrawEnum<T>(bool undoable, string title, object o, string fieldName, float width, T[] allowedValues,
		bool affectsFile, string help = null, T defaultValue = default) where T : struct, Enum
	{
		var value = defaultValue;
		if (o != null)
			value = GetValueFromFieldOrProperty<T>(o, fieldName);

		var itemWidth = DrawHelp(help, width);
		ImGui.SetNextItemWidth(itemWidth);
		var newValue = value;
		bool ret;
		if (allowedValues != null)
			ret = ComboFromEnum(GetElementTitle(title, fieldName), ref newValue, allowedValues,
				GetElementTitle(title, fieldName));
		else
			ret = ComboFromEnum(GetElementTitle(title, fieldName), ref newValue);
		if (ret)
		{
			if (!newValue.Equals(value))
			{
				if (undoable)
					ActionQueue.Instance.Do(
						new ActionSetObjectFieldOrPropertyValue<T>(o, fieldName, newValue, value, affectsFile));
				else
					SetFieldOrPropertyToValue(o, fieldName, newValue);
			}
		}

		return ret;
	}

	#endregion Enum

	#region Step Graph Selection

	public static void DrawRowStepGraphMultiSelection(bool undoable, string title, object o, string fieldName, bool affectsFile,
		string help = null)
	{
		var originalValues = GetValueFromFieldOrProperty<HashSet<ChartType>>(o, fieldName);

		// Draw the title and help.
		DrawRowTitleAndAdvanceColumn(title);
		DrawHelp(help, ImGui.GetContentRegionAvail().X);

		// Start a new table with one column. The first row is buttons, the second is a sub-table for choosing types.
		if (ImGui.BeginTable("StepGraph Outer Table", 1))
		{
			// First row of buttons.
			ImGui.TableNextRow();
			ImGui.TableSetColumnIndex(0);
			if (ImGui.Button("Select All"))
			{
				if (undoable)
				{
					var allSet = new HashSet<ChartType>();
					foreach (var chartType in SupportedSinglePlayerChartTypes)
						allSet.Add(chartType);

					ActionQueue.Instance.Do(
						new ActionSetObjectFieldOrPropertyReferenceNoClone<HashSet<ChartType>>(o, fieldName, allSet,
							affectsFile));
				}
				else
				{
					originalValues.Clear();
					foreach (var chartType in SupportedSinglePlayerChartTypes)
						originalValues.Add(chartType);
				}
			}

			// Second row with a sub-table.
			ImGui.TableNextRow();
			ImGui.TableSetColumnIndex(0);
			if (ImGui.BeginTable("StepGraph Inner Table", 3))
			{
				for (var i = 0; i < StartupStepGraphOptions.Count;)
				{
					// If we reach a row with no valid types, stop adding rows.
					var rowIsEmpty = true;
					for (var col = 0; col < 3; col++)
					{
						if (StartupStepGraphOptions[i + col] != null)
						{
							rowIsEmpty = false;
							break;
						}
					}

					if (rowIsEmpty)
						break;

					// Start a new row.
					ImGui.TableNextRow();
					for (var col = 0; col < 3; col++, i++)
					{
						// Start a new column.
						ImGui.TableSetColumnIndex(col);
						if (StartupStepGraphOptions[i] != null)
						{
							var originalValue = originalValues.Contains(StartupStepGraphOptions[i].Value);
							var value = originalValue;
							if (ImGui.Checkbox(GetPrettyEnumString(StartupStepGraphOptions[i].Value), ref value) &&
							    value != originalValue)
							{
								// Update the value with an undoable action.
								if (undoable)
								{
									// Replace the set with a new set.
									var newValues = new HashSet<ChartType>();
									foreach (var chartType in SupportedSinglePlayerChartTypes)
									{
										if (chartType == StartupStepGraphOptions[i].Value)
										{
											if (value)
												newValues.Add(chartType);
										}
										else
										{
											if (originalValues.Contains(chartType))
											{
												newValues.Add(chartType);
											}
										}
									}

									ActionQueue.Instance.Do(
										new ActionSetObjectFieldOrPropertyReferenceNoClone<HashSet<ChartType>>(o, fieldName,
											newValues, affectsFile));
								}
								// Update the value directly.
								else
								{
									if (value)
										originalValues.Add(StartupStepGraphOptions[i].Value);
									else
										originalValues.Remove(StartupStepGraphOptions[i].Value);
								}
							}
						}
					}
				}

				ImGui.EndTable();
			}

			ImGui.EndTable();
		}
	}

	#endregion Step Graph Selection

	#region Color Edit 3

	public static void DrawRowColorEdit3(
		bool undoable,
		string title,
		object o,
		string fieldName,
		ImGuiColorEditFlags flags,
		bool affectsFile,
		string help = null)
	{
		DrawRowTitleAndAdvanceColumn(title);
		DrawColorEdit3(undoable, title, o, fieldName, ImGui.GetContentRegionAvail().X, flags, affectsFile, help);
	}

	public static void DrawColorEdit3(
		bool undoable,
		string title,
		object o,
		string fieldName,
		float width,
		ImGuiColorEditFlags flags,
		bool affectsFile,
		string help = null)
	{
		(bool, Vector3) Func(Vector3 v)
		{
			var r = ImGui.ColorEdit3(GetElementTitle(title, fieldName), ref v, flags);
			return (r, v);
		}

		DrawLiveEditValue<Vector3>(undoable, title, o, fieldName, width, affectsFile, Func, Vector3Compare, help);
	}

	#endregion Color Edit 3

	#region Color Edit 4

	public static void DrawRowColorEdit4(
		bool undoable,
		string title,
		object o,
		string fieldName,
		ImGuiColorEditFlags flags,
		bool affectsFile,
		string help = null)
	{
		DrawRowTitleAndAdvanceColumn(title);
		DrawColorEdit4(undoable, title, o, fieldName, ImGui.GetContentRegionAvail().X, flags, affectsFile, help);
	}

	private static void DrawColorEdit4(
		bool undoable,
		string title,
		object o,
		string fieldName,
		float width,
		ImGuiColorEditFlags flags,
		bool affectsFile,
		string help = null)
	{
		(bool, Vector4) Func(Vector4 v)
		{
			var r = ImGui.ColorEdit4(GetElementTitle(title, fieldName), ref v, flags);
			return (r, v);
		}

		DrawLiveEditValue<Vector4>(undoable, title, o, fieldName, width, affectsFile, Func, Vector4Compare, help);
	}

	#endregion Color Edit 4

	#region Display Tempo

	/// <summary>
	/// Draws a row for a custom set of controls to edit an EditorChart's display tempo.
	/// </summary>
	/// <param name="undoable">Whether operations should be undoable or not.</param>
	/// <param name="chart">EditorChart object to control.</param>
	/// <param name="actualMinTempo">Actual min tempo of the Chart.</param>
	/// <param name="actualMaxTempo">Actual max tempo of the Chart.</param>
	public static void DrawRowDisplayTempo(
		bool undoable,
		EditorChart chart,
		double actualMinTempo,
		double actualMaxTempo)
	{
		const string title = "Display Tempo";
		const string help = "How the tempo for this chart should be displayed." +
		                    "\nRandom:    The actual tempo will be hidden and replaced with a random display." +
		                    "\nSpecified: A specified tempo or tempo range will be displayed." +
		                    "\n           This is a good option when tempo gimmicks would result in a misleading actual tempo range." +
		                    "\nActual:    The actual tempo or tempo range will be displayed.";
		DrawRowTitleAndAdvanceColumn(title);

		var spacing = ImGui.GetStyle().ItemSpacing.X;

		var tempoControlWidth = ImGui.GetContentRegionAvail().X - DisplayTempoEnumWidth - spacing;
		var specifiedSplitTempoWidth = Math.Max(1.0f,
			(ImGui.GetContentRegionAvail().X - DisplayTempoEnumWidth - RangeToWidth - CheckBoxWidth - spacing * 4.0f) * 0.5f);
		var actualSplitTempoWidth = Math.Max(1.0f,
			(ImGui.GetContentRegionAvail().X - DisplayTempoEnumWidth - RangeToWidth - spacing * 3.0f) * 0.5f);

		// Draw an enum for choosing the DisplayTempoMode.
		// Use a custom action to set the mode afterward if we detect it has changed.
		var previousDisplayMode = chart?.DisplayTempoMode ?? DisplayTempoMode.Actual;
		var currentDisplayMode = previousDisplayMode;
		var itemWidth = DrawHelp(help, DisplayTempoEnumWidth);
		ImGui.SetNextItemWidth(itemWidth);
		ComboFromEnum(GetElementTitle(title, "Combo"), ref currentDisplayMode);
		if (chart != null && currentDisplayMode != previousDisplayMode)
		{
			ActionQueue.Instance.Do(new ActionSetDisplayTempoMode(chart, currentDisplayMode));
		}

		// The remainder of the row depends on the mode.
		switch (chart?.DisplayTempoMode ?? DisplayTempoMode.Actual)
		{
			// For a Random display, just draw a disabled InputText with "???".
			case DisplayTempoMode.Random:
			{
				PushDisabled();
				var text = "???";
				ImGui.SetNextItemWidth(Math.Max(1.0f, tempoControlWidth));
				ImGui.SameLine();
				ImGui.InputText("", ref text, 4);
				PopDisabled();
				break;
			}

			// For a Specified display, draw the specified range.
			case DisplayTempoMode.Specified:
			{
				// DragDouble for the min.
				ImGui.SameLine();
				ImGui.SetNextItemWidth(specifiedSplitTempoWidth);
				DrawDragDouble(undoable, GetElementTitle(title, "SpecifiedMin"), chart,
					nameof(EditorChart.DisplayTempoSpecifiedTempoMin), specifiedSplitTempoWidth,
					null,
					0.001f, "%.6f", true);

				// "to" text to split the min and max.
				ImGui.SameLine();
				Text("to", RangeToWidth);

				// Checkbox for whether or not to use a distinct max.
				ImGui.SameLine();
				if (DrawCheckbox(false, GetElementTitle(title, "SpecifiedCheckbox"), chart,
					    nameof(EditorChart.DisplayTempoShouldAllowEditsOfMax), CheckBoxWidth, true))
				{
					if (undoable)
					{
						// Enqueue a custom action so that the ShouldAllowEditsOfMax and previous max tempo can be undone together.
						ActionQueue.Instance.Do(
							new ActionSetDisplayTempoAllowEditsOfMax(chart, chart!.DisplayTempoShouldAllowEditsOfMax));
					}
				}

				// If not using a distinct max, disable the max DragDouble and ensure that the max is set to the min.
				if (chart != null && !chart.DisplayTempoShouldAllowEditsOfMax)
				{
					PushDisabled();

					if (!chart.DisplayTempoSpecifiedTempoMin.DoubleEquals(chart.DisplayTempoSpecifiedTempoMax))
						chart.DisplayTempoSpecifiedTempoMax = chart.DisplayTempoSpecifiedTempoMin;
				}

				// DragDouble for the max.
				ImGui.SameLine();
				ImGui.SetNextItemWidth(specifiedSplitTempoWidth);
				DrawDragDouble(undoable, GetElementTitle(title, "SpecifiedMax"), chart,
					nameof(EditorChart.DisplayTempoSpecifiedTempoMax),
					ImGui.GetContentRegionAvail().X, null,
					0.001f, "%.6f", true);

				// Pop the disabled setting if we pushed it before.
				if (chart != null && !chart.DisplayTempoShouldAllowEditsOfMax)
				{
					PopDisabled();
				}

				break;
			}

			case DisplayTempoMode.Actual:
			{
				// The controls for the actual tempo are always disabled.
				PushDisabled();

				// If the actual tempo is one value then just draw one DragDouble.
				if (actualMinTempo.DoubleEquals(actualMaxTempo))
				{
					ImGui.SetNextItemWidth(Math.Max(1.0f, tempoControlWidth));
					ImGui.SameLine();
					DragDouble(ref actualMinTempo, GetElementTitle(title, "Actual"));
				}

				// If the actual tempo is a range then draw the min and max.
				else
				{
					// DragDouble for the min.
					ImGui.SetNextItemWidth(actualSplitTempoWidth);
					ImGui.SameLine();
					DragDouble(ref actualMinTempo, GetElementTitle(title, "ActualMin"));

					// "to" text to split the min and max.
					ImGui.SameLine();
					ImGui.TextUnformatted("to");

					// DragDouble for the max.
					ImGui.SetNextItemWidth(actualSplitTempoWidth);
					ImGui.SameLine();
					DragDouble(ref actualMaxTempo, GetElementTitle(title, "ActualMax"));
				}

				PopDisabled();
				break;
			}
		}
	}

	#endregion Display Tempo

	#region Step Tightening

	public static void DrawRowPerformedChartConfigDistanceTightening(
		StepTighteningConfig config,
		string title,
		string help)
	{
		DrawRowTitleAndAdvanceColumn(title);

		var width = DrawHelp(help, ImGui.GetContentRegionAvail().X);
		var spacing = ImGui.GetStyle().ItemSpacing.X;
		var rangeControlWidth = width - (CheckBoxWidth + spacing);

		// Enabled checkbox.
		DrawCheckbox(true, title + "check", config, nameof(Config.StepTightening.DistanceTighteningEnabled),
			CheckBoxWidth, false);
		var enabled = config.IsDistanceTighteningEnabled();
		if (!enabled)
			PushDisabled();

		DrawDragDoubleRange(true, title, config, nameof(StepTighteningConfig.DistanceMin),
			nameof(StepTighteningConfig.DistanceMax),
			false, rangeControlWidth, 0.01f, "%.6f", 0.0, 10.0);

		if (!enabled)
			PopDisabled();
	}

	public static void DrawRowPerformedChartConfigStretchTightening(
		StepTighteningConfig config,
		string title,
		string help)
	{
		DrawRowTitleAndAdvanceColumn(title);

		var width = DrawHelp(help, ImGui.GetContentRegionAvail().X);
		var spacing = ImGui.GetStyle().ItemSpacing.X;
		var rangeControlWidth = width - (CheckBoxWidth + spacing);

		// Enabled checkbox.
		DrawCheckbox(true, title + "check", config, nameof(Config.StepTightening.StretchTighteningEnabled),
			CheckBoxWidth, false);
		var enabled = config.IsStretchTighteningEnabled();
		if (!enabled)
			PushDisabled();

		DrawDragDoubleRange(true, title, config, nameof(StepTighteningConfig.StretchDistanceMin),
			nameof(StepTighteningConfig.StretchDistanceMax),
			false, rangeControlWidth, 0.01f, "%.6f", 0.0, 10.0);

		if (!enabled)
			PopDisabled();
	}

	public static void DrawRowPerformedChartConfigSpeedTightening(
		EditorPerformedChartConfig config,
		string title,
		string help)
	{
		DrawRowTitleAndAdvanceColumn(title);

		var width = DrawHelp(help, ImGui.GetContentRegionAvail().X);
		var spacing = ImGui.GetStyle().ItemSpacing.X;
		var rangeWidth = width - (CheckBoxWidth + NoteTypeComboWidth + NotesFromTextWidth + BpmTextWidth + spacing * 5);

		// Enabled checkbox.
		DrawCheckbox(true, title + "check", config.Config.StepTightening, nameof(Config.StepTightening.SpeedTighteningEnabled),
			CheckBoxWidth, false);
		var enabled = config.Config.StepTightening.IsSpeedTighteningEnabled();
		if (!enabled)
			PushDisabled();

		// Note Type.
		var index = config.TravelSpeedNoteTypeDenominatorIndex;
		ImGui.SameLine();
		ImGui.SetNextItemWidth(NoteTypeComboWidth);
		var newIndex = index;
		var ret = ComboFromArray("", ref newIndex, ValidNoteTypeStrings);
		if (ret)
		{
			if (newIndex != index)
			{
				ActionQueue.Instance.Do(
					new ActionSetObjectFieldOrPropertyValue<int>(config,
						nameof(EditorPerformedChartConfig.TravelSpeedNoteTypeDenominatorIndex), newIndex,
						false));
			}
		}

		// From text.
		ImGui.SameLine();
		Text("notes from", NotesFromTextWidth);

		// BPM Range.
		ImGui.SameLine();
		DrawDragIntRange(
			true,
			"",
			config,
			nameof(EditorPerformedChartConfig.TravelSpeedMinBPM),
			nameof(EditorPerformedChartConfig.TravelSpeedMaxBPM),
			false,
			rangeWidth,
			null,
			1F,
			"%i",
			1, 1000);

		// BPM text.
		ImGui.SameLine();
		Text("BPM", BpmTextWidth);

		if (!enabled)
			PopDisabled();
	}

	#endregion Step Tightening

	#region Foot Select

	public static void DrawRowPatternConfigStartFootChoice(
		bool undoable,
		string title,
		EditorPatternConfig editorConfig,
		string help = null)
	{
		DrawRowTitleAndAdvanceColumn(title);

		var config = editorConfig.Config;
		var choice = config.StartingFootChoice;

		// For simple enum-only choices just draw the enum and help.
		if (choice != PatternConfigStartingFootChoice.Specified)
		{
			DrawEnum<PatternConfigStartingFootChoice>(undoable, title, config, nameof(PatternConfig.StartingFootChoice),
				ImGui.GetContentRegionAvail().X, null, false, help);
			return;
		}

		// For the Specified choice, draw the help, then a shorter enum, then the foot choice enum.
		var footComboWidth = ImGui.GetContentRegionAvail().X - PatternConfigShortEnumWidth - ImGui.GetStyle().ItemSpacing.X;
		DrawEnum<PatternConfigStartingFootChoice>(undoable, title, config, nameof(PatternConfig.StartingFootChoice),
			PatternConfigShortEnumWidth, null, false, help);
		ImGui.SameLine();
		DrawEnum<Foot>(undoable, title, editorConfig, nameof(EditorPatternConfig.StartingFootSpecified),
			footComboWidth, null, false);
	}

	#endregion Foot Select

	#region Lane Select

	public static void DrawRowPatternConfigStartFootLaneChoice(
		Editor editor,
		bool undoable,
		string title,
		EditorPatternConfig editorConfig,
		ChartType? chartType,
		bool left,
		string help = null)
	{
		DrawRowTitleAndAdvanceColumn(title);

		var config = editorConfig.Config;
		var choice = left ? config.LeftFootStartChoice : config.RightFootStartChoice;
		var choiceFieldName = left ? nameof(PatternConfig.LeftFootStartChoice) : nameof(PatternConfig.RightFootStartChoice);
		var valueFieldName =
			left ? nameof(PatternConfig.LeftFootStartLaneSpecified) : nameof(PatternConfig.RightFootStartLaneSpecified);

		// For simple enum-only choices just draw the enum and help.
		if (choice != PatternConfigStartFootChoice.SpecifiedLane)
		{
			DrawEnum<PatternConfigStartFootChoice>(undoable, title, config, choiceFieldName,
				ImGui.GetContentRegionAvail().X, null, false, help);
			return;
		}

		// For the SpecifiedLane choice, draw the help, then a shorter enum, then the single lane choice control.
		var laneChoiceWidth = ImGui.GetContentRegionAvail().X - PatternConfigShortEnumWidth - ImGui.GetStyle().ItemSpacing.X;
		DrawEnum<PatternConfigStartFootChoice>(undoable, title, config, choiceFieldName,
			PatternConfigShortEnumWidth, null, false, help);
		ImGui.SameLine();

		DrawSingleLaneChoice(GetElementTitle(title, valueFieldName), editor, undoable, config, valueFieldName, false,
			laneChoiceWidth, chartType);
	}

	public static void DrawRowPatternConfigEndFootLaneChoice(
		Editor editor,
		bool undoable,
		string title,
		EditorPatternConfig editorConfig,
		ChartType? chartType,
		bool left,
		string help = null)
	{
		DrawRowTitleAndAdvanceColumn(title);

		var config = editorConfig.Config;
		var choice = left ? config.LeftFootEndChoice : config.RightFootEndChoice;
		var choiceFieldName = left ? nameof(PatternConfig.LeftFootEndChoice) : nameof(PatternConfig.RightFootEndChoice);
		var valueFieldName =
			left ? nameof(PatternConfig.LeftFootEndLaneSpecified) : nameof(PatternConfig.RightFootEndLaneSpecified);

		// For simple enum-only choices just draw the enum and help.
		if (choice != PatternConfigEndFootChoice.SpecifiedLane)
		{
			DrawEnum<PatternConfigEndFootChoice>(undoable, title, config, choiceFieldName,
				ImGui.GetContentRegionAvail().X, null, false, help);
			return;
		}

		// For the SpecifiedLane choice, draw the help, then a shorter enum, then the single lane choice control.
		var laneChoiceWidth = ImGui.GetContentRegionAvail().X - PatternConfigShortEnumWidth - ImGui.GetStyle().ItemSpacing.X;
		DrawEnum<PatternConfigEndFootChoice>(undoable, title, config, choiceFieldName,
			PatternConfigShortEnumWidth, null, false, help);
		ImGui.SameLine();

		DrawSingleLaneChoice(GetElementTitle(title, valueFieldName), editor, undoable, config, valueFieldName, false,
			laneChoiceWidth, chartType);
	}

	private static void DrawSingleLaneChoice(
		string id,
		Editor editor,
		bool undoable,
		object objectToUpdate,
		string fieldNameToUpdate,
		bool affectsFile,
		float width,
		ChartType? chartType)
	{
		var selectedLane = GetValueFromFieldOrProperty<int>(objectToUpdate, fieldNameToUpdate);
		var originalSelectedLane = selectedLane;

		var dragIntWidth = width;
		var defaultXSpacing = ImGui.GetStyle().ItemSpacing.X;

		if (chartType != null)
		{
			// Determine the width of the buttons and update the drag int width.
			var numLanes = GetChartProperties(chartType.Value).GetNumInputs();
			var buttonsWidth = numLanes * (ArrowIconWidth + ImGui.GetStyle().FramePadding.X * 2);
			dragIntWidth -= buttonsWidth + defaultXSpacing;

			// Set tighter spacing.
			var originalItemSpacingX = defaultXSpacing;
			var originalItemSpacingY = ImGui.GetStyle().ItemSpacing.Y;

			// Remove button color.
			// This looks better and works around an issue where when using ImageButton
			// we can't set an odd pixel height, which in practice is needed to match the normal
			// element height. This looks a pixel off if button colors are enabled.
			ImGui.PushStyleColor(ImGuiCol.Button, Vector4.Zero);

			// Icons.
			var textureAtlas = editor.GetTextureAtlas();
			var imGuiTextureAtlasTexture = editor.GetTextureAtlasImGuiTexture();
			var (atlasW, atlasH) = textureAtlas.GetDimensions();
			var icons = ArrowGraphicManager.GetIcons(chartType.Value);
			var dimIcons = ArrowGraphicManager.GetDimIcons(chartType.Value);
			for (var lane = 0; lane < numLanes; lane++)
			{
				ImGui.SameLine();

				var (x, y, w, h) = textureAtlas.GetSubTextureBounds(selectedLane == lane ? icons[lane] : dimIcons[lane]);

				if (ImGui.ImageButton($"##{id}SingleLaneChoice{lane}",
					    imGuiTextureAtlasTexture,
					    ArrowIconSize,
					    new Vector2(x / (float)atlasW, y / (float)atlasH),
					    new Vector2((x + w) / (float)atlasW, (y + h) / (float)atlasH)))
				{
					selectedLane = lane;
				}

				// We need to set the spacing before calling SameLine.
				if (lane == 0)
					ImGui.GetStyle().ItemSpacing.X = 0;
				else if (lane == numLanes - 1)
					ImGui.GetStyle().ItemSpacing.X = originalItemSpacingX;

				ImGui.SameLine();
			}

			// Restore button color.
			ImGui.PopStyleColor(1);

			// Restore spacing.
			ImGui.GetStyle().ItemSpacing.X = originalItemSpacingX;
			ImGui.GetStyle().ItemSpacing.Y = originalItemSpacingY;
		}

		// Persist new selection if it changed.
		if (selectedLane != originalSelectedLane)
		{
			if (undoable)
			{
				ActionQueue.Instance.Do(
					new ActionSetObjectFieldOrPropertyValue<int>(objectToUpdate, fieldNameToUpdate, selectedLane, affectsFile));
			}
			else
			{
				SetFieldOrPropertyToValue(objectToUpdate, fieldNameToUpdate, selectedLane);
			}
		}

		// Lane value.
		var maxLane = GetMaxNumLanesForAnySupportedChartType();
		DrawDragInt(true, $"##{id}SingleLaneChoiceDragInt", objectToUpdate, fieldNameToUpdate, dragIntWidth, false, "", 0.1f,
			"%i", 0, maxLane - 1);
	}

	#endregion Lane Select

	#region Subdivisions

	public static void DrawRowSubdivisions(
		bool undoable,
		string title,
		object o,
		string fieldName,
		bool affectsFile,
		string help = null)
	{
		DrawRowTitleAndAdvanceColumn(title);

		var itemWidth = DrawHelp(help, ImGui.GetContentRegionAvail().X);
		ImGui.SetNextItemWidth(itemWidth);

		var elementTitle = GetElementTitle(title, fieldName);
		var currentValue = GetValueFromFieldOrProperty<SubdivisionType>(o, fieldName);
		var originalValue = currentValue;
		var currentBeatSubdivision = GetBeatSubdivision(currentValue);
		var currentMeasureSubdivision = GetMeasureSubdivision(currentValue);

		ImGui.PushStyleColor(ImGuiCol.Text, ArrowGraphicManager.GetArrowColorForSubdivision(currentBeatSubdivision));

		if (ImGui.BeginCombo($"{elementTitle}Combo", GetPrettySubdivisionString(currentValue)))
		{
			ImGui.PopStyleColor();

			foreach (var subdivisionType in Enum.GetValues(typeof(SubdivisionType)))
			{
				var beatSubdivision = GetBeatSubdivision((SubdivisionType)subdivisionType);
				var measureSubdivision = GetMeasureSubdivision((SubdivisionType)subdivisionType);
				ImGui.PushStyleColor(ImGuiCol.Text, ArrowGraphicManager.GetArrowColorForSubdivision(beatSubdivision));

				var isSelected = currentMeasureSubdivision == measureSubdivision;
				if (ImGui.Selectable(GetPrettySubdivisionString((SubdivisionType)subdivisionType), isSelected))
				{
					currentValue = (SubdivisionType)subdivisionType;
				}

				if (isSelected)
				{
					ImGui.SetItemDefaultFocus();
				}

				ImGui.PopStyleColor();
			}

			ImGui.EndCombo();
		}
		else
		{
			ImGui.PopStyleColor();
		}

		if (currentValue != originalValue)
		{
			if (undoable)
				ActionQueue.Instance.Do(
					new ActionSetObjectFieldOrPropertyValue<SubdivisionType>(o, fieldName, currentValue, affectsFile));
			else
				SetFieldOrPropertyToValue(o, fieldName, currentValue);
		}
	}

	public static void DrawRowSnapLevels(
		string title,
		SnapManager snapManager,
		string help = null)
	{
		DrawRowTitleAndAdvanceColumn(title);

		var p = Preferences.Instance;
		var snapLevels = snapManager.GetSnapLevels();
		var snapLevelIndex = p.SnapIndex;

		var comboWidth = DrawHelp(help, ImGui.GetContentRegionAvail().X);
		var elementTitle = GetElementTitle(title);

		// Snap level.
		ImGui.SetNextItemWidth(comboWidth);
		ImGui.PushStyleColor(ImGuiCol.Text, snapLevels[snapLevelIndex].GetColor());
		if (ImGui.BeginCombo($"{elementTitle}SnapLevels", snapLevels[snapLevelIndex].GetText()))
		{
			var newIndex = snapLevelIndex;
			ImGui.PopStyleColor();

			for (var snapIndex = 0; snapIndex < snapLevels.Count; snapIndex++)
			{
				if (!SnapManager.IsSnapIndexValidForSnapLock(snapIndex))
					continue;

				var snapLevel = snapLevels[snapIndex];
				ImGui.PushStyleColor(ImGuiCol.Text, snapLevel.GetColor());
				var isSelected = snapIndex == snapLevelIndex;
				if (ImGui.Selectable(snapLevel.GetText(), isSelected))
				{
					newIndex = snapIndex;
				}

				if (isSelected)
				{
					ImGui.SetItemDefaultFocus();
				}

				ImGui.PopStyleColor();
			}

			// Update the snap level.
			p.SnapIndex = newIndex;

			ImGui.EndCombo();
		}
		else
		{
			ImGui.PopStyleColor();
		}
	}

	public static void DrawRowSnapLockLevels(
		string title,
		SnapManager snapManager,
		string help = null)
	{
		DrawRowTitleAndAdvanceColumn(title);

		var p = Preferences.Instance;
		var snapLevels = snapManager.GetSnapLevels();
		var snapLockIndex = p.SnapLockIndex;

		var comboWidth = DrawHelp(help, ImGui.GetContentRegionAvail().X);
		var elementTitle = GetElementTitle(title);

		// Snap lock level.
		ImGui.SameLine();
		ImGui.SetNextItemWidth(comboWidth);
		ImGui.PushStyleColor(ImGuiCol.Text, snapLevels[snapLockIndex].GetColor());
		if (ImGui.BeginCombo($"{elementTitle}SnapLockLevels", snapLevels[snapLockIndex].GetText()))
		{
			var newIndex = snapLockIndex;
			ImGui.PopStyleColor();

			for (var snapIndex = 0; snapIndex < snapLevels.Count; snapIndex++)
			{
				var snapLevel = snapLevels[snapIndex];
				ImGui.PushStyleColor(ImGuiCol.Text, snapLevel.GetColor());
				var isSelected = snapIndex == snapLockIndex;
				if (ImGui.Selectable(snapLevel.GetText(), isSelected))
				{
					newIndex = snapIndex;
				}

				if (isSelected)
				{
					ImGui.SetItemDefaultFocus();
				}

				ImGui.PopStyleColor();
			}

			// Update the lock level and ensure the snap index is valid for it.
			if (newIndex != snapLockIndex)
			{
				p.SnapLockIndex = newIndex;
				var shouldAdjustSnapIndex = false;
				while (!SnapManager.IsSnapIndexValidForSnapLock(p.SnapIndex))
				{
					shouldAdjustSnapIndex = true;
					p.SnapIndex++;
					if (p.SnapIndex >= snapLevels.Count)
						p.SnapIndex = 0;
				}

				// When changing the snap lock and the snap index was set to specific note type
				// we should avoid setting it to None. It is better to set it to the new lock level.
				if (shouldAdjustSnapIndex && p.SnapLockIndex != 0 && p.SnapIndex == 0)
					p.SnapIndex = p.SnapLockIndex;
			}

			ImGui.EndCombo();
		}
		else
		{
			ImGui.PopStyleColor();
		}
	}

	#endregion Subdivisions

	#region Misc Editor Events

	private static double? MiscEditorEventHeight;

	public static double GetMiscEditorEventHeight()
	{
		if (MiscEditorEventHeight != null)
			return (double)MiscEditorEventHeight;

		unsafe
		{
			if (ImGuiFont.NativePtr == null || !ImGuiFont.IsLoaded())
				return 0.0;
		}

		ImGui.PushFont(ImGuiFont);
		MiscEditorEventHeight = ImGui.GetStyle().FramePadding.Y * 2 + ImGui.GetFontSize() + 2;
		ImGui.PopFont();
		return (double)MiscEditorEventHeight;
	}

	public static double GetMiscEditorEventDragIntWidgetWidth(int i, string format)
	{
		return GetMiscEditorEventStringWidth(FormatImGuiInt(format, i));
	}

	public static void MiscEditorEventDragIntWidget(
		string id,
		EditorEvent e,
		string fieldName,
		int x,
		int y,
		int width,
		uint colorRGBA,
		bool selected,
		bool canBeDeleted,
		float speed,
		string format,
		float alpha,
		string help,
		int min = int.MinValue,
		int max = int.MaxValue)
	{
		if (alpha <= 0.0f)
			return;

		void Func(float elementWidth)
		{
			DrawDragInt(true, $"##{id}", e, fieldName, elementWidth, true, "", speed, format, min, max);
		}

		MiscEditorEventWidget(id, e, x, y, width, colorRGBA, selected, canBeDeleted, alpha, help, Func);
	}

	public static double GetMiscEditorEventDragDoubleWidgetWidth(double d, string format)
	{
		return GetMiscEditorEventStringWidth(FormatImGuiDouble(format, d));
	}

	public static void MiscEditorEventDragDoubleWidget(
		string id,
		EditorEvent e,
		string fieldName,
		int x,
		int y,
		int width,
		uint colorRGBA,
		bool selected,
		bool canBeDeleted,
		float speed,
		string format,
		float alpha,
		string help,
		double min = double.MinValue,
		double max = double.MaxValue)
	{
		if (alpha <= 0.0f)
			return;

		void Func(float elementWidth)
		{
			DrawDragDouble(true, $"##{id}", e, fieldName, elementWidth, "", speed, format, true, min, max);
		}

		MiscEditorEventWidget(id, e, x, y, width, colorRGBA, selected, canBeDeleted, alpha, help, Func);
	}

	public static void MiscEditorEventPreviewDragDoubleWidget(
		string id,
		EditorEvent e,
		string fieldName,
		int x,
		int y,
		int width,
		uint colorRGBA,
		bool selected,
		float speed,
		string format,
		float alpha,
		string help,
		double min = double.MinValue,
		double max = double.MaxValue)
	{
		if (alpha <= 0.0f)
			return;

		void Func(float elementWidth)
		{
			DrawDragDouble(true, $"##{id}", e, fieldName, elementWidth, "", speed, format, true, min, max);
		}

		MiscEditorEventWidget(id, e, x, y, width, colorRGBA, selected, false, alpha, help, Func);
	}

	public static double GetMiscEditorEventStringWidth(string s)
	{
		unsafe
		{
			if (ImGuiFont.NativePtr == null || !ImGuiFont.IsLoaded())
				return 0.0;
		}

		ImGui.PushFont(ImGuiFont);
		var width = ImGui.CalcTextSize(s).X + GetCloseWidth();
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
		uint colorRGBA,
		bool selected,
		bool canBeDeleted,
		float alpha,
		string help)
	{
		if (alpha <= 0.0f)
			return;

		void Func(float elementWidth)
		{
			DrawTimeSignatureInput(true, $"##{id}", e, fieldName, elementWidth, true);
		}

		MiscEditorEventWidget(id, e, x, y, width, colorRGBA, selected, canBeDeleted, alpha, help, Func);
	}

	public static void MiscEditorEventMultipliersWidget(
		string id,
		EditorEvent e,
		string fieldName,
		int x,
		int y,
		int width,
		uint colorRGBA,
		bool selected,
		bool canBeDeleted,
		float alpha,
		string help)
	{
		if (alpha <= 0.0f)
			return;

		void Func(float elementWidth)
		{
			DrawMultipliersInput(true, $"##{id}", e, fieldName, elementWidth, true);
		}

		MiscEditorEventWidget(id, e, x, y, width, colorRGBA, selected, canBeDeleted, alpha, help, Func);
	}

	public static void MiscEditorEventLabelWidget(
		string id,
		EditorEvent e,
		string fieldName,
		int x,
		int y,
		int width,
		uint colorRGBA,
		bool selected,
		bool canBeDeleted,
		float alpha,
		string help)
	{
		if (alpha <= 0.0f)
			return;

		void Func(float elementWidth)
		{
			DrawLabelInput(true, $"##{id}", e, fieldName, elementWidth, true);
		}

		MiscEditorEventWidget(id, e, x, y, width, colorRGBA, selected, canBeDeleted, alpha, help, Func);
	}

	public static void MiscEditorEventLastSecondHintWidget(
		string id,
		EditorEvent e,
		string fieldName,
		int x,
		int y,
		int width,
		uint colorRGBA,
		bool selected,
		bool canBeDeleted,
		float speed,
		string format,
		float alpha,
		string help,
		double min = double.MinValue,
		double max = double.MaxValue)
	{
		if (alpha <= 0.0f)
			return;

		void Func(float elementWidth)
		{
			DrawDragDoubleCached(true, $"##{id}", e, fieldName, elementWidth, "", speed, format, true, min, max);
		}

		MiscEditorEventWidget(id, e, x, y, width, colorRGBA, selected, canBeDeleted, alpha, help, Func);
	}

	public static void MiscEditorEventScrollRateInterpolationInputWidget(
		string id,
		EditorEvent e,
		string fieldName,
		int x,
		int y,
		int width,
		uint colorRGBA,
		bool selected,
		bool canBeDeleted,
		float alpha,
		string help)
	{
		if (alpha <= 0.0f)
			return;

		void Func(float elementWidth)
		{
			DrawScrollRateInterpolationInput(true, $"##{id}", e, fieldName, elementWidth, true);
		}

		MiscEditorEventWidget(id, e, x, y, width, colorRGBA, selected, canBeDeleted, alpha, help, Func);
	}

	public static void MiscEditorEventAttackWidget(
		string id,
		EditorAttackEvent e,
		int x,
		int y,
		int width,
		uint colorRGBA,
		bool selected,
		float alpha,
		string help,
		Action requestEditCallback)
	{
		if (alpha <= 0.0f)
			return;

		void Func(float elementWidth)
		{
			var colorPushCount = 0;
			if (alpha < 1.0f)
			{
				PushAlpha(ImGuiCol.Text, alpha);
				colorPushCount++;
			}

			ImGui.PushStyleColor(ImGuiCol.Button, colorRGBA);
			colorPushCount++;

			if (ImGui.Button($"{e.GetMiscEventText()}##{id}"))
			{
				requestEditCallback();
			}

			ImGui.PopStyleColor(colorPushCount);
		}

		MiscEditorEventWidget(id, e, x, y, width, colorRGBA, selected, true, alpha, help, Func);
	}

	public static void MiscEditorEventPatternWidget(
		string id,
		EditorPatternEvent e,
		int x,
		int y,
		int width,
		uint colorRGBA,
		bool selected,
		float alpha,
		string help,
		Action requestEditCallback)
	{
		if (alpha <= 0.0f)
			return;

		void Func(float elementWidth)
		{
			var colorPushCount = 1;
			ImGui.PushStyleColor(ImGuiCol.Text, e.GetMiscEventTextColor());
			if (alpha < 1.0f)
			{
				PushAlpha(ImGuiCol.Text, alpha);
				colorPushCount++;
			}

			ImGui.PushStyleColor(ImGuiCol.Button, colorRGBA);
			colorPushCount++;

			if (ImGui.Button($"{e.GetMiscEventText()}##{id}"))
			{
				requestEditCallback();
			}

			ImGui.PopStyleColor(colorPushCount);
		}

		MiscEditorEventWidget(id, e, x, y, width, colorRGBA, selected, true, alpha, help, Func);
	}

	private static void MiscEditorEventWidget(
		string id,
		EditorEvent e,
		int x,
		int y,
		int width,
		uint colorRGBA,
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
			ImGui.PushStyleColor(ImGuiCol.ChildBg, 0xFF484848);
			ImGui.PushStyleColor(ImGuiCol.Border, 0xFFFFFFFF);
			colorPushCount += 2;
		}
		else
		{
			ImGui.PushStyleColor(ImGuiCol.ChildBg, UIWindowColor);
			colorPushCount += 1;
		}

		// Color the frame background to help differentiate controls.
		ImGui.PushStyleColor(ImGuiCol.FrameBg, colorRGBA);
		colorPushCount += 1;

		// If fading out, multiply key window elements by the alpha value.
		if (alpha < 1.0f)
		{
			PushAlpha(ImGuiCol.ChildBg, alpha);
			PushAlpha(ImGuiCol.Button, alpha);
			PushAlpha(ImGuiCol.FrameBg, alpha);
			PushAlpha(ImGuiCol.Text, alpha);
			PushAlpha(ImGuiCol.Border, alpha);
			colorPushCount += 5;
		}

		var height = (int)e.H;

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
		if (ImGui.BeginChild($"##Widget{id}", new Vector2(width, height), ImGuiChildFlags.Border, ChartAreaChildWindowFlags))
		{
			var elementWidth = width - GetCloseWidth();

			// Draw the control.
			func(elementWidth);

			// Record whether or not we should draw help text.
			if (!string.IsNullOrEmpty(help) && ImGui.IsItemHovered())
				drawHelp = true;

			// Delete button
			ImGui.SameLine();
			if (!canBeDeleted)
				PushDisabled();
			if (ImGui.Button($"X##{id}", new Vector2(GetCloseWidth(), 0.0f)))
				ActionQueue.Instance.Do(new ActionDeleteEditorEvents(e));
			if (!canBeDeleted)
				PopDisabled();
		}

		ImGui.EndChild();

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

	#region Attacks

	public static void DrawRowModifier(
		object o,
		string fieldName,
		bool affectsFile,
		string[] modifierChoices)
	{
		const string title = "Modifier";
		const string help = "Modifier to apply."
		                    + "\nThe modifiers supported by Stepmania vary by version and fork. Modifier names are not"
		                    + " case-sensitive. The dropdown list is meant as a convenience and while it contains most"
		                    + " modifiers supported by modern versions Stepmania it should not be considered exhaustive"
		                    + " or accurate for all versions."
		                    + "\n\nIn addition to the modifiers listed, Stepmania also supports the following values."
		                    + "\n<number>x:       X speed modifier"
		                    + "\nc<number>:       C speed modifier"
		                    + "\nm<number>:       M speed modifier"
		                    + "\n<noteskin name>: Noteskin";

		DrawRowTitleAndAdvanceColumn(title);
		var width = DrawHelp(help, ImGui.GetContentRegionAvail().X);
		var spacing = ImGui.GetStyle().ItemSpacing.X;

		var controlWidth = (int)((width - spacing) * 0.5);

		// Text input for manual entry.
		var textInputTitle = GetElementTitle(title, "TextInput");
		DrawTextInput(true, textInputTitle, o, fieldName, controlWidth, affectsFile,
			EditorAttackEvent.IsValidModString);

		// Combo control for quick selection.
		ImGui.SameLine();
		var comboTitle = GetElementTitle(title, "Combo");
		ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
		var selectedIndex = -1;
		if (ComboFromArray(comboTitle, ref selectedIndex, modifierChoices))
		{
			if (selectedIndex >= 0 && selectedIndex < modifierChoices.Length)
			{
				ActionQueue.Instance.Do(
					new ActionSetObjectFieldOrPropertyReference<string>(o, fieldName, modifierChoices[selectedIndex],
						affectsFile));
			}
		}
	}

	#endregion Attacks

	#region Chart Position

	public static void DrawRowChartPosition(
		string title,
		Editor editor,
		EditorEvent editorEvent,
		string help = null)
	{
		DrawRowTitleAndAdvanceColumn(title);

		var width = DrawHelp(help, ImGui.GetContentRegionAvail().X);
		var dragIntWidth = (width - ButtonGoWidth - ButtonUseCurrentRowWidth - ImGui.GetStyle().ItemSpacing.X * 4.0f) / 3.0f;
		var dragIntCacheKey = GetCacheKey(title, "ChartPositionDragInt");

		// DragInt for row
		var row = (int)editorEvent.GetChartPosition();
		var value = row;
		var rowToCache = row;
		ImGui.SetNextItemWidth(dragIntWidth);
		if (DragInt(ref value, GetElementTitle(title, "DragIntRow"), 1.0f, "row %i", 0))
		{
			editorEvent.GetEditorChart().MoveEvent(editorEvent, value);
		}

		if (ImGui.IsItemActivated())
		{
			SetCachedValue(dragIntCacheKey, rowToCache);
		}

		if (ImGui.IsItemDeactivatedAfterEdit())
		{
			var originalRow = GetCachedValue<int>(dragIntCacheKey);
			var newRow = value;
			if (newRow != originalRow)
			{
				ActionQueue.Instance.Do(new ActionMoveEditorEvent(editorEvent, newRow, originalRow));
			}
		}

		// DragInt for beat
		ImGui.SameLine();
		row = (int)editorEvent.GetChartPosition();
		value = row / MaxValidDenominator;
		rowToCache = row;
		ImGui.SetNextItemWidth(dragIntWidth);
		if (DragInt(ref value, GetElementTitle(title, "DragIntBeat"), 0.1f, "beat %i", 0))
		{
			var newRow = value * MaxValidDenominator;
			editorEvent.GetEditorChart().MoveEvent(editorEvent, newRow);
		}

		if (ImGui.IsItemActivated())
		{
			SetCachedValue(dragIntCacheKey, rowToCache);
		}

		if (ImGui.IsItemDeactivatedAfterEdit())
		{
			var originalRow = GetCachedValue<int>(dragIntCacheKey);
			var newRow = value * MaxValidDenominator;
			if (newRow != originalRow)
			{
				ActionQueue.Instance.Do(new ActionMoveEditorEvent(editorEvent, newRow, originalRow));
			}
		}

		// DragInt for measure
		ImGui.SameLine();
		row = (int)editorEvent.GetChartPosition();
		value = (int)editorEvent.GetEditorChart().GetMeasureForEvent(editorEvent);
		rowToCache = row;
		ImGui.SetNextItemWidth(dragIntWidth);
		if (DragInt(ref value, GetElementTitle(title, "DragIntMeasure"), 0.1f, "measure %i", 0))
		{
			var newRow = (int)editorEvent.GetEditorChart().GetChartPositionForMeasure(value);
			editorEvent.GetEditorChart().MoveEvent(editorEvent, newRow);
		}

		if (ImGui.IsItemActivated())
		{
			SetCachedValue(dragIntCacheKey, rowToCache);
		}

		if (ImGui.IsItemDeactivatedAfterEdit())
		{
			var originalRow = GetCachedValue<int>(dragIntCacheKey);
			var newRow = (int)editorEvent.GetEditorChart().GetChartPositionForMeasure(value);
			if (newRow != originalRow)
			{
				ActionQueue.Instance.Do(new ActionMoveEditorEvent(editorEvent, newRow, originalRow));
			}
		}

		// Use current row button
		ImGui.SameLine();
		if (ImGui.Button($"Use Current{GetElementTitle(title, "CurrentButton")}", new Vector2(ButtonUseCurrentRowWidth, 0.0f)))
		{
			var editorRow = (int)Math.Max(0.0, Math.Round(editor.GetPosition().ChartPosition));
			if (row != editorRow)
			{
				ActionQueue.Instance.Do(new ActionMoveEditorEvent(editorEvent, editorRow, row));
			}
		}

		// Go button
		ImGui.SameLine();
		if (ImGui.Button($"Go{GetElementTitle(title, "GoButton")}", new Vector2(ButtonGoWidth, 0.0f)))
		{
			editor.SetChartPosition(row);
		}
	}

	public static void DrawRowChartPositionFromLength(
		string title,
		Editor editor,
		EditorEvent editorEvent,
		string lengthField,
		string help = null)
	{
		DrawRowTitleAndAdvanceColumn(title);

		var width = DrawHelp(help, ImGui.GetContentRegionAvail().X);
		var dragIntWidth = (width - ButtonGoWidth - ButtonUseCurrentRowWidth - ImGui.GetStyle().ItemSpacing.X * 4.0f) / 3.0f;

		var dragIntCacheKey = GetCacheKey(title, "ChartPositionFromLengthDragInt");

		var length = GetValueFromFieldOrProperty<int>(editorEvent, lengthField);

		// DragInt for row
		var startPos = (int)editorEvent.GetChartPosition();
		var endRow = startPos + length;
		var value = endRow;
		var lengthToCache = length;
		ImGui.SetNextItemWidth(dragIntWidth);
		if (DragInt(ref value, GetElementTitle(title, "DragIntRow"), 1.0f, "row %i", startPos))
		{
			var newLen = Math.Max(0, value - startPos);
			SetFieldOrPropertyToValue(editorEvent, lengthField, newLen);
		}

		if (ImGui.IsItemActivated())
		{
			SetCachedValue(dragIntCacheKey, lengthToCache);
		}

		if (ImGui.IsItemDeactivatedAfterEdit())
		{
			var originalLen = GetCachedValue<int>(dragIntCacheKey);
			var newLen = Math.Max(0, value - startPos);
			if (newLen != originalLen)
			{
				ActionQueue.Instance.Do(
					new ActionSetObjectFieldOrPropertyValue<int>(editorEvent, lengthField, newLen, originalLen, true));
			}
		}

		// DragInt for beat
		ImGui.SameLine();
		endRow = startPos + length;
		var startRow = startPos / MaxValidDenominator;
		value = endRow / MaxValidDenominator;
		lengthToCache = length;
		ImGui.SetNextItemWidth(dragIntWidth);
		if (DragInt(ref value, GetElementTitle(title, "DragIntBeat"), 0.1f, "beat %i", startRow))
		{
			var newEndRow = value * MaxValidDenominator;
			var newLen = Math.Max(0, newEndRow - startPos);
			SetFieldOrPropertyToValue(editorEvent, lengthField, newLen);
		}

		if (ImGui.IsItemActivated())
		{
			SetCachedValue(dragIntCacheKey, lengthToCache);
		}

		if (ImGui.IsItemDeactivatedAfterEdit())
		{
			var originalLen = GetCachedValue<int>(dragIntCacheKey);
			var newEndRow = value * MaxValidDenominator;
			var newLen = Math.Max(0, newEndRow - startPos);
			if (newLen != originalLen)
			{
				ActionQueue.Instance.Do(
					new ActionSetObjectFieldOrPropertyValue<int>(editorEvent, lengthField, newLen, originalLen, true));
			}
		}

		// DragInt for measure
		ImGui.SameLine();
		endRow = startPos + length;
		var startMeasure = (int)editorEvent.GetEditorChart().GetMeasureForEvent(editorEvent);
		value = (int)editorEvent.GetEditorChart().GetMeasureForChartPosition(endRow);
		lengthToCache = length;
		ImGui.SetNextItemWidth(dragIntWidth);
		if (DragInt(ref value, GetElementTitle(title, "DragIntMeasure"), 0.1f, "measure %i", startMeasure))
		{
			var newEndRow = (int)editorEvent.GetEditorChart().GetChartPositionForMeasure(value);
			var newLen = Math.Max(0, newEndRow - startPos);
			SetFieldOrPropertyToValue(editorEvent, lengthField, newLen);
		}

		if (ImGui.IsItemActivated())
		{
			SetCachedValue(dragIntCacheKey, lengthToCache);
		}

		if (ImGui.IsItemDeactivatedAfterEdit())
		{
			var originalLen = GetCachedValue<int>(dragIntCacheKey);
			var newEndRow = (int)editorEvent.GetEditorChart().GetChartPositionForMeasure(value);
			var newLen = Math.Max(0, newEndRow - startPos);
			if (newLen != originalLen)
			{
				ActionQueue.Instance.Do(
					new ActionSetObjectFieldOrPropertyValue<int>(editorEvent, lengthField, newLen, originalLen, true));
			}
		}

		// Use current row button
		ImGui.SameLine();
		if (ImGui.Button($"Use Current{GetElementTitle(title, "CurrentButton")}", new Vector2(ButtonUseCurrentRowWidth, 0.0f)))
		{
			var editorPos = (int)Math.Max(0.0, Math.Round(editor.GetPosition().ChartPosition));
			var newLen = Math.Max(0, editorPos - startPos);
			if (newLen != length)
			{
				ActionQueue.Instance.Do(
					new ActionSetObjectFieldOrPropertyValue<int>(editorEvent, lengthField, newLen, length, true));
			}
		}

		// Go button
		ImGui.SameLine();
		if (ImGui.Button($"Go{GetElementTitle(title, "GoButton")}", new Vector2(ButtonGoWidth, 0.0f)))
		{
			editor.SetChartPosition(endRow);
		}
	}

	public static void DrawRowChartPositionLength(
		bool affectsFile,
		string title,
		object o,
		string lengthField,
		string help)
	{
		DrawRowTitleAndAdvanceColumn(title);

		var width = DrawHelp(help, ImGui.GetContentRegionAvail().X);
		var dragIntWidth = (width - ImGui.GetStyle().ItemSpacing.X) * 0.5f;

		// DragInts for length.
		DrawChartPositionLengthDragInts(affectsFile, title, o, lengthField, dragIntWidth);
	}

	public static void DrawRowChartPositionLength(
		bool affectsFile,
		string title,
		object o,
		string lengthField,
		string inclusiveField,
		string help)
	{
		DrawRowTitleAndAdvanceColumn(title);

		var width = DrawHelp(help, ImGui.GetContentRegionAvail().X);
		var dragIntWidth = (width - TextInclusiveWidth - CheckBoxWidth - ImGui.GetStyle().ItemSpacing.X * 3.0f) * 0.5f;

		// Inclusive checkbox.
		DrawCheckbox(true, title, o, inclusiveField, CheckBoxWidth, true);
		ImGui.SameLine();
		Text("Inclusive", TextInclusiveWidth);

		// DragInts for length.
		ImGui.SameLine();
		DrawChartPositionLengthDragInts(affectsFile, title, o, lengthField, dragIntWidth);
	}

	private static void DrawChartPositionLengthDragInts(
		bool affectsFile,
		string title,
		object o,
		string lengthField,
		float dragIntWidth)
	{
		var dragIntCacheKey = GetCacheKey(title, "ChartPositionLengthDragInt");
		var rows = GetValueFromFieldOrProperty<int>(o, lengthField);

		// DragInt for length as row
		var value = rows;
		var rowsToCache = rows;
		ImGui.SetNextItemWidth(dragIntWidth);
		if (DragInt(ref value, GetElementTitle(title, "DragIntRows"), 1.0f, "%i rows", 0))
		{
			SetFieldOrPropertyToValue(o, lengthField, value);
		}

		if (ImGui.IsItemActivated())
		{
			SetCachedValue(dragIntCacheKey, rowsToCache);
		}

		if (ImGui.IsItemDeactivatedAfterEdit())
		{
			var originalRows = GetCachedValue<int>(dragIntCacheKey);
			var newRows = value;
			if (newRows != originalRows)
			{
				ActionQueue.Instance.Do(
					new ActionSetObjectFieldOrPropertyValue<int>(o, lengthField, newRows, originalRows, affectsFile));
			}

			rows = newRows;
		}

		// DragInt for length as beats
		ImGui.SameLine();
		value = rows / MaxValidDenominator;
		rowsToCache = rows;
		ImGui.SetNextItemWidth(dragIntWidth);
		if (DragInt(ref value, GetElementTitle(title, "DragIntBeats"), 0.1f, "%i beats", 0))
		{
			var newRows = value * MaxValidDenominator;
			SetFieldOrPropertyToValue(o, lengthField, newRows);
		}

		if (ImGui.IsItemActivated())
		{
			SetCachedValue(dragIntCacheKey, rowsToCache);
		}

		if (ImGui.IsItemDeactivatedAfterEdit())
		{
			var originalRows = GetCachedValue<int>(dragIntCacheKey);
			var newRows = value * MaxValidDenominator;
			if (newRows != originalRows)
			{
				ActionQueue.Instance.Do(
					new ActionSetObjectFieldOrPropertyValue<int>(o, lengthField, newRows, originalRows, affectsFile));
			}
		}
	}

	#endregion Chart Position

	#region Chart Select

	public static void DrawRowTimingChart(
		bool undoable,
		string title,
		EditorSong song,
		string help = null)
	{
		DrawRowTitleAndAdvanceColumn(title);

		var remainingWidth = DrawHelp(help, ImGui.GetContentRegionAvail().X);
		var comboWidth = Math.Max(1.0f,
			remainingWidth - ButtonApplyWidth - ImGui.GetStyle().ItemSpacing.X);

		if (song != null)
		{
			ImGui.SetNextItemWidth(comboWidth);

			var chart = song.TimingChart;

			var selectedName = chart?.GetDescriptiveName() ?? "None";
			if (ImGui.BeginCombo("", selectedName))
			{
				UIChartList.DrawChartList(
					song,
					chart,
					selectedChart =>
					{
						if (chart != selectedChart)
						{
							if (undoable)
							{
								ActionQueue.Instance.Do(new ActionSetObjectFieldOrPropertyReferenceNoClone<EditorChart>(
									song, nameof(EditorSong.TimingChart), selectedChart, true));
							}
							else
							{
								song.TimingChart = selectedChart;
							}
						}
					});
				ImGui.EndCombo();
			}

			if (chart == null)
				PushDisabled();

			ImGui.SameLine();


			if (ImGui.Button("Apply...", new Vector2(ButtonApplyWidth, 0.0f)))
			{
				ImGui.OpenPopup("ApplyTimingPopup");
			}

			if (ImGui.BeginPopup("ApplyTimingPopup"))
			{
				if (ImGui.Selectable("Timing events to all other charts"))
				{
					var allOtherCharts = chart?.GetAllOtherEditorCharts();
					if (allOtherCharts?.Count > 0)
					{
						ActionQueue.Instance.Do(new ActionCopyEventsBetweenCharts(
							chart,
							UICopyEventsBetweenCharts.GetTimingTypes(),
							allOtherCharts));
					}
				}

				if (ImGui.Selectable("Timing and scroll events to all other charts"))
				{
					var allOtherCharts = chart?.GetAllOtherEditorCharts();
					if (allOtherCharts?.Count > 0)
					{
						ActionQueue.Instance.Do(new ActionCopyEventsBetweenCharts(
							chart,
							UICopyEventsBetweenCharts.GetTimingAndScrollTypes(),
							allOtherCharts));
					}
				}

				if (ImGui.Selectable("All events which cannot differ per chart in sm files to all other charts"))
				{
					var allOtherCharts = chart?.GetAllOtherEditorCharts();
					if (allOtherCharts?.Count > 0)
					{
						ActionQueue.Instance.Do(new ActionCopyEventsBetweenCharts(
							chart,
							UICopyEventsBetweenCharts.GetStepmaniaTypes(),
							allOtherCharts));
					}
				}

				ImGui.Separator();
				if (ImGui.Selectable("Advanced Event Copy..."))
				{
					UICopyEventsBetweenCharts.Instance.Open(true);
				}

				ImGui.EndPopup();
			}

			if (chart == null)
				PopDisabled();
		}
	}

	#endregion Chart Select

	#region Plot

	public static void DrawRowPlot(
		string title,
		ref float values,
		int numValues,
		string overlayText,
		float maxValue,
		float height,
		int stride,
		string help = null)
	{
		DrawRowTitleAndAdvanceColumn(title);
		ImGui.SetNextItemWidth(DrawHelp(help, ImGui.GetContentRegionAvail().X));

		ImGui.PlotLines(
			"",
			ref values,
			numValues,
			0,
			overlayText,
			0.0f,
			maxValue,
			new Vector2(0.0f, height),
			stride);
	}

	#endregion Plot

	#region Stream

	public static void DrawRowStream(string title, string stream, Editor editor, string help = null)
	{
		DrawRowTitleAndAdvanceColumn(title);

		var remainingWidth = DrawHelp(help, ImGui.GetContentRegionAvail().X);
		var textWidth = remainingWidth - ButtonSettingsSize.X - ButtonCopySize.X - ImGui.GetStyle().ItemSpacing.X * 2;

		PushDisabled();
		ImGui.SetNextItemWidth(textWidth);
		ImGui.InputText(GetElementTitle(title), ref stream, 1024);
		PopDisabled();

		ImGui.SameLine();
		if (ImGui.Button("Settings", ButtonSettingsSize))
		{
			UIStreamPreferences.Instance.Open(true);
		}

		ImGui.SameLine();
		if (ImGui.Button("Copy", ButtonCopySize))
		{
			editor.SetClipboardText(stream);
		}
	}

	#endregion Stream

	#region Player Selection

	public static void DrawRowPlayerSelection(string title, int maxPlayers, ArrowGraphicManager arrowGraphicManager,
		string help = null)
	{
		DrawTitle(title, help);

		var currentPlayer = Preferences.Instance.Player;
		var originalPlayer = currentPlayer;

		if (arrowGraphicManager != null)
			ImGui.PushStyleColor(ImGuiCol.Text, arrowGraphicManager.GetArrowColor(0, 0, false, originalPlayer));
		if (ImGui.BeginCombo(GetElementTitle(title), $"Player {originalPlayer + 1}"))
		{
			if (arrowGraphicManager != null)
				ImGui.PopStyleColor();

			for (var i = 0; i < maxPlayers; i++)
			{
				if (arrowGraphicManager != null)
					ImGui.PushStyleColor(ImGuiCol.Text, arrowGraphicManager.GetArrowColor(0, 0, false, i));

				var isSelected = i == originalPlayer;
				if (ImGui.Selectable($"Player {i + 1}", isSelected))
				{
					currentPlayer = i;
				}

				if (isSelected)
				{
					ImGui.SetItemDefaultFocus();
				}

				if (arrowGraphicManager != null)
					ImGui.PopStyleColor();
			}

			ImGui.EndCombo();
		}
		else
		{
			if (arrowGraphicManager != null)
				ImGui.PopStyleColor();
		}

		if (currentPlayer != originalPlayer)
		{
			Preferences.Instance.Player = currentPlayer;
		}
	}

	#endregion Player Selection

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

	private static bool CharCompare(char a, char b)
	{
		return a == b;
	}

	private static bool Vector3Compare(Vector3 a, Vector3 b)
	{
		return a == b;
	}

	private static bool Vector4Compare(Vector4 a, Vector4 b)
	{
		return a == b;
	}

	#endregion

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
		var remainderWidth = hasHelp ? Math.Max(1.0f, width - GetHelpWidth() - ImGui.GetStyle().ItemSpacing.X) : width;
		//var remainderWidth = Math.Max(1.0f, width - GetHelpWidth() - ImGui.GetStyle().ItemSpacing.X);

		if (hasHelp)
		{
			HelpMarker(help);
			ImGui.SameLine();
		}
		//else
		//{
		//	ImGui.Dummy(new Vector2(HelpWidth, 1));
		//	ImGui.SameLine();
		//}

		return remainderWidth;
	}

	// ReSharper disable once UnusedMethodReturnValue.Local
	private static bool DrawCachedEditReference<T>(
		bool undoable,
		string title,
		object o,
		string fieldName,
		float width,
		bool affectsFile,
		Func<T, (bool, T)> imGuiFunc,
		Func<T, T, bool> compareFunc,
		Func<T, bool> validationFunc = null,
		string help = null) where T : class, ICloneable
	{
		// Get the cached value.
		var cacheKey = GetCacheKey(title, fieldName);
		if (!TryGetCachedValue(cacheKey, out T cachedValue))
		{
			cachedValue = null;
			// ReSharper disable once ExpressionIsAlwaysNull
			SetCachedValue(cacheKey, cachedValue);
		}

		// Get the current value.
		var value = GetValueFromFieldOrProperty<T>(o, fieldName);

		// Draw the help marker and determine the remaining width.
		var textWidth = DrawHelp(help, width);
		ImGui.SetNextItemWidth(textWidth);

		// Draw the ImGui control using the cached value.
		// We do not want to see the effect of changing the value outside the control
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
				ActionQueue.Instance.Do(
					new ActionSetObjectFieldOrPropertyReference<T>(o, fieldName, (T)cachedValue.Clone(), affectsFile));
			else
				SetFieldOrPropertyToValue(o, fieldName, cachedValue);
			value = cachedValue;
		}

		// Always update the cached value if the control is not active.
		if (!ImGui.IsItemActive())
			SetCachedValue(cacheKey, value);

		return result;
	}

	private static bool DrawCachedEditValue<T>(
		bool undoable,
		string title,
		object o,
		string fieldName,
		float width,
		bool affectsFile,
		Func<T, (bool, T)> imGuiFunc,
		Func<T, T, bool> compareFunc,
		Func<T, bool> validationFunc = null,
		string help = null) where T : struct
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
		// We do not want to see the effect of changing the value outside the control
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
				ActionQueue.Instance.Do(new ActionSetObjectFieldOrPropertyValue<T>(o, fieldName, cachedValue, affectsFile));
			else
				SetFieldOrPropertyToValue(o, fieldName, cachedValue);
			value = cachedValue;

			// Consider the frame after letting go where the item is deactivated, and we add an undoable action
			// to update the value to be an edit that should return true.
			result = true;
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
	/// <param name="title">ImGui element title.</param>
	/// <param name="o">Object being edited.</param>
	/// <param name="fieldName">Name of field or property on object to edit.</param>
	/// <param name="width">ImGui element width.</param>
	/// <param name="affectsFile">Whether or not editing this value affects the saved file.</param>
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
		bool affectsFile,
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

		// Draw the ImGui control using the actual value, not the cached value.
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
					new ActionSetObjectFieldOrPropertyValue<T>(o, fieldName, value, GetCachedValue<T>(cacheKey), affectsFile));
				// Consider the frame after letting go where the item is deactivated, and we add an undoable action
				// to update the value to be an edit that should return true.
				result = true;
			}
		}

		return result;
	}
}

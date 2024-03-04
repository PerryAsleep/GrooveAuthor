using Fumen.Converters;
using ImGuiNET;
using StepManiaEditor.AutogenConfig;
using StepManiaLibrary.PerformedChart;
using static StepManiaEditor.ImGuiUtils;

namespace StepManiaEditor;

/// <summary>
/// Class for drawing UI to edit an EditorPatternConfig.
/// </summary>
internal sealed class UIPatternConfig
{
	private static readonly int TitleColumnWidth = UiScaled(140);
	private static readonly int StepTypeWeightWidth = UiScaled(26);
	private static readonly int DefaultWidth = UiScaled(460);

	public const string WindowTitle = "Pattern Config";

	public static readonly string HelpText =
		$"Pattern Configs are settings used by {Utils.GetAppName()} to generate new step patterns."
		+ " Full details can be found in the documentation.";

	private const string EndChoiceHelpText = "Which lane the {0} foot should end on."
	                                         + "\nAutomatic Ignore Following Steps:        The {0} foot ending lane should be chosen automatically with"
	                                         + "\n                                         no consideration given to any following steps."
	                                         + "\nAutomatic Same Lane To Following:        The {0} foot ending lane should be chosen automatically such"
	                                         + "\n                                         that it ends on the same lane as its following step."
	                                         + "\nAutomatic New Lane To Following:         The {0} foot ending lane should be chosen automatically such"
	                                         + "\n                                         that it ends on a lane that can step to its following step's"
	                                         + "\n                                         lane."
	                                         + "\nAutomatic Same Or New Lane As Following: The {0} foot ending lane should be chosen automatically such"
	                                         + "\n                                         that it ends on the same lane as its following step or it ends"
	                                         + "\n                                         on a lane that can step to its following step's lane."
	                                         + "\nSpecified Lane:                          The {0} foot should end on an explicitly specified lane.";

	private static readonly string EndChoiceHelpTextLeft = string.Format(EndChoiceHelpText, "left");
	private static readonly string EndChoiceHelpTextRight = string.Format(EndChoiceHelpText, "right");

	private const string StartChoiceHelpText = "Which lane the {0} foot should start on."
	                                           + "\nAutomatic Same Lane:        The {0} foot should start on the same lane it is already on."
	                                           + "\nAutomatic New Lane:         The {0} foot should start with a step to a new lane from the"
	                                           + "\n                            lane it is already on."
	                                           + "\nAutomatic Same Or New Lane: The {0} foot should start either on the same lane it was"
	                                           + "\n                            already on, or with a step to a new lane fome the lane it is"
	                                           + "\n                            already on."
	                                           + "\n                            lane it is already on."
	                                           + "\nSpecified Lane:             The {0} foot should start on an explicitly specified lane.";

	private static readonly string StartChoiceHelpTextLeft = string.Format(StartChoiceHelpText, "left");
	private static readonly string StartChoiceHelpTextRight = string.Format(StartChoiceHelpText, "right");

	private readonly Editor Editor;

	public UIPatternConfig(Editor editor)
	{
		Editor = editor;
	}

	public void Draw()
	{
		var p = Preferences.Instance;
		if (!p.ShowPatternListWindow)
			return;

		var editorConfig = PatternConfigManager.Instance.GetConfig(p.ActivePatternConfigForWindow);
		if (editorConfig == null)
			return;

		var currentChartType = Editor.GetActiveChart()?.ChartType;

		if (BeginWindow(WindowTitle, ref p.ShowPatternListWindow, DefaultWidth))
		{
			DrawConfig("PatternConfig", Editor, editorConfig, currentChartType, true);
		}

		ImGui.End();
	}

	public static void DrawConfig(string id, Editor editor, EditorPatternConfig editorConfig, SMCommon.ChartType? chartType,
		bool drawDelete)
	{
		var disabled = !editor.CanEdit() || editorConfig.IsDefault();
		if (disabled)
			PushDisabled();

		if (ImGuiLayoutUtils.BeginTable($"PatternConfigTableIdentification##{id}", TitleColumnWidth))
		{
			ImGuiLayoutUtils.DrawRowTextInput(true, "Custom Name", editorConfig, nameof(EditorPatternConfig.Name), false,
				"Optional custom name for identifying this Pattern Config.");

			ImGuiLayoutUtils.EndTable();
		}

		ImGui.Separator();
		if (ImGuiLayoutUtils.BeginTable($"PatternConfigTableBeat##{id}", TitleColumnWidth))
		{
			ImGuiLayoutUtils.DrawRowSubdivisions(true, "Note Type", editorConfig, nameof(EditorPatternConfig.PatternType),
				false, "The types of notes to use when generating the pattern.");

			ImGuiLayoutUtils.DrawRowDragIntWithEnabledCheckbox(true, "Step Repetition Limit", editorConfig.Config,
				nameof(PatternConfig.MaxSameArrowsInARowPerFoot), nameof(PatternConfig.LimitSameArrowsInARowPerFoot), false,
				"Maximum number of repeated steps on the same arrow per foot.", 0.1f, "%i", 0, 100);

			ImGuiLayoutUtils.DrawRowDragInt2(true, "Step Type Weights", editorConfig.Config,
				nameof(PatternConfig.SameArrowStepWeight),
				nameof(PatternConfig.NewArrowStepWeight), false, "Same", "New", StepTypeWeightWidth,
				"Weights of step types to use in the pattern."
				+ "\nSame: Relative weight of same arrow steps."
				+ "\nNew:  Relative weight of new arrow steps.",
				0.2f, "%i", 0, 100, 0, 100);

			ImGuiLayoutUtils.DrawRowDragInt(true, "Step Type Check Period", editorConfig.Config,
				nameof(PatternConfig.StepTypeCheckPeriod),
				false, "Period in steps at which costs for deviating from the desired Step Type Weights are evaluated.", 1F,
				"%i steps", 0, 512);

			ImGuiLayoutUtils.EndTable();
		}

		ImGui.Separator();
		if (ImGuiLayoutUtils.BeginTable($"PatternConfigTableStart##{id}", TitleColumnWidth))
		{
			ImGuiLayoutUtils.DrawRowPatternConfigStartFootChoice(true, "Starting Foot", editorConfig,
				"How to choose the starting foot."
				+ "\nRandom:    Choose the starting foot randomly."
				+ "\nAutomatic: Choose the starting foot automatically so that it alternates from the previous steps."
				+ "\nSpecified: Use a specified starting foot.");

			ImGuiLayoutUtils.DrawRowPatternConfigStartFootLaneChoice(editor, true, "Left Foot Start Lane", editorConfig,
				chartType, true, StartChoiceHelpTextLeft);

			ImGuiLayoutUtils.DrawRowPatternConfigStartFootLaneChoice(editor, true, "Right Foot Start Lane", editorConfig,
				chartType, false, StartChoiceHelpTextRight);

			ImGuiLayoutUtils.EndTable();
		}

		ImGui.Separator();
		if (ImGuiLayoutUtils.BeginTable($"PatternConfigTableEnd##{id}", TitleColumnWidth))
		{
			ImGuiLayoutUtils.DrawRowPatternConfigEndFootLaneChoice(editor, true, "Left Foot End Lane", editorConfig,
				chartType, true, EndChoiceHelpTextLeft);

			ImGuiLayoutUtils.DrawRowPatternConfigEndFootLaneChoice(editor, true, "Right Foot End Lane", editorConfig,
				chartType, false, EndChoiceHelpTextRight);

			ImGuiLayoutUtils.EndTable();
		}

		if (drawDelete)
		{
			ImGui.Separator();
			if (ImGuiLayoutUtils.BeginTable($"PatternConfigDelete##{id}", TitleColumnWidth))
			{
				if (ImGuiLayoutUtils.DrawRowButton("Delete Pattern Config", "Delete",
					    "Delete this Pattern Config."))
				{
					ActionQueue.Instance.Do(new ActionDeletePatternConfig(editor, editorConfig.Guid));
				}

				ImGuiLayoutUtils.EndTable();
			}
		}

		ImGui.Separator();
		if (ImGuiLayoutUtils.BeginTable($"PatternConfigRestore##{id}", TitleColumnWidth))
		{
			// Never disabled the documentation button.
			if (disabled)
				PopDisabled();
			if (ImGuiLayoutUtils.DrawRowButton("Help", "Open Documentation", HelpText))
			{
				Documentation.OpenDocumentation(Documentation.Page.PatternConfigs);
			}

			if (disabled)
				PushDisabled();

			if (ImGuiLayoutUtils.DrawRowButton("Restore Defaults", "Restore Defaults",
				    "Restore config values to their defaults."))
			{
				editorConfig.RestoreDefaults();
			}

			ImGuiLayoutUtils.EndTable();
		}

		if (disabled)
			PopDisabled();
	}
}

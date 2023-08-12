using System.Numerics;
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

	public const string WindowTitle = "Pattern Config";

	public const string HelpText = "Pattern Configs are settings used by GrooveAuthor to generate new step patterns."
	                               + " Full details on the config values and how they are used to assign costs can be found"
	                               + " in the online documentation.";

	private const string EndChoiceHelpText = "Which lane the {0} foot should end on."
	                                         + "\nAutomatic Ignore Following Steps:        The {0} foot ending lane should be chosen automatically with"
	                                         + "\n                                         no consideration given to any following steps."
	                                         + "\nAutomatic Same Lane To Following:        The {0} foot ending lane should be chosen automatically such"
	                                         + "\n                                         that it ends on the same lane as its following step."
	                                         + "\nAutomatic New Lane To Following:         The {0} foot ending lane should be chosen automatically such"
	                                         + "\n                                         that it ends on a lane that can step to its following step's"
	                                         + "\n                                         lane."
	                                         + "\nAutomatic Same Or New Lane As Following: The {0} foot ending lane should be chosen automatically such"
	                                         + "\n                                         that it ends on the same lane as its following step or it ends."
	                                         + "\n                                         on a lane that can step to its following step's lane."
	                                         + "\nSpecified Lane:                          The {0} foot should end on an explicitly specified lane.";

	private static readonly string EndChoiceHelpTextLeft = string.Format(EndChoiceHelpText, "left");
	private static readonly string EndChoiceHelpTextRight = string.Format(EndChoiceHelpText, "right");

	private const string StartChoiceHelpText = "Which lane the {0} foot should start on."
	                                           + "\nAutomatic Same Lane: The {0} foot should start on the same lane it is already on."
	                                           + "\nAutomatic New Lane:  The {0} foot should start with a step to a new lane from the"
	                                           + "\n                     lane it is already on."
	                                           + "\nSpecified Lane:      The {0} foot should start on an explicitly specified lane.";

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

		ImGui.SetNextWindowSize(new Vector2(0, 0), ImGuiCond.FirstUseEver);
		if (ImGui.Begin(WindowTitle, ref p.ShowPatternListWindow, ImGuiWindowFlags.NoScrollbar))
		{
			var disabled = !Editor.CanEdit() || editorConfig.IsDefault();
			if (disabled)
				PushDisabled();

			if (ImGuiLayoutUtils.BeginTable("PatternConfigTableIdentification", TitleColumnWidth))
			{
				ImGuiLayoutUtils.DrawRowTextInput(true, "Name", editorConfig, nameof(EditorPatternConfig.Name), false,
					"Configuration name.");

				ImGuiLayoutUtils.DrawRowTextInput(true, "Description", editorConfig,
					nameof(EditorPatternConfig.Description), false,
					"Configuration description.");

				ImGuiLayoutUtils.EndTable();
			}

			ImGui.Separator();
			if (ImGuiLayoutUtils.BeginTable("PatternConfigTableBeat", TitleColumnWidth))
			{
				ImGuiLayoutUtils.DrawRowSubdivisions(true, "Note Type", editorConfig, nameof(EditorPatternConfig.PatternType),
					false, "The types of notes to use when generating the pattern.");

				ImGuiLayoutUtils.DrawRowDragIntWithEnabledCheckbox(true, "Step Repetition Limit", editorConfig.Config,
					nameof(PatternConfig.MaxSameArrowsInARowPerFoot), nameof(PatternConfig.LimitSameArrowsInARowPerFoot), false,
					"Maximum number of repeated steps on the same arrow per foot.", 0.1f, "%i", 0, 100);

				ImGuiLayoutUtils.EndTable();
			}

			ImGui.Separator();
			if (ImGuiLayoutUtils.BeginTable("PatternConfigTableStart", TitleColumnWidth))
			{
				ImGuiLayoutUtils.DrawRowPatternConfigStartFootChoice(true, "Starting Foot", editorConfig,
					"How to choose the starting foot."
					+ "\nRandom:    Choose the starting foot randomly."
					+ "\nAutomatic: Choose the starting foot automatically so that it alternates from the previous steps."
					+ "\nSpecified: Use a specified starting foot.");

				ImGuiLayoutUtils.DrawRowPatternConfigStartFootLaneChoice(Editor, true, "Left Foot Start Lane", editorConfig,
					currentChartType, true, StartChoiceHelpTextLeft);

				ImGuiLayoutUtils.DrawRowPatternConfigStartFootLaneChoice(Editor, true, "Right Foot Start Lane", editorConfig,
					currentChartType, false, StartChoiceHelpTextRight);

				ImGuiLayoutUtils.EndTable();
			}

			ImGui.Separator();
			if (ImGuiLayoutUtils.BeginTable("PatternConfigTableEnd", TitleColumnWidth))
			{
				ImGuiLayoutUtils.DrawRowPatternConfigEndFootLaneChoice(Editor, true, "Left Foot End Lane", editorConfig,
					currentChartType, true, EndChoiceHelpTextLeft);

				ImGuiLayoutUtils.DrawRowPatternConfigEndFootLaneChoice(Editor, true, "Right Foot End Lane", editorConfig,
					currentChartType, false, EndChoiceHelpTextRight);

				ImGuiLayoutUtils.EndTable();
			}

			ImGui.Separator();
			if (ImGuiLayoutUtils.BeginTable("PatternConfigDelete", TitleColumnWidth))
			{
				if (ImGuiLayoutUtils.DrawRowButton("Delete Pattern Config", "Delete",
					    "Delete this Pattern Config."))
				{
					ActionQueue.Instance.Do(new ActionDeletePatternConfig(editorConfig.Guid));
				}

				ImGuiLayoutUtils.EndTable();
			}

			ImGui.Separator();
			if (ImGuiLayoutUtils.BeginTable("Pattern Config Restore", TitleColumnWidth))
			{
				ImGuiLayoutUtils.DrawTitle("Help", HelpText);

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

		ImGui.End();
	}
}

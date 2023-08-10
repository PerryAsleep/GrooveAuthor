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
	private static readonly int TitleColumnWidth = UiScaled(240);

	public const string WindowTitle = "Pattern Config";

	public const string HelpText = "Pattern Configs are settings used by GrooveAuthor to generate new step patterns."
	                               + " Full details on the config values and how they are used to assign costs can be found"
	                               + " in the online documentation.";

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
			if (ImGuiLayoutUtils.BeginTable("PatternConfigTableStart", TitleColumnWidth))
			{
				ImGuiLayoutUtils.DrawRowPatternConfigStartFootChoice(true, "Starting Foot", editorConfig,
					"How to choose the starting foot."
					+ "\nRandom:    Choose the starting foot randomly."
					+ "\nAutomatic: Choose the starting foot automatically so that it alternates from the previous steps."
					+ "\nSpecified: Use a specified starting foot.");

				ImGuiLayoutUtils.DrawRowPatternConfigStartFootLaneChoice(Editor, true, "Left Foot Start Lane", editorConfig,
					currentChartType, true,
					"Which lane the left foot should start on."
					+ "\nAutomatic Same Lane: The left foot should start on the same lane it is already on."
					+ "\nAutomatic New Lane:  The left foot should start with a step to a new lane from the"
					+ "\n                     lane it is already on."
					+ "\nSpecified Lane:      The left foot should start on an explicitly specified lane.");

				ImGuiLayoutUtils.DrawRowPatternConfigStartFootLaneChoice(Editor, true, "Right Foot Start Lane", editorConfig,
					currentChartType, false,
					"Which lane the right foot should start on."
					+ "\nAutomatic Same Lane: The right foot should start on the same lane it is already on."
					+ "\nAutomatic New Lane:  The right foot should start with a step to a new lane from the"
					+ "\n                     lane it is already on."
					+ "\nSpecified Lane:      The right foot should start on an explicitly specified lane.");

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

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

			var config = editorConfig.Config;

			if (ImGuiLayoutUtils.BeginTable("PatternConfigTable", TitleColumnWidth))
			{
				ImGuiLayoutUtils.DrawRowTextInput(true, "Name", editorConfig, nameof(EditorPatternConfig.Name), false,
					"Configuration name.");

				ImGuiLayoutUtils.DrawRowTextInput(true, "Description", editorConfig,
					nameof(EditorPatternConfig.Description), false,
					"Configuration description.");

				ImGuiLayoutUtils.DrawRowEnum<PatternConfigStartingFootChoice>(true, "Starting Foot", config,
					nameof(PatternConfig.StartingFootChoice), false,
					"How to choose the starting foot."
					+ "\nRandom:    Choose the starting foot randomly."
					+ "\nAutomatic: Choose the starting foot automatically so that it alternates from the previous steps."
					+ "\nSpecified: Use a specified starting foot.");

				ImGuiLayoutUtils.DrawRowPatternConfigStartFootChoice(Editor, true, "Left Foot Start Lane", config,
					currentChartType, true, "TODO");
				ImGuiLayoutUtils.DrawRowPatternConfigStartFootChoice(Editor, true, "Right Foot Start Lane", config,
					currentChartType, false, "TODO");

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

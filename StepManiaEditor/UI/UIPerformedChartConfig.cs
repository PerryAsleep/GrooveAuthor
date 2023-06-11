using System.Numerics;
using ImGuiNET;
using StepManiaLibrary.PerformedChart;
using static StepManiaEditor.ImGuiUtils;
using static StepManiaEditor.PreferencesPerformedChartConfig;

namespace StepManiaEditor;

/// <summary>
/// Class for drawing UI to edit a PerformedChartConfig.
/// </summary>
internal sealed class UIPerformedChartConfig
{
	private static readonly int TitleColumnWidth = UiScaled(240);

	public const string HelpText = "Performed Chart Configs are settings used by the Editor to generate Charts and patterns."
	                               + " TODO";

	private readonly Editor Editor;

	public UIPerformedChartConfig(Editor editor)
	{
		Editor = editor;
	}

	public void Draw()
	{
		var p = Preferences.Instance.PreferencesPerformedChartConfig;
		if (!p.ShowPerformedChartListWindow)
			return;

		if (!p.Configs.ContainsKey(p.ActivePerformedChartConfigForWindow))
			return;

		var namedConfig = p.Configs[p.ActivePerformedChartConfigForWindow];

		ImGui.SetNextWindowSize(new Vector2(0, 0), ImGuiCond.FirstUseEver);
		if (ImGui.Begin("Performed Chart Config", ref p.ShowPerformedChartListWindow, ImGuiWindowFlags.NoScrollbar))
		{
			var disabled = !Editor.CanEdit() || namedConfig.IsDefaultConfig();
			if (disabled)
				PushDisabled();

			var config = namedConfig.Config;

			if (ImGuiLayoutUtils.BeginTable("PerformedChartConfigTable", TitleColumnWidth))
			{
				ImGuiLayoutUtils.DrawRowTextInput(true, "Name", namedConfig, nameof(NamedConfig.Name), true,
					Preferences.Instance.PreferencesPerformedChartConfig.IsNewConfigNameValid,
					"Configuration name.");

				ImGuiLayoutUtils.DrawRowTextInput(true, "Description", namedConfig, nameof(NamedConfig.Description), true,
					"Configuration description.");

				ImGuiLayoutUtils.EndTable();
			}

			ImGui.Separator();
			if (ImGuiLayoutUtils.BeginTable("PerformedChartConfigArrowWeights", TitleColumnWidth))
			{
				ImGuiLayoutUtils.DrawTitle("Arrow Weights");

				ImGuiLayoutUtils.EndTable();
			}

			ImGui.Separator();
			if (ImGuiLayoutUtils.BeginTable("PerformedChartConfigStepTightening", TitleColumnWidth))
			{
				ImGuiLayoutUtils.DrawTitle("Step Tightening");
				
				ImGuiLayoutUtils.EndTable();
			}

			ImGui.Separator();
			if (ImGuiLayoutUtils.BeginTable("PerformedChartConfigLateralTightening", TitleColumnWidth))
			{
				ImGuiLayoutUtils.DrawTitle("Lateral Tightening");
				ImGuiLayoutUtils.DrawRowDragDouble(true, "Speed", config.LateralTightening,
					nameof(Config.LateralTightening.Speed), false,
					"Speed in panel lengths per second over which continuous lateral movement will be subject to lateral tightening "
					+ "costs based on the NPS checks below.",
					0.0001f, "%.6f", 0.0, 100.0);
				ImGuiLayoutUtils.DrawRowDragDouble(true, "Relative NPS", config.LateralTightening,
					nameof(Config.LateralTightening.RelativeNPS), false,
					"Multiplier. If the notes per second of a section of steps is over the chart's average notes per second multiplied "
					+ "by this value then the section is considered to be fast enough to apply a lateral body movement cost to.",
					0.0001f, "%.6f", 0.0, 100.0);
				ImGuiLayoutUtils.DrawRowDragDouble(true, "Absolute NPS", config.LateralTightening,
					nameof(Config.LateralTightening.AbsoluteNPS), false,
					"Absolute notes per second value. If the notes per second of a section of steps is over this value then the section "
					+ "is considered to be fast enough to apply a lateral body movement cost to.",
					0.0001f, "%.6f", 0.0, 100.0);
				ImGuiLayoutUtils.EndTable();
			}

			ImGui.Separator();
			if (ImGuiLayoutUtils.BeginTable("PerformedChartConfigFacing", TitleColumnWidth))
			{
				ImGuiLayoutUtils.DrawTitle("Facing");
				ImGuiLayoutUtils.DrawRowDragDouble(true, "Max Inward Percent", config.Facing,
					nameof(Config.Facing.MaxInwardPercentage), false,
					"Maximum percentage of steps which are allowed to face the body inward.",
					0.0001f, "%.6f", 0.0, 1.0);
				ImGuiLayoutUtils.DrawRowDragDouble(true, "Max Outward Percent", config.Facing,
					nameof(Config.Facing.MaxOutwardPercentage), false,
					"Maximum percentage of steps which are allowed to face the body outward.",
					0.0001f, "%.6f", 0.0, 1.0);
				ImGuiLayoutUtils.EndTable();
			}

			ImGui.Separator();
			if (ImGuiLayoutUtils.BeginTable("PerformedChartConfigDelete", TitleColumnWidth))
			{
				if (ImGuiLayoutUtils.DrawRowButton("Delete Performed Chart Config", "Delete",
						"Delete this Performed Chart Config."))
				{
					ActionQueue.Instance.Do(new ActionDeletePerformedChartConfig(namedConfig.Name));
				}
				ImGuiLayoutUtils.EndTable();
			}

			ImGui.Separator();
			if (ImGuiLayoutUtils.BeginTable("Performed Chart Config Restore", TitleColumnWidth))
			{
				ImGuiLayoutUtils.DrawTitle("Help", HelpText);

				if (ImGuiLayoutUtils.DrawRowButton("Restore Defaults", "Restore Defaults",
						"Restore config values to their defaults."))
				{
					namedConfig.RestoreDefaults();
				}

				ImGuiLayoutUtils.EndTable();
			}

			if (disabled)
				PopDisabled();
		}

		ImGui.End();
	}
}

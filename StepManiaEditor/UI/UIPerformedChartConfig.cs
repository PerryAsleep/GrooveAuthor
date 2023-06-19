using System.Collections.Generic;
using System.Numerics;
using ImGuiNET;
using StepManiaLibrary.PerformedChart;
using static StepManiaEditor.ImGuiUtils;
using static StepManiaEditor.PreferencesPerformedChartConfig;
using static StepManiaLibrary.PerformedChart.Config;

namespace StepManiaEditor;

/// <summary>
/// Class for drawing UI to edit a PerformedChartConfig.
/// </summary>
internal sealed class UIPerformedChartConfig
{
	private static readonly int TitleColumnWidth = UiScaled(240);

	public const string WindowTitle = "Performed Chart Config";

	public const string HelpText = "Performed Chart Configs are settings used by the Editor to generate Charts and patterns."
	                               + " When generating steps, all possible paths are considered. Costs are assigned to paths"
	                               + " based on Performed Chart Config values, and the path with the lowest cost is chosen."
	                               + " Full details on the config values and how they are used to assign costs can be found"
	                               + " in the online documentation.";

	private readonly Editor Editor;
	private readonly List<ImGuiArrowWeightsWidget> ArrowWeightsWidgets;

	public UIPerformedChartConfig(Editor editor)
	{
		Editor = editor;
		ArrowWeightsWidgets = new List<ImGuiArrowWeightsWidget>(Editor.SupportedChartTypes.Length);
		foreach (var _ in Editor.SupportedChartTypes)
		{
			ArrowWeightsWidgets.Add(new ImGuiArrowWeightsWidget(Editor));
		}
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
		if (ImGui.Begin(WindowTitle, ref p.ShowPerformedChartListWindow, ImGuiWindowFlags.NoScrollbar))
		{
			var disabled = !Editor.CanEdit() || namedConfig.IsDefaultConfig();
			if (disabled)
				PushDisabled();

			var config = namedConfig.Config;

			if (ImGuiLayoutUtils.BeginTable("PerformedChartConfigTable", TitleColumnWidth))
			{
				ImGuiLayoutUtils.DrawRowTextInput(true, "Name", namedConfig, nameof(NamedConfig.Name), false,
					Preferences.Instance.PreferencesPerformedChartConfig.IsNewConfigNameValid,
					"Configuration name.");

				ImGuiLayoutUtils.DrawRowTextInput(true, "Description", namedConfig, nameof(NamedConfig.Description), false,
					"Configuration description.");

				ImGuiLayoutUtils.EndTable();
			}

			ImGui.Separator();
			if (ImGuiLayoutUtils.BeginTable("PerformedChartConfigStepTightening", TitleColumnWidth))
			{
				ImGuiLayoutUtils.DrawTitle("Step Tightening");

				ImGuiLayoutUtils.DrawRowDragDoubleRange(true, "Stretch", config.StepTightening,
					nameof(StepTighteningConfig.StretchDistanceMin), nameof(StepTighteningConfig.StretchDistanceMax), false,
					"When limiting stretch, the range for tightening."
					+ "\nDistance in panel lengths."
					+ "\nSet both values to 0.0 to disable stretch tightening.", 0.01f, "%.6f", 0.0, 10.0);

				ImGuiLayoutUtils.DrawRowDragDoubleRange(true, "Distance", config.StepTightening,
					nameof(StepTighteningConfig.TravelDistanceMin), nameof(StepTighteningConfig.TravelDistanceMax), false,
					"When limiting travel distance, the range for tightening."
					+ "\nDistance in panel lengths."
					+ "\nSet both values to 0.0 to disable distance tightening.", 0.01f, "%.6f", 0.0, 10.0);

				ImGuiLayoutUtils.DrawRowDragDoubleRange(true, "Speed", config.StepTightening,
					nameof(StepTighteningConfig.TravelSpeedMinTimeSeconds),
					nameof(StepTighteningConfig.TravelSpeedMaxTimeSeconds), false,
					"When limiting travel speed, the range for tightening."
					+ "\nTime in seconds between steps for one foot."
					+ "\nSet both values to 0.0 to disable speed tightening.", 0.01f, "%.6f", 0.0, 10.0);

				ImGuiLayoutUtils.EndTable();
			}

			ImGui.Separator();
			if (ImGuiLayoutUtils.BeginTable("PerformedChartConfigLateralTightening", TitleColumnWidth))
			{
				ImGuiLayoutUtils.DrawTitle("Lateral Tightening");
				ImGuiLayoutUtils.DrawRowDragDouble(true, "Speed", config.LateralTightening,
					nameof(Config.LateralTightening.Speed), false,
					"Speed in panel lengths per second over which continuous lateral movement will be subject to lateral tightening "
					+ "costs based on the NPS checks below."
					+ "\nSet this to a high value to disable lateral tightening.",
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
				ImGuiLayoutUtils.DrawRowDragDouble(true, "Inward Cutoff Percent", config.Facing,
					nameof(Config.Facing.InwardPercentageCutoff), false,
					"Value to use for comparing arrow positions, representing a percentage of the total width of the pads."
					+ "\nFor example if this value is 0.5, then the arrows must be on the outer half of the pads."
					+ "\nIf it is 0.33, they must be on the outer third of the pads, etc.",
					0.0001f, "%.6f", 0.0, 1.0);
				ImGuiLayoutUtils.DrawRowDragDouble(true, "Max Outward Percent", config.Facing,
					nameof(Config.Facing.MaxOutwardPercentage), false,
					"Maximum percentage of steps which are allowed to face the body outward.",
					0.0001f, "%.6f", 0.0, 1.0);
				ImGuiLayoutUtils.DrawRowDragDouble(true, "Outward Cutoff Percent", config.Facing,
					nameof(Config.Facing.OutwardPercentageCutoff), false,
					"Value to use for comparing arrow positions, representing a percentage of the total width of the pads."
					+ "\nFor example if this value is 0.5, then the arrows must be on the outer half of the pads."
					+ "\nIf it is 0.33, they must be on the outer third of the pads, etc.",
					0.0001f, "%.6f", 0.0, 1.0);
				ImGuiLayoutUtils.EndTable();
			}

			ImGui.Separator();

			// Always enable the collapsable arrow weights section so it can be viewed.
			if (disabled)
				PopDisabled();
			if (ImGui.CollapsingHeader("Arrow Weights"))
			{
				if (disabled)
					PushDisabled();

				if (ImGuiLayoutUtils.BeginTable("ArrowWeightsTable", TitleColumnWidth))
				{
					ImGuiLayoutUtils.DrawTitle("Arrow Weights",
						"Desired distribution of arrows per Chart type.");

					if (ImGui.BeginTable("Arrow Weights Table", 2, ImGuiTableFlags.RowBg | ImGuiTableFlags.Borders))
					{
						ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthStretch, 100);
						ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed,
							ImGuiArrowWeightsWidget.GetFullWidth(namedConfig));

						var index = 0;
						foreach (var chartType in Editor.SupportedChartTypes)
						{
							ImGui.TableNextRow();

							ImGui.TableSetColumnIndex(0);
							ImGui.Text(GetPrettyEnumString(chartType));

							ImGui.TableSetColumnIndex(1);
							ArrowWeightsWidgets[index].DrawConfig(namedConfig, chartType);
							index++;
						}

						ImGui.EndTable();
					}

					ImGuiLayoutUtils.EndTable();
				}
			}
			else
			{
				if (disabled)
					PushDisabled();
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

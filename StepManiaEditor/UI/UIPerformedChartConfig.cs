using System.Collections.Generic;
using System.Numerics;
using ImGuiNET;
using StepManiaEditor.AutogenConfig;
using StepManiaLibrary.PerformedChart;
using static StepManiaEditor.ImGuiUtils;
using static StepManiaLibrary.PerformedChart.Config;

namespace StepManiaEditor;

/// <summary>
/// Class for drawing UI to edit an EditorPerformedChartConfig.
/// </summary>
internal sealed class UIPerformedChartConfig
{
	private static readonly int TitleColumnWidth = UiScaled(200);

	public const string WindowTitle = "Performed Chart Config";

	public static readonly string HelpText =
		$"Performed Chart Configs are settings used by {Editor.GetAppName()} to generate Charts and patterns."
		+ " When generating steps, all possible paths are considered. Costs are assigned to paths"
		+ " based on Performed Chart Config values, and the path with the lowest cost is chosen."
		+ " Full details on the config values and how they are used to assign costs can be found"
		+ " in the documentation.";

	private readonly Editor Editor;
	private static readonly List<ImGuiArrowWeightsWidget> ArrowWeightsWidgets;

	static UIPerformedChartConfig()
	{
		ArrowWeightsWidgets = new List<ImGuiArrowWeightsWidget>(Editor.SupportedChartTypes.Length);
		foreach (var _ in Editor.SupportedChartTypes)
		{
			ArrowWeightsWidgets.Add(new ImGuiArrowWeightsWidget());
		}
	}

	public UIPerformedChartConfig(Editor editor)
	{
		Editor = editor;
	}

	public void Draw()
	{
		var p = Preferences.Instance;
		if (!p.ShowPerformedChartListWindow)
			return;

		var editorConfig = PerformedChartConfigManager.Instance.GetConfig(p.ActivePerformedChartConfigForWindow);
		if (editorConfig == null)
			return;

		ImGui.SetNextWindowSize(Vector2.Zero, ImGuiCond.FirstUseEver);
		if (ImGui.Begin(WindowTitle, ref p.ShowPerformedChartListWindow, ImGuiWindowFlags.NoScrollbar))
		{
			DrawConfig("UIPerformedChartConfig", Editor, editorConfig, true);
		}

		ImGui.End();
	}

	public static void DrawConfig(string id, Editor editor, EditorPerformedChartConfig editorConfig, bool drawDelete)
	{
		var disabled = !editor.CanEdit() || editorConfig.IsDefault();
		if (disabled)
			PushDisabled();

		var config = editorConfig.Config;

		if (ImGuiLayoutUtils.BeginTable($"PerformedChartConfigTable##{id}", TitleColumnWidth))
		{
			ImGuiLayoutUtils.DrawRowTextInput(true, "Name", editorConfig, nameof(EditorPerformedChartConfig.Name), false,
				"Configuration name.");

			ImGuiLayoutUtils.DrawRowTextInput(true, "Description", editorConfig,
				nameof(EditorPerformedChartConfig.Description), false,
				"Configuration description.");

			ImGuiLayoutUtils.EndTable();
		}

		ImGui.Separator();
		if (ImGuiLayoutUtils.BeginTable($"PerformedChartConfigStepTightening##{id}", TitleColumnWidth))
		{
			ImGuiLayoutUtils.DrawTitle("Step Tightening");

			ImGuiLayoutUtils.DrawRowDragDoubleLatitudeLongitude(true, "Movement Compensation", config.StepTightening,
				nameof(StepTighteningConfig.LateralMinPanelDistance),
				nameof(StepTighteningConfig.LongitudinalMinPanelDistance), false,
				"When performing distance calculations for Step Tightening rules, how much lateral and longitudinal\n"
				+ "compensation should exist for a foot needing to minimally step on panel. Step tightening\n"
				+ "distance checks measure the minimum possible moves between panels. These values are used\n"
				+ "to know how far into a panel a foot actually needs to go to trigger it. These values should\n"
				+ "be chosen carefully to represent real-world movements.\n"
				+ "\n"
				+ "Lateral:      How far in from the edge of a panel the center of the foot needs to minimally be in the\n"
				+ "              lateral (side-to-side) direction in order to activate the panel.\n"
				+ "Longitudinal: How far in from the edge of a panel the center of the foot needs to minimally be in the\n"
				+ "              longitudinal (front-to-back) direction in order to activate the panel.\n"
				+ "\n"
				+ "These measurement unit is in panel dimensions and assume square panels.\n"
				+ "0.5 would mean the foot needs to be centered on the panel in the given dimension.\n"
				+ "0.0 would mean the foot could be directly over the edge in the given dimension.\n"
				+ "A negative value would mean the foot could be centered off the panel and still trigger it.",
				0.001f, "%.6f", 0.0, 2.0);

			ImGuiLayoutUtils.DrawRowPerformedChartConfigStretchTightening(config.StepTightening, "Stretch",
				"When limiting the stretch distance between both feet, the range for tightening."
				+ "\nThe distance is in panel lengths and takes into account the Movement Compensation values specified above.");

			ImGuiLayoutUtils.DrawRowPerformedChartConfigDistanceTightening(config.StepTightening, "Distance",
				"When limiting individual step travel distance, the range for tightening."
				+ "\nThe distance is in panel lengths and takes into account the Movement Compensation values specified above.");

			ImGuiLayoutUtils.DrawRowPerformedChartConfigSpeedTightening(editorConfig, "Speed",
				"When limiting individual step travel speed, the speed range over which to ramp up tightening costs."
				+ "\nAny speed at or above the minimum speed specified by the this range will be subject to tightening."
				+ "\nIn other words, the tightening does not stop above the final specified tempo."
				+ "\nThe speed range specified by these values is exclusive."
				+ "\nThe distance used for speed measurements is in panel lengths and takes into account the Movement"
				+ "\nCompensation values specified above.");

			var speedDisabled = !config.StepTightening.IsSpeedTighteningEnabled();
			if (speedDisabled)
				PushDisabled();
			ImGuiLayoutUtils.DrawRowDragDouble(true, "Speed Min Distance", config.StepTightening,
				nameof(StepTighteningConfig.SpeedTighteningMinDistance), false,
				"Minimum distance for applying speed tightening."
				+ "\nSetting a minimum distance can be helpful if you want to treat a range of short movements as equally acceptable."
				+ "\nThe distance is in panel lengths and takes into account the Movement Compensation values specified above.",
				0.01f, "%.6f", 0.0, 10.0);
			if (speedDisabled)
				PopDisabled();

			ImGuiLayoutUtils.EndTable();
		}

		ImGui.Separator();
		if (ImGuiLayoutUtils.BeginTable($"PerformedChartConfigLateralTightening##{id}", TitleColumnWidth))
		{
			ImGuiLayoutUtils.DrawTitle("Lateral Tightening");

			ImGuiLayoutUtils.DrawRowCheckbox(true, "Enabled", config.LateralTightening,
				nameof(Config.LateralTightening.Enabled), false,
				"Whether or not to enable lateral tightening controls.");

			var lateralTighteningDisabled = !config.LateralTightening.IsEnabled();
			if (lateralTighteningDisabled)
				PushDisabled();

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

			if (lateralTighteningDisabled)
				PopDisabled();
		}

		ImGui.Separator();
		if (ImGuiLayoutUtils.BeginTable($"PerformedChartConfigFacing##{id}", TitleColumnWidth))
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
		if (ImGuiLayoutUtils.BeginTable($"PerformedChartConfigTransitions##{id}", TitleColumnWidth))
		{
			ImGuiLayoutUtils.DrawTitle("Transition Control");

			ImGuiLayoutUtils.DrawRowCheckbox(true, "Enabled", config.Transitions,
				nameof(Config.Transitions.Enabled), false,
				"Whether or not to enable transition controls.");

			var transitionsDisabled = !config.Transitions.IsEnabled();
			if (transitionsDisabled)
				PushDisabled();

			ImGuiLayoutUtils.DrawRowDragInt(true, "Min Steps Per Transition", config.Transitions,
				nameof(Config.Transitions.StepsPerTransitionMin), false,
				"Desired minimum number of steps before transitions should occur."
				+ "\nIncrease this value to prevent frequent transitions.",
				1, "%i", 0, 2048);
			ImGuiLayoutUtils.DrawRowDragInt(true, "Max Steps Per Transition", config.Transitions,
				nameof(Config.Transitions.StepsPerTransitionMax), false,
				"Desired maximum number of steps before transitions should occur."
				+ "\nDecrease this value to prevent long segments with no transitions.",
				1, "%i", 0, 2048);
			ImGuiLayoutUtils.DrawRowDragInt(true, "Min Pads Width", config.Transitions,
				nameof(Config.Transitions.MinimumPadWidth), false,
				"Minimum width of the pads needed in order to apply transition control rules."
				+ "\nUse this value to prevent unwanted application for ChartTypes which you do not consider"
				+ "\nto have transitions.",
				1, "%i", 0, 32);
			ImGuiLayoutUtils.DrawRowDragDouble(true, "Transition Cutoff Percent", config.Transitions,
				nameof(Config.Transitions.TransitionCutoffPercentage), false,
				"Value for determining what constitutes a transition, representing a percentage of the total"
				+ "\nwidth of the pads."
				+ "\nFor example if this value is 0.5, then moving from one half of the pads to the other will"
				+ "\nbe treated as a transition.",
				0.0001f, "%.6f", 0.0, 1.0);
			ImGuiLayoutUtils.EndTable();

			if (transitionsDisabled)
				PopDisabled();
		}

		ImGui.Separator();

		// Always enable the collapsable arrow weights section so it can be viewed.
		if (disabled)
			PopDisabled();
		if (ImGui.CollapsingHeader($"Arrow Weights##{id}"))
		{
			if (disabled)
				PushDisabled();

			if (ImGuiLayoutUtils.BeginTable($"ArrowWeightsTable##{id}", TitleColumnWidth))
			{
				ImGuiLayoutUtils.DrawTitle("Arrow Weights",
					"Desired distribution of arrows per Chart type.");

				if (ImGui.BeginTable("Arrow Weights Table", 2, ImGuiTableFlags.RowBg | ImGuiTableFlags.Borders))
				{
					ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthStretch, 100);
					ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed,
						ImGuiArrowWeightsWidget.GetFullWidth(editorConfig));

					var index = 0;
					foreach (var chartType in Editor.SupportedChartTypes)
					{
						ImGui.TableNextRow();

						ImGui.TableSetColumnIndex(0);
						ImGui.Text(GetPrettyEnumString(chartType));

						ImGui.TableSetColumnIndex(1);
						ArrowWeightsWidgets[index].DrawConfig(editor, editorConfig, chartType);
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

		if (drawDelete)
		{
			ImGui.Separator();
			if (ImGuiLayoutUtils.BeginTable($"PerformedChartConfigDelete##{id}", TitleColumnWidth))
			{
				if (ImGuiLayoutUtils.DrawRowButton("Delete Performed Chart Config", "Delete",
					    "Delete this Performed Chart Config."))
				{
					ActionQueue.Instance.Do(new ActionDeletePerformedChartConfig(editor, editorConfig.Guid));
				}

				ImGuiLayoutUtils.EndTable();
			}
		}

		ImGui.Separator();
		if (ImGuiLayoutUtils.BeginTable($"PerformedChartConfigRestore##{id}", TitleColumnWidth))
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
}

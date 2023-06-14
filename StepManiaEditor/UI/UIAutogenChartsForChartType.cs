using System.Collections.Generic;
using System.Numerics;
using ImGuiNET;
using static Fumen.Converters.SMCommon;
using static StepManiaEditor.ImGuiUtils;

namespace StepManiaEditor;

/// <summary>
/// Class for drawing options for autogenerating a set of Charts for a ChartType.
/// </summary>
internal sealed class UIAutogenChartsForChartType
{
	public const string WindowTitle = "Autogen Charts";

	private static readonly int TitleWidth = UiScaled(100);

	private readonly Editor Editor;

	/// <summary>
	/// Whether or not this window is showing.
	/// This state is tracked internally and not persisted.
	/// </summary>
	private bool Showing;
	/// <summary>
	/// The ChartType to use for sourcing charts for autogeneration.
	/// </summary>
	private ChartType? SourceChartType;
	/// <summary>
	/// The ChartType to use for the destination chart for autogeneration.
	/// </summary>
	private ChartType DestinationChartType = ChartType.dance_single;
	/// <summary>
	/// The name of the PerformedChartConfig to use for autogeneration.
	/// </summary>
	private string PerformedChartConfigName;

	public UIAutogenChartsForChartType(Editor editor)
	{
		Editor = editor;
	}

	/// <summary>
	/// Show this UI with the given EditorChart as the source EditorChart for autogeneration.
	/// </summary>
	public void Show()
	{
		SourceChartType = null;
		DestinationChartType = SourceChartType ?? ChartType.dance_single;
		PerformedChartConfigName = PreferencesPerformedChartConfig.DefaultConfigName;
		Showing = true;
	}

	/// <summary>
	/// Close this UI if it is showing.
	/// </summary>
	public void Close()
	{
		Showing = false;
		SourceChartType = null;
		DestinationChartType = ChartType.dance_single;
		PerformedChartConfigName = null;
	}

	/// <summary>
	/// Helper method called before drawing to ensure that the SourceChartType is set.
	/// </summary>
	private void RefreshSourceChartType()
	{
		var song = Editor.GetActiveSong();

		// If the SourceChartType is not set, try to set it.
		if (SourceChartType == null)
		{
			// Use the active Chart, if one exists.
			var activeChart = Editor.GetActiveChart();
			if (activeChart != null)
			{
				SourceChartType = activeChart.ChartType;
				return;
			}

			// Failing that use, use any Chart from the active Song.
			if (song != null)
			{
				var charts = song.GetCharts();
				if (charts?.Count > 0)
				{
					SourceChartType = charts[0].ChartType;
				}
			}
		}
	}

	public void Draw()
	{
		if (!Showing)
			return;

		RefreshSourceChartType();

		ImGui.SetNextWindowSize(new Vector2(0, 0), ImGuiCond.FirstUseEver);
		if (ImGui.Begin(WindowTitle, ref Showing, ImGuiWindowFlags.NoScrollbar))
		{
			if (ImGuiLayoutUtils.BeginTable("Autogen Contents", TitleWidth))
			{
				// Source Chart.
				const string sourceTypeTitle = "Source Type";
				const string sourceTypeHelp = "The type of Charts to use for generating new Charts from.";
				if (SourceChartType != null)
				{
					var sourceType = SourceChartType.Value;
					ImGuiLayoutUtils.DrawRowEnum(sourceTypeTitle, "AutogenChartsSourceChartType", ref sourceType, Editor.SupportedChartTypes,
						sourceTypeHelp);
					SourceChartType = sourceType;
				}
				else
				{
					ImGuiLayoutUtils.DrawTitle(sourceTypeTitle, sourceTypeHelp);
					ImGui.SameLine();
					ImGui.Text("No available Charts.");
				}

				// Destination ChartType.
				ImGuiLayoutUtils.DrawRowEnum("New Type", "AutogenChartsDestinationChartType", ref DestinationChartType, Editor.SupportedChartTypes,
					"Type of Charts to generate.");

				// Performed Chart Config.
				var configValues = Preferences.Instance.PreferencesPerformedChartConfig.GetSortedConfigNames();
				ImGuiLayoutUtils.DrawSelectableConfigFromList("Config", "AutogenChartsPerformedChartConfigName",
					ref PerformedChartConfigName, configValues,
					() => PreferencesPerformedChartConfig.ShowEditUI(PerformedChartConfigName),
					() =>
					{
						Preferences.Instance.ShowAutogenConfigsWindow = true;
						ImGui.SetWindowFocus(UIAutogenConfigs.WindowTitle);
					},
					PreferencesPerformedChartConfig.CreateNewConfigAndShowEditUI,
					"Performed Chart Config.");

				ImGuiLayoutUtils.EndTable();
			}

			ImGui.Separator();

			var performedChartConfig =
				Preferences.Instance.PreferencesPerformedChartConfig.GetNamedConfig(PerformedChartConfigName);
			var canStart = SourceChartType != null && performedChartConfig != null;

			var numCharts = 0;
			IReadOnlyList<EditorChart> sourceCharts = null;
			if (canStart)
			{
				sourceCharts = Editor.GetActiveSong().GetCharts(SourceChartType!.Value);
				numCharts = sourceCharts?.Count ?? 0;
			}

			canStart &= (numCharts > 0);

			string buttonText;
			switch (numCharts)
			{
				case 0: buttonText = "Autogen";
					break;
				case 1: buttonText = $"Autogen 1 {GetPrettyEnumString(DestinationChartType)} Chart";
					break;
				default:
					buttonText = $"Autogen {numCharts} {GetPrettyEnumString(DestinationChartType)} Charts";
					break;
			}

			// Confirm button
			if (!canStart)
				PushDisabled();
			if (ImGui.Button(buttonText))
			{
				ActionQueue.Instance.Do(new ActionAutogenerateCharts(Editor, sourceCharts, DestinationChartType, performedChartConfig!.Config));
				Close();
			}

			if (numCharts == 0 && SourceChartType != null)
				ToolTip($"No {GetPrettyEnumString(SourceChartType.Value)} Charts available.");

			if (!canStart)
				PopDisabled();

			// Cancel button
			ImGui.SameLine();
			if (ImGui.Button("Cancel"))
			{
				Close();
			}
		}
		else
		{
			Close();
		}

		ImGui.End();
	}
}
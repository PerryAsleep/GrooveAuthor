using System.Numerics;
using ImGuiNET;
using static Fumen.Converters.SMCommon;
using static StepManiaEditor.ImGuiUtils;

namespace StepManiaEditor;

/// <summary>
/// Class for drawing options for autogenerating a single Chart.
/// </summary>
internal sealed class UIAutogenChart
{
	public const string WindowTitle = "Autogen Chart";

	private static readonly int TitleWidth = UiScaled(100);

	private readonly Editor Editor;

	/// <summary>
	/// Whether or not this window is showing.
	/// This state is tracked internally and not persisted.
	/// </summary>
	private bool Showing;
	/// <summary>
	/// The EditorChart to use as the source chart for autogeneration.
	/// </summary>
	private EditorChart SourceChart;
	/// <summary>
	/// The ChartType to use for the destination chart for autogeneration.
	/// </summary>
	private ChartType DestinationChartType = ChartType.dance_single;
	/// <summary>
	/// The name of the PerformedChartConfig to use for autogeneration.
	/// </summary>
	private string PerformedChartConfigName;

	public UIAutogenChart(Editor editor)
	{
		Editor = editor;
	}

	/// <summary>
	/// Show this UI with the given EditorChart as the source EditorChart for autogeneration.
	/// </summary>
	/// <param name="sourceChart">The source EditorChart to use for autogeneration. May be null.</param>
	public void Show(EditorChart sourceChart)
	{
		SourceChart = sourceChart;
		DestinationChartType = SourceChart?.ChartType ?? ChartType.dance_single;
		PerformedChartConfigName = PreferencesPerformedChartConfig.DefaultConfigName;
		Showing = true;
	}

	/// <summary>
	/// Close this UI if it is showing.
	/// </summary>
	public void Close()
	{
		Showing = false;
		SourceChart = null;
		DestinationChartType = ChartType.dance_single;
		PerformedChartConfigName = null;
	}

	/// <summary>
	/// Helper method called before drawing to ensure that the SourceChart is a valid
	/// Chart that hasn't been deleted, and that, if unset, it set to the best chart to use.
	/// </summary>
	private void RefreshSourceChart()
	{
		var song = Editor.GetActiveSong();

		// If the SourceChart is set to a valid Chart, ensure that Chart still exists.
		// If the Chart does not exist, set the SourceChart to null.
		if (SourceChart != null)
		{
			if (song == null)
			{
				SourceChart = null;
			}
			else
			{
				var charts = song.GetCharts();
				var sourceChartFound = false;
				foreach (var chart in charts)
				{
					if (SourceChart == chart)
					{
						sourceChartFound = true;
						break;
					}
				}

				if (!sourceChartFound)
				{
					SourceChart = null;
				}
			}
		}

		// If the SourceChart is not set, try to set it.
		if (SourceChart == null)
		{
			// Use the active Chart, if one exists.
			SourceChart = Editor.GetActiveChart();
			if (SourceChart != null)
				return;

			// Failing that use, use any Chart from the active Song.
			if (song != null)
			{
				var charts = song.GetCharts();
				if (charts?.Count > 0)
				{
					SourceChart = charts[0];
				}
			}
		}
	}

	public void Draw()
	{
		if (!Showing)
			return;

		RefreshSourceChart();

		ImGui.SetNextWindowSize(new Vector2(0, 0), ImGuiCond.FirstUseEver);
		if (ImGui.Begin(WindowTitle, ref Showing, ImGuiWindowFlags.NoScrollbar))
		{
			if (ImGuiLayoutUtils.BeginTable("Autogen Contents", TitleWidth))
			{
				// Source Chart.
				ImGuiLayoutUtils.DrawTitle("Source Chart", "The chart to use for generating a new chart from.");
				ImGui.SameLine();
				if (SourceChart != null)
				{
					var selectedName = SourceChart.GetDescriptiveName();
					if (ImGui.BeginCombo("Autogen Source Chart", selectedName))
					{
						UIChartList.DrawChartList(
							Editor.GetActiveSong(),
							SourceChart,
							null,
							selectedChart => SourceChart = selectedChart,
							false,
							null);
						ImGui.EndCombo();
					}
				}
				else
				{
					ImGui.Text("No available Charts.");
				}

				// Expressed Chart Config.
				const string title = "Expression";
				const string help = "Expressed Chart Config."
					+ "\nThis config is defined on the source Chart in the Chart Properties window."
					+ "\nChanging it here changes it on the source Chart.";
				if (SourceChart != null)
					UIChartProperties.DrawChartExpressedChartConfigWidget(SourceChart, title, help);
				else
					ImGuiLayoutUtils.DrawTitleAndText(title, "No available Charts.", help);

				// Destination ChartType.
				ImGuiLayoutUtils.DrawRowEnum("New Chart Type", "AutogenChartChartType", ref DestinationChartType, Editor.SupportedChartTypes,
					"Type of Chart to generate.");

				// Performed Chart Config.
				var configValues = Preferences.Instance.PreferencesPerformedChartConfig.GetSortedConfigNames();
				ImGuiLayoutUtils.DrawSelectableConfigFromList("Config", "AutogenChartPerformedChartConfigName",
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
			var canStart = SourceChart != null && performedChartConfig != null;

			// Confirm button
			if (!canStart)
				PushDisabled();
			if (ImGui.Button("Autogen"))
			{
				ActionQueue.Instance.Do(new ActionAutogenerateChart(Editor, SourceChart, DestinationChartType, performedChartConfig!.Config));
				Close();
			}
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
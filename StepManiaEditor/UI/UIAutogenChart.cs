using System.Numerics;
using ImGuiNET;
using StepManiaEditor.AutogenConfig;
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
		Showing = true;
	}

	/// <summary>
	/// Close this UI if it is showing.
	/// </summary>
	public void Close()
	{
		Showing = false;
		SourceChart = null;
	}

	public void Draw()
	{
		if (!Showing)
			return;

		Utils.EnsureChartReferencesValidChartFromActiveSong(ref SourceChart, Editor);

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
					ImGuiLayoutUtils.DrawExpressedChartConfigCombo(SourceChart, title, help);
				else
					ImGuiLayoutUtils.DrawTitleAndText(title, "No available Charts.", help);

				// Destination ChartType.
				ImGuiLayoutUtils.DrawRowEnum("New Chart Type", "AutogenChartChartType",
					ref Preferences.Instance.LastSelectedAutogenChartType,
					Editor.SupportedChartTypes,
					"Type of Chart to generate.");

				// Performed Chart Config.
				var configGuids = PerformedChartConfigManager.Instance.GetSortedConfigGuids();
				var configNames = PerformedChartConfigManager.Instance.GetSortedConfigNames();
				var selectedIndex = 0;
				for (var i = 0; i < configGuids.Length; i++)
				{
					if (configGuids[i].Equals(Preferences.Instance.LastSelectedAutogenPerformedChartConfig))
					{
						selectedIndex = i;
						break;
					}
				}

				ImGuiLayoutUtils.DrawSelectableConfigFromList("Config", "AutogenChartPerformedChartConfigName",
					ref selectedIndex, configNames,
					() => EditorPerformedChartConfig.ShowEditUI(Preferences.Instance
						.LastSelectedAutogenPerformedChartConfig),
					() =>
					{
						Preferences.Instance.ShowAutogenConfigsWindow = true;
						ImGui.SetWindowFocus(UIAutogenConfigs.WindowTitle);
					},
					EditorPerformedChartConfig.CreateNewConfigAndShowEditUI,
					"Performed Chart Config.");
				Preferences.Instance.LastSelectedAutogenPerformedChartConfig = configGuids[selectedIndex];

				ImGuiLayoutUtils.EndTable();
			}

			ImGui.Separator();

			var performedChartConfig =
				PerformedChartConfigManager.Instance.GetConfig(Preferences.Instance
					.LastSelectedAutogenPerformedChartConfig);
			var canStart = SourceChart != null && performedChartConfig != null;

			// Confirm button
			if (!canStart)
				PushDisabled();
			if (ImGui.Button($"Autogen {GetPrettyEnumString(Preferences.Instance.LastSelectedAutogenChartType)} Chart"))
			{
				ActionQueue.Instance.Do(new ActionAutoGenerateCharts(Editor, SourceChart,
					Preferences.Instance.LastSelectedAutogenChartType,
					performedChartConfig!.Config));
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

using ImGuiNET;
using StepManiaEditor.AutogenConfig;
using static StepManiaEditor.ImGuiUtils;

namespace StepManiaEditor;

/// <summary>
/// Class for drawing options for autogenerating a single Chart.
/// </summary>
internal sealed class UIAutogenChart : UIWindow
{
	private static readonly int TitleWidth = UiScaled(100);
	private static readonly int DefaultWidth = UiScaled(560);

	private Editor Editor;

	/// <summary>
	/// Whether or not this window is showing.
	/// This state is tracked internally and not persisted.
	/// </summary>
	private bool Showing;

	/// <summary>
	/// The EditorChart to use as the source chart for autogeneration.
	/// </summary>
	private EditorChart SourceChart;

	public static UIAutogenChart Instance { get; } = new();

	private UIAutogenChart() : base("Autogen Chart")
	{
	}

	public void Init(Editor editor)
	{
		Editor = editor;
	}


	public override void Open(bool focus)
	{
		Showing = true;
		if (focus)
			Focus();
	}

	/// <summary>
	/// Close this UI if it is showing.
	/// </summary>
	public override void Close()
	{
		Showing = false;
		SourceChart = null;
	}

	/// <summary>
	/// Sets the EditorChart to use as the source EditorChart for autogeneration.
	/// </summary>
	/// <param name="sourceChart">The source EditorChart to use for autogeneration. May be null.</param>
	public void SetChart(EditorChart sourceChart)
	{
		SourceChart = sourceChart;
	}

	public void Draw()
	{
		if (!Showing)
			return;

		Utils.EnsureChartReferencesValidChartFromActiveSong(ref SourceChart, Editor, true);

		if (BeginWindow(WindowTitle, ref Showing, DefaultWidth, ImGuiWindowFlags.NoCollapse))
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
							selectedChart => SourceChart = selectedChart);
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
					ImGuiLayoutUtils.DrawRowTitleAndText(title, "No available Charts.", help);

				// Destination ChartType.
				ImGuiLayoutUtils.DrawRowEnum("New Chart Type", "AutogenChartChartType",
					ref Preferences.Instance.LastSelectedAutogenChartType,
					Editor.SupportedSinglePlayerChartTypes,
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
					() => { UIAutogenConfigs.Instance.Open(true); },
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

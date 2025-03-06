using System.Collections.Generic;
using ImGuiNET;
using StepManiaEditor.AutogenConfig;
using static Fumen.Converters.SMCommon;
using static StepManiaEditor.ImGuiUtils;

namespace StepManiaEditor;

/// <summary>
/// Class for drawing options for autogenerating a set of Charts for a ChartType.
/// </summary>
internal sealed class UIAutogenChartsForChartType : UIWindow
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
	/// The ChartType to use for sourcing charts for autogeneration.
	/// </summary>
	private ChartType? SourceChartType;

	public static UIAutogenChartsForChartType Instance { get; } = new();

	private UIAutogenChartsForChartType() : base("Autogen Charts")
	{
	}

	public void Init(Editor editor)
	{
		Editor = editor;
	}

	public override void Open(bool focus)
	{
		SourceChartType = null;
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
		SourceChartType = null;
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
			// Use the focused Chart, if one exists.
			var focusedChart = Editor.GetFocusedChart();
			if (focusedChart != null && focusedChart.SupportsAutogenFeatures())
			{
				SourceChartType = focusedChart.ChartType;
				return;
			}

			// Failing that use, use any Chart from the active Song.
			if (song != null)
			{
				var charts = song.GetCharts();
				if (charts != null)
				{
					foreach (var existingChart in charts)
					{
						if (existingChart.SupportsAutogenFeatures())
						{
							SourceChartType = existingChart.ChartType;
							break;
						}
					}
				}
			}
		}
	}

	public void Draw()
	{
		if (!Showing)
			return;

		RefreshSourceChartType();

		if (BeginWindow(WindowTitle, ref Showing, DefaultWidth, ImGuiWindowFlags.NoCollapse))
		{
			if (ImGuiLayoutUtils.BeginTable("Autogen Contents", TitleWidth))
			{
				// Source Chart.
				const string sourceTypeTitle = "Source Type";
				const string sourceTypeHelp = "The type of Charts to use for generating new Charts from.";
				if (SourceChartType != null)
				{
					var sourceType = SourceChartType.Value;
					ImGuiLayoutUtils.DrawRowEnum(sourceTypeTitle, "AutogenChartsSourceChartType", ref sourceType,
						Editor.SupportedSinglePlayerChartTypes,
						sourceTypeHelp);
					SourceChartType = sourceType;
				}
				else
				{
					ImGuiLayoutUtils.DrawTitle(sourceTypeTitle, sourceTypeHelp);
					ImGui.SameLine();
					ImGui.TextUnformatted("No available Charts.");
				}

				// Destination ChartType.
				ImGuiLayoutUtils.DrawRowEnum("New Type", "AutogenChartsDestinationChartType",
					ref Preferences.Instance.LastSelectedAutogenChartType, Editor.SupportedSinglePlayerChartTypes,
					"Type of Charts to generate.");

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

				ImGuiLayoutUtils.DrawSelectableConfigFromList("Config", "AutogenChartsPerformedChartConfigName",
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
			var canStart = SourceChartType != null && performedChartConfig != null;

			var numCharts = 0;
			IReadOnlyList<EditorChart> sourceCharts = null;
			if (canStart)
			{
				sourceCharts = Editor.GetActiveSong().GetCharts(SourceChartType!.Value);
				numCharts = sourceCharts?.Count ?? 0;
			}

			canStart &= numCharts > 0;

			string buttonText;
			switch (numCharts)
			{
				case 0:
					buttonText = "Autogen";
					break;
				case 1:
					buttonText = $"Autogen 1 {GetPrettyEnumString(Preferences.Instance.LastSelectedAutogenChartType)} Chart";
					break;
				default:
					buttonText =
						$"Autogen {numCharts} {GetPrettyEnumString(Preferences.Instance.LastSelectedAutogenChartType)} Charts";
					break;
			}

			// Confirm button
			if (!canStart)
				PushDisabled();
			if (ImGui.Button(buttonText))
			{
				ActionQueue.Instance.Do(new ActionAutoGenerateCharts(Editor, sourceCharts,
					Preferences.Instance.LastSelectedAutogenChartType, performedChartConfig!.Config));
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

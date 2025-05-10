using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Fumen;
using Fumen.ChartDefinition;
using StepManiaEditor.AutogenConfig;
using StepManiaLibrary.ExpressedChart;
using StepManiaLibrary.PerformedChart;
using static Fumen.Converters.SMCommon;
using Config = StepManiaLibrary.PerformedChart.Config;

namespace StepManiaEditor;

/// <summary>
/// Action to autogenerate one or more EditorCharts from existing EditorCharts.
/// </summary>
internal sealed class ActionAutoGenerateCharts : EditorAction
{
	private readonly Editor Editor;
	private readonly EditorSong EditorSong;
	private readonly List<EditorChart> SourceCharts;
	private readonly ChartType ChartType;
	private readonly Config PerformedChartConfig;
	private readonly int RandomSeed;
	private readonly EditorChart PreviouslyFocusedChart;
	private readonly List<EditorChart> NewEditorCharts;
	private int NumComplete;

	public ActionAutoGenerateCharts(
		Editor editor,
		EditorChart sourceChart,
		ChartType chartType,
		Config performedChartConfig) : base(true, false)
	{
		Editor = editor;
		SourceCharts = [sourceChart];
		NewEditorCharts = [null];
		PerformedChartConfig = performedChartConfig;
		EditorSong = sourceChart.GetEditorSong();
		PreviouslyFocusedChart = Editor.GetFocusedChart();
		ChartType = chartType;
		RandomSeed = new Random().Next();
	}

	public ActionAutoGenerateCharts(
		Editor editor,
		IReadOnlyList<EditorChart> sourceCharts,
		ChartType chartType,
		Config performedChartConfig) : base(true, false)
	{
		Editor = editor;
		SourceCharts = new List<EditorChart>(sourceCharts.Count);
		SourceCharts.AddRange(sourceCharts);
		NewEditorCharts = new List<EditorChart>(sourceCharts.Count);
		for (var i = 0; i < sourceCharts.Count; i++)
			NewEditorCharts.Add(null);
		PerformedChartConfig = performedChartConfig;
		EditorSong = SourceCharts[0].GetEditorSong();
		PreviouslyFocusedChart = Editor.GetFocusedChart();
		ChartType = chartType;
		RandomSeed = new Random().Next();
	}

	public override string ToString()
	{
		if (SourceCharts.Count > 1)
			return $"Autogenerate {SourceCharts.Count} {ImGuiUtils.GetPrettyEnumString(ChartType)} Charts.";
		return $"Autogenerate {ImGuiUtils.GetPrettyEnumString(ChartType)} Chart.";
	}

	public override bool AffectsFile()
	{
		return true;
	}

	/// <summary>
	/// Autogenerate a single EditorChart from the given sourceChart.
	/// </summary>
	/// <param name="sourceChart">EditorChart to generate from.</param>
	/// <param name="index">The index of this EditorChart.</param>
	private void AutogenerateSingleChart(EditorChart sourceChart, int index)
	{
		var errorString = $"Failed to autogenerate {ImGuiUtils.GetPrettyEnumString(ChartType)} Chart.";

		if (!Editor.GetStepGraph(sourceChart.ChartType, out var inputStepGraph, true) || inputStepGraph == null)
		{
			Logger.Error(errorString);
			OnChartAutogenComplete(index, null);
			return;
		}

		if (!Editor.GetStepGraph(ChartType, out var outputStepGraph, true) || outputStepGraph == null)
		{
			Logger.Error(errorString);
			OnChartAutogenComplete(index, null);
			return;
		}

		if (!Editor.GetStepGraphRootNodes(ChartType, out var rootNodes))
		{
			Logger.Error($"{errorString} No {ImGuiUtils.GetPrettyEnumString(ChartType)} root nodes are present.");
			OnChartAutogenComplete(index, null);
			return;
		}

		var expressedChartConfig = ExpressedChartConfigManager.Instance.GetConfig(sourceChart.ExpressedChartConfig);
		if (expressedChartConfig == null)
		{
			Logger.Error($"{errorString} No {sourceChart.ExpressedChartConfig} Expressed Chart Config defined.");
			OnChartAutogenComplete(index, null);
			return;
		}

		StepTypeFallbacks fallbacks = null;
		if (!inputStepGraph.PadData.CanFitWithin(outputStepGraph.PadData))
		{
			fallbacks = Editor.GetStepTypeFallbacks();
			if (fallbacks == null)
			{
				Logger.Error($"{errorString} No StepType fallbacks are present.");
				OnChartAutogenComplete(index, null);
				return;
			}
		}

		async Task OnChartSaved(Chart chart)
		{
			EditorChart newEditorChart = null;
			await Task.Run(() =>
			{
				try
				{
					// Create an ExpressedChart.
					var expressedChart = ExpressedChart.CreateFromSMEvents(
						chart.Layers[0].Events,
						inputStepGraph,
						expressedChartConfig.Config,
						sourceChart.Rating);
					if (expressedChart == null)
					{
						throw new Exception("Could not create ExpressedChart.");
					}

					// Create a PerformedChart.
					var performedChart = PerformedChart.CreateFromExpressedChart(
						outputStepGraph,
						PerformedChartConfig,
						rootNodes,
						fallbacks,
						expressedChart,
						RandomSeed,
						null);
					if (performedChart == null)
					{
						throw new Exception("Could not create PerformedChart.");
					}

					// Convert PerformedChart to list of Events.
					var newChartEvents = performedChart.CreateSMChartEvents();
					CopyNonPerformanceEvents(chart.Layers[0].Events, newChartEvents);
					newChartEvents.Sort(new SMEventComparer());
					SetEventTimeFromRows(newChartEvents);

					// Create a new Chart from the Events.
					var chartTypeString = ChartTypeString(ChartType);
					var newChart = new Chart
					{
						Artist = chart.Artist,
						ArtistTransliteration = chart.ArtistTransliteration,
						Genre = chart.Genre,
						GenreTransliteration = chart.GenreTransliteration,
						Author = chart.Author,
						Description = chart.Description,
						MusicFile = chart.MusicFile,
						ChartOffsetFromMusic = chart.ChartOffsetFromMusic,
						Tempo = chart.Tempo,
						DifficultyRating = chart.DifficultyRating,
						DifficultyType = chart.DifficultyType,
						Extras = new Extras(chart.Extras),
						Type = chartTypeString,
						NumPlayers = 1,
						NumInputs = outputStepGraph.NumArrows,
					};
					newChart.Layers.Add(new Layer { Events = newChartEvents });

					// Create a new EditorChart from the new Chart.
					newEditorChart = new EditorChart(EditorSong, newChart);
				}
				catch (Exception e)
				{
					Logger.Error($"{errorString} {e}");
					newEditorChart = null;
				}
			});

			MainThreadDispatcher.RunOnMainThread(() => OnChartAutogenComplete(index, newEditorChart));
		}

		// Create a Chart from the EditorChart.
		sourceChart.SaveToChart(new EditorSong.SaveParameters(FileFormatType.SSC, null),
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
			(chart, _) => OnChartSaved(chart));
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
	}

	protected override void DoImplementation()
	{
		// Reset the counter so we can determine when all charts are complete.
		NumComplete = 0;

		// Kick off tasks to generate each chart.
		var index = 0;
		foreach (var chart in SourceCharts)
		{
			AutogenerateSingleChart(chart, index);
			index++;
		}
	}

	private void OnChartAutogenComplete(int chartIndex, EditorChart newChart)
	{
		// Record the new EditorChart.
		NewEditorCharts[chartIndex] = newChart;
		NumComplete++;
		var lastChart = NumComplete == NewEditorCharts.Count;
		if (newChart != null)
		{
			Editor.AddChart(newChart, lastChart);
		}

		// If this is the last EditorChart to have been created, mark this EditorAction complete.
		if (lastChart)
		{
			OnDone();
		}
	}

	protected override void UndoImplementation()
	{
		for (var i = 0; i < NewEditorCharts.Count; i++)
		{
			if (NewEditorCharts[i] == null)
				continue;
			Editor.DeleteChart(NewEditorCharts[i], PreviouslyFocusedChart);
			NewEditorCharts[i] = null;
		}

		NumComplete = 0;
	}
}

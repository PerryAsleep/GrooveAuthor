using System;
using System.Threading.Tasks;
using Fumen;
using Fumen.ChartDefinition;
using StepManiaLibrary;
using StepManiaLibrary.PerformedChart;
using static Fumen.Converters.SMCommon;

namespace StepManiaEditor;

internal sealed class ActionAutogenerateChart : EditorAction
{
	private readonly Editor Editor;
	private readonly EditorSong EditorSong;
	private readonly EditorChart SourceChart;
	private readonly ChartType ChartType;
	private readonly Config PerformedChartConfig;
	private readonly int RandomSeed;
	private readonly EditorChart PreviouslyActiveChart;
	private EditorChart NewEditorChart;
	private bool WasSuccessful;

	public ActionAutogenerateChart(
		Editor editor,
		EditorChart sourceChart,
		ChartType chartType,
		Config performedChartConfig) : base(true, false)
	{
		Editor = editor;
		SourceChart = sourceChart;
		PerformedChartConfig = performedChartConfig;
		EditorSong = SourceChart.GetEditorSong();
		PreviouslyActiveChart = Editor.GetActiveChart();
		ChartType = chartType;
		WasSuccessful = false;
		RandomSeed = new Random().Next();
	}

	public override string ToString()
	{
		return $"Autogenerate {ImGuiUtils.GetPrettyEnumString(ChartType)} Chart.";
	}

	public override bool AffectsFile()
	{
		return true;
	}

	protected override void DoImplementation()
	{
		var errorString = $"Failed to autogenerate {ImGuiUtils.GetPrettyEnumString(ChartType)} Chart.";

		if (!Editor.GetStepGraph(SourceChart.ChartType, out var inputStepGraph))
		{
			Logger.Error($"{errorString} No {ImGuiUtils.GetPrettyEnumString(SourceChart.ChartType)} StepGraph is loaded.");
			OnDone();
			return;
		}

		if (!Editor.GetStepGraph(ChartType, out var outputStepGraph))
		{
			Logger.Error($"{errorString} No {ImGuiUtils.GetPrettyEnumString(ChartType)} StepGraph is loaded.");
			OnDone();
			return;
		}

		if (!Editor.GetStepGraphRootNodes(ChartType, out var rootNodes))
		{
			Logger.Error($"{errorString} No {ImGuiUtils.GetPrettyEnumString(ChartType)} root nodes are present.");
			OnDone();
			return;
		}

		var expressedChartConfig =
			Preferences.Instance.PreferencesExpressedChartConfig.GetConfig(SourceChart.ExpressedChartConfig);
		if (expressedChartConfig == null)
		{
			Logger.Error($"{errorString} No {SourceChart.ExpressedChartConfig} Expressed Chart Config defined.");
			OnDone();
			return;
		}

		StepTypeFallbacks fallbacks = null;
		if (!inputStepGraph.PadData.CanFitWithin(outputStepGraph.PadData))
		{
			fallbacks = Editor.GetStepTypeFallbacks();
			if (fallbacks == null)
			{
				Logger.Error($"{errorString} No StepType fallbacks are present.");
				OnDone();
				return;
			}
		}

		async void OnChartSaved(Chart chart)
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
						expressedChartConfig,
						SourceChart.Rating);
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
					SetEventTimeAndMetricPositionsFromRows(newChartEvents);

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
					newEditorChart = new EditorChart(EditorSong, newChart, Editor);
				}
				catch (Exception e)
				{
					Logger.Error($"{errorString} {e}");
					newEditorChart = null;
				}
			});

			if (newEditorChart != null)
			{
				NewEditorChart = newEditorChart;
				Editor.AddChart(NewEditorChart, true);
				WasSuccessful = true;
			}

			OnDone();
		}

		// Create a Chart from the EditorChart.
		SourceChart.SaveToChart((chart, _) => OnChartSaved(chart));
	}

	protected override void UndoImplementation()
	{
		if (WasSuccessful)
		{
			Editor.DeleteChart(NewEditorChart, PreviouslyActiveChart);
		}

		WasSuccessful = false;
	}
}

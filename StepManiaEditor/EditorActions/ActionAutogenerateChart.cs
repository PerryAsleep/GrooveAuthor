using System;
using System.Collections.Generic;
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
		ChartType chartType) : base(true, false)
	{
		Editor = editor;
		SourceChart = sourceChart;
		EditorSong = SourceChart.GetEditorSong();
		PreviouslyActiveChart = Editor.GetActiveChart();
		ChartType = chartType;
		WasSuccessful = false;
		RandomSeed = new Random().Next();

		// TODO: Configurable PerformedChartConfig.
		PerformedChartConfig = new Config();

		var arrowWeights = new Dictionary<string, List<int>>
		{
			[ChartTypeString(ChartType.dance_single)] = new() { 25, 25, 25, 25 },
			[ChartTypeString(ChartType.dance_double)] = new() { 6, 12, 10, 22, 22, 12, 10, 6 },
			[ChartTypeString(ChartType.dance_solo)] = new() { 13, 12, 25, 25, 12, 13 },
			[ChartTypeString(ChartType.dance_threepanel)] = new() { 25, 50, 25 },
			[ChartTypeString(ChartType.pump_single)] = new() { 17, 16, 34, 16, 17 },
			[ChartTypeString(ChartType.pump_halfdouble)] = new() { 13, 12, 25, 25, 12, 13 }, // WRONG
			[ChartTypeString(ChartType.pump_double)] = new() { 6, 8, 7, 8, 22, 22, 8, 7, 8, 6 }, // WRONG
			[ChartTypeString(ChartType.smx_beginner)] = new() { 25, 50, 25 },
			[ChartTypeString(ChartType.smx_single)] = new() { 25, 21, 8, 21, 25 },
			[ChartTypeString(ChartType.smx_dual)] = new() { 8, 17, 25, 25, 17, 8 },
			[ChartTypeString(ChartType.smx_full)] = new() { 6, 8, 7, 8, 22, 22, 8, 7, 8, 6 },
		};
		PerformedChartConfig.ArrowWeights = arrowWeights;

		var stepTighteningConfig = new Config.StepTighteningConfig
		{
			TravelSpeedMinTimeSeconds = 0.176471,
			TravelSpeedMaxTimeSeconds = 0.24,
			TravelDistanceMin = 2.0,
			TravelDistanceMax = 3.0,
			StretchDistanceMin = 3.0,
			StretchDistanceMax = 4.0,
		};
		PerformedChartConfig.StepTightening = stepTighteningConfig;

		var lateralTighteningConfig = new Config.LateralTighteningConfig
		{
			RelativeNPS = 1.65,
			AbsoluteNPS = 12.0,
			Speed = 3.0,
		};
		PerformedChartConfig.LateralTightening = lateralTighteningConfig;

		var facingConfig = new Config.FacingConfig
		{
			MaxInwardPercentage = 1.0,
			MaxOutwardPercentage = 1.0,
		};
		PerformedChartConfig.Facing = facingConfig;

		PerformedChartConfig.Init();
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

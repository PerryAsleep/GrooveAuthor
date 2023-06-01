using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Fumen;
using Fumen.ChartDefinition;
using StepManiaLibrary;
using StepManiaLibrary.PerformedChart;
using static Fumen.Converters.SMCommon;

namespace StepManiaEditor
{
	internal sealed class ActionAutogenerateChart : EditorAction
	{
		private Editor Editor;
		private EditorSong EditorSong;
		private EditorChart SourceChart;
		private EditorChart PreivouslyActiveChart;
		private EditorChart NewEditorChart;
		private ChartType ChartType;
		private StepManiaLibrary.PerformedChart.Config PerformedChartConfig;
		private int RandomSeed;
		private bool WasSuccessful;

		public ActionAutogenerateChart(
			Editor editor,
			EditorChart sourceChart,
			ChartType chartType) : base(true, false)
		{
			Editor = editor;
			SourceChart = sourceChart;
			EditorSong = SourceChart.GetEditorSong();
			PreivouslyActiveChart = Editor.GetActiveChart();
			ChartType = chartType;
			WasSuccessful = false;
			RandomSeed = new Random().Next();

			// TODO: Configurable PerformedChartConfig.
			PerformedChartConfig = new Config();

			var arrowWeights = new Dictionary<string, List<int>>();
			arrowWeights[ChartTypeString(ChartType.dance_single)] = new List<int> { 25, 25, 25, 25 };
			arrowWeights[ChartTypeString(ChartType.dance_double)] = new List<int> { 6, 12, 10, 22, 22, 12, 10, 6 };
			arrowWeights[ChartTypeString(ChartType.dance_solo)] = new List<int> { 13, 12, 25, 25, 12, 13 };
			arrowWeights[ChartTypeString(ChartType.dance_threepanel)] = new List<int> { 25, 50, 25 };
			arrowWeights[ChartTypeString(ChartType.pump_single)] = new List<int> { 17, 16, 34, 16, 17 };
			arrowWeights[ChartTypeString(ChartType.pump_halfdouble)] = new List<int> { 13, 12, 25, 25, 12, 13 }; // WRONG
			arrowWeights[ChartTypeString(ChartType.pump_double)] = new List<int> { 6, 8, 7, 8, 22, 22, 8, 7, 8, 6 }; // WRONG
			arrowWeights[ChartTypeString(ChartType.smx_beginner)] = new List<int> { 25, 50, 25 };
			arrowWeights[ChartTypeString(ChartType.smx_single)] = new List<int> { 25, 21, 8, 21, 25 };
			arrowWeights[ChartTypeString(ChartType.smx_dual)] = new List<int> { 8, 17, 25, 25, 17, 8 };
			arrowWeights[ChartTypeString(ChartType.smx_full)] = new List<int> { 6, 8, 7, 8, 22, 22, 8, 7, 8, 6 };
			PerformedChartConfig.ArrowWeights = arrowWeights;

			var stepTighteningConfig = new Config.StepTighteningConfig();
			stepTighteningConfig.TravelSpeedMinTimeSeconds = 0.176471;
			stepTighteningConfig.TravelSpeedMaxTimeSeconds = 0.24;
			stepTighteningConfig.TravelDistanceMin = 2.0;
			stepTighteningConfig.TravelDistanceMax = 3.0;
			stepTighteningConfig.StretchDistanceMin = 3.0;
			stepTighteningConfig.StretchDistanceMax = 4.0;
			PerformedChartConfig.StepTightening = stepTighteningConfig;

			var lateralTighteningConfig = new Config.LateralTighteningConfig();
			lateralTighteningConfig.RelativeNPS = 1.65;
			lateralTighteningConfig.AbsoluteNPS = 12.0;
			lateralTighteningConfig.Speed = 3.0;
			PerformedChartConfig.LateralTightening = lateralTighteningConfig;

			var facingConfig = new Config.FacingConfig();
			facingConfig.MaxInwardPercentage = 1.0;
			facingConfig.MaxOutwardPercentage = 1.0;
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

			var expressedChartConfig = Preferences.Instance.PreferencesExpressedChartConfig.GetConfig(SourceChart.ExpressedChartConfig);
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

			// Create a Chart from the EditorChart.
			SourceChart.SaveToChart(async (Chart chart, Dictionary<string, string> properties) =>
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
							NumInputs = outputStepGraph.NumArrows
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
			});
		}

		protected override void UndoImplementation()
		{
			if (WasSuccessful)
			{
				Editor.DeleteChart(NewEditorChart, PreivouslyActiveChart);
			}
			WasSuccessful = false;
		}
	}
}

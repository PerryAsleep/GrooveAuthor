using System.Collections.Generic;
using System.Text.Json.Serialization;
using static Fumen.Converters.SMCommon;

namespace StepManiaEditor;

/// <summary>
/// Information saved with Preferences per recently opened song.
/// </summary>
internal sealed class SavedSongInformation : IActiveChartListProvider
{
	private const int UnsetChartIndex = -1;

	/// <summary>
	/// Information saved about a chart for showing the last visible charts again
	/// when the song file is reloaded. We want to uniquely identify a chart but charts
	/// have nothing inherently unique about them. Multiple charts can have the same
	/// type, difficulty type, name, description, rating, etc. This application uses
	/// guids but those are not persisted and it is best to not try add more
	/// application-specific saved data in the sm/ssc files unless it is needed. We also
	/// do not want to save the sorted chart index because if that sort logic ever changes
	/// it could have significant effects on which charts are shown at startup. Given
	/// that, we save off a handful of fields which in the overwhelming majority of cases
	/// will uniquely identify a chart. If a song has multiple charts with the same info
	/// saved here then we will choose an arbitrary one.
	/// </summary>
	public class SavedChartInformation
	{
		[JsonInclude] public ChartType ChartType;
		[JsonInclude] public ChartDifficultyType ChartDifficultyType;
		[JsonInclude] public int Rating;
		[JsonInclude] public string Name;
		[JsonInclude] public string Description;

		/// <summary>
		/// Default constructor. Implemented only to support json deserialization.
		/// </summary>
		public SavedChartInformation()
		{
		}

		/// <summary>
		/// Constructor taking all needed information for persistence.
		/// </summary>
		public SavedChartInformation(ActiveEditorChart activeChart)
		{
			var chart = activeChart.GetChart();
			ChartType = chart.ChartType;
			ChartDifficultyType = chart.ChartDifficultyType;
			Rating = chart.Rating;
			Name = chart.Name;
			Description = chart.Description;
		}

		/// <summary>
		/// Constructor for deprecated flow where only the ChartType and ChartDifficultyType are known.
		/// </summary>
		public SavedChartInformation(ChartType chartType, ChartDifficultyType chartDifficultyType)
		{
			ChartType = chartType;
			ChartDifficultyType = chartDifficultyType;
		}

		public EditorChart GetExactMatchingChart(EditorSong song)
		{
			var chartsMatchingType = song.GetCharts(ChartType);
			if (chartsMatchingType == null)
				return null;
			foreach (var chart in chartsMatchingType)
			{
				if (chart.ChartDifficultyType == ChartDifficultyType
				    && chart.Rating == Rating
				    && chart.Name == Name
				    && chart.Description == Description)
				{
					return chart;
				}
			}

			return null;
		}

		public EditorChart GetBestMatchingChart(EditorSong song)
		{
			return song.SelectBestChart(ChartType, ChartDifficultyType);
		}
	}

	[JsonInclude] public string FileName;
	[JsonInclude] public double SpacingZoom = 1.0;
	[JsonInclude] public double ChartPosition;
	[JsonInclude] public List<SavedChartInformation> ActiveCharts = new();
	[JsonInclude] public int FocusedChartIndex = UnsetChartIndex;

	[JsonInclude]
	[JsonPropertyName("LastChartType")]
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
	public ChartType LastChartTypeDeprecated { private get; set; }

	[JsonInclude]
	[JsonPropertyName("LastChartDifficultyType")]
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
	public ChartDifficultyType LastChartDifficultyTypeDeprecated { private get; set; }

	/// <summary>
	/// Default constructor. Implemented only to support json deserialization.
	/// </summary>
	public SavedSongInformation()
	{
	}

	/// <summary>
	/// Constructor taking all needed information for persistence.
	/// </summary>
	public SavedSongInformation(string fileName, double spacingZoom, double chartPosition,
		IReadOnlyList<ActiveEditorChart> activeCharts, ActiveEditorChart focusedChart)
	{
		FileName = fileName;
		Update(activeCharts, focusedChart, spacingZoom, chartPosition);
	}

	public void PostLoad()
	{
		// Migrate from deprecated data.
		if (ActiveCharts == null || ActiveCharts.Count == 0)
		{
			ActiveCharts = new List<SavedChartInformation>
			{
				new(LastChartTypeDeprecated, LastChartDifficultyTypeDeprecated),
			};
			FocusedChartIndex = 0;
		}

		// We want to not serialize these but System.Text.Json does not have a clean way
		// of preventing only serialization without also preventing deserialization. So
		// set them to their default values and decorate them with JsonIgnoreCondition.WhenWritingDefault.
		LastChartTypeDeprecated = default;
		LastChartDifficultyTypeDeprecated = default;
	}

	public void Update(IReadOnlyList<ActiveEditorChart> activeCharts, ActiveEditorChart focusedChart, double spacingZoom,
		double chartPosition)
	{
		ActiveCharts.Clear();
		FocusedChartIndex = UnsetChartIndex;
		for (var i = 0; i < activeCharts.Count; i++)
		{
			if (focusedChart == activeCharts[i])
				FocusedChartIndex = i;
			ActiveCharts.Add(new SavedChartInformation(activeCharts[i]));
		}

		SpacingZoom = spacingZoom;
		ChartPosition = chartPosition;
	}

	#region IActiveChartListProvider

	public List<EditorChart> GetChartsToUseForActiveCharts(EditorSong song)
	{
		var charts = new List<EditorChart>();
		for (var i = 0; i < ActiveCharts.Count; i++)
		{
			if (i == FocusedChartIndex)
			{
				var chart = GetChartToUseForFocusedChart(song);
				if (chart != null)
				{
					if (!charts.Contains(chart))
						charts.Add(chart);
				}
			}
			else
			{
				var chart = ActiveCharts[i].GetExactMatchingChart(song);
				if (chart != null)
				{
					if (!charts.Contains(chart))
						charts.Add(chart);
				}
			}
		}

		return charts;
	}

	public EditorChart GetChartToUseForFocusedChart(EditorSong song)
	{
		// Allow fallback to charts even if they don't perfectly match.
		var focusedChartData = ActiveCharts[FocusedChartIndex];
		var match = focusedChartData.GetExactMatchingChart(song);
		if (match != null)
			return match;
		return focusedChartData.GetBestMatchingChart(song);
	}

	#endregion IActiveChartListProvider
}

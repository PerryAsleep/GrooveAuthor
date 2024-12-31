using System.Collections.Generic;
using static Fumen.Converters.SMCommon;

namespace StepManiaEditor;

/// <summary>
/// IActiveChartListProvider for selecting one chart using a preferred ChartType and ChartDifficultyType.
/// </summary>
internal sealed class DefaultChartListProvider : IActiveChartListProvider
{
	private readonly ChartType PreferredChartType;
	private readonly ChartDifficultyType PreferredChartDifficultyType;

	public DefaultChartListProvider(ChartType preferredChartType, ChartDifficultyType preferredChartDifficultyType)
	{
		PreferredChartType = preferredChartType;
		PreferredChartDifficultyType = preferredChartDifficultyType;
	}

	#region IActiveChartListProvider

	public List<EditorChart> GetChartsToUseForActiveCharts(EditorSong song)
	{
		var activeCharts = new List<EditorChart>();
		var focusedChart = GetChartToUseForFocusedChart(song);
		if (focusedChart != null)
			activeCharts.Add(focusedChart);
		return activeCharts;
	}

	public EditorChart GetChartToUseForFocusedChart(EditorSong song)
	{
		return song.SelectBestChart(PreferredChartType, PreferredChartDifficultyType);
	}

	#endregion IActiveChartListProvider
}

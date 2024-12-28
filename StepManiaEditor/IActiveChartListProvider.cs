using System.Collections.Generic;

namespace StepManiaEditor;

/// <summary>
/// Interface for classes which can provide a list of active charts and a focused chart to use
/// for a given EditorSong.
/// </summary>
internal interface IActiveChartListProvider
{
	public List<EditorChart> GetChartsToUseForActiveCharts(EditorSong song);
	public EditorChart GetChartToUseForFocusedChart(EditorSong song);
}

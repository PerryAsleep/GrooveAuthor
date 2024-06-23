using Fumen;
using static StepManiaEditor.EditorEvents.Containers.EventTreeUtils;

namespace StepManiaEditor.EditorEvents.Containers;

/// <summary>
/// Read-only interface for specialization of RedBlackTree on EditorInterpolatedRateAlteringEvents
/// with additional methods for finding the active interpolated scroll rate based on EditorPosition.
/// </summary>
internal interface IReadOnlyInterpolatedRateAlteringEventTree : IReadOnlyRedBlackTree<EditorInterpolatedRateAlteringEvent>
{
	double FindScrollRate(IReadOnlyEditorPosition p);
}

/// <summary>
/// Specialization of RedBlackTree on EditorInterpolatedRateAlteringEvents with additional
/// methods for finding the active interpolated scroll rate based on EditorPosition.
/// </summary>
internal sealed class InterpolatedRateAlteringEventTree : RedBlackTree<EditorInterpolatedRateAlteringEvent>,
	IReadOnlyInterpolatedRateAlteringEventTree
{
	/// <summary>
	/// Underlying chart which owns this InterpolatedRateAlteringEventTree.
	/// </summary>
	private readonly EditorChart Chart;

	/// <summary>
	/// Constructor
	/// </summary>
	/// <param name="chart">EditorChart which owns this InterpolatedRateAlteringEventTree.</param>
	public InterpolatedRateAlteringEventTree(EditorChart chart)
	{
		Chart = chart;
	}

	/// <summary>
	/// Finds the active interpolated scroll rate for the given IReadOnlyEditorPosition.
	/// </summary>
	/// <param name="position">IReadOnlyEditorPosition to find the scroll rate of.</param>
	/// <returns>Active interpolated scroll rate for the given IReadOnlyEditorPosition</returns>
	public double FindScrollRate(IReadOnlyEditorPosition position)
	{
		EditorInterpolatedRateAlteringEvent activeRate;
		var pos = (EditorRateAlteringEvent)EditorEvent.CreateEvent(
			EventConfig.CreateSearchEventConfigWithOnlyRow(Chart, position.ChartPosition));
		var enumerator = FindGreatestPreceding(pos, true);
		if (enumerator != null && EnsureGreatestLessThanOrEqualToPosition(enumerator, position.ChartPosition))
		{
			enumerator.MoveNext();
			activeRate = enumerator.Current;
		}
		else
		{
			FirstValue(out activeRate);
		}

		if (activeRate != null)
		{
			if (activeRate!.InterpolatesByTime())
				return activeRate.GetInterpolatedScrollRateFromTime(position.ChartTime);
			return activeRate.GetInterpolatedScrollRateFromRow(position.ChartPosition);
		}

		return 1.0;
	}
}

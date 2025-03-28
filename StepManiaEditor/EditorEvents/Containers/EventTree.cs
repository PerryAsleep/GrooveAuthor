using System.Collections.Generic;
using System.Diagnostics;
using Fumen;
using static System.Diagnostics.Debug;
using static StepManiaEditor.EditorEvents.Containers.EventTreeUtils;
using static StepManiaLibrary.Constants;

namespace StepManiaEditor.EditorEvents.Containers;

/// <summary>
/// Read-only interface for specialization of RedBlackTree on EditorEvents
/// with additional methods for performing searches for events based on
/// chart time and chart position.
/// </summary>
internal interface IReadOnlyEventTree : IReadOnlyRedBlackTree<EditorEvent>
{
	IReadOnlyRedBlackTreeEnumerator FindBestByPosition(double chartPosition);
	IReadOnlyRedBlackTreeEnumerator FindGreatestBeforeChartPosition(double chartPosition);
	IReadOnlyRedBlackTreeEnumerator FindGreatestAtOrBeforeChartPosition(double chartPosition);
	IReadOnlyRedBlackTreeEnumerator FindLeastAfterChartPosition(double chartPosition);
	IReadOnlyRedBlackTreeEnumerator FindLeastAtOrAfterChartPosition(double chartPosition);
	IReadOnlyRedBlackTreeEnumerator FindBestByTime(double chartTime);
	IReadOnlyRedBlackTreeEnumerator FindGreatestBeforeChartTime(double chartTime);
	IReadOnlyRedBlackTreeEnumerator FindGreatestAtOrBeforeChartTime(double chartTime);
	IReadOnlyRedBlackTreeEnumerator FindLeastAfterChartTime(double chartTime);
	IReadOnlyRedBlackTreeEnumerator FindLeastAtOrAfterChartTime(double chartTime);
	EditorEvent FindPreviousEventWithLooping(double chartPosition);
	EditorEvent FindNextEventWithLooping(double chartPosition);
	EditorEvent FindNoteAt(int row, int lane, bool ignoreNotesBeingEdited);
	List<EditorEvent> FindEventsAtRow(int row);
}

/// <summary>
/// Specialization of RedBlackTree on EditorEvents with additional
/// methods for performing searches for events based on chart time
/// and chart position.
/// </summary>
internal class EventTree : RedBlackTree<EditorEvent>, IReadOnlyEventTree
{
	/// <summary>
	/// Underlying chart which owns this EventTree.
	/// </summary>
	private readonly EditorChart Chart;

	/// <summary>
	/// Debug flag for checking the tree to ensure events are sorted as expected.
	/// When set, lists will be generated from the tree for easy debugger inspection.
	/// </summary>
	private readonly bool DebugEditorEventSort = false;

	/// <summary>
	/// List just for looking the previous state of the sorted tree in the debugger.
	/// </summary>
	// ReSharper disable once NotAccessedField.Local
	private List<EditorEvent> PreviousList = [];

	/// <summary>
	/// Constructor
	/// </summary>
	/// <param name="chart">EditorChart which owns this EventTree.</param>
	public EventTree(EditorChart chart)
	{
		Chart = chart;
	}

	/// <summary>
	/// Find the EditorEvent that is the greatest event which precedes the given chart position.
	/// If no EditorEvent precedes the given chart position, instead find the EditorEvent that
	/// is the least event which follows or is equal to the given chart position.
	/// </summary>
	/// <returns>Enumerator to best value or null if a value could not be found.</returns>
	public IReadOnlyRedBlackTree<EditorEvent>.IReadOnlyRedBlackTreeEnumerator FindBestByPosition(double chartPosition)
	{
		var pos = EditorEvent.CreateEvent(EventConfig.CreateSearchEventConfigWithOnlyRow(Chart, chartPosition));
		var enumerator = FindGreatestPreceding(pos);
		if (enumerator != null && EnsureGreatestLessThanPosition(enumerator, chartPosition))
			return enumerator;
		enumerator = FindLeastFollowing(pos, true);
		if (enumerator == null || !EnsureLeastGreaterThanOrEqualToPosition(enumerator, chartPosition))
			return null;
		return enumerator;
	}

	public IReadOnlyRedBlackTree<EditorEvent>.IReadOnlyRedBlackTreeEnumerator FindGreatestAtOrBeforeChartPosition(
		double chartPosition)
	{
		var pos = EditorEvent.CreateEvent(EventConfig.CreateSearchEventConfigWithOnlyRow(Chart, chartPosition));
		var enumerator = FindGreatestPreceding(pos, true);
		if (enumerator != null && EnsureGreatestLessThanOrEqualToPosition(enumerator, chartPosition))
			return enumerator;
		return null;
	}

	public IReadOnlyRedBlackTree<EditorEvent>.IReadOnlyRedBlackTreeEnumerator FindGreatestBeforeChartPosition(
		double chartPosition)
	{
		var pos = EditorEvent.CreateEvent(EventConfig.CreateSearchEventConfigWithOnlyRow(Chart, chartPosition));
		var enumerator = FindGreatestPreceding(pos);
		if (enumerator != null && EnsureGreatestLessThanPosition(enumerator, chartPosition))
			return enumerator;
		return null;
	}

	public IReadOnlyRedBlackTree<EditorEvent>.IReadOnlyRedBlackTreeEnumerator FindLeastAfterChartPosition(double chartPosition)
	{
		var pos = EditorEvent.CreateEvent(EventConfig.CreateSearchEventConfigWithOnlyRow(Chart, chartPosition));
		var enumerator = FindLeastFollowing(pos);
		if (enumerator != null && EnsureLeastGreaterThanPosition(enumerator, chartPosition))
			return enumerator;
		return null;
	}

	public IReadOnlyRedBlackTree<EditorEvent>.IReadOnlyRedBlackTreeEnumerator FindLeastAtOrAfterChartPosition(
		double chartPosition)
	{
		var pos = EditorEvent.CreateEvent(EventConfig.CreateSearchEventConfigWithOnlyRow(Chart, chartPosition));
		var enumerator = FindLeastFollowing(pos, true);
		if (enumerator != null && EnsureLeastGreaterThanOrEqualToPosition(enumerator, chartPosition))
			return enumerator;
		return null;
	}

	/// <summary>
	/// Find the EditorEvent that is the greatest event which precedes the given chart time.
	/// If no EditorEvent precedes the given chart time, instead find the EditorEvent that
	/// is the least event which follows or is equal to the given chart time.
	/// </summary>
	/// <returns>Enumerator to best value or null if a value could not be found.</returns>
	public IReadOnlyRedBlackTree<EditorEvent>.IReadOnlyRedBlackTreeEnumerator FindBestByTime(double chartTime)
	{
		var pos = EditorEvent.CreateEvent(EventConfig.CreateSearchEventConfigWithOnlyTime(Chart, chartTime));
		var enumerator = FindGreatestPreceding(pos);
		if (enumerator != null && EnsureGreatestLessThanTime(enumerator, chartTime))
			return enumerator;
		enumerator = FindLeastFollowing(pos, true);
		if (enumerator == null || !EnsureLeastGreaterThanOrEqualToTime(enumerator, chartTime))
			return null;
		return enumerator;
	}

	public IReadOnlyRedBlackTree<EditorEvent>.IReadOnlyRedBlackTreeEnumerator FindGreatestBeforeChartTime(double chartTime)
	{
		var pos = EditorEvent.CreateEvent(EventConfig.CreateSearchEventConfigWithOnlyTime(Chart, chartTime));
		var enumerator = FindGreatestPreceding(pos);
		if (enumerator != null && EnsureGreatestLessThanTime(enumerator, chartTime))
			return enumerator;
		return null;
	}

	public IReadOnlyRedBlackTree<EditorEvent>.IReadOnlyRedBlackTreeEnumerator FindGreatestAtOrBeforeChartTime(double chartTime)
	{
		var pos = EditorEvent.CreateEvent(EventConfig.CreateSearchEventConfigWithOnlyTime(Chart, chartTime));
		var enumerator = FindGreatestPreceding(pos, true);
		if (enumerator != null && EnsureGreatestLessThanOrEqualToTime(enumerator, chartTime))
			return enumerator;
		return null;
	}

	public IReadOnlyRedBlackTree<EditorEvent>.IReadOnlyRedBlackTreeEnumerator FindLeastAfterChartTime(double chartTime)
	{
		var pos = EditorEvent.CreateEvent(EventConfig.CreateSearchEventConfigWithOnlyTime(Chart, chartTime));
		var enumerator = FindLeastFollowing(pos);
		if (enumerator != null && EnsureLeastGreaterThanTime(enumerator, chartTime))
			return enumerator;
		return null;
	}

	public IReadOnlyRedBlackTree<EditorEvent>.IReadOnlyRedBlackTreeEnumerator FindLeastAtOrAfterChartTime(double chartTime)
	{
		var pos = EditorEvent.CreateEvent(EventConfig.CreateSearchEventConfigWithOnlyTime(Chart, chartTime));
		var enumerator = FindLeastFollowing(pos, true);
		if (enumerator != null && EnsureLeastGreaterThanOrEqualToTime(enumerator, chartTime))
			return enumerator;
		return null;
	}

	public EditorEvent FindNoteAt(int row, int lane, bool ignoreNotesBeingEdited)
	{
		var pos = EditorEvent.CreateEvent(EventConfig.CreateSearchEventConfig(Chart, row));

		// Find the first event at the given row.
		var best = FindLeastFollowing(pos);
		if (best == null)
			return null;

		// Scan forward to the last note in the row to make sure we consider all notes this row.
		while (best.MoveNext())
		{
			if (best.Current!.GetRow() > row)
			{
				best.MovePrev();
				if (!best.IsCurrentValid())
					return null;
				break;
			}
		}

		if (!best.IsCurrentValid())
			best.MovePrev();

		// Scan backwards finding a note in the given lane and row, or a hold
		// which starts before the given now but ends at or after it.
		do
		{
			if (best.Current!.GetLane() != lane)
				continue;
			if (ignoreNotesBeingEdited && best.Current.IsBeingEdited())
				continue;
			if (best.Current.GetRow() <= row)
			{
				if (best.Current.GetEndRow() >= row)
					return best.Current;
				return null;
			}
		} while (best.MovePrev());

		return null;
	}

	public List<EditorEvent> FindEventsAtRow(int row)
	{
		var events = new List<EditorEvent>();
		var enumerator = FindBestByPosition(row);
		if (enumerator == null)
			return events;
		while (enumerator.MoveNext() && enumerator.Current!.GetRow() <= row)
		{
			if (enumerator.Current.GetRow() == row)
				events.Add(enumerator.Current);
		}

		return events;
	}

	public EditorEvent FindPreviousEventWithLooping(double chartPosition)
	{
		if (GetCount() == 0)
			return null;

		var enumerator = FindGreatestBeforeChartPosition(chartPosition);
		if (enumerator != null && enumerator.MoveNext() && enumerator.IsCurrentValid())
			return enumerator.Current;

		enumerator = Last();
		if (enumerator != null && enumerator.MoveNext() && enumerator.IsCurrentValid())
			return enumerator.Current;

		return null;
	}

	public EditorEvent FindNextEventWithLooping(double chartPosition)
	{
		if (GetCount() == 0)
			return null;

		var enumerator = FindLeastAfterChartPosition(chartPosition);
		if (enumerator != null && enumerator.MoveNext() && enumerator.IsCurrentValid())
			return enumerator.Current;

		enumerator = First();
		if (enumerator != null && enumerator.MoveNext() && enumerator.IsCurrentValid())
			return enumerator.Current;

		return null;
	}

	public new void Insert(EditorEvent data)
	{
		Validate();
		base.Insert(data);
		Validate();
	}

	public new bool Delete(EditorEvent data)
	{
		Validate();
		var ret = base.Delete(data);
		Validate();
		return ret;
	}

	/// <summary>
	/// Debug validation method to assert that the tree is consistent.
	/// The editor can alter event timing, which requires removing events from the tree,
	/// altering the events (and handling any side effects) and re-adding the events. This
	/// method can help ensure when adding new edit operations that they function as
	/// expected. Ideally those operations should be covered by unit tests.
	/// </summary>
	[Conditional("DEBUG")]
	public void Validate()
	{
		if (!DebugEditorEventSort)
			return;

		var enumerator = First();
		var list = new List<EditorEvent>();
		while (enumerator != null && enumerator.MoveNext())
		{
			list.Add(enumerator.Current);
		}

		var previousRow = 0;
		var laneNotes = new EditorEvent[Chart.NumInputs];
		var eventsByTypeAtCurrentRow = new HashSet<System.Type>();
		for (var i = 0; i < list.Count; i++)
		{
			// Ensure events are sorted as expected.
			if (i > 0)
			{
				var previousBeforeThis = list[i - 1].CompareTo(list[i]) < 0;
				var thisAfterPrevious = list[i].CompareTo(list[i - 1]) > 0;
				Assert(previousBeforeThis && thisAfterPrevious);
			}

			if (i < list.Count - 1)
			{
				var thisBeforeNext = list[i].CompareTo(list[i + 1]) < 0;
				var nextAfterThis = list[i + 1].CompareTo(list[i]) > 0;
				Assert(thisBeforeNext && nextAfterThis);
			}

			// Ensure rows never decrease.
			var row = list[i].GetRow();
			Assert(row >= previousRow);

			// Update row tracking variables.
			if (row != previousRow)
			{
				for (var l = 0; l < Chart.NumInputs; l++)
					laneNotes[l] = null;
				eventsByTypeAtCurrentRow.Clear();
			}

			// Ensure there aren't two events at the same row and lane.
			var lane = list[i].GetLane();
			if (lane != InvalidArrowIndex)
			{
				Assert(laneNotes[lane] == null);
				laneNotes[lane] = list[i];
			}
			// Ensure there aren't two non-lane events at the same row with the same type.
			else
			{
				Assert(!eventsByTypeAtCurrentRow.Contains(list[i].GetType()));
				eventsByTypeAtCurrentRow.Add(list[i].GetType());
			}

			previousRow = row;
		}

		PreviousList = list;
	}
}

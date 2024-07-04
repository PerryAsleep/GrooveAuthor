using Fumen;
using Fumen.ChartDefinition;
using static StepManiaEditor.EditorEvents.Containers.EventTreeUtils;

namespace StepManiaEditor.EditorEvents.Containers;

/// <summary>
/// Read-only interface for specialization of RedBlackTree on EditorRateAlteringEvents
/// with additional methods for performing searches for events based on
/// chart time and chart position.
/// </summary>
internal interface IReadOnlyRateAlteringEventTree : IReadOnlyRedBlackTree<EditorRateAlteringEvent>
{
	IReadOnlyRedBlackTreeEnumerator FindBest(IReadOnlyEditorPosition p);
	IReadOnlyRedBlackTreeEnumerator FindBestByTime(double chartTime);
	IReadOnlyRedBlackTreeEnumerator FindBestByPosition(double chartPosition);
	EditorRateAlteringEvent FindActiveRateAlteringEvent(EditorEvent editorEvent);
	EditorRateAlteringEvent FindActiveRateAlteringEvent(Event smEvent);
	IReadOnlyRedBlackTreeEnumerator FindActiveRateAlteringEventEnumeratorForTime(double chartTime, bool allowEqualTo = true);
	EditorRateAlteringEvent FindActiveRateAlteringEventForTime(double chartTime, bool allowEqualTo = true);

	IReadOnlyRedBlackTreeEnumerator FindActiveRateAlteringEventEnumeratorForPosition(double chartPosition,
		bool allowEqualTo = true);

	EditorRateAlteringEvent FindActiveRateAlteringEventForPosition(double chartPosition, bool allowEqualTo = true);

	TEvent FindEventAtRow<TEvent>(int row) where TEvent : EditorRateAlteringEvent;
}

/// <summary>
/// Specialization of RedBlackTree on EditorRateAlteringEvents with additional
/// methods for performing searches for events based on chart time
/// and chart position.
/// </summary>
internal class RateAlteringEventTree : RedBlackTree<EditorRateAlteringEvent>, IReadOnlyRateAlteringEventTree
{
	/// <summary>
	/// Underlying chart which owns this RateAlteringEventTree.
	/// </summary>
	private readonly EditorChart Chart;

	/// <summary>
	/// Constructor
	/// </summary>
	/// <param name="chart">EditorChart which owns this RateAlteringEventTree.</param>
	public RateAlteringEventTree(EditorChart chart)
	{
		Chart = chart;
	}

	/// <summary>
	/// Find the EditorRateAlteringEvents that is the greatest event which precedes the given chart position.
	/// If no EditorRateAlteringEvents precedes the given chart position, instead find the EditorRateAlteringEvents that
	/// is the least event which follows or is equal to the given chart position.
	/// </summary>
	/// <returns>Enumerator to best value or null if a value could not be found.</returns>
	public IReadOnlyRedBlackTree<EditorRateAlteringEvent>.IReadOnlyRedBlackTreeEnumerator FindBest(IReadOnlyEditorPosition p)
	{
		if (Preferences.Instance.PreferencesScroll.SpacingMode == Editor.SpacingMode.ConstantTime)
			return FindBestByTime(p.ChartTime);
		return FindBestByPosition(p.ChartPosition);
	}

	/// <summary>
	/// Find the EditorRateAlteringEvent that is the greatest event which precedes the given time.
	/// If no EditorRateAlteringEvent precedes the given time, instead find the EditorRateAlteringEvent
	/// that is the least event which follows or is equal to the given time.
	/// </summary>
	/// <returns>Enumerator to best EditorRateAlteringEvent or null if a value could not be found.</returns>
	public IReadOnlyRedBlackTree<EditorRateAlteringEvent>.IReadOnlyRedBlackTreeEnumerator FindBestByTime(double chartTime)
	{
		// Set up a dummy event to use for searching.
		var pos = (EditorRateAlteringEvent)EditorEvent.CreateEvent(
			EventConfig.CreateSearchEventConfigWithOnlyTime(Chart, chartTime));

		var enumerator = FindGreatestPreceding(pos);
		if (enumerator != null && EnsureGreatestLessThanTime(enumerator, chartTime))
			return enumerator;
		enumerator = FindLeastFollowing(pos, true);
		if (enumerator != null)
			EnsureLeastGreaterThanOrEqualToTime(enumerator, chartTime);
		return enumerator;
	}

	/// <summary>
	/// Find the EditorRateAlteringEvent that is the greatest event which precedes the given chart position.
	/// If no EditorRateAlteringEvent precedes the given chart position, instead find the EditorRateAlteringEvent
	/// that is the least event which follows or is equal to the given chart position.
	/// </summary>
	/// <returns>Enumerator to best EditorRateAlteringEvent or null if a value could not be found.</returns>
	public IReadOnlyRedBlackTree<EditorRateAlteringEvent>.IReadOnlyRedBlackTreeEnumerator FindBestByPosition(double chartPosition)
	{
		// Set up a dummy event to use for searching.
		var pos = (EditorRateAlteringEvent)EditorEvent.CreateEvent(
			EventConfig.CreateSearchEventConfigWithOnlyRow(Chart, chartPosition));

		var enumerator = FindGreatestPreceding(pos);
		if (enumerator != null && EnsureGreatestLessThanPosition(enumerator, chartPosition))
			return enumerator;
		enumerator = FindLeastFollowing(pos, true);
		if (enumerator != null)
			EnsureLeastGreaterThanOrEqualToPosition(enumerator, chartPosition);
		return enumerator;
	}

	/// <summary>
	/// Finds the active EditorRateAlteringEvent for the given EditorEvent.
	/// Prefer this method over FindActiveRateAlteringEventForTime and FindActiveRateAlteringEventForPosition
	/// as some rate altering events on the same row as other events occur before those events and others
	/// occur after.
	/// </summary>
	/// <param name="editorEvent">EditorEvent in question.</param>
	/// <returns>The active EditorRateAlteringEvent or null if none could be found.</returns>
	public EditorRateAlteringEvent FindActiveRateAlteringEvent(EditorEvent editorEvent)
	{
		if (FindGreatestPrecedingValue(editorEvent, true, out var activeEvent))
			return activeEvent;
		FirstValue(out activeEvent);
		return activeEvent;
	}

	/// <summary>
	/// Finds the active EditorRateAlteringEvent for the given Stepmania Event.
	/// Prefer this method over FindActiveRateAlteringEventForTime and FindActiveRateAlteringEventForPosition
	/// as some rate altering events on the same row as other events occur before those events and others
	/// occur after.
	/// </summary>
	/// <param name="smEvent">Event in question.</param>
	/// <returns>The active EditorRateAlteringEvent or null if none could be found.</returns>
	public EditorRateAlteringEvent FindActiveRateAlteringEvent(Event smEvent)
	{
		var enumerator = FindGreatestPreceding(smEvent, EditorEvent.CompareEditorEventToSmEvent, true);
		if (enumerator != null && EnsureGreatestLessThanOrEqualTo(enumerator, smEvent))
		{
			enumerator.MoveNext();
			return enumerator.Current;
		}

		FirstValue(out var editorEvent);
		return editorEvent;
	}

	/// <summary>
	/// Finds the active EditorRateAlteringEvent enumerator for the given chart time.
	/// This method is suitable when a chart time is known and it is not for an EditorEvent or Stepmania Event.
	/// If an EditorEvent or Stepmania Event is known, prefer FindActiveRateAlteringEvent as event sorting
	/// may result in certain events falling before or afters at the same row.
	/// </summary>
	/// <param name="chartTime">Chart time in question.</param>
	/// <param name="allowEqualTo">
	/// If true, also consider an event to be active it occurs at the same time as the given chart time.
	/// </param>
	/// <returns>Enumerator to the active EditorRateAlteringEvent or null if none could be found.</returns>
	public IReadOnlyRedBlackTree<EditorRateAlteringEvent>.IReadOnlyRedBlackTreeEnumerator
		FindActiveRateAlteringEventEnumeratorForTime(
			double chartTime, bool allowEqualTo = true)
	{
		var pos = (EditorRateAlteringEvent)EditorEvent.CreateEvent(
			EventConfig.CreateSearchEventConfigWithOnlyTime(Chart, chartTime));

		var enumerator = FindGreatestPreceding(pos, allowEqualTo);
		if (enumerator != null && (allowEqualTo
			    ? EnsureGreatestLessThanOrEqualToTime(enumerator, chartTime)
			    : EnsureGreatestLessThanTime(enumerator, chartTime)))
		{
			return enumerator;
		}

		return First();
	}

	/// <summary>
	/// Finds the active EditorRateAlteringEvent for the given chart time.
	/// This method is suitable when a chart time is known and it is not for an EditorEvent or Stepmania Event.
	/// If an EditorEvent or Stepmania Event is known, prefer FindActiveRateAlteringEvent as event sorting
	/// may result in certain events falling before or afters at the same row.
	/// </summary>
	/// <param name="chartTime">Chart time in question.</param>
	/// <param name="allowEqualTo">
	/// If true, also consider an event to be active it occurs at the same time as the given chart time.
	/// </param>
	/// <returns>The active EditorRateAlteringEvent or null if none could be found.</returns>
	public EditorRateAlteringEvent FindActiveRateAlteringEventForTime(double chartTime, bool allowEqualTo = true)
	{
		var pos = (EditorRateAlteringEvent)EditorEvent.CreateEvent(
			EventConfig.CreateSearchEventConfigWithOnlyTime(Chart, chartTime));

		var enumerator = FindGreatestPreceding(pos, allowEqualTo);
		if (enumerator != null && (allowEqualTo
			    ? EnsureGreatestLessThanOrEqualToTime(enumerator, chartTime)
			    : EnsureGreatestLessThanTime(enumerator, chartTime)))
		{
			enumerator.MoveNext();
			return enumerator.Current;
		}

		FirstValue(out var editorEvent);
		return editorEvent;
	}

	/// <summary>
	/// Finds the active EditorRateAlteringEvent enumerator for the given position.
	/// This method is suitable when a position is known and it is not for an EditorEvent or Stepmania Event.
	/// If an EditorEvent or Stepmania Event is known, prefer FindActiveRateAlteringEvent as event sorting
	/// may result in certain events falling before or afters at the same row.
	/// </summary>
	/// <param name="chartPosition">Position in question.</param>
	/// <param name="allowEqualTo">
	/// If true, also consider an event to be active it occurs at the same position as the given position.
	/// </param>
	/// <returns>Enumerator to the active EditorRateAlteringEvent or null if none could be found.</returns>
	public IReadOnlyRedBlackTree<EditorRateAlteringEvent>.IReadOnlyRedBlackTreeEnumerator
		FindActiveRateAlteringEventEnumeratorForPosition(
			double chartPosition, bool allowEqualTo = true)
	{
		var pos = (EditorRateAlteringEvent)EditorEvent.CreateEvent(
			EventConfig.CreateSearchEventConfigWithOnlyRow(Chart, chartPosition));
		var enumerator = FindGreatestPreceding(pos, allowEqualTo);
		if (enumerator != null && (allowEqualTo
			    ? EnsureGreatestLessThanOrEqualToPosition(enumerator, chartPosition)
			    : EnsureGreatestLessThanPosition(enumerator, chartPosition)))
		{
			return enumerator;
		}

		return First();
	}

	/// <summary>
	/// Finds the active EditorRateAlteringEvent for the given position.
	/// This method is suitable when a position is known and it is not for an EditorEvent or Stepmania Event.
	/// If an EditorEvent or Stepmania Event is known, prefer FindActiveRateAlteringEvent as event sorting
	/// may result in certain events falling before or afters at the same row.
	/// </summary>
	/// <param name="chartPosition">Position in question.</param>
	/// <param name="allowEqualTo">
	/// If true, also consider an event to be active it occurs at the same position as the given position.
	/// </param>
	/// <returns>The active EditorRateAlteringEvent or null if none could be found.</returns>
	public EditorRateAlteringEvent FindActiveRateAlteringEventForPosition(double chartPosition, bool allowEqualTo = true)
	{
		var pos = (EditorRateAlteringEvent)EditorEvent.CreateEvent(
			EventConfig.CreateSearchEventConfigWithOnlyRow(Chart, chartPosition));
		var enumerator = FindGreatestPreceding(pos, allowEqualTo);
		if (enumerator != null && (allowEqualTo
			    ? EnsureGreatestLessThanOrEqualToPosition(enumerator, chartPosition)
			    : EnsureGreatestLessThanPosition(enumerator, chartPosition)))
		{
			enumerator.MoveNext();
			return enumerator!.Current;
		}

		FirstValue(out var editorEvent);
		return editorEvent;
	}

	/// <summary>
	/// Finds an EditorRateAlteringEvent of a given type at a specific row.
	/// </summary>
	/// <typeparam name="TEvent">Type of EditorRateAlteringEvent to find.</typeparam>
	/// <param name="row">Row of EditorRateAlteringEvent to find.</param>
	/// <returns>The found EditorRateAlteringEvent or null if non exists for given inputs.</returns>
	public TEvent FindEventAtRow<TEvent>(int row) where TEvent : EditorRateAlteringEvent
	{
		var pos = (EditorRateAlteringEvent)EditorEvent.CreateEvent(
			EventConfig.CreateSearchEventConfigWithOnlyRow(Chart, row));
		var enumerator = FindGreatestPreceding(pos);
		if (enumerator == null || !EnsureGreatestLessThanPosition(enumerator, row))
		{
			enumerator = First();
			if (enumerator == null)
				return null;
		}
		while (enumerator.MoveNext() && enumerator.Current!.GetRow() <= row)
		{
			if (enumerator.Current.GetRow() > row)
				break;
			if (enumerator.Current.GetRow() < row)
				continue;
			if (enumerator.Current.GetType() == typeof(TEvent))
				return (TEvent)enumerator.Current;
		}
		return null;
	}
}

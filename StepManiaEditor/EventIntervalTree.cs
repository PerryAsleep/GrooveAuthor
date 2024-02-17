using Fumen;

namespace StepManiaEditor;

/// <summary>
/// Read-only interface for specialization of IntervalTree on EditorEvents
/// with additional methods for performing common Editor-related actions.
/// </summary>
internal interface IReadOnlyEventIntervalTree<TValue> : IReadOnlyIntervalTree<double, TValue>
{
	IReadOnlyIntervalTreeEnumerator FindBestByPosition(double position);
	TValue FindPreviousEventWithLooping(double chartPosition);
	TValue FindNextEventWithLooping(double chartPosition);
}

/// <summary>
/// Specialization of IntervalTree on EditorEvents with additional
/// methods for performing common Editor-related actions.
/// </summary>
internal class EventIntervalTree<TValue> : IntervalTree<double, TValue>, IReadOnlyEventIntervalTree<TValue>
	where TValue : EditorEvent
{
	/// <summary>
	/// Find the EditorEvent that is the greatest event which precedes the given chart position.
	/// If no EditorEvent precedes the given chart position, instead find the EditorEvent that
	/// is the least event which follows or is equal to the given chart position.
	/// </summary>
	/// <returns>Enumerator to best value or null if a value could not be found.</returns>
	public IReadOnlyIntervalTree<double, TValue>.IReadOnlyIntervalTreeEnumerator FindBestByPosition(double position)
	{
		var enumerator = FindGreatestPreceding(position) ?? FindLeastFollowing(position, true);
		return enumerator;
	}

	public TValue FindPreviousEventWithLooping(double position)
	{
		if (GetCount() == 0)
			return null;

		var enumerator = FindGreatestPreceding(position);
		if (enumerator != null && enumerator.MoveNext() && enumerator.IsCurrentValid())
			return enumerator.Current;

		enumerator = Last();
		if (enumerator != null && enumerator.MoveNext() && enumerator.IsCurrentValid())
			return enumerator.Current;

		return null;
	}

	public TValue FindNextEventWithLooping(double position)
	{
		if (GetCount() == 0)
			return null;

		var enumerator = FindLeastFollowing(position);
		if (enumerator != null && enumerator.MoveNext() && enumerator.IsCurrentValid())
			return enumerator.Current;

		enumerator = First();
		if (enumerator != null && enumerator.MoveNext() && enumerator.IsCurrentValid())
			return enumerator.Current;

		return null;
	}
}

using Fumen;

namespace StepManiaEditor;

/// <summary>
/// Specialization of RedBlackTree on EditorRateAlteringEvents with additional
/// methods for performing searches for events based on chart time
/// and chart position.
/// </summary>
internal class RateAlteringEventTree : RedBlackTree<EditorRateAlteringEvent>
{
	private readonly EditorChart Chart;

	public RateAlteringEventTree(EditorChart chart)
	{
		Chart = chart;
	}

	public IRedBlackTreeEnumerator FindBest(EditorPosition p)
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
	public IRedBlackTreeEnumerator FindBestByTime(double chartTime)
	{
		// Set up a dummy event to use for searching.
		var pos = (EditorRateAlteringEvent)EditorEvent.CreateEvent(
			EventConfig.CreateSearchEventConfigWithOnlyTime(Chart, chartTime));

		var enumerator = FindGreatestPreceding(pos);
		if (enumerator != null && EnsureLessThanTime(enumerator, chartTime))
			return enumerator;
		enumerator = FindLeastFollowing(pos, true);
		if (enumerator != null)
			EnsureGreaterThanOrEqualToTime(enumerator, chartTime);
		return enumerator;
	}

	/// <summary>
	/// Find the EditorRateAlteringEvent that is the greatest event which precedes the given chart position.
	/// If no EditorRateAlteringEvent precedes the given chart position, instead find the EditorRateAlteringEvent
	/// that is the least event which follows or is equal to the given chart position.
	/// </summary>
	/// <returns>Enumerator to best EditorRateAlteringEvent or null if a value could not be found.</returns>
	public IRedBlackTreeEnumerator FindBestByPosition(double chartPosition)
	{
		// Set up a dummy event to use for searching.
		var pos = (EditorRateAlteringEvent)EditorEvent.CreateEvent(
			EventConfig.CreateSearchEventConfigWithOnlyRow(Chart, chartPosition));

		var enumerator = FindGreatestPreceding(pos);
		if (enumerator != null && EnsureLessThanPosition(enumerator, chartPosition))
			return enumerator;
		enumerator = FindLeastFollowing(pos, true);
		if (enumerator != null)
			EnsureGreaterThanOrEqualToPosition(enumerator, chartPosition);
		return enumerator;
	}

	// ReSharper disable UnusedMember.Local
	private static bool EnsureLessThanTime(IRedBlackTreeEnumerator e, double chartTime)
	{
		while (e.MoveNext() && e.Current!.GetChartTime() < chartTime)
		{
		}

		while (e.MovePrev() && e.Current!.GetChartTime() >= chartTime)
		{
		}

		return UnsetAndReturnIfWasValid(e);
	}

	private static bool EnsureLessThanOrEqualToTime(IRedBlackTreeEnumerator e, double chartTime)
	{
		while (e.MoveNext() && e.Current!.GetChartTime() <= chartTime)
		{
		}

		while (e.MovePrev() && e.Current!.GetChartTime() > chartTime)
		{
		}

		return UnsetAndReturnIfWasValid(e);
	}

	private static bool EnsureGreaterThanTime(IRedBlackTreeEnumerator e, double chartTime)
	{
		while (e.MovePrev() && e.Current!.GetChartTime() > chartTime)
		{
		}

		while (e.MoveNext() && e.Current!.GetChartTime() <= chartTime)
		{
		}

		return UnsetAndReturnIfWasValid(e);
	}

	private static bool EnsureGreaterThanOrEqualToTime(IRedBlackTreeEnumerator e, double chartTime)
	{
		while (e.MovePrev() && e.Current!.GetChartTime() >= chartTime)
		{
		}

		while (e.MoveNext() && e.Current!.GetChartTime() < chartTime)
		{
		}

		return UnsetAndReturnIfWasValid(e);
	}

	private static bool EnsureLessThanPosition(IRedBlackTreeEnumerator e, double chartPosition)
	{
		while (e.MoveNext() && e.Current!.GetChartPosition() < chartPosition)
		{
		}

		while (e.MovePrev() && e.Current!.GetChartPosition() >= chartPosition)
		{
		}

		return UnsetAndReturnIfWasValid(e);
	}

	private static bool EnsureLessThanOrEqualToPosition(IRedBlackTreeEnumerator e, double chartPosition)
	{
		while (e.MoveNext() && e.Current!.GetChartPosition() <= chartPosition)
		{
		}

		while (e.MovePrev() && e.Current!.GetChartPosition() > chartPosition)
		{
		}

		return UnsetAndReturnIfWasValid(e);
	}

	private static bool EnsureGreaterThanPosition(IRedBlackTreeEnumerator e, double chartPosition)
	{
		while (e.MovePrev() && e.Current!.GetChartPosition() > chartPosition)
		{
		}

		while (e.MoveNext() && e.Current!.GetChartPosition() <= chartPosition)
		{
		}

		return UnsetAndReturnIfWasValid(e);
	}

	private static bool EnsureGreaterThanOrEqualToPosition(IRedBlackTreeEnumerator e, double chartPosition)
	{
		while (e.MovePrev() && e.Current!.GetChartPosition() >= chartPosition)
		{
		}

		while (e.MoveNext() && e.Current!.GetChartPosition() < chartPosition)
		{
		}

		return UnsetAndReturnIfWasValid(e);
	}
	// ReSharper restore UnusedMember.Local

	private static bool UnsetAndReturnIfWasValid(IRedBlackTreeEnumerator e)
	{
		var ret = e.IsCurrentValid();
		e.Unset();
		return ret;
	}
}

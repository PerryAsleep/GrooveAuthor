using Fumen;

namespace StepManiaEditor
{
	/// <summary>
	/// Specialization of RedBlackTree on EditorRateAlteringEvents with additional
	/// methods for performing searches for events based on chart time
	/// and chart position.
	/// </summary>
	internal class RateAlteringEventTree : RedBlackTree<EditorRateAlteringEvent>
	{
		private EditorChart Chart;

		public RateAlteringEventTree(EditorChart chart)
		{
			Chart = chart;
		}

		public Enumerator FindBest(EditorPosition p)
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
		public Enumerator FindBestByTime(double chartTime)
		{
			// Set up a dummy event to use for searching.
			var pos = new EditorDummyRateAlteringEventWithTime(Chart, chartTime);
			
			var enumerator = FindGreatestPreceding(pos, false);
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
		/// that is the least event which follows or is equal to the given chart posisiton.
		/// </summary>
		/// <returns>Enumerator to best EditorRateAlteringEvent or null if a value could not be found.</returns>
		public Enumerator FindBestByPosition(double chartPosition)
		{
			// Set up a dummy event to use for searching.
			var pos = new EditorDummyRateAlteringEventWithRow(Chart, chartPosition);

			var enumerator = FindGreatestPreceding(pos, false);
			if (enumerator != null && EnsureLessThanPosition(enumerator, chartPosition))
				return enumerator;
			enumerator = FindLeastFollowing(pos, true);
			if (enumerator != null)
				EnsureGreaterThanOrEqualToPosition(enumerator, chartPosition);
			return enumerator;
		}

		private static bool EnsureLessThanTime(Enumerator e, double chartTime)
		{
			while (e.MoveNext() && e.Current.GetChartTime() < chartTime) { }
			while (e.MovePrev() && e.Current.GetChartTime() >= chartTime) { }
			return UnsetAndReturnIfWasValid(e);
		}
		private static bool EnsureLessThanOrEqualToTime(Enumerator e, double chartTime)
		{
			while (e.MoveNext() && e.Current.GetChartTime() <= chartTime) { }
			while (e.MovePrev() && e.Current.GetChartTime() > chartTime) { }
			return UnsetAndReturnIfWasValid(e);
		}
		private static bool EnsureGreaterThanTime(Enumerator e, double chartTime)
		{
			while (e.MovePrev() && e.Current.GetChartTime() > chartTime) { }
			while (e.MoveNext() && e.Current.GetChartTime() <= chartTime) { }
			return UnsetAndReturnIfWasValid(e);
		}
		private static bool EnsureGreaterThanOrEqualToTime(Enumerator e, double chartTime)
		{
			while (e.MovePrev() && e.Current.GetChartTime() >= chartTime) { }
			while (e.MoveNext() && e.Current.GetChartTime() < chartTime) { }
			return UnsetAndReturnIfWasValid(e);
		}
		private static bool EnsureLessThanPosition(Enumerator e, double chartPosition)
		{
			while (e.MoveNext() && e.Current.GetChartPosition() < chartPosition) { }
			while (e.MovePrev() && e.Current.GetChartPosition() >= chartPosition) { }
			return UnsetAndReturnIfWasValid(e);
		}
		private static bool EnsureLessThanOrEqualToPosition(Enumerator e, double chartPosition)
		{
			while (e.MoveNext() && e.Current.GetChartPosition() <= chartPosition) { }
			while (e.MovePrev() && e.Current.GetChartPosition() > chartPosition) { }
			return UnsetAndReturnIfWasValid(e);
		}
		private static bool EnsureGreaterThanPosition(Enumerator e, double chartPosition)
		{
			while (e.MovePrev() && e.Current.GetChartPosition() > chartPosition) { }
			while (e.MoveNext() && e.Current.GetChartPosition() <= chartPosition) { }
			return UnsetAndReturnIfWasValid(e);
		}
		private static bool EnsureGreaterThanOrEqualToPosition(Enumerator e, double chartPosition)
		{
			while (e.MovePrev() && e.Current.GetChartPosition() >= chartPosition) { }
			while (e.MoveNext() && e.Current.GetChartPosition() < chartPosition) { }
			return UnsetAndReturnIfWasValid(e);
		}

		private static bool UnsetAndReturnIfWasValid(Enumerator e)
		{
			var ret = e.IsCurrentValid();
			e.Unset();
			return ret;
		}
	}
}

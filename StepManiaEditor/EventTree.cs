using Fumen;
using static Fumen.Converters.SMCommon;
using System.Collections.Generic;

namespace StepManiaEditor
{
	/// <summary>
	/// Specialization of RedBlackTree on EditorEvents with additional
	/// methods for performing searches for events based on chart time
	/// and chart position.
	/// </summary>
	internal class EventTree : RedBlackTree<EditorEvent>
	{
		private EditorChart Chart;

		public EventTree(EditorChart chart)
		{
			Chart = chart;
		}

		/// <summary>
		/// Given a RedBlackTree<EditorEvent> and a value, find the greatest preceding value.
		/// If no value precedes the given value, instead find the least value that follows or is
		/// equal to the given value.
		/// </summary>
		/// <remarks>
		/// This is a common pattern when knowing a position or a time and wanting to find the first event to
		/// start enumerator over for rendering.
		/// </remarks>
		/// <remarks>Helper for UpdateChartEvents.</remarks>
		/// <returns>Enumerator to best value or null if a value could not be found.</returns>
		public Enumerator FindBest(double chartPosition)
		{
			var pos = EditorEvent.CreateDummyEvent(Chart, CreateDummyFirstEventForRow((int)chartPosition), chartPosition);
			var enumerator = FindGreatestPreceding(pos, false);
			if (enumerator == null)
				enumerator = FindLeastFollowing(pos, true);
			return enumerator;
		}

		public Enumerator FindFirstAfterChartTime(double chartTime)
		{
			// Events are sorted by row, so we need to convert the time to row to find an event.
			var chartPosition = 0.0;
			if (!Chart.TryGetChartPositionFromTime(chartTime, ref chartPosition))
				return null;

			// Find the greatest preceding or least following event by row.
			// Many rows may occur at the same time, so no matter how we choose an enumerator by row
			// we will need to check the time. Leverage the FindBest() logic, then check the time.
			var enumerator = FindBest(chartPosition);
			if (enumerator == null)
				return null;

			// The enumerator may be before or after the given time. If it is after the given time,
			// we can move backwards until we are before, then move forward once to get the appropriate event.
			while (enumerator.MovePrev() && enumerator.Current.GetChartTime() >= chartTime) { }
			while (enumerator.MoveNext() && enumerator.Current.GetChartTime() < chartTime) { }

			// Unset the enumerator so callers receive the enumerator in a state consistent with
			// other operations which return an enumerator.
			enumerator.Unset();
			return enumerator;
		}

		public Enumerator FindFirstAfterChartPosition(double chartPosition)
		{
			var pos = EditorEvent.CreateDummyEvent(Chart, CreateDummyFirstEventForRow((int)chartPosition), chartPosition);
			return FindLeastFollowing(pos, false);
		}

		public EditorEvent FindNoteAt(int row, int lane, bool ignoreNotesBeingEdited)
		{
			var pos = EditorEvent.CreateDummyEvent(Chart, CreateDummyFirstEventForRow(row), row);

			// Find the greatest preceding event.
			var best = FindGreatestPreceding(pos);
			if (best == null)
				return null;

			// Scan forward to the last note in the row to make sure we consider all notes this row.
			while (best.MoveNext())
			{
				if (best.Current.GetRow() > row)
				{
					best.MovePrev();
					break;
				}
			}
			if (best.Current == null)
				best.MovePrev();

			// Scan backwards finding a note in the given lane and row, or a hold
			// which starts before the given now but ends at or after it.
			do
			{
				if (best.Current.GetLane() != lane)
					continue;
				if (ignoreNotesBeingEdited && best.Current.IsBeingEdited())
					continue;
				if (best.Current.GetRow() == row)
					return best.Current;
				if (!(best.Current is EditorHoldStartNoteEvent hsn))
					return null;
				return hsn.GetHoldEndNote().GetRow() >= row ? best.Current : null;
			} while (best.MovePrev());

			return null;
		}

		public List<EditorEvent> FindEventsAtRow(int row)
		{
			var events = new List<EditorEvent>();
			var enumerator = FindBest(row);
			if (enumerator == null)
				return events;
			while (enumerator.MoveNext() && enumerator.Current.GetRow() <= row)
			{
				if (enumerator.Current.GetRow() == row)
					events.Add(enumerator.Current);
			}

			return events;
		}
	}
}

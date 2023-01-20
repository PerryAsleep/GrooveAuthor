using Fumen;
using static Fumen.Converters.SMCommon;
using System.Collections.Generic;
using static System.Diagnostics.Debug;
using System.Diagnostics;
using static StepManiaLibrary.Constants;

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

		/// <summary>
		/// Debug flag for checking the tree to ensure events are sorted as expected.
		/// When set, lists will be generated from the tree for easy debugger inspection.
		/// </summary>
		private bool DebugEditorEventSort = false;
		/// <summary>
		/// List just for looking the previous state of the sorted tree in the debugger.
		/// </summary>
		private List<EditorEvent> PreviousList = new List<EditorEvent>();

		public EventTree(EditorChart chart)
		{
			Chart = chart;
		}

		/// <summary>
		/// Find the EditorEvent that is the greatest event which precedes the given chart position.
		/// If no EditorEvent precedes the given chart position, instead find the EditorEvent that
		/// is the least event which follows or is equal to the given chart posisiton.
		/// </summary>
		/// <returns>Enumerator to best value or null if a value could not be found.</returns>
		public Enumerator FindBestByPosition(double chartPosition)
		{
			var pos = EditorEvent.CreateEvent(CreateDummyConfig(chartPosition));
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
			var enumerator = FindBestByPosition(chartPosition);
			if (enumerator == null)
				return null;

			EnsureGreaterThanTime(enumerator, chartTime);
			return enumerator;
		}

		public Enumerator FindFirstAfterChartPosition(double chartPosition)
		{
			var pos = EditorEvent.CreateEvent(CreateDummyConfig(chartPosition));
			var enumerator = FindLeastFollowing(pos, false);
			if (enumerator == null)
				return null;

			EnsureGreaterThanPosition(enumerator, chartPosition);
			return enumerator;
		}

		public EditorEvent FindNoteAt(int row, int lane, bool ignoreNotesBeingEdited)
		{
			var pos = EditorEvent.CreateEvent(CreateDummyConfig(row));

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
			var enumerator = FindBestByPosition(row);
			if (enumerator == null)
				return events;
			while (enumerator.MoveNext() && enumerator.Current.GetRow() <= row)
			{
				if (enumerator.Current.GetRow() == row)
					events.Add(enumerator.Current);
			}

			return events;
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
			while(enumerator != null && enumerator.MoveNext())
			{
				list.Add(enumerator.Current);
			}

			var previousRow = 0;
			var laneNotes = new EditorEvent[Chart.NumInputs];
			var eventsByTypeAtCurrentRow = new HashSet<System.Type>();
			for (int i = 0; i < list.Count; i++)
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

		private EditorEvent.EventConfig CreateDummyConfig(double chartPosition)
		{
			// The dummy event will not equal any other event in the tree when compared to it.
			return new EditorEvent.EventConfig
			{
				EditorChart = Chart,
				ChartEvent = CreateDummyFirstEventForRow((int)chartPosition),
				ChartPosition = chartPosition,
				UseDoubleChartPosition = true,
				IsDummyEvent = true
			};
		}

		private static void EnsureLessThanTime(Enumerator e, double chartTime)
		{
			while (e.MoveNext() && e.Current.GetChartTime() < chartTime) { }
			while (e.MovePrev() && e.Current.GetChartTime() >= chartTime) { }
			e.Unset();
		}
		private static void EnsureLessThanOrEqualToTime(Enumerator e, double chartTime)
		{
			while (e.MoveNext() && e.Current.GetChartTime() <= chartTime) { }
			while (e.MovePrev() && e.Current.GetChartTime() > chartTime) { }
			e.Unset();
		}
		private static void EnsureGreaterThanTime(Enumerator e, double chartTime)
		{
			while (e.MovePrev() && e.Current.GetChartTime() > chartTime) { }
			while (e.MoveNext() && e.Current.GetChartTime() <= chartTime) { }
			e.Unset();
		}
		private static void EnsureGreaterThanOrEqualToTime(Enumerator e, double chartTime)
		{
			while (e.MovePrev() && e.Current.GetChartTime() >= chartTime) { }
			while (e.MoveNext() && e.Current.GetChartTime() < chartTime) { }
			e.Unset();
		}
		private static void EnsureLessThanPosition(Enumerator e, double chartPosition)
		{
			while (e.MoveNext() && e.Current.GetChartPosition() < chartPosition) { }
			while (e.MovePrev() && e.Current.GetChartPosition() >= chartPosition) { }
			e.Unset();
		}
		private static void EnsureLessThanOrEqualToPosition(Enumerator e, double chartPosition)
		{
			while (e.MoveNext() && e.Current.GetChartPosition() <= chartPosition) { }
			while (e.MovePrev() && e.Current.GetChartPosition() > chartPosition) { }
			e.Unset();
		}
		private static void EnsureGreaterThanPosition(Enumerator e, double chartPosition)
		{
			while (e.MovePrev() && e.Current.GetChartPosition() > chartPosition) { }
			while (e.MoveNext() && e.Current.GetChartPosition() <= chartPosition) { }
			e.Unset();
		}
		private static void EnsureGreaterThanOrEqualToPosition(Enumerator e, double chartPosition)
		{
			while (e.MovePrev() && e.Current.GetChartPosition() >= chartPosition) { }
			while (e.MoveNext() && e.Current.GetChartPosition() < chartPosition) { }
			e.Unset();
		}
	}
}

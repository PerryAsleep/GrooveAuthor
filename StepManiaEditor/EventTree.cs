﻿using Fumen;
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
		/// Find the EditorEvent that is the greatest event which precedes the given chart position.
		/// If no EditorEvent precedes the given chart position, instead find the EditorEvent that
		/// is the least event which follows or is equal to the given chart posisiton.
		/// </summary>
		/// <returns>Enumerator to best value or null if a value could not be found.</returns>
		public Enumerator FindBestByPosition(double chartPosition)
		{
			// The dummy event will not equal any other event in the tree when compared to it.
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
			var enumerator = FindBestByPosition(chartPosition);
			if (enumerator == null)
				return null;

			EnsureGreaterThanTime(enumerator, chartTime);
			return enumerator;
		}

		public Enumerator FindFirstAfterChartPosition(double chartPosition)
		{
			var pos = EditorEvent.CreateDummyEvent(Chart, CreateDummyFirstEventForRow((int)chartPosition), chartPosition);
			var enumerator = FindLeastFollowing(pos, false);
			if (enumerator == null)
				return null;

			EnsureGreaterThanPosition(enumerator, chartPosition);
			return enumerator;
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
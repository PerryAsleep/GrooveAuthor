using System;
using Fumen;
using Fumen.ChartDefinition;

namespace StepManiaEditor.EditorEvents.Containers;

/// <summary>
/// Common utility methods for binary search trees of EditorEvents.
/// </summary>
internal sealed class EventTreeUtils
{
	#region RedBlackTree Utils

	public static bool EnsureGreatestLessThanTime<T>(
		IReadOnlyRedBlackTree<T>.IReadOnlyRedBlackTreeEnumerator e,
		double chartTime) where T : EditorEvent
	{
		while (e.MoveNext() && e.Current!.GetChartTime() < chartTime)
		{
		}

		while (e.MovePrev() && e.Current!.GetChartTime() >= chartTime)
		{
		}

		return UnsetAndReturnIfWasValid(e);
	}

	public static bool EnsureGreatestLessThanOrEqualToTime<T>(
		IReadOnlyRedBlackTree<T>.IReadOnlyRedBlackTreeEnumerator e,
		double chartTime) where T : EditorEvent
	{
		while (e.MoveNext() && e.Current!.GetChartTime() <= chartTime)
		{
		}

		while (e.MovePrev() && e.Current!.GetChartTime() > chartTime)
		{
		}

		return UnsetAndReturnIfWasValid(e);
	}

	public static bool EnsureLeastGreaterThanTime<T>(
		IReadOnlyRedBlackTree<T>.IReadOnlyRedBlackTreeEnumerator e,
		double chartTime) where T : EditorEvent
	{
		while (e.MovePrev() && e.Current!.GetChartTime() > chartTime)
		{
		}

		while (e.MoveNext() && e.Current!.GetChartTime() <= chartTime)
		{
		}

		return UnsetAndReturnIfWasValid(e);
	}

	public static bool EnsureLeastGreaterThanOrEqualToTime<T>(
		IReadOnlyRedBlackTree<T>.IReadOnlyRedBlackTreeEnumerator e,
		double chartTime) where T : EditorEvent
	{
		while (e.MovePrev() && e.Current!.GetChartTime() >= chartTime)
		{
		}

		while (e.MoveNext() && e.Current!.GetChartTime() < chartTime)
		{
		}

		return UnsetAndReturnIfWasValid(e);
	}

	public static bool EnsureGreatestLessThanPosition<T>(
		IReadOnlyRedBlackTree<T>.IReadOnlyRedBlackTreeEnumerator e,
		double chartPosition) where T : EditorEvent
	{
		while (e.MoveNext() && e.Current!.GetChartPosition() < chartPosition)
		{
		}

		while (e.MovePrev() && e.Current!.GetChartPosition() >= chartPosition)
		{
		}

		return UnsetAndReturnIfWasValid(e);
	}

	public static bool EnsureGreatestLessThanOrEqualToPosition<T>(
		IReadOnlyRedBlackTree<T>.IReadOnlyRedBlackTreeEnumerator e,
		double chartPosition) where T : EditorEvent
	{
		while (e.MoveNext() && e.Current!.GetChartPosition() <= chartPosition)
		{
		}

		while (e.MovePrev() && e.Current!.GetChartPosition() > chartPosition)
		{
		}

		return UnsetAndReturnIfWasValid(e);
	}

	public static bool EnsureLeastGreaterThanPosition<T>(
		IReadOnlyRedBlackTree<T>.IReadOnlyRedBlackTreeEnumerator e,
		double chartPosition) where T : EditorEvent
	{
		while (e.MovePrev() && e.Current!.GetChartPosition() > chartPosition)
		{
		}

		while (e.MoveNext() && e.Current!.GetChartPosition() <= chartPosition)
		{
		}

		return UnsetAndReturnIfWasValid(e);
	}

	public static bool EnsureLeastGreaterThanOrEqualToPosition<T>(
		IReadOnlyRedBlackTree<T>.IReadOnlyRedBlackTreeEnumerator e,
		double chartPosition) where T : EditorEvent
	{
		while (e.MovePrev() && e.Current!.GetChartPosition() >= chartPosition)
		{
		}

		while (e.MoveNext() && e.Current!.GetChartPosition() < chartPosition)
		{
		}

		return UnsetAndReturnIfWasValid(e);
	}

	public static bool EnsureGreatestLessThanOrEqualTo<T>(
		IReadOnlyRedBlackTree<T>.IReadOnlyRedBlackTreeEnumerator e,
		Event smEvent) where T : EditorEvent
	{
		while (e.MoveNext() && EditorEvent.CompareEditorEventToSmEvent(e.Current, smEvent) <= 0)
		{
		}

		while (e.MovePrev() && EditorEvent.CompareEditorEventToSmEvent(e.Current, smEvent) > 0)
		{
		}

		return UnsetAndReturnIfWasValid(e);
	}

	public static bool UnsetAndReturnIfWasValid<T>(
		IReadOnlyRedBlackTree<T>.IReadOnlyRedBlackTreeEnumerator e) where T : EditorEvent
	{
		var ret = e.IsCurrentValid();
		e.Unset();
		return ret;
	}

	#endregion RedBlackTree Utils

	#region IntervalTree Utils

	public static bool EnsureGreatestLessThanPosition<T>(
		IReadOnlyIntervalTree<double, T>.IReadOnlyIntervalTreeEnumerator e,
		double chartPosition) where T : EditorEvent, IEquatable<T>
	{
		while (e.MoveNext() && e.Current!.GetChartPosition() < chartPosition)
		{
		}

		while (e.MovePrev() && e.Current!.GetChartPosition() >= chartPosition)
		{
		}

		return UnsetAndReturnIfWasValid(e);
	}

	public static bool EnsureLeastGreaterThanOrEqualToPosition<T>(
		IReadOnlyIntervalTree<double, T>.IReadOnlyIntervalTreeEnumerator e,
		double chartPosition) where T : EditorEvent, IEquatable<T>
	{
		while (e.MovePrev() && e.Current!.GetChartPosition() >= chartPosition)
		{
		}

		while (e.MoveNext() && e.Current!.GetChartPosition() < chartPosition)
		{
		}

		return UnsetAndReturnIfWasValid(e);
	}

	public static bool UnsetAndReturnIfWasValid<T>
		(IReadOnlyIntervalTree<double, T>.IReadOnlyIntervalTreeEnumerator e) where T : EditorEvent, IEquatable<T>
	{
		var ret = e.IsCurrentValid();
		e.Unset();
		return ret;
	}

	#endregion IntervalTree Utils
}

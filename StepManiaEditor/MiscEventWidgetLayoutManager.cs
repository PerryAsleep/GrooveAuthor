using System;
using System.Collections.Generic;
using static StepManiaEditor.ImGuiUtils;

namespace StepManiaEditor;

/// <summary>
/// Class to help position the miscellaneous, non-note, EditorEvent widgets.
/// These widgets are adjacent to the chart and there may be more than one present for
/// a single row. This class helps sort them per row and reposition them as needed.
///
/// Expected Usage:
///  Call BeginFrame at the start of each frame.
///  Call PositionEvent once per each visible miscellaneous EditorEvent per frame.
/// </summary>
internal sealed class MiscEventWidgetLayoutManager
{
	/// <summary>
	/// Ordered list of all the types of events to the left of the chart.
	/// The first type is the right-most type and the last type is the left-most.
	/// </summary>
	private static readonly List<Type> LeftTypes;

	/// <summary>
	/// Ordered list of all the types of events to the right of the chart.
	/// The first type is the left-most type and the last type is the right-most.
	/// </summary>
	private static readonly List<Type> RightTypes;

	/// <summary>
	/// WidgetData for every type of EditorEvent this class manages.
	/// </summary>
	private static readonly Dictionary<Type, WidgetData> Data;

	/// <summary>
	/// EditorEvents being positioned on the left.
	/// </summary>
	private readonly Dictionary<double, Dictionary<Type, EditorEvent>> CurrentFrameLeftEvents = new();

	/// <summary>
	/// EditorEvents being positioned on the right.
	/// </summary>
	private readonly Dictionary<double, Dictionary<Type, EditorEvent>> CurrentFrameRightEvents = new();

	public static readonly int ElementPadding = UiScaled(2);

	private double LeftAnchorPos;
	private double RightAnchorPos;
	private int MaxWidth;

	private class WidgetData
	{
		public int LeftOrder = -1;
		public int RightOrder = -1;
	}

	static MiscEventWidgetLayoutManager()
	{
		Data = new Dictionary<Type, WidgetData>
		{
			{ typeof(EditorTimeSignatureEvent), new WidgetData() },
			{ typeof(EditorTempoEvent), new WidgetData() },
			{ typeof(EditorStopEvent), new WidgetData() },
			{ typeof(EditorDelayEvent), new WidgetData() },
			{ typeof(EditorWarpEvent), new WidgetData() },
			{ typeof(EditorScrollRateEvent), new WidgetData() },
			{ typeof(EditorInterpolatedRateAlteringEvent), new WidgetData() },
			{ typeof(EditorTickCountEvent), new WidgetData() },
			{ typeof(EditorMultipliersEvent), new WidgetData() },
			{ typeof(EditorFakeSegmentEvent), new WidgetData() },
			{ typeof(EditorLabelEvent), new WidgetData() },
			{ typeof(EditorPatternEvent), new WidgetData() },
			{ typeof(EditorPreviewRegionEvent), new WidgetData() },
			{ typeof(EditorLastSecondHintEvent), new WidgetData() },
		};

		LeftTypes =
		[
			typeof(EditorTimeSignatureEvent),
			typeof(EditorStopEvent),
			typeof(EditorDelayEvent),
			typeof(EditorWarpEvent),
			typeof(EditorTickCountEvent),
			typeof(EditorMultipliersEvent),
			typeof(EditorLabelEvent),
		];
		RightTypes =
		[
			typeof(EditorPreviewRegionEvent),
			typeof(EditorLastSecondHintEvent),
			typeof(EditorPatternEvent),
			typeof(EditorTempoEvent),
			typeof(EditorScrollRateEvent),
			typeof(EditorInterpolatedRateAlteringEvent),
			typeof(EditorFakeSegmentEvent),
		];

		for (var i = 0; i < LeftTypes.Count; i++)
			Data[LeftTypes[i]].LeftOrder = i;
		for (var i = 0; i < RightTypes.Count; i++)
			Data[RightTypes[i]].RightOrder = i;
	}

	public void BeginFrame(double leftAnchorPos, double rightAnchorPos, int maxWidth)
	{
		LeftAnchorPos = leftAnchorPos;
		RightAnchorPos = rightAnchorPos;
		MaxWidth = maxWidth;
		CurrentFrameLeftEvents.Clear();
		CurrentFrameRightEvents.Clear();
	}

	private double GetMinXForLeft()
	{
		return LeftAnchorPos - MaxWidth;
	}

	public double GetMaxYForSingleRow()
	{
		var numRows = Math.Max(LeftTypes.Count, RightTypes.Count);
		return numRows * ImGuiLayoutUtils.GetMiscEditorEventHeight() + (numRows - 1) * ElementPadding;
	}

	private (double, double) AdvanceLeft(EditorEvent eventToPosition, EditorEvent lastEvent)
	{
		// Set the position to be to the left of the last event.
		var x = lastEvent.X - eventToPosition.W - ElementPadding;
		var y = lastEvent.Y;

		// If this element would go beyond the max width, wrap it to a new line.
		if (x < GetMinXForLeft())
		{
			x = Math.Max(LeftAnchorPos - eventToPosition.W, GetMinXForLeft());
			y += eventToPosition.H + ElementPadding;
		}

		return (x, y);
	}

	private double GetMaxXForRight()
	{
		return RightAnchorPos + MaxWidth;
	}

	private (double, double) AdvanceRight(EditorEvent eventToPosition, EditorEvent lastEvent)
	{
		// Set the position to be to the right of the last event.
		var x = lastEvent.X + lastEvent.W + ElementPadding;
		var y = lastEvent.Y;

		// If this element would go beyond the max width, wrap it to a new line.
		if (x + eventToPosition.W > GetMaxXForRight())
		{
			x = Math.Min(RightAnchorPos, GetMaxXForRight() - eventToPosition.W);
			y += eventToPosition.H + ElementPadding;
		}

		return (x, y);
	}

	/// <summary>
	/// Positions the given EditorEvent by setting its X and Y values.
	/// Assumes that all EditorEvents being positioned through this class have their W values
	/// already set correctly.
	/// </summary>
	/// <param name="e">EditorEvent object to position.</param>
	/// <param name="rowY">Y position of the row of this event.</param>
	public void PositionEvent(EditorEvent e, double rowY)
	{
		// Get the current frame events for this position.
		var positionKey = Preferences.Instance.PreferencesScroll.SpacingMode == Editor.SpacingMode.ConstantTime
			? e.GetChartTime()
			: e.GetChartPosition();
		if (!CurrentFrameLeftEvents.TryGetValue(positionKey, out var leftEvents))
		{
			leftEvents = new Dictionary<Type, EditorEvent>();
			CurrentFrameLeftEvents[positionKey] = leftEvents;
		}

		if (!CurrentFrameRightEvents.TryGetValue(positionKey, out var rightEvents))
		{
			rightEvents = new Dictionary<Type, EditorEvent>();
			CurrentFrameRightEvents[positionKey] = rightEvents;
		}

		var t = e.GetType();
		var added = false;

		if (Data.TryGetValue(t, out var widgetData))
		{
			// Check for adding this event's widget to the left.
			var order = widgetData.LeftOrder;
			if (order >= 0)
			{
				var x = Math.Max(LeftAnchorPos - e.W, GetMinXForLeft());
				var y = rowY;
				EditorEvent lastEvent = null;

				var yMin = y;
				var yMax = y;
				for (var i = 0; i < LeftTypes.Count; i++)
				{
					if (!leftEvents.ContainsKey(LeftTypes[i]))
						continue;
					var currentEvent = leftEvents[LeftTypes[i]];

					// Shift this widget to the left of existing widgets on this row
					// which should precede it.
					if (i < order)
					{
						lastEvent = currentEvent;
						(x, y) = AdvanceLeft(e, lastEvent);

						yMin = Math.Min(lastEvent.Y, yMin);
						yMax = Math.Max(lastEvent.Y + lastEvent.H, yMax);
					}

					// Shift widgets after this widget further to the left.
					else if (i > order)
					{
						// Upon encountering the first widget to the left of the widget to be added,
						// add the widget.
						if (!added)
						{
							e.X = x;
							e.Y = y;
							leftEvents[t] = e;
							added = true;
							lastEvent = e;

							yMin = Math.Min(e.Y, yMin);
							yMax = Math.Max(e.Y + e.H, yMax);
						}

						// Shift existing widget further left.
						(x, y) = AdvanceLeft(currentEvent, lastEvent);
						currentEvent.X = x;
						currentEvent.Y = y;
						lastEvent = currentEvent;

						yMin = Math.Min(lastEvent.Y, yMin);
						yMax = Math.Max(lastEvent.Y + lastEvent.H, yMax);
					}
				}

				// Set position of this widget and record it.
				if (!added)
				{
					e.X = x;
					e.Y = y;
					leftEvents[t] = e;
					added = true;

					yMin = Math.Min(e.Y, yMin);
					yMax = Math.Max(e.Y + e.H, yMax);
				}

				// Update the Y positions of all events on this row to center them.
				var top = rowY - (yMax - yMin) * 0.5;
				foreach (var leftEvent in leftEvents)
				{
					leftEvent.Value.Y = leftEvent.Value.Y - yMin + top;
				}
			}

			// Check for adding this event's widget to the right.
			order = widgetData.RightOrder;
			if (order >= 0)
			{
				var x = Math.Min(RightAnchorPos, GetMaxXForRight() - e.W);
				var y = rowY;
				EditorEvent lastEvent = null;

				var yMin = y;
				var yMax = y;
				for (var i = 0; i < RightTypes.Count; i++)
				{
					if (!rightEvents.ContainsKey(RightTypes[i]))
						continue;
					var currentEvent = rightEvents[RightTypes[i]];

					// Shift this widget to the right of existing widgets on this row
					// which should precede it.
					if (i < order)
					{
						lastEvent = currentEvent;
						(x, y) = AdvanceRight(e, lastEvent);

						yMin = Math.Min(lastEvent.Y, yMin);
						yMax = Math.Max(lastEvent.Y + lastEvent.H, yMax);
					}

					// Shift widgets after this widget further to the right.
					else if (i > order)
					{
						// Upon encountering the first widget to the right of the widget to be added,
						// add the widget.
						if (!added)
						{
							e.X = x;
							e.Y = y;
							rightEvents[t] = e;
							added = true;
							lastEvent = e;

							yMin = Math.Min(e.Y, yMin);
							yMax = Math.Max(e.Y + e.H, yMax);
						}

						// Shift existing widget further right.
						(x, y) = AdvanceRight(currentEvent, lastEvent);
						currentEvent.X = x;
						currentEvent.Y = y;
						lastEvent = currentEvent;

						yMin = Math.Min(lastEvent.Y, yMin);
						yMax = Math.Max(lastEvent.Y + lastEvent.H, yMax);
					}
				}

				// Set position of this widget and record it.
				if (!added)
				{
					e.X = x;
					e.Y = y;
					rightEvents[t] = e;

					yMin = Math.Min(e.Y, yMin);
					yMax = Math.Max(e.Y + e.H, yMax);
				}

				// Update the Y positions of all events on this row to center them.
				var top = rowY - (yMax - yMin) * 0.5;
				foreach (var rightEvent in rightEvents)
				{
					rightEvent.Value.Y = rightEvent.Value.Y - yMin + top;
				}
			}
		}
	}
}

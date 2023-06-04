using System;
using System.Collections.Generic;
using static StepManiaEditor.ImGuiUtils;

namespace StepManiaEditor;

/// <summary>
/// Class to help position the miscellaneous, non-note, EditorEvent widgets.
/// These widgets are adjacent to the chart and there may be more than one present for
/// a single row. This class helps sort them per row and reposition as needed.
///
/// Expected Usage:
///  Call BeginFrame at the start of each frame.
///  Call PositionEvent once per each each visible miscellaneous EditorEvent per frame.
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
	private static readonly Dictionary<double, Dictionary<Type, EditorEvent>> CurrentFrameLeftEvents = new();

	/// <summary>
	/// EditorEvents being positioned on the right.
	/// </summary>
	private static readonly Dictionary<double, Dictionary<Type, EditorEvent>> CurrentFrameRightEvents = new();

	private static double LeftAnchorPos;
	private static double RightAnchorPos;

	private static readonly int ElementPadding = UiScaled(2);

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
			{ typeof(EditorPreviewRegionEvent), new WidgetData() },
			{ typeof(EditorLastSecondHintEvent), new WidgetData() },
		};

		LeftTypes = new List<Type>
		{
			typeof(EditorTimeSignatureEvent),
			typeof(EditorStopEvent),
			typeof(EditorDelayEvent),
			typeof(EditorWarpEvent),
			typeof(EditorTickCountEvent),
			typeof(EditorMultipliersEvent),
			typeof(EditorLabelEvent),
		};
		RightTypes = new List<Type>
		{
			typeof(EditorPreviewRegionEvent),
			typeof(EditorLastSecondHintEvent),
			typeof(EditorTempoEvent),
			typeof(EditorScrollRateEvent),
			typeof(EditorInterpolatedRateAlteringEvent),
			typeof(EditorFakeSegmentEvent),
		};

		for (var i = 0; i < LeftTypes.Count; i++)
			Data[LeftTypes[i]].LeftOrder = i;
		for (var i = 0; i < RightTypes.Count; i++)
			Data[RightTypes[i]].RightOrder = i;
	}

	public static void BeginFrame(double leftAnchorPos, double rightAnchorPos)
	{
		LeftAnchorPos = leftAnchorPos;
		RightAnchorPos = rightAnchorPos;
		CurrentFrameLeftEvents.Clear();
		CurrentFrameRightEvents.Clear();
	}

	/// <summary>
	/// Positions the given EditorEvent by setting its X and Y values.
	/// Assumes that all EditorEvents being positioned through this class have their W values
	/// already set correctly.
	/// </summary>
	/// <param name="e">EditorEvent object to position.</param>
	/// <param name="rowY">Y position of the row of this event.</param>
	public static void PositionEvent(EditorEvent e, double rowY)
	{
		if (PositionEvent(e))
			e.Y = rowY - ImGuiLayoutUtils.GetMiscEditorEventHeight() * 0.5;
	}

	/// <summary>
	/// Positions the given EditorEvent by setting its X value.
	/// Assumes that all EditorEvents being positioned through this class have their W values
	/// already set correctly.
	/// </summary>
	/// <param name="e">EditorEvent object to position.</param>
	public static bool PositionEvent(EditorEvent e)
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
				var x = LeftAnchorPos - e.W;
				for (var i = 0; i < LeftTypes.Count; i++)
				{
					if (!leftEvents.ContainsKey(LeftTypes[i]))
						continue;

					// Shift this widget to the left of existing widgets on this row
					// which should precede it.
					if (i < order)
						x -= leftEvents[LeftTypes[i]].W + ElementPadding;
					// Shift widgets after this widget further to the left.
					else if (i > order)
						leftEvents[LeftTypes[i]].X = leftEvents[LeftTypes[i]].X - (e.W + ElementPadding);
				}

				// Set position of this widget and record it.
				e.X = x;
				leftEvents[t] = e;
				added = true;
			}

			// Check for adding this event's widget to the right.
			order = widgetData.RightOrder;
			if (order >= 0)
			{
				var x = RightAnchorPos;
				for (var i = 0; i < RightTypes.Count; i++)
				{
					if (!rightEvents.ContainsKey(RightTypes[i]))
						continue;

					// Shift this widget to the right of existing widgets on this row
					// which should precede it.
					if (i < order)
						x += rightEvents[RightTypes[i]].W + ElementPadding;
					// Shift widgets after this widget further to the right.
					else if (i > order)
						rightEvents[RightTypes[i]].X = rightEvents[RightTypes[i]].X + (e.W + ElementPadding);
				}

				// Set position of this widget and record it.
				e.X = x;
				rightEvents[t] = e;
				added = true;
			}
		}

		return added;
	}
}

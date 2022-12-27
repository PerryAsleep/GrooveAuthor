using System;
using System.Collections.Generic;
using static StepManiaEditor.Utils;

namespace StepManiaEditor
{
	/// <summary>
	/// Class to help position the miscellaneous, non-note, IPlaceable widgets.
	/// These widgets are adjacent to the chart and there may be more than one present for
	/// a single row. This class helps sort them per row and reposition as needed.
	///
	/// Expected Usage:
	///  Call BeginFrame at the start of each frame.
	///  Call PositionEvent once per each each visible miscellaneous IPlaceable per frame.
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
		/// WidgetData for every type of IPlaceable object this class manages.
		/// </summary>
		private static readonly Dictionary<Type, WidgetData> Data;

		/// <summary>
		/// IPlaceable objects being positioned on the left.
		/// </summary>
		private static readonly Dictionary<double, Dictionary<Type, IPlaceable>> CurrentFrameLeftEvents = new Dictionary<double, Dictionary<Type, IPlaceable>>();
		/// <summary>
		/// IPlaceable objects being positioned on the right.
		/// </summary>
		private static readonly Dictionary<double, Dictionary<Type, IPlaceable>> CurrentFrameRightEvents = new Dictionary<double, Dictionary<Type, IPlaceable>>();

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
		/// Positions the given IPlaceable by setting its X and Y values.
		/// Assumes that all IPlaceables being positioned through this class have their W values
		/// already set correctly.
		/// </summary>
		/// <param name="e">IPlaceable object to position.</param>
		/// <param name="rowY">Y position of the row of this event.</param>
		public static void PositionEvent(IPlaceable e, double rowY)
		{
			// Adjust y so that the widget is centered in y.
			var y = rowY - ImGuiLayoutUtils.GetMiscEditorEventHeight() * 0.5;

			// Get the current frame events for this position.
			Dictionary<Type, IPlaceable> leftEvents;
			if (!CurrentFrameLeftEvents.TryGetValue(y, out leftEvents))
			{
				leftEvents = new Dictionary<Type, IPlaceable>();
				CurrentFrameLeftEvents[y] = leftEvents;
			}
			Dictionary<Type, IPlaceable> rightEvents;
			if (!CurrentFrameRightEvents.TryGetValue(y, out rightEvents))
			{
				rightEvents = new Dictionary<Type, IPlaceable>();
				CurrentFrameRightEvents[y] = rightEvents;
			}

			var t = e.GetType();
			
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
							x -= (leftEvents[LeftTypes[i]].W + ElementPadding);
						// Shift widgets after this widget further to the left.
						else if (i > order)
							leftEvents[LeftTypes[i]].X = leftEvents[LeftTypes[i]].X - (e.W + ElementPadding);
					}

					// Set position of this widget and record it.
					e.X = x;
					e.Y = y;
					leftEvents[t] = e;
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
							x += (rightEvents[RightTypes[i]].W + ElementPadding);
						// Shift widgets after this widget further to the right.
						else if (i > order)
							rightEvents[RightTypes[i]].X = rightEvents[RightTypes[i]].X + (e.W + ElementPadding);
					}

					// Set position of this widget and record it.
					e.X = x;
					e.Y = y;
					rightEvents[t] = e;
				}
			}
		}
	}
}

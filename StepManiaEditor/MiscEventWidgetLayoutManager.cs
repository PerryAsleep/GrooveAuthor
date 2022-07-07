using System;
using System.Collections.Generic;

namespace StepManiaEditor
{
	/// <summary>
	/// Class to help position the miscellaneous, non-note, EditorEvent widgets.
	/// These widgets are adjacent to the chart and there may be more than one present for
	/// a single row. This class helps sort them per row and reposition as needed.
	///
	/// Expected Usage:
	///  Call BeginFrame at the start of each frame.
	///  Call PositionEvent once per each each visible miscellaneous EditorEvent per frame.
	/// </summary>
	public class MiscEventWidgetLayoutManager
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
		/// EditorEvents on the current row being position on the left.
		/// </summary>
		private static readonly Dictionary<Type, EditorEvent> CurrentLeftEvents = new Dictionary<Type, EditorEvent>();
		/// <summary>
		/// EditorEvents on the current row being position on the right.
		/// </summary>
		private static readonly Dictionary<Type, EditorEvent> CurrentRightEvents = new Dictionary<Type, EditorEvent>();

		private static int LastRow;
		private static double LeftAnchorPos;
		private static double RightAnchorPos;

		private const int ElementPadding = 2;

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
			};

			LeftTypes = new List<Type>
			{
				typeof(EditorTimeSignatureEvent),
				typeof(EditorStopEvent),
				typeof(EditorDelayEvent),
				typeof(EditorWarpEvent),
			};
			RightTypes = new List<Type>
			{
				typeof(EditorTempoEvent),
				typeof(EditorScrollRateEvent),
				typeof(EditorInterpolatedRateAlteringEvent),
			};


			for (var i = 0; i < LeftTypes.Count; i++)
				Data[LeftTypes[i]].LeftOrder = i;
			for (var i = 0; i < RightTypes.Count; i++)
				Data[RightTypes[i]].RightOrder = i;
		}

		public static void BeginFrame(double leftAnchorPos, double rightAnchorPos)
		{
			LastRow = 0;
			LeftAnchorPos = leftAnchorPos;
			RightAnchorPos = rightAnchorPos;
			CurrentLeftEvents.Clear();
			CurrentRightEvents.Clear();
		}

		/// <summary>
		/// Positions the given EditorEvent by setting its X and Y values.
		/// Assumes that all EditorEvents being position through this class have their W values
		/// already set correctly.
		/// </summary>
		/// <param name="e">EditorEvent to position.</param>
		/// <param name="rowY">Y position of the row of this event.</param>
		public static void PositionEvent(EditorEvent e, double rowY)
		{
			// Adjust y so that the widget is centered in y.
			var y = rowY - ImGuiLayoutUtils.GetMiscEditorEventHeight(true) * 0.5;

			// If this event is for a new row, clear the event lists so we no longer
			// reposition anything
			if (e.GetRow() != LastRow)
			{
				CurrentLeftEvents.Clear();
				CurrentRightEvents.Clear();
			}

			var t = e.GetType();

			// Check for adding this event's widget to the left.
			var order = Data[t].LeftOrder;
			if (order >= 0)
			{
				var x = LeftAnchorPos - e.GetW();
				for (var i = 0; i < LeftTypes.Count; i++)
				{
					if (!CurrentLeftEvents.ContainsKey(LeftTypes[i]))
						continue;
					
					// Shift this widget to the left of existing widgets on this row
					// which should precede it.
					if (i < order)
						x -= (CurrentLeftEvents[LeftTypes[i]].GetW() + ElementPadding);
					// Shift widgets after this widget further to the left.
					else if (i > order)
						CurrentLeftEvents[LeftTypes[i]].SetX(CurrentLeftEvents[LeftTypes[i]].GetX() - (e.GetW() + ElementPadding));
				}

				// Set position of this widget and record it.
				e.SetPosition(x, y);
				CurrentLeftEvents[t] = e;
			}

			// Check for adding this event's widget to the right.
			order = Data[t].RightOrder;
			if (order >= 0)
			{
				var x = RightAnchorPos;
				for (var i = 0; i < RightTypes.Count; i++)
				{
					if (!CurrentRightEvents.ContainsKey(RightTypes[i]))
						continue;

					// Shift this widget to the right of existing widgets on this row
					// which should precede it.
					if (i < order)
						x += (CurrentRightEvents[RightTypes[i]].GetW() + ElementPadding);
					// Shift widgets after this widget further to the right.
					else if (i > order)
						CurrentRightEvents[RightTypes[i]].SetX(CurrentRightEvents[RightTypes[i]].GetX() + (e.GetW() + ElementPadding));
				}

				// Set position of this widget and record it.
				e.SetPosition(x, y);
				CurrentRightEvents[t] = e;
			}

			LastRow = e.GetRow();
		}
	}
}

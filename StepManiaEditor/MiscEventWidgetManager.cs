using System;
using System.Collections.Generic;
using System.Text;

namespace StepManiaEditor
{
	public class MiscEventWidgetManager
	{
		private static List<Type> LeftTypes;
		private static List<Type> RightTypes;
		private static Dictionary<Type, WidgetData> Data = new Dictionary<Type, WidgetData>();

		private static Dictionary<Type, EditorEvent> CurrentLeftEvents = new Dictionary<Type, EditorEvent>();
		private static Dictionary<Type, EditorEvent> CurrentRightEvents = new Dictionary<Type, EditorEvent>();

		private static int LastRow;
		private static double LeftAnchorPos;
		private static double RightAnchorPos;

		private class WidgetData
		{
			public int LeftOrder = -1;
			public int RightOrder = -1;
			public double Width;
		}

		static MiscEventWidgetManager()
		{
			Data = new Dictionary<Type, WidgetData>
			{
				{ typeof(EditorTimeSignatureEvent), new WidgetData { Width = 100} },
				{ typeof(EditorTempoEvent), new WidgetData { Width = 100} },
				{ typeof(EditorStopEvent), new WidgetData { Width = 100} },
				{ typeof(EditorDelayEvent), new WidgetData { Width = 100} },
				{ typeof(EditorWarpEvent), new WidgetData { Width = 100} },
				{ typeof(EditorScrollRateEvent), new WidgetData { Width = 100} },
				{ typeof(EditorInterpolatedRateAlteringEvent), new WidgetData { Width = 100} },
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
			{
				Data[LeftTypes[i]].LeftOrder = i;
			}
			for (var i = 0; i < RightTypes.Count; i++)
			{
				Data[RightTypes[i]].RightOrder = i;
			}
		}

		public static void BeginFrame(double leftAnchorPos, double rightAnchorPos)
		{
			LastRow = 0;
			LeftAnchorPos = leftAnchorPos;
			RightAnchorPos = rightAnchorPos;
			CurrentLeftEvents.Clear();
			CurrentRightEvents.Clear();
		}

		public static void PositionEvent(EditorEvent e, double y)
		{
			if (e.GetRow() != LastRow)
			{
				CurrentLeftEvents.Clear();
				CurrentRightEvents.Clear();
			}

			var t = e.GetType();
			var order = Data[t].LeftOrder;
			if (order >= 0)
			{
				var x = LeftAnchorPos - e.W;
				for (var i = 0; i < LeftTypes.Count; i++)
				{
					if (CurrentLeftEvents.ContainsKey(LeftTypes[i]))
					{
						if (i < order)
						{
							x -= CurrentLeftEvents[LeftTypes[i]].W;
						}
						else if (i > order)
						{
							CurrentLeftEvents[LeftTypes[i]].X -= e.W;
						}
					}
				}
				e.X = x;
				e.Y = y;
				e.W = Data[t].Width;
				CurrentLeftEvents[t] = e;
			}
			order = Data[t].RightOrder;
			if (order >= 0)
			{
				var x = RightAnchorPos;
				for (var i = 0; i < RightTypes.Count; i++)
				{
					if (CurrentRightEvents.ContainsKey(RightTypes[i]))
					{
						if (i < order)
						{
							x += CurrentRightEvents[RightTypes[i]].W;
						}
						else if (i > order)
						{
							CurrentRightEvents[RightTypes[i]].X += e.W;
						}
					}
				}
				e.X = x;
				e.Y = y;
				e.W = Data[t].Width;
				CurrentLeftEvents[t] = e;
			}

			LastRow = e.GetRow();
		}
	}
}

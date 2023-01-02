using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StepManiaEditor
{
	internal sealed class EditorLastSecondHintEvent : EditorEvent
	{
		public EditorLastSecondHintEvent(EditorChart editorChart, double chartPosition)
			: base(new EventConfig { EditorChart = editorChart, ChartPosition = chartPosition, UseDoubleChartPosition = true })
		{

		}

		public override bool IsMiscEvent() { return true; }
		public override bool IsSelectableWithoutModifiers() { return false; }
		public override bool IsSelectableWithModifiers() { return true; }
	}
}

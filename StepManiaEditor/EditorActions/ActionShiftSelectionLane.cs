using StepManiaLibrary;
using System.Collections.Generic;

namespace StepManiaEditor
{
	/// <summary>
	/// Action which shifts the lanes of selected notes to higher or lower indexes and optionally wraps around.
	/// </summary>
	internal sealed class ActionShiftSelectionLane : ActionTransformSelectionLanes
	{
		/// <summary>
		/// Whether to shift right or left.
		/// </summary>
		private bool Right;
		/// <summary>
		/// Whether to wrap around.
		/// </summary>
		private bool Wrap;

		public ActionShiftSelectionLane(Editor editor, EditorChart chart, IEnumerable<EditorEvent> events, bool right, bool wrap)
			: base(editor, chart, events)
		{
			Right = right;
			Wrap = wrap;
		}

		public override string ToString()
		{
			var dir = Right ? "Right" : "Left";
			return $"Shift Notes {dir}.";
		}

		protected override bool CanTransform(EditorEvent e, PadData padData)
		{
			if (e.GetLane() == Constants.InvalidArrowIndex)
				return false;
			return true;
		}

		protected override bool DoTransform(EditorEvent e, PadData padData)
		{
			return DoTransformInternal(e, Right);
		}

		private bool DoTransformInternal(EditorEvent e, bool right)
		{
			var lane = e.GetLane();
			if (right)
			{
				lane++;
				if (lane >= Chart.NumInputs)
				{
					if (!Wrap)
						return false;
					lane = 0;
				}
			}
			else
			{
				lane--;
				if (lane < 0)
				{
					if (!Wrap)
						return false;
					lane = Chart.NumInputs - 1;
				}
			}
			e.SetLane(lane);
			return true;
		}

		protected override void UndoTransform(EditorEvent e, PadData padData)
		{
			DoTransformInternal(e, !Right);
		}
	}
}

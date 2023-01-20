using StepManiaLibrary;
using System.Collections.Generic;

namespace StepManiaEditor
{
	/// <summary>
	/// Action which flips the lanes of selected notes.
	/// </summary>
	internal sealed class ActionFlipSelection : ActionTransformSelectionLanes
	{
		public ActionFlipSelection(Editor editor, EditorChart chart, IEnumerable<EditorEvent> events)
			: base(editor, chart, events)
		{
		}

		public override string ToString()
		{
			return $"Flip Notes.";
		}

		protected override bool CanTransform(EditorEvent e, PadData padData)
		{
			var lane = e.GetLane();
			if (lane == Constants.InvalidArrowIndex)
				return false;
			if (padData.ArrowData[lane].FlippedLane == Constants.InvalidArrowIndex)
				return false;
			return true;
		}

		protected override bool DoTransform(EditorEvent e, PadData padData)
		{
			e.SetLane(padData.ArrowData[e.GetLane()].FlippedLane);
			return true;
		}

		protected override void UndoTransform(EditorEvent e, PadData padData)
		{
			DoTransform(e, padData);
		}
	}
}

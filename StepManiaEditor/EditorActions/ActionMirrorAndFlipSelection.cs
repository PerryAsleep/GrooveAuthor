using StepManiaLibrary;
using System.Collections.Generic;

namespace StepManiaEditor
{
	/// <summary>
	/// Action which mirrors and flips the lanes of selected notes.
	/// </summary>
	internal sealed class ActionMirrorAndFlipSelection : ActionTransformSelectionLanes
	{
		public ActionMirrorAndFlipSelection(Editor editor, EditorChart chart, IEnumerable<EditorEvent> events)
			: base(editor, chart, events)
		{
		}

		public override string ToString()
		{
			return $"Mirror and Flip Notes.";
		}

		protected override bool CanTransform(EditorEvent e, PadData padData)
		{
			var lane = e.GetLane();
			if (lane == Constants.InvalidArrowIndex)
				return false;
			var transformedLane = padData.ArrowData[lane].MirroredLane;
			if (transformedLane == Constants.InvalidArrowIndex)
				return false;
			transformedLane = padData.ArrowData[transformedLane].FlippedLane;
			if (transformedLane == Constants.InvalidArrowIndex)
				return false;
			return true;
		}

		protected override bool DoTransform(EditorEvent e, PadData padData)
		{
			e.SetLane(padData.ArrowData[padData.ArrowData[e.GetLane()].MirroredLane].FlippedLane);
			return true;
		}

		protected override void UndoTransform(EditorEvent e, PadData padData)
		{
			DoTransform(e, padData);
		}
	}
}

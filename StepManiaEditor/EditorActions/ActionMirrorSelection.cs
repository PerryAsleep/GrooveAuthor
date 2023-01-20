using StepManiaLibrary;
using System.Collections.Generic;

namespace StepManiaEditor
{
	/// <summary>
	/// Action which mirrors the lanes of selected notes.
	/// </summary>
	internal sealed class ActionMirrorSelection : ActionTransformSelectionLanes
	{
		public ActionMirrorSelection(Editor editor, EditorChart chart, IEnumerable<EditorEvent> events)
			: base(editor, chart, events)
		{
		}

		public override string ToString()
		{
			return $"Mirror Notes.";
		}

		protected override bool CanTransform(EditorEvent e, PadData padData)
		{
			var lane = e.GetLane();
			if (lane == Constants.InvalidArrowIndex)
				return false;
			if (padData.ArrowData[lane].MirroredLane == Constants.InvalidArrowIndex)
				return false;
			return true;
		}

		protected override bool DoTransform(EditorEvent e, PadData padData)
		{
			e.SetLane(padData.ArrowData[e.GetLane()].MirroredLane);
			return true;
		}

		protected override void UndoTransform(EditorEvent e, PadData padData)
		{
			DoTransform(e, padData);
		}
	}
}

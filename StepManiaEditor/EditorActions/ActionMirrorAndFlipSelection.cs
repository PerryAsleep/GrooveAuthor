using StepManiaLibrary;
using System.Collections.Generic;

namespace StepManiaEditor;

/// <summary>
/// Action which mirrors and flips the lanes of selected notes.
/// </summary>
internal sealed class ActionMirrorAndFlipSelection : ActionTransformSelectionLanes
{
	public ActionMirrorAndFlipSelection(Editor editor, EditorChart chart, IEnumerable<EditorEvent> events)
		: base(editor, chart, events, CanTransform)
	{
	}

	public override string ToString()
	{
		return "Mirror and Flip Notes.";
	}

	/// <summary>
	/// Returns whether or not the given event can be transformed.
	/// This returns true if the transformation makes sense for the given event, even if
	/// it would have no effect. For example, mirroring an event makes sense for a lane
	/// note, even if there are an odd number of lanes and the event in question is in
	/// the middle lane and will end up in the same spot. Mirroring does not make sense
	/// for events which are not lane notes because mirroring only changes event lanes.
	/// </summary>
	/// <param name="e">Event to check.</param>
	/// <param name="padData">PadData for the event's chart.</param>
	/// <returns>Whether or not the given event can be transformed.</returns>
	private static bool CanTransform(EditorEvent e, PadData padData)
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

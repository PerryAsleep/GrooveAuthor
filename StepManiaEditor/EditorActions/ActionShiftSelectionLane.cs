using StepManiaLibrary;
using System.Collections.Generic;

namespace StepManiaEditor;

/// <summary>
/// Action which shifts the lanes of selected notes to higher or lower indexes and optionally wraps around.
/// </summary>
internal sealed class ActionShiftSelectionLane : ActionTransformSelectionLanes
{
	/// <summary>
	/// Whether to shift right or left.
	/// </summary>
	private readonly bool Right;

	/// <summary>
	/// Whether to wrap around.
	/// </summary>
	private readonly bool Wrap;

	public ActionShiftSelectionLane(Editor editor, EditorChart chart, IEnumerable<EditorEvent> events, bool right, bool wrap)
		: base(editor, chart, events, CanTransform)
	{
		Right = right;
		Wrap = wrap;
	}

	public override string ToString()
	{
		var dir = Right ? "Right" : "Left";
		return $"Shift Notes {dir}.";
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

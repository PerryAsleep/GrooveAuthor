using Fumen;
using Fumen.ChartDefinition;

namespace StepManiaEditor;

/// <summary>
/// Class for managing autoplay input on the active Receptors for a given EditorChart.
/// </summary>
internal sealed class AutoPlayer
{
	/// <summary>
	/// Tracked state per lane.
	/// Used for comparing previous update values to current values to issue autoplay input.
	/// </summary>
	private sealed class AutoPlayState
	{
		/// <summary>
		/// The next Event relevant for input.
		/// </summary>
		public Event NextEvent { get; private set; }

		/// <summary>
		/// The chart time of the next input event. If there is no next input it is 0.0.
		/// </summary>
		public double NextEventTime { get; private set; }

		/// <summary>
		/// Flag for whether or not this state is set to valid data or not. When it is not
		/// set to valid data we do not want to use for comparisons as it would result in
		/// autoplaying the lane incorrectly when playback begins.
		/// </summary>
		public bool IsUnset { get; private set; }

		public AutoPlayState()
		{
			Reset();
		}

		public void Reset()
		{
			IsUnset = true;
			NextEventTime = 0.0;
			NextEvent = null;
		}

		public void Update(double nextTime, Event nextEvent)
		{
			IsUnset = false;
			NextEvent = nextEvent;
			NextEventTime = nextTime;
		}

		public bool HasValidNextEvent()
		{
			return !IsUnset && NextEvent != null;
		}
	}

	private readonly AutoPlayState[] AutoPlayStates;
	private readonly Receptor[] Receptors;
	private readonly EditorChart ActiveChart;

	public AutoPlayer(EditorChart chart, Receptor[] receptors)
	{
		Receptors = receptors;
		ActiveChart = chart;
		AutoPlayStates = new AutoPlayState[ActiveChart.NumInputs];
		for (var i = 0; i < AutoPlayStates.Length; i++)
			AutoPlayStates[i] = new AutoPlayState();
	}

	/// <summary>
	/// Update autoplay with the given EditorPosition.
	/// </summary>
	/// <param name="position">The current position of the chart.</param>
	public void Update(IReadOnlyEditorPosition position)
	{
		if (ActiveChart == null || AutoPlayStates == null || Receptors == null)
			return;

		// Gather all the next input events for the current position.
		var nextInputs = GetNextInputs(position);

		// Check each lane.
		for (var lane = 0; lane < ActiveChart.NumInputs; lane++)
		{
			var nextEvent = nextInputs[lane];
			var nextEventTime = nextEvent?.TimeSeconds ?? 0.0;

			// If the previous call's next event row doesn't match the current next event row
			// then something has changed and we should update input.
			if (!AutoPlayStates[lane].NextEventTime.DoubleEquals(nextEventTime))
			{
				// Since we have already passed a note, we should offset any animations so they begin
				// as if they started at the precise moment the event passed.
				var timeDelta = AutoPlayStates[lane].IsUnset ? 0.0 : position.ChartTime - AutoPlayStates[lane].NextEventTime;

				// The new next event can be null at the end of the chart. We need to release any
				// held input in this case.
				if (nextEvent == null && Receptors[lane].IsAutoplayHeld())
				{
					Receptors[lane].OnAutoplayInputUp(timeDelta);
				}

				// Only process inputs if the last state is valid.
				// This helps ensure that when starting playing in the middle of a chart
				// we don't incorrectly show input immediately.
				if (AutoPlayStates[lane].HasValidNextEvent())
				{
					// If the event that just passed is a hold end, release input.
					if (AutoPlayStates[lane].NextEvent is LaneHoldEndNote)
					{
						if (!Receptors[lane].IsAutoplayHeld())
						{
							Receptors[lane].OnAutoplayInputDown(timeDelta);
						}

						Receptors[lane].OnAutoplayInputUp(timeDelta);

						// Warp edge case.
						// If we went from a hold end to another hold end, it means a hold
						// ended and started at the same time. Add an effect for stepping down
						// on the new hold.
						if (nextEvent is LaneHoldEndNote)
						{
							Receptors[lane].OnAutoplayInputDown(timeDelta);
						}
					}
					else if (AutoPlayStates[lane].NextEvent is LaneHoldStartNote)
					{
						Receptors[lane].OnAutoplayInputDown(timeDelta);

						// Warp edge case.
						// If the event following a hold start isn't a hold end it means the
						// hold ended and a new one started at the same time. Add an effect
						// for releasing the old hold.
						if (nextEvent is not LaneHoldEndNote)
						{
							Receptors[lane].OnAutoplayInputUp(timeDelta);
						}
					}
					else if (AutoPlayStates[lane].NextEvent is LaneTapNote)
					{
						Receptors[lane].OnAutoplayInputDown(timeDelta);

						// Warp edge case.
						// On taps we normally press and release. But if the next note is a hold
						// end note it means a hold started at the same time as the tap and we
						// should stay held down.
						if (nextEvent is not LaneHoldEndNote)
						{
							Receptors[lane].OnAutoplayInputUp(timeDelta);
						}
					}
				}

				// If the next event is a hold end (i.e. we are in a hold) and the currently
				// state is unset (i.e. we just started playback), then start input to
				// hold the note.
				else if (AutoPlayStates[lane].IsUnset && nextEvent is LaneHoldEndNote)
				{
					Receptors[lane].OnAutoplayInputDown(timeDelta);
				}
			}

			// Update the state for next time.
			AutoPlayStates[lane].Update(nextEventTime, nextEvent);
		}
	}

	/// <summary>
	/// Stop all autoplay input.
	/// </summary>
	public void Stop()
	{
		if (ActiveChart == null || AutoPlayStates == null || Receptors == null)
			return;

		for (var lane = 0; lane < ActiveChart.NumInputs; lane++)
		{
			AutoPlayStates[lane].Reset();
			Receptors[lane].OnAutoplayInputCancel();
		}
	}

	/// <summary>
	/// Given a chart position, returns the next Stepmania Event per lane that is relevant for
	/// simulating input. The results are returned as an array where the index is the lane
	/// and the element at each index is the Event. The events which are relevant for simulating
	/// input are taps (LaneTapNote), hold downs (LaneHoldStartNote) and hold releases
	/// (LaneHoldEndNote).
	/// </summary>
	/// <param name="position">The current position of the chart.</param>
	/// <returns>Array of next Events for input per lane.</returns>
	private Event[] GetNextInputs(IReadOnlyEditorPosition position)
	{
		var nextNotes = new Event[ActiveChart.NumInputs];
		for (var i = 0; i < ActiveChart.NumInputs; i++)
			nextNotes[i] = null;
		var numFound = 0;

		// First, scan backwards to find all holds which may be overlapping.
		// Holds may end after the given chart time which started before it.
		// We want to use time and not row for this because simulating input only
		// cares about time.
		var overlappingHolds = ActiveChart.GetHoldsOverlappingTime(position.ChartTime);
		for (var i = 0; i < ActiveChart.NumInputs; i++)
		{
			var hold = overlappingHolds[i];
			if (hold == null)
				continue;
			if (hold.GetChartTime() > position.ChartTime)
				nextNotes[i] = overlappingHolds[i].GetEvent();
			else
				nextNotes[i] = overlappingHolds[i].GetHoldEndEvent();
			numFound++;
		}

		// Scan forward until we have collected a note for every lane.
		var enumerator = ActiveChart.GetEvents().FindBestByTime(position.ChartTime);
		if (enumerator == null)
			return nextNotes;
		while (enumerator.MoveNext() && numFound < ActiveChart.NumInputs)
		{
			var c = enumerator.Current;

			if (c!.GetLane() == StepManiaLibrary.Constants.InvalidArrowIndex || nextNotes[c.GetLane()] != null)
				continue;
			if (c is not (EditorTapNoteEvent or EditorHoldNoteEvent or EditorLiftNoteEvent))
				continue;
			if (c.IsFake())
				continue;

			if (c.GetChartTime() < position.ChartTime && c.GetEndChartTime() >= position.ChartTime)
			{
				nextNotes[c.GetLane()] = c.GetAdditionalEvent();
				numFound++;
			}

			else if (c.GetChartTime() >= position.ChartTime)
			{
				nextNotes[c.GetLane()] = c.GetEvent();
				numFound++;
			}
		}

		return nextNotes;
	}
}

using Fumen.ChartDefinition;
using System.Linq;

namespace StepManiaEditor
{
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
			/// The next EditorEvent relevant for input. May be null in the case of releasing holds.
			/// </summary>
			public EditorEvent NextEvent { get; private set; }
			/// <summary>
			/// The row of the next input event. If there is no next input it is -1.
			/// </summary>
			public int NextEventRow { get; private set; }
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
				NextEventRow = -1;
				NextEventTime = 0.0;
				NextEvent = null;
			}

			public void Update(int nextRow, double nextTime, EditorEvent nextEvent)
			{
				IsUnset = false;
				NextEvent = nextEvent;
				NextEventRow = nextRow;
				NextEventTime = nextTime;
			}

			public bool HasValidNextEvent()
			{
				if (IsUnset || (NextEvent == null && NextEventRow < 0))
					return false;
				return true;
			}

			public bool IsNextEventHoldRelease()
			{
				return NextEvent == null && NextEventRow > 0;
			}
		}

		private AutoPlayState[] AutoPlayStates = null;
		private Receptor[] Receptors = null;
		private EditorChart ActiveChart = null;

		public AutoPlayer(EditorChart chart, Receptor[] receptors)
		{
			Receptors = receptors;
			ActiveChart = chart;
			AutoPlayStates = new AutoPlayState[ActiveChart.NumInputs];
			for(var i = 0; i < AutoPlayStates.Count(); i++)
				AutoPlayStates[i] = new AutoPlayState();
		}

		/// <summary>
		/// Update autoplay with the given EditorPostion.
		/// </summary>
		/// <param name="position">The current position of the chart.</param>
		public void Update(EditorPosition position)
		{
			if (ActiveChart == null || AutoPlayStates == null || Receptors == null)
				return;

			// Gather all the next input events for the current position.
			var nextInputs = ActiveChart.GetNextInputs(position.ChartPosition);

			// Check each lane.
			for (var lane = 0; lane < ActiveChart.NumInputs; lane++)
			{
				var nextEventRow = nextInputs[lane].Item1;
				var nextEvent = nextInputs[lane].Item2;
				var nextEventTime = 0.0;
				var nextEventIsHoldEnd = nextEvent == null && nextEventRow > 0;
				ActiveChart.TryGetTimeFromChartPosition(nextEventRow, ref nextEventTime);

				// If the previous call's next event row doesn't match the current next event row
				// then something as changed and we should update input.
				if (AutoPlayStates[lane].NextEventRow != nextEventRow)
				{
					// Since we have already passed a note, we should offset any animations so they begin
					// as if they started at the precise moment the event passed.
					var timeDelta = AutoPlayStates[lane].IsUnset ? 0.0 :
						position.ChartTime - AutoPlayStates[lane].NextEventTime;

					// The new next event can be null at the end of the chart. We need to release any
					// held input in this case.
					if (nextEventRow < 0 && Receptors[lane].IsAutoplayHeld())
					{
						Receptors[lane].OnAutoplayInputUp(timeDelta);
					}

					// Only process inputs if the last state is valid.
					// This helps ensure that when starting playing in the middle of a chart
					// we don't incorrectly show input immediately.
					if (AutoPlayStates[lane].HasValidNextEvent())
					{
						// If the event that just passed is a hold end, release input.
						if (AutoPlayStates[lane].IsNextEventHoldRelease())
						{
							Receptors[lane].OnAutoplayInputUp(timeDelta);
						}
						else
						{
							// For both taps an hold starts, press input.
							Receptors[lane].OnAutoplayInputDown(timeDelta);

							// For taps, release them immediately.
							if (AutoPlayStates[lane].NextEvent.GetFirstEvent() is LaneTapNote)
							{
								Receptors[lane].OnAutoplayInputUp(timeDelta);
							}
						}
					}

					// If the next event is a hold end (i.e. we are in a hold) and the currently
					// state is unset (i.e. we just started playback), then start input to
					// hold the note.
					else if (AutoPlayStates[lane].IsUnset && nextEventIsHoldEnd)
					{
						Receptors[lane].OnAutoplayInputDown(timeDelta);
					}
				}

				// Update the state for next time.
				AutoPlayStates[lane].Update(nextEventRow, nextEventTime, nextEvent);
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
	}
}

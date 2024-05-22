using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using Fumen;
using static Fumen.Converters.SMCommon;
using static StepManiaEditor.Utils;

namespace StepManiaEditor;

/// <summary>
/// StepDensity provides density-related information about an EditorChart including:
///  - Stream breakdown strings
///  - TODO: density visualization
/// Expected Usage:
///  Construct with EditorChart.
///  Call AddEvent/AddEvents when EditorEvents are added to the EditorChart.
///  Call DeleteEvent/DeleteEvents when EditorEvents are deleted from the EditorChart.
/// </summary>
internal sealed class StepDensity : Fumen.IObserver<PreferencesStream>, Fumen.IObserver<EditorChart>
{
	/// <summary>
	/// Data per measure.
	/// </summary>
	private class Measure
	{
		public double StartTime;
		public double EndTime;
		public int Steps;
	}

	/// <summary>
	/// EditorChart to provide density information of.
	/// </summary>
	private readonly EditorChart EditorChart;

	/// <summary>
	/// The number of measures by steps in the measure.
	/// </summary>
	private readonly int[] MeasuresByStepCount = new int[RowsPerMeasure];

	/// <summary>
	/// The greatest step count for any measure.
	/// </summary>
	private int GreatestStepCountPerMeasure;

	/// <summary>
	/// All Measures in the EditorChart.
	/// </summary>
	private readonly List<Measure> Measures = new();

	/// <summary>
	/// Stream breakdown represented as an IntervalTree.
	/// </summary>
	private IntervalTree<int, Tuple<int, int>> Streams = new();

	/// <summary>
	/// Constructor
	/// </summary>
	/// <param name="editorChart">EditorChart to provide density information of.</param>
	public StepDensity(EditorChart editorChart)
	{
		EditorChart = editorChart;
		Preferences.Instance.PreferencesStream.AddObserver(this);
		EditorChart.AddObserver(this);

		ResizeMeasures();
		RecomputeMeasureTiming();
		RecomputeStreams(true);
	}

	public int GetGreatestStepCountPerMeasure()
	{
		return GreatestStepCountPerMeasure;
	}

	/// <summary>
	/// Returns a string representation of the stream breakdown of the EditorChart.
	/// </summary>
	/// <returns>String representation of the stream breakdown of the EditorChart.</returns>
	public string GetStreamBreakdown()
	{
		var p = Preferences.Instance.PreferencesStream;
		var sb = new StringBuilder();
		var first = true;
		var previousStreamLastMeasure = 0;
		foreach (var stream in Streams)
		{
			var streamLength = stream.Item2 - stream.Item1 + 1;
			if (streamLength < p.MinimumLengthToConsiderStream)
				continue;

			if (!first)
			{
				var breakLength = stream.Item1 - (previousStreamLastMeasure + 1);
				if (breakLength <= p.ShortBreakCutoff)
				{
					sb.Append(p.ShortBreakCharacter);
				}
				else if (p.ShowBreakLengths)
				{
					sb.Append($" ({breakLength}) ");
				}
				else
				{
					sb.Append(p.LongBreakCharacter);
				}
			}

			sb.Append(streamLength);

			previousStreamLastMeasure = stream.Item2;
			first = false;
		}

		return sb.ToString();
	}

	#region Adding and Deleting Events

	/// <summary>
	/// Called when adding an event to the EditorChart.
	/// </summary>
	/// <param name="editorEvent">EditorEvent added.</param>
	public void AddEvent(EditorEvent editorEvent)
	{
		if (!editorEvent.IsStep() || editorEvent is not EditorLastSecondHintEvent)
			return;

		ResizeMeasures();

		if (!editorEvent.IsStep())
			return;

		AddEventInternal(editorEvent);
		RefreshGreatestStepCountPerMeasure();
	}

	/// <summary>
	/// Called when adding multiple events to the EditorChart.
	/// Prefer this method over multiple calls to AddEvent for better performance.
	/// </summary>
	/// <param name="editorEvents">EditorEvents added.</param>
	public void AddEvents(List<EditorEvent> editorEvents)
	{
		ResizeMeasures();
		foreach (var editorEvent in editorEvents)
			AddEventInternal(editorEvent);
		RefreshGreatestStepCountPerMeasure();
	}

	/// <summary>
	/// Performs all logic for adding an EditorEvent that can't be split out when adding
	/// multiple events simultaneously.
	/// </summary>
	/// <param name="editorEvent">EditorEvent added.</param>
	private void AddEventInternal(EditorEvent editorEvent)
	{
		if (!editorEvent.IsStep())
			return;

		// Update the step count for the this step's measure.
		var measureNumber = GetMeasureNumber(editorEvent);
		var previousSteps = Measures[measureNumber].Steps;
		var newSteps = previousSteps + 1;
		Measures[measureNumber].Steps = newSteps;

		// Update the measure count by step number so we can update the greatest step count per measure.
		var newStepCountForMeasure = Measures[measureNumber].Steps;
		MeasuresByStepCount[newStepCountForMeasure - 1]--;
		MeasuresByStepCount[newStepCountForMeasure]++;

		// Update stream.
		var minNotesPerMeasureForStream = GetMeasureSubdivision(Preferences.Instance.PreferencesStream.NoteType);
		if (previousSteps < minNotesPerMeasureForStream && newSteps >= minNotesPerMeasureForStream)
		{
			var precedingStreams = Streams.FindAllOverlapping(measureNumber - 1);
			Debug.Assert(precedingStreams.Count <= 1);
			var followingStreams = Streams.FindAllOverlapping(measureNumber + 1);
			Debug.Assert(followingStreams.Count <= 1);

			// This measure joined to two streams.
			if (precedingStreams.Count > 0 && followingStreams.Count > 0)
			{
				var deleted = Streams.Delete(precedingStreams[0].Item1, precedingStreams[0].Item2);
				Debug.Assert(deleted);
				deleted = Streams.Delete(followingStreams[0].Item1, followingStreams[0].Item2);
				Debug.Assert(deleted);
				Streams.Insert(new Tuple<int, int>(precedingStreams[0].Item1, followingStreams[0].Item2),
					precedingStreams[0].Item1, followingStreams[0].Item2);
			}
			// This measure extended a preceding stream.
			else if (precedingStreams.Count > 0)
			{
				var deleted = Streams.Delete(precedingStreams[0].Item1, precedingStreams[0].Item2);
				Debug.Assert(deleted);
				Streams.Insert(new Tuple<int, int>(precedingStreams[0].Item1, measureNumber), precedingStreams[0].Item1,
					measureNumber);
			}
			// This measure extended a following stream.
			else if (followingStreams.Count > 0)
			{
				var deleted = Streams.Delete(followingStreams[0].Item1, followingStreams[0].Item2);
				Debug.Assert(deleted);
				Streams.Insert(new Tuple<int, int>(measureNumber, followingStreams[0].Item2), measureNumber,
					followingStreams[0].Item2);
			}
			// This stream does not extend any existing streams.
			else
			{
				Streams.Insert(new Tuple<int, int>(measureNumber, measureNumber), measureNumber, measureNumber);
			}
		}
	}

	/// <summary>
	/// Called when deleting an event from the EditorChart.
	/// </summary>
	/// <param name="editorEvent">EditorEvent deleted.</param>
	public void DeleteEvent(EditorEvent editorEvent)
	{
		if (!editorEvent.IsStep() || editorEvent is not EditorLastSecondHintEvent)
			return;

		ResizeMeasures();

		if (!editorEvent.IsStep())
			return;

		ResizeMeasures();
		DeleteEventInternal(editorEvent);
		RefreshGreatestStepCountPerMeasure();
	}

	/// <summary>
	/// Called when deleting multiple events from the EditorChart.
	/// Prefer this method over multiple calls to DeleteEvent for better performance.
	/// </summary>
	/// <param name="editorEvents">EditorEvents deleted.</param>
	public void DeleteEvents(List<EditorEvent> editorEvents)
	{
		ResizeMeasures();
		foreach (var editorEvent in editorEvents)
			DeleteEventInternal(editorEvent);
		RefreshGreatestStepCountPerMeasure();
	}

	/// <summary>
	/// Performs all logic for deleting an EditorEvent that can't be split out when deleting
	/// multiple events simultaneously.
	/// </summary>
	/// <param name="editorEvent">EditorEvent deleted.</param>
	private void DeleteEventInternal(EditorEvent editorEvent)
	{
		if (!editorEvent.IsStep())
			return;

		// Update the step count for the this step's measure.
		var measureNumber = GetMeasureNumber(editorEvent);
		var previousSteps = Measures[measureNumber].Steps;
		var newSteps = previousSteps - 1;
		Measures[measureNumber].Steps = newSteps;

		// Update the measure count by step number so we can update the greatest step count per measure.
		var newStepCountForMeasure = Measures[measureNumber].Steps;
		MeasuresByStepCount[newStepCountForMeasure + 1]--;
		MeasuresByStepCount[newStepCountForMeasure]++;

		// Update stream.
		var minNotesPerMeasureForStream = GetMeasureSubdivision(Preferences.Instance.PreferencesStream.NoteType);
		if (previousSteps >= minNotesPerMeasureForStream && newSteps < minNotesPerMeasureForStream)
		{
			var overlappingStreams = Streams.FindAllOverlapping(measureNumber);
			Debug.Assert(overlappingStreams.Count == 1);
			var streamStartMeasureNumber = overlappingStreams[0].Item1;
			var streamEndMeasureNumber = overlappingStreams[0].Item2;

			// Delete the stream that was broken. We may replace it with shorter streams below.
			var deleted = Streams.Delete(streamStartMeasureNumber, streamEndMeasureNumber);
			Debug.Assert(deleted);

			// The stream to break is one measure long so we do not need to add new streams to replace it.
			if (streamStartMeasureNumber == streamEndMeasureNumber)
			{
				// Removed above.
			}
			// The measure that broke the stream started the stream. Shorten it.
			else if (measureNumber == streamStartMeasureNumber)
			{
				Streams.Insert(new Tuple<int, int>(streamStartMeasureNumber + 1, streamEndMeasureNumber),
					streamStartMeasureNumber + 1, streamEndMeasureNumber);
			}
			// The measure that broke the stream ended the stream. Shorten it.
			else if (measureNumber == streamEndMeasureNumber)
			{
				Streams.Insert(new Tuple<int, int>(streamStartMeasureNumber, streamEndMeasureNumber - 1),
					streamStartMeasureNumber, streamEndMeasureNumber - 1);
			}
			// The measure that broke the stream split it into two other streams.
			else
			{
				Streams.Insert(new Tuple<int, int>(streamStartMeasureNumber, measureNumber - 1), streamStartMeasureNumber,
					measureNumber - 1);
				Streams.Insert(new Tuple<int, int>(measureNumber + 1, streamEndMeasureNumber), measureNumber + 1,
					streamEndMeasureNumber);
			}
		}
	}

	#endregion Adding and Deleting Events

	/// <summary>
	/// Recomputes the timing for all measures.
	/// Performs an O(N) scan over all measures.
	/// Determining timing per measure is an O(log(N)) operation on the number of rate altering events in the chart.
	/// </summary>
	private void RecomputeMeasureTiming()
	{
		var t = 0.0;
		for (var m = 0; m < Measures.Count; m++)
		{
			if (m == 0)
			{
				EditorChart.TryGetTimeFromChartPosition(m * RowsPerMeasure, ref t);
				Measures[m].StartTime = t;
			}
			else
			{
				Measures[m].StartTime = Measures[m - 1].EndTime;
			}
		}
	}

	/// <summary>
	/// Resizes the Measures List making it match the EditorChart.
	/// </summary>
	private void ResizeMeasures()
	{
		var lastMeasure = GetLastMeasureNumber();
		var size = lastMeasure + 1;

		// Add measures.
		while (Measures.Count < size)
		{
			var m = new Measure();
			if (Measures.Count > 0)
				m.StartTime = Measures[^1].EndTime;
			else
				EditorChart.TryGetTimeFromChartPosition(0, ref m.StartTime);
			EditorChart.TryGetTimeFromChartPosition((Measures.Count + 1) * RowsPerMeasure, ref m.EndTime);
			Measures.Add(m);
		}

		// Remove measures.
		if (Measures.Count > size)
			Measures.RemoveRange(size, Measures.Count - size);
	}

	/// <summary>
	/// Refreshes the GreatestStepCountPerMeasure based on MeasuresByStepCount.
	/// </summary>
	private void RefreshGreatestStepCountPerMeasure()
	{
		GreatestStepCountPerMeasure = 0;
		for (var i = RowsPerMeasure - 1; i >= 0; i--)
		{
			if (MeasuresByStepCount[i] > 0)
			{
				GreatestStepCountPerMeasure = i;
				break;
			}
		}
	}

	/// <summary>
	/// Recomputes the stream breakdown of the EditorChart.
	/// Performs an O(N) scan over all EditorEvents.
	/// </summary>
	private void RecomputeStreams(bool initializeMeasureSteps)
	{
		Streams = new IntervalTree<int, Tuple<int, int>>();
		var minNotesPerMeasureForStream = GetMeasureSubdivision(Preferences.Instance.PreferencesStream.NoteType);
		var currentMeasure = -1;
		var currentStepsPerMeasure = 0;
		var lastStreamMeasureStart = -1;
		var lastStreamMeasure = -1;
		foreach (var editorEvent in EditorChart.GetEvents())
		{
			var measureNumber = GetMeasureNumber(editorEvent);

			if (measureNumber > currentMeasure)
			{
				// This measure did not stream
				if (currentStepsPerMeasure < minNotesPerMeasureForStream)
				{
					// If we were tracking a stream, commit it.
					if (lastStreamMeasureStart >= 0)
					{
						Streams.Insert(new Tuple<int, int>(lastStreamMeasureStart, lastStreamMeasure), lastStreamMeasureStart,
							lastStreamMeasure);
					}

					// Reset stream tracking.
					lastStreamMeasure = -1;
					lastStreamMeasureStart = -1;
				}

				// Reset step tracking for the measure.
				currentMeasure = measureNumber;
				currentStepsPerMeasure = 0;
			}

			// Track step towards stream.
			if (editorEvent.IsStep())
			{
				currentStepsPerMeasure++;
				if (currentStepsPerMeasure >= minNotesPerMeasureForStream)
				{
					if (lastStreamMeasureStart < 0)
						lastStreamMeasureStart = measureNumber;
					lastStreamMeasure = measureNumber;
				}

				if (initializeMeasureSteps)
					Measures[measureNumber].Steps++;
			}
		}

		// Commit any final stream.
		if (lastStreamMeasureStart >= 0)
		{
			Streams.Insert(new Tuple<int, int>(lastStreamMeasureStart, lastStreamMeasure), lastStreamMeasureStart,
				lastStreamMeasure);
		}
	}

	#region Measure Determination

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static int GetMeasureNumber(EditorEvent editorEvent)
	{
		return GetMeasureNumber(editorEvent.GetRow());
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static int GetMeasureNumber(int row)
	{
		return row / RowsPerMeasure;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private int GetLastMeasureNumber()
	{
		return GetMeasureNumber((int)EditorChart.GetEndPosition());
	}

	#endregion Measure Determination

	#region IObserver

	public void OnNotify(string eventId, PreferencesStream notifier, object payload)
	{
		switch (eventId)
		{
			// When the note type of the stream changes we need to recompute all streams.
			case PreferencesStream.NotificationNoteTypeChanged:
				RecomputeStreams(false);
				break;
		}
	}

	public void OnNotify(string eventId, EditorChart notifier, object payload)
	{
		switch (eventId)
		{
			// When timing changes we need to recompute all measure timing.
			case EditorChart.NotificationTimingChanged:
				RecomputeMeasureTiming();
				break;
		}
	}

	#endregion IObserver
}

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
///  - Aggregate Measure data for density visualizations. See StepDensityEffect.
/// Expected Usage:
///  Construct with EditorChart.
///  Call AddEvent/AddEvents when EditorEvents are added to the EditorChart.
///  Call DeleteEvent/DeleteEvents when EditorEvents are deleted from the EditorChart.
/// </summary>
internal sealed class StepDensity : Notifier<StepDensity>, Fumen.IObserver<PreferencesStream>, Fumen.IObserver<EditorChart>
{
	public const string NotificationMeasuresChanged = "MeasuresChanged";

	/// <summary>
	/// Always keep at least 256 measures allocated to avoid unnecessary reallocation.
	/// Measures are small and this will capture most normal-length songs.
	/// </summary>
	private const int MinMeasuresCapacity = 256;

	/// <summary>
	/// Data per measure.
	/// </summary>
	public readonly struct Measure
	{
		public Measure(double startTime, byte steps, byte rowsWithSteps)
		{
			StartTime = startTime;
			Steps = steps;
			RowsWithSteps = rowsWithSteps;
		}

		public readonly double StartTime;
		public readonly byte Steps;
		public readonly byte RowsWithSteps;
	}

	private class StreamSegment : IEquatable<StreamSegment>
	{
		private readonly int StartMeasure;
		private readonly int LastMeasure;

		public StreamSegment(int startMeasure, int lastMeasure)
		{
			StartMeasure = startMeasure;
			LastMeasure = lastMeasure;
		}

		public int GetLength()
		{
			return LastMeasure - StartMeasure + 1;
		}

		public int GetStartMeasure()
		{
			return StartMeasure;
		}

		public int GetLastMeasure()
		{
			return LastMeasure;
		}

		public bool Equals(StreamSegment other)
		{
			return ReferenceEquals(this, other);
		}

		public override bool Equals(object obj)
		{
			return ReferenceEquals(this, obj);
		}

		public override int GetHashCode()
		{
			// ReSharper disable once BaseObjectGetHashCodeCallInGetHashCode
			return base.GetHashCode();
		}
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
	/// All Measures in the EditorChart.
	/// </summary>
	private readonly DynamicArray<Measure> Measures = new(MinMeasuresCapacity);

	/// <summary>
	/// Stream breakdown represented as an IntervalTree.
	/// </summary>
	private IntervalTree<int, StreamSegment> Streams = new();

	/// <summary>
	/// Cached stream breakdown.
	/// </summary>
	private string StreamBreakdown;

	/// <summary>
	/// Dirty flag for stream breakdown.
	/// </summary>
	private bool StreamBreakdownDirty = true;

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
		Notify(NotificationMeasuresChanged, this);
	}

	/// <summary>
	/// Returns the array of all Measures.
	/// </summary>
	/// <returns>All Measures.</returns>
	public IReadOnlyDynamicArray<Measure> GetMeasures()
	{
		return Measures;
	}

	/// <summary>
	/// Returns a string representation of the stream breakdown of the EditorChart.
	/// </summary>
	/// <returns>String representation of the stream breakdown of the EditorChart.</returns>
	public string GetStreamBreakdown()
	{
		if (!StreamBreakdownDirty)
			return StreamBreakdown;

		var p = Preferences.Instance.PreferencesStream;
		var sb = new StringBuilder();
		var first = true;
		var previousStreamLastMeasure = 0;
		var anyStreams = false;
		foreach (var stream in Streams)
		{
			var streamLength = stream.GetLength();
			if (streamLength < p.MinimumLengthToConsiderStream)
				continue;

			if (!first)
			{
				var breakLength = stream.GetStartMeasure() - (previousStreamLastMeasure + 1);
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

			anyStreams = true;
			sb.Append(streamLength);

			previousStreamLastMeasure = stream.GetLastMeasure();
			first = false;
		}

		if (!anyStreams)
			StreamBreakdown = "No Streams";
		else
			StreamBreakdown = sb.ToString();
		StreamBreakdownDirty = false;
		return StreamBreakdown;
	}

	#region Adding and Deleting Events

	/// <summary>
	/// Called when adding an event to the EditorChart.
	/// </summary>
	/// <param name="editorEvent">EditorEvent added.</param>
	public void AddEvent(EditorEvent editorEvent)
	{
		if (editorEvent is EditorLastSecondHintEvent)
		{
			ResizeMeasures();
			return;
		}

		if (!editorEvent.IsStep())
			return;

		ResizeMeasures();
		AddEventInternal(editorEvent);
		Notify(NotificationMeasuresChanged, this);
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
		Notify(NotificationMeasuresChanged, this);
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
		var previousRowsWithSteps = Measures[measureNumber].RowsWithSteps;
		var newRowsWithSteps = previousRowsWithSteps;
		if (EditorChart.GetStepTotals().GetNumStepsAtRow(editorEvent.GetRow()) == 1)
			newRowsWithSteps++;
		Measures[measureNumber] = new Measure(Measures[measureNumber].StartTime, (byte)newSteps, newRowsWithSteps);

		// Update the measure count by step number so we can update the greatest step count per measure.
		var newStepCountForMeasure = Measures[measureNumber].Steps;
		MeasuresByStepCount[newStepCountForMeasure - 1]--;
		MeasuresByStepCount[newStepCountForMeasure]++;

		// Update stream.
		var minNotesPerMeasureForStream = GetMeasureSubdivision(Preferences.Instance.PreferencesStream.NoteType);
		var addedStream = false;
		if (Preferences.Instance.PreferencesStream.AccumulationType == StepAccumulationType.Step)
			addedStream = previousSteps < minNotesPerMeasureForStream && newSteps >= minNotesPerMeasureForStream;
		else if (Preferences.Instance.PreferencesStream.AccumulationType == StepAccumulationType.Row)
			addedStream = previousRowsWithSteps < minNotesPerMeasureForStream && newRowsWithSteps >= minNotesPerMeasureForStream;
		if (addedStream)
		{
			var precedingStreams = Streams.FindAllOverlapping(measureNumber - 1);
			Debug.Assert(precedingStreams.Count <= 1);
			var followingStreams = Streams.FindAllOverlapping(measureNumber + 1);
			Debug.Assert(followingStreams.Count <= 1);

			// This measure joined to two streams.
			if (precedingStreams.Count > 0 && followingStreams.Count > 0)
			{
				var deleted = Streams.Delete(precedingStreams[0], precedingStreams[0].GetStartMeasure(),
					precedingStreams[0].GetLastMeasure());
				Debug.Assert(deleted);
				deleted = Streams.Delete(followingStreams[0], followingStreams[0].GetStartMeasure(),
					followingStreams[0].GetLastMeasure());
				Debug.Assert(deleted);
				Streams.Insert(new StreamSegment(precedingStreams[0].GetStartMeasure(), followingStreams[0].GetLastMeasure()),
					precedingStreams[0].GetStartMeasure(), followingStreams[0].GetLastMeasure());
			}
			// This measure extended a preceding stream.
			else if (precedingStreams.Count > 0)
			{
				var deleted = Streams.Delete(precedingStreams[0], precedingStreams[0].GetStartMeasure(),
					precedingStreams[0].GetLastMeasure());
				Debug.Assert(deleted);
				Streams.Insert(new StreamSegment(precedingStreams[0].GetStartMeasure(), measureNumber),
					precedingStreams[0].GetStartMeasure(),
					measureNumber);
			}
			// This measure extended a following stream.
			else if (followingStreams.Count > 0)
			{
				var deleted = Streams.Delete(followingStreams[0], followingStreams[0].GetStartMeasure(),
					followingStreams[0].GetLastMeasure());
				Debug.Assert(deleted);
				Streams.Insert(new StreamSegment(measureNumber, followingStreams[0].GetLastMeasure()), measureNumber,
					followingStreams[0].GetLastMeasure());
			}
			// This stream does not extend any existing streams.
			else
			{
				Streams.Insert(new StreamSegment(measureNumber, measureNumber), measureNumber, measureNumber);
			}
		}

		StreamBreakdownDirty = true;
	}

	/// <summary>
	/// Called when deleting an event from the EditorChart.
	/// </summary>
	/// <param name="editorEvent">EditorEvent deleted.</param>
	public void DeleteEvent(EditorEvent editorEvent)
	{
		if (editorEvent is EditorLastSecondHintEvent)
		{
			ResizeMeasures();
			return;
		}

		if (!editorEvent.IsStep())
			return;

		DeleteEventInternal(editorEvent);
		ResizeMeasures();
		Notify(NotificationMeasuresChanged, this);
	}

	/// <summary>
	/// Called when deleting multiple events from the EditorChart.
	/// Prefer this method over multiple calls to DeleteEvent for better performance.
	/// </summary>
	/// <param name="editorEvents">EditorEvents deleted.</param>
	public void DeleteEvents(List<EditorEvent> editorEvents)
	{
		foreach (var editorEvent in editorEvents)
			DeleteEventInternal(editorEvent);
		ResizeMeasures();
		Notify(NotificationMeasuresChanged, this);
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
		var previousSteps = measureNumber < Measures.GetSize() ? Measures[measureNumber].Steps : 1;
		var newSteps = previousSteps - 1;
		var previousRowsWithSteps = Measures[measureNumber].RowsWithSteps;
		var newRowsWithSteps = previousRowsWithSteps;
		if (EditorChart.GetStepTotals().GetNumStepsAtRow(editorEvent.GetRow()) == 0)
			newRowsWithSteps--;
		if (measureNumber < Measures.GetSize())
			Measures[measureNumber] = new Measure(Measures[measureNumber].StartTime, (byte)newSteps, newRowsWithSteps);

		// Update the measure count by step number so we can update the greatest step count per measure.
		var newStepCountForMeasure = measureNumber < Measures.GetSize() ? Measures[measureNumber].Steps : 0;
		MeasuresByStepCount[newStepCountForMeasure + 1]--;
		MeasuresByStepCount[newStepCountForMeasure]++;

		// Update stream.
		var minNotesPerMeasureForStream = GetMeasureSubdivision(Preferences.Instance.PreferencesStream.NoteType);
		var deletedStream = false;
		if (Preferences.Instance.PreferencesStream.AccumulationType == StepAccumulationType.Step)
			deletedStream = previousSteps >= minNotesPerMeasureForStream && newSteps < minNotesPerMeasureForStream;
		else if (Preferences.Instance.PreferencesStream.AccumulationType == StepAccumulationType.Row)
			deletedStream = previousRowsWithSteps >= minNotesPerMeasureForStream &&
			                newRowsWithSteps < minNotesPerMeasureForStream;
		if (deletedStream)
		{
			var overlappingStreams = Streams.FindAllOverlapping(measureNumber);
			Debug.Assert(overlappingStreams.Count == 1);
			var streamStartMeasureNumber = overlappingStreams[0].GetStartMeasure();
			var streamEndMeasureNumber = overlappingStreams[0].GetLastMeasure();

			// Delete the stream that was broken. We may replace it with shorter streams below.
			var deleted = Streams.Delete(overlappingStreams[0], streamStartMeasureNumber, streamEndMeasureNumber);
			Debug.Assert(deleted);

			// The stream to break is one measure long so we do not need to add new streams to replace it.
			if (streamStartMeasureNumber == streamEndMeasureNumber)
			{
				// Removed above.
			}
			// The measure that broke the stream started the stream. Shorten it.
			else if (measureNumber == streamStartMeasureNumber)
			{
				Streams.Insert(new StreamSegment(streamStartMeasureNumber + 1, streamEndMeasureNumber),
					streamStartMeasureNumber + 1, streamEndMeasureNumber);
			}
			// The measure that broke the stream ended the stream. Shorten it.
			else if (measureNumber == streamEndMeasureNumber)
			{
				Streams.Insert(new StreamSegment(streamStartMeasureNumber, streamEndMeasureNumber - 1),
					streamStartMeasureNumber, streamEndMeasureNumber - 1);
			}
			// The measure that broke the stream split it into two other streams.
			else
			{
				Streams.Insert(new StreamSegment(streamStartMeasureNumber, measureNumber - 1), streamStartMeasureNumber,
					measureNumber - 1);
				Streams.Insert(new StreamSegment(measureNumber + 1, streamEndMeasureNumber), measureNumber + 1,
					streamEndMeasureNumber);
			}
		}

		StreamBreakdownDirty = true;
	}

	#endregion Adding and Deleting Events

	/// <summary>
	/// Recomputes the timing for all measures.
	/// Performs an O(N) scan over all measures.
	/// </summary>
	private void RecomputeMeasureTiming()
	{
		var numMeasures = Measures.GetSize();
		var enumerator = EditorChart.GetRateAlteringEvents().First();
		enumerator.MoveNext();
		var currentRae = enumerator.Current!;
		EditorRateAlteringEvent nextRae = null;
		var nextRaeRow = 0;
		if (enumerator.MoveNext())
		{
			nextRae = enumerator.Current!;
			nextRaeRow = nextRae.GetRow();
		}

		for (var m = 0; m < numMeasures; m++)
		{
			var row = m * RowsPerMeasure;
			while (nextRae != null && row >= nextRaeRow)
			{
				currentRae = nextRae;
				if (enumerator.MoveNext())
				{
					nextRae = enumerator.Current!;
					nextRaeRow = nextRae.GetRow();
				}
				else
				{
					nextRae = null;
					nextRaeRow = 0;
				}
			}

			var steps = Measures[m].Steps;
			var rowsWithSteps = Measures[m].RowsWithSteps;
			Measures[m] = new Measure(currentRae.GetChartTimeFromPosition(row), steps, rowsWithSteps);
		}
	}

	/// <summary>
	/// Resizes the Measures List making it match the EditorChart.
	/// </summary>
	private void ResizeMeasures()
	{
		var newSize = GetLastMeasureNumber() + 1;
		Measures.UpdateCapacity(Math.Max(MinMeasuresCapacity, newSize));

		IReadOnlyRedBlackTree<EditorRateAlteringEvent>.IReadOnlyRedBlackTreeEnumerator enumerator = null;
		EditorRateAlteringEvent currentRae = null;
		EditorRateAlteringEvent nextRae = null;
		var nextRaeRow = 0;

		while (Measures.GetSize() < newSize)
		{
			var row = Measures.GetSize() * RowsPerMeasure;

			if (enumerator == null)
			{
				enumerator = EditorChart.GetRateAlteringEvents().FindActiveRateAlteringEventEnumeratorForPosition(row, false);
				enumerator.MoveNext();
				currentRae = enumerator.Current!;
				if (enumerator.MoveNext())
				{
					nextRae = enumerator.Current!;
					nextRaeRow = nextRae.GetRow();
				}
			}

			while (nextRae != null && row >= nextRaeRow)
			{
				currentRae = nextRae;
				if (enumerator.MoveNext())
				{
					nextRae = enumerator.Current!;
					nextRaeRow = nextRae.GetRow();
				}
				else
				{
					nextRae = null;
					nextRaeRow = 0;
				}
			}

			Measures.Add(new Measure(currentRae.GetChartTimeFromPosition(row), 0, 0));
		}
	}

	/// <summary>
	/// Recomputes the stream breakdown of the EditorChart.
	/// Performs an O(N) scan over all EditorEvents.
	/// </summary>
	private void RecomputeStreams(bool initializeMeasureSteps)
	{
		Streams = new IntervalTree<int, StreamSegment>();
		var minNotesPerMeasureForStream = GetMeasureSubdivision(Preferences.Instance.PreferencesStream.NoteType);
		var countStepsForStream = Preferences.Instance.PreferencesStream.AccumulationType == StepAccumulationType.Step;
		var currentMeasure = -1;
		var currentStepsPerMeasure = 0;
		var currentRowsWithStepsPerMeasure = 0;
		var lastStreamMeasureStart = -1;
		var lastStreamMeasure = -1;

		var lastRowWithStep = -1;
		foreach (var editorEvent in EditorChart.GetEvents())
		{
			var measureNumber = GetMeasureNumber(editorEvent);

			if (measureNumber > currentMeasure)
			{
				if (initializeMeasureSteps && currentMeasure >= 0 && currentStepsPerMeasure > 0)
				{
					Measures[currentMeasure] = new Measure(Measures[currentMeasure].StartTime, (byte)currentStepsPerMeasure,
						(byte)currentRowsWithStepsPerMeasure);
				}

				// This measure did not stream
				if ((countStepsForStream && currentStepsPerMeasure < minNotesPerMeasureForStream)
				    || (!countStepsForStream && currentRowsWithStepsPerMeasure < minNotesPerMeasureForStream))
				{
					// If we were tracking a stream, commit it.
					if (lastStreamMeasureStart >= 0)
					{
						Streams.Insert(new StreamSegment(lastStreamMeasureStart, lastStreamMeasure), lastStreamMeasureStart,
							lastStreamMeasure);
					}

					// Reset stream tracking.
					lastStreamMeasure = -1;
					lastStreamMeasureStart = -1;
				}

				// Reset step tracking for the measure.
				currentMeasure = measureNumber;
				currentStepsPerMeasure = 0;
				currentRowsWithStepsPerMeasure = 0;
			}

			// Track step towards stream.
			if (editorEvent.IsStep())
			{
				var row = editorEvent.GetRow();
				currentStepsPerMeasure++;
				if (lastRowWithStep != row)
				{
					currentRowsWithStepsPerMeasure++;
				}

				if ((countStepsForStream && currentStepsPerMeasure >= minNotesPerMeasureForStream)
				    || (!countStepsForStream && currentRowsWithStepsPerMeasure >= minNotesPerMeasureForStream))
				{
					if (lastStreamMeasureStart < 0)
						lastStreamMeasureStart = measureNumber;
					lastStreamMeasure = measureNumber;
				}

				lastRowWithStep = row;
			}
		}

		// Commit any final step count per measure.
		if (initializeMeasureSteps && currentMeasure >= 0 && currentStepsPerMeasure > 0)
		{
			Measures[currentMeasure] = new Measure(Measures[currentMeasure].StartTime, (byte)currentStepsPerMeasure,
				(byte)currentRowsWithStepsPerMeasure);
		}

		// Commit any final stream.
		if (lastStreamMeasureStart >= 0)
		{
			Streams.Insert(new StreamSegment(lastStreamMeasureStart, lastStreamMeasure), lastStreamMeasureStart,
				lastStreamMeasure);
		}

		StreamBreakdownDirty = true;
	}

	#region Measure Number Determination

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

	public double GetLastMeasurePlusOneTime()
	{
		var time = 0.0;
		var row = (GetLastMeasureNumber() + 1) * RowsPerMeasure;
		EditorChart.TryGetTimeFromChartPosition(row, ref time);
		return time;
	}

	#endregion Measure Number Determination

	#region IObserver

	public void OnNotify(string eventId, PreferencesStream notifier, object payload)
	{
		switch (eventId)
		{
			// When the note type of the stream changes we need to recompute all streams.
			case PreferencesStream.NotificationNoteTypeChanged:
				RecomputeStreams(false);
				break;
			case PreferencesStream.NotificationStreamTextParametersChanged:
				StreamBreakdownDirty = true;
				break;
			case PreferencesStream.NotificationAccumulationTypeChanged:
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
				Notify(NotificationMeasuresChanged, this);
				break;
		}
	}

	#endregion IObserver
}

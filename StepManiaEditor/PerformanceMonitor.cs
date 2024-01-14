using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;

namespace StepManiaEditor;

/// <summary>
/// Class for tracking how long operations take.
/// Maintains a circular buffer of data per frame. Per frame, a series of timed operations are recorded.
/// Implements IEnumerable so results can be enumerated.
/// When enumerating, the IFrameData order will be most recent to least recent.
/// Expected Usage:
///  Construct PerformanceMonitor with a desired buffer length.
///  Call BeginFrame at the start of each frame.
///  Call Time to perform and time an Action.
///  Call StartTiming and EndTiming to track the start and end of an operation.
///  Call SetTime to set the tracked time of an operation directly from external tracking.
///  Call SetEnabled to pause and resume tracking.
///  Call GetEnumerator to enumerate frame data.
/// </summary>
internal sealed class PerformanceMonitor : IEnumerable
{
	#region Public Interfaces

	/// <summary>
	/// Data per frame.
	/// </summary>
	public interface IFrameData
	{
		/// <summary>
		/// Gets the length of the frame in ticks.
		/// </summary>
		/// <returns>Length of the frame in ticks.</returns>
		public long GetTicks();

		/// <summary>
		/// Gets the length of the frame in seconds.
		/// </summary>
		/// <returns>Length of the frame in seconds.</returns>
		public double GetSeconds();

		/// <summary>
		/// Gets the ITimingData for all timed operations this frame.
		/// </summary>
		/// <returns>ITimingData for all timed operations this frame.</returns>
		public IEnumerable<ITimingData> GetTimingData();
	}

	/// <summary>
	/// Data per timed operation per frame.
	/// </summary>
	public interface ITimingData
	{
		/// <summary>
		/// Gets the total length of the operation in ticks.
		/// </summary>
		/// <returns>Total length of the operation in ticks.</returns>
		public long GetTicks();

		/// <summary>
		/// Gets the total length of the operation in seconds.
		/// </summary>
		/// <returns>Total length of the operation in seconds.</returns>
		public double GetSeconds();
	}

	#endregion Public Interfaces

	#region Subclasses

	/// <summary>
	/// Data per frame.
	/// </summary>
	private sealed class FrameData : IFrameData
	{
		/// <summary>
		/// All TimingData for this frame.
		/// </summary>
		public readonly TimingData[] Timings;

		/// <summary>
		/// Constructor
		/// </summary>
		/// <param name="numTimings">Number of operations per frame to be timed.</param>
		public FrameData(int numTimings)
		{
			Timings = new TimingData[numTimings];
			for (var i = 0; i < numTimings; i++)
			{
				Timings[i] = new TimingData();
			}
		}

		/// <summary>
		/// Resets this FrameData so it can be re-used.
		/// </summary>
		public void Reset()
		{
			for (var i = 0; i < Timings.Length; i++)
			{
				Timings[i].Reset();
			}
		}

		#region IFrameData

		public long GetTicks()
		{
			if (Timings.Length == 0)
				return 0L;
			// Assumes the first TimingData is for the entire frame.
			return Timings[0].GetTicks();
		}

		public double GetSeconds()
		{
			if (Timings.Length == 0)
				return 0L;
			// Assumes the first TimingData is for the entire frame.
			return Timings[0].GetSeconds();
		}

		public IEnumerable<ITimingData> GetTimingData()
		{
			return Timings;
		}

		#endregion IFrameData
	}

	/// <summary>
	/// Data per timed operation per frame.
	/// </summary>
	private sealed class TimingData : ITimingData
	{
		/// <summary>
		/// Start time of an operation in ticks.
		/// </summary>
		private long StartTicks;

		/// <summary>
		/// Total time tracked for an operation in ticks.
		/// </summary>
		private long TotalTicks;

		/// <summary>
		/// Resets this TimingData so it can be re-used.
		/// </summary>
		public void Reset()
		{
			TotalTicks = 0L;
		}

		/// <summary>
		/// Returns whether or not this TimingData is complete.
		/// Complete TimingData has had at least one call to end timing or add time.
		/// </summary>
		/// <returns>True if this TimingData is complete and false otherwise.</returns>
		public bool IsComplete()
		{
			return TotalTicks != 0L;
		}

		/// <summary>
		/// Start timing an operation. It is expected this is followed by a call to EndTiming.
		/// </summary>
		/// <param name="startTicks">Tick count at the start of the operation.</param>
		public void StartTiming(long startTicks)
		{
			StartTicks = startTicks;
		}

		/// <summary>
		/// Stop timing an operation. It is expected this is preceded by a call to StartTiming.
		/// </summary>
		/// <param name="endTicks">Tick count at the end of the operation.</param>
		public void EndTiming(long endTicks)
		{
			// Add to the existing TotalTicks.
			// We want to support multiple start and stop calls per frame.
			TotalTicks += endTicks - StartTicks;
		}

		/// <summary>
		/// Add a tick time to the tracked tick count without a pair of StartTiming/EndTiming calls.
		/// </summary>
		/// <param name="totalTicks">Total ticks to count.</param>
		public void AddTime(long totalTicks)
		{
			TotalTicks += totalTicks;
		}

		#region ITimingData

		public long GetTicks()
		{
			return TotalTicks;
		}

		public double GetSeconds()
		{
			return (double)TotalTicks / TimeSpan.TicksPerSecond;
		}

		#endregion ITimingData
	}

	#endregion Subclasses

	/// <summary>
	/// Identifier to use for implicitly tracking the time of an entire frame.
	/// </summary>
	private const string FrameTimingIdentifier = "Frame";

	/// <summary>
	/// Stopwatch for timing operations.
	/// </summary>
	private readonly Stopwatch Timer;

	/// <summary>
	/// Whether or not tracking is enabled.
	/// </summary>
	private bool Enabled = true;

	/// <summary>
	/// Circular buffer of FrameData.
	/// </summary>
	private readonly FrameData[] Frames;

	/// <summary>
	/// The index of the FrameData for the current frame which is actively being recorded.
	/// </summary>
	private int CurrentFrameIndex = -1;

	/// <summary>
	/// The index of the last completed FrameData that we can enumerate back to.
	/// </summary>
	private int LastValidFrameIndex = -1;

	/// <summary>
	/// The identifiers of operations we expect to track data for.
	/// </summary>
	private readonly string[] TimingTypes;

	/// <summary>
	/// Dictionary of operation identifiers to their index in the TimingTypes array.
	/// </summary>
	private readonly Dictionary<string, int> TimingTypeIndexes;

	/// <summary>
	/// Constructor.
	/// </summary>
	/// <param name="bufferSize">The number of frames to track data for.</param>
	/// <param name="timingTypes">
	/// Array of string identifiers of all the operations that will be tracked.
	/// </param>
	public PerformanceMonitor(int bufferSize, string[] timingTypes)
	{
		// Start timer for tracking operation times.
		Timer = new Stopwatch();
		Timer.Start();

		// Set up TimingTypes and TimingTypeIndexes.
		TimingTypes = new string[timingTypes.Length + 1];
		TimingTypes[0] = FrameTimingIdentifier;
		TimingTypeIndexes = new Dictionary<string, int> { { FrameTimingIdentifier, 0 } };
		for (var i = 0; i < timingTypes.Length; i++)
		{
			// Add 1 to account for the implicit Frame timing.
			TimingTypes[i + 1] = timingTypes[i];
			TimingTypeIndexes.Add(timingTypes[i], i + 1);
		}

		// Set up Frames.
		Frames = new FrameData[bufferSize];
		for (var i = 0; i < bufferSize; i++)
		{
			Frames[i] = new FrameData(TimingTypes.Length);
		}
	}

	IEnumerator IEnumerable.GetEnumerator()
	{
		return GetEnumerator();
	}

	public FrameEnum GetEnumerator()
	{
		return new FrameEnum(this);
	}

	public int GetMaxNumFrames()
	{
		return Frames.Length;
	}

	public int GetNumTimingsPerFrame()
	{
		return TimingTypes.Length;
	}

	public string[] GetTimingTypes()
	{
		return TimingTypes;
	}

	private FrameData GetCurrentFrameData()
	{
		if (CurrentFrameIndex < 0)
			return null;
		return Frames[CurrentFrameIndex];
	}

	private bool TryGetMostRecentCompletedFrameIndex(out int index)
	{
		index = 0;
		if (CurrentFrameIndex < 0)
			return false;
		index = CurrentFrameIndex;
		GetTimingIndex(FrameTimingIdentifier, out var frameTimingIndex);
		if (!Frames[index].Timings[frameTimingIndex].IsComplete())
		{
			if (index == LastValidFrameIndex)
				return false;
			DecrementIndex(ref index);
		}

		return true;
	}

	private int GetNumFramesInUse()
	{
		var lastFrameRelative = LastValidFrameIndex;
		if (lastFrameRelative > CurrentFrameIndex)
			lastFrameRelative -= Frames.Length;
		return CurrentFrameIndex - lastFrameRelative;
	}

	private bool GetTimingIndex(string identifier, out int index)
	{
		return TimingTypeIndexes.TryGetValue(identifier, out index);
	}

	private void IncrementIndex(ref int index)
	{
		index = (index + 1) % Frames.Length;
	}

	private void DecrementIndex(ref int index)
	{
		index--;
		if (index < 0)
			index = Frames.Length - 1;
	}

	public void SetEnabled(bool enabled)
	{
		Enabled = enabled;
	}

	public bool IsEnabled()
	{
		return Enabled;
	}

	public void StartTiming(string identifier)
	{
		if (!Enabled)
			return;
		var frame = GetCurrentFrameData();
		if (frame == null)
			return;
		if (!GetTimingIndex(identifier, out var index))
			return;
		frame.Timings[index].StartTiming(Timer.ElapsedTicks);
	}

	public void EndTiming(string identifier)
	{
		if (!Enabled)
			return;
		var frame = GetCurrentFrameData();
		if (frame == null)
			return;
		if (!GetTimingIndex(identifier, out var index))
			return;
		frame.Timings[index].EndTiming(Timer.ElapsedTicks);
	}

	public void Time(string identifier, Action action)
	{
		if (!Enabled)
		{
			action();
			return;
		}

		var frame = GetCurrentFrameData();
		if (frame == null)
			return;
		if (!GetTimingIndex(identifier, out var index))
			return;

		frame.Timings[index].StartTiming(Timer.ElapsedTicks);
		action();
		frame.Timings[index].EndTiming(Timer.ElapsedTicks);
	}

	public void SetTime(string identifier, long ticks)
	{
		if (!Enabled)
			return;
		var frame = GetCurrentFrameData();
		if (frame == null)
			return;
		if (!GetTimingIndex(identifier, out var index))
			return;
		frame.Timings[index].AddTime(ticks);
	}

	public void BeginFrame(long ticksAtFrameStart)
	{
		GetTimingIndex(FrameTimingIdentifier, out var frameTimingIndex);

		// Complete previous frame.
		var frame = GetCurrentFrameData();
		if (frame != null && !frame.Timings[frameTimingIndex].IsComplete())
		{
			frame.Timings[frameTimingIndex].EndTiming(ticksAtFrameStart);
			if (LastValidFrameIndex < 0)
				LastValidFrameIndex = CurrentFrameIndex;
		}

		if (!Enabled)
			return;

		// Advance frame.
		if (GetNumFramesInUse() == Frames.Length - 1)
			IncrementIndex(ref LastValidFrameIndex);
		IncrementIndex(ref CurrentFrameIndex);

		// Initialize new frame.
		frame = GetCurrentFrameData();
		frame.Reset();
		frame.Timings[frameTimingIndex].StartTiming(ticksAtFrameStart);
	}

	#region Enumerator

	/// <summary>
	/// Enumerator for IFrameData.
	/// Enumerates from the most recent completed IFrameData to the least recent completed IFrameData.
	/// </summary>
	public class FrameEnum : IEnumerator
	{
		private int Index;
		private bool BeforeFirst;
		private bool CanBeEnumerated;
		private readonly PerformanceMonitor PerformanceMonitor;

		public FrameEnum(PerformanceMonitor performanceMonitor)
		{
			PerformanceMonitor = performanceMonitor;
			Reset();
		}

		public bool MoveNext()
		{
			if (BeforeFirst)
			{
				BeforeFirst = false;
				return CanBeEnumerated;
			}

			BeforeFirst = false;

			if (Index == PerformanceMonitor.LastValidFrameIndex)
				CanBeEnumerated = false;
			if (!CanBeEnumerated)
				return false;

			PerformanceMonitor.DecrementIndex(ref Index);
			return true;
		}

		public void Reset()
		{
			BeforeFirst = true;
			CanBeEnumerated = PerformanceMonitor.TryGetMostRecentCompletedFrameIndex(out Index);
		}

		object IEnumerator.Current => Current;

		public IFrameData Current
		{
			get
			{
				if (BeforeFirst || !CanBeEnumerated)
				{
					throw new InvalidOperationException();
				}

				return PerformanceMonitor.Frames[Index];
			}
		}
	}

	#endregion Enumerator
}

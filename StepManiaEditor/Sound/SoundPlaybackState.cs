using System;

namespace StepManiaEditor;

/// <summary>
/// Wrapper around an EditorSound for maintaining playback state.
/// Does not actually play the sound. It is expected that an external
/// source use the sample index maintained by this class to control playback of
/// the wrapped EditorSound's sample data.
/// </summary>
internal sealed class SoundPlaybackState
{
	/// <summary>
	/// The EditorSound being played.
	/// </summary>
	private readonly EditorSound Sound;

	/// <summary>
	/// The current sample index of the EditorSound.
	/// </summary>
	private long SampleIndex;

	/// <summary>
	/// Whether or not the EditorSound is playing.
	/// </summary>
	private bool Playing;

	/// <summary>
	/// Lock for updating state in a thread-safe manner.
	/// </summary>
	private readonly object Lock = new();

	/// <summary>
	/// Constructor
	/// </summary>
	/// <param name="sound">EditorSound.</param>
	public SoundPlaybackState(EditorSound sound)
	{
		Sound = sound;
	}

	/// <summary>
	/// Gets the EditorSound wrapped by this SoundPlaybackState.
	/// </summary>
	/// <returns>EditorSound wrapped by this SoundPlaybackState.</returns>
	public EditorSound GetSound()
	{
		return Sound;
	}

	/// <summary>
	/// Start playing the sound at the given sample index.
	/// </summary>
	/// <param name="sampleIndex">Sample index to start playing at.</param>
	public void StartPlaying(long sampleIndex)
	{
		lock (Lock)
		{
			Playing = true;
			SetSampleIndex(sampleIndex);
		}
	}

	/// <summary>
	/// Stops playing the sound.
	/// </summary>
	/// <param name="keepSampleIndex">
	/// If true, keep the current sample index.
	/// If false, reset the sample index to 0.
	/// </param>
	public void StopPlaying(bool keepSampleIndex = false)
	{
		lock (Lock)
		{
			Playing = false;
			if (!keepSampleIndex)
			{
				SampleIndex = 0L;
			}

			SampleIndex = 0L;
		}
	}

	/// <summary>
	/// Returns whether or not the sound is playing.
	/// </summary>
	/// <returns>Whether or not the sound is playing.</returns>
	public bool IsPlaying()
	{
		lock (Lock)
		{
			return Playing;
		}
	}

	/// <summary>
	/// Sets the current sample index of the SoundPlaybackState to the given value.
	/// Will clamp the given index to be within the valid sample range.
	/// </summary>
	/// <param name="sampleIndex">Sample index to set.</param>
	public void SetSampleIndex(long sampleIndex)
	{
		lock (Lock)
		{
			// Clamp the sample index to the valid range if we have data.
			var (numChannels, sampleData) = Sound.GetSampleData();
			if (numChannels > 0 && sampleData != null)
			{
				var numSamples = sampleData.Length / numChannels;
				SampleIndex = Math.Clamp(sampleIndex, 0L, numSamples);
			}
			// If we do not have data yet just clamp the lower bound.
			else
			{
				SampleIndex = Math.Max(sampleIndex, 0L);
			}
		}
	}

	/// <summary>
	/// Gets the current sample index of the SoundPlaybackState.
	/// </summary>
	/// <returns>Current sample index of the SoundPlaybackState.</returns>
	public long GetSampleIndex()
	{
		lock (Lock)
		{
			return SampleIndex;
		}
	}

	/// <summary>
	/// Gets the lock this EditorSound uses for controlling mutations.
	/// </summary>
	/// <remarks>
	/// This is poor encapsulation.
	/// If this class ever gets expanded beyond being a simple helper for MusicManager this should be
	/// reconsidered.
	/// </remarks>
	/// <returns>Lock object for mutating this EditorSound.</returns>
	public object GetLock()
	{
		return Lock;
	}

	/// <summary>
	/// Callback for when the EditorSound issues a notification.
	/// </summary>
	public void OnNotify(string eventId, EditorSound notifier, object payload)
	{
		switch (eventId)
		{
			case EditorSound.NotificationSampleDataChanged:
				// Now that the sample data has changed, check for clamping the SampleIndex.
				SetSampleIndex(GetSampleIndex());
				break;
		}
	}
}

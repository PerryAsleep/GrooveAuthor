using System;
using System.Collections.Generic;
using FMOD;
using Fumen;

namespace StepManiaEditor;

/// <summary>
/// Class for managing the music sound, preview sound, and assist tick sounds.
/// 
/// Offers idempotent asynchronous methods for loading sound files from disk.
/// 
/// Offers methods for playing the music and setting the music time directly.
/// Gracefully handles desired music times which are negative or outside of the
/// range of the music sound.
/// 
/// Offers methods for playing and stopping the music preview. The music preview
/// may be its own sound from an independent audio file, or it may be a range of
/// the music audio file. When playing the preview sound, it will loop continuously
/// until told to stop.
/// 
/// Offers method for playing the music with an offset. Normally offsets used to sync
/// visuals and audio would be implemented on the visuals by shifting everything up or
/// down to match the audio, however in an Editor the positions need to be precise.
/// If for example the delay between audio and video is substantial and the editor is
/// paused at a specific row, and then playback begins, if the offset were implemented
/// visually, then the notes would visibly jerk. It may be preferable to a user to
/// instead have the audio shift instead.
///
/// Handles automatically reloading sound files if they change externally.
///
/// Expected usage:
///  Call LoadMusicAsync whenever the music file changes.
///  Call LoadMusicPreviewAsync whenever the preview file changes.
///  Call Update once each frame.
///  Call StartPlayback and StopPlayback to start and stop playing the music.
///  Call StartPreviewPlayback and StopPreviewPlayback to start and stop playing the preview.
///  Call Shutdown at the end of the session.
/// </summary>
internal sealed class MusicManager
{
	/// <summary>
	/// Internal state of the MusicManager.
	/// </summary>
	private enum PlayingState
	{
		PlayingNothing,

		/// <summary>
		/// Playing the song music.
		/// Not necessarily the same as MusicData.IsPlaying since we may be
		/// leveraging the music SoundData to play the preview.
		/// </summary>
		PlayingMusic,

		/// <summary>
		/// Playing the song preview.
		/// Not necessarily the same as PreviewData.IsPlaying since we may be
		/// leveraging the music SoundData to play the preview.
		/// </summary>
		PlayingPreview,
	}

	/// <summary>
	/// Name of the DSP this class manages.
	/// </summary>
	private const string DspName = "MusicDsp";

	/// <summary>
	/// SoundManager for low level sound management.
	/// </summary>
	private readonly SoundManager SoundManager;

	/// <summary>
	/// SoundPlaybackState for the assist tick sound.
	/// </summary>
	private readonly SoundPlaybackState AssistTickData;

	/// <summary>
	/// SoundPlaybackState for the current music.
	/// </summary>
	private readonly SoundPlaybackState MusicData;

	/// <summary>
	/// SoundPlaybackState for the current preview music.
	/// </summary>
	private readonly SoundPlaybackState PreviewData;

	/// <summary>
	/// Sample rate for all sounds managed through this class.
	/// In practice, this is the FMOD engine sample rate, which by default is 48,000hz.
	/// </summary>
	private readonly uint SampleRate;

	/// <summary>
	/// The current sample index of the music.
	/// This may be negative or greater than the music's sample range.
	/// This is used to control timing of all sounds needing to play relative to the music.
	/// </summary>
	private int SampleIndex;

	/// <summary>
	/// List of sample indexes of upcoming assist ticks to play.
	/// </summary>
	private List<int> NextAssistTickStartMusicSamples;

	/// <summary>
	/// Lock for thread safe mutations of members needed in the DSP callback.
	/// </summary>
	private readonly object Lock = new();

	/// <summary>
	/// Internal state.
	/// </summary>
	private PlayingState State = PlayingState.PlayingNothing;

	/// <summary>
	/// Whether or not a distinct sound file should be used for the preview sound.
	/// Typically, the preview is a region of the music rather than a distinct sound.
	/// </summary>
	private bool ShouldBeUsingPreviewFile;

	/// <summary>
	/// When using the music sound for the preview, the sample index of the preview start.
	/// </summary>
	private int PreviewStartSampleIndex;

	/// <summary>
	/// When using the music sound for the preview, the length in samples of the preview.
	/// </summary>
	private int PreviewLengthSamples;

	/// <summary>
	/// When using the music sound for the preview, the length in samples of the period over which to fade in.
	/// </summary>
	private int PreviewFadeInTimeSamples;

	/// <summary>
	/// When using the music sound for the preview, the length in samples of the period over which to fade out.
	/// </summary>
	private int PreviewFadeOutTimeSamples;

	/// <summary>
	/// The main volume.
	/// </summary>
	private float MainVolume = 1.0f;

	/// <summary>
	/// The music volume.
	/// </summary>
	private float MusicVolume = 1.0f;

	/// <summary>
	/// The assist tick volume.
	/// </summary>
	private float AssistTickVolume = 1.0f;

	/// <summary>
	/// The assist tick attack time in seconds.
	/// </summary>
	private float AssistTickAttackTime;

	/// <summary>
	/// Whether or not to play assist tick sounds.
	/// </summary>
	private bool UseAssistTick;

	/// <summary>
	/// Internal offset for the music.
	/// The offset is applied to the music but not the preview, as a way of visually synchronizing the music
	/// with the chart as it is played. The preview is played without visuals and should be started precisely
	/// at the specified time.
	/// </summary>
	private double MusicOffset;

	/// <summary>
	/// Constructor.
	/// </summary>
	/// <param name="soundManager">SoundManager.</param>
	/// <param name="musicOffset">Offset to use for playing the music.</param>
	public MusicManager(SoundManager soundManager, double musicOffset)
	{
		SoundManager = soundManager;
		SampleRate = SoundManager.GetSampleRate();
		SetMusicOffset(musicOffset);

		// Create a ChannelGroup for music and preview audio.
		SoundManager.CreateChannelGroup("DspChannelGroup", out var dspChannelGroup);

		AssistTickData = new SoundPlaybackState(new EditorSound(SoundManager, null, SampleRate, false));
		MusicData = new SoundPlaybackState(new EditorSound(SoundManager, new SoundMipMap(), SampleRate, true));
		PreviewData = new SoundPlaybackState(new EditorSound(SoundManager, null, SampleRate, true));

		SetPreviewParameters(0.0, 0.0, 0.0, 1.5);

		// Load the assist tick sound.
		AssistTickData.GetSound().LoadAsync("assist-tick.wav", false);

		// Create the DSP.
		SoundManager.CreateDsp(DspName, dspChannelGroup, DspRead, this);
	}

	/// <summary>
	/// Shut down the MusicManager.
	/// Disposes of any resources needing disposal.
	/// </summary>
	public void Shutdown()
	{
		SoundManager.DestroyDsp(DspName);
	}

	#region Accessors

	/// <summary>
	/// Gets the SoundMipMap for the currently loaded music.
	/// </summary>
	/// <returns>SoundMipMap.</returns>
	public SoundMipMap GetMusicMipMap()
	{
		return MusicData.GetSound().GetSoundMipMap();
	}

	/// <summary>
	/// Gets the length of the music in seconds.
	/// </summary>
	/// <returns>Length of the music in seconds.</returns>
	public double GetMusicLengthInSeconds()
	{
		return MusicData.GetSound().GetLengthInSeconds();
	}

	/// <summary>
	/// Gets the current time of the music in seconds.
	/// </summary>
	/// <returns>Current time of the music in seconds.</returns>
	public double GetMusicSongTime()
	{
		return GetTimeFromSampleIndex(SampleIndex, MusicOffset);
	}

	#endregion Accessors

	#region Loading

	/// <summary>
	/// Unload all loaded sounds asynchronously.
	/// </summary>
	public void UnloadAsync()
	{
		LoadSoundAsync(PreviewData, null);
		LoadSoundAsync(MusicData, null);
	}

	/// <summary>
	/// Loads the music audio file specified by the given path.
	/// If the given path is empty or negative then it is assumed that no music file
	/// is set.
	/// Will unload any previously loaded music sound file.
	/// Will not reload the previously loaded music file if called again with the same
	/// file.
	/// Will update the music SoundMipMap to reflect the music.
	/// </summary>
	/// <param name="fullPathToMusicFile">
	/// Full path to the audio file representing the music.
	/// </param>
	/// <param name="force">
	/// If true then the music will be loaded even if the given path is the same
	/// as the previously loaded music. If false, then the sound will not by loaded if
	/// the previously loaded music was from the same path.
	/// </param>
	/// <param name="generateMipMap">
	/// If true then generate a SoundMipMap for the music.
	/// </param>
	public void LoadMusicAsync(
		string fullPathToMusicFile,
		bool force = false,
		bool generateMipMap = true)
	{
		LoadSoundAsync(MusicData, fullPathToMusicFile, force, generateMipMap);
	}

	/// <summary>
	/// Loads the music preview audio file specified by the given path.
	/// If the given path is empty or negative then it is assumed that no preview file
	/// is set and the preview should be determined by a range over the music file.
	/// Will unload any previously loaded preview sound file.
	/// Will not reload the previously loaded preview file if called again with the same
	/// file.
	/// </summary>
	/// <param name="fullPathToMusicFile">
	/// Full path to the audio file representing the preview.
	/// </param>
	/// <param name="force">
	/// If true then the preview music will be loaded even if the given path is the same
	/// as the previously loaded preview. If false, then the sound will not by loaded if
	/// the previously loaded preview was from the same path.
	/// </param>
	public void LoadMusicPreviewAsync(string fullPathToMusicFile, bool force = false)
	{
		// Record that we should be using a unique preview file instead of
		// the music file if we were given a non-empty string.
		ShouldBeUsingPreviewFile = !string.IsNullOrEmpty(fullPathToMusicFile);
		LoadSoundAsync(PreviewData, fullPathToMusicFile, force);
	}

	/// <summary>
	/// Private helper to asynchronously load a SoundData object.
	/// Could mostly be internal to SoundData, but we set the time internally
	/// based on SetSoundPositionInternal, which takes into account MusicManager state.
	/// We set this before loading the SoundMipMap, in the middle of the async load.
	/// </summary>
	private static void LoadSoundAsync(
		SoundPlaybackState soundData,
		string fullPathToSoundFile,
		bool force = false,
		bool generateMipMap = true)
	{
		// It is common for Charts to re-use the same sound files.
		// Do not reload the sound file if we were already using it.
		if (!force && fullPathToSoundFile == soundData.GetSound().GetFile())
			return;

		soundData.GetSound().LoadAsync(fullPathToSoundFile, generateMipMap);
	}

	#endregion Loading

	#region Unit Conversion

	/// <summary>
	/// Given a time in seconds and an offset, return the sample index.
	/// </summary>
	/// <param name="time">Time in seconds.</param>
	/// <param name="offset">Offset in seconds.</param>
	/// <returns>Sample index.</returns>
	private int GetSampleIndexFromTime(double time, double offset)
	{
		return (int)((time + offset) * SampleRate);
	}

	/// <summary>
	/// Given a sample index and an offset, return the time in seconds.
	/// </summary>
	/// <param name="sampleIndex">Sample index.</param>
	/// <param name="offset">Offset in seconds.</param>
	/// <returns>Time in seconds.</returns>
	private double GetTimeFromSampleIndex(int sampleIndex, double offset)
	{
		return (double)sampleIndex / SampleRate - offset;
	}

	#endregion Unit Converstion

	#region Configuration

	/// <summary>
	/// Sets parameters used for playing the preview when it uses the music file instead
	/// of an independent file.
	/// </summary>
	/// <param name="startTime">Start time of preview in seconds.</param>
	/// <param name="length">Length of preview in seconds.</param>
	/// <param name="fadeInTime">Time in seconds over which to fade in the preview when playing.</param>
	/// <param name="fadeOutTime">Time in seconds over which to fade out the preview when playing.</param>
	public void SetPreviewParameters(double startTime, double length, double fadeInTime, double fadeOutTime)
	{
		PreviewStartSampleIndex = GetSampleIndexFromTime(startTime, 0.0);
		PreviewLengthSamples = GetSampleIndexFromTime(length, 0.0);
		PreviewFadeInTimeSamples = GetSampleIndexFromTime(fadeInTime, 0.0);
		PreviewFadeOutTimeSamples = GetSampleIndexFromTime(fadeOutTime, 0.0);
	}

	/// <summary>
	/// Sets the offset to be used when playing the music.
	/// Changes to this value will not have an effect until the next time the music is played.
	/// </summary>
	/// <param name="offset">New music offset value.</param>
	public void SetMusicOffset(double offset)
	{
		MusicOffset = offset;
	}

	/// <summary>
	/// Sets the main volume.
	/// </summary>
	/// <param name="volume">Desired volume. Will be clamped to be between 0.0f and 1.0f.</param>
	public void SetMainVolume(float volume)
	{
		MainVolume = Math.Clamp(volume, 0.0f, 1.0f);
	}

	/// <summary>
	/// Sets the music and preview volume.
	/// </summary>
	/// <param name="volume">Desired volume. Will be clamped to be between 0.0f and 1.0f.</param>
	public void SetMusicVolume(float volume)
	{
		MusicVolume = Math.Clamp(volume, 0.0f, 1.0f);
	}

	/// <summary>
	/// Sets the assist tick volume.
	/// </summary>
	/// <param name="volume">Desired volume. Will be clamped to be between 0.0f and 1.0f.</param>
	public void SetAssistTickVolume(float volume)
	{
		AssistTickVolume = Math.Clamp(volume, 0.0f, 1.0f);
	}

	/// <summary>
	/// Sets the attack time of the assist tick sound.
	/// </summary>
	/// <param name="attackTime">Assist tick attack time. Will be clamped to be at least 0.0f.</param>
	public void SetAssistTickAttackTime(float attackTime)
	{
		AssistTickAttackTime = Math.Max(0.0f, attackTime);
	}

	/// <summary>
	/// Sets whether or not to play assist tick sounds.
	/// </summary>
	/// <param name="useAssistTick">Whether or not to play assist tick sounds.</param>
	public void SetUseAssistTick(bool useAssistTick)
	{
		UseAssistTick = useAssistTick;
	}

	#endregion Configuration

	#region Playback Controls

	/// <summary>
	/// Start playing the music.
	/// </summary>
	/// <param name="musicTimeInSeconds">
	/// Desired music time in seconds.
	/// Can be negative and outside the time range of the music sound.
	/// </param>
	public void StartPlayback(double musicTimeInSeconds)
	{
		System.Diagnostics.Debug.Assert(State == PlayingState.PlayingNothing || State == PlayingState.PlayingMusic);
		State = PlayingState.PlayingMusic;

		lock (Lock)
		{
			var sampleIndex = GetSampleIndexFromTime(musicTimeInSeconds, MusicOffset);
			SampleIndex = sampleIndex;
			MusicData.StartPlaying(SampleIndex);
		}
	}

	/// <summary>
	/// Stop playing the music.
	/// </summary>
	public void StopPlayback()
	{
		State = PlayingState.PlayingNothing;
		MusicData.StopPlaying(true);
	}

	/// <summary>
	/// Start playing the preview.
	/// If the last call to LoadMusicPreviewAsync was for a non null and non empty file string,
	/// then the preview is assumed to be the audio file specified by that path.
	/// If the last call to LoadMusicPreviewAsync was for a null or empty file string, then the
	/// preview is assumed to be the music file using a range defined by the parameters set
	/// in SetPreviewParameters.
	/// </summary>
	/// <returns>
	/// True if the preview was successfully started and false otherwise.
	/// The preview may fail to play if the audio did not load successfully
	/// or an invalid preview range is defined.
	/// </returns>
	public bool StartPreviewPlayback()
	{
		System.Diagnostics.Debug.Assert(State == PlayingState.PlayingNothing);

		var neededSoundData = ShouldBeUsingPreviewFile ? PreviewData : MusicData;

		// If the sound file is not loaded yet, just ignore the request.
		if (!neededSoundData.GetSound().IsLoaded())
			return false;

		// Don't play anything if the range is not valid.
		if (!ShouldBeUsingPreviewFile && PreviewLengthSamples <= 0)
			return false;

		State = PlayingState.PlayingPreview;
		PreviewData.StartPlaying(ShouldBeUsingPreviewFile ? 0 : PreviewStartSampleIndex);

		return true;
	}

	/// <summary>
	/// Stop playing the preview.
	/// </summary>
	public void StopPreviewPlayback()
	{
		System.Diagnostics.Debug.Assert(State == PlayingState.PlayingPreview);
		PreviewData.StopPlaying();
		State = PlayingState.PlayingNothing;
	}

	/// <summary>
	/// Sets the music to the given time in seconds.
	/// If a preview is playing that uses the music file, then the given value will
	/// be set after the preview is stopped.
	/// The given time my be negative or outside the time range of the music sound.
	/// </summary>
	/// <param name="musicTimeInSeconds">The desired music time in seconds.</param>
	public void SetMusicTimeInSeconds(double musicTimeInSeconds)
	{
		lock (Lock)
		{
			SampleIndex = GetSampleIndexFromTime(musicTimeInSeconds, MusicOffset);
			SetSoundTimeInternal(MusicData, musicTimeInSeconds, MusicOffset);
		}
	}

	/// <summary>
	/// Private internal method for setting the music sound to a desired time in seconds.
	/// Used for both setting the time for the music and for the preview when the preview
	/// uses the music file instead of an independent preview file.
	/// The given time my be negative or outside the time range of the music sound.
	/// </summary>
	/// <param name="soundData">Sound data to set the time on.</param>
	/// <param name="timeInSeconds">Sound time in seconds.</param>
	/// <param name="offset">Offset to be added to the time.</param>
	private void SetSoundTimeInternal(SoundPlaybackState soundData, double timeInSeconds, double offset)
	{
		soundData.SetSampleIndex(GetSampleIndexFromTime(timeInSeconds, offset));
	}

	#endregion Playback Controls

	#region Update

	/// <summary>
	/// Perform time-dependent updates.
	/// </summary>
	public void Update(EditorChart chart)
	{
		// Set next assist tick times.
		UpdateNextAssistTickTimes(chart);
	}

	/// <summary>
	/// Updates the internal list of next assist tick times to consider when
	/// processing in the DSP. This list is meant to be small, while comfortably
	/// covering all ticks in the next DSP calls for a frame. This timing is loose
	/// so the list will be an overestimate, potentially including more ticks than
	/// needed.
	/// </summary>
	/// <param name="chart">The current EditorChart to add assist tick sounds for.</param>
	private void UpdateNextAssistTickTimes(EditorChart chart)
	{
		// Early out.
		if (!UseAssistTick)
		{
			NextAssistTickStartMusicSamples = null;
			return;
		}

		// When getting the next assist tick times we need to look ahead to ensure we capture enough
		// time so that the next tick is covered, and the next DSP callback is covered. One second
		// is very safely over both.
		const float lookAheadTime = 1.0f;

		// When getting the next assist tick times we need to account for potentially starting playback mid-tick.
		var precedingTimeCompensation = AssistTickData.GetSound().GetLengthInSeconds();

		var nextAssistTickStartMusicSamples = new List<int>();
		if (chart != null)
		{
			var chartEvents = chart.GetEvents();

			// We want to get the time that the music is playing, which is offset from the song time.
			// To accomplish this, we pass in a 0.0 offset parameter when getting the time.
			var currentSongTime = GetTimeFromSampleIndex(SampleIndex, 0.0) - precedingTimeCompensation;
			var currentChartTime = EditorPosition.GetChartTimeFromSongTime(chart, currentSongTime);
			var enumerator = chartEvents.FindFirstAfterChartTime(currentChartTime);
			if (enumerator != null)
			{
				var previousEventSoundTime = 0.0;
				while (enumerator.MoveNext())
				{
					var editorEvent = enumerator.Current;
					var chartTime = editorEvent!.GetChartTime();
					if (chartTime > currentChartTime + lookAheadTime)
						break;

					// Only tick for taps and holds.
					if (editorEvent is EditorTapNoteEvent || editorEvent is EditorHoldNoteEvent)
					{
						var songTime = EditorPosition.GetSongTimeFromChartTime(chart, chartTime);

						// It is very common for more than one event to occur at the same time. We only want
						// to record one time in this case.
						if (nextAssistTickStartMusicSamples.Count > 0 && songTime <= previousEventSoundTime)
							continue;
						previousEventSoundTime = songTime;

						// Record the assist tick time, taking into account the attack time.
						nextAssistTickStartMusicSamples.Add(GetSampleIndexFromTime(songTime - AssistTickAttackTime, 0.0));
					}
				}
			}
		}

		// Store the results into the NextAssistTickStartMusicSamples member for the DSP.
		lock (Lock)
		{
			NextAssistTickStartMusicSamples =
				nextAssistTickStartMusicSamples.Count > 0 ? nextAssistTickStartMusicSamples : null;
		}
	}

	#endregion Update

	#region DSP

	/// <summary>
	/// Callback from FMOD to render audio.
	/// From FMOD: This callback receives an input signal, allows the user to filter or process the data and write it to the output.
	/// </summary>
	/// <param name="dspState">
	/// DSP plugin state.
	/// </param>
	/// <param name="inBufferIntPtr">
	/// Incoming floating point -1.0 to +1.0 ranged data. Data will be interleaved if inChannels is greater than 1.
	/// </param>
	/// <param name="outBufferIntPtr">
	/// Outgoing floating point -1.0 to +1.0 ranged data. The dsp writer must write to this pointer else there will be silence.
	/// Data must be interleaved if outChannels is greater than 1.
	/// </param>
	/// <param name="length">
	/// Length of the incoming and outgoing buffers.
	/// Units: Samples.
	/// </param>
	/// <param name="inChannels">
	/// Number of channels of interleaved PCM data in the inBufferIntPtr parameter. Example: 1 = mono, 2 = stereo, 6 = 5.1.
	/// </param>
	/// <param name="outChannels">
	/// Number of channels of interleaved PCM data in the outBufferIntPtr parameter. Example: 1 = mono, 2 = stereo, 6 = 5.1.
	/// </param>
	/// <returns>
	/// FMOD RESULT.
	/// </returns>
	private unsafe RESULT DspRead(
		ref DSP_STATE dspState,
		IntPtr inBufferIntPtr,
		IntPtr outBufferIntPtr,
		uint length,
		int inChannels,
		ref int outChannels)
	{
		var outFloatBuffer = (float*)outBufferIntPtr.ToPointer();

		// Render the preview if we are playing it.
		if (RenderPreview(outFloatBuffer, length, ref outChannels))
			return RESULT.OK;

		// Otherwise, render the music and any ticks.
		RenderMusicAndTicks(outFloatBuffer, length, ref outChannels);
		return RESULT.OK;
	}

	/// <summary>
	/// Renders the preview into the given float buffer.
	/// </summary>
	/// <param name="buffer">Output float buffer.</param>
	/// <param name="length">Length of the buffer in samples.</param>
	/// <param name="outChannels">Number of channels in the buffer.</param>
	/// <returns>True if the preview is playing and rendered and false otherwise.</returns>
	private unsafe bool RenderPreview(float* buffer, uint length, ref int outChannels)
	{
		// Get the preview data.
		float[] previewData = null;
		var previewNumChannels = 0;
		int previewSampleIndex;
		var previewSoundStartSampleInclusive = 0;
		var previewSoundEndSampleExclusive = 0;
		bool previewPlaying;
		lock (PreviewData.GetLock())
		{
			previewPlaying = PreviewData.IsPlaying();
			previewSampleIndex = PreviewData.GetSampleIndex();
			if (previewPlaying)
			{
				// Get the preview data either from a preview file, or the music file.
				if (ShouldBeUsingPreviewFile)
				{
					(previewNumChannels, previewData) = PreviewData.GetSound().GetSampleData();
					previewSoundStartSampleInclusive = 0;
					if (previewData != null)
						previewSoundEndSampleExclusive = previewData.Length / previewNumChannels;
				}
				else
				{
					lock (MusicData.GetLock())
					{
						(previewNumChannels, previewData) = MusicData.GetSound().GetSampleData();
						previewSoundStartSampleInclusive = PreviewStartSampleIndex;
						previewSoundEndSampleExclusive = PreviewStartSampleIndex + PreviewLengthSamples;
					}
				}

				// Write at least as many channels as the preview sound contains.
				if (previewNumChannels > outChannels)
					outChannels = previewNumChannels;

				// Update the preview tracking for the next call.
				var endPreviewSample = (int)(previewSampleIndex + length);
				while (endPreviewSample >= previewSoundEndSampleExclusive)
				{
					endPreviewSample -= previewSoundEndSampleExclusive - previewSoundStartSampleInclusive;
				}

				PreviewData.SetSampleIndex(endPreviewSample);
			}
		}

		if (!previewPlaying)
			return false;

		// By default the preview plays at the music volume.
		var defaultVolume = MusicVolume * MainVolume;

		// If the preview is playing, render it.
		for (var relativeSampleIndex = 0; relativeSampleIndex < length; relativeSampleIndex++)
		{
			// Adjust the volume based on fading in and out.
			var volume = defaultVolume;
			if (previewSampleIndex > previewSoundEndSampleExclusive - PreviewFadeOutTimeSamples)
			{
				volume *= Interpolation.Lerp(1.0f, 0.0f, 0, PreviewFadeOutTimeSamples,
					previewSampleIndex - (previewSoundEndSampleExclusive - PreviewFadeOutTimeSamples));
			}
			else if (previewSampleIndex < previewSoundStartSampleInclusive + PreviewFadeInTimeSamples)
			{
				volume *= Interpolation.Lerp(0.0f, 1.0f, 0, PreviewFadeInTimeSamples,
					previewSampleIndex - previewSoundStartSampleInclusive);
			}

			// Write to the output buffer.
			for (var channelIndex = 0; channelIndex < outChannels; channelIndex++)
			{
				if (channelIndex < previewNumChannels)
				{
					buffer[relativeSampleIndex * outChannels + channelIndex] =
						previewData![previewSampleIndex * previewNumChannels + channelIndex] * volume;
				}
				else
				{
					buffer[relativeSampleIndex * outChannels + channelIndex] = 0.0f;
				}
			}

			// Advance preview sample index and loop if needed.
			previewSampleIndex++;
			if (previewSampleIndex >= previewSoundEndSampleExclusive)
			{
				previewSampleIndex -= previewSoundEndSampleExclusive - previewSoundStartSampleInclusive;
			}
		}

		return true;
	}

	/// <summary>
	/// Renders the music and any ticks into the given float buffer.
	/// </summary>
	/// <param name="buffer">Output float buffer.</param>
	/// <param name="length">Length of the buffer in samples.</param>
	/// <param name="outChannels">Number of channels in the buffer.</param>
	/// <returns>True if the music is playing and rendered and false otherwise.</returns>
	private unsafe bool RenderMusicAndTicks(float* buffer, uint length, ref int outChannels)
	{
		// Get the music data.
		float[] musicData;
		int musicNumChannels;
		var musicSampleIndex = 0;
		var lastMusicSampleToUseExclusive = 0;
		bool musicPlaying;
		var sampleIndexStartInclusive = 0;
		var sampleIndexEndExclusive = 0;
		lock (MusicData.GetLock())
		{
			(musicNumChannels, musicData) = MusicData.GetSound().GetSampleData();
			if (musicNumChannels > outChannels)
				outChannels = musicNumChannels;
			musicPlaying = MusicData.IsPlaying();

			// Update the sample indexes.
			if (musicPlaying)
			{
				// Update the sample index used for tracking the position of all sounds.
				lock (Lock)
				{
					musicSampleIndex = SampleIndex;
					sampleIndexStartInclusive = SampleIndex;
					sampleIndexEndExclusive = sampleIndexStartInclusive + (int)length;
					if (musicData != null)
						lastMusicSampleToUseExclusive = musicData.Length / musicNumChannels;
					SampleIndex += (int)length;
					MusicData.SetSampleIndex(SampleIndex);
				}
			}
		}

		// Get assist tick start times that are relevant for this callback.
		List<int> nextAssistTickTimes = null;
		var nextAssistTickIndex = 0;
		lock (Lock)
		{
			if (NextAssistTickStartMusicSamples != null)
			{
				nextAssistTickTimes = new List<int>(NextAssistTickStartMusicSamples.Count);
				foreach (var nextAssistTickTime in NextAssistTickStartMusicSamples)
				{
					// Intentionally include times which precede the sample range for this callback.
					if (nextAssistTickTime >= sampleIndexEndExclusive)
						break;
					nextAssistTickTimes.Add(nextAssistTickTime);
				}
			}
		}

		// Get the assist tick data.
		float[] assistTickData;
		int assistTickNumChannels;
		var assistTickNumSamples = 0;
		var assistTickSampleIndex = 0;
		var assistTickPlaying = false;
		lock (AssistTickData.GetLock())
		{
			(assistTickNumChannels, assistTickData) = AssistTickData.GetSound().GetSampleData();

			if (assistTickData != null)
			{
				// Capture assist tick sound data for rendering.
				assistTickNumSamples = assistTickData.Length / assistTickNumChannels;
				assistTickSampleIndex = AssistTickData.GetSampleIndex();
				assistTickPlaying = AssistTickData.IsPlaying();

				// If music isn't playing, the assist tick sound shouldn't play either.
				if (!musicPlaying)
				{
					AssistTickData.StopPlaying();
				}

				// If music is playing, then the assist tick sound may also play.
				else
				{
					// It is possible to start playing the music mid-tick. In this case
					// we should start playing the tick and set the sample index accordingly.
					if (!assistTickPlaying && nextAssistTickTimes != null)
					{
						// Check the potential ticks, which can start before this callback's sample range.
						for (var nextTickTimeIndex = 0; nextTickTimeIndex < nextAssistTickTimes.Count; nextTickTimeIndex++)
						{
							// If this tick starts within the sample range of this callback we are done checking.
							var potentialTickStartSample = nextAssistTickTimes[nextTickTimeIndex];
							if (potentialTickStartSample >= sampleIndexStartInclusive)
							{
								break;
							}

							// If the assist tick overlaps the start sample of this callback, it represents a tick
							// which should have started in the past that we need to partially render.
							if (potentialTickStartSample < sampleIndexStartInclusive
							    && potentialTickStartSample + assistTickNumSamples > sampleIndexStartInclusive)
							{
								assistTickPlaying = true;
								assistTickSampleIndex = sampleIndexStartInclusive - potentialTickStartSample;
							}
						}

						// If we found a partial tick that should be playing, update the AssistTickData.
						if (assistTickPlaying)
						{
							AssistTickData.StartPlaying(assistTickSampleIndex);
						}
					}

					// We need to advance the SampleIndex on the AssistTickData for the next callback.
					// This is done here instead of loop over all samples below to minimize the amount of work
					// done in the lock.
					// There may be multiple assist ticks played during this callback. We need to advance the
					// SampleIndex to the end of the final tick that will play during this callback.
					// The final tick will be denoted by the last index in NextAssistTickStartMusicSamples.
					var lastNextTickStartInRange = -1;
					if (nextAssistTickTimes != null)
					{
						for (var nextTickTimeIndex = nextAssistTickTimes.Count - 1; nextTickTimeIndex >= 0; nextTickTimeIndex--)
						{
							if (nextAssistTickTimes[nextTickTimeIndex] < sampleIndexEndExclusive)
							{
								lastNextTickStartInRange = nextAssistTickTimes[nextTickTimeIndex];
								break;
							}
						}
					}

					if (lastNextTickStartInRange >= 0)
					{
						AssistTickData.StartPlaying(sampleIndexEndExclusive - lastNextTickStartInRange);
					}
					else
					{
						if (AssistTickData.IsPlaying())
						{
							AssistTickData.SetSampleIndex(AssistTickData.GetSampleIndex() +
							                              (sampleIndexEndExclusive - sampleIndexStartInclusive));
						}
					}

					if (AssistTickData.GetSampleIndex() >= assistTickNumSamples)
					{
						AssistTickData.StopPlaying();
					}
				}
			}
		}

		// Early out now that we have updated the tracking members.
		if (!musicPlaying)
		{
			var outBufferLen = length * outChannels;
			for (var i = 0; i < outBufferLen; i++)
				buffer[i] = 0.0f;
			return false;
		}

		// Advance the next tick times such that the next one doesn't precede the sample range.
		if (nextAssistTickTimes != null)
		{
			while (nextAssistTickIndex < nextAssistTickTimes.Count && nextAssistTickTimes[nextAssistTickIndex] < musicSampleIndex)
			{
				nextAssistTickIndex++;
			}
		}

		// Render the music and the assist ticks together.
		var musicValuesForSample = new float[outChannels];
		var assistTickValuesForSample = new float[outChannels];
		for (var relativeSampleIndex = 0; relativeSampleIndex < length; relativeSampleIndex++)
		{
			// Check for starting a new assist tick.
			if (nextAssistTickTimes != null && nextAssistTickIndex < nextAssistTickTimes.Count)
			{
				if (musicSampleIndex == nextAssistTickTimes[nextAssistTickIndex])
				{
					assistTickSampleIndex = 0;
					assistTickPlaying = true;
					nextAssistTickIndex++;
				}
			}

			// Get the values for the music for this sample.
			if (musicSampleIndex >= 0 && musicSampleIndex < lastMusicSampleToUseExclusive)
			{
				for (var channelIndex = 0; channelIndex < outChannels; channelIndex++)
				{
					if (channelIndex < musicNumChannels)
					{
						musicValuesForSample[channelIndex] =
							musicData![musicSampleIndex * musicNumChannels + channelIndex] * MusicVolume;
					}
					else
					{
						musicValuesForSample[channelIndex] = 0.0f;
					}
				}
			}
			else
			{
				for (var channelIndex = 0; channelIndex < outChannels; channelIndex++)
				{
					musicValuesForSample[channelIndex] = 0.0f;
				}
			}

			// Get the values for the assist tick clap for this sample.
			if (UseAssistTick && assistTickPlaying)
			{
				for (var channelIndex = 0; channelIndex < outChannels; channelIndex++)
				{
					if (channelIndex < assistTickNumChannels)
					{
						assistTickValuesForSample[channelIndex] =
							assistTickData![assistTickSampleIndex * assistTickNumChannels + channelIndex] * AssistTickVolume;
					}
					else
					{
						assistTickValuesForSample[channelIndex] = 0.0f;
					}
				}

				assistTickSampleIndex++;
				if (assistTickSampleIndex >= assistTickNumSamples)
				{
					assistTickSampleIndex = 0;
					assistTickPlaying = false;
				}
			}

			// Render the results.
			for (var channelIndex = 0; channelIndex < outChannels; channelIndex++)
			{
				// There is no need to clamp here. FMOD will clamp later internally.
				buffer[relativeSampleIndex * outChannels + channelIndex] =
					(musicValuesForSample[channelIndex] + assistTickValuesForSample[channelIndex]) * MainVolume;
			}

			musicSampleIndex++;
		}

		return true;
	}

	#endregion DSP
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using FMOD;

namespace StepManiaEditor;

internal sealed class MusicDsp
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

	private class SoundData
	{
		public int NumChannels;
		public float[] Data;
		public int SampleIndex;
		public bool Playing;
		public readonly object Lock = new();
	}

	private readonly SoundManager SoundManager;
	private readonly Func<double> GetMusicTimeFunction;

	private readonly SoundData AssistTickData = new();
	private readonly SoundData MusicData = new();
	private int SampleIndex = 0;
	private List<int> NextAssistTickStartMusicSamples = null;
	private readonly object NextAssistTickTimesLock = new();

	// State.
	private PlayingState State = PlayingState.PlayingNothing;

	public MusicDsp(SoundManager soundManager, Func<double> getMusicTimeFunction)
	{
		SoundManager = soundManager;
		GetMusicTimeFunction = getMusicTimeFunction;
		LoadAssistTick();
		// TODO: Disposable?
		CreateDsp();
	}

	private async void LoadAssistTick()
	{
		var sound = await SoundManager.LoadAsync("clap.ogg");

		float[] assisTickData = null;
		int asistTickNumChannels = 0;

		await Task.Run(() => SoundManager.GetSamples(sound, out assisTickData, out asistTickNumChannels));

		lock (AssistTickData.Lock)
		{
			AssistTickData.Data = assisTickData;
			AssistTickData.NumChannels = asistTickNumChannels;
		}
	}

	private void CreateDsp()
	{
		SoundManager.CreateDsp("MusicDsp", DspRead, this);
	}

	private async void SetMusic(Sound? musicSound)
	{
		lock (MusicData.Lock)
		{
			MusicData.NumChannels = 0;
			MusicData.Data = null;
		}

		if (musicSound == null)
			return;

		float[] musicData = null;
		int musicNumChannels = 0;

		await Task.Run(() => SoundManager.GetSamples(musicSound.Value, out musicData, out musicNumChannels));

		lock (MusicData.Lock)
		{
			MusicData.Data = musicData;
			MusicData.NumChannels = musicNumChannels;
		}
	}

	private int GetMusicSampleIndexFromTime()
	{
		var musicTimeInSeconds = GetMusicTimeFunction();
		MusicData.
	}

	public void StartMusic()
	{
		System.Diagnostics.Debug.Assert(State == PlayingState.PlayingNothing || State == PlayingState.PlayingMusic);
		State = PlayingState.PlayingMusic;

		var musicTimeInSeconds = GetMusicTimeFunction();
		lock (MusicData.Lock)
		{
			MusicData.Playing = true;
			MusicData.SampleIndex = 0;
		}
	}

	public void StopMusic()
	{
		State = PlayingState.PlayingNothing;

		lock (MusicData.Lock)
		{
			MusicData.Playing = false;
			MusicData.SampleIndex = 0;
		}
	}

	public void Update(IReadOnlyEventTree chartEvents)
	{

		// Set next assist tick times.
		lock (NextAssistTickTimesLock)
		{
			NextAssistTickStartMusicSamples = null;
		}
	}

	private unsafe RESULT DspRead(
		ref DSP_STATE dsp_state,
		IntPtr inBufferIntPtr,
		IntPtr outBufferIntPtr,
		uint length,
		int inChannels,
		ref int outChannels)
	{
		// TODO: Will the number channels be a problem? Can we force it to 2?
		// TODO: Will different sample rates between the sounds be a problem? Can we force them to be equal?

		IntPtr userData;
		FMOD.DSP_STATE_FUNCTIONS functions = (DSP_STATE_FUNCTIONS)Marshal.PtrToStructure(dsp_state.functions, typeof(DSP_STATE_FUNCTIONS));
		functions.getuserdata(ref dsp_state, out userData);
		GCHandle objHandle = GCHandle.FromIntPtr(userData);
		MusicDsp obj = objHandle.Target as MusicDsp;

		int sampleIndexStartInclusive = 0;
		int sampleIndexEndExclusive = 0;

		float* outFloatBuffer = (float*)outBufferIntPtr.ToPointer();

		// Get music data.
		float[] musicData = null;
		int musicNumChannels = 0;
		int musicSampleIndex = 0;
		var musicNumSamples = 0;
		var lastMusicSampleToUseExclusive = 0;
		var musicPlaying = false;
		lock (MusicData.Lock)
		{
			if (MusicData.Data != null)
			{
				musicNumSamples = MusicData.Data.Length / MusicData.NumChannels;
				musicPlaying = MusicData.Playing;
				if (musicPlaying)
				{
					// Update the sample index used for tracking the position of all sounds.
					sampleIndexStartInclusive = SampleIndex;
					sampleIndexEndExclusive = sampleIndexStartInclusive + (int)length;
					SampleIndex += (int)length;

					musicData = MusicData.Data;
					musicNumChannels = MusicData.NumChannels;
					musicSampleIndex = MusicData.SampleIndex;

					// Update the sample index of the music.
					lastMusicSampleToUseExclusive = musicSampleIndex + (int)length;
					MusicData.SampleIndex += (int)length;
					if (MusicData.SampleIndex > musicNumSamples)
					{
						MusicData.SampleIndex = musicNumSamples;
						lastMusicSampleToUseExclusive = musicNumSamples;
					}
				}
			}
		}

		// Get assist tick start times that are relevant for this callback.
		List<int> nextAssistTickTimes = null;
		var nextAssistTickIndex = 0;
		lock (NextAssistTickTimesLock)
		{
			nextAssistTickTimes = NextAssistTickStartMusicSamples;
		}

		// Get the assist tick data.
		float[] assistTickData = null;
		var assistTickNumChannels = 0;
		var assistTickNumSamples = 0;
		var assistTickSampleIndex = 0;
		var assistTickPlaying = false;
		lock (AssistTickData.Lock)
		{
			if (AssistTickData.Data != null)
			{
				// Capture assist tick sound data for rendering.
				assistTickNumSamples = AssistTickData.Data.Length / AssistTickData.NumChannels;
				assistTickData = AssistTickData.Data;
				assistTickNumChannels = AssistTickData.NumChannels;
				assistTickSampleIndex = AssistTickData.SampleIndex;
				assistTickPlaying = AssistTickData.Playing;

				// If music isn't playing, the assist tick sound shouldn't play either.
				if (!musicPlaying)
				{
					AssistTickData.Playing = false;
					AssistTickData.SampleIndex = 0;
				}

				// If music is playing, then the assist tick sound may also play.
				else
				{
					// We need to advance the SampleIndex on the AssistTickData.
					// There may be multiple assist ticks played during this callback. We need to advance the
					// SampleIndex to the end of the final tick that will play during this callback.
					// The final tick will be denoted by the last index in NextAssistTickStartMusicSamples.
					var lastNextTickStartInRange = 0;
					for (var nextTickTimeIndex = nextAssistTickTimes.Count - 1; nextTickTimeIndex >= 0; nextTickTimeIndex--)
					{
						if (nextAssistTickTimes[nextTickTimeIndex] < sampleIndexEndExclusive)
						{
							lastNextTickStartInRange = nextAssistTickTimes[nextTickTimeIndex];
							break;
						}
					}

					// Update SampleIndex.
					if (lastNextTickStartInRange >= 0)
					{
						var remainingSamplesAfterLastNextTickStart = sampleIndexEndExclusive - lastNextTickStartInRange;
						AssistTickData.SampleIndex = remainingSamplesAfterLastNextTickStart;
					}
					else
					{
						if (AssistTickData.Playing)
						{
							AssistTickData.SampleIndex += (sampleIndexEndExclusive - sampleIndexStartInclusive);
						}
					}

					if (AssistTickData.SampleIndex >= assistTickNumSamples)
					{
						AssistTickData.SampleIndex = 0;
						AssistTickData.Playing = false;
					}
				}
			}
		}

		// Early out.
		if (!musicPlaying)
			return RESULT.OK;

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
			if (musicSampleIndex < lastMusicSampleToUseExclusive)
			{
				for (var channelIndex = 0; channelIndex < outChannels; channelIndex++)
				{
					if (channelIndex < musicNumChannels)
					{
						musicValuesForSample[channelIndex] = musicData[musicSampleIndex * musicNumChannels + channelIndex];
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
			if (assistTickPlaying)
			{
				for (var channelIndex = 0; channelIndex < outChannels; channelIndex++)
				{
					if (channelIndex < assistTickNumChannels)
					{
						assistTickValuesForSample[channelIndex] = assistTickData[assistTickSampleIndex * assistTickNumChannels + channelIndex];
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
				outFloatBuffer[relativeSampleIndex * outChannels + channelIndex] =
					musicValuesForSample[channelIndex] + assistTickValuesForSample[channelIndex];
			}

			musicSampleIndex++;
		}

		return RESULT.OK;
	}
}

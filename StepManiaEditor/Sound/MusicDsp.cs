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

	private class SoundData
	{
		public int NumChannels;
		public float[] Data;
		public int SampleIndex;
		public bool Playing;
		public readonly object Lock = new();
	}

	private readonly SoundManager SoundManager;

	private readonly SoundData AssistTickData = new();
	private readonly SoundData MusicData = new();
	private List<int> NextAssistTickStartMusicSamples = null;
	private readonly object NextAssistTickTimesLock = new();

	public MusicDsp(SoundManager soundManager, int bufferNumSamples)
	{
		SoundManager = soundManager;
		LoadAssistTick();
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
		IntPtr userData;
		FMOD.DSP_STATE_FUNCTIONS functions = (DSP_STATE_FUNCTIONS)Marshal.PtrToStructure(dsp_state.functions, typeof(DSP_STATE_FUNCTIONS));
		functions.getuserdata(ref dsp_state, out userData);
		GCHandle objHandle = GCHandle.FromIntPtr(userData);
		MusicDsp obj = objHandle.Target as MusicDsp;

		// Get music data.
		float[] musicData = null;
		int musicNumChannels = 0;
		int musicSampleIndex = 0;
		var musicNumSamples = MusicData.Data.Length / MusicData.NumChannels;
		var lastMusicSampleToUse = 0;
		var musicPlaying = false;
		lock (MusicData.Lock)
		{
			musicPlaying = MusicData.Playing;
			if (musicPlaying)
			{
				musicData = MusicData.Data;
				musicNumChannels = MusicData.NumChannels;
				musicSampleIndex = MusicData.SampleIndex;
				lastMusicSampleToUse = musicSampleIndex + (int)length - 1;
				MusicData.SampleIndex += (int)length;

				if (MusicData.SampleIndex > musicNumSamples)
				{
					MusicData.SampleIndex = musicNumSamples;
					lastMusicSampleToUse = musicNumSamples - 1;
				}
			}
		}

		// Get assist tick data.
		List<int> nextAssistTickTimesLock = null;
		lock (NextAssistTickTimesLock)
		{
			nextAssistTickTimesLock = NextAssistTickStartMusicSamples;
		}

		float[] assistTickData = null;
		int asistTickNumChannels = 0;
		lock (AssistTickData.Lock)
		{
			assistTickData = AssistTickData.Data;
			asistTickNumChannels = AssistTickData.NumChannels;

			if (!musicPlaying)
			{
				AssistTickData.Playing = false;
				AssistTickData.SampleIndex = 0;
			}
			else
			{

			}
		}

		if (!musicPlaying)
			return RESULT.OK;

		var musicValuesForSample = new float[outChannels];

		for (var sampleIndex = 0; sampleIndex < length; sampleIndex++)
		{
			// Get the values for the music for this sample.
			if (sampleIndex <= lastMusicSampleToUse)
			{
				for (var channelIndex = 0; channelIndex < outChannels; channelIndex++)
				{
					// TODO: Will the number channels be a problem? Can we force it to 2?
					if (channelIndex < musicNumChannels)
					{
						musicValuesForSample[channelIndex] = musicData[sampleIndex * musicNumChannels + channelIndex];
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
		}


		return RESULT.OK;
	}
}

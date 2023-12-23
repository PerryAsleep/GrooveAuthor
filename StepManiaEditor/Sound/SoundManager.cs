using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FMOD;
using Fumen;
using System.Runtime.InteropServices;

namespace StepManiaEditor;

public class SoundManager
{
	private FMOD.System System;
	private readonly Dictionary<string, DspHandle> DspHandles = new();

	private float[] AssistTickSamples;
	private int AssistTickNumChannels;
	private object AssistTickSampleIndexLock = new();
	private int AssistTickPlaybackSampleIndex = -1;

	public SoundManager()
	{
		ErrCheck(Factory.System_Create(out System));
		ErrCheck(System.init(100, INITFLAGS.NORMAL, IntPtr.Zero));
	}

	public unsafe RESULT DspRead(
		ref DSP_STATE dsp_state,
		IntPtr inBufferIntPtr,
		IntPtr outBufferIntPtr,
		uint length,
		int inChannels,
		ref int outChannels)
	{
		IntPtr userData;

		var assistTickSampleIndex = -1;
		var assistTickTotalSamples = 0;
		if (AssistTickNumChannels > 0 && AssistTickSamples != null)
			assistTickTotalSamples = AssistTickSamples.Length / AssistTickNumChannels;
		lock (AssistTickSampleIndexLock)
		{
			if (AssistTickPlaybackSampleIndex >= 0)
			{
				assistTickSampleIndex = AssistTickPlaybackSampleIndex;
				AssistTickPlaybackSampleIndex += (int)length;
				if (AssistTickPlaybackSampleIndex > assistTickTotalSamples)
				{
					AssistTickPlaybackSampleIndex = -1;
				}
			}
		}

		FMOD.DSP_STATE_FUNCTIONS functions = (DSP_STATE_FUNCTIONS)Marshal.PtrToStructure(dsp_state.functions, typeof(DSP_STATE_FUNCTIONS));
		functions.getuserdata(ref dsp_state, out userData);
		GCHandle objHandle = GCHandle.FromIntPtr(userData);
		SoundManager obj = objHandle.Target as SoundManager;

		//int lengthElements = (int)length * inChannels;
		//Marshal.Copy(inBufferIntPtr, obj.DspBuffer, 0, lengthElements);
		//Marshal.Copy(obj.DspBuffer, 0, outBufferIntPtr, lengthElements);



		var inBuffer = (float*)inBufferIntPtr.ToPointer();
		var outBuffer = (float*)outBufferIntPtr.ToPointer();

		for (var sampleIndex = 0; sampleIndex < length; sampleIndex++)
		{
			if (assistTickSampleIndex >= 0)
			{
				for (var channelIndex = 0; channelIndex < outChannels; channelIndex++)
				{
					var assistTickIndex = assistTickSampleIndex * AssistTickNumChannels +
					                      (Math.Min(channelIndex, AssistTickNumChannels - 1));
					var assistTickSample = AssistTickSamples[assistTickIndex];
					var inputSample = inBuffer[(sampleIndex * inChannels) + channelIndex];
					var outputSample = Math.Max(-1.0f, Math.Min(1.0f, inputSample + assistTickSample));

					outBuffer[(sampleIndex * inChannels) + channelIndex] = outputSample;
				}

				assistTickSampleIndex++;
				if (assistTickSampleIndex >= assistTickTotalSamples)
					assistTickSampleIndex = -1;
			}
			else
			{
				for (var channelIndex = 0; channelIndex < outChannels; channelIndex++)
				{
					outBuffer[(sampleIndex * inChannels) + channelIndex] = inBuffer[(sampleIndex * inChannels) + channelIndex];
				}
			}
		}

		return RESULT.OK;
	}

	public void CreateDsp(string name, DSP_READCALLBACK readCallback, object userData)
	{
		var handle = new DspHandle(System, readCallback, userData);
		DspHandles.Add(name, handle);

		ErrCheck(System.getMasterChannelGroup(out var mainGroup));
		ErrCheck(mainGroup.addDSP(0, handle.GetDsp()));
	}

	public void DestroyDsp(string name)
	{
		if (!DspHandles.TryGetValue(name, out var handle))
		{
			throw new ArgumentException($"No DSP found for {name}");
		}
		DspHandles.Remove(name);

		ErrCheck(System.getMasterChannelGroup(out var mainGroup));
		ErrCheck(mainGroup.removeDSP(handle!.GetDsp()));
	}

	public unsafe bool GetSamples(Sound sound, out float[] samples, out int numChannels)
	{
		numChannels = 0;
		samples = null;

		if (!ErrCheck(sound.getLength(out var length, TIMEUNIT.PCMBYTES)))
			return false;
		if (!ErrCheck(sound.getFormat(out _, out var format, out numChannels, out var bits)))
			return false;
		if (!ErrCheck(sound.@lock(0, length, out var ptr1, out var ptr2, out var len1, out var len2)))
			return false;

		// Early outs for data that can't be parsed.
		if (format != SOUND_FORMAT.PCM8
		    && format != SOUND_FORMAT.PCM16
		    && format != SOUND_FORMAT.PCM24
		    && format != SOUND_FORMAT.PCM32
		    && format != SOUND_FORMAT.PCMFLOAT)
		{
			Logger.Warn($"Unsupported sound format: {format:G}");
			ErrCheck(sound.unlock(ptr1, ptr2, len1, len2));
			return false;
		}

		if (numChannels < 1)
		{
			Logger.Warn($"Sound has {numChannels} channels. Expected at least one.");
			ErrCheck(sound.unlock(ptr1, ptr2, len1, len2));
			return false;
		}

		if (bits < 1)
		{
			Logger.Warn($"Sound has {bits} bits per sample. Expected at least one.");
			ErrCheck(sound.unlock(ptr1, ptr2, len1, len2));
			return false;
		}

		samples = new float[length];

		var ptr = (byte*)ptr1.ToPointer();

		var bitsPerSample = (uint)bits * (uint)numChannels;
		var bytesPerSample = bitsPerSample >> 3;
		var totalNumSamples = length / bytesPerSample;
		var bytesPerChannelPerSample = (uint)(bits >> 3);

		// Constants for converting sound formats to floats.
		const float invPcm8Max = 1.0f / byte.MaxValue;
		const float invPcm16Max = 1.0f / short.MaxValue;
		const float invPcm24Max = 1.0f / 8388607;
		const float invPcm32Max = 1.0f / int.MaxValue;

		// Get a function for parsing samples.
		// In practice this more performant than using the switch in the loop below.
		Func<long, float> parseSample;
		switch (format)
		{
			case SOUND_FORMAT.PCM8:
			{
				parseSample = i => ptr[i] * invPcm8Max;
				break;
			}
			case SOUND_FORMAT.PCM16:
			{
				parseSample = i => (ptr[i]
				                    + (short)(ptr[i + 1] << 8)) * invPcm16Max;
				break;
			}
			case SOUND_FORMAT.PCM24:
			{
				parseSample = i => (((ptr[i] << 8)
				                     + (ptr[i + 1] << 16)
				                     + (ptr[i + 2] << 24)) >> 8) * invPcm24Max;
				break;
			}
			case SOUND_FORMAT.PCM32:
			{
				parseSample = i => (ptr[i]
				                    + (ptr[i + 1] << 8)
				                    + (ptr[i + 2] << 16)
				                    + (ptr[i + 3] << 24)) * invPcm32Max;
				break;
			}
			case SOUND_FORMAT.PCMFLOAT:
			{
				parseSample = i => ((float*)ptr)[i >> 2];
				break;
			}

			default:
			{
				ErrCheck(sound.unlock(ptr1, ptr2, len1, len2));
				return false;
			}
		}

		var sampleIndex = 0;
		var outSampleIndex = 0;
		while (sampleIndex < totalNumSamples)
		{
			for (var channel = 0; channel < numChannels; channel++)
			{
				var byteIndex = sampleIndex * bytesPerSample + channel * bytesPerChannelPerSample;
				samples[outSampleIndex] = parseSample(byteIndex);
				outSampleIndex++;
			}
			sampleIndex++;
		}

		ErrCheck(sound.unlock(ptr1, ptr2, len1, len2));
		return true;
	}

	public async Task<Sound> LoadAsync(string fileName, MODE mode = MODE.DEFAULT)
	{
		return await Task.Run(() =>
		{
			ErrCheck(System.createSound(fileName, mode, out var sound), $"Failed to load {fileName}");
			return sound;
		});
	}

	public void CreateChannelGroup(string name, out ChannelGroup channelGroup)
	{
		ErrCheck(System.createChannelGroup(name, out channelGroup));
	}

	public void PlaySound(Sound sound, ChannelGroup channelGroup, out Channel channel)
	{
		ErrCheck(System.playSound(sound, channelGroup, true, out channel));
	}

	public void Update()
	{
		ErrCheck(System.update());
	}

	public static bool ErrCheck(RESULT result, string failureMessage = null)
	{
		if (result != RESULT.OK)
		{
			if (!string.IsNullOrEmpty(failureMessage))
			{
				Logger.Error($"{failureMessage} {result:G}");
			}
			else
			{
				Logger.Error($"FMOD error: {result:G}");
			}

			return false;
		}

		return true;
	}
}

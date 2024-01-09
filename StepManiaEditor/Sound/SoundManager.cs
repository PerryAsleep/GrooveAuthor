using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FMOD;
using Fumen;

namespace StepManiaEditor;

/// <summary>
/// Class for managing low-level sound functionality.
/// Wraps FMOD functionality.
/// Expected Usage:
///  Call Update once each frame.
///  If creating a DSP through CreateDSP, call DestroyDsp later to dispose of it.
/// </summary>
public class SoundManager
{
	/// <summary>
	/// FMOD System.
	/// </summary>
	private FMOD.System System;

	/// <summary>
	/// All DspHandles.
	/// </summary>
	private readonly Dictionary<string, DspHandle> DspHandles = new();

	/// <summary>
	/// Constructor.
	/// </summary>
	/// <param name="dspBufferSize">Size of the DSP buffers in samples.</param>
	/// <param name="dspNumBuffers">Number of DSP buffers.</param>
	public SoundManager(uint dspBufferSize, int dspNumBuffers)
	{
		ErrCheck(Factory.System_Create(out System));
		ErrCheck(System.setDSPBufferSize(dspBufferSize, dspNumBuffers));
		ErrCheck(System.init(100, INITFLAGS.NORMAL, IntPtr.Zero));
		//System.setOutput(OUTPUTTYPE.WAVWRITER);
	}

	/// <summary>
	/// Asynchronously load a sound file from disk into an FMOD Sound.
	/// </summary>
	/// <param name="fileName"></param>
	/// <param name="mode"></param>
	/// <returns>Loaded Sound.</returns>
	public async Task<Sound> LoadAsync(string fileName, MODE mode = MODE.DEFAULT)
	{
		return await Task.Run(() =>
		{
			ErrCheck(System.createSound(fileName, mode, out var sound), $"Failed to load {fileName}");
			return sound;
		});
	}

	/// <summary>
	/// Creates a ChannelGroup identified by the given name.
	/// </summary>
	/// <param name="name">Name of the ChannelGroup.</param>
	/// <param name="channelGroup">Created ChannelGroup.</param>
	public void CreateChannelGroup(string name, out ChannelGroup channelGroup)
	{
		ErrCheck(System.createChannelGroup(name, out channelGroup));
	}

	/// <summary>
	/// Plays a Sound on the given ChannelGroup.
	/// </summary>
	/// <param name="sound">Sound to play.</param>
	/// <param name="channelGroup">ChannelGroup to play the Sound on.</param>
	/// <param name="channel">Channel assigned to the Sound.</param>
	public void PlaySound(Sound sound, ChannelGroup channelGroup, out Channel channel)
	{
		ErrCheck(System.playSound(sound, channelGroup, true, out channel));
	}

	/// <summary>
	/// Perform time-dependent updates.
	/// </summary>
	public void Update()
	{
		// Update the FMOD System.
		ErrCheck(System.update());
	}

	/// <summary>
	/// Checks the given FMOD RESULT for error values and logs error messages.
	/// </summary>
	/// <param name="result">FMOD RESULT.</param>
	/// <param name="failureMessage">Optional message to append to error log messages.</param>
	/// <returns>True if the results is not an error and false if it is an error.</returns>
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

	#region Resampling

	/// <summary>
	/// Gets the sample rate of the engine that all sounds ultimately process at.
	/// </summary>
	/// <returns>System sample rate.</returns>
	public uint GetSampleRate()
	{
		ErrCheck(System.getSoftwareFormat(out var sampleRate, out var _, out var _));
		return (uint)sampleRate;
	}

	/// <summary>
	/// Allocates a buffer to hold PCM float data for the given sound at the given sample rate.
	/// The resulting buffer can then be filled using FillSamplesAsync.
	/// </summary>
	/// <param name="sound">Sound to allocate the buffer for.</param>
	/// <param name="sampleRate">The desired output sample rate.</param>
	/// <param name="samples">Buffer of PCM float data to allocate.</param>
	/// <param name="numChannels">Number of channels in the buffer.</param>
	/// <returns>True if successful and false if an error was encountered.</returns>
	public static bool AllocateSampleBuffer(Sound sound, uint sampleRate, out float[] samples, out int numChannels)
	{
		numChannels = 0;
		samples = null;

		if (!ErrCheck(sound.getDefaults(out var soundFrequency, out var _)))
			return false;
		var inputSampleRate = (uint)soundFrequency;
		if (!ErrCheck(sound.getLength(out var inputLength, TIMEUNIT.PCMBYTES)))
			return false;
		if (!ErrCheck(sound.getFormat(out _, out var format, out numChannels, out var bits)))
			return false;
		if (!ErrCheck(sound.@lock(0, inputLength, out var ptr1, out var ptr2, out var len1, out var len2)))
			return false;

		// Early out for data that can't be parsed.
		if (!ValidateFormat(format, numChannels, bits))
		{
			ErrCheck(sound.unlock(ptr1, ptr2, len1, len2));
			return false;
		}

		var bitsPerSample = (uint)bits * (uint)numChannels;
		var bytesPerSample = bitsPerSample >> 3;
		var totalNumInputSamples = inputLength / bytesPerSample;
		var sampleRateRatio = (double)sampleRate / inputSampleRate;
		var totalNumOutputSamples = (uint)(totalNumInputSamples * sampleRateRatio);
		var outputLength = totalNumOutputSamples * (uint)numChannels;
		samples = new float[outputLength];

		ErrCheck(sound.unlock(ptr1, ptr2, len1, len2));
		return true;
	}

	/// <summary>
	/// Helper function for validating SOUND_FORMAT and other common properties of a Sound needed
	/// for parsing its data.
	/// </summary>
	/// <param name="format">SOUND_FORMAT of the Sound.</param>
	/// <param name="numChannels">Number of channels of the Sound.</param>
	/// <param name="bitsPerSample">Bits per sample of the Sound</param>
	/// <returns>True if the data is valid for parsing and false otherwise.</returns>
	public static bool ValidateFormat(SOUND_FORMAT format, int numChannels, int bitsPerSample)
	{
		// Early outs for data that can't be parsed.
		if (format != SOUND_FORMAT.PCM8
		    && format != SOUND_FORMAT.PCM16
		    && format != SOUND_FORMAT.PCM24
		    && format != SOUND_FORMAT.PCM32
		    && format != SOUND_FORMAT.PCMFLOAT)
		{
			Logger.Warn($"Unsupported sound format: {format:G}");
			return false;
		}

		if (numChannels < 1)
		{
			Logger.Warn($"Sound has {numChannels} channels. Expected at least one.");
			return false;
		}

		if (bitsPerSample < 1)
		{
			Logger.Warn($"Sound has {bitsPerSample} bits per sample. Expected at least one.");
			return false;
		}

		return true;
	}

	/// <summary>
	/// Gets a function to use for parsing a sample out of a byte array into floats.
	/// </summary>
	/// <param name="format">SOUND_FORMAT of the byte array.</param>
	/// <param name="ptr">Byte array.</param>
	/// <returns>Function for parsing the byte array into floats.</returns>
	public static unsafe Func<long, float> GetParseSampleFunc(SOUND_FORMAT format, byte* ptr)
	{
		// Constants for converting sound formats to floats.
		const float invPcm8Max = 1.0f / byte.MaxValue;
		const float invPcm16Max = 1.0f / short.MaxValue;
		const float invPcm24Max = 1.0f / 8388607;
		const float invPcm32Max = 1.0f / int.MaxValue;

		// Get a function for parsing samples.
		// In practice this more performant than using the switch in the loop below.
		switch (format)
		{
			case SOUND_FORMAT.PCM8:
			{
				return i => ptr[i] * invPcm8Max;
			}
			case SOUND_FORMAT.PCM16:
			{
				return i => (ptr[i]
				             + (short)(ptr[i + 1] << 8)) * invPcm16Max;
			}
			case SOUND_FORMAT.PCM24:
			{
				return i => (((ptr[i] << 8)
				              + (ptr[i + 1] << 16)
				              + (ptr[i + 2] << 24)) >> 8) * invPcm24Max;
			}
			case SOUND_FORMAT.PCM32:
			{
				return i => (ptr[i]
				             + (ptr[i + 1] << 8)
				             + (ptr[i + 2] << 16)
				             + (ptr[i + 3] << 24)) * invPcm32Max;
			}
			case SOUND_FORMAT.PCMFLOAT:
			{
				return i => ((float*)ptr)[i >> 2];
			}
			default:
			{
				return null;
			}
		}
	}

	/// <summary>
	/// Fills a buffer allocated previously from AllocateSampleBuffer with float PCM data
	/// from the given sound at the given sample rate.
	/// </summary>
	/// <param name="sound">Sound to read the sample data from.</param>
	/// <param name="sampleRate">Sample rate of the buffer.</param>
	/// <param name="samples">Buffer to fill.</param>
	/// <param name="numChannels">Number of channels in the buffer.</param>
	/// <param name="token">CancellationToken for cancelling the work.</param>
	/// <returns>True if successful and false if an error was encountered.</returns>
	public static async Task<bool> FillSamplesAsync(Sound sound, uint sampleRate, float[] samples, int numChannels,
		CancellationToken token)
	{
		var result = false;
		await Task.Run(() =>
		{
			try
			{
				result = FillSamples(sound, sampleRate, samples, numChannels, token);
			}
			catch (OperationCanceledException)
			{
				// Ignored.
			}
		}, token);
		token.ThrowIfCancellationRequested();
		return result;
	}

	/// <summary>
	/// Fills a buffer allocated previously from AllocateSampleBuffer with float PCM data
	/// from the given sound at the given sample rate.
	/// </summary>
	/// <param name="sound">Sound to read the sample data from.</param>
	/// <param name="sampleRate">Sample rate of the buffer.</param>
	/// <param name="samples">Buffer to fill.</param>
	/// <param name="numChannels">Number of channels in the buffer.</param>
	/// <param name="token">CancellationToken for cancelling the work.</param>
	/// <returns>True if successful and false if an error was encountered.</returns>
	private static unsafe bool FillSamples(Sound sound, uint sampleRate, float[] samples, int numChannels,
		CancellationToken token)
	{
		if (!ErrCheck(sound.getDefaults(out var soundFrequency, out var _)))
			return false;
		var inputSampleRate = (uint)soundFrequency;
		if (!ErrCheck(sound.getLength(out var inputLength, TIMEUNIT.PCMBYTES)))
			return false;
		if (!ErrCheck(sound.getFormat(out _, out var format, out var soundNumChannels, out var bits)))
			return false;
		if (soundNumChannels != numChannels)
		{
			Logger.Warn($"Provided channel count {numChannels} does not match expected channel count {soundNumChannels}.");
			return false;
		}

		if (!ErrCheck(sound.@lock(0, inputLength, out var ptr1, out var ptr2, out var len1, out var len2)))
			return false;

		// Early out for data that can't be parsed.
		if (!ValidateFormat(format, numChannels, bits))
		{
			ErrCheck(sound.unlock(ptr1, ptr2, len1, len2));
			return false;
		}

		var ptr = (byte*)ptr1.ToPointer();

		var bitsPerSample = (uint)bits * (uint)numChannels;
		var bytesPerSample = bitsPerSample >> 3;
		var bytesPerChannelPerSample = (uint)(bits >> 3);
		var totalNumInputSamples = inputLength / bytesPerSample;
		var sampleRateRatio = (double)sampleRate / inputSampleRate;
		var totalNumOutputSamples = (uint)(totalNumInputSamples * sampleRateRatio);
		var outputLength = totalNumOutputSamples * (uint)numChannels;
		if (samples.Length != outputLength)
		{
			Logger.Warn($"Provided buffer length {samples.Length} does match expected buffer length {outputLength}.");
			ErrCheck(sound.unlock(ptr1, ptr2, len1, len2));
			return false;
		}

		// To resample we perform four point hermite spline interpolation.
		// Set up data for storing the source points for resampling.
		const int numHermitePoints = 4;
		var hermiteTimeRange = 1.0f / inputSampleRate;
		var hermitePoints = new float[numHermitePoints];
		var maxInputSampleIndex = len1 - bytesPerSample;

		// Get a function for parsing samples.
		var parseSample = GetParseSampleFunc(format, ptr);

		try
		{
			var sampleIndex = 0;
			while (sampleIndex < totalNumOutputSamples)
			{
				// Determine the time of the desired sample.
				var t = (double)sampleIndex / sampleRate;
				// Find the start of the four points in the original data corresponding to this time
				// so we can use them for hermite spline interpolation. Note the minus 1 here is to
				// account for four samples. The floor and the minus one result in getting the sample
				// two indexes preceding the desired time.
				var startInputSampleIndex = (int)(t * inputSampleRate) - 1;
				// Determine the time of the x1 sample in order to find the normalized time.
				var x1Time = (double)(startInputSampleIndex + 1) / inputSampleRate;
				// Determine the normalized time for the interpolation.
				var normalizedTime = (float)((t - x1Time) / hermiteTimeRange);

				for (var channel = 0; channel < numChannels; channel++)
				{
					var inputChannelOffset = channel * bytesPerChannelPerSample;

					// Get all four input points for the interpolation.
					for (var hermiteIndex = 0; hermiteIndex < numHermitePoints; hermiteIndex++)
					{
						// Get the input index. We need to clamp as it is expected at the ends for the range to exceed the
						// range of the input data.
						var inputIndex = Math.Clamp((startInputSampleIndex + hermiteIndex) * bytesPerSample + inputChannelOffset,
							0, maxInputSampleIndex);
						// Parse the sample at this index.
						// This often results in redundant parses, but in practice optimizing them out isn't a big gain
						// and it adds a lot of complexity. The main perf hit is InterpolateHermite.
						hermitePoints[hermiteIndex] = parseSample(inputIndex);
					}

					// Now that all four samples are known, interpolate them and store the result.
					samples[sampleIndex * numChannels + channel] = Interpolation.HermiteInterpolate(hermitePoints[0],
						hermitePoints[1], hermitePoints[2], hermitePoints[3], normalizedTime);
				}

				sampleIndex++;

				// Periodically check for cancellation.
				if (sampleIndex % 524288 == 0)
					token.ThrowIfCancellationRequested();
			}
		}
		finally
		{
			ErrCheck(sound.unlock(ptr1, ptr2, len1, len2));
		}

		return true;
	}

	#endregion Resampling

	#region DSP

	/// <summary>
	/// Creates a new DSP on the master ChannelGroup.
	/// The given readCallback will be called with the given userData for DSP processing.
	/// It is expected that DestroyDsp is called later to clean up the DSP.
	/// </summary>
	/// <param name="name">DSP name identifier. Must be unique.</param>
	/// <param name="readCallback">DSP_READCALLBACK to invoke for DSP processing.</param>
	/// <param name="userData">User data to pass into the callback.</param>
	public void CreateDsp(string name, DSP_READCALLBACK readCallback, object userData)
	{
		ErrCheck(System.getMasterChannelGroup(out var mainGroup));
		CreateDsp(name, mainGroup, readCallback, userData);
	}

	/// <summary>
	/// Creates a new DSP on the given ChannelGroup.
	/// The given readCallback will be called with the given userData for DSP processing.
	/// It is expected that DestroyDsp is called later to clean up the DSP.
	/// </summary>
	/// <param name="name">DSP name identifier. Must be unique.</param>
	/// <param name="channelGroup">ChannelGroup to attach the DSP to.</param>
	/// <param name="readCallback">DSP_READCALLBACK to invoke for DSP processing.</param>
	/// <param name="userData">User data to pass into the callback.</param>
	public void CreateDsp(string name, ChannelGroup channelGroup, DSP_READCALLBACK readCallback, object userData)
	{
		var handle = new DspHandle(System, readCallback, userData);
		DspHandles.Add(name, handle);
		ErrCheck(channelGroup.addDSP(0, handle.GetDsp()));
	}

	/// <summary>
	/// Destroys the DSP identified by the given name.
	/// </summary>
	/// <param name="name">DSP name.</param>
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

	#endregion DSP
}

using System;
using System.Threading;
using System.Threading.Tasks;
using FMOD;
using Fumen;

namespace StepManiaEditor;

/// <summary>
/// A SoundMipMap is mip mapped sound data used for visualizing a sound waveform.
/// The data at each mip level represents pre-computed values a range of underlying
/// samples.
/// This data allows for performant rendering of waveform data by minimizing the number
/// of samples that need to be looped over when rendering an arbitrary range of
/// samples per pixel.
/// </summary>
public class SoundMipMap
{
	/// <summary>
	/// Data stored at one mip level.
	/// An array of 32 bit uints where each uint represents data for a range of underlying audio samples.
	/// Each uint holds three values which can be get and set using the public static methods of this class:
	///  - A minimum value representing the lowest value of the underlying samples.
	///  - A maximum value representing the highest value of the underlying samples.
	///  - A distance value representing the sum of the deltas between each underlying sample from its
	///	previous sample divided by the number of samples. This is stored as a ratio rather than a
	///	sum in order to require less memory.
	/// All values are in the range of [0-R) where R is the total x pixel range.
	/// At each mip level, the number of samples represented by each SampleDataPerChannel entry
	/// is 2 to the power of the mip level. For example at mip level 0 each uint
	/// entry corresponds to 1 sample. At mip level 1 it is 2 samples, at 2 it is 4, etc.
	/// </summary>
	public class MipLevel
	{
		/// <summary>
		/// 9 bits of storage for each of the two range values.
		/// </summary>
		public const int MaximumRange = 0x1FF;

		/// <summary>
		/// 14 bits of storage for the distance/samples value.
		/// </summary>
		public const int MaximumDistanceRange = 0x3FFF;

		/// <summary>
		/// All data at this mip level.
		/// </summary>
		public readonly uint[] Data;

		/// <summary>
		/// Constructor. Will allocate data and set appropriate defaults to start filling.
		/// </summary>
		/// <param name="numSamples">Number of samples at this mip level.</param>
		/// <param name="numChannels">Number of channels of audio per sample.</param>
		public MipLevel(uint numSamples, uint numChannels)
		{
			var len = numSamples * numChannels;
			Data = new uint[len];

			// Set the min to the maximum value that can be stored in 9 bits: 0x1FF.
			// Set the max to the minimum value: 0x0.
			// Set the distance to the minimum value: 0x0.
			for (var i = 0; i < len; i++)
				Data[i] = 0x1FF;
		}

		public static ushort GetMin(uint sampleData)
		{
			return (ushort)(sampleData & 0x1FF);
		}

		public static void SetMin(ref uint sampleData, ushort min)
		{
			sampleData = (sampleData & 0xFFFFFE00) | min;
		}

		public static ushort GetMax(uint sampleData)
		{
			return (ushort)((sampleData & 0x3FE00) >> 9);
		}

		public static void SetMax(ref uint sampleData, ushort max)
		{
			sampleData = (sampleData & 0xFFFC01FF) | ((uint)max << 9);
		}

		public static ushort GetDistanceOverSamples(uint sampleData)
		{
			return (ushort)((sampleData & 0xFFFC0000) >> 18);
		}

		public static void SetDistanceOverSamples(ref uint sampleData, ushort d)
		{
			sampleData = (sampleData & 0x3FFFF) | ((uint)d << 18);
		}
	}

	/// <summary>
	/// MipLevel array containing all data.
	/// </summary>
	public MipLevel[] MipLevels;

	/// <summary>
	/// Number of channels of underlying Sound.
	/// </summary>
	private uint NumChannels;

	/// <summary>
	/// Whether or not the MipLevels data has been allocated.
	/// </summary>
	private bool MipLevelsAllocated;

	/// <summary>
	/// Whether or not the MipLevels data is fully populated after allocation.
	/// </summary>
	private bool MipMapDataLoaded;

	/// <summary>
	/// number of Tasks to use to parallelize the mip map creation work.
	/// </summary>
	private int LoadParallelism = 1;

	/// <summary>
	/// Cached sound sample rate in Hz.
	/// </summary>
	private uint SampleRate;

	/// <summary>
	/// Object to use for locking when creating and destroying MipLevels.
	/// This class exposes MipLevels publicly. It is expected that other
	/// users of MipLevels lock this object when they need it so that this
	/// class does not destroy or create MipLevels while they are being used.
	/// We don't need to lock while editing the data in MipLevels, as only
	/// this class writes to it, but we need to lock when we are going to
	/// delete the data.
	/// We could have other classes cache MipLevels but we want to control
	/// when this data is deleted with a manual Garbage Collector Collect because
	/// otherwise we could see memory usage double when resetting and loading
	/// a new song. As this data can be multiple GBs, we want to avoid that.
	/// </summary>
	private readonly object MipLevelsLock = new();

	/// <summary>
	/// Public method for externally locking MipLevels.
	/// </summary>
	/// <param name="lockTaken">Whether or not the lock was taken.</param>
	public void TryLockMipLevels(ref bool lockTaken)
	{
		Monitor.TryEnter(MipLevelsLock, ref lockTaken);
	}

	/// <summary>
	/// Public method for externally unlocking MipLevels.
	/// </summary>
	public void UnlockMipLevels()
	{
		Monitor.Exit(MipLevelsLock);
	}

	/// <summary>
	/// Reset the SoundMipMap.
	/// Will invoke a Collect on the GarbageCollector to free MipLevels data.
	/// </summary>
	public void Reset()
	{
		// Clear data.
		bool shouldGarbageCollect;
		lock (MipLevelsLock)
		{
			shouldGarbageCollect = MipLevels != null;
			MipLevelsAllocated = false;
			MipMapDataLoaded = false;
			MipLevels = null;
			NumChannels = 0;
		}

		// Garbage collect to free the MipLevels data.
		if (shouldGarbageCollect)
			GC.Collect();
	}

	/// <summary>
	/// Sets the number of Tasks to use to parallelize the mip map creation work.
	/// </summary>
	/// <param name="loadParallelism">Number of Tasks to use to parallelize the mip map creation work</param>
	public void SetLoadParallelism(int loadParallelism)
	{
		LoadParallelism = loadParallelism;
	}

	/// <summary>
	/// Returns the number of samples at the lowest mip level.
	/// Returns 0 if the Sound has not been loaded or the mip map data generation failed.
	/// </summary>
	/// <returns>Number of samples at the lowest mip level.</returns>
	public uint GetMipLevel0NumSamples()
	{
		if (NumChannels == 0 || MipLevels == null || MipLevels.Length == 0)
			return 0;
		return (uint)MipLevels[0].Data.Length / NumChannels;
	}

	/// <summary>
	/// Returns the number of channels of the Sound.
	/// </summary>
	/// <returns>Number of channels of the Sound.</returns>
	public uint GetNumChannels()
	{
		return NumChannels;
	}

	/// <summary>
	/// Returns whether or not the mip map data has been fully generated.
	/// </summary>
	/// <returns>Whether or not the mip map data has been fully generated.</returns>
	public bool IsMipMapDataLoaded()
	{
		return MipMapDataLoaded;
	}

	/// <summary>
	/// Returns whether or not the mip map data has been allocated.
	/// </summary>
	/// <returns>Whether or not the mip map data has been allocated.</returns>
	public bool IsMipMapDataAllocated()
	{
		return MipLevelsAllocated;
	}

	/// <summary>
	/// Returns the sample rate in hz of the Sound.
	/// </summary>
	/// <returns>Sample rate in hz of the Sound.</returns>
	public uint GetSampleRate()
	{
		return SampleRate;
	}

	/// <summary>
	/// Creates the mip map data for the given Sound.
	/// </summary>
	/// <param name="sound">Sound to create mip map data for.</param>
	/// <param name="sampleRate">Sample rate of sound in hz.</param>
	/// <param name="xRange">Total x range in pixels for all channels.</param>
	/// <param name="token">CancellationToken for cancelling this Task.</param>
	public async Task CreateMipMapAsync(Sound sound, uint sampleRate, int xRange, CancellationToken token)
	{
		if (!sound.hasHandle())
		{
			Logger.Warn("Cannot create sound mip map data. Invalid sound handle.");
			return;
		}

		SampleRate = sampleRate;

		Logger.Info("Generating sound mip map...");
		var success = await Task.Run(async () => await InternalCreateMipMapAsync(sound, xRange, token), token);
		if (!success)
		{
			Logger.Warn("Failed generating sound mip map.");
			return;
		}

		Logger.Info("Finished generating sound mip map.");
		MipMapDataLoaded = true;
	}

	/// <summary>
	/// Creates and fills out the MipLevels for the loaded Sound.
	/// </summary>
	/// <remarks>
	/// Expect this method to take a long time as it must loop over all samples and
	/// update all mip level data.
	/// </remarks>
	/// <param name="sound">Sound to create mip map data for.</param>
	/// <param name="xRange">Total x range in pixels for all channels.</param>
	/// <param name="token">CancellationToken for cancelling this Task.</param>
	/// <returns>True if successful and false otherwise.</returns>
	private async Task<bool> InternalCreateMipMapAsync(Sound sound, int xRange, CancellationToken token)
	{
		// Get the sound data from FMOD.
		if (!SoundManager.ErrCheck(sound.getLength(out var length, TIMEUNIT.PCMBYTES)))
			return false;
		if (!SoundManager.ErrCheck(sound.getFormat(out _, out var format, out var numChannels, out var bits)))
			return false;
		if (!SoundManager.ErrCheck(sound.@lock(0, length, out var ptr1, out var ptr2, out var len1, out var len2)))
			return false;
		var soundNeedsUnlock = true;

		try
		{
			// Early outs for data that can't be parsed.
			if (format != SOUND_FORMAT.PCM8
			    && format != SOUND_FORMAT.PCM16
			    && format != SOUND_FORMAT.PCM24
			    && format != SOUND_FORMAT.PCM32
			    && format != SOUND_FORMAT.PCMFLOAT)
			{
				Logger.Warn($"Unsupported sound format: {format:G}");
				SoundManager.ErrCheck(sound.unlock(ptr1, ptr2, len1, len2));
				return false;
			}

			if (numChannels < 1)
			{
				Logger.Warn($"Sound has {numChannels} channels. Expected at least one.");
				SoundManager.ErrCheck(sound.unlock(ptr1, ptr2, len1, len2));
				return false;
			}

			if (bits < 1)
			{
				Logger.Warn($"Sound has {bits} bits per sample. Expected at least one.");
				SoundManager.ErrCheck(sound.unlock(ptr1, ptr2, len1, len2));
				return false;
			}

			NumChannels = (uint)numChannels;

			var xRangePerChannel = xRange / numChannels;
			var bitsPerSample = (uint)bits * (uint)numChannels;
			var bytesPerSample = bitsPerSample >> 3;
			var totalNumSamples = length / bytesPerSample;
			var bytesPerChannelPerSample = (uint)(bits >> 3);

			var highestValuePerChannel = xRangePerChannel - 1;
			if (highestValuePerChannel > MipLevel.MaximumRange)
			{
				Logger.Warn(
					$"Provided range {xRange} ({xRangePerChannel} per channel) is too high. Maximum supported pixels per channel is {MipLevel.MaximumRange + 1}.");
				return false;
			}

			// Determine how many mip levels encompass sample ranges that are small enough that they are guaranteed
			// to not overflow 14 bits of storage. We can fill these in the first pass.
			// For the values which could be too large we will fill them by summing the two values from the
			// previous mip level in a second pass.
			var numFirstPassMipLevels = 0;
			var d = MipLevel.MaximumDistanceRange / xRangePerChannel;
			while (d > 0)
			{
				numFirstPassMipLevels++;
				d >>= 1;
			}

			// The values being stored are distance deltas. In practice these deltas are very small compared to the
			// actual range the samples can cover. For example, music won't have samples that alternate from 0.0 one sample
			// to 1.0 the next, and back. Because we can reasonably expect these deltas to be small we can increase
			// the number of levels we fill on the first pass. This saves some time filling and reduces compounding rounding
			// errors.
			numFirstPassMipLevels += 1;

			// Determine the number of mip levels needed.
			var numMipLevels = 0;
			var samplesPerIndex = 1;
			while (samplesPerIndex < totalNumSamples)
			{
				numMipLevels++;
				samplesPerIndex <<= 1;
			}

			// Default to using 1 task to do all the work synchronously
			var samplesPerTask = totalNumSamples;
			var highestMipLevelToFillPerTask = numMipLevels - 1;
			var numTasks = 1u;

			// Allocate memory for all the MipLevels.
			// While looping over the MipLevels, determine how many tasks to split up loading into
			// and how many samples to loop over per task.
			MipLevels = new MipLevel[numMipLevels];
			var mipSampleSize = 1u;
			var previousNumSamples = 0u;
			for (var mipLevelIndex = 0; mipLevelIndex < numMipLevels; mipLevelIndex++)
			{
				// Allocate memory for the mip level.
				var numSamples = totalNumSamples / mipSampleSize;
				if (totalNumSamples % mipSampleSize != 0)
					numSamples++;
				MipLevels[mipLevelIndex] = new MipLevel(numSamples, (uint)numChannels);

				// If this mip level divides the data by the number of parallel workers desired,
				// record information about this level for the tasks.
				if (previousNumSamples > LoadParallelism && numSamples <= LoadParallelism)
				{
					highestMipLevelToFillPerTask = mipLevelIndex;
					samplesPerTask = mipSampleSize;
					numTasks = numSamples;
				}

				previousNumSamples = numSamples;
				mipSampleSize <<= 1;

				token.ThrowIfCancellationRequested();
			}

			MipLevelsAllocated = true;

			// Divide up the mip map generation into parallel tasks.
			// Each task must operate on a completely independent set of MipLevel data.
			// Otherwise we would need to lock around non-atomic operations like += and min/max updates.
			// This will fill the densest mip levels, and then afterwards we can fill
			// each remaining sparse mip level by combining samples from it's previous level.
			var tasks = new Task[numTasks];
			for (var i = 0; i < numTasks; i++)
			{
				var startSample = (uint)(i * samplesPerTask);
				var endSample = (uint)Math.Min(totalNumSamples, (i + 1) * samplesPerTask);
				tasks[i] = Task.Run(() =>
					FillMipMapWorker(
						ptr1,
						startSample,
						endSample,
						bytesPerSample,
						bytesPerChannelPerSample,
						highestMipLevelToFillPerTask,
						xRangePerChannel,
						format,
						numFirstPassMipLevels,
						token), token);
			}

			await Task.WhenAll(tasks);

			token.ThrowIfCancellationRequested();

			// Fill remaining data  that wasn't filled by the above tasks.
			var startIndex = Math.Min(highestMipLevelToFillPerTask + 1, numFirstPassMipLevels);
			for (var mipLevelIndex = startIndex; mipLevelIndex < numMipLevels; mipLevelIndex++)
			{
				var mipDataNumSamples = MipLevels[mipLevelIndex].Data.Length / numChannels;
				var previousMipLevelIndex = mipLevelIndex - 1;
				var needsToFillMinAndMax = startIndex >= highestMipLevelToFillPerTask + 1;

				// We need to fill the min and max values and the distance tracking values.
				// This assumes that the number of mip levels needing distance tracking values filled is larger
				// than the number of mip levels needing mip levels filled, but in practice that is always true
				// as the sound data is large.
				if (needsToFillMinAndMax)
				{
					for (var mipSampleIndex = 0; mipSampleIndex < mipDataNumSamples; mipSampleIndex++)
					{
						for (var channel = 0; channel < NumChannels; channel++)
						{
							var relativeSampleIndex = mipSampleIndex * numChannels + channel;
							var previousRelativeSampleIndex = ((mipSampleIndex * numChannels) << 1) + channel;

							// We want to combine values from the corresponding two samples from the previous, more dense
							// mip level data. First, just take the first sample.
							var previousMin = MipLevel.GetMin(MipLevels[previousMipLevelIndex].Data[previousRelativeSampleIndex]);
							var previousMax = MipLevel.GetMax(MipLevels[previousMipLevelIndex].Data[previousRelativeSampleIndex]);
							var previousDistance =
								(float)MipLevel.GetDistanceOverSamples(MipLevels[previousMipLevelIndex]
									.Data[previousRelativeSampleIndex]);

							// Then combine it with the second sample, if there is one.
							var secondPreviousRelativeSampleIndex = previousRelativeSampleIndex + numChannels;
							if (secondPreviousRelativeSampleIndex < MipLevels[previousMipLevelIndex].Data.Length)
							{
								var s2 = MipLevels[previousMipLevelIndex].Data[secondPreviousRelativeSampleIndex];
								var s2Min = MipLevel.GetMin(s2);
								var s2Max = MipLevel.GetMin(s2);

								if (previousMin > s2Min)
									previousMin = s2Min;
								if (previousMax < s2Max)
									previousMax = s2Max;
								previousDistance += MipLevel.GetDistanceOverSamples(s2);
							}

							// Update the data at this mip level index based on the combined samples.
							uint v = 0;
							MipLevel.SetMin(ref v, previousMin);
							MipLevel.SetMax(ref v, previousMax);
							MipLevel.SetDistanceOverSamples(ref v, (ushort)(previousDistance * 0.5f + 0.5f));
							MipLevels[mipLevelIndex].Data[relativeSampleIndex] = v;
						}
					}
				}

				// We only need to fill the distance tracking values.
				else
				{
					for (var mipSampleIndex = 0; mipSampleIndex < mipDataNumSamples; mipSampleIndex++)
					{
						for (var channel = 0; channel < NumChannels; channel++)
						{
							var relativeSampleIndex = mipSampleIndex * numChannels + channel;
							var previousRelativeSampleIndex = ((mipSampleIndex * numChannels) << 1) + channel;

							// We want to combine values from the corresponding two samples from the previous, more dense
							// mip level data. First, just take the first sample.
							var previousDistance =
								(float)MipLevel.GetDistanceOverSamples(MipLevels[previousMipLevelIndex]
									.Data[previousRelativeSampleIndex]);

							// Then combine it with the second sample, if there is one.
							var secondPreviousRelativeSampleIndex = previousRelativeSampleIndex + numChannels;
							if (secondPreviousRelativeSampleIndex < MipLevels[previousMipLevelIndex].Data.Length)
							{
								var s2 = MipLevels[previousMipLevelIndex].Data[secondPreviousRelativeSampleIndex];
								previousDistance += MipLevel.GetDistanceOverSamples(s2);
							}

							// Update the data at this mip level index based on the combined samples.
							var v = MipLevels[mipLevelIndex].Data[relativeSampleIndex];
							MipLevel.SetDistanceOverSamples(ref v, (ushort)(previousDistance * 0.5f + 0.5f));
							MipLevels[mipLevelIndex].Data[relativeSampleIndex] = v;
						}
					}
				}

				token.ThrowIfCancellationRequested();
			}
		}
		catch (OperationCanceledException)
		{
			Reset();
			if (soundNeedsUnlock)
			{
				SoundManager.ErrCheck(sound.unlock(ptr1, ptr2, len1, len2));
				soundNeedsUnlock = false;
			}

			throw;
		}
		finally
		{
			if (soundNeedsUnlock)
			{
				SoundManager.ErrCheck(sound.unlock(ptr1, ptr2, len1, len2));
			}
		}

		return true;
	}

	/// <summary>
	/// Called by CreateMipMap to perform a portion of the work of filling MipLevels data.
	/// This method can be run on its own thead but it is expected that the data range covered
	/// by the provided sampleStart, sampleEnd, and numMipLevels is not also being modified
	/// on another thread. The operations to fill a SampleDataPerChannel are not thread safe.
	/// </summary>
	/// <remarks>
	/// Unsafe due to byte array usage from native library.
	/// </remarks>
	private unsafe void FillMipMapWorker(
		IntPtr intPtr,
		uint startSample,
		uint endSample,
		uint bytesPerSample,
		uint bytesPerChannelPerSample,
		int numMipLevels,
		int xRangePerChannel,
		SOUND_FORMAT format,
		int numMipLevelsToFillDistance,
		CancellationToken token)
	{
		var ptr = (byte*)intPtr.ToPointer();

		// Constants for converting sound formats to floats.
		const float invPcm8Max = 1.0f / byte.MaxValue;
		const float invPcm16Max = 1.0f / short.MaxValue;
		const float invPcm24Max = 1.0f / 8388607;
		const float invPcm32Max = 1.0f / int.MaxValue;

		var maxValuePerChannel = (ushort)(xRangePerChannel - 1);

		// Loop over every sample for every channel.
		var sampleIndex = startSample;

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
				return;
		}

		// Set the first previous tracking values.
		var previousValues = new float[NumChannels];
		if (sampleIndex == 0)
		{
			for (var channel = 0; channel < NumChannels; channel++)
			{
				previousValues[channel] = 0.5f;
			}
		}
		else
		{
			for (var channel = 0; channel < NumChannels; channel++)
			{
				var byteIndex = (sampleIndex - 1) * bytesPerSample + channel * bytesPerChannelPerSample;
				previousValues[channel] = parseSample(byteIndex);
			}
		}

		// Loop over every sample and fill.
		while (sampleIndex < endSample)
		{
			for (var channel = 0; channel < NumChannels; channel++)
			{
				var byteIndex = sampleIndex * bytesPerSample + channel * bytesPerChannelPerSample;
				var value = parseSample(byteIndex);

				// Determine values for mip data.
				var xValueForChannelSample = (ushort)(maxValuePerChannel * (value + 1.0f) * 0.5f);
				if (xValueForChannelSample > maxValuePerChannel)
					xValueForChannelSample = maxValuePerChannel;
				var distance = Math.Abs(value - previousValues[channel]);
				var distanceScaled = (ushort)(distance * maxValuePerChannel);

				// Update mip data.
				var numSamplesPerMipLevel = 1;
				for (var mipLevelIndex = 0; mipLevelIndex < numMipLevels; mipLevelIndex++, numSamplesPerMipLevel <<= 1)
				{
					// Always fill the min and max values.
					var relativeSampleIndex = (sampleIndex >> mipLevelIndex) * NumChannels + channel;
					ref var v = ref MipLevels[mipLevelIndex].Data[relativeSampleIndex];
					if (MipLevel.GetMin(v) > xValueForChannelSample)
						MipLevel.SetMin(ref v, xValueForChannelSample);
					if (MipLevel.GetMax(v) < xValueForChannelSample)
						MipLevel.SetMax(ref v, xValueForChannelSample);

					// At mip level 0 we don't need to do any modulo operations for performing the final divide.
					if (mipLevelIndex == 0)
					{
						var d = (ushort)(MipLevel.GetDistanceOverSamples(v) + distanceScaled);
						MipLevel.SetDistanceOverSamples(ref v, d);
					}

					// At other mip levels, accumulate a sum and then divide it once we have summed all samples.
					else if (mipLevelIndex < numMipLevelsToFillDistance)
					{
						// Add the distance.
						var d = (ushort)(MipLevel.GetDistanceOverSamples(v) + distanceScaled);
						MipLevel.SetDistanceOverSamples(ref v, d);

						// If this is the last sample index for the chunk, divide and finalize.
						if (sampleIndex % numSamplesPerMipLevel == numSamplesPerMipLevel - 1)
						{
							MipLevel.SetDistanceOverSamples(ref v, (ushort)((float)d / numSamplesPerMipLevel + 0.5f));
						}
					}
				}

				previousValues[channel] = value;
			}

			sampleIndex++;

			// Every 15 seconds of audio assuming 44.1kHz.
			if (sampleIndex % 661500 == 0)
				token.ThrowIfCancellationRequested();
		}
	}
}

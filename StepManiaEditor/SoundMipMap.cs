using System;
using System.Threading;
using System.Threading.Tasks;
using FMOD;
using Fumen;

namespace StepManiaEditor
{
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
		/// An array of SampleDataPerChannel where each SampleDataPerChannel entry represents data
		/// for a range of underlying audio samples.
		/// At each mip level, the number of samples represented by each SampleDataPerChannel entry
		/// is 2 to the power of the mip level. For example at mip level 0 each SampleDataPerChannel
		/// entry corresponds to 1 sample. At mip level 1 it is 2 samples, at 2 it is 4, etc.
		/// </summary>
		public class MipLevel
		{
			/// <summary>
			/// Data combined from one or more underlying samples of audio data.
			/// </summary>
			public struct SampleDataPerChannel
			{
				/// <summary>
				/// Minimum x value of samples. Range is from 0 to the provided range to
				/// CreateMipMapAsync divided by the number of channels.
				/// </summary>
				public ushort MinX;
				/// <summary>
				/// Maximum x value of samples. Range is from 0 to the provided range to
				/// CreateMipMapAsync divided by the number of channels.
				/// </summary>
				public ushort MaxX;
				/// <summary>
				/// Sum of the squares of the samples where each sample is represented as
				/// a floating point value from -1.0f to 1.0f. Used to visualize root mean
				/// square.
				/// </summary>
				public float SumOfSquares;
			}

			/// <summary>
			/// Array of SampleDataPerChannel. Channels are interleaved.
			/// </summary>
			public readonly SampleDataPerChannel[] Data;

			/// <summary>
			/// Constructor. Will allocate data and set appropriate defaults to start filling.
			/// </summary>
			/// <param name="numSamples">Number of samples at this mip level.</param>
			/// <param name="numChannels">Number of channels of audio per sample.</param>
			public MipLevel(uint numSamples, uint numChannels)
			{
				var len = numSamples * numChannels;
				Data = new SampleDataPerChannel[len];
				for (var i = 0; i < len; i++)
				{
					Data[i].MinX = ushort.MaxValue;
				}
			}
		}

		/// <summary>
		/// SoundManager.
		/// </summary>
		private readonly SoundManager SoundManager;
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
		private readonly object MipLevelsLock = new object();

		/// <summary>
		/// Constructor.
		/// </summary>
		/// <param name="soundManager">SoundManager.</param>
		public SoundMipMap(SoundManager soundManager)
		{
			SoundManager = soundManager;
		}

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
			bool shouldGC;
			lock (MipLevelsLock)
			{
				shouldGC = MipLevels != null;
				MipLevelsAllocated = false;
				MipMapDataLoaded = false;
				MipLevels = null;
				NumChannels = 0;
			}
			// Garbage collect to free the MipLevels data.
			if (shouldGC)
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
			return SoundManager.GetSampleRate();
		}

		/// <summary>
		/// Creates the mip map data for the given Sound.
		/// </summary>
		/// <param name="sound">Sound to create mip map data for.</param>
		/// <param name="xRange">Total x range in pixels for all channels.</param>
		/// <param name="token">CancellationToken for cancelling this Task.</param>
		public async Task CreateMipMapAsync(Sound sound, int xRange, CancellationToken token)
		{
			if (!sound.hasHandle())
			{
				Logger.Warn("Cannot create sound mip map data. Invalid sound handle.");
				return;
			}

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
					MipLevels[mipLevelIndex] = new MipLevel(numSamples, (uint) numChannels);

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
							token));
				}
				await Task.WhenAll(tasks);

				token.ThrowIfCancellationRequested();

				// Fill remaining mip levels that weren't filled by the above tasks.
				for (var mipLevelIndex = highestMipLevelToFillPerTask + 1; mipLevelIndex < numMipLevels; mipLevelIndex++)
				{
					var mipDataNumSamples = MipLevels[mipLevelIndex].Data.Length / numChannels;
					var previousMipLevelIndex = mipLevelIndex - 1;

					for (var mipSampleIndex = 0; mipSampleIndex < mipDataNumSamples; mipSampleIndex++)
					{
						for (var channel = 0; channel < NumChannels; channel++)
						{
							var relativeSampleIndex = mipSampleIndex * numChannels + channel;
							var previousRelativeSampleIndex = ((mipSampleIndex * numChannels) << 1) + channel;

							// We want to combine values from the corresponding two samples from the previous, more dense
							// mip level data. First, just take the first sample.
							var previousMin = MipLevels[previousMipLevelIndex].Data[previousRelativeSampleIndex].MinX;
							var previousMax = MipLevels[previousMipLevelIndex].Data[previousRelativeSampleIndex].MaxX;
							var previousSum = MipLevels[previousMipLevelIndex].Data[previousRelativeSampleIndex].SumOfSquares;

							// Then combine it with the second sample, if there is one.
							var secondPreviousRelativeSampleIndex = previousRelativeSampleIndex + numChannels;
							if (secondPreviousRelativeSampleIndex < MipLevels[previousMipLevelIndex].Data.Length)
							{
								if (previousMin > MipLevels[previousMipLevelIndex].Data[secondPreviousRelativeSampleIndex].MinX)
									previousMin = MipLevels[previousMipLevelIndex].Data[secondPreviousRelativeSampleIndex].MinX;
								if (previousMax < MipLevels[previousMipLevelIndex].Data[secondPreviousRelativeSampleIndex].MaxX)
									previousMax = MipLevels[previousMipLevelIndex].Data[secondPreviousRelativeSampleIndex].MaxX;
								previousSum += MipLevels[previousMipLevelIndex].Data[secondPreviousRelativeSampleIndex].SumOfSquares;
							}

							// Update the data at this mip level index based on the combined samples.
							MipLevels[mipLevelIndex].Data[relativeSampleIndex].MinX = previousMin;
							MipLevels[mipLevelIndex].Data[relativeSampleIndex].MaxX = previousMax;
							MipLevels[mipLevelIndex].Data[relativeSampleIndex].SumOfSquares = previousSum;
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
					soundNeedsUnlock = false;
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
			CancellationToken token)
		{
			var ptr = (byte*)intPtr.ToPointer();

			// Constants for converting sound formats to floats.
			const float invPcm8Max = 1.0f / byte.MaxValue;
			const float invPcm16Max = 1.0f / short.MaxValue;
			const float invPcm24Max = 1.0f / 8388607;
			const float invPcm32Max = 1.0f / int.MaxValue;

			// Loop over every sample for every channel.
			var sampleIndex = startSample;
			var value = 0.0f;
			while (sampleIndex < endSample)
			{
				for (var channel = 0; channel < NumChannels; channel++)
				{
					var byteIndex = sampleIndex * bytesPerSample + channel * bytesPerChannelPerSample;

					switch (format)
					{
						case SOUND_FORMAT.PCM8:
							{
								value = ptr[byteIndex] * invPcm8Max;
								break;
							}
						case SOUND_FORMAT.PCM16:
							{
								value = ((short)ptr[byteIndex]
								         + (short)(ptr[byteIndex + 1] << 8)) * invPcm16Max;
								break;
							}
						case SOUND_FORMAT.PCM24:
							{
								value = (((int)(ptr[byteIndex] << 8)
								          + (int)(ptr[byteIndex + 1] << 16)
								          + (int)(ptr[byteIndex + 2] << 24)) >> 8) * invPcm24Max;
								break;
							}
						case SOUND_FORMAT.PCM32:
							{
								value = ((int)ptr[byteIndex]
								         + (int)(ptr[byteIndex + 1] << 8)
								         + (int)(ptr[byteIndex + 2] << 16)
								         + (int)(ptr[byteIndex + 3] << 24)) * invPcm32Max;
								break;
							}
						case SOUND_FORMAT.PCMFLOAT:
							{
								value = ((float*)ptr)[byteIndex >> 2];
								break;
							}
					}

					// Determine values for mip data.
					var xValueForChannelSample = (ushort)((xRangePerChannel - 1) * (value + 1.0f) * 0.5f);
					var square = value * value;

					// Update mip data.
					for (var mipLevelIndex = 0; mipLevelIndex < numMipLevels; mipLevelIndex++)
					{
						var relativeSampleIndex = (sampleIndex >> mipLevelIndex) * NumChannels + channel;
						if (MipLevels[mipLevelIndex].Data[relativeSampleIndex].MinX > xValueForChannelSample)
							MipLevels[mipLevelIndex].Data[relativeSampleIndex].MinX = xValueForChannelSample;
						if (MipLevels[mipLevelIndex].Data[relativeSampleIndex].MaxX < xValueForChannelSample)
							MipLevels[mipLevelIndex].Data[relativeSampleIndex].MaxX = xValueForChannelSample;
						MipLevels[mipLevelIndex].Data[relativeSampleIndex].SumOfSquares += square;
					}
				}

				sampleIndex++;

				// Every 15 seconds of audio assuming 44.1kHz.
				if (sampleIndex % 661500 == 0)
					token.ThrowIfCancellationRequested();
			}
		}
	}
}

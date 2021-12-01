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

		private readonly SoundManager SoundManager;
		public Sound Sound;
		public MipLevel[] MipLevels;
		private uint NumChannels;

		private bool SoundLoaded;
		private bool MipMapDataLoaded;
		private bool MipLevelsAllocated;

		public SoundMipMap(SoundManager soundManager)
		{
			SoundManager = soundManager;
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
		/// Loads the sound specified by the given file name.
		/// </summary>
		/// <param name="soundFileName">File name of sound to load.</param>
		/// <returns>Generated Sound.</returns>
		public async Task<Sound> LoadSoundAsync(string soundFileName)
		{
			Logger.Info($"Loading {soundFileName}...");
			Sound = await SoundManager.LoadAsync(soundFileName);
			if (Sound.hasHandle())
			{
				Logger.Info($"Finished loading {soundFileName}.");
				SoundLoaded = true;
			}
			return Sound;
		}

		/// <summary>
		/// Creates the mip map data for the Sound.
		/// </summary>
		/// <param name="xRange">Total x range in pixels for all channels.</param>
		public async Task CreateMipMapAsync(int xRange)
		{
			if (!SoundLoaded)
			{
				Logger.Warn("Cannot create sound mip map data. Sound is not loaded.");
				return;
			}

			if (!Sound.hasHandle())
			{
				Logger.Warn("Cannot create sound mip map data. Invalid sound handle.");
				return;
			}

			Logger.Info("Generating sound mip map...");
			var success = false;
			await Task.Run(() => { success = CreateMipMap(xRange); });
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
		/// Unsafe due to byte array usage from native library.
		/// </remarks>
		/// <param name="xRange">Total x range in pixels for all channels.</param>
		/// <returns>True if successful and false otherwise.</returns>
		private unsafe bool CreateMipMap(int xRange)
		{
			// Get the sound data from FMOD.
			if (!SoundManager.ErrCheck(Sound.getLength(out var length, TIMEUNIT.PCMBYTES)))
				return false;
			if (!SoundManager.ErrCheck(Sound.getFormat(out _, out var format, out var numChannels, out var bits)))
				return false;
			if (!SoundManager.ErrCheck(Sound.@lock(0, length, out var ptr1, out var ptr2, out var len1, out var len2)))
				return false;

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
			if (bits < 1)
			{
				Logger.Warn($"Sound has {bits} bits per sample. Expected at least one.");
				return false;
			}

			var ptr = (byte*)ptr1.ToPointer();

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

			// Allocate memory for all the MipLevels.
			MipLevels = new MipLevel[numMipLevels];
			var mipSampleSize = 1;
			for (var mipLevelIndex = 0; mipLevelIndex < numMipLevels; mipLevelIndex++)
			{
				var numSamples = (uint) (totalNumSamples / mipSampleSize);
				if (totalNumSamples % mipSampleSize != 0)
					numSamples++;
				MipLevels[mipLevelIndex] = new MipLevel(numSamples, (uint)numChannels);
				mipSampleSize <<= 1;
			}
			MipLevelsAllocated = true;

			// Constants for converting sound formats to floats.
			const float invPcm8Max = 1.0f / byte.MaxValue;
			const float invPcm16Max = 1.0f / short.MaxValue;
			const float invPcm24Max = 1.0f / 8388607;
			const float invPcm32Max = 1.0f / int.MaxValue;

			// Loop over every sample for every channel.
			var sampleIndex = 0;
			var value = 0.0f;
			while (sampleIndex < totalNumSamples)
			{
				for (var channel = 0; channel < numChannels; channel++)
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
							value = ((short)ptr[byteIndex] + (short)(ptr[byteIndex + 1] << 8)) * invPcm16Max;
							break;
						}
						case SOUND_FORMAT.PCM24:
						{
							value = (((int)(ptr[byteIndex] << 8) + (int)(ptr[byteIndex + 1] << 16) + (int)(ptr[byteIndex + 2] << 24)) >> 8) * invPcm24Max;
							break;
						}
						case SOUND_FORMAT.PCM32:
						{
							value = ((int)ptr[byteIndex] + (int)(ptr[byteIndex + 1] << 8) + (int)(ptr[byteIndex + 2] << 16) + (int)(ptr[byteIndex + 3] << 24)) * invPcm32Max;
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
						var relativeSampleIndex = (sampleIndex >> mipLevelIndex) * numChannels + channel;
						if (MipLevels[mipLevelIndex].Data[relativeSampleIndex].MinX > xValueForChannelSample)
							MipLevels[mipLevelIndex].Data[relativeSampleIndex].MinX = xValueForChannelSample;
						if (MipLevels[mipLevelIndex].Data[relativeSampleIndex].MaxX < xValueForChannelSample)
							MipLevels[mipLevelIndex].Data[relativeSampleIndex].MaxX = xValueForChannelSample;
						MipLevels[mipLevelIndex].Data[relativeSampleIndex].SumOfSquares += square;
					}
				}

				sampleIndex++;
			}

			SoundManager.ErrCheck(Sound.unlock(ptr1, ptr2, len1, len2));

			return true;
		}
	}
}

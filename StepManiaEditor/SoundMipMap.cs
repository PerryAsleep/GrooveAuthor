using System;
using System.Threading.Tasks;
using FMOD;

namespace StepManiaEditor
{
	public class SoundMipMap
	{
		public class MipLevel
		{
			public struct SampleDataPerChannel
			{
				public ushort MinX;
				public ushort MaxX;
				public uint NumDirectionChanges;
			}

			public MipLevel(uint numSamples, uint numChannels)
			{
				var len = numSamples * numChannels;
				Data = new SampleDataPerChannel[len];
				for (var i = 0; i < len; i++)
				{
					Data[i].MinX = ushort.MaxValue;
				}
			}

			public readonly SampleDataPerChannel[] Data;
		}

		private SoundManager SoundManager;
		public Sound Sound;
		public MipLevel[] MipLevels;
		private int NumChannels;

		private bool SoundLoaded;
		private bool MipMapDataLoaded;
		private bool MipLevelsAllocated;

		public SoundMipMap(SoundManager soundManager)
		{
			SoundManager = soundManager;
		}

		public int GetMipLevel0NumSamples()
		{
			return MipLevels[0].Data.Length / NumChannels;
		}

		public int GetNumChannels()
		{
			return NumChannels;
		}

		public bool IsMipMapDataLoaded()
		{
			return MipMapDataLoaded;
		}

		public bool IsMipMapDataAllocated()
		{
			return MipLevelsAllocated;
		}

		public uint GetSampleRate()
		{
			return SoundManager.GetSampleRate();
		}

		public async Task<Sound> LoadSoundAsync(SoundManager soundManager, string soundFileName)
		{
			// TODO: Investigate loading streaming so we can speed up the mip map load even further.
			Sound = await soundManager.LoadAsync(soundFileName);
			SoundLoaded = true;
			return Sound;
		}

		public async Task CreateMipMapAsync(int xRange)
		{
			await Task.Run(() => { CreateMipMap(xRange); });
			MipMapDataLoaded = true;
		}
		
		private unsafe void CreateMipMap(int xRange)
		{
			SoundManager.ErrCheck(Sound.getLength(out var length, TIMEUNIT.PCMBYTES));
			SoundManager.ErrCheck(Sound.getFormat(out var type, out var format, out var numChannels, out var bits));
			SoundManager.ErrCheck(Sound.@lock(0, length, out var ptr1, out var ptr2, out var len1, out var len2));

			byte* ptr = (byte*)ptr1.ToPointer();

			NumChannels = numChannels;

			int xRangePerChannel = xRange / numChannels;

			uint bitsPerSample = (uint)bits * (uint)numChannels;
			uint bytesPerSample = bitsPerSample >> 3;
			uint totalNumSamples = length / bytesPerSample;
			uint bytesPerChannelPerSample = (uint)(bits / 8);

			var numMipLevels = 0;
			var samplesPerIndex = 1;
			while (samplesPerIndex < totalNumSamples)
			{
				numMipLevels++;
				samplesPerIndex <<= 1;
			}

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

			var sampleIndex = 0;
			var previousXValues = new ushort[numChannels];
			var previousSlopePositive = new bool[numChannels];
			for (var channel = 0; channel < numChannels; channel++)
			{
				previousXValues[channel] = 0;
				previousSlopePositive[channel] = false;
			}

			while (sampleIndex < totalNumSamples)
			{
				for (var channel = 0; channel < numChannels; channel++)
				{
					var byteIndex = sampleIndex * bytesPerSample + channel * bytesPerChannelPerSample;

					float value = 0.0f;
					switch (format)
					{
						case SOUND_FORMAT.PCM8:
						{
							value = ptr[byteIndex] / (float)byte.MaxValue;
							break;
						}
						case SOUND_FORMAT.PCM16:
						{
							value = ((short)ptr[byteIndex] + (short)(ptr[byteIndex + 1] << 8)) / (float)short.MaxValue;
							break;
						}
						case SOUND_FORMAT.PCM24:
						{
							// TODO: Test
							value = ((int)ptr[byteIndex] + (int)(ptr[byteIndex + 1] << 8) + (int)(ptr[byteIndex + 2] << 16)) / (float)8388607;
							break;
						}
						case SOUND_FORMAT.PCM32:
						{
							// TODO: Test
							value = ((int)ptr[byteIndex] + (int)(ptr[byteIndex + 1] << 8) + (int)(ptr[byteIndex + 2] << 16) + (int)(ptr[byteIndex + 3] << 24)) / (float)Int32.MaxValue;
							break;
						}
						case SOUND_FORMAT.PCMFLOAT:
						{
							// TODO: Test
							value = ((float*)ptr)[byteIndex >> 2];
							break;
						}
					}
					
					var xValueForChannelSample = (ushort)((xRangePerChannel - 1) * (value + 1.0f) / 2.0f);

					bool directionChange = false;
					if (xValueForChannelSample != previousXValues[channel])
					{
						bool bSlopePositive = xValueForChannelSample > previousXValues[channel];
						if (bSlopePositive != previousSlopePositive[channel])
						{
							directionChange = true;
							previousSlopePositive[channel] = bSlopePositive;
						}
					}
					
					var powerOfTwo = 1;
					for (var mipLevelIndex = 0; mipLevelIndex < numMipLevels; mipLevelIndex++)
					{
						var relativeSampleIndex = ((sampleIndex / powerOfTwo) * numChannels) + channel;
						if (MipLevels[mipLevelIndex].Data[relativeSampleIndex].MinX > xValueForChannelSample)
							MipLevels[mipLevelIndex].Data[relativeSampleIndex].MinX = xValueForChannelSample;
						if (MipLevels[mipLevelIndex].Data[relativeSampleIndex].MaxX < xValueForChannelSample)
							MipLevels[mipLevelIndex].Data[relativeSampleIndex].MaxX = xValueForChannelSample;
						if (directionChange)
							MipLevels[mipLevelIndex].Data[relativeSampleIndex].NumDirectionChanges++;
						powerOfTwo <<= 1;
					}

					previousXValues[channel] = xValueForChannelSample;
				}

				sampleIndex++;
			}

			SoundManager.ErrCheck(Sound.unlock(ptr1, ptr2, len1, len2));
		}
	}
}

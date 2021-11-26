using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace StepManiaEditor
{
	public class WaveFormRenderer
	{
		/// <summary>
		/// Number of textures to use for buffering. Double buffering is fine.
		/// </summary>
		private const int NumTextures = 2;
		/// <summary>
		/// Color for sparse area of waveform. Dark green in BRG565.
		/// </summary>
		private const ushort ColorSparse = 0x3E0;
		/// <summary>
		/// Color for dense area of waveform. Light green in BRG565.
		/// </summary>
		private const ushort ColorDense = 0x7E0;

		/// <summary>
		/// Width of texture in pixels.
		/// </summary>
		private readonly uint Width;
		/// <summary>
		/// Height of texture in pixels.
		/// </summary>
		private readonly uint Height;
		/// <summary>
		/// Y Offset in pixels of the focal point of the waveform.
		/// At 0, the waveform will be rendered such that the given time is at the top of the texture.
		/// </summary>
		private int YFocusOffset;

		/// <summary>
		/// Textures to render to. Array for double buffering.
		/// </summary>
		private readonly Texture2D[] Textures;
		/// <summary>
		/// BRG565 data to set on the texture after updating each frame.
		/// </summary>
		private readonly ushort[] BRG565Data;
		/// <summary>
		/// One row of dense colored pixels, used for copying memory quickly into the data buffer instead of looping.
		/// </summary>
		private readonly ushort[] DenseLine;
		/// <summary>
		/// One row of sparse colored pixels, used for copying memory quickly into the data buffer instead of looping.
		/// </summary>
		private readonly ushort[] SparseLine;

		/// <summary>
		/// Index into Textures array to control which texture we write to while the other is being rendered.
		/// </summary>
		private int TextureIndex;
		
		/// <summary>
		/// SoundMipMap data to use for rendering.
		/// </summary>
		private SoundMipMap MipMap;
		/// <summary>
		/// Cached sample rate of the audio in hz.
		/// </summary>
		private uint SampleRate;

		public WaveFormRenderer(GraphicsDevice graphicsDevice, uint width, uint height)
		{
			Width = width;
			Height = height;

			// Set up the textures.
			Textures = new Texture2D[NumTextures];
			for (var i = 0; i < NumTextures; i++)
			{
				Textures[i] = new Texture2D(graphicsDevice, (int)Width, (int)Height, false, SurfaceFormat.Bgr565);
			}

			// Set up the pixel data.
			BRG565Data = new ushort[Width * Height];
			DenseLine = new ushort[Width];
			SparseLine = new ushort[Width];
			for (var i = 0; i < Width; i++)
			{
				DenseLine[i] = ColorDense;
				SparseLine[i] = ColorSparse;
			}
		}

		public void SetSoundMipMap(SoundMipMap mipMap)
		{
			MipMap = mipMap;
			SampleRate = MipMap.GetSampleRate();
		}

		public void SetYFocusOffset(int yFocusOffset)
		{
			YFocusOffset = yFocusOffset;
		}

		public void Draw(SpriteBatch spriteBatch)
		{
			// Draw the current texture.
			spriteBatch.Draw(Textures[TextureIndex], new Vector2(0, 0), null, Color.White);
			// Advance to the next texture index for the next frame.
			TextureIndex = (TextureIndex + 1) % NumTextures;
		}

		public void Update(double soundTimeSeconds, double zoom)
		{
			UpdateTexture(soundTimeSeconds, zoom);
		}

		private void UpdateTexture(double soundTimeSeconds, double zoom)
		{
			if (MipMap == null || !MipMap.IsMipMapDataAllocated())
			{
				return;
			}

			var texture = Textures[TextureIndex];
			
			// Clear the pixel data to all black.
			// Array.Clear is the most efficient way to do this in practice.
			Array.Clear(BRG565Data, 0, (int)(Width * Height));

			var numChannels = MipMap.GetNumChannels();
			var totalWidthPerChannel = (uint)(Width / numChannels);

			//uint endSample = startSample + (uint)(SampleRate / Zoom);
			//uint numSamples = endSample - startSample;
			// range shown = 1 second / Zoom
			double samplesPerPixel = (double)SampleRate / Height / zoom;
			// Quantizing the samples per pixel to an integer value guarantees that for a given zoom
			// level, the same samples will always be grouped together. This prevents a jittering
			// artifact that could occur otherwise due to samples being grouped with different sets
			// depending on the range we are looking at.
			// When the zoom is so high that individual samples are spread out across multiple pixels,
			// do not quantize. This could introduce jitter, but at this zoom level the scroll speed
			// would need to be unrealistically slow to notice any artifact.
			if (samplesPerPixel > 1.0)
			{
				//samplesPerPixel = (long) samplesPerPixel;
			}

			var startSampleOffset = (YFocusOffset * samplesPerPixel * -1);
			long startSample = (long)(soundTimeSeconds * SampleRate + startSampleOffset);

			// Snap the start sample so that the waveform doesn't jitter while scrolling
			// by moving samples between pixel boundaries on different frames.
			var pixel = (int)(startSample / samplesPerPixel);
			startSample = (long)(pixel * samplesPerPixel);

			uint totalNumSamples = (uint)MipMap.GetMipLevel0NumSamples();

			var minXPerChannel = new ushort[numChannels];
			var maxXPerChannel = new ushort[numChannels];
			var dirChangesPerChannel = new uint[numChannels];

			// Set up structures to track the previous values.
			var previousXMin = new ushort[numChannels];
			var previousXMax = new ushort[numChannels];
			if (startSample > 0 && startSample < totalNumSamples + 1)
			{
				for (var channel = 0; channel < numChannels; channel++)
				{
					var data = MipMap.MipLevels[0].Data[(startSample - 1) * numChannels + channel];
					previousXMin[channel] = data.MinX;
					previousXMax[channel] = data.MaxX;
				}
			}
			else
			{
				for (var channel = 0; channel < numChannels; channel++)
				{
					var channelMidX = (ushort)((totalWidthPerChannel >> 1) - 1);
					previousXMin[channel] = channelMidX;
					previousXMax[channel] = channelMidX;
				}
			}

			var sampleIndex = startSample;
			for (uint y = 0; y < Height; y++)
			{
				long numSamplesUsedThisLoop = 0;
				long endSampleForPixel = (long)((y + 1) * samplesPerPixel) + startSample;
				if (endSampleForPixel == sampleIndex)
					endSampleForPixel++;
				if (endSampleForPixel > totalNumSamples)
					endSampleForPixel = totalNumSamples;

				for (var channel = 0; channel < numChannels; channel++)
				{
					minXPerChannel[channel] = ushort.MaxValue;
					maxXPerChannel[channel] = 0;
					dirChangesPerChannel[channel] = 0;
				}

				var bUsePreviousSample = false;
				if (sampleIndex < 0)
				{
					if (endSampleForPixel < 0)
					{
						bUsePreviousSample = true;
					}
					else
					{
						sampleIndex = 0;
					}
				}

				if (sampleIndex > totalNumSamples)
				{
					bUsePreviousSample = true;
				}

				// If the zoom is so great that this y pixel has no samples, use the previous sample.
				if (sampleIndex >= endSampleForPixel)
				{
					bUsePreviousSample = true;
				}

				if (bUsePreviousSample)
				{
					for (var channel = 0; channel < numChannels; channel++)
					{
						minXPerChannel[channel] = previousXMin[channel];
						maxXPerChannel[channel] = previousXMax[channel];
					}
				}

				// Normal case, one or more samples for this y pixel
				else
				{
					numSamplesUsedThisLoop = endSampleForPixel - sampleIndex;
					while (sampleIndex < endSampleForPixel)
					{
						uint powerOfTwo = 2;
						uint mipLevelIndex = 1;
						while (sampleIndex % powerOfTwo == 0 && sampleIndex + powerOfTwo < endSampleForPixel)
						{
							powerOfTwo <<= 1;
							mipLevelIndex++;
						}
						mipLevelIndex--;
						powerOfTwo >>= 1;

						for (var channel = 0; channel < numChannels; channel++)
						{
							var relativeSampleIndex = ((sampleIndex / powerOfTwo) * numChannels) + channel;
							var data = MipMap.MipLevels[mipLevelIndex].Data[relativeSampleIndex];

							var curMinX = data.MinX;
							var curMaxX = data.MaxX;
							if (curMinX < minXPerChannel[channel])
								minXPerChannel[channel] = curMinX;
							if (curMaxX > maxXPerChannel[channel])
								maxXPerChannel[channel] = curMaxX;
							dirChangesPerChannel[channel] += data.NumDirectionChanges;
						}

						sampleIndex += powerOfTwo;
					}
				}

				for (var channel = 0; channel < numChannels; channel++)
				{
					// Prevent gaps due to samples being more than one pixel in x apart
					var minX = minXPerChannel[channel];
					var maxX = maxXPerChannel[channel];
					if (minX > previousXMax[channel] + 1)
					{
						minX = (ushort)(previousXMax[channel] + 1);
						if (maxX < minX)
							maxX = minX;
					}
					else if (previousXMin[channel] > 0 && maxX < previousXMin[channel] - 1)
					{
						maxX = (ushort)(previousXMin[channel] - 1);
						if (minX > maxX)
							minX = maxX;
					}

					// Record unmodified values for comparisons with previous values.
					// This way we don't introduce box artifacts when samples are sparse.
					previousXMin[channel] = minXPerChannel[channel];
					previousXMax[channel] = maxXPerChannel[channel];

					var range = maxX - minX;
					uint denseMinX = 0;
					uint denseMaxX = 0;
					uint dirChanges = 0;
					// Don't draw any dense values if we are only processing one sample.
					if (numSamplesUsedThisLoop > 1)
					{
						dirChanges = dirChangesPerChannel[channel];
						var directionChangePercentage = (float)dirChanges / samplesPerPixel; //numSamplesForRow;
						if (directionChangePercentage > 1.0f)
							directionChangePercentage = 1.0f;
						var densePercentage = directionChangePercentage * 0.9f;
						var denseRange = range * densePercentage;
						denseMinX = minX + (uint)((range - (denseRange)) * 0.5f);
						denseMaxX = (uint)(denseMinX + denseRange);
					}

					var startIndexForRowAndChannel = Width * y + (channel * totalWidthPerChannel);

					var densePixelStart = startIndexForRowAndChannel + denseMinX;
					var densePixelEnd = startIndexForRowAndChannel + denseMaxX;
					var sparsePixelStart = startIndexForRowAndChannel + minX;
					var sparsePixelEnd = startIndexForRowAndChannel + maxX;

					// Copy the sparse color line into the waveform pixel data.
					Buffer.BlockCopy(SparseLine, 0, BRG565Data, (int)(sparsePixelStart << 1), (int)((sparsePixelEnd + 1 - sparsePixelStart) << 1));
					// Copy the dense color line into the waveform pixel data.
					if (dirChanges > 0)
						Buffer.BlockCopy(DenseLine, 0, BRG565Data, (int)(densePixelStart << 1), (int)((densePixelEnd + 1 - densePixelStart) << 1));
				}
			}

			texture.SetData(BRG565Data);
		}
	}
}

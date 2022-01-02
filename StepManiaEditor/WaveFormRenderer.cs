using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace StepManiaEditor
{
	/// <summary>
	/// Renders a SoundMipMap as a waveform to an underlying double-buffered set of Textures.
	/// Used for performant real-time rendering of animating audio data.
	/// Call Update() to update the underlying Textures.
	/// Call Draw() to render the generated Textures to the given SpriteBatch.
	/// When drawing, the X value from the set FocalPoint will be used to center the Texture in X.
	/// The Y value of the FocalPoint is not used for drawing the Texture, but is used for
	/// controlling at what y pixel value the provided sound time from Update() should be rendered
	/// at. An optional x and y value can be provided to Draw() to reposition the Texture as needed.
	/// </summary>
	public class WaveFormRenderer
	{
		/// <summary>
		/// Number of textures to use for buffering. Double buffering is fine.
		/// </summary>
		private const int NumTextures = 2;
		/// <summary>
		/// Color for sparse area of waveform. BGR565.
		/// </summary>
		private ushort ColorSparse;
		/// <summary>
		/// Color for dense area of waveform. BGR565.
		/// </summary>
		private ushort ColorDense;
		/// <summary>
		/// Flag for controlling whether or not to scale in x when zooming.
		/// </summary>
		private bool ScaleXWhenZooming = true;

		/// <summary>
		/// Width of texture in pixels.
		/// </summary>
		private readonly uint Width;
		/// <summary>
		/// Height of texture in pixels.
		/// </summary>
		private readonly uint Height;
		/// <summary>
		/// The focal point for orienting the waveform and controlling zooming.
		/// Units are in pixels and are in screen space.
		/// The underlying texture is rendered at an offset that takes the x value of the FocalPoint into account.
		/// The underlying texture is rendered at a y offset of 0, with the FocalPoint's y value taken into
		/// account when generating the waveform on the texture.
		/// </summary>
		private Vector2 FocalPoint;
		/// <summary>
		/// Scale in X to apply to each channel.
		/// </summary>
		private float XPerChannelScale = 1.0f;

		/// <summary>
		/// Textures to render to. Array for double buffering.
		/// </summary>
		private readonly Texture2D[] Textures;
		/// <summary>
		/// BGR565 data to set on the texture after updating each frame.
		/// </summary>
		private readonly ushort[] BGR565Data;
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
		/// Constructor.
		/// </summary>
		/// <param name="graphicsDevice">GraphicsDevice to use for creating textures.</param>
		/// <param name="width">Texture width in pixels.</param>
		/// <param name="height">Texture height in pixels.</param>
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
			BGR565Data = new ushort[Width * Height];
			DenseLine = new ushort[Width];
			SparseLine = new ushort[Width];
			for (var i = 0; i < Width; i++)
			{
				DenseLine[i] = ColorDense;
				SparseLine[i] = ColorSparse;
			}
		}

		/// <summary>
		/// Sets the x scale per channel.
		/// Expected to be a value between 0.0f and 1.0f.
		/// </summary>
		/// <param name="xPerChannelScale">X scale per channel.</param>
		public void SetXPerChannelScale(float xPerChannelScale)
		{
			XPerChannelScale = Math.Min(1.0f, Math.Max(0.0f, xPerChannelScale));
		}

		/// <summary>
		/// Sets whether or not zoom values should scale in x.
		/// Even when scaling due to zoom in x, there will be a max x scale as defined by SetXPerChannelScale.
		/// Zoom values will always scale in y.
		/// </summary>
		/// <param name="scaleXWhenZooming">Whether or not to scale in x when zooming.</param>
		public void SetScaleXWhenZooming(bool scaleXWhenZooming)
		{
			ScaleXWhenZooming = scaleXWhenZooming;
		}

		/// <summary>
		/// Set the dense and sparse colors of the waveform.
		/// </summary>
		/// <param name="dr">Dense red value as a float between 0.0f and 1.0f.</param>
		/// <param name="dg">Dense green value as a float between 0.0f and 1.0f.</param>
		/// <param name="db">Dense blue value as a float between 0.0f and 1.0f.</param>
		/// <param name="sr">Sparse red value as a float between 0.0f and 1.0f.</param>
		/// <param name="sg">Sparse green value as a float between 0.0f and 1.0f.</param>
		/// <param name="sb">Sparse blue value as a float between 0.0f and 1.0f.</param>
		public void SetColors(float dr, float dg, float db, float sr, float sg, float sb)
		{
			// TODO: Reevaluate if 565 format is needed.
			ushort colorDense = (ushort)(((ushort)(dr * 31) << 11) + ((ushort)(dg * 63) << 5) + (ushort)(db * 31));
			if (colorDense != ColorDense)
			{
				ColorDense = colorDense;
				for (var i = 0; i < Width; i++)
				{
					DenseLine[i] = ColorDense;
				}
			}

			ushort colorSparse = (ushort)(((ushort)(sr * 31) << 11) + ((ushort)(sg * 63) << 5) + (ushort)(sb * 31));
			if (colorSparse != ColorSparse)
			{
				ColorSparse = colorSparse;
				for (var i = 0; i < Width; i++)
				{
					SparseLine[i] = ColorSparse;
				}
			}
		}

		/// <summary>
		/// Sets the SoundMipMap to use.
		/// </summary>
		/// <param name="mipMap">SoundMipMap to use.</param>
		public void SetSoundMipMap(SoundMipMap mipMap)
		{
			MipMap = mipMap;
		}

		/// <summary>
		/// Sets the FocalPoint to the given value.
		/// Values are expected to be pixels in screen space.
		/// </summary>
		/// <param name="focalPoint">Vector representing the focal point.</param>
		public void SetFocalPoint(Vector2 focalPoint)
		{
			FocalPoint = focalPoint;
		}

		/// <summary>
		/// Renders the waveform.
		/// </summary>
		/// <param name="spriteBatch">SpriteBatch to use for rendering the texture.</param>
		/// <param name="x">Optional x offset to draw at.</param>
		/// <param name="y">Optional y offset to draw at.</param>
		public void Draw(SpriteBatch spriteBatch, int x = 0, int y = 0)
		{
			// Draw the current texture.
			spriteBatch.Draw(Textures[TextureIndex], new Vector2(x + FocalPoint.X - (Width >> 1), y), null, Color.White);
			// Advance to the next texture index for the next frame.
			TextureIndex = (TextureIndex + 1) % NumTextures;
		}

		/// <summary>
		/// Perform time-dependent updates.
		/// Updates the underlying texture to represent the waveform at the given time and zoom level.
		/// </summary>
		/// <param name="soundTimeSeconds">Time of the underlying sound in seconds.</param>
		/// <param name="zoom">Zoom level.</param>
		public void Update(double soundTimeSeconds, double zoom, double pixelsPerSecond)
		{
			UpdateTexture(soundTimeSeconds, zoom, pixelsPerSecond);
		}

		/// <summary>
		/// Updates the underlying texture to represent the waveform at the given time and zoom level.
		/// </summary>
		/// <param name="soundTimeSeconds">Time of the underlying sound in seconds.</param>
		/// <param name="zoom">Zoom level.</param>
		private void UpdateTexture(double soundTimeSeconds, double zoom, double pixelsPerSecond)
		{
			// Get the correct texture to update.
			var texture = Textures[TextureIndex];

			// Clear the pixel data to all black.
			// Array.Clear is the most efficient way to do this in practice.
			Array.Clear(BGR565Data, 0, (int)(Width * Height));

			var lockTaken = false;
			try
			{
				// Try to lock, but don't require it. If the lock is already taken then SoundMipMap is destroying
				// or allocating the data. In that case we should just draw the clear texture rather than waiting.
				MipMap.TryLockMipLevels(ref lockTaken);
				if (lockTaken)
				{
					var sampleRate = MipMap.GetSampleRate();

					// Don't render unless there is SoundMipMap data to use.
					// It doesn't matter if the SoundMipMap data is fully generated yet as it can
					// still be partially renderer.
					if (MipMap == null || !MipMap.IsMipMapDataAllocated())
					{
						texture.SetData(BGR565Data);
						return;
					}

					// Determine the zoom to use in x. Zoom in x is separate from zoom in y.
					var xZoom = ScaleXWhenZooming ? Math.Min(1.0, zoom) : 1.0;
					var renderWidth = Width * xZoom;

					var numChannels = MipMap.GetNumChannels();
					var totalWidthPerChannel = (uint)(renderWidth / numChannels);

					//uint endSample = startSample + (uint)(sampleRate / Zoom);
					//uint numSamples = endSample - startSample;
					var samplesPerPixel = sampleRate / pixelsPerSecond / zoom;

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

					var startSampleOffset = (FocalPoint.Y * samplesPerPixel * -1);
					var startSample = (long)(soundTimeSeconds * sampleRate + startSampleOffset);

					// Snap the start sample so that the waveform doesn't jitter while scrolling
					// by moving samples between pixel boundaries on different frames.
					var pixel = (int)(startSample / samplesPerPixel);
					startSample = (long)(pixel * samplesPerPixel);

					var totalNumSamples = MipMap.GetMipLevel0NumSamples();
					var channelMidX = (ushort)(((Width / numChannels) >> 1) - 1);

					// Set up structures for determining the values to use for each row of pixels.
					var minXPerChannel = new ushort[numChannels];
					var maxXPerChannel = new ushort[numChannels];
					var sumOfSquaresPerChannel = new float[numChannels];

					// Set up structures to track the previous values.
					var previousXMin = new ushort[numChannels];
					var previousXMax = new ushort[numChannels];
					// If the first sample index falls within the range of the underlying sound,
					// then copy the previous sample's data for the first previous values. This
					// ensures that when rendering samples that are heavily zoomed in we start
					// the first pixel at the correct location.
					if (startSample > 0 && startSample < totalNumSamples + 1)
					{
						for (var channel = 0; channel < numChannels; channel++)
						{
							var data = MipMap.MipLevels[0].Data[(startSample - 1) * numChannels + channel];
							previousXMin[channel] = data.MinX;
							previousXMax[channel] = data.MaxX;
						}
					}
					// If the first sample index to be rendered is before the first sample in the audio then
					// default to using the middle of the sample range.
					else
					{
						for (var channel = 0; channel < numChannels; channel++)
						{
							previousXMin[channel] = channelMidX;
							previousXMax[channel] = channelMidX;
						}
					}

					// Loop over every y pixel and update the pixels in BGR565Data for that row.
					var sampleIndex = startSample;
					for (uint y = 0; y < Height; y++)
					{
						var bSilentSample = false;
						var bUsePreviousSample = false;
						var numSamplesUsedThisLoop = 0L;

						// Determine the last sample to be considered for this row.
						var endSampleForPixel = (long)((y + 1) * samplesPerPixel) + startSample;
						// Always use at least one sample for data to be rendered.
						if (endSampleForPixel == sampleIndex)
							endSampleForPixel++;

						// Handling for the last pixel being beyond the end of the sound.
						if (endSampleForPixel > totalNumSamples)
						{
							// If both the start and end sample are beyond the end, render silence.
							if ((long)(y * samplesPerPixel) + startSample > totalNumSamples)
							{
								bSilentSample = true;
							}

							// Clamp the end sample.
							endSampleForPixel = totalNumSamples;
						}

						// Set up base values for looping over samples to calculate the min, max, and
						// number of direction changes.
						for (var channel = 0; channel < numChannels; channel++)
						{
							minXPerChannel[channel] = ushort.MaxValue;
							maxXPerChannel[channel] = 0;
							sumOfSquaresPerChannel[channel] = 0.0f;
						}

						// Handling for the first sample for this row being before the start of the sound.
						if (sampleIndex < 0)
						{
							// If the entire range of samples for this row are before the start, use
							// the previous sample, which will default to silence.
							if (endSampleForPixel < 0)
							{
								bUsePreviousSample = true;
							}
							// Otherwise we will loop over samples, so clamp the start sample to 0.
							else
							{
								sampleIndex = 0;
							}
						}

						if (sampleIndex > totalNumSamples)
						{
							bUsePreviousSample = true;
						}

						// If the zoom is so great that this row has no samples, use the previous sample.
						if (sampleIndex >= endSampleForPixel)
						{
							bUsePreviousSample = true;
						}

						// Edge case, use a silent sample.
						if (bSilentSample)
						{
							for (var channel = 0; channel < numChannels; channel++)
							{
								minXPerChannel[channel] = channelMidX;
								maxXPerChannel[channel] = channelMidX;
							}
						}

						// Edge case, use the previous sample.
						else if (bUsePreviousSample)
						{
							for (var channel = 0; channel < numChannels; channel++)
							{
								minXPerChannel[channel] = previousXMin[channel];
								maxXPerChannel[channel] = previousXMax[channel];
							}
						}

						// Normal case, use one or more samples for this row.
						else
						{
							numSamplesUsedThisLoop = endSampleForPixel - sampleIndex;
							while (sampleIndex < endSampleForPixel)
							{
								// Determine the greatest power of two that evenly divides the current sample
								// index and also will not exceed the last sample for this row.
								var powerOfTwo = 2u;
								var mipLevelIndex = 1u;
								while (sampleIndex % powerOfTwo == 0 && sampleIndex + powerOfTwo < endSampleForPixel)
								{
									powerOfTwo <<= 1;
									mipLevelIndex++;
								}

								mipLevelIndex--;
								powerOfTwo >>= 1;

								for (var channel = 0; channel < numChannels; channel++)
								{
									// Use the precomputed sample data at the appropriate mip level.
									var relativeSampleIndex = ((sampleIndex / powerOfTwo) * numChannels) + channel;
									var data = MipMap.MipLevels[mipLevelIndex].Data[relativeSampleIndex];

									// Update tracking variables for min, max, and rms.
									var curMinX = data.MinX;
									var curMaxX = data.MaxX;
									if (curMinX < minXPerChannel[channel])
										minXPerChannel[channel] = curMinX;
									if (curMaxX > maxXPerChannel[channel])
										maxXPerChannel[channel] = curMaxX;
									sumOfSquaresPerChannel[channel] += data.SumOfSquares;
								}

								sampleIndex += powerOfTwo;
							}
						}

						// Now that the min, max, and sum of squares are known for the sample
						// range for this row, convert those value into indices into pixel data so we can
						// update the data.
						for (var channel = 0; channel < numChannels; channel++)
						{
							// Somewhat kludgy, but because we start rendering the waveform before all the
							// mip map data is available, we may process data in its default state where
							// minX is ushort.MaxValue and maxX is 0. In this case it's more graceful to
							// render silence.
							if (maxXPerChannel[channel] < minXPerChannel[channel])
							{
								minXPerChannel[channel] = channelMidX;
								maxXPerChannel[channel] = channelMidX;
							}

							// Prevent gaps due to samples being more than one column apart.
							// Extend this row's min or max to reach the previous row's max or min.
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

							// Scale in the min and max x values based on the zoom and the channel scaling.
							minX = (ushort)(minX * xZoom * XPerChannelScale);
							maxX = (ushort)(maxX * xZoom * XPerChannelScale);
							var range = maxX - minX;

							// Determine the min and max x values for the dense range.
							ushort denseMinX = 0;
							ushort denseMaxX = 0;
							var denseRange = 0.0;
							// Don't draw any dense values if we are only processing one sample.
							if (numSamplesUsedThisLoop > 1)
							{
								// Compute root mean square.
								var rms = Math.Sqrt(sumOfSquaresPerChannel[channel] * (1.0f / numSamplesUsedThisLoop));
								if (rms > 1.0)
									rms = 1.0f;
								var densePercentage = rms;
								denseRange = range * densePercentage;
								denseMinX = (ushort)(minX + (ushort)((range - denseRange) * 0.5f));
								denseMaxX = (ushort)(denseMinX + denseRange);
							}

							// Determine the start index in the overall texture data for this channel.
							var startIndexForRowAndChannel = (int)
								// Start pixel for this row.
								(Width * y
								 // Account for the space to the left of the start due to being zoomed in.
								 + ((Width - renderWidth) * 0.5f)
								 // Account for the offset due to x scaling.
								 + ((totalWidthPerChannel - (totalWidthPerChannel * XPerChannelScale)) * 0.5f)
								 // Account for the channel offset.
								 + (channel * totalWidthPerChannel));

							// Compute the pixel indices for the sparse and dense regions.
							var densePixelStart = startIndexForRowAndChannel + denseMinX;
							var densePixelEnd = startIndexForRowAndChannel + denseMaxX;
							var sparsePixelStart = startIndexForRowAndChannel + minX;
							var sparsePixelEnd = startIndexForRowAndChannel + maxX;

							// Copy the sparse color line into the waveform pixel data.
							Buffer.BlockCopy(SparseLine, 0, BGR565Data, sparsePixelStart << 1,
								(sparsePixelEnd + 1 - sparsePixelStart) << 1);
							// Copy the dense color line into the waveform pixel data.
							if (denseRange > 0.0)
								Buffer.BlockCopy(DenseLine, 0, BGR565Data, densePixelStart << 1,
									(densePixelEnd + 1 - densePixelStart) << 1);
						}
					}
				}
			}
			finally
			{
				if (lockTaken)
					MipMap.UnlockMipLevels();
			}

			// Update the texture with the updated data.
			texture.SetData(BGR565Data);
		}
	}
}

using System;
using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using Vector2 = Microsoft.Xna.Framework.Vector2;
using static Fumen.FumenExtensions;

namespace StepManiaEditor;

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
	/// Sentinel value for an invalid index.
	/// </summary>
	private const long QuantizedSampleIndexInvalid = -1L;

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
	/// The y focal point for orienting the waveform and controlling zooming.
	/// Units are in pixels and are in screen space.
	/// The sound time provided in UpdateTexture is the time at this focal point position.
	/// </summary>
	private int FocalPointY;

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
	/// Bit positions for fast determination of a number's lowest bit index.
	/// See https://graphics.stanford.edu/~seander/bithacks.html and
	/// https://en.wikipedia.org/wiki/De_Bruijn_sequence
	/// </summary>
	private static readonly int[] DeBruijnBitPositions =
	{
		0, 1, 28, 2, 29, 14, 24, 3, 30, 22, 20, 15, 25, 17, 4, 8,
		31, 27, 13, 23, 21, 19, 16, 7, 26, 12, 18, 6, 11, 5, 10, 9,
	};

	/// <summary>
	/// Zoom level last frame. Used to determine if we can re-use any of the last frame's data.
	/// </summary>
	private double LastZoom;

	/// <summary>
	/// Pixels per second last frame. Used to determine if we can re-use any of the last frame's data.
	/// </summary>
	private double LastPixelsPerSecond = 1.0;

	/// <summary>
	/// Last frame's quantized sample start index.
	/// </summary>
	private long LastQuantizedIndexStart = QuantizedSampleIndexInvalid;

	/// <summary>
	/// Last frame's quantized sample end index.
	/// </summary>
	private long LastQuantizedIndexEnd = QuantizedSampleIndexInvalid;

	/// <summary>
	/// Whether or not the mip map data was loading last frame.
	/// </summary>
	private bool WasMipMapDataLoadingLastFrame = true;

	/// <summary>
	/// X scale to apply to the dense region.
	/// </summary>
	private float DenseScale = 6.0f;

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
		xPerChannelScale = Math.Min(1.0f, Math.Max(0.0f, xPerChannelScale));
		if (XPerChannelScale.FloatEquals(xPerChannelScale))
			return;

		XPerChannelScale = xPerChannelScale;
		InvalidateLastFrameData();
	}

	/// <summary>
	/// Sets whether or not zoom values should scale in x.
	/// Even when scaling due to zoom in x, there will be a max x scale as defined by SetXPerChannelScale.
	/// Zoom values will always scale in y.
	/// </summary>
	/// <param name="scaleXWhenZooming">Whether or not to scale in x when zooming.</param>
	public void SetScaleXWhenZooming(bool scaleXWhenZooming)
	{
		if (ScaleXWhenZooming == scaleXWhenZooming)
			return;
		ScaleXWhenZooming = scaleXWhenZooming;
		InvalidateLastFrameData();
	}

	/// <summary>
	/// Set the dense and sparse colors of the waveform.
	/// </summary>
	/// <param name="colorDense">Dense color in BGR565 format.</param>
	/// <param name="colorSparse">Sparse color in BGR565 format.</param>
	public void SetColors(ushort colorDense, ushort colorSparse)
	{
		var diff = false;
		if (colorDense != ColorDense)
		{
			ColorDense = colorDense;
			for (var i = 0; i < Width; i++)
			{
				DenseLine[i] = ColorDense;
			}

			diff = true;
		}

		if (colorSparse != ColorSparse)
		{
			ColorSparse = colorSparse;
			for (var i = 0; i < Width; i++)
			{
				SparseLine[i] = ColorSparse;
			}

			diff = true;
		}

		if (diff)
			InvalidateLastFrameData();
	}

	public void SetDenseScale(float denseScale)
	{
		if (!DenseScale.FloatEquals(denseScale))
		{
			DenseScale = denseScale;
			InvalidateLastFrameData();
		}
	}

	/// <summary>
	/// Sets the SoundMipMap to use.
	/// </summary>
	/// <param name="mipMap">SoundMipMap to use.</param>
	public void SetSoundMipMap(SoundMipMap mipMap)
	{
		if (MipMap == mipMap)
			return;
		MipMap = mipMap;
		InvalidateLastFrameData();
	}

	/// <summary>
	/// Sets the FocalPoint Y value to the given value.
	/// Values are expected to be pixels in screen space.
	/// </summary>
	/// <param name="focalPointY">Focal point y value.</param>
	public void SetFocalPointY(int focalPointY)
	{
		if (FocalPointY == focalPointY)
			return;
		FocalPointY = focalPointY;
		InvalidateLastFrameData();
	}

	/// <summary>
	/// Invalidates the last frame data such that the entire texture will be recreated during the next update.
	/// </summary>
	private void InvalidateLastFrameData()
	{
		LastQuantizedIndexStart = QuantizedSampleIndexInvalid;
		LastQuantizedIndexEnd = QuantizedSampleIndexInvalid;
	}

	/// <summary>
	/// Returns whether or not the last frame data is valid and can be used as a performance optimization
	/// during the next update.
	/// </summary>
	private bool IsLastFrameDataValid()
	{
		return LastQuantizedIndexStart != QuantizedSampleIndexInvalid && LastQuantizedIndexEnd != QuantizedSampleIndexInvalid;
	}

	/// <summary>
	/// Renders the waveform.
	/// </summary>
	/// <param name="spriteBatch">SpriteBatch to use for rendering the texture.</param>
	/// <param name="x">X position to draw at.</param>
	/// <param name="y">Y position to draw at.</param>
	public void Draw(SpriteBatch spriteBatch, int x = 0, int y = 0)
	{
		// Draw the current texture.
		spriteBatch.Draw(Textures[TextureIndex], new Vector2(x, y), null, Color.White);
		// Advance to the next texture index for the next frame.
		TextureIndex = (TextureIndex + 1) % NumTextures;
	}

	/// <summary>
	/// Perform time-dependent updates.
	/// Updates the underlying texture to represent the waveform at the given time and zoom level.
	/// </summary>
	/// <param name="soundTimeSeconds">Time of the underlying sound in seconds.</param>
	/// <param name="zoom">Zoom level.</param>
	/// <param name="pixelsPerSecond">The number of y pixels which cover 1 second of time in the sound.</param>
	public void Update(double soundTimeSeconds, double zoom, double pixelsPerSecond)
	{
		UpdateTexture(soundTimeSeconds, zoom, pixelsPerSecond);
	}

	/// <summary>
	/// Updates the underlying texture to represent the waveform at the given time and zoom level.
	/// </summary>
	/// <param name="soundTimeSeconds">Time of the underlying sound in seconds.</param>
	/// <param name="zoom">Zoom level.</param>
	/// <param name="pixelsPerSecond">The number of y pixels which cover 1 second of time in the sound.</param>
	private void UpdateTexture(double soundTimeSeconds, double zoom, double pixelsPerSecond)
	{
		// Get the correct texture to update.
		var texture = Textures[TextureIndex];

		var lockTaken = false;
		try
		{
			// Try to lock, but don't require it. If the lock is already taken then SoundMipMap is destroying
			// or allocating the data. In that case we should just draw the clear texture rather than waiting.
			MipMap.TryLockMipLevels(ref lockTaken);
			if (!lockTaken)
			{
				Array.Clear(BGR565Data, 0, (int)(Width * Height));
			}
			else
			{
				// Don't render unless there is SoundMipMap data to use.
				// It doesn't matter if the SoundMipMap data is fully generated yet as it can
				// still be partially renderer.
				if (MipMap == null || !MipMap.IsMipMapDataAllocated())
				{
					Array.Clear(BGR565Data, 0, (int)(Width * Height));
					texture.SetData(BGR565Data);
					return;
				}

				// If the parameters have changed since last time, invalidate the last frame data so we do not use it.
				// Also invalidate the last frame data if the SoundMipMap is still loading since the underlying data will
				// be changing each frame.
				var isMipMapDataLoaded = MipMap.IsMipMapDataLoaded();
				if (!zoom.DoubleEquals(LastZoom)
				    || !pixelsPerSecond.DoubleEquals(LastPixelsPerSecond)
				    || !isMipMapDataLoaded
				    || WasMipMapDataLoadingLastFrame)
				{
					InvalidateLastFrameData();
				}

				WasMipMapDataLoadingLastFrame = !isMipMapDataLoaded;

				// Determine the zoom to use in x. Zoom in x is separate from zoom in y.
				var xZoom = ScaleXWhenZooming ? Math.Min(1.0, zoom) : 1.0;
				var renderWidth = Width * xZoom;
				var numChannels = MipMap.GetNumChannels();
				var totalWidthPerChannel = (uint)(renderWidth / numChannels);

				var sampleRate = MipMap.GetSampleRate();
				var samplesPerPixel = sampleRate / pixelsPerSecond / zoom;

				// For a given pixel per second rate, we must ensure that the same samples are always grouped
				// together when rendering to prevent jittering artifacts and to allow reusing portions of the
				// previous frame's buffer. To accomplish this we need to quantize the sample indices we use per
				// pixel to samples which fall on consistent integer boundaries that match the samples per pixel.
				var startSampleOffset = FocalPointY * samplesPerPixel * -1;
				var startSampleDouble = soundTimeSeconds * sampleRate + startSampleOffset;
				var quantizedStartSampleIndex = (long)(startSampleDouble / samplesPerPixel);
				var quantizedEndSampleIndex = quantizedStartSampleIndex + Height;

				// Try to reuse the buffer state from the last frame if it overlaps the area covered this frame.
				uint yStart = 0;
				var yEnd = Height;
				if (IsLastFrameDataValid())
				{
					// The previous range is identical to this range.
					// Just reuse last frame's buffer.
					if (LastQuantizedIndexStart == quantizedStartSampleIndex)
					{
						yEnd = 0;
					}

					// The previous range overlaps the end of this frame's range.
					else if (LastQuantizedIndexStart > quantizedStartSampleIndex
					         && LastQuantizedIndexStart < quantizedEndSampleIndex)
					{
						var diff = LastQuantizedIndexStart - quantizedStartSampleIndex;

						// This frame, compute from pixel 0 to the the start of last frame's data.
						yEnd = (uint)diff;
						// Copy the start of last frame's buffer to where it falls within this frame's range.
						Array.Copy(BGR565Data, 0, BGR565Data, (int)(Width * diff), (Height - diff) * Width);
						// Clear the top of the buffer so we can write to it.
						Array.Clear(BGR565Data, 0, (int)(Width * diff));
					}

					// The previous range overlaps the start of this frame's range.
					else if (LastQuantizedIndexEnd - 1 > quantizedStartSampleIndex
					         && LastQuantizedIndexEnd < quantizedEndSampleIndex)
					{
						var diff = quantizedEndSampleIndex - LastQuantizedIndexEnd;

						// This frame, compute from the end of last frame's data to the end of the texture.
						yStart = (uint)(Height - diff);
						// Copy the end of last frame's buffer to where it falls within this frame's range.
						Array.Copy(BGR565Data, diff * Width, BGR565Data, 0, (Height - diff) * Width);
						// Clear the bottom of the buffer so we can write to it.
						Array.Clear(BGR565Data, (int)(yStart * Width), (int)(diff * Width));
					}

					// The previous range does not overlap the new range at all.
					// Clear the entire buffer.
					else
					{
						Array.Clear(BGR565Data, 0, (int)(Width * Height));
					}
				}
				// No valid last frame data to leverage, clear the buffer and fill it entirely.
				else
				{
					Array.Clear(BGR565Data, 0, (int)(Width * Height));
				}

				// Update the last frame tracking variables for the next frame.
				LastZoom = zoom;
				LastPixelsPerSecond = pixelsPerSecond;
				LastQuantizedIndexStart = quantizedStartSampleIndex;
				LastQuantizedIndexEnd = quantizedEndSampleIndex;

				var totalNumSamples = MipMap.GetMipLevel0NumSamples();
				var channelMidX = (ushort)(((Width / numChannels) >> 1) - 1);

				// Set up structures for determining the values to use for each row of pixels.
				var minXPerChannel = new ushort[numChannels];
				var maxXPerChannel = new ushort[numChannels];
				var totalDistancePerChannel = new float[numChannels];

				// Set up structures to track the previous values.
				var previousXMin = new ushort[numChannels];
				var previousXMax = new ushort[numChannels];

				// If the first sample index falls within the range of the underlying sound,
				// then copy the previous sample's data for the first previous values. This
				// ensures that when rendering samples that are heavily zoomed in we start
				// the first pixel at the correct location.
				var previousSample = (long)((quantizedStartSampleIndex + yStart - 1) * samplesPerPixel + 0.5);
				if (previousSample > 0 && previousSample < totalNumSamples)
				{
					for (var channel = 0; channel < numChannels; channel++)
					{
						var data = MipMap.MipLevels[0].Data[previousSample * numChannels + channel];
						previousXMin[channel] = SoundMipMap.MipLevel.GetMin(data);
						previousXMax[channel] = SoundMipMap.MipLevel.GetMax(data);
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
				for (var y = yStart; y < yEnd; y++)
				{
					var bSilentSample = false;
					var bUsePreviousSample = false;
					var numSamplesUsedThisLoop = 0L;

					// Determine the last sample to be considered for this row.
					var quantizedIndex = quantizedStartSampleIndex + y;
					var sampleIndex = (long)(quantizedIndex * samplesPerPixel + 0.5);
					var endSampleForPixel = (long)((quantizedIndex + 1) * samplesPerPixel + 0.5);

					// Always use at least one sample for data to be rendered.
					if (endSampleForPixel == sampleIndex)
						endSampleForPixel++;

					// Handling for the last pixel being beyond the end of the sound.
					if (endSampleForPixel > totalNumSamples)
					{
						// If both the start and end sample are beyond the end, render silence.
						if ((long)(y * samplesPerPixel) + sampleIndex > totalNumSamples)
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
						totalDistancePerChannel[channel] = 0.0f;
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
							// Determine the largest power of two which evenly divides the current sample index.
							var powerOfTwo = sampleIndex & -sampleIndex;
							// For the first sample, use the same range for this pixel.
							if (sampleIndex == 0)
								powerOfTwo = endSampleForPixel & -endSampleForPixel;
							// Halve the power of two until we do not overshoot samples for this pixel.
							while (sampleIndex + powerOfTwo > endSampleForPixel)
								powerOfTwo >>= 1;
							// Get the index for this power of two for looking up the appropriate mip level data.
							var mipLevelIndex = DeBruijnBitPositions[(uint)(powerOfTwo * 0x077CB531U) >> 27];

							// Get the data and sample index into that data.
							var mipLevelData = MipMap.MipLevels[mipLevelIndex];
							var relativeSampleIndex = sampleIndex / powerOfTwo * numChannels;

							// Update tracking variables for each channel.
							for (var channel = 0;
							     channel < numChannels;
							     channel++, relativeSampleIndex++)
							{
								var data = mipLevelData.Data[relativeSampleIndex];
								var min = SoundMipMap.MipLevel.GetMin(data);
								var max = SoundMipMap.MipLevel.GetMax(data);
								var d = SoundMipMap.MipLevel.GetDistanceOverSamples(data);

								if (min < minXPerChannel[channel])
									minXPerChannel[channel] = min;
								if (max > maxXPerChannel[channel])
									maxXPerChannel[channel] = max;
								totalDistancePerChannel[channel] += (float)d * powerOfTwo;
							}

							sampleIndex += powerOfTwo;
						}
					}

					// Now that the values are known for the sample range for this row, convert
					// those value into indices into pixel data so we can update the data.
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
							var factor = totalDistancePerChannel[channel] / numSamplesUsedThisLoop
							                                              / totalWidthPerChannel * DenseScale;
							if (factor > 1.0f)
								factor = 1.0f;
							var densePercentage = factor;
							denseRange = range * densePercentage;
							denseMinX = (ushort)(minX + (ushort)((range - denseRange) * 0.5f));
							denseMaxX = (ushort)(denseMinX + denseRange);
						}

						// Determine the start index in the overall texture data for this channel.
						var startIndexForRowAndChannel = (int)
							// Start pixel for this row.
							(Width * y
							 // Account for the space to the left of the start due to being zoomed in.
							 + (Width - renderWidth) * 0.5f
							 // Account for the offset due to x scaling.
							 + (totalWidthPerChannel - totalWidthPerChannel * XPerChannelScale) * 0.5f
							 // Account for the channel offset.
							 + channel * totalWidthPerChannel);

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

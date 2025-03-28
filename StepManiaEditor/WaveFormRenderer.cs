﻿using System;
using Microsoft.Xna.Framework.Graphics;
using MonoGameExtensions;
using static Fumen.FumenExtensions;

namespace StepManiaEditor;

/// <summary>
/// Renders a SoundMipMap as a waveform to an underlying double-buffered set of Textures.
/// Used for performant real-time rendering of animating audio data.
/// Call Update() to update the underlying Textures.
/// Call Draw() to render the generated Textures to the given SpriteBatch.
///
/// Positioning:
/// The Y value of the FocalPoint is used for controlling at what y pixel value the provided
/// sound time from Update() should be rendered at.
/// </summary>
public class WaveFormRenderer
{
	/// <summary>
	/// Sentinel value for an invalid index.
	/// </summary>
	private const long QuantizedSampleIndexInvalid = -1L;

	/// <summary>
	/// Color for sparse area of waveform. BGR565.
	/// </summary>
	private ushort ColorSparse;

	/// <summary>
	/// Color for dense area of waveform. BGR565.
	/// </summary>
	private ushort ColorDense;

	/// <summary>
	/// Width of texture in pixels.
	/// </summary>
	private uint TextureWidth;

	/// <summary>
	/// Height of texture in pixels.
	/// </summary>
	private uint TextureHeight;

	/// <summary>
	/// Height of the visible area of the waveform in pixels.
	/// Less than or equal to TextureHeight.
	/// This is tracked separately as UI resizing can cause the visible area to change
	/// often, but we do not want to perform expensive texture resizes that often.
	/// </summary>
	private uint VisibleHeight;

	/// <summary>
	/// The y focal point for orienting the waveform and controlling zooming.
	/// Units are in pixels and are in local screen space. This represents an offset from the top of the Waveform.
	/// The sound time provided in UpdateTexture is the time at this focal point position.
	/// </summary>
	private int FocalPointLocalY;

	/// <summary>
	/// Scale in X to apply to each channel.
	/// </summary>
	private float XPerChannelScale = 1.0f;

	/// <summary>
	/// RenderTarget2D to render to.
	/// </summary>
	private DoubleBufferedRenderTarget2D<ushort> RenderTarget;

	/// <summary>
	/// One row of dense colored pixels, used for copying memory quickly into the data buffer instead of looping.
	/// </summary>
	private ushort[] DenseLine;

	/// <summary>
	/// One row of sparse colored pixels, used for copying memory quickly into the data buffer instead of looping.
	/// </summary>
	private ushort[] SparseLine;

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
	[
		0, 1, 28, 2, 29, 14, 24, 3, 30, 22, 20, 15, 25, 17, 4, 8,
		31, 27, 13, 23, 21, 19, 16, 7, 26, 12, 18, 6, 11, 5, 10, 9,
	];

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
	/// <param name="textureWidth">Texture width in pixels.</param>
	/// <param name="textureHeight">Texture height in pixels.</param>
	public WaveFormRenderer(GraphicsDevice graphicsDevice, uint textureWidth, uint textureHeight)
	{
		Resize(graphicsDevice, textureWidth, textureHeight, textureHeight);
	}

	/// <summary>
	/// Resize the WaveForm.
	/// </summary>
	/// <param name="graphicsDevice">GraphicsDevice to use for creating textures.</param>
	/// <param name="textureWidth">Texture width in pixels.</param>
	/// <param name="textureHeight">Texture height in pixels.</param>
	/// <param name="visibleHeight">Visible height in pixels of the waveform.</param>
	public void Resize(GraphicsDevice graphicsDevice, uint textureWidth, uint textureHeight, uint visibleHeight)
	{
		textureWidth = Math.Max(1, textureWidth);
		textureHeight = Math.Max(1, textureHeight);
		visibleHeight = Math.Clamp(visibleHeight, 1, textureHeight);

		var shouldInvalidateData = false;
		if (TextureWidth != textureWidth || TextureHeight != textureHeight)
		{
			shouldInvalidateData = true;

			TextureWidth = textureWidth;
			TextureHeight = textureHeight;

			// Set up the render target.
			RenderTarget = new DoubleBufferedRenderTarget2D<ushort>(graphicsDevice, (int)TextureWidth, (int)TextureHeight,
				SurfaceFormat.Bgr565, DepthFormat.Depth24);

			// Set up the pixel data.
			DenseLine = new ushort[TextureWidth];
			SparseLine = new ushort[TextureWidth];
			for (var i = 0; i < TextureWidth; i++)
			{
				DenseLine[i] = ColorDense;
				SparseLine[i] = ColorSparse;
			}
		}

		if (VisibleHeight != visibleHeight)
		{
			VisibleHeight = visibleHeight;
			shouldInvalidateData = true;
		}

		if (shouldInvalidateData)
			InvalidateLastFrameData();
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
			for (var i = 0; i < TextureWidth; i++)
			{
				DenseLine[i] = ColorDense;
			}

			diff = true;
		}

		if (colorSparse != ColorSparse)
		{
			ColorSparse = colorSparse;
			for (var i = 0; i < TextureWidth; i++)
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
	/// Sets the local focal point Y value to the given value.
	/// Values are expected to be pixels in screen space relative to the top of the Waveform texture.
	/// </summary>
	/// <param name="focalPointLocalY">Focal point y value.</param>
	public void SetFocalPointLocalY(int focalPointLocalY)
	{
		if (FocalPointLocalY == focalPointLocalY)
			return;
		FocalPointLocalY = focalPointLocalY;
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
	public RenderTarget2D Draw()
	{
		return RenderTarget.Draw();
	}

	/// <summary>
	/// Perform time-dependent updates.
	/// Updates the underlying texture to represent the waveform at the given time and zoom level.
	/// </summary>
	/// <param name="soundTimeSeconds">Time of the underlying sound in seconds.</param>
	/// <param name="pixelsPerSecond">The number of y pixels which cover 1 second of time in the sound.</param>
	public void Update(double soundTimeSeconds, double pixelsPerSecond)
	{
		var lockTaken = false;
		var renderTargetData = RenderTarget.GetCurrentData();
		var (lastFrameDataValid, lastFrameData) = RenderTarget.GetLastFrameData();
		try
		{
			// Try to lock, but don't require it. If the lock is already taken then SoundMipMap is destroying
			// or allocating the data. In that case we should just draw the clear texture rather than waiting.
			MipMap.TryLockMipLevels(ref lockTaken);
			if (!lockTaken)
			{
				Array.Clear(renderTargetData, 0, (int)(TextureWidth * VisibleHeight));
			}
			else
			{
				if (!lastFrameDataValid)
					InvalidateLastFrameData();

				// Don't render unless there is SoundMipMap data to use.
				// It doesn't matter if the SoundMipMap data is fully generated yet as it can
				// still be partially renderer.
				if (MipMap == null || !MipMap.IsMipMapDataAllocated())
				{
					Array.Clear(renderTargetData, 0, (int)(TextureWidth * VisibleHeight));
					return;
				}

				// If the parameters have changed since last time, invalidate the last frame data so we do not use it.
				// Also invalidate the last frame data if the SoundMipMap is still loading since the underlying data will
				// be changing each frame.
				var isMipMapDataLoaded = MipMap.IsMipMapDataLoaded();
				if (!pixelsPerSecond.DoubleEquals(LastPixelsPerSecond)
				    || !isMipMapDataLoaded
				    || WasMipMapDataLoadingLastFrame)
				{
					InvalidateLastFrameData();
				}

				WasMipMapDataLoadingLastFrame = !isMipMapDataLoaded;

				// Determine the zoom to use in x. Zoom in x is separate from zoom in y.
				var renderWidth = TextureWidth;
				var numChannels = MipMap.GetNumChannels();
				var widthPerChannel = TextureWidth / numChannels;
				var totalWidthPerChannel = renderWidth / numChannels;
				var sampleRate = MipMap.GetSampleRate();
				var samplesPerPixel = sampleRate / pixelsPerSecond;

				// For a given pixel per second rate, we must ensure that the same samples are always grouped
				// together when rendering to prevent jittering artifacts and to allow reusing portions of the
				// previous frame's buffer. To accomplish this we need to quantize the sample indices we use per
				// pixel to samples which fall on consistent integer boundaries that match the samples per pixel.
				var startSampleOffset = FocalPointLocalY * samplesPerPixel * -1;
				var startSampleDouble = soundTimeSeconds * sampleRate + startSampleOffset;
				var quantizedStartSampleIndex = (long)(startSampleDouble / samplesPerPixel);
				var quantizedEndSampleIndex = quantizedStartSampleIndex + VisibleHeight;

				// Try to reuse the buffer state from the last frame if it overlaps the area covered this frame.
				uint yStart = 0;
				var yEnd = VisibleHeight;
				if (IsLastFrameDataValid())
				{
					// The previous range is identical to this range.
					// Just reuse last frame's buffer.
					if (LastQuantizedIndexStart == quantizedStartSampleIndex)
					{
						Array.Copy(lastFrameData, renderTargetData, TextureWidth * TextureHeight);
						yEnd = 0;
					}

					// The previous range overlaps the end of this frame's range.
					else if (LastQuantizedIndexStart > quantizedStartSampleIndex
					         && LastQuantizedIndexStart < quantizedEndSampleIndex)
					{
						var diff = LastQuantizedIndexStart - quantizedStartSampleIndex;

						// This frame, compute from pixel 0 to the start of last frame's data.
						yEnd = (uint)diff;
						// Copy the start of last frame's buffer to where it falls within this frame's range.
						Array.Copy(lastFrameData, 0, renderTargetData, (int)(TextureWidth * diff),
							(VisibleHeight - diff) * TextureWidth);
						// Clear the top of the buffer so we can write to it.
						Array.Clear(renderTargetData, 0, (int)(TextureWidth * diff));
					}

					// The previous range overlaps the start of this frame's range.
					else if (LastQuantizedIndexEnd - 1 > quantizedStartSampleIndex
					         && LastQuantizedIndexEnd < quantizedEndSampleIndex)
					{
						var diff = quantizedEndSampleIndex - LastQuantizedIndexEnd;

						// This frame, compute from the end of last frame's data to the end of the texture.
						yStart = (uint)(VisibleHeight - diff);
						// Copy the end of last frame's buffer to where it falls within this frame's range.
						Array.Copy(lastFrameData, diff * TextureWidth, renderTargetData, 0,
							(VisibleHeight - diff) * TextureWidth);
						// Clear the bottom of the buffer so we can write to it.
						Array.Clear(renderTargetData, (int)(yStart * TextureWidth), (int)(diff * TextureWidth));
					}

					// The previous range does not overlap the new range at all.
					// Clear the entire buffer.
					else
					{
						Array.Clear(renderTargetData, 0, (int)(TextureWidth * VisibleHeight));
					}
				}
				// No valid last frame data to leverage, clear the buffer and fill it entirely.
				else
				{
					Array.Clear(renderTargetData, 0, (int)(TextureWidth * VisibleHeight));
				}

				// Update the last frame tracking variables for the next frame.
				LastPixelsPerSecond = pixelsPerSecond;
				LastQuantizedIndexStart = quantizedStartSampleIndex;
				LastQuantizedIndexEnd = quantizedEndSampleIndex;

				var totalNumSamples = MipMap.GetMipLevel0NumSamples();
				var channelMidX = (ushort)(((TextureWidth / numChannels) >> 1) - 1);

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

				// Loop over every y pixel and update the pixels in renderTargetData for that row.
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
						minX = (ushort)(minX * XPerChannelScale);
						maxX = (ushort)(maxX * XPerChannelScale);
						var range = maxX - minX;

						// Determine the min and max x values for the dense range.
						ushort denseMinX = 0;
						ushort denseMaxX = 0;
						var denseRange = 0.0;
						// Don't draw any dense values if we are only processing one sample.
						if (numSamplesUsedThisLoop > 1)
						{
							var factor = totalDistancePerChannel[channel] / numSamplesUsedThisLoop / widthPerChannel * DenseScale;
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
							(TextureWidth * y
							 // Account for the space to the left of the start due to being zoomed in.
							 + (TextureWidth - renderWidth) * 0.5f
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
						Buffer.BlockCopy(SparseLine, 0, renderTargetData, sparsePixelStart << 1,
							(sparsePixelEnd + 1 - sparsePixelStart) << 1);
						// Copy the dense color line into the waveform pixel data.
						if (denseRange > 0.0)
							Buffer.BlockCopy(DenseLine, 0, renderTargetData, densePixelStart << 1,
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
	}
}

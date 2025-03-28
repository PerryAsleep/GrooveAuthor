using System;
using Fumen;

namespace StepManiaEditor;

/// <summary>
/// Class for stretching a sound.
/// Does not preserve rates.
/// </summary>
internal sealed class TimeStretcher
{
	/// <summary>
	/// Stretches the sound represented by the given input buffer by the given rate using simple spline interpolation.
	/// </summary>
	/// <param name="sampleIndex">The current sample index into the inputBuffer.</param>
	/// <param name="inputBuffer">The buffer containing the sound data to stretch.</param>
	/// <param name="sampleRate">The sample rate of the input buffer.</param>
	/// <param name="numChannels">The number of the input and output buffers.</param>
	/// <param name="outputBuffer">The output buffer to write into. This buffer will be filled.</param>
	/// <param name="rate">The time stretch rate.</param>
	public static void ProcessSound(
		long sampleIndex,
		float[] inputBuffer,
		uint sampleRate,
		int numChannels,
		float[] outputBuffer,
		double rate)
	{
		// The bulk of this logic is duplicated in SoundManager.FillSamples.
		// Ideally it lives in one spot, but the logic is slightly different between
		// the two current implementations due to the formats of the data being parsed.
		// Abstracting out the differences would involve perf overhead that currently
		// isn't worth it.
		const int numHermitePoints = 4;
		var hermiteTimeRange = 1.0f / sampleRate;
		var hermitePoints = new float[numHermitePoints];
		var maxInputIndex = inputBuffer.Length - 1;

		var startTime = sampleIndex / (double)sampleRate;
		var outputBufferNumSamples = outputBuffer.Length / numChannels;

		for (var s = 0; s < outputBufferNumSamples; s++)
		{
			var t = startTime + s * rate / sampleRate;

			// Find the start of the four points in the original data corresponding to this time
			// so we can use them for hermite spline interpolation. Note the minus 1 here is to
			// account for four samples. The floor and the minus one result in getting the sample
			// two indexes preceding the desired time.
			var startInputSampleIndex = (int)(t * sampleRate) - 1;
			// Determine the time of the x1 sample in order to find the normalized time.
			var x1Time = (double)(startInputSampleIndex + 1) / sampleRate;
			// Determine the normalized time for the interpolation.
			var normalizedTime = (float)((t - x1Time) / hermiteTimeRange);

			for (var channel = 0; channel < numChannels; channel++)
			{
				// Get all four input points for the interpolation.
				for (var hermiteIndex = 0; hermiteIndex < numHermitePoints; hermiteIndex++)
				{
					// Get the input index. We need to clamp as it is expected at the ends for the range to exceed the
					// range of the input data.
					var inputIndex = Math.Clamp((startInputSampleIndex + hermiteIndex) * numChannels + channel, 0, maxInputIndex);
					// Parse the sample at this index.
					// This often results in redundant parses, but in practice optimizing them out isn't a big gain,
					// and it adds a lot of complexity. The main perf hit is InterpolateHermite.
					hermitePoints[hermiteIndex] = inputBuffer[inputIndex];
				}

				// Now that all four samples are known, interpolate them and store the result.
				outputBuffer[s * numChannels + channel] = Interpolation.HermiteInterpolate(hermitePoints[0],
					hermitePoints[1], hermitePoints[2], hermitePoints[3], normalizedTime);
			}
		}
	}
}

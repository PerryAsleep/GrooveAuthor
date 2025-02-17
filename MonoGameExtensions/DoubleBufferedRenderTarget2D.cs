using Microsoft.Xna.Framework.Graphics;

namespace MonoGameExtensions;

/// <summary>
/// Class for managing double-buffered render targets.
/// </summary>
/// <typeparam name="T">Type of color data.</typeparam>
public sealed class DoubleBufferedRenderTarget2D<T> where T : struct
{
	/// <summary>
	/// Number of render targets.
	/// </summary>
	private const int NumTargets = 2;

	/// <summary>
	/// Each render target.
	/// </summary>
	private readonly RenderTarget2D[] RenderTargets;

	/// <summary>
	/// Color data for each render target.
	/// </summary>
	private readonly T[][] Data;

	/// <summary>
	/// Flag for whether the last frame data is valid.
	/// </summary>
	private readonly bool[] LastFrameDataValid;

	/// <summary>
	/// Current buffer index.
	/// </summary>
	private int CurrentTargetIndex;

	public DoubleBufferedRenderTarget2D(
		GraphicsDevice graphicsDevice,
		int width,
		int height,
		SurfaceFormat preferredFormat,
		DepthFormat preferredDepthFormat)
	{
		RenderTargets = new RenderTarget2D[NumTargets];
		Data = new T[NumTargets][];
		LastFrameDataValid = new bool[NumTargets];
		for (var i = 0; i < NumTargets; i++)
		{
			RenderTargets[i] = new RenderTarget2D(graphicsDevice, width, height, false, preferredFormat, preferredDepthFormat);

			// Our render targets use internally managed color data. For graphics engines like OpenGL updating
			// data on a RenderTarget2D involves synchronizing that data with the GPU which can stall the frame.
			// This is only needed if the data has the potential to change before rendering. Since we are
			// double-buffered we can guarantee that the data will remain valid so we can avoid the expensive
			// synchronization.
			RenderTargets[i].DoNotSynchronizeSetDataCallsWithGPU = true;

			// Have the render targets pin our data and re-use it. Without doing this they would alloc and free
			// a buffer per-frame with each call to SetData.
			Data[i] = new T[width * height];
			RenderTargets[i].PinData(Data[i]);
		}
	}

	/// <summary>
	/// Gets the data for the current frame.
	/// This data is safe to manipulate up until the next Draw call.
	/// </summary>
	/// <returns></returns>
	public T[] GetCurrentData()
	{
		return Data[CurrentTargetIndex];
	}

	/// <summary>
	/// Gets the data from the previous frame.
	/// This data is safe to read but not safe to write.
	/// </summary>
	/// <returns>
	/// Tuple with the following values:
	///  Boolean representing whether the data is valid for use. The last frame data will not be valid on the first frame.
	///  The data from the last frame.
	/// </returns>
	public (bool, T[]) GetLastFrameData()
	{
		var lastFrame = CurrentTargetIndex - 1;
		if (lastFrame < 0)
			lastFrame = NumTargets - 1;
		return (LastFrameDataValid[lastFrame], Data[lastFrame]);
	}

	/// <summary>
	/// Draws
	/// </summary>
	/// <returns></returns>
	public RenderTarget2D Draw()
	{
		var rt = RenderTargets[CurrentTargetIndex];
		rt.SetData(Data[CurrentTargetIndex]);
		LastFrameDataValid[CurrentTargetIndex] = true;
		CurrentTargetIndex = (CurrentTargetIndex + 1) % NumTargets;
		return rt;
	}
}

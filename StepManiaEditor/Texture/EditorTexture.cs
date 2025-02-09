using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Fumen;
using Microsoft.Xna.Framework.Graphics;
using MonoGameExtensions;
using SkiaSharp;
using static MonoGameExtensions.TextureUtils;

namespace StepManiaEditor;

/// <summary>
/// A MonoGame Texture to render via ImGui or Monogame.
/// May internally control multiple Texture2D objects for animated textures like gifs.
/// Meant for dynamically loaded textures that are not atlassed and not known until runtime.
/// Provides idempotent asynchronous loading methods to update the Texture with a new file from disk.
/// When loading a new image over a previous image there will be a brief period of time when two textures
/// are loaded in memory at once. The old texture will be released for garbage collection after the new
/// texture is fully loaded.
///
/// Expected usage:
///  Call LoadAsync or UnloadAsync to load or unload the texture.
///  Call Draw or DrawButton once per frame as needed to draw the texture through ImGui.
///  Call DrawTexture once per frame as needed to draw the texture through Monogame.
/// </summary>
internal sealed class EditorTexture : IDisposable
{
	/// <summary>
	/// CancellableTask for loading the EditorTexture.
	/// </summary>
	internal sealed class TextureLoadTask : CancellableTask<string>
	{
		/// <summary>
		/// Whether or not to cache the texture color when loading.
		/// </summary>
		private readonly bool CacheTextureColor;

		/// <summary>
		/// GraphicsDevice for loading the texture.
		/// </summary>
		private readonly GraphicsDevice GraphicsDevice;

		/// <summary>
		/// The last Textures loaded.
		/// </summary>
		private Texture2D[] Textures;

		/// <summary>
		/// The last frame durations loaded.
		/// </summary>
		private double[] FrameDurations;

		/// <summary>
		/// The color of the last Texture loaded.
		/// </summary>
		private uint TextureColor;

		/// <summary>
		/// Lock for updating and retrieving results.
		/// </summary>
		private readonly object Lock = new();

		/// <summary>
		/// Whether or not to log errors.
		/// </summary>
		private bool LogErrors = true;

		public TextureLoadTask(bool cacheTextureColor, GraphicsDevice graphicsDevice)
		{
			CacheTextureColor = cacheTextureColor;
			GraphicsDevice = graphicsDevice;
		}

		public (Texture2D[], double[], uint) GetResults()
		{
			lock (Lock)
			{
				return (Textures, FrameDurations, TextureColor);
			}
		}

		public void SetLogErrors(bool logErrors)
		{
			LogErrors = logErrors;
		}

		/// <summary>
		/// Called when loading has been cancelled.
		/// </summary>
		protected override void Cancel()
		{
			// No action needed.
		}

		/// <summary>
		/// Called when loading should begin.
		/// </summary>
		/// <param name="state">File path of the texture to use for loading.</param>
		protected override async Task DoWork(string state)
		{
			// The state used is the path of the file for the texture.
			var filePath = state;
			Texture2D[] textures = null;
			double[] frameDurations = null;
			uint textureColor = 0;
			try
			{
				// Don't try to load video files. We expect them to fail.
				if (!string.IsNullOrEmpty(filePath) && !IsVideoFile(filePath))
				{
					// Gif handling.
					if (IsGif(filePath))
					{
						using var codec = SKCodec.Create(filePath);

						CancellationTokenSource.Token.ThrowIfCancellationRequested();

						var frameCount = codec.FrameCount;
						textures = new Texture2D[frameCount];
						frameDurations = new double[frameCount];

						// Get frame durations.
						var frameInfo = codec.FrameInfo;
						for (var i = 0; i < frameCount; i++)
							frameDurations[i] = Math.Max(0.0, frameInfo[i].Duration / 1000.0);

						// Decode each frame into raw RGBA data so we can wrap it with a texture.
						var imageInfo = new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Rgba8888);
						var pixels = new byte[imageInfo.RowBytes * imageInfo.Height];
						for (var i = 0; i < frameCount; i++)
						{
							// Decode the image.
							var options = new SKCodecOptions(i);
							unsafe
							{
								fixed (byte* p = pixels)
								{
									codec.GetPixels(imageInfo, (IntPtr)p, options);
								}
							}

							CancellationTokenSource.Token.ThrowIfCancellationRequested();

							// Create a texture for it.
							textures[i] = new Texture2D(GraphicsDevice, imageInfo.Width, imageInfo.Height);
							textures[i].SetData(pixels);

							CancellationTokenSource.Token.ThrowIfCancellationRequested();
						}
					}

					// Normal image handling.
					else
					{
						await using var fileStream = File.OpenRead(filePath);
						CancellationTokenSource.Token.ThrowIfCancellationRequested();
						textures = new Texture2D[1];
						textures[0] = Texture2D.FromStream(GraphicsDevice, fileStream);
						CancellationTokenSource.Token.ThrowIfCancellationRequested();
						frameDurations = new double[1];
						frameDurations[0] = 0.0;
					}
				}
			}
			catch (OperationCanceledException)
			{
				throw;
			}
			catch (Exception e)
			{
				if (LogErrors)
				{
					Logger.Error($"Failed to load texture from \"{filePath}\". {e.Message}");
				}

				textures = null;
				frameDurations = null;
				textureColor = 0;
			}

			CancellationTokenSource.Token.ThrowIfCancellationRequested();

			if (CacheTextureColor && textures != null)
			{
				textureColor = TextureUtils.GetTextureColor(textures[0]);
			}

			// Save results.
			lock (Lock)
			{
				Textures = textures;
				FrameDurations = frameDurations;
				TextureColor = textureColor;
			}
		}
	}

	private readonly ImGuiRenderer ImGuiRenderer;
	private readonly TextureLoadTask LoadTask;

	private string FilePath;
	private Texture2D[] TexturesMonogame;
	private double[] TextureDurations;
	private double TotalDuration;
	private IntPtr[] TexturesImGui;
	private uint TextureColor;
	private bool Bound;
	private bool Disposed;
	private int Frame;

	private bool NewTextureReady;
	private Texture2D[] NewTextures;
	private double[] NewDurations;
	private uint NewTextureColor;

	private static int Id;
	private readonly string ImGuiId;

	private readonly uint Width;
	private readonly uint Height;

	private readonly object TextureSwapLock = new();

	/// <summary>
	/// Constructor.
	/// </summary>
	public EditorTexture(
		GraphicsDevice graphicsDevice,
		ImGuiRenderer imGuiRenderer,
		uint width,
		uint height,
		bool cacheTextureColor)
	{
		ImGuiId = Id++.ToString();
		ImGuiRenderer = imGuiRenderer;
		Width = width;
		Height = height;
		LoadTask = new TextureLoadTask(cacheTextureColor, graphicsDevice);
	}

	/// <summary>
	/// Constructor.
	/// Will begin an asynchronous load of the texture from the image located at the specified path.
	/// </summary>
	public EditorTexture(
		GraphicsDevice graphicsDevice,
		ImGuiRenderer imGuiRenderer,
		uint width,
		uint height,
		string filePath,
		bool cacheTextureColor)
	{
		ImGuiId = Id++.ToString();
		ImGuiRenderer = imGuiRenderer;
		Width = width;
		Height = height;
		NewTextureReady = false;
		NewTextures = null;
		NewDurations = null;
		LoadTask = new TextureLoadTask(cacheTextureColor, graphicsDevice);
		_ = LoadAsync(filePath);
	}

	~EditorTexture()
	{
		Dispose(false);
	}

	public void Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	// ReSharper disable once UnusedParameter.Local
	private void Dispose(bool disposing)
	{
		if (Disposed)
			return;

		lock (TextureSwapLock)
		{
			if (Bound)
			{
				Bound = false;
				foreach (var textureImGui in TexturesImGui)
					ImGuiRenderer.UnbindTexture(textureImGui);
			}

			TexturesMonogame = null;
		}

		Disposed = true;
	}

	/// <summary>
	/// Returns whether or not this texture is bound and ready to render.
	/// </summary>
	public bool IsBound()
	{
		CheckForSwappingToNewTextures();
		return Bound;
	}

	/// <summary>
	/// Gets the average color of the underlying texture.
	/// Will return black if this EditorTexture was configured to not cache the texture color.
	/// </summary>
	/// <returns></returns>
	public uint GetTextureColor()
	{
		return TextureColor;
	}

	/// <summary>
	/// Load the texture from the image located at the specified path.
	/// Will early-out if the given filePath is already loaded unless force is true.
	/// </summary>
	/// <param name="filePath">Path of texture to load.</param>
	/// <param name="force">If true, load the texture even if it was already loaded.</param>
	/// <param name="logErrors">If true, log any errors with loading.</param>
	public async Task LoadAsync(string filePath, bool force = false, bool logErrors = true)
	{
		// Early out.
		if (FilePath == filePath && !force)
			return;
		FilePath = filePath;

		LoadTask.SetLogErrors(logErrors);

		// Start the load. If we are already loading, return. The previous call
		// will use the newly provided file path.
		var taskComplete = await LoadTask.Start(filePath);
		if (!taskComplete)
			return;

		// Get the newly loaded textures and color.
		var (newTextures, newDurations, newTextureColor) = LoadTask.GetResults();

		// We cannot swap textures now because we may be in the middle of submitting
		// instructions to ImGui. If we were unbind the texture during these calls, ImGui
		// would generate an error when at the end of the instructions it goes to draw
		// the unbound image.
		lock (TextureSwapLock)
		{
			NewTextures = newTextures;
			NewDurations = newDurations;
			NewTextureColor = newTextureColor;
			NewTextureReady = true;
		}
	}

	/// <summary>
	/// Returns whether or not the given texture file is a video file.
	/// </summary>
	/// <param name="filePath">Texture file path.</param>
	/// <returns>Whether or not the given texture file is a video file.</returns>
	private static bool IsVideoFile(string filePath)
	{
		if (!Fumen.Path.GetExtensionWithoutSeparator(filePath, out var extension))
			return false;
		return Utils.ExpectedVideoFormats.Contains(extension);
	}

	/// <summary>
	/// Returns whether or not the given texture file is a gif.
	/// </summary>
	/// <param name="filePath">Texture file path.</param>
	/// <returns>Whether or not the given texture file is a gif.</returns>
	private static bool IsGif(string filePath)
	{
		if (!Fumen.Path.GetExtensionWithoutSeparator(filePath, out var extension))
			return false;
		return extension == "gif";
	}

	/// <summary>
	/// Updates time dependent data.
	/// For animated textures like gifs this will update the frame.
	/// </summary>
	/// <param name="currentTime">Total application time in seconds.</param>
	public void Update(double currentTime)
	{
		Frame = 0;
		if (TotalDuration <= 0.0 || TextureDurations == null || TextureDurations.Length == 0)
			return;
		var relativeTime = currentTime - (int)(currentTime / TotalDuration) * TotalDuration;
		while (Frame < TextureDurations.Length)
		{
			if (relativeTime < TextureDurations[Frame])
				break;
			relativeTime -= TextureDurations[Frame];
			Frame++;
		}
	}

	/// <summary>
	/// Unload the currently loaded texture.
	/// Idempotent.
	/// </summary>
	public void UnloadAsync()
	{
		_ = LoadAsync(null);
	}

	/// <summary>
	/// Checks if new textures have finished loading.
	/// If so, swaps to the new textures and releases the previous textures so they
	/// can be garbage collected.
	/// </summary>
	private void CheckForSwappingToNewTextures()
	{
		lock (TextureSwapLock)
		{
			// Check if we need to swap to a newly loaded textures.
			if (!NewTextureReady)
				return;
			NewTextureReady = false;

			// Unbind the previous textures.
			if (Bound)
			{
				Bound = false;
				foreach (var textureImGui in TexturesImGui)
					ImGuiRenderer.UnbindTexture(textureImGui);
			}

			// Update the textures to the newly loaded textures and bind them.
			TexturesMonogame = NewTextures;
			TextureDurations = NewDurations;
			TotalDuration = 0;
			Frame = 0;
			if (TextureDurations != null)
				foreach (var duration in TextureDurations)
					TotalDuration += duration;
			if (TexturesMonogame != null)
				TexturesImGui = new IntPtr[TexturesMonogame.Length];
			else
				TexturesImGui = null;
			if (TexturesMonogame != null)
			{
				for (var i = 0; i < TexturesMonogame.Length; i++)
					TexturesImGui![i] = ImGuiRenderer.BindTexture(TexturesMonogame[i]);
				Bound = true;
			}

			TextureColor = NewTextureColor;
			NewTextures = null;
			NewDurations = null;
		}
	}

	/// <summary>
	/// Draws the texture as an image through ImGui.
	/// </summary>
	/// <param name="mode">TextureLayoutMode for how to lay out this texture.</param>
	/// <returns>True if the texture was successfully drawn and false otherwise.</returns>
	public bool Draw(TextureLayoutMode mode = TextureLayoutMode.Box)
	{
		// Prior to drawing, check if there is a newly loaded texture to swap to.
		CheckForSwappingToNewTextures();

		if (!Bound)
			return false;

		ImGuiUtils.DrawImage(ImGuiId, TexturesImGui[Frame], TexturesMonogame[Frame], Width, Height, mode);
		return true;
	}

	/// <summary>
	/// Draws the texture as a button through ImGui.
	/// </summary>
	/// <param name="mode">TextureLayoutMode for how to lay out this texture.</param>
	/// <returns>
	/// Tuple where the first value represents if the texture was successfully drawn.
	/// The second value represents if the button was pressed.
	/// </returns>
	public (bool, bool) DrawButton(TextureLayoutMode mode = TextureLayoutMode.Box)
	{
		// Prior to drawing, check if there is a newly loaded texture to swap to.
		CheckForSwappingToNewTextures();

		if (!Bound)
			return (false, false);

		return (true, ImGuiUtils.DrawButton(ImGuiId, TexturesImGui[Frame], TexturesMonogame[Frame], Width, Height, mode));
	}

	/// <summary>
	/// Draws the texture through the given SpriteBatch.
	/// </summary>
	/// <param name="spriteBatch">SpriteBatch to use for drawing the texture.</param>
	/// <param name="x">X position to draw the texture.</param>
	/// <param name="y">Y position to draw the texture.</param>
	/// <param name="w">Width of area to draw the texture.</param>
	/// <param name="h">Height of are to draw the texture.</param>
	/// <param name="mode">TextureLayoutMode for how to lay out this texture.</param>
	public void DrawTexture(SpriteBatch spriteBatch, int x, int y, uint w, uint h, TextureLayoutMode mode = TextureLayoutMode.Box)
	{
		// Prior to drawing, check if there is a newly loaded texture to swap to.
		CheckForSwappingToNewTextures();

		if (!Bound)
			return;

		TextureUtils.DrawTexture(spriteBatch, TexturesMonogame[Frame], x, y, w, h, mode);
	}
}

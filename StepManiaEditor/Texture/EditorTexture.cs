using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Fumen;
using Microsoft.Xna.Framework.Graphics;
using MonoGameExtensions;
using static MonoGameExtensions.TextureUtils;

namespace StepManiaEditor;

/// <summary>
/// A MonoGame Texture to render via ImGui.
/// Meant for dynamically loaded textures that are not atlassed and not known until runtime.
/// Provides idempotent asynchronous loading methods to update the Texture with a new file from disk.
/// When loading a new image over a previous image there will be a brief period of time where two textures
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
		/// The last Texture loaded.
		/// </summary>
		private Texture2D Texture;

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

		public (Texture2D, uint) GetResults()
		{
			lock (Lock)
			{
				return (Texture, TextureColor);
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
			Texture2D texture = null;
			uint textureColor = 0;
			try
			{
				// Don't try to load video files. We expect them to fail.
				if (!string.IsNullOrEmpty(filePath) && !IsVideoFile(filePath))
				{
					await using var fileStream = File.OpenRead(filePath);
					texture = Texture2D.FromStream(GraphicsDevice, fileStream);
				}
			}
			catch (Exception e)
			{
				if (LogErrors)
				{
					Logger.Error($"Failed to load texture from \"{filePath}\". {e.Message}");
				}

				texture = null;
			}

			CancellationTokenSource.Token.ThrowIfCancellationRequested();

			if (CacheTextureColor && texture != null)
			{
				textureColor = TextureUtils.GetTextureColor(texture);
			}

			// Save results.
			lock (Lock)
			{
				Texture = texture;
				TextureColor = textureColor;
			}
		}
	}

	private readonly ImGuiRenderer ImGuiRenderer;
	private readonly TextureLoadTask LoadTask;

	private string FilePath;
	private Texture2D TextureMonogame;
	private Texture2D NewTexture;
	private uint TextureColor;
	private uint NewTextureColor;
	private IntPtr TextureImGui;
	private bool Bound;
	private bool NewTextureReady;
	private bool Disposed;

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
		NewTexture = null;
		LoadTask = new TextureLoadTask(cacheTextureColor, graphicsDevice);
		LoadAsync(filePath);
	}

	~EditorTexture()
	{
		Dispose();
	}

	public void Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	private void Dispose(bool disposing)
	{
		if (Disposed)
			return;

		if (disposing)
		{
			lock (TextureSwapLock)
			{
				if (Bound)
				{
					Bound = false;
					ImGuiRenderer.UnbindTexture(TextureImGui);
				}

				TextureMonogame = null;
			}
		}

		Disposed = true;
	}

	/// <summary>
	/// Returns whether or not this texture is bound and ready to render.
	/// </summary>
	public bool IsBound()
	{
		CheckForSwappingToNewTexture();
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
	public async void LoadAsync(string filePath, bool force = false, bool logErrors = true)
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

		// Get the newly loaded texture and color.
		var (newTexture, newTextureColor) = LoadTask.GetResults();

		// We cannot swap textures now because we may be in the middle of submitting
		// instructions to ImGui. If we were unbind the texture during these calls, ImGui
		// would generate an error when at the end of the instructions it goes to draw
		// the unbound image.
		lock (TextureSwapLock)
		{
			NewTexture = newTexture;
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
	/// Unload the currently loaded texture.
	/// Idempotent.
	/// </summary>
	public void UnloadAsync()
	{
		LoadAsync(null);
	}

	/// <summary>
	/// Checks if a new texture as finished loading.
	/// If so, swaps to the new texture and releases the previous texture so it
	/// can be garbage collected.
	/// </summary>
	private void CheckForSwappingToNewTexture()
	{
		lock (TextureSwapLock)
		{
			// Check if we need to swap to a newly loaded texture.
			if (!NewTextureReady)
				return;
			NewTextureReady = false;

			// Unbind the previous texture.
			if (Bound)
			{
				Bound = false;
				ImGuiRenderer.UnbindTexture(TextureImGui);
			}

			// Update the texture to the newly loaded texture and bind it.
			TextureMonogame = NewTexture;
			if (NewTexture != null)
			{
				TextureImGui = ImGuiRenderer.BindTexture(TextureMonogame);
				Bound = true;
			}

			TextureColor = NewTextureColor;
			NewTexture = null;
		}
	}

	/// <summary>
	/// Draws the texture as an image through ImGui.
	/// </summary>
	/// <param name="mode">TextureLayoutMode for how to layout this texture.</param>
	/// <returns>True if the texture was successfully drawn and false otherwise.</returns>
	public bool Draw(TextureLayoutMode mode = TextureLayoutMode.Box)
	{
		// Prior to drawing, check if there is a newly loaded texture to swap to.
		CheckForSwappingToNewTexture();

		if (!Bound)
			return false;

		ImGuiUtils.DrawImage(ImGuiId, TextureImGui, TextureMonogame, Width, Height, mode);
		return true;
	}

	/// <summary>
	/// Draws the texture as a button through ImGui.
	/// </summary>
	/// <param name="mode">TextureLayoutMode for how to layout this texture.</param>
	/// <returns>
	/// Tuple where the first value represents if the texture was successfully drawn.
	/// The second value represents if the button was pressed.
	/// </returns>
	public (bool, bool) DrawButton(TextureLayoutMode mode = TextureLayoutMode.Box)
	{
		// Prior to drawing, check if there is a newly loaded texture to swap to.
		CheckForSwappingToNewTexture();

		if (!Bound)
			return (false, false);

		return (true, ImGuiUtils.DrawButton(ImGuiId, TextureImGui, TextureMonogame, Width, Height, mode));
	}

	/// <summary>
	/// Draws the texture through the given SpriteBatch.
	/// </summary>
	/// <param name="spriteBatch">SpriteBatch to use for drawing the texture.</param>
	/// <param name="x">X position to draw the texture.</param>
	/// <param name="y">Y position to draw the texture.</param>
	/// <param name="w">Width of area to draw the texture.</param>
	/// <param name="h">Height of are to draw the texture.</param>
	/// <param name="mode">TextureLayoutMode for how to layout this texture.</param>
	public void DrawTexture(SpriteBatch spriteBatch, int x, int y, uint w, uint h, TextureLayoutMode mode = TextureLayoutMode.Box)
	{
		// Prior to drawing, check if there is a newly loaded texture to swap to.
		CheckForSwappingToNewTexture();

		if (!Bound)
			return;

		TextureUtils.DrawTexture(spriteBatch, TextureMonogame, x, y, w, h, mode);
	}
}

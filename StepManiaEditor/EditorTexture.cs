using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Fumen;
using Microsoft.Xna.Framework.Graphics;

namespace StepManiaEditor
{
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
	public class EditorTexture : IDisposable
	{
		private readonly ImGuiRenderer ImGuiRenderer;
		private readonly GraphicsDevice GraphicsDevice;

		private CancellationTokenSource LoadCancellationTokenSource;
		private Task LoadTask;

		private string FilePath;
		private bool CacheTextureColor;
		private Texture2D TextureMonogame;
		private Texture2D NewTexture;
		private uint TextureColor;
		private uint NewTextureColor;
		private IntPtr TextureImGui;
		private bool Bound;
		private bool NewTextureReady;
		private bool Disposed;

		private static int id = 0;
		private readonly string ImGuiId;

		private readonly uint Width;
		private readonly uint Height;

		private readonly object TextureSwapLock = new object();

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
			ImGuiId = id++.ToString();
			GraphicsDevice = graphicsDevice;
			ImGuiRenderer = imGuiRenderer;
			Width = width;
			Height = height;
			CacheTextureColor = cacheTextureColor;
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
			ImGuiId = id++.ToString();
			GraphicsDevice = graphicsDevice;
			ImGuiRenderer = imGuiRenderer;
			Width = width;
			Height = height;
			NewTextureReady = false;
			NewTexture = null;
			CacheTextureColor = cacheTextureColor;
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

		protected virtual void Dispose(bool disposing)
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
		/// </summary>
		public async void LoadAsync(string filePath)
		{
			if (FilePath == filePath)
				return;

			FilePath = filePath;

			if (LoadCancellationTokenSource != null)
			{
				if (LoadCancellationTokenSource.IsCancellationRequested)
					return;
				LoadCancellationTokenSource?.Cancel();
				await LoadTask;
			}

			filePath = FilePath;
			Texture2D newTexture = null;
			uint newTextureColor = 0;

			LoadCancellationTokenSource = new CancellationTokenSource();
			LoadTask = Task.Run(() =>
			{
				try
				{
					try
					{
						if (!string.IsNullOrEmpty(filePath))
						{
							using var fileStream = File.OpenRead(filePath);
							newTexture = Texture2D.FromStream(GraphicsDevice, fileStream);
						}
						else
							newTexture = null;
					}
					catch (Exception e)
					{
						Logger.Error($"Failed to load texture from \"{filePath}\". {e}");
						newTexture = null;
					}
					if (CacheTextureColor && newTexture != null)
					{
						newTextureColor = Utils.GetTextureColor(newTexture);
					}
				}
				catch (OperationCanceledException)
				{

				}
				finally
				{
					LoadCancellationTokenSource?.Dispose();
					LoadCancellationTokenSource = null;
				}
			}, LoadCancellationTokenSource.Token);
			await LoadTask;

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
		public bool Draw(Utils.TextureLayoutMode mode = Utils.TextureLayoutMode.Box)
		{
			// Prior to drawing, check if there is a newly loaded texture to swap to.
			CheckForSwappingToNewTexture();

			if (!Bound)
				return false;

			Utils.DrawImage(ImGuiId, TextureImGui, TextureMonogame, Width, Height, mode);
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
		public (bool, bool) DrawButton(Utils.TextureLayoutMode mode = Utils.TextureLayoutMode.Box)
		{
			// Prior to drawing, check if there is a newly loaded texture to swap to.
			CheckForSwappingToNewTexture();

			if (!Bound)
				return (false, false);

			return (true, Utils.DrawButton(ImGuiId, TextureImGui, TextureMonogame, Width, Height, mode));
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
		public void DrawTexture(SpriteBatch spriteBatch, int x, int y, uint w, uint h, Utils.TextureLayoutMode mode = Utils.TextureLayoutMode.Box)
		{
			// Prior to drawing, check if there is a newly loaded texture to swap to.
			CheckForSwappingToNewTexture();

			if (!Bound)
				return;

			Utils.DrawTexture(spriteBatch, TextureMonogame, x, y, w, h, mode);
		}
	}
}

using Microsoft.Xna.Framework.Graphics;

namespace StepManiaEditor;

internal interface IReadOnlyEditorImageData
{
	string Path { get; }
	public EditorTexture GetTexture();
}

/// <summary>
/// Small class to hold a Texture for a song or chart property that
/// represents a file path to an image asset.
/// </summary>
internal sealed class EditorImageData : IReadOnlyEditorImageData
{
	private readonly string FileDirectory;
	private readonly EditorTexture Texture;
	private string PathInternal = "";

	/// <summary>
	/// Path property.
	/// On set, begins an asynchronous load of the image asset specified to the Texture.
	/// </summary>
	public string Path
	{
		get => PathInternal;
		set
		{
			var newValue = value ?? "";
			if (PathInternal == newValue)
				return;

			PathInternal = newValue;
			if (!string.IsNullOrEmpty(PathInternal))
				Texture?.LoadAsync(Fumen.Path.Combine(FileDirectory, PathInternal));
			else
				Texture?.UnloadAsync();
		}
	}

	/// <summary>
	/// Constructor.
	/// When constructed through this method, no Texture will be used.
	/// </summary>
	public EditorImageData(string path)
	{
		Path = path;
	}

	/// <summary>
	/// Constructor.
	/// When constructed through this method, a Texture will be used and loaded asynchronously
	/// whenever the Path changes.
	/// </summary>
	public EditorImageData(
		string fileDirectory,
		GraphicsDevice graphicsDevice,
		ImGuiRenderer imGuiRenderer,
		uint width,
		uint height,
		string path,
		bool cacheTextureColor)
	{
		FileDirectory = fileDirectory;
		Texture = new EditorTexture(graphicsDevice, imGuiRenderer, width, height, cacheTextureColor);
		Path = path;
	}

	public EditorTexture GetTexture()
	{
		return Texture;
	}
}

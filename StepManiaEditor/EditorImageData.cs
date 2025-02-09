using System;
using System.IO;
using Fumen;
using Microsoft.Xna.Framework.Graphics;

namespace StepManiaEditor;

internal interface IReadOnlyEditorImageData
{
	string Path { get; }
	public EditorTexture GetTexture();
}

/// <summary>
/// Small class to hold a Texture for a song or chart property that
/// represents a file path to an image asset. Will automatically reload
/// the texture if it changes on disk.
/// </summary>
internal sealed class EditorImageData : IReadOnlyEditorImageData
{
	private string FileDirectory;
	private readonly EditorTexture Texture;
	private FileSystemWatcher FileWatcher;

	/// <summary>
	/// Path property.
	/// </summary>
	public string Path { get; private set; }

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
		Texture = new EditorTexture(graphicsDevice, imGuiRenderer, width, height, cacheTextureColor);
		UpdatePath(fileDirectory, path);
	}

	public EditorTexture GetTexture()
	{
		return Texture;
	}

	public void Update(double currentTime)
	{
		Texture?.Update(currentTime);
	}

	/// <summary>
	/// Update the path for this image.
	/// Will begin an asynchronous load of the texture if the image has changed.
	/// </summary>
	/// <param name="fileDirectory">The directory of the song file owning this image.</param>
	/// <param name="path">The relative path to the image from the song file.</param>
	public void UpdatePath(string fileDirectory, string path)
	{
		path ??= "";
		fileDirectory ??= "";
		var oldFullPath = Fumen.Path.Combine(FileDirectory, Path);
		var newFullPath = Fumen.Path.Combine(fileDirectory, path);
		var isNewFile = oldFullPath != newFullPath;
		if (isNewFile)
			StopObservingFile();

		FileDirectory = fileDirectory;
		Path = path;

		if (isNewFile)
		{
			// If the path is specified, load the texture and start observing the file.
			if (!string.IsNullOrEmpty(Path))
			{
				if (Texture != null)
				{
					_ = Texture.LoadAsync(Fumen.Path.Combine(FileDirectory, Path));
					StartObservingFile();
				}
			}

			// If the path is empty, unload
			else
			{
				Texture?.UnloadAsync();
			}
		}
	}

	#region File Observation

	private void StartObservingFile()
	{
		try
		{
			var fullPath = Fumen.Path.Combine(FileDirectory, Path);
			// Remove potential relative directory symbols like ".." as FileSystemWatcher
			// throws exceptions when they are present.
			fullPath = System.IO.Path.GetFullPath(fullPath);
			var dir = System.IO.Path.GetDirectoryName(fullPath);
			var file = System.IO.Path.GetFileName(fullPath);
			if (!string.IsNullOrEmpty(dir) && !string.IsNullOrEmpty(file))
			{
				FileWatcher = new FileSystemWatcher(dir);
				FileWatcher.NotifyFilter = NotifyFilters.LastWrite;
				FileWatcher.Changed += OnFileChangedNotification;
				FileWatcher.Filter = file;
				FileWatcher.EnableRaisingEvents = true;
			}
		}
		catch (Exception e)
		{
			Logger.Error($"Failed to observe {Path} for changes: {e}");
		}
	}

	private void OnFileChangedNotification(object sender, FileSystemEventArgs e)
	{
		if (e.ChangeType != WatcherChangeTypes.Changed)
			return;

		// Force a reload of the texture.
		// Do not log errors because Windows issues many notifications on a single save.
		// This results in expected failures to load as external applications have locks
		// on the file during some of the notifications while it is being saved.
		Logger.Info($"Reloading {Path} due to external modification.");
		Texture?.LoadAsync(Fumen.Path.Combine(FileDirectory, Path), true, false);
	}

	private void StopObservingFile()
	{
		FileWatcher = null;
	}

	#endregion File Observation
}

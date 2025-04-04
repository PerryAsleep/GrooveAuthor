using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Fumen;
using Microsoft.Xna.Framework.Graphics;
using static Fumen.Converters.ItgManiaPack;
using static StepManiaEditor.ImGuiUtils;

namespace StepManiaEditor;

/// <summary>
/// EditorPack represents a pack of Songs. This may optionally wrap an ItgManiaPack.
/// Many operations on EditorPack are asynchronous, and the application does not care
/// about waiting on them. For example, the pack needs to refresh from disk if the
/// song changes, creating and deleting the ITGmania involve async disk IO, and saving
/// and loading are async. Refreshing the pack from disk can also occur automatically
/// if the ITGmania pack file is changed externally. It is important that calls to
/// modify the EditorPack are processed in order. In order to avoid pushing that
/// responsibility to the caller, and in order to manage the complexity around automatic
/// updates due to the pack changing on disk, this class internally uses a WorkQueue
/// to run its async work sequentially. For calls where it is important for a caller
/// to respond to the work completing, callbacks can be provided, as with for example
/// SaveItgManiaPack.
/// </summary>
internal sealed class EditorPack : Fumen.IObserver<EditorItgManiaPack>, IDisposable
{
	/// <summary>
	/// The active EditorSong which is responsible for loading this EditorPack.
	/// </summary>
	private EditorSong ActiveSong;

	/// <summary>
	/// EditorImageData for the pack banner.
	/// </summary>
	private readonly EditorImageData Banner;

	/// <summary>
	/// Name of the pack, inferred from the pack directory.
	/// </summary>
	private string PackNameFromDirectory;

	/// <summary>
	/// Pack directory which holds the song folders.
	/// </summary>
	private string PackDirectory;

	/// <summary>
	/// All songs in the pack.
	/// </summary>
	private List<PackSong> Songs = [];

	/// <summary>
	/// Optional ITGmania pack.
	/// </summary>
	private EditorItgManiaPack EditorItgManiaPack;

	/// <summary>
	/// PackLoadTask for loading pad data from disk.
	/// </summary>
	private readonly PackLoadTask PackLoadTask = new();

	/// <summary>
	/// WorkQueue for processing edits sequentially.
	/// </summary>
	private readonly WorkQueue WorkQueue = new();

	/// <summary>
	/// Constructor.
	/// </summary>
	/// <param name="graphicsDevice">GraphicsDevice to use for rendering the banner.</param>
	/// <param name="imGuiRenderer">ImGuiRenderer to use for rendering the banner.</param>
	public EditorPack(GraphicsDevice graphicsDevice, ImGuiRenderer imGuiRenderer)
	{
		Banner = new EditorImageData(null, graphicsDevice, imGuiRenderer, (uint)GetBannerWidth(),
			(uint)GetBannerHeight(), null, false);
	}

	#region Accessors

	public string GetPackName()
	{
		if (EditorItgManiaPack != null && !string.IsNullOrEmpty(EditorItgManiaPack.Title))
			return EditorItgManiaPack.Title;
		return PackNameFromDirectory;
	}

	public string GetPackDirectory()
	{
		return PackDirectory;
	}

	public IReadOnlyList<PackSong> GetSongs()
	{
		return Songs;
	}

	public EditorImageData GetBanner()
	{
		return Banner;
	}

	public EditorItgManiaPack GetItgManiaPack()
	{
		return EditorItgManiaPack;
	}

	#endregion Accessors

	/// <summary>
	/// Set the EditorSong which determines which pack is active. This may cause
	/// the pack to reload from disk if the given EditorSong is in a different
	/// pack from the previous EditorSong.
	/// </summary>
	/// <remarks>
	/// This may not take effect immediately depending on other running work.
	/// </remarks>
	/// <param name="song">Current EditorSong to use for determining the pack.</param>
	public void SetSong(EditorSong song)
	{
		WorkQueue.Enqueue(async () =>
		{
			var dirty = !AreSongsInSamePack(ActiveSong, song);
			ActiveSong = song;
			if (dirty)
			{
				await RefreshInternal();
			}
		});
	}

	/// <summary>
	/// Create a new ITGmania pack file on disk.
	/// </summary>
	/// <remarks>
	/// This may not take effect immediately depending on other running work.
	/// </remarks>
	/// <param name="offset">SyncOffSetType to use for the new pack.</param>
	public void CreateItgManiaPack(SyncOffSetType offset)
	{
		WorkQueue.Enqueue(async Task () =>
		{
			// Clear the old pack if we have one.
			EditorItgManiaPack?.Dispose();
			EditorItgManiaPack?.RemoveObserver(this);
			EditorItgManiaPack = null;

			// Get the pack directory for saving.
			var packDirectory = GetPackDirectory();
			if (string.IsNullOrEmpty(packDirectory))
			{
				Logger.Error("Could not determine pack directory.");
				return;
			}

			// Create a new pack.
			EditorItgManiaPack = await EditorItgManiaPack.CreateNewPack(
				this,
				System.IO.Path.Join(packDirectory, FileName),
				PackNameFromDirectory,
				Banner?.Path ?? "",
				offset);
			EditorItgManiaPack?.AddObserver(this);
		});
	}

	/// <summary>
	/// Delete the ITGmania pack file on disk.
	/// </summary>
	/// <remarks>
	/// This may not take effect immediately depending on other running work.
	/// </remarks>
	public void DeleteItgManiaPack()
	{
		WorkQueue.Enqueue(async () =>
		{
			if (EditorItgManiaPack == null)
				return;

			EditorItgManiaPack?.Delete();
			EditorItgManiaPack?.Dispose();
			EditorItgManiaPack?.RemoveObserver(this);
			EditorItgManiaPack = null;

			// After deleting the Pack.ini file, refresh the pack.
			await RefreshInternal();
		});
	}

	/// <summary>
	/// Saves the underlying ITGmania pack to disk.
	/// </summary>
	/// <remarks>
	/// This may not take effect immediately depending on other running work.
	/// </remarks>
	/// <param name="onlyIfUnsavedChanges">
	/// If true, and there are no unsaved changes, then do not save.
	/// </param>
	/// <param name="callback">
	/// Optional callback to invoke when saving is complete. This callback may not execute on the same
	/// thread that calls this function.
	/// </param>
	public void SaveItgManiaPack(bool onlyIfUnsavedChanges, Action callback = null)
	{
		WorkQueue.Enqueue(async () =>
		{
			if (EditorItgManiaPack == null)
			{
				callback?.Invoke();
				return;
			}

			if (onlyIfUnsavedChanges && !EditorItgManiaPack.HasUnsavedChanges())
			{
				callback?.Invoke();
				return;
			}

			await EditorItgManiaPack.SaveAsync();
			callback?.Invoke();
		});
	}

	/// <summary>
	/// Updates time dependent data.
	/// For packs with animated banners this will update the frame.
	/// This will also pump the internal WorkQueue.
	/// </summary>
	/// <param name="currentTime">Total application time in seconds.</param>
	public void Update(double currentTime)
	{
		WorkQueue.Update();
		Banner?.Update(currentTime);
	}

	/// <summary>
	/// Refresh all songs in the pack from disk.
	/// </summary>
	/// <remarks>
	/// This may not take effect immediately depending on other running work.
	/// </remarks>
	public void Refresh()
	{
		WorkQueue.Enqueue(RefreshInternal);
	}

	/// <summary>
	/// Private implementation of Refresh. Will asynchronously reload assets from disk.
	/// </summary>
	private async Task RefreshInternal()
	{
		// Clear everything related to the current song.
		Songs.Clear();
		PackNameFromDirectory = null;
		PackDirectory = null;
		EditorItgManiaPack?.Dispose();
		EditorItgManiaPack?.RemoveObserver(this);
		EditorItgManiaPack = null;
		Banner.UpdatePath(null, null);
		if (ActiveSong == null || string.IsNullOrEmpty(ActiveSong.GetFileDirectory()))
		{
			return;
		}

		// Get the pack directory so we can reload the song information from the pack.
		DirectoryInfo packDirectoryInfo;
		try
		{
			var songDirInfo = new DirectoryInfo(ActiveSong.GetFileDirectory());
			if (!songDirInfo.Exists)
				return;
			packDirectoryInfo = songDirInfo.Parent;
			if (packDirectoryInfo == null)
				return;
		}
		catch (Exception e)
		{
			Logger.Error($"Failed refreshing pack. {e}");
			return;
		}

		// Asynchronously load the pack.
		var results = await PackLoadTask.Start(new PackLoadState(packDirectoryInfo, Banner));
		if (results == null)
			return;

		// Update our state based on the results.
		PackNameFromDirectory = results.GetPackName();
		PackDirectory = packDirectoryInfo.FullName;
		Songs = results.GetSongs();
		EditorItgManiaPack = EditorItgManiaPack.CreatePackFromLoadedItgManiaPack(
			this,
			System.IO.Path.Join(packDirectoryInfo.FullName, FileName),
			results.GetItgManiaPack());
		EditorItgManiaPack?.AddObserver(this);
	}

	/// <summary>
	/// Returns true if the pack's data can be edited. In practice this data is the
	/// ITGmania pack. Edits to this data should not be performed while other asynchronous
	/// work is occurring.
	/// </summary>
	/// <returns>True if this EditorPack can be edited and false otherwise.</returns>
	public bool CanBeEdited()
	{
		return WorkQueue.IsEmpty();
	}

	/// <summary>
	/// Returns whether the given songs are in the same pack.
	/// </summary>
	/// <param name="songA">First song to compare.</param>
	/// <param name="songB">Second song to compare.</param>
	/// <returns>True if the songs are in the same pack and false otherwise.</returns>
	public static bool AreSongsInSamePack(EditorSong songA, EditorSong songB)
	{
		try
		{
			if (songA == null && songB == null)
				return true;
			if (songA == null || songB == null)
				return false;
			return AreSongsDirectoriesFromSamePack(songA.GetFileDirectory(), songB.GetFileDirectory());
		}
		catch (Exception)
		{
			return false;
		}
	}

	/// <summary>
	/// Returns whether the songs represented by the given files are in the same pack.
	/// </summary>
	/// <param name="songAFile">First song file to compare.</param>
	/// <param name="songBFile">Second song file to compare.</param>
	/// <returns>True if the songs are in the same pack and false otherwise.</returns>
	public static bool AreSongsInSamePack(string songAFile, string songBFile)
	{
		try
		{
			if (string.IsNullOrEmpty(songAFile) && string.IsNullOrEmpty(songBFile))
				return true;
			if (string.IsNullOrEmpty(songAFile) || string.IsNullOrEmpty(songBFile))
				return false;

			var songADirInfo = Directory.GetParent(songAFile);
			if (songADirInfo == null)
				return false;
			var songBDirInfo = Directory.GetParent(songBFile);
			if (songBDirInfo == null)
				return false;

			return AreSongsDirectoriesFromSamePack(songADirInfo.FullName, songBDirInfo.FullName);
		}
		catch (Exception)
		{
			return false;
		}
	}

	/// <summary>
	/// Returns whether the songs represented by the given directories are in the same pack.
	/// </summary>
	/// <param name="songADir">First song file directory to compare.</param>
	/// <param name="songBDir">Second song file directory to compare.</param>
	/// <returns>True if the songs are in the same pack and false otherwise.</returns>
	private static bool AreSongsDirectoriesFromSamePack(string songADir, string songBDir)
	{
		try
		{
			var songADirInfo = new DirectoryInfo(songADir);
			if (!songADirInfo.Exists)
				return false;
			var songAPackDir = songADirInfo.Parent;
			if (songAPackDir == null || !songAPackDir.Exists)
				return false;
			if (string.IsNullOrEmpty(songAPackDir.FullName))
				return false;

			var songBDirInfo = new DirectoryInfo(songBDir);
			if (!songBDirInfo.Exists)
				return false;
			var songBPackDir = songBDirInfo.Parent;
			if (songBPackDir == null || !songBPackDir.Exists)
				return false;
			if (string.IsNullOrEmpty(songBPackDir.FullName))
				return false;
			return songAPackDir.FullName.Equals(songBPackDir.FullName);
		}
		catch (Exception)
		{
			return false;
		}
	}

	/// <summary>
	/// Returns whether this pack has unsaved changes.
	/// </summary>
	/// <returns>True if this pack has unsaved changes and false otherwise.</returns>
	public bool HasUnsavedChanges()
	{
		return EditorItgManiaPack?.HasUnsavedChanges() ?? false;
	}

	#region IObserver

	public void OnNotify(string eventId, EditorItgManiaPack notifier, object payload)
	{
		switch (eventId)
		{
			case EditorItgManiaPack.NotificationBannerChanged:
			{
				// If an explicit banner is set, use that.
				if (!string.IsNullOrEmpty(EditorItgManiaPack.Banner))
				{
					Banner.UpdatePath(EditorItgManiaPack.GetPackDirectory(), EditorItgManiaPack.Banner);
				}
				// If no explicit banner is set, fall back to Stepmania logic.
				else
				{
					var (dir, name) = PackLoadTask.GetBannerFromDirectory(PackDirectory);
					Banner.UpdatePath(dir, name);
				}

				break;
			}
		}
	}

	#endregion IObserver

	#region IDisposable

	public void Dispose()
	{
		EditorItgManiaPack?.Dispose();
		EditorItgManiaPack?.RemoveObserver(this);
		EditorItgManiaPack = null;
		Banner?.Dispose();
	}

	#endregion IDisposable
}

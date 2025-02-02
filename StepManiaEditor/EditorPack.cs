using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Fumen;
using Microsoft.Xna.Framework.Graphics;
using static StepManiaEditor.ImGuiUtils;

namespace StepManiaEditor;

/// <summary>
/// EditorPack represents a pack of Songs.
/// </summary>
internal sealed class EditorPack
{
	private EditorSong ActiveSong;
	private DirectoryInfo PackDirectoryInfo;
	private readonly EditorImageData Banner = new(null);
	private string PackName;

	private List<PackSong> Songs = new();
	private readonly PackLoadTask PackLoadTask = new();

	public EditorPack(GraphicsDevice graphicsDevice, ImGuiRenderer imGuiRenderer)
	{
		Banner = new EditorImageData(null, graphicsDevice, imGuiRenderer, (uint)GetBannerWidth(),
			(uint)GetBannerHeight(), null, false);
	}

	public void SetSong(EditorSong song)
	{
		var dirty = !AreSongsInSamePack(ActiveSong, song);
		ActiveSong = song;
		if (dirty)
			_ = Refresh();
	}

	public string GetPackName()
	{
		return PackName;
	}

	public IReadOnlyList<PackSong> GetSongs()
	{
		return Songs;
	}

	public EditorImageData GetBanner()
	{
		return Banner;
	}

	/// <summary>
	/// Updates time dependent data.
	/// For packs with animated banners this will update the frame.
	/// </summary>
	/// <param name="currentTime">Total application time in seconds.</param>
	public void Update(double currentTime)
	{
		Banner?.Update(currentTime);
	}

	private static bool AreSongsInSamePack(EditorSong songA, EditorSong songB)
	{
		try
		{
			if (songA == null && songB == null)
				return true;
			if (songA == null || songB == null)
				return false;

			var songADirInfo = new DirectoryInfo(songA.GetFileDirectory());
			if (!songADirInfo.Exists)
				return false;
			var songAPackDir = songADirInfo.Parent;
			if (songAPackDir == null || !songAPackDir.Exists)
				return false;
			if (string.IsNullOrEmpty(songAPackDir.FullName))
				return false;

			var songBDirInfo = new DirectoryInfo(songB.GetFileDirectory());
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
	/// Refresh all songs in the pack from disk.
	/// </summary>
	/// <returns></returns>
	public async Task Refresh()
	{
		Songs.Clear();
		PackName = null;
		PackDirectoryInfo = null;
		Banner.UpdatePath(null, null);
		if (ActiveSong == null)
			return;

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

		var taskComplete = await PackLoadTask.Start(new PackLoadState(packDirectoryInfo, Banner));
		if (!taskComplete)
			return;
		if (ActiveSong != null)
		{
			(PackName, Songs) = PackLoadTask.GetResults();
		}
	}
}

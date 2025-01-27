using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Fumen;

namespace StepManiaEditor;

/// <summary>
/// State used for performing async loads of pack files.
/// </summary>
internal sealed class PackLoadState
{
	private readonly DirectoryInfo PackDirectoryInfo;
	private readonly EditorImageData PackBannerImage;

	public PackLoadState(DirectoryInfo packDirectoryInfo, EditorImageData packBannerImage)
	{
		PackDirectoryInfo = packDirectoryInfo;
		PackBannerImage = packBannerImage;
	}

	public DirectoryInfo GetPackDirectoryInfo()
	{
		return PackDirectoryInfo;
	}

	public EditorImageData GetPackBannerImage()
	{
		return PackBannerImage;
	}
}

/// <summary>
/// CancellableTask for performing async loads of pack files.
/// </summary>
internal sealed class PackLoadTask : CancellableTask<PackLoadState>
{
	private readonly object Lock = new();

	private List<PackSong> Songs;
	private string PackName;

	protected override async Task DoWork(PackLoadState state)
	{
		var songs = new List<PackSong>();
		var packDirectoryInfo = state.GetPackDirectoryInfo();
		var packName = packDirectoryInfo.Name;
		var dirs = packDirectoryInfo.EnumerateDirectories();
		var token = CancellationTokenSource.Token;
		token.ThrowIfCancellationRequested();
		foreach (var songDirectory in dirs)
		{
			var files = songDirectory.GetFiles();
			FileInfo smFile = null;
			FileInfo sscFile = null;
			foreach (var file in files)
			{
				if (file.Extension == ".ssc")
					sscFile = file;
				else if (file.Extension == ".sm")
					smFile = file;
			}

			if (sscFile != null || smFile != null)
			{
				songs.Add(new PackSong(songDirectory, sscFile, smFile));
			}
		}

		token.ThrowIfCancellationRequested();

		if (songs.Count > 0)
		{
			var tasks = new Task<bool>[songs.Count];
			for (var i = 0; i < songs.Count; i++)
			{
				tasks[i] = songs[i].LoadAsync(CancellationTokenSource.Token);
			}

			await Task.WhenAll(tasks);
			token.ThrowIfCancellationRequested();
			for (var i = songs.Count - 1; i >= 0; i--)
				if (!tasks[i].Result)
					songs.RemoveAt(i);
			songs.Sort(new PackSongComparer());
		}

		LoadPackBanner(state);

		// Save results
		lock (Lock)
		{
			Songs = songs;
			PackName = packName;
		}
	}

	private void LoadPackBanner(PackLoadState state)
	{
		// This order matches Stepmania. See SongManager::AddGroup.
		List<string> preferredExtensions =
		[
			".png",
			".jpg",
			".jpeg",
			".gif",
			".bmp",
		];

		FileInfo bannerFileInfo = null;
		var packDirectoryInfo = state.GetPackDirectoryInfo();
		var files = packDirectoryInfo.GetFiles();
		foreach (var extension in preferredExtensions)
		{
			foreach (var file in files)
			{
				if (file.Extension == extension)
				{
					bannerFileInfo = file;
					break;
				}
			}

			if (bannerFileInfo != null)
				break;
		}

		if (bannerFileInfo == null)
			return;

		state.GetPackBannerImage().UpdatePath(bannerFileInfo.DirectoryName, bannerFileInfo.Name);
	}

	/// <summary>
	/// Called when loading has been cancelled.
	/// </summary>
	protected override void Cancel()
	{
		ClearResults();
	}

	public (string, List<PackSong>) GetResults()
	{
		lock (Lock)
		{
			return (PackName, Songs);
		}
	}

	public void ClearResults()
	{
		lock (Lock)
		{
			PackName = null;
			Songs.Clear();
		}
	}
}

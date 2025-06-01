using System.Collections.Generic;
using System.IO;
using System.Text;
using static StepManiaEditor.EditorSongImageUtils;

namespace StepManiaEditor;

/// <summary>
/// Action to set multiple assets on an EditorSong.
/// </summary>
internal sealed class ActionSetSongAssets : EditorAction
{
	private readonly EditorSong Song;
	private readonly bool IfUnset;

	private class Asset
	{
		public readonly string PrettyName;
		public readonly string PreviousPath;
		public readonly string NewPath;
		public readonly string SongPropertyName;

		public Asset(string prettyName, string previousPath, string newPath, string songPropertyName)
		{
			PrettyName = prettyName;
			PreviousPath = previousPath;
			NewPath = newPath;
			SongPropertyName = songPropertyName;
		}
	}

	private readonly List<Asset> AssetUpdates;

	public ActionSetSongAssets(EditorSong song, bool ifUnset) : base(false, false)
	{
		Song = song;
		IfUnset = ifUnset;

		var directory = Song.GetFileDirectory();
		var files = Directory.GetFiles(Song.GetFileDirectory());
		var imagePaths = TryFindBestImages(directory, files);
		var lyricsPath = TryFindBestLyrics(directory, files);

		AssetUpdates = new List<Asset>();

		void AddAssetUpdate(string prettyName, string propertyName, string newPath)
		{
			if (string.IsNullOrEmpty(newPath))
				return;
			var currentPath = Utils.GetValueFromFieldOrProperty<string>(Song, propertyName);
			if (IfUnset && !string.IsNullOrEmpty(currentPath))
				return;
			if (newPath == currentPath)
				return;
			AssetUpdates.Add(new Asset(prettyName, currentPath, newPath, propertyName));
		}

		void AddImageAssetUpdate(string prettyName, string propertyName, SongImageType imageType)
		{
			if (!imagePaths.TryGetValue(imageType, out var newPath))
				return;
			AddAssetUpdate(prettyName, propertyName, newPath);
		}

		AddImageAssetUpdate("Background", nameof(EditorSong.BackgroundPath), SongImageType.Background);
		AddImageAssetUpdate("Banner", nameof(EditorSong.BannerPath), SongImageType.Banner);
		AddImageAssetUpdate("CD Title", nameof(EditorSong.CDTitlePath), SongImageType.CDTitle);
		AddImageAssetUpdate("Jacket", nameof(EditorSong.JacketPath), SongImageType.Jacket);
		AddImageAssetUpdate("CD Image", nameof(EditorSong.CDImagePath), SongImageType.CDImage);
		AddImageAssetUpdate("Disc Image", nameof(EditorSong.DiscImagePath), SongImageType.DiscImage);
		AddAssetUpdate("Lyrics", nameof(EditorSong.LyricsPath), lyricsPath);
	}

	public bool WillHaveAnEffect()
	{
		return AssetUpdates.Count > 0;
	}

	public override string ToString()
	{
		var sb = new StringBuilder();
		sb.Append("Apply all ");
		if (IfUnset)
			sb.Append("unset ");
		sb.Append("assets (");

		if (!WillHaveAnEffect())
		{
			sb.Append("none changed");
		}
		else
		{
			var first = true;
			foreach (var update in AssetUpdates)
			{
				if (!first)
					sb.Append(", ");
				sb.Append($"{update.PrettyName}: {update.NewPath}");
				first = false;
			}
		}

		sb.Append(')');
		return sb.ToString();
	}

	public override bool AffectsFile()
	{
		return true;
	}

	protected override void DoImplementation()
	{
		foreach (var update in AssetUpdates)
			Utils.SetFieldOrPropertyToValue(Song, update.SongPropertyName, update.NewPath);
	}

	protected override void UndoImplementation()
	{
		foreach (var update in AssetUpdates)
			Utils.SetFieldOrPropertyToValue(Song, update.SongPropertyName, update.PreviousPath);
	}
}

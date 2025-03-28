using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Fumen;
using Fumen.ChartDefinition;
using Fumen.Converters;

namespace StepManiaEditor;

/// <summary>
/// PackSong represents a Song within an EditorPack.
/// These Songs do not have fully loaded Charts.
/// </summary>
internal sealed class PackSong
{
	private Song Song;
	private string Ratings;
	private readonly DirectoryInfo DirectoryInfo;
	private readonly FileInfo SscFile;
	private readonly FileInfo SmFile;

	public PackSong(DirectoryInfo directoryInfo, FileInfo sscFile, FileInfo smFile)
	{
		DirectoryInfo = directoryInfo;
		SscFile = sscFile;
		SmFile = smFile;
	}

	public Song GetSong()
	{
		return Song;
	}

	public string GetRatingsString()
	{
		return Ratings;
	}

	public string GetDirectoryName()
	{
		return DirectoryInfo.Name;
	}

	public FileInfo GetFileInfo()
	{
		return SscFile ?? SmFile;
	}

	/// <summary>
	/// Asynchronously load the Song.
	/// </summary>
	/// <param name="token">CancellationToken</param>
	/// <returns>True if the song was loaded successfully and false otherwise.</returns>
	public async Task<bool> LoadAsync(CancellationToken token)
	{
		token.ThrowIfCancellationRequested();

		var fileInfo = GetFileInfo();
		if (fileInfo == null)
			return false;
		var fileName = fileInfo.FullName;
		var reader = Reader.CreateReader(fileName);
		if (reader == null)
		{
			Logger.Error($"Unsupported file format. Cannot parse {fileName}");
			return false;
		}

		token.ThrowIfCancellationRequested();
		Song = await reader.LoadMetaDataAsync(token);
		CacheRatings();
		return Song != null;
	}

	/// <summary>
	/// Cache string representation of the Song's Chart ratings.
	/// </summary>
	private void CacheRatings()
	{
		if (Song == null)
			return;

		var charts = new List<Chart>(Song.Charts);
		charts.Sort(new PackChartComparer());
		string lastType = null;
		var sb = new StringBuilder();
		foreach (var chart in charts)
		{
			var firstChartOfType = false;
			if (chart.Type != lastType)
			{
				if (lastType != null)
				{
					sb.Append('|');
				}

				firstChartOfType = true;
				lastType = chart.Type;
			}

			if (!firstChartOfType)
				sb.Append(',');
			sb.Append((int)chart.DifficultyRating);
		}

		Ratings = sb.ToString();
	}
}

/// <summary>
/// Custom Comparer for Charts within a PackSong.
/// </summary>
internal sealed class PackChartComparer : IComparer<Chart>
{
	private static readonly Dictionary<string, int> ChartTypeOrder = new()
	{
		{ "dance-single", 0 },
		{ "dance-double", 1 },
		{ "dance-couple", 2 },
		{ "dance-routine", 3 },
		{ "dance-solo", 4 },
		{ "dance-threepanel", 5 },

		{ "pump-single", 6 },
		{ "pump-halfdouble", 7 },
		{ "pump-double", 8 },
		{ "pump-couple", 9 },
		{ "pump-routine", 10 },

		{ "smx-beginner", 11 },
		{ "smx-single", 12 },
		{ "smx-dual", 13 },
		{ "smx-full", 14 },
		{ "smx-team", 15 },
	};

	private static readonly Dictionary<string, int> DifficultyTypeOrder = new()
	{
		{ "Beginner", 0 },
		{ "Easy", 1 },
		{ "Medium", 2 },
		{ "Hard", 3 },
		{ "Challenge", 4 },
		{ "Edit", 5 },
	};

	public static int Compare(Chart c1, Chart c2)
	{
		if (null == c1 && null == c2)
			return 0;
		if (null == c1)
			return 1;
		if (null == c2)
			return -1;

		// Compare by Type.
		int comparison;
		var c1HasChartTypeOrder = ChartTypeOrder.TryGetValue(c1.Type, out var c1Order);
		var c2HasChartTypeOrder = ChartTypeOrder.TryGetValue(c2.Type, out var c2Order);
		if (c1HasChartTypeOrder != c2HasChartTypeOrder)
		{
			return c1HasChartTypeOrder ? -1 : 1;
		}

		if (c1HasChartTypeOrder)
		{
			comparison = c1Order - c2Order;
			if (comparison != 0)
				return comparison;
		}

		// Compare by DifficultyType.
		var c1HasDifficultyTypeOrder = DifficultyTypeOrder.TryGetValue(c1.Type, out c1Order);
		var c2HasDifficultyTypeOrder = DifficultyTypeOrder.TryGetValue(c2.Type, out c2Order);
		if (c1HasDifficultyTypeOrder != c2HasDifficultyTypeOrder)
		{
			return c1HasDifficultyTypeOrder ? -1 : 1;
		}

		if (c1HasDifficultyTypeOrder)
		{
			comparison = c1Order - c2Order;
			if (comparison != 0)
				return comparison;
		}

		var ratingComparison = c1.DifficultyRating - c2.DifficultyRating;
		if (ratingComparison != 0.0)
			return ratingComparison > 0 ? 1 : -1;
		return 0;
	}

	int IComparer<Chart>.Compare(Chart c1, Chart c2)
	{
		return Compare(c1, c2);
	}
}

/// <summary>
/// Comparer for sorting PackSongs.
/// </summary>
internal sealed class PackSongComparer : IComparer<PackSong>
{
	int IComparer<PackSong>.Compare(PackSong p1, PackSong p2)
	{
		if (p1 == null && p2 == null)
			return 0;
		if (p1 == null)
			return -1;
		if (p2 == null)
			return 1;

		var song1 = p1.GetSong();
		var song2 = p2.GetSong();
		int comparison;

		// Sort by title, preferring transliterated titles.
		if (song1 != null && song2 != null)
		{
			var p1Title = song1.TitleTransliteration;
			if (string.IsNullOrEmpty(p1Title))
				p1Title = song1.Title ?? "";

			var p2Title = song2.TitleTransliteration;
			if (string.IsNullOrEmpty(p2Title))
				p2Title = song2.Title ?? "";
			comparison = string.Compare(p1Title, p2Title, StringComparison.CurrentCulture);
			if (comparison != 0)
				return comparison;
		}
		else if (song1 == null)
		{
			return 1;
		}
		else
		{
			return -1;
		}

		// Sort by folder name.
		comparison = string.Compare(p1.GetDirectoryName(), p2.GetDirectoryName(), StringComparison.CurrentCulture);
		return comparison;
	}
}

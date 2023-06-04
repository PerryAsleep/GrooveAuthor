using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static StepManiaEditor.Utils;
using static StepManiaEditor.ImGuiUtils;

namespace StepManiaEditor;

/// <summary>
/// Utility functions for EditorSong images like the background, banner, etc.
/// </summary>
internal sealed class EditorSongImageUtils
{
	private const float AspectRatioMatchTolerance = 0.05f;
	private const string BannerPostfix = "bn";
	private const string BannerContains = "banner";
	private const string BackgroundPostfix = "bg";
	private const string BackgroundContains = "background";

	public enum SongImageType
	{
		Background,
		Banner,
		Jacket,
		CDImage,
		DiscImage,
		CDTitle,
	}

	/// <summary>
	/// Given a path to a directory, finds the best banner image file in the directory or null if no banner image file could be found.
	/// </summary>
	/// <param name="directory">Full path to the directory.</param>
	/// <returns>Full path to the best banner image or null if none could be found.</returns>
	public static string TryFindBestBanner(string directory)
	{
		return TryFindBestAsset(
			directory,
			GetUnscaledBannerWidth(),
			GetUnscaledBannerHeight(),
			BannerPostfix,
			BannerContains,
			false,
			false,
			false);
	}

	/// <summary>
	/// Given a path to a directory, finds the best background image file in the directory or null if no banner image file could be found.
	/// </summary>
	/// <param name="directory">Full path to the directory.</param>
	/// <returns>Full path to the best background image or null if none could be found.</returns>
	public static string TryFindBestBackground(string directory)
	{
		return TryFindBestAsset(
			directory,
			GetUnscaledBackgroundWidth(),
			GetUnscaledBackgroundHeight(),
			BackgroundPostfix,
			BackgroundContains,
			true,
			true,
			true);
	}

	private static string TryFindBestAsset(
		string directory,
		int w,
		int h,
		string endsWith,
		string contains,
		bool includeVideoFiles,
		bool preferBiggestFile,
		bool preferAnyVideo)
	{
		try
		{
			var files = Directory.GetFiles(directory);

			// Check for files which match StepMania's expected names.
			var assetFiles = new List<string>();
			var videoFiles = new List<string>();
			foreach (var file in files)
			{
				try
				{
					var (fileNameNoExtension, extension) = GetFileNameAndExtension(file);
					var imageFile = ExpectedImageFormats.Contains(extension);
					var videoFile = ExpectedVideoFormats.Contains(extension);

					if (!includeVideoFiles && !imageFile)
						continue;
					if (includeVideoFiles && !imageFile && !videoFile)
						continue;

					assetFiles.Add(file);
					if (videoFile)
						videoFiles.Add(file);
					if (fileNameNoExtension.EndsWith(endsWith) || fileNameNoExtension.Contains(contains))
					{
						return file;
					}
				}
				catch (Exception)
				{
					// Ignored.
				}
			}

			// Try to match expected dimensions.
			var filesWithExpectedAspectRatio = new List<string>();
			string biggestFile = null;
			var biggestSize = 0;
			foreach (var file in assetFiles)
			{
				try
				{
					var (sourceWidth, sourceHeight) = GetImageDimensions(file);
					if (DoesAspectRatioMatch(sourceWidth, sourceHeight, w, h))
						filesWithExpectedAspectRatio.Add(file);

					var size = sourceWidth * sourceHeight;
					if (size > biggestSize && sourceWidth >= w && sourceHeight >= h)
					{
						biggestSize = size;
						biggestFile = file;
					}

					if (sourceWidth == w && sourceHeight == h)
					{
						return file;
					}
				}
				catch (Exception)
				{
					// Ignored.
				}
			}

			// Try to match expected aspect ratio.
			if (filesWithExpectedAspectRatio.Count > 0)
				return filesWithExpectedAspectRatio[0];

			// If configured to prefer the biggest file and a file exists that is at
			// least as expected dimensions, use that.
			if (preferBiggestFile && !string.IsNullOrEmpty(biggestFile))
				return biggestFile;

			// If configured to prefer video files and any video file exists, use that.
			if (preferAnyVideo && videoFiles.Count > 0)
				return videoFiles[0];
		}
		catch (Exception)
		{
			// Ignored.
		}

		return null;
	}

	private static (string, string) GetFileNameAndExtension(string filePath)
	{
		var fileNameNoExtension = Path.GetFileNameWithoutExtension(filePath).ToLower();
		var extension = Path.GetExtension(filePath);
		if (extension.StartsWith('.'))
			extension = extension.Substring(1);
		return (fileNameNoExtension, extension);
	}

	private static (int, int) GetImageDimensions(string filePath)
	{
		var w = 1;
		var h = 1;
		try
		{
			using Stream stream = File.OpenRead(filePath);
			using var sourceImage = System.Drawing.Image.FromStream(stream, false, false);
			w = sourceImage.Width;
			h = sourceImage.Height;
		}
		catch (Exception)
		{
			// Ignored.
		}

		return (w, h);
	}

	private static bool DoesAspectRatioMatch(int sW, int sH, int eW, int eH)
	{
		var sourceAspectRatio = (float)sW / sH;
		var expectedAspectRation = (float)eW / eH;
		return Math.Abs(sourceAspectRatio - expectedAspectRation) < AspectRatioMatchTolerance;
	}

	/// <summary>
	/// Given a path to an image file, returns which SongImageType is best suited for this image.
	/// </summary>
	/// <param name="imagePath">Full path to the image file.</param>
	/// <returns>Best SongImageType for this image.</returns>
	public static SongImageType GetBestSongImageType(string imagePath)
	{
		try
		{
			var (fileNameNoExtension, _) = GetFileNameAndExtension(imagePath);

			// If the file name or file dimensions match expectations for a banner image, assume it is a banner.
			if (fileNameNoExtension.EndsWith(BannerPostfix) || fileNameNoExtension.Contains(BannerContains))
				return SongImageType.Banner;
			var (sourceWidth, sourceHeight) = GetImageDimensions(imagePath);
			if (DoesAspectRatioMatch(sourceWidth, sourceHeight, GetUnscaledBannerWidth(), GetUnscaledBannerHeight()))
				return SongImageType.Banner;

			// The other image types are all square and not common.
		}
		catch (Exception)
		{
			// Ignored.
		}

		// The best image by default is a background.
		return SongImageType.Background;
	}
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SkiaSharp;
using static StepManiaEditor.Utils;
using static StepManiaEditor.ImGuiUtils;

namespace StepManiaEditor;

/// <summary>
/// Utility functions for EditorSong images like the background, banner, etc.
/// </summary>
internal sealed class EditorSongImageUtils
{
	private const float AspectRatioMatchTolerance = 0.05f;

	/// <summary>
	/// Image types.
	/// The order is used for matching files.
	/// </summary>
	public enum SongImageType
	{
		Background,
		Banner,
		CDTitle,
		Jacket,
		CDImage,
		DiscImage,
	}

	/// <summary>
	/// Data per each SongImageType for performing searches.
	/// </summary>
	private class ImageSearchData
	{
		private readonly List<string> Prefixes;
		private readonly List<string> Contains;
		private readonly List<string> Postfixes;
		private readonly bool ShouldMatchAspectRatio;
		private readonly int ExpectedWidth;
		private readonly int ExpectedHeight;
		private readonly bool IncludeVideoFiles;
		private readonly bool PreferBiggestFile;
		private readonly bool PreferAnyVideo;

		public ImageSearchData(
			List<string> prefixes = null,
			List<string> contains = null,
			List<string> postfixes = null,
			bool shouldMatchAspectRatio = false,
			int expectedWidth = 0,
			int expectedHeight = 0,
			bool includeVideoFiles = false,
			bool preferBiggestFile = false,
			bool preferAnyVideo = false)
		{
			Prefixes = prefixes;
			Contains = contains;
			Postfixes = postfixes;
			ShouldMatchAspectRatio = shouldMatchAspectRatio;
			ExpectedWidth = expectedWidth;
			ExpectedHeight = expectedHeight;
			IncludeVideoFiles = includeVideoFiles;
			PreferBiggestFile = preferBiggestFile;
			PreferAnyVideo = preferAnyVideo;
		}

		public string TryFindBestAsset(string directory)
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

						if (!IncludeVideoFiles && !imageFile)
							continue;
						if (IncludeVideoFiles && !imageFile && !videoFile)
							continue;

						assetFiles.Add(file);
						if (videoFile)
							videoFiles.Add(file);

						if (MatchesName(fileNameNoExtension))
							return file;
					}
					catch (Exception)
					{
						// Ignored.
					}
				}

				// Try to match expected dimensions.
				if (ShouldMatchAspectRatio || PreferBiggestFile)
				{
					var filesWithExpectedAspectRatio = new List<string>();
					string biggestFile = null;
					var biggestSize = 0;
					foreach (var file in assetFiles)
					{
						try
						{
							var (sourceWidth, sourceHeight) = GetImageDimensions(file);
							if (DoesAspectRatioMatch(sourceWidth, sourceHeight, ExpectedWidth, ExpectedHeight))
								filesWithExpectedAspectRatio.Add(file);

							var size = sourceWidth * sourceHeight;
							if (size > biggestSize && sourceWidth >= ExpectedWidth && sourceHeight >= ExpectedHeight)
							{
								biggestSize = size;
								biggestFile = file;
							}

							if (sourceWidth == ExpectedWidth && sourceHeight == ExpectedHeight)
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
					if (ShouldMatchAspectRatio && filesWithExpectedAspectRatio.Count > 0)
						return filesWithExpectedAspectRatio[0];

					// If configured to prefer the biggest file and a file exists that is at
					// least as expected dimensions, use that.
					if (PreferBiggestFile && !string.IsNullOrEmpty(biggestFile))
						return biggestFile;
				}

				// If configured to prefer video files and any video file exists, use that.
				if (PreferAnyVideo && videoFiles.Count > 0)
					return videoFiles[0];
			}
			catch (Exception)
			{
				// Ignored.
			}

			return null;
		}

		public bool Matches(string fileName, int w, int h)
		{
			if (MatchesName(fileName))
				return true;
			if (ShouldMatchAspectRatio && DoesAspectRatioMatch(w, h, ExpectedWidth, ExpectedHeight))
				return true;
			return false;
		}

		private bool MatchesName(string fileName)
		{
			if (Prefixes != null)
			{
				foreach (var prefix in Prefixes)
				{
					if (fileName.StartsWith(prefix))
					{
						return true;
					}
				}
			}

			if (Contains != null)
			{
				foreach (var contains in Contains)
				{
					if (fileName.Contains(contains))
					{
						return true;
					}
				}
			}

			if (Postfixes != null)
			{
				foreach (var postfix in Postfixes)
				{
					if (fileName.EndsWith(postfix))
					{
						return true;
					}
				}
			}

			return false;
		}

		private static bool DoesAspectRatioMatch(int sW, int sH, int eW, int eH)
		{
			if (sW == 0 || sH == 0 || eW == 0 || eH == 0)
				return false;
			var sourceAspectRatio = (float)sW / sH;
			var expectedAspectRation = (float)eW / eH;
			return Math.Abs(sourceAspectRatio - expectedAspectRation) < AspectRatioMatchTolerance;
		}
	}

	private static readonly Dictionary<SongImageType, ImageSearchData> ImageData = new();

	static EditorSongImageUtils()
	{
		ImageData[SongImageType.Background] = new ImageSearchData(null, ["background"], ["bg"], true, BackgroundWidthDefaultDPI,
			BackgroundHeightDefaultDPI, true, true, true);
		ImageData[SongImageType.Banner] =
			new ImageSearchData(null, ["banner"], [" bn"], true, BannerWidthDefaultDPI, BannerHeightDefaultDPI);
		ImageData[SongImageType.CDTitle] = new ImageSearchData(null, ["cdtitle"]);
		ImageData[SongImageType.Jacket] = new ImageSearchData(["jk_"], ["jacket", "albumart"]);
		ImageData[SongImageType.CDImage] = new ImageSearchData(null, null, ["-cd"]);
		ImageData[SongImageType.DiscImage] = new ImageSearchData(null, null, [" disc", " title"]);
	}

	#region Private Helpers

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
		try
		{
			using var codec = SKCodec.Create(filePath);
			return (codec.Info.Width, codec.Info.Height);
		}
		catch (Exception)
		{
			return (1, 1);
		}
	}

	#endregion Private Helpers

	#region Public Search Methods

	/// <summary>
	/// Given a path to a directory, finds the best image file of the given type in the directory or null if no image file could be found.
	/// </summary>
	/// <param name="directory">Full path to the directory.</param>
	/// <param name="imageType">SongImageType to search for.</param>
	/// <returns>Full path to the best image or null if none could be found.</returns>
	public static string TryFindBestImage(SongImageType imageType, string directory)
	{
		return ImageData[imageType].TryFindBestAsset(directory);
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
			var (w, h) = GetImageDimensions(imagePath);
			foreach (var songType in Enum.GetValues(typeof(SongImageType)).Cast<SongImageType>())
			{
				if (ImageData[songType].Matches(fileNameNoExtension, w, h))
					return songType;
			}
		}
		catch (Exception)
		{
			// Ignored.
		}

		// The best image by default is a background.
		return SongImageType.Background;
	}

	#endregion Public Search Methods
}

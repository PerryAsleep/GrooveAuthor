using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Fumen;
using ImGuiNET;
using Microsoft.Xna.Framework.Graphics;
using Path = Fumen.Path;

namespace StepManiaEditor
{
	/// <summary>
	/// Class for drawing Song properties UI.
	/// </summary>
	public class UISongProperties
	{
		private static EditorSong EditorSong;

		private readonly Editor Editor;
		private static EmptyTexture EmptyTextureBanner;
		private static EmptyTexture EmptyTextureCDTitle;

		public UISongProperties(Editor editor, GraphicsDevice graphicsDevice, ImGuiRenderer imGuiRenderer)
		{
			EmptyTextureBanner = new EmptyTexture(graphicsDevice, imGuiRenderer, Utils.BannerWidth, Utils.BannerHeight);
			EmptyTextureCDTitle = new EmptyTexture(graphicsDevice, imGuiRenderer, Utils.CDTitleWidth, Utils.CDTitleHeight);
			Editor = editor;
		}

		public void Draw(EditorSong editorSong)
		{
			EditorSong = editorSong;

			if (!Preferences.Instance.ShowSongPropertiesWindow)
				return;

			ImGui.SetNextWindowSize(new Vector2(0, 0), ImGuiCond.FirstUseEver);
			ImGui.Begin("Song Properties", ref Preferences.Instance.ShowSongPropertiesWindow, ImGuiWindowFlags.NoScrollbar);

			if (EditorSong == null)
				Utils.PushDisabled();

			if (EditorSong != null)
			{
				var (bound, pressed) = EditorSong.Banner.GetTexture().DrawButton();
				if (pressed || (!bound && EmptyTextureBanner.DrawButton()))
					BrowseBanner();

				ImGui.SameLine();

				(bound, pressed) = EditorSong.CDTitle.GetTexture().DrawButton();
				if (pressed || (!bound && EmptyTextureCDTitle.DrawButton()))
					BrowseCDTitle();
			}
			else
			{
				EmptyTextureBanner.DrawButton();
				ImGui.SameLine();
				EmptyTextureCDTitle.DrawButton();
			}

			if (ImGuiLayoutUtils.BeginTable("SongInfoTable", 100))
			{
				ImGuiLayoutUtils.DrawRowTextInputWithTransliteration(true, "Title", EditorSong, nameof(EditorSong.Title), nameof(EditorSong.TitleTransliteration), true,
					"The title of the song.");
				ImGuiLayoutUtils.DrawRowTextInputWithTransliteration(true, "Subtitle", EditorSong, nameof(EditorSong.Subtitle), nameof(EditorSong.SubtitleTransliteration), true,
					"The subtitle of the song.");
				ImGuiLayoutUtils.DrawRowTextInputWithTransliteration(true, "Artist", EditorSong, nameof(EditorSong.Artist), nameof(EditorSong.ArtistTransliteration), true,
					"The artist who composed the song.");
				ImGuiLayoutUtils.DrawRowTextInput(true, "Genre", EditorSong, nameof(EditorSong.Genre), true,
					"The genre of the song.");
				ImGuiLayoutUtils.DrawRowTextInput(true, "Credit", EditorSong, nameof(EditorSong.Credit), true,
					"Who this file should be credited to.");
				ImGuiLayoutUtils.DrawRowTextInput(true, "Origin", EditorSong, nameof(EditorSong.Origin), true,
					"(Uncommon) What game this song originated from.");
				ImGuiLayoutUtils.EndTable();
			}

			ImGui.Separator();
			if (ImGuiLayoutUtils.BeginTable("SongAssetsTable", 100))
			{
				ImGuiLayoutUtils.DrawRowAutoFileBrowse("Banner", EditorSong?.Banner, nameof(EditorSong.Banner.Path), TryFindBestBanner, BrowseBanner, ClearBanner, true,
					"The banner graphic to display for this song when it is selected in the song wheel."
					+ "\nITG banners are 418x164."
					+ "\nDDR banners are 512x160 or 256x80.");

				ImGuiLayoutUtils.DrawRowAutoFileBrowse("Background", EditorSong?.Background, nameof(EditorSong.Background.Path), TryFindBestBackground, BrowseBackground, ClearBackground, true,
					"The background graphic to display for this song while it is being played."
					+ "\nITG backgrounds are 640x480.");

				ImGuiLayoutUtils.DrawRowFileBrowse("CD Title", EditorSong?.CDTitle, nameof(EditorSong.CDTitle.Path), BrowseCDTitle, ClearCDTitle, true,
					"The CD title graphic is most commonly used as a logo for the file author."
					+ "\nDimensions are arbitrary.");

				ImGuiLayoutUtils.DrawRowFileBrowse("Jacket", EditorSong?.Jacket, nameof(EditorSong.CDTitle.Path), BrowseJacket, ClearJacket, true,
					"(Uncommon) Jacket graphic."
					+ "\nMeant for themes which display songs with jacket assets in the song wheel like DDR X2."
					+ "\nTypically square, but dimensions are arbitrary.");

				ImGuiLayoutUtils.DrawRowFileBrowse("CD Image", EditorSong?.CDImage, nameof(EditorSong.CDTitle.Path), BrowseCDImage, ClearCDImage, true,
					"(Uncommon) CD image graphic."
					+ "\nOriginally meant to capture song select graphics which looked like CDs from the original DDR."
					+ "\nTypically square, but dimensions are arbitrary.");

				ImGuiLayoutUtils.DrawRowFileBrowse("Disc Image", EditorSong?.DiscImage, nameof(EditorSong.CDTitle.Path), BrowseDiscImage, ClearDiscImage, true,
					"(Uncommon) Disc Image graphic."
					+ "\nOriginally meant to capture PIU song select graphics, which were discs in very old versions."
					+ "\nMore modern PIU uses rectangular banners, but dimensions are arbitrary.");

				ImGuiLayoutUtils.DrawRowFileBrowse("Preview Video", EditorSong, nameof(EditorSong.PreviewVideoPath), BrowsePreviewVideoFile, ClearPreviewVideoFile, true,
					"(Uncommon) The preview video file." +
					"\nMeant for themes based on PIU where videos play on the song select screen.");
				ImGuiLayoutUtils.EndTable();
			}

			ImGui.Separator();
			if (ImGuiLayoutUtils.BeginTable("SongMusicTimingTable", 100))
			{
				ImGuiLayoutUtils.DrawRowFileBrowse("Music", EditorSong, nameof(EditorSong.MusicPath), BrowseMusicFile, ClearMusicFile, true,
					"The default audio file to use for all Charts for this Song." +
					"\nIn most cases all Charts use the same Music and it is defined here at the Song level.");

				ImGuiLayoutUtils.DrawRowDragDouble(true, "Music Offset", EditorSong, nameof(EditorSong.MusicOffset), true,
					"The music offset from the start of the chart.",
					0.0001f, "%.6f seconds");

				ImGuiLayoutUtils.DrawRowFileBrowse("Preview File", EditorSong, nameof(EditorSong.MusicPreviewPath), BrowseMusicPreviewFile, ClearMusicPreviewFile, true,
					"(Uncommon) An audio file to use for a preview instead of playing a range from the music file.");

				// TODO: Better Preview Controls
				ImGuiLayoutUtils.DrawRowDragDouble(true, "Preview Start", EditorSong, nameof(EditorSong.SampleStart), true,
					"Music preview start time.",
					0.0001f, "%.6f seconds");
				ImGuiLayoutUtils.DrawRowDragDouble(true, "Preview Length", EditorSong, nameof(EditorSong.SampleLength), true,
					"Music preview length.",
					0.0001f, "%.6f seconds", 0.0);
				if (ImGuiLayoutUtils.DrawRowButton(null, Editor.IsPlayingPreview() ? "Stop Preview" : "Play Preview"))
					Editor.OnTogglePlayPreview();

				// TODO: Better LastSecondHint Controls.
				ImGuiLayoutUtils.DrawRowDragDouble(true, "Last Second Hint", EditorSong, nameof(EditorSong.LastSecondHint), true,
					"The specified end time of the song." +
					"\nOptional. When not set StepMania will stop a chart shortly after the last note." +
					"\nUseful if you want the chart to continue after the last note." +
					"\nStepMania will ignore this value if it is less than or equal to 0.0.",
					0.0001f, "%.6f seconds", 0.0);
				
				ImGuiLayoutUtils.EndTable();
			}

			ImGui.Separator();
			if (ImGuiLayoutUtils.BeginTable("SongMiscTable", 100))
			{
				ImGuiLayoutUtils.DrawRowEnum<Selectable>(true, "Selectable", EditorSong, nameof(EditorSong.Selectable), true,
					"(Uncommon) Under what conditions this song should be selectable." +
					"\nMeant to capture stage requirements from DDR like extra stage and one more extra stage." +
					"\nLeave as YES if you are unsure what to use.");

				ImGuiLayoutUtils.DrawRowFileBrowse("Lyrics", EditorSong, nameof(EditorSong.LyricsPath), BrowseLyricsFile, ClearLyricsFile, true,
					"(Uncommon) Lyrics file for displaying lyrics while the song plays.");
				
				ImGuiLayoutUtils.EndTable();
			}

			if (EditorSong == null)
				Utils.PopDisabled();

			ImGui.End();
		}

		private static string TryFindBestAsset(
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
				var files = Directory.GetFiles(EditorSong.FileDirectory);

				// Check for files which match StepMania's expected names.
				var assetFiles = new List<string>();
				var videoFiles = new List<string>();
				foreach (var file in files)
				{
					try
					{
						var fileNameNoExtension = System.IO.Path.GetFileNameWithoutExtension(file).ToLower();
						var extension = System.IO.Path.GetExtension(file);
						if (extension.StartsWith('.'))
							extension = extension.Substring(1);
						var imageFile = Utils.ExpectedImageFormats.Contains(extension);
						var videoFile = Utils.ExpectedVideoFormats.Contains(extension);

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
				var expectedAspectRatio = (float)w / h;
				string biggestFile = null;
				int biggestSize = 0;
				foreach (var file in assetFiles)
				{
					try
					{
						using Stream stream = File.OpenRead(file);
						using var sourceImage = System.Drawing.Image.FromStream(stream, false, false);

						var sourceAspectRatio = (float)sourceImage.Width / sourceImage.Height;
						if (Math.Abs(sourceAspectRatio - expectedAspectRatio) < 0.001f)
							filesWithExpectedAspectRatio.Add(file);

						var size = sourceImage.Width * sourceImage.Height;
						if (size > biggestSize && sourceImage.Width >= w && sourceImage.Height >= h)
						{
							biggestSize = size;
							biggestFile = file;
						}

						if (sourceImage.Width == w && sourceImage.Height == h)
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

		private static void TryFindBestBanner()
		{
			var bestFile = TryFindBestAsset(Utils.BannerWidth, Utils.BannerHeight, " bn", "banner", false, false, false);
			if (!string.IsNullOrEmpty(bestFile))
			{
				var relativePath = Path.GetRelativePath(EditorSong.FileDirectory, bestFile);
				if (relativePath != EditorSong.Banner.Path)
					ActionQueue.Instance.Do(new ActionSetObjectFieldOrPropertyReference<string>(EditorSong.Banner, nameof(EditorSong.Banner.Path), relativePath, true));
				else
					Logger.Info($"Song banner is already set to the best automatic choice: '{relativePath}'.");
			}
			else
			{
				Logger.Info("Could not automatically determine the song banner.");
			}
		}

		private static void BrowseBanner()
		{
			var relativePath = Utils.BrowseFile(
				"Banner",
				EditorSong.FileDirectory,
				EditorSong.Banner.Path,
				Utils.FileOpenFilterForImages("Banner", true));
			if (relativePath != null && relativePath != EditorSong.Banner.Path)
				ActionQueue.Instance.Do(new ActionSetObjectFieldOrPropertyReference<string>(EditorSong.Banner, nameof(EditorSong.Banner.Path), relativePath, true));
		}

		private static void ClearBanner()
		{
			if (!string.IsNullOrEmpty(EditorSong.Banner.Path))
				ActionQueue.Instance.Do(new ActionSetObjectFieldOrPropertyReference<string>(EditorSong.Banner, nameof(EditorSong.Banner.Path), "", true));
		}

		private static void TryFindBestBackground()
		{
			var bestFile = TryFindBestAsset(Utils.BannerWidth, Utils.BannerHeight, "bg", "background", true, true, true);
			if (!string.IsNullOrEmpty(bestFile))
			{
				var relativePath = Path.GetRelativePath(EditorSong.FileDirectory, bestFile);
				if (relativePath != EditorSong.Background.Path)
					ActionQueue.Instance.Do(new ActionSetObjectFieldOrPropertyReference<string>(EditorSong.Background, nameof(EditorSong.Background.Path), relativePath, true));
				else
					Logger.Info($"Song background is already set to the best automatic choice: '{relativePath}'.");
			}
			else
			{
				Logger.Info("Could not automatically determine the song background.");
			}
		}

		private static void BrowseBackground()
		{
			var relativePath = Utils.BrowseFile(
				"Background",
				EditorSong.FileDirectory,
				EditorSong.Banner.Path,
				Utils.FileOpenFilterForImagesAndVideos("Background", true));
			if (relativePath != null && relativePath != EditorSong.Background.Path)
				ActionQueue.Instance.Do(new ActionSetObjectFieldOrPropertyReference<string>(EditorSong.Background, nameof(EditorSong.Background.Path), relativePath, true));
		}

		private static void ClearBackground()
		{
			if (!string.IsNullOrEmpty(EditorSong.Background.Path))
				ActionQueue.Instance.Do(new ActionSetObjectFieldOrPropertyReference<string>(EditorSong.Background, nameof(EditorSong.Background.Path), "", true));
		}

		private static void BrowseCDTitle()
		{
			var relativePath = Utils.BrowseFile(
				"CD Title",
				EditorSong.FileDirectory,
				EditorSong.CDTitle.Path,
				Utils.FileOpenFilterForImages("CD Title", true));
			if (relativePath != null && relativePath != EditorSong.CDTitle.Path)
				ActionQueue.Instance.Do(new ActionSetObjectFieldOrPropertyReference<string>(EditorSong.CDTitle, nameof(EditorSong.CDTitle.Path), relativePath, true));
		}

		private static void ClearCDTitle()
		{
			if (!string.IsNullOrEmpty(EditorSong.CDTitle.Path))
				ActionQueue.Instance.Do(new ActionSetObjectFieldOrPropertyReference<string>(EditorSong.CDTitle, nameof(EditorSong.CDTitle.Path), "", true));
		}

		private static void BrowseJacket()
		{
			var relativePath = Utils.BrowseFile(
				"Jacket",
				EditorSong.FileDirectory,
				EditorSong.Jacket.Path,
				Utils.FileOpenFilterForImages("Jacket", true));
			if (relativePath != null && relativePath != EditorSong.Jacket.Path)
				ActionQueue.Instance.Do(new ActionSetObjectFieldOrPropertyReference<string>(EditorSong.Jacket, nameof(EditorSong.Jacket.Path), relativePath, true));
		}

		private static void ClearJacket()
		{
			if (!string.IsNullOrEmpty(EditorSong.Jacket.Path))
				ActionQueue.Instance.Do(new ActionSetObjectFieldOrPropertyReference<string>(EditorSong.Jacket, nameof(EditorSong.Jacket.Path), "", true));
		}

		private static void BrowseCDImage()
		{
			var relativePath = Utils.BrowseFile(
				"CD Image",
				EditorSong.FileDirectory,
				EditorSong.CDImage.Path,
				Utils.FileOpenFilterForImages("CD Image", true));
			if (relativePath != null && relativePath != EditorSong.CDImage.Path)
				ActionQueue.Instance.Do(new ActionSetObjectFieldOrPropertyReference<string>(EditorSong.CDImage, nameof(EditorSong.CDImage.Path), relativePath, true));
		}

		private static void ClearCDImage()
		{
			if (!string.IsNullOrEmpty(EditorSong.CDImage.Path))
				ActionQueue.Instance.Do(new ActionSetObjectFieldOrPropertyReference<string>(EditorSong.CDImage, nameof(EditorSong.CDImage.Path), "", true));
		}

		private static void BrowseDiscImage()
		{
			var relativePath = Utils.BrowseFile(
				"Disc Image",
				EditorSong.FileDirectory,
				EditorSong.DiscImage.Path,
				Utils.FileOpenFilterForImages("Disc Image", true));
			if (relativePath != null && relativePath != EditorSong.DiscImage.Path)
				ActionQueue.Instance.Do(new ActionSetObjectFieldOrPropertyReference<string>(EditorSong.DiscImage, nameof(EditorSong.DiscImage.Path), relativePath, true));
		}

		private static void ClearDiscImage()
		{
			if (!string.IsNullOrEmpty(EditorSong.DiscImage.Path))
				ActionQueue.Instance.Do(new ActionSetObjectFieldOrPropertyReference<string>(EditorSong.DiscImage, nameof(EditorSong.DiscImage.Path), "", true));
		}

		private static void BrowseMusicFile()
		{
			var relativePath = Utils.BrowseFile(
				"Music",
				EditorSong.FileDirectory,
				EditorSong.MusicPath,
				Utils.FileOpenFilterForAudio("Music", true));
			if (relativePath != null && relativePath != EditorSong.MusicPath)
				ActionQueue.Instance.Do(new ActionSetObjectFieldOrPropertyReference<string>(EditorSong, nameof(EditorSong.MusicPath), relativePath, true));
		}

		private static void ClearMusicFile()
		{
			if (!string.IsNullOrEmpty(EditorSong.MusicPath))
				ActionQueue.Instance.Do(new ActionSetObjectFieldOrPropertyReference<string>(EditorSong, nameof(EditorSong.MusicPath), "", true));
		}

		private static void BrowseMusicPreviewFile()
		{
			var relativePath = Utils.BrowseFile(
				"Music Preview",
				EditorSong.FileDirectory,
				EditorSong.MusicPreviewPath,
				Utils.FileOpenFilterForAudio("Music Preview", true));
			if (relativePath != null && relativePath != EditorSong.MusicPreviewPath)
				ActionQueue.Instance.Do(new ActionSetObjectFieldOrPropertyReference<string>(EditorSong, nameof(EditorSong.MusicPreviewPath), relativePath, true));
		}

		private static void ClearMusicPreviewFile()
		{
			if (!string.IsNullOrEmpty(EditorSong.MusicPreviewPath))
				ActionQueue.Instance.Do(new ActionSetObjectFieldOrPropertyReference<string>(EditorSong, nameof(EditorSong.MusicPreviewPath), "", true));
		}

		private static void BrowseLyricsFile()
		{
			var relativePath = Utils.BrowseFile(
				"Lyrics",
				EditorSong.FileDirectory,
				EditorSong.LyricsPath,
				Utils.FileOpenFilterForLyrics("Lyrics", true));
			if (relativePath != null && relativePath != EditorSong.LyricsPath)
				ActionQueue.Instance.Do(new ActionSetObjectFieldOrPropertyReference<string>(EditorSong, nameof(EditorSong.LyricsPath), relativePath, true));
		}

		private static void ClearLyricsFile()
		{
			if (!string.IsNullOrEmpty(EditorSong.LyricsPath))
				ActionQueue.Instance.Do(new ActionSetObjectFieldOrPropertyReference<string>(EditorSong, nameof(EditorSong.LyricsPath), "", true));
		}

		private static void BrowsePreviewVideoFile()
		{
			var relativePath = Utils.BrowseFile(
				"Preview Video",
				EditorSong.FileDirectory,
				EditorSong.PreviewVideoPath,
				Utils.FileOpenFilterForVideo("Preview Video", true));
			if (relativePath != null && relativePath != EditorSong.PreviewVideoPath)
				ActionQueue.Instance.Do(new ActionSetObjectFieldOrPropertyReference<string>(EditorSong, nameof(EditorSong.PreviewVideoPath), relativePath, true));
		}

		private static void ClearPreviewVideoFile()
		{
			if (!string.IsNullOrEmpty(EditorSong.PreviewVideoPath))
				ActionQueue.Instance.Do(new ActionSetObjectFieldOrPropertyReference<string>(EditorSong, nameof(EditorSong.PreviewVideoPath), "", true));
		}
	}
}

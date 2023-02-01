using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Fumen;
using ImGuiNET;
using Microsoft.Xna.Framework.Graphics;
using Path = Fumen.Path;
using static StepManiaEditor.Utils;

namespace StepManiaEditor
{
	/// <summary>
	/// Class for drawing Song properties UI.
	/// </summary>
	internal sealed class UISongProperties
	{
		private readonly Editor Editor;
		private EditorSong EditorSong;

		private static EmptyTexture EmptyTextureBanner;
		private static EmptyTexture EmptyTextureCDTitle;

		private static readonly int TitleColumnWidth = UiScaled(100);

		public UISongProperties(Editor editor, GraphicsDevice graphicsDevice, ImGuiRenderer imGuiRenderer)
		{
			EmptyTextureBanner = new EmptyTexture(graphicsDevice, imGuiRenderer, (uint)GetBannerWidth(), (uint)GetBannerHeight());
			EmptyTextureCDTitle = new EmptyTexture(graphicsDevice, imGuiRenderer, (uint)GetCDTitleWidth(), (uint)GetCDTitleHeight());
			Editor = editor;
		}

		public void Draw(EditorSong editorSong)
		{
			EditorSong = editorSong;

			if (!Preferences.Instance.ShowSongPropertiesWindow)
				return;

			ImGui.SetNextWindowSize(new Vector2(0, 0), ImGuiCond.FirstUseEver);
			if (ImGui.Begin("Song Properties", ref Preferences.Instance.ShowSongPropertiesWindow, ImGuiWindowFlags.NoScrollbar))
			{

				if (EditorSong == null)
					PushDisabled();

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

				if (ImGuiLayoutUtils.BeginTable("SongInfoTable", TitleColumnWidth))
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
				if (ImGuiLayoutUtils.BeginTable("SongAssetsTable", TitleColumnWidth))
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
				if (ImGuiLayoutUtils.BeginTable("SongMusicTimingTable", TitleColumnWidth))
				{
					ImGuiLayoutUtils.DrawRowFileBrowse("Music", EditorSong, nameof(EditorSong.MusicPath), BrowseMusicFile, ClearMusicFile, true,
						"The default audio file to use for all Charts for this Song." +
						"\nIn most cases all Charts use the same Music and it is defined here at the Song level.");

					ImGuiLayoutUtils.DrawRowDragDouble(true, "Music Offset", EditorSong, nameof(EditorSong.MusicOffset), true,
						"The music offset from the start of the chart.",
						0.0001f, "%.6f seconds");

					ImGuiLayoutUtils.DrawRowFileBrowse("Preview File", EditorSong, nameof(EditorSong.MusicPreviewPath), BrowseMusicPreviewFile, ClearMusicPreviewFile, true,
						"(Uncommon) An audio file to use for a preview instead of playing a range from the music file.");

					ImGuiLayoutUtils.DrawRowDragDoubleWithSetAndGoButtons(true, "Preview Start", EditorSong, nameof(EditorSong.SampleStart), true,
						SetPreviewStartFromCurrentTime, "Use Current Time", JumpToPreviewStart,
						"Music preview start time.\n" +
						EditorPreviewRegionEvent.PreviewDescription,
						0.0001f, "%.6f seconds");
					ImGuiLayoutUtils.DrawRowDragDoubleWithSetAndGoButtons(true, "Preview Length", EditorSong, nameof(EditorSong.SampleLength), true,
						SetPreviewEndFromCurrentTime, "Use Current Time", JumpToPreviewEnd,
						"Music preview length.\n" +
						EditorPreviewRegionEvent.PreviewDescription,
						0.0001f, "%.6f seconds", 0.0);
					if (ImGuiLayoutUtils.DrawRowButton(null, Editor.IsPlayingPreview() ? "Stop Preview" : "Play Preview",
						"Toggle Preview playback. Playback can be toggled with P. Playback can be cancelled with Esc."))
						Editor.OnTogglePlayPreview();

					ImGuiLayoutUtils.DrawRowDragDoubleWithSetAndGoButtons(true, "End Hint", EditorSong, nameof(EditorSong.LastSecondHint), true,
						SetLastSecondHintFromCurrentTime, "Use Current Time", JumpToLastSecondHint,
						EditorLastSecondHintEvent.LastSecondHintDescription,
						0.0001f, "%.6f seconds", 0.0);

					ImGuiLayoutUtils.EndTable();
				}

				ImGui.Separator();
				if (ImGuiLayoutUtils.BeginTable("SongMiscTable", TitleColumnWidth))
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
					PopDisabled();
			}
			ImGui.End();
		}

		private string TryFindBestAsset(
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
						if (Math.Abs(sourceAspectRatio - expectedAspectRatio) < 0.05f)
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

		private void TryFindBestBanner()
		{
			var bestFile = TryFindBestAsset(GetUnscaledBannerWidth(), GetUnscaledBannerHeight(), "bn", "banner", false, false, false);
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

		private void BrowseBanner()
		{
			var relativePath = BrowseFile(
				"Banner",
				EditorSong.FileDirectory,
				EditorSong.Banner.Path,
				FileOpenFilterForImages("Banner", true));
			if (relativePath != null && relativePath != EditorSong.Banner.Path)
				ActionQueue.Instance.Do(new ActionSetObjectFieldOrPropertyReference<string>(EditorSong.Banner, nameof(EditorSong.Banner.Path), relativePath, true));
		}

		private void ClearBanner()
		{
			if (!string.IsNullOrEmpty(EditorSong.Banner.Path))
				ActionQueue.Instance.Do(new ActionSetObjectFieldOrPropertyReference<string>(EditorSong.Banner, nameof(EditorSong.Banner.Path), "", true));
		}

		private void TryFindBestBackground()
		{
			var bestFile = TryFindBestAsset(GetUnscaledBackgroundWidth(), GetUnscaledBackgroundHeight(), "bg", "background", true, true, true);
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

		private void BrowseBackground()
		{
			var relativePath = BrowseFile(
				"Background",
				EditorSong.FileDirectory,
				EditorSong.Banner.Path,
				FileOpenFilterForImagesAndVideos("Background", true));
			if (relativePath != null && relativePath != EditorSong.Background.Path)
				ActionQueue.Instance.Do(new ActionSetObjectFieldOrPropertyReference<string>(EditorSong.Background, nameof(EditorSong.Background.Path), relativePath, true));
		}

		private void ClearBackground()
		{
			if (!string.IsNullOrEmpty(EditorSong.Background.Path))
				ActionQueue.Instance.Do(new ActionSetObjectFieldOrPropertyReference<string>(EditorSong.Background, nameof(EditorSong.Background.Path), "", true));
		}

		private void BrowseCDTitle()
		{
			var relativePath = BrowseFile(
				"CD Title",
				EditorSong.FileDirectory,
				EditorSong.CDTitle.Path,
				FileOpenFilterForImages("CD Title", true));
			if (relativePath != null && relativePath != EditorSong.CDTitle.Path)
				ActionQueue.Instance.Do(new ActionSetObjectFieldOrPropertyReference<string>(EditorSong.CDTitle, nameof(EditorSong.CDTitle.Path), relativePath, true));
		}

		private void ClearCDTitle()
		{
			if (!string.IsNullOrEmpty(EditorSong.CDTitle.Path))
				ActionQueue.Instance.Do(new ActionSetObjectFieldOrPropertyReference<string>(EditorSong.CDTitle, nameof(EditorSong.CDTitle.Path), "", true));
		}

		private void BrowseJacket()
		{
			var relativePath = BrowseFile(
				"Jacket",
				EditorSong.FileDirectory,
				EditorSong.Jacket.Path,
				FileOpenFilterForImages("Jacket", true));
			if (relativePath != null && relativePath != EditorSong.Jacket.Path)
				ActionQueue.Instance.Do(new ActionSetObjectFieldOrPropertyReference<string>(EditorSong.Jacket, nameof(EditorSong.Jacket.Path), relativePath, true));
		}

		private void ClearJacket()
		{
			if (!string.IsNullOrEmpty(EditorSong.Jacket.Path))
				ActionQueue.Instance.Do(new ActionSetObjectFieldOrPropertyReference<string>(EditorSong.Jacket, nameof(EditorSong.Jacket.Path), "", true));
		}

		private void BrowseCDImage()
		{
			var relativePath = BrowseFile(
				"CD Image",
				EditorSong.FileDirectory,
				EditorSong.CDImage.Path,
				FileOpenFilterForImages("CD Image", true));
			if (relativePath != null && relativePath != EditorSong.CDImage.Path)
				ActionQueue.Instance.Do(new ActionSetObjectFieldOrPropertyReference<string>(EditorSong.CDImage, nameof(EditorSong.CDImage.Path), relativePath, true));
		}

		private void ClearCDImage()
		{
			if (!string.IsNullOrEmpty(EditorSong.CDImage.Path))
				ActionQueue.Instance.Do(new ActionSetObjectFieldOrPropertyReference<string>(EditorSong.CDImage, nameof(EditorSong.CDImage.Path), "", true));
		}

		private void BrowseDiscImage()
		{
			var relativePath = BrowseFile(
				"Disc Image",
				EditorSong.FileDirectory,
				EditorSong.DiscImage.Path,
				FileOpenFilterForImages("Disc Image", true));
			if (relativePath != null && relativePath != EditorSong.DiscImage.Path)
				ActionQueue.Instance.Do(new ActionSetObjectFieldOrPropertyReference<string>(EditorSong.DiscImage, nameof(EditorSong.DiscImage.Path), relativePath, true));
		}

		private void ClearDiscImage()
		{
			if (!string.IsNullOrEmpty(EditorSong.DiscImage.Path))
				ActionQueue.Instance.Do(new ActionSetObjectFieldOrPropertyReference<string>(EditorSong.DiscImage, nameof(EditorSong.DiscImage.Path), "", true));
		}

		private void BrowseMusicFile()
		{
			var relativePath = BrowseFile(
				"Music",
				EditorSong.FileDirectory,
				EditorSong.MusicPath,
				FileOpenFilterForAudio("Music", true));
			if (relativePath != null && relativePath != EditorSong.MusicPath)
				ActionQueue.Instance.Do(new ActionSetObjectFieldOrPropertyReference<string>(EditorSong, nameof(EditorSong.MusicPath), relativePath, true));
		}

		private void ClearMusicFile()
		{
			if (!string.IsNullOrEmpty(EditorSong.MusicPath))
				ActionQueue.Instance.Do(new ActionSetObjectFieldOrPropertyReference<string>(EditorSong, nameof(EditorSong.MusicPath), "", true));
		}

		private void BrowseMusicPreviewFile()
		{
			var relativePath = BrowseFile(
				"Music Preview",
				EditorSong.FileDirectory,
				EditorSong.MusicPreviewPath,
				FileOpenFilterForAudio("Music Preview", true));
			if (relativePath != null && relativePath != EditorSong.MusicPreviewPath)
				ActionQueue.Instance.Do(new ActionSetObjectFieldOrPropertyReference<string>(EditorSong, nameof(EditorSong.MusicPreviewPath), relativePath, true));
		}

		private void ClearMusicPreviewFile()
		{
			if (!string.IsNullOrEmpty(EditorSong.MusicPreviewPath))
				ActionQueue.Instance.Do(new ActionSetObjectFieldOrPropertyReference<string>(EditorSong, nameof(EditorSong.MusicPreviewPath), "", true));
		}

		private void BrowseLyricsFile()
		{
			var relativePath = BrowseFile(
				"Lyrics",
				EditorSong.FileDirectory,
				EditorSong.LyricsPath,
				FileOpenFilterForLyrics("Lyrics", true));
			if (relativePath != null && relativePath != EditorSong.LyricsPath)
				ActionQueue.Instance.Do(new ActionSetObjectFieldOrPropertyReference<string>(EditorSong, nameof(EditorSong.LyricsPath), relativePath, true));
		}

		private void ClearLyricsFile()
		{
			if (!string.IsNullOrEmpty(EditorSong.LyricsPath))
				ActionQueue.Instance.Do(new ActionSetObjectFieldOrPropertyReference<string>(EditorSong, nameof(EditorSong.LyricsPath), "", true));
		}

		private void BrowsePreviewVideoFile()
		{
			var relativePath = BrowseFile(
				"Preview Video",
				EditorSong.FileDirectory,
				EditorSong.PreviewVideoPath,
				FileOpenFilterForVideo("Preview Video", true));
			if (relativePath != null && relativePath != EditorSong.PreviewVideoPath)
				ActionQueue.Instance.Do(new ActionSetObjectFieldOrPropertyReference<string>(EditorSong, nameof(EditorSong.PreviewVideoPath), relativePath, true));
		}

		private void ClearPreviewVideoFile()
		{
			if (!string.IsNullOrEmpty(EditorSong.PreviewVideoPath))
				ActionQueue.Instance.Do(new ActionSetObjectFieldOrPropertyReference<string>(EditorSong, nameof(EditorSong.PreviewVideoPath), "", true));
		}

		private void SetPreviewStartFromCurrentTime()
		{
			var startTime = Math.Max(0.0, Editor.GetPosition().SongTime);
			ActionQueue.Instance.Do(new ActionSetObjectFieldOrPropertyValue<double>(EditorSong, nameof(EditorSong.SampleStart), startTime, true));
		}

		private void JumpToPreviewStart()
		{
			Editor.GetPosition().SongTime = EditorSong.SampleStart;
		}

		private void SetPreviewEndFromCurrentTime()
		{
			var currentTime = Math.Max(0.0, Editor.GetPosition().SongTime);
			var startTime = EditorSong.SampleStart;
			var sampleLength = Math.Max(0.0, currentTime - startTime);
			ActionQueue.Instance.Do(new ActionSetObjectFieldOrPropertyValue<double>(EditorSong, nameof(EditorSong.SampleLength), sampleLength, true));
		}

		private void JumpToPreviewEnd()
		{
			Editor.GetPosition().SongTime = EditorSong.SampleStart + EditorSong.SampleLength;
		}

		private void SetLastSecondHintFromCurrentTime()
		{
			var currentTime = Math.Max(0.0, Editor.GetPosition().ChartTime);
			ActionQueue.Instance.Do(new ActionSetObjectFieldOrPropertyValue<double>(EditorSong, nameof(EditorSong.LastSecondHint), currentTime, true));
		}

		private void JumpToLastSecondHint()
		{
			Editor.GetPosition().ChartTime = EditorSong.LastSecondHint;
		}
	}
}

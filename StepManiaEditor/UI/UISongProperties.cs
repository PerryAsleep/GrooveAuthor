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
using static StepManiaEditor.ImGuiUtils;
using static StepManiaEditor.EditorSongImageUtils;

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
		private static readonly float ButtonSetWidth = UiScaled(108);
		private static readonly float ButtonGoWidth = UiScaled(20);
		private static readonly float ButtonSyncWidth = UiScaled(60);
		private static readonly float ButtonApplyItgOffsetWidth = UiScaled(110);

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
				var disabled = !Editor.CanSongBeEdited(EditorSong);
				if (disabled)
					PushDisabled();

				if (EditorSong != null)
				{
					var (bound, pressed) = EditorSong.GetBanner().GetTexture().DrawButton();
					if (pressed || (!bound && EmptyTextureBanner.DrawButton()))
						BrowseBanner();

					ImGui.SameLine();

					(bound, pressed) = EditorSong.GetCDTitle().GetTexture().DrawButton();
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
					ImGuiLayoutUtils.DrawRowAutoFileBrowse("Banner", EditorSong, nameof(EditorSong.BannerPath), TryFindBestBanner, BrowseBanner, ClearBanner, true,
						"The banner graphic to display for this song when it is selected in the song wheel."
						+ "\nITG banners are 418x164."
						+ "\nDDR banners are 512x160 or 256x80.");

					ImGuiLayoutUtils.DrawRowAutoFileBrowse("Background", EditorSong, nameof(EditorSong.BackgroundPath), TryFindBestBackground, BrowseBackground, ClearBackground, true,
						"The background graphic to display for this song while it is being played."
						+ "\nITG backgrounds are 640x480.");

					ImGuiLayoutUtils.DrawRowFileBrowse("CD Title", EditorSong, nameof(EditorSong.CDTitlePath), BrowseCDTitle, ClearCDTitle, true,
						"The CD title graphic is most commonly used as a logo for the file author."
						+ "\nDimensions are arbitrary.");

					ImGuiLayoutUtils.DrawRowFileBrowse("Jacket", EditorSong, nameof(EditorSong.CDTitlePath), BrowseJacket, ClearJacket, true,
						"(Uncommon) Jacket graphic."
						+ "\nMeant for themes which display songs with jacket assets in the song wheel like DDR X2."
						+ "\nTypically square, but dimensions are arbitrary.");

					ImGuiLayoutUtils.DrawRowFileBrowse("CD Image", EditorSong, nameof(EditorSong.CDTitlePath), BrowseCDImage, ClearCDImage, true,
						"(Uncommon) CD image graphic."
						+ "\nOriginally meant to capture song select graphics which looked like CDs from the original DDR."
						+ "\nTypically square, but dimensions are arbitrary.");

					ImGuiLayoutUtils.DrawRowFileBrowse("Disc Image", EditorSong, nameof(EditorSong.CDTitlePath), BrowseDiscImage, ClearDiscImage, true,
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

					ImGuiLayoutUtils.DrawRowDragDoubleWithOneButton(true, "Music Offset", EditorSong, nameof(EditorSong.MusicOffset), true,
						ApplyItgSongOffset, "Apply 9ms Offset", ButtonApplyItgOffsetWidth, EditorSong?.SyncOffset.DoubleEquals(0.0) ?? false,
						"The music offset from the start of the chart."
						+ "\nClicking the Apply 9ms Offset button will add an additional 9ms to the offset and"
						+ "\nset the Song Sync (below) to account for the 9ms offset so that the editor can"
						+ "\ncompensate and keep the arrows and Waveform in sync."
						+ "\nApplying a 9ms offset through clicking the button is not idempotent.",
						0.0001f, "%.6f seconds");

					ImGuiLayoutUtils.DrawRowFileBrowse("Preview File", EditorSong, nameof(EditorSong.MusicPreviewPath), BrowseMusicPreviewFile, ClearMusicPreviewFile, true,
						"(Uncommon) An audio file to use for a preview instead of playing a range from the music file.");

					ImGuiLayoutUtils.DrawRowDragDoubleWithTwoButtons(true, "Preview Start", EditorSong, nameof(EditorSong.SampleStart), true,
						SetPreviewStartFromCurrentTime, "Use Current Time", ButtonSetWidth,
						JumpToPreviewStart, "Go", ButtonGoWidth,
						"Music preview start time.\n" +
						EditorPreviewRegionEvent.PreviewDescription,
						0.0001f, "%.6f seconds");
					ImGuiLayoutUtils.DrawRowDragDoubleWithTwoButtons(true, "Preview Length", EditorSong, nameof(EditorSong.SampleLength), true,
						SetPreviewEndFromCurrentTime, "Use Current Time", ButtonSetWidth,
						JumpToPreviewEnd, "Go", ButtonGoWidth,
						"Music preview length.\n" +
						EditorPreviewRegionEvent.PreviewDescription,
						0.0001f, "%.6f seconds", 0.0);
					if (ImGuiLayoutUtils.DrawRowButton(null, Editor.IsPlayingPreview() ? "Stop Preview" : "Play Preview",
						"Toggle Preview playback. Playback can be toggled with P. Playback can be cancelled with Esc."))
						Editor.OnTogglePlayPreview();

					ImGuiLayoutUtils.DrawRowDragDoubleWithTwoButtons(true, "End Hint", EditorSong, nameof(EditorSong.LastSecondHint), true,
						SetLastSecondHintFromCurrentTime, "Use Current Time", ButtonSetWidth,
						JumpToLastSecondHint, "Go", ButtonGoWidth,
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

				ImGui.Separator();
				if (ImGuiLayoutUtils.BeginTable("SongCustomOptionsTable", TitleColumnWidth))
				{
					ImGuiLayoutUtils.DrawRowDragDoubleWithTwoButtons(true, "Song Sync", EditorSong, nameof(EditorSong.SyncOffset), true,
						SetSyncItg, "9ms (ITG)", ButtonSyncWidth,
						SetSyncDdr, "0ms (DDR)", ButtonSyncWidth,
						"(Editor Only) Adjust the editor visuals to account for this song's sync." +
						"\nIf the song sync is not 0, then the arrows will appear shifted from the Waveform." +
						"\nIf this song has a built in sync, then set this value so the editor can account for it" +
						"\nso the arrows and the Waveform line up perfectly." +
						"\n9ms (ITG): (More Common) Most custom content uses a 9ms offset to account for a bug in ITG2." +
						"\n           If this song is synced with a 9ms offset then use this option." +
						"\n0ms (DDR): (Less Common) Use this option of the song has no sync offset built in and is" +
						"\n           already synced perfectly." +
						"\nThe default song sync value can be set in the Options menu." +
						"\nSee https://itgwiki.dominick.cc/en/packs-and-simfiles/the-9ms-bias for more information."
						,
						0.0001f, "%.6f seconds", 0.0);

					ImGuiLayoutUtils.EndTable();
				}

				if (disabled)
					PopDisabled();
			}
			ImGui.End();
		}

		private void TryFindBestBanner()
		{
			var bestFile = EditorSongImageUtils.TryFindBestBanner(EditorSong.GetFileDirectory());
			if (!string.IsNullOrEmpty(bestFile))
			{
				var relativePath = Path.GetRelativePath(EditorSong.GetFileDirectory(), bestFile);
				if (relativePath != EditorSong.BannerPath)
					ActionQueue.Instance.Do(new ActionSetObjectFieldOrPropertyReference<string>(EditorSong, nameof(EditorSong.BannerPath), relativePath, true));
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
				EditorSong.GetFileDirectory(),
				EditorSong.BannerPath,
				FileOpenFilterForImages("Banner", true));
			Editor.UpdateSongImage(SongImageType.Banner, relativePath);
		}

		private void ClearBanner()
		{
			Editor.UpdateSongImage(SongImageType.Banner, "");
		}

		private void TryFindBestBackground()
		{
			var bestFile = EditorSongImageUtils.TryFindBestBackground(EditorSong.GetFileDirectory());
			if (!string.IsNullOrEmpty(bestFile))
			{
				var relativePath = Path.GetRelativePath(EditorSong.GetFileDirectory(), bestFile);
				if (relativePath != EditorSong.BackgroundPath)
					ActionQueue.Instance.Do(new ActionSetObjectFieldOrPropertyReference<string>(EditorSong, nameof(EditorSong.BackgroundPath), relativePath, true));
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
				EditorSong.GetFileDirectory(),
				EditorSong.BackgroundPath,
				FileOpenFilterForImagesAndVideos("Background", true));
			Editor.UpdateSongImage(SongImageType.Background, relativePath);
		}

		private void ClearBackground()
		{
			Editor.UpdateSongImage(SongImageType.Background, "");
		}

		private void BrowseCDTitle()
		{
			var relativePath = BrowseFile(
				"CD Title",
				EditorSong.GetFileDirectory(),
				EditorSong.CDTitlePath,
				FileOpenFilterForImages("CD Title", true));
			Editor.UpdateSongImage(SongImageType.CDTitle, relativePath);
		}

		private void ClearCDTitle()
		{
			Editor.UpdateSongImage(SongImageType.CDTitle, "");
		}

		private void BrowseJacket()
		{
			var relativePath = BrowseFile(
				"Jacket",
				EditorSong.GetFileDirectory(),
				EditorSong.JacketPath,
				FileOpenFilterForImages("Jacket", true));
			Editor.UpdateSongImage(SongImageType.Jacket, relativePath);
		}

		private void ClearJacket()
		{
			Editor.UpdateSongImage(SongImageType.Jacket, "");
		}

		private void BrowseCDImage()
		{
			var relativePath = BrowseFile(
				"CD Image",
				EditorSong.GetFileDirectory(),
				EditorSong.CDImagePath,
				FileOpenFilterForImages("CD Image", true));
			Editor.UpdateSongImage(SongImageType.CDImage, relativePath);
		}

		private void ClearCDImage()
		{
			Editor.UpdateSongImage(SongImageType.CDImage, "");
		}

		private void BrowseDiscImage()
		{
			var relativePath = BrowseFile(
				"Disc Image",
				EditorSong.GetFileDirectory(),
				EditorSong.DiscImagePath,
				FileOpenFilterForImages("Disc Image", true));
			Editor.UpdateSongImage(SongImageType.DiscImage, relativePath);
		}

		private void ClearDiscImage()
		{
			Editor.UpdateSongImage(SongImageType.DiscImage, "");
		}

		private void BrowseMusicFile()
		{
			var relativePath = BrowseFile(
				"Music",
				EditorSong.GetFileDirectory(),
				EditorSong.MusicPath,
				FileOpenFilterForAudio("Music", true));
			Editor.UpdateMusicPath(relativePath);
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
				EditorSong.GetFileDirectory(),
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
				EditorSong.GetFileDirectory(),
				EditorSong.LyricsPath,
				FileOpenFilterForLyrics("Lyrics", true));
			Editor.UpdateLyricsPath(relativePath);
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
				EditorSong.GetFileDirectory(),
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

		private void ApplyItgSongOffset()
		{
			var multiple = new ActionMultiple();
			multiple.EnqueueAndDo(new ActionSetObjectFieldOrPropertyValue<double>(EditorSong, nameof(EditorSong.MusicOffset), EditorSong.MusicOffset + 0.009, true));
			multiple.EnqueueAndDo(new ActionSetObjectFieldOrPropertyValue<double>(EditorSong, nameof(EditorSong.SyncOffset), 0.009, true));
			ActionQueue.Instance.EnqueueWithoutDoing(multiple);
		}

		private void SetSyncItg()
		{
			ActionQueue.Instance.Do(new ActionSetObjectFieldOrPropertyValue<double>(EditorSong, nameof(EditorSong.SyncOffset), 0.009, true));
		}

		private void SetSyncDdr()
		{
			ActionQueue.Instance.Do(new ActionSetObjectFieldOrPropertyValue<double>(EditorSong, nameof(EditorSong.SyncOffset), 0.0, true));
		}
	}
}

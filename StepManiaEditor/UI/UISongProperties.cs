﻿using System;
using System.Numerics;
using Fumen;
using Fumen.Converters;
using ImGuiNET;
using Microsoft.Xna.Framework.Graphics;
using static StepManiaEditor.Utils;
using static StepManiaEditor.ImGuiUtils;

namespace StepManiaEditor;

/// <summary>
/// Class for drawing Song properties UI.
/// </summary>
internal sealed class UISongProperties : UIWindow
{
	private Editor Editor;
	private EditorSong EditorSong;

	private EmptyTexture EmptyTextureBanner;
	private EmptyTexture EmptyTextureCDTitle;

	private static readonly int TitleColumnWidth = UiScaled(90);
	private static readonly float ButtonSetWidth = UiScaled(108);
	private static readonly float ButtonGoWidth = UiScaled(20);
	private static readonly float ButtonSyncWidth = UiScaled(60);
	private static readonly float ButtonHelpWidth = UiScaled(32);
	private static readonly float ButtonApplyItgOffsetWidth = UiScaled(110);
	private static readonly Vector2 DefaultPosition = new(UiScaled(0), UiScaled(21));
	public static readonly Vector2 DefaultSize = new(UiScaled(637), UiScaled(610));
	public static readonly Vector2 DefaultSizeSmall = new(UiScaled(457), UiScaled(577));

	public static UISongProperties Instance { get; } = new();

	private UISongProperties() : base("Song Properties")
	{
	}

	public void Init(Editor editor, GraphicsDevice graphicsDevice, ImGuiRenderer imGuiRenderer)
	{
		EmptyTextureBanner = new EmptyTexture(graphicsDevice, imGuiRenderer, (uint)GetBannerWidth(), (uint)GetBannerHeight());
		EmptyTextureCDTitle = new EmptyTexture(graphicsDevice, imGuiRenderer, (uint)GetCDTitleWidth(), (uint)GetCDTitleHeight());
		Editor = editor;
	}

	public override void Open(bool focus)
	{
		Preferences.Instance.ShowSongPropertiesWindow = true;
		if (focus)
			Focus();
	}

	public override void Close()
	{
		Preferences.Instance.ShowSongPropertiesWindow = false;
	}

	public void Draw(EditorSong editorSong)
	{
		EditorSong = editorSong;

		if (!Preferences.Instance.ShowSongPropertiesWindow)
			return;

		ImGui.SetNextWindowPos(DefaultPosition, ImGuiCond.FirstUseEver);
		ImGui.SetNextWindowSize(DefaultSize, ImGuiCond.FirstUseEver);
		if (ImGui.Begin(WindowTitle, ref Preferences.Instance.ShowSongPropertiesWindow, ImGuiWindowFlags.AlwaysVerticalScrollbar))
		{
			var disabled = !Editor.CanSongBeEdited(EditorSong);
			if (disabled)
				PushDisabled();

			if (EditorSong != null)
			{
				var (bound, pressed) = EditorSong.GetBanner().GetTexture().DrawButton();
				if (pressed || (!bound && EmptyTextureBanner.DrawButton()))
					BrowseBanner(Editor.GetPlatformInterface());

				ImGui.SameLine();

				(bound, pressed) = EditorSong.GetCDTitle().GetTexture().DrawButton();
				if (pressed || (!bound && EmptyTextureCDTitle.DrawButton()))
					BrowseCDTitle(Editor.GetPlatformInterface());
			}
			else
			{
				EmptyTextureBanner.DrawButton();
				ImGui.SameLine();
				EmptyTextureCDTitle.DrawButton();
			}

			if (ImGuiLayoutUtils.BeginTable("SongInfoTable", TitleColumnWidth))
			{
				ImGuiLayoutUtils.DrawRowTextInputWithTransliteration(true, "Title", EditorSong, nameof(EditorSong.Title),
					nameof(EditorSong.TitleTransliteration), true, string.IsNullOrEmpty(EditorSong?.Title),
					"The title of the song.\nStepmania requires song titles to be set.");
				ImGuiLayoutUtils.DrawRowTextInputWithTransliteration(true, "Subtitle", EditorSong, nameof(EditorSong.Subtitle),
					nameof(EditorSong.SubtitleTransliteration), true, false,
					"The subtitle of the song.");
				ImGuiLayoutUtils.DrawRowTextInputWithTransliteration(true, "Artist", EditorSong, nameof(EditorSong.Artist),
					nameof(EditorSong.ArtistTransliteration), true, string.IsNullOrEmpty(EditorSong?.Artist),
					"The artist who composed the song.\nStepmania requires song artists to be set.");
				ImGuiLayoutUtils.DrawRowTextInput(true, "Credit", EditorSong, nameof(EditorSong.Credit), true,
					"Who this file should be credited to.");
				ImGuiLayoutUtils.EndTable();
			}

			ImGui.Separator();
			if (ImGuiLayoutUtils.BeginTable("SongAssetsTable", TitleColumnWidth))
			{
				ImGuiLayoutUtils.DrawRowAutoFileBrowse("Banner", EditorSong, nameof(EditorSong.BannerPath),
					TryFindBestBanner,
					() => BrowseBanner(Editor.GetPlatformInterface()),
					ClearBanner,
					true,
					"The banner graphic to display for this song when it is selected in the song wheel."
					+ "\nITG banners are 418x164."
					+ "\nDDR banners are 512x160 or 256x80.");

				ImGuiLayoutUtils.DrawRowAutoFileBrowse("Background", EditorSong, nameof(EditorSong.BackgroundPath),
					TryFindBestBackground,
					() => BrowseBackground(Editor.GetPlatformInterface()),
					ClearBackground,
					true,
					"The background graphic to display for this song while it is being played."
					+ "\nITG backgrounds are 640x480.");

				ImGuiLayoutUtils.DrawRowAutoFileBrowse("CD Title", EditorSong, nameof(EditorSong.CDTitlePath),
					TryFindCDTitle,
					() => BrowseCDTitle(Editor.GetPlatformInterface()),
					ClearCDTitle,
					true,
					"The CD title graphic is most commonly used as a logo for the file author."
					+ "\nDimensions are arbitrary.");
				ImGuiLayoutUtils.EndTable();
			}

			ImGui.Separator();
			if (ImGuiLayoutUtils.BeginTable("SongMusicTimingTable", TitleColumnWidth))
			{
				var musicError = EditorSong?.IsMusicInvalid() ?? true;
				if (musicError)
					PushErrorColor();
				ImGuiLayoutUtils.DrawRowFileBrowse("Music", EditorSong, nameof(EditorSong.MusicPath),
					() => BrowseMusicFile(Editor.GetPlatformInterface()),
					ClearMusicFile, true,
					"The default audio file to use for all Charts for this Song." +
					"\nIn most cases all Charts use the same Music and it is defined here at the Song level." +
					"\nStepmania requires music to be set.");
				if (musicError)
					PopErrorColor();

				ImGuiLayoutUtils.DrawRowDragDoubleWithOneButton(true, "Music Offset", EditorSong, nameof(EditorSong.MusicOffset),
					true,
					ApplyItgSongOffset, "Apply 9ms Offset", ButtonApplyItgOffsetWidth,
					EditorSong?.SyncOffset.DoubleEquals(0.0) ?? false,
					"The music offset from the start of the chart."
					+ "\nClicking the Apply 9ms Offset button will add an additional 9ms to the offset and"
					+ $"\nset the Song Sync (below) to account for the 9ms offset so that {GetAppName()} can"
					+ "\ncompensate and keep the arrows and Waveform in sync."
					+ "\nApplying a 9ms offset through clicking the button is not idempotent.",
					0.0001f, "%.6f seconds");

				ImGuiLayoutUtils.DrawRowDragDoubleWithTwoButtons(true, "Preview Start", EditorSong,
					nameof(EditorSong.SampleStart), true,
					SetPreviewStartFromCurrentTime, "Use Current Time", ButtonSetWidth,
					JumpToPreviewStart, "Go", ButtonGoWidth,
					"Music preview start time.\n" +
					EditorPreviewRegionEvent.GetPreviewDescription(),
					0.0001f, "%.6f seconds");
				ImGuiLayoutUtils.DrawRowDragDoubleWithTwoButtons(true, "Preview Length", EditorSong,
					nameof(EditorSong.SampleLength), true,
					SetPreviewEndFromCurrentTime, "Use Current Time", ButtonSetWidth,
					JumpToPreviewEnd, "Go", ButtonGoWidth,
					"Music preview length.\n" +
					EditorPreviewRegionEvent.GetPreviewDescription(),
					0.0001f, "%.6f seconds", 0.0);

				ImGuiLayoutUtils.DrawRowDragDoubleWithTwoButtons(true, "End Hint", EditorSong, nameof(EditorSong.LastSecondHint),
					true,
					SetLastSecondHintFromCurrentTime, "Use Current Time", ButtonSetWidth,
					JumpToLastSecondHint, "Go", ButtonGoWidth,
					EditorLastSecondHintEvent.LastSecondHintDescription,
					0.0001f, "%.6f seconds", 0.0);

				ImGuiLayoutUtils.EndTable();
			}

			ImGui.Separator();
			if (ImGuiLayoutUtils.BeginTable("SongCustomOptionsTable", TitleColumnWidth))
			{
				ImGuiLayoutUtils.DrawRowDragDoubleWithThreeButtons(true, "Song Sync", EditorSong, nameof(EditorSong.SyncOffset),
					true,
					SetSyncItg, "9ms (ITG)", ButtonSyncWidth,
					SetSyncDdr, "0ms (DDR)", ButtonSyncWidth,
					() => Documentation.OpenDocumentation(Documentation.Page.SongSync), "Help", ButtonHelpWidth,
					"(Editor Only) Adjust visuals to account for this song's sync." +
					"\nIf this song has a built in sync other than 0ms, then the notes will appear shifted from" +
					"\nthe Waveform and sound effects like assist ticks will be off. Set this value to the song's" +
					$"\nbuilt in sync so {GetAppName()} can compensate for it." +
					"\n9ms (ITG): (More Common) Most custom content uses a 9ms offset to account for a bug in ITG2." +
					"\n           If this song is synced with a 9ms offset then use this option." +
					"\n0ms (DDR): (Less Common) Use this option of the song has no sync offset built in and is" +
					"\n           already synced perfectly." +
					"\nThe default song sync value can be set in the Options menu.",
					0.0001f, "%.6f seconds", 0.0);

				ImGuiLayoutUtils.DrawRowTimingChart(true, "Timing Chart", EditorSong,
					"(Editor Only) The chart which should be used for song timing data." +
					"\nThere is a bug in Stepmania where even if all charts specify valid timing data, Stepmania" +
					"\nwill still use timing data defined on the song instead of the selected chart for beat-driven" +
					"\nanimations like the receptors pulsing and the cursor bouncing on the song wheel. If no timing" +
					"\ndata is defined on the song, these animations will play at 60bpm. To work around this bug," +
					$"\ntiming data like the tempo must also be defined on the song. This field tells {GetAppName()}" +
					"\nwhich chart to use for the song timing data." +
					"\n\nAdditionally, when saving an sm file which does not support chart-level timing, this field" +
					"\nis used to determine which chart to use for the song timing data." +
					"\n\nApply: Shows options for applying this chart's timing and other miscellaneous events to all other charts.");

				ImGuiLayoutUtils.EndTable();
			}

			ImGui.Separator();
			if (ImGuiLayoutUtils.BeginTable("UncommonProperties", TitleColumnWidth))
			{
				ImGuiLayoutUtils.DrawRowTextInput(true, "Genre", EditorSong, nameof(EditorSong.Genre), true,
					"The genre of the song.");

				ImGuiLayoutUtils.DrawRowTextInput(true, "Origin", EditorSong, nameof(EditorSong.Origin), true,
					"What game this song originated from.");

				ImGuiLayoutUtils.EndTable();
			}

			ImGui.Separator();
			if (ImGuiLayoutUtils.BeginTable("UncommonPreview", TitleColumnWidth))
			{
				ImGuiLayoutUtils.DrawRowFileBrowse("Preview File", EditorSong, nameof(EditorSong.MusicPreviewPath),
					() => BrowseMusicPreviewFile(Editor.GetPlatformInterface()),
					ClearMusicPreviewFile,
					true,
					"An audio file to use for a preview instead of playing a range from the music file.");

				ImGuiLayoutUtils.EndTable();
			}

			ImGui.Separator();
			if (ImGuiLayoutUtils.BeginTable("UncommonAssets", TitleColumnWidth))
			{
				ImGuiLayoutUtils.DrawRowAutoFileBrowse("Jacket", EditorSong, nameof(EditorSong.JacketPath),
					TryFindBestJacket,
					() => BrowseJacket(Editor.GetPlatformInterface()),
					ClearJacket,
					true,
					"Jacket graphic."
					+ "\nMeant for themes which display songs with jacket assets in the song wheel like DDR X2."
					+ "\nTypically square, but dimensions are arbitrary.");

				ImGuiLayoutUtils.DrawRowAutoFileBrowse("CD Image", EditorSong, nameof(EditorSong.CDImagePath),
					TryFindBestCDImage,
					() => BrowseCDImage(Editor.GetPlatformInterface()),
					ClearCDImage,
					true,
					"CD image graphic."
					+ "\nOriginally meant to capture song select graphics which looked like CDs from the original DDR."
					+ "\nTypically square, but dimensions are arbitrary.");

				ImGuiLayoutUtils.DrawRowAutoFileBrowse("Disc Image", EditorSong, nameof(EditorSong.DiscImagePath),
					TryFindBestDiscImage,
					() => BrowseDiscImage(Editor.GetPlatformInterface()),
					ClearDiscImage,
					true,
					"Disc Image graphic."
					+ "\nOriginally meant to capture PIU song select graphics, which were discs in very old versions."
					+ "\nMore modern PIU uses rectangular banners, but dimensions are arbitrary.");

				ImGuiLayoutUtils.DrawRowFileBrowse("Preview Video", EditorSong, nameof(EditorSong.PreviewVideoPath),
					() => BrowsePreviewVideoFile(Editor.GetPlatformInterface()),
					ClearPreviewVideoFile,
					true,
					"The preview video file." +
					"\nMeant for themes based on PIU where videos play on the song select screen.");

				ImGuiLayoutUtils.EndTable();
			}

			ImGui.Separator();
			if (ImGuiLayoutUtils.BeginTable("UncommonMisc", TitleColumnWidth))
			{
				ImGuiLayoutUtils.DrawRowEnum<Selectable>(true, "Selectable", EditorSong, nameof(EditorSong.Selectable), true,
					"Under what conditions this song should be selectable." +
					"\nMeant to capture stage requirements from DDR like extra stage and one more extra stage." +
					"\nLeave as YES if you are unsure what to use.");

				ImGuiLayoutUtils.DrawRowAutoFileBrowse("Lyrics", EditorSong, nameof(EditorSong.LyricsPath),
					TryFindBestLyrics,
					() => BrowseLyricsFile(Editor.GetPlatformInterface()),
					ClearLyricsFile,
					true,
					"Lyrics file for displaying lyrics while the song plays.");

				ImGuiLayoutUtils.EndTable();
			}

			if (disabled)
				PopDisabled();
		}

		ImGui.End();
	}

	private void TryFindBestBanner()
	{
		var relativePath =
			EditorSongImageUtils.TryFindBestImage(EditorSongImageUtils.SongImageType.Banner, EditorSong.GetFileDirectory());
		if (!string.IsNullOrEmpty(relativePath))
		{
			if (relativePath != EditorSong.BannerPath)
				ActionQueue.Instance.Do(new ActionSetObjectFieldOrPropertyReference<string>(EditorSong,
					nameof(EditorSong.BannerPath), relativePath, true));
			else
				Logger.Info($"Song banner is already set to the best automatic choice: '{relativePath}'.");
		}
		else
		{
			Logger.Info("Could not automatically determine the song banner.");
		}
	}

	private void BrowseBanner(IEditorPlatform platformInterface)
	{
		var relativePath = platformInterface.BrowseFile(
			"Banner",
			EditorSong.GetFileDirectory(),
			EditorSong.BannerPath,
			GetExtensionsForImages(), true);
		Editor.UpdateSongImage(EditorSongImageUtils.SongImageType.Banner, relativePath);
	}

	private void ClearBanner()
	{
		Editor.UpdateSongImage(EditorSongImageUtils.SongImageType.Banner, "");
	}

	private void TryFindBestBackground()
	{
		var relativePath =
			EditorSongImageUtils.TryFindBestImage(EditorSongImageUtils.SongImageType.Background, EditorSong.GetFileDirectory());
		if (!string.IsNullOrEmpty(relativePath))
		{
			if (relativePath != EditorSong.BackgroundPath)
				ActionQueue.Instance.Do(new ActionSetObjectFieldOrPropertyReference<string>(EditorSong,
					nameof(EditorSong.BackgroundPath), relativePath, true));
			else
				Logger.Info($"Song background is already set to the best automatic choice: '{relativePath}'.");
		}
		else
		{
			Logger.Info("Could not automatically determine the song background.");
		}
	}

	private void BrowseBackground(IEditorPlatform platformInterface)
	{
		var relativePath = platformInterface.BrowseFile(
			"Background",
			EditorSong.GetFileDirectory(),
			EditorSong.BackgroundPath,
			GetExtensionsForImagesAndVideos(), true);
		Editor.UpdateSongImage(EditorSongImageUtils.SongImageType.Background, relativePath);
	}

	private void ClearBackground()
	{
		Editor.UpdateSongImage(EditorSongImageUtils.SongImageType.Background, "");
	}

	private void TryFindCDTitle()
	{
		var relativePath =
			EditorSongImageUtils.TryFindBestImage(EditorSongImageUtils.SongImageType.CDTitle, EditorSong.GetFileDirectory());
		if (!string.IsNullOrEmpty(relativePath))
		{
			if (relativePath != EditorSong.CDTitlePath)
				ActionQueue.Instance.Do(new ActionSetObjectFieldOrPropertyReference<string>(EditorSong,
					nameof(EditorSong.CDTitlePath), relativePath, true));
			else
				Logger.Info($"CD title is already set to the best automatic choice: '{relativePath}'.");
		}
		else
		{
			Logger.Info("Could not automatically determine the CD title.");
		}
	}

	private void BrowseCDTitle(IEditorPlatform platformInterface)
	{
		var relativePath = platformInterface.BrowseFile(
			"CD Title",
			EditorSong.GetFileDirectory(),
			EditorSong.CDTitlePath,
			GetExtensionsForImages(), true);
		Editor.UpdateSongImage(EditorSongImageUtils.SongImageType.CDTitle, relativePath);
	}

	private void ClearCDTitle()
	{
		Editor.UpdateSongImage(EditorSongImageUtils.SongImageType.CDTitle, "");
	}

	private void TryFindBestJacket()
	{
		var relativePath =
			EditorSongImageUtils.TryFindBestImage(EditorSongImageUtils.SongImageType.Jacket, EditorSong.GetFileDirectory());
		if (!string.IsNullOrEmpty(relativePath))
		{
			if (relativePath != EditorSong.JacketPath)
				ActionQueue.Instance.Do(new ActionSetObjectFieldOrPropertyReference<string>(EditorSong,
					nameof(EditorSong.JacketPath), relativePath, true));
			else
				Logger.Info($"Jacket is already set to the best automatic choice: '{relativePath}'.");
		}
		else
		{
			Logger.Info("Could not automatically determine the jacket.");
		}
	}

	private void BrowseJacket(IEditorPlatform platformInterface)
	{
		var relativePath = platformInterface.BrowseFile(
			"Jacket",
			EditorSong.GetFileDirectory(),
			EditorSong.JacketPath,
			GetExtensionsForImages(), true);
		Editor.UpdateSongImage(EditorSongImageUtils.SongImageType.Jacket, relativePath);
	}

	private void ClearJacket()
	{
		Editor.UpdateSongImage(EditorSongImageUtils.SongImageType.Jacket, "");
	}

	private void TryFindBestCDImage()
	{
		var relativePath =
			EditorSongImageUtils.TryFindBestImage(EditorSongImageUtils.SongImageType.CDImage, EditorSong.GetFileDirectory());
		if (!string.IsNullOrEmpty(relativePath))
		{
			if (relativePath != EditorSong.CDImagePath)
				ActionQueue.Instance.Do(new ActionSetObjectFieldOrPropertyReference<string>(EditorSong,
					nameof(EditorSong.CDImagePath), relativePath, true));
			else
				Logger.Info($"CD image is already set to the best automatic choice: '{relativePath}'.");
		}
		else
		{
			Logger.Info("Could not automatically determine the CD image.");
		}
	}

	private void BrowseCDImage(IEditorPlatform platformInterface)
	{
		var relativePath = platformInterface.BrowseFile(
			"CD Image",
			EditorSong.GetFileDirectory(),
			EditorSong.CDImagePath,
			GetExtensionsForImages(), true);
		Editor.UpdateSongImage(EditorSongImageUtils.SongImageType.CDImage, relativePath);
	}

	private void ClearCDImage()
	{
		Editor.UpdateSongImage(EditorSongImageUtils.SongImageType.CDImage, "");
	}

	private void TryFindBestDiscImage()
	{
		var relativePath =
			EditorSongImageUtils.TryFindBestImage(EditorSongImageUtils.SongImageType.DiscImage, EditorSong.GetFileDirectory());
		if (!string.IsNullOrEmpty(relativePath))
		{
			if (relativePath != EditorSong.DiscImagePath)
				ActionQueue.Instance.Do(new ActionSetObjectFieldOrPropertyReference<string>(EditorSong,
					nameof(EditorSong.DiscImagePath), relativePath, true));
			else
				Logger.Info($"Disc image is already set to the best automatic choice: '{relativePath}'.");
		}
		else
		{
			Logger.Info("Could not automatically determine the disc image.");
		}
	}

	private void BrowseDiscImage(IEditorPlatform platformInterface)
	{
		var relativePath = platformInterface.BrowseFile(
			"Disc Image",
			EditorSong.GetFileDirectory(),
			EditorSong.DiscImagePath,
			GetExtensionsForImages(), true);
		Editor.UpdateSongImage(EditorSongImageUtils.SongImageType.DiscImage, relativePath);
	}

	private void ClearDiscImage()
	{
		Editor.UpdateSongImage(EditorSongImageUtils.SongImageType.DiscImage, "");
	}

	private void BrowseMusicFile(IEditorPlatform platformInterface)
	{
		var relativePath = platformInterface.BrowseFile(
			"Music",
			EditorSong.GetFileDirectory(),
			EditorSong.MusicPath,
			GetExtensionsForAudio(), true);
		Editor.UpdateMusicPath(relativePath);
	}

	private void ClearMusicFile()
	{
		if (!string.IsNullOrEmpty(EditorSong.MusicPath))
			ActionQueue.Instance.Do(
				new ActionSetObjectFieldOrPropertyReference<string>(EditorSong, nameof(EditorSong.MusicPath), "", true));
	}

	private void BrowseMusicPreviewFile(IEditorPlatform platformInterface)
	{
		var relativePath = platformInterface.BrowseFile(
			"Music Preview",
			EditorSong.GetFileDirectory(),
			EditorSong.MusicPreviewPath,
			GetExtensionsForAudio(), true);
		if (relativePath != null && relativePath != EditorSong.MusicPreviewPath)
			ActionQueue.Instance.Do(new ActionSetObjectFieldOrPropertyReference<string>(EditorSong,
				nameof(EditorSong.MusicPreviewPath), relativePath, true));
	}

	private void ClearMusicPreviewFile()
	{
		if (!string.IsNullOrEmpty(EditorSong.MusicPreviewPath))
			ActionQueue.Instance.Do(
				new ActionSetObjectFieldOrPropertyReference<string>(EditorSong, nameof(EditorSong.MusicPreviewPath), "", true));
	}

	private void TryFindBestLyrics()
	{
		var relativePath = EditorSongImageUtils.TryFindBestLyrics(EditorSong.GetFileDirectory());
		if (!string.IsNullOrEmpty(relativePath))
		{
			if (relativePath != EditorSong.LyricsPath)
				ActionQueue.Instance.Do(new ActionSetObjectFieldOrPropertyReference<string>(EditorSong,
					nameof(EditorSong.LyricsPath), relativePath, true));
			else
				Logger.Info($"Lyrics are already set to the best automatic choice: '{relativePath}'.");
		}
		else
		{
			Logger.Info("Could not automatically determine the lyrics.");
		}
	}

	private void BrowseLyricsFile(IEditorPlatform platformInterface)
	{
		var relativePath = platformInterface.BrowseFile(
			"Lyrics",
			EditorSong.GetFileDirectory(),
			EditorSong.LyricsPath,
			GetExtensionsForLyrics(), true);
		Editor.UpdateLyricsPath(relativePath);
	}

	private void ClearLyricsFile()
	{
		if (!string.IsNullOrEmpty(EditorSong.LyricsPath))
			ActionQueue.Instance.Do(
				new ActionSetObjectFieldOrPropertyReference<string>(EditorSong, nameof(EditorSong.LyricsPath), "", true));
	}

	private void BrowsePreviewVideoFile(IEditorPlatform platformInterface)
	{
		var relativePath = platformInterface.BrowseFile(
			"Preview Video",
			EditorSong.GetFileDirectory(),
			EditorSong.PreviewVideoPath,
			GetExtensionsForVideo(), true);
		if (relativePath != null && relativePath != EditorSong.PreviewVideoPath)
			ActionQueue.Instance.Do(new ActionSetObjectFieldOrPropertyReference<string>(EditorSong,
				nameof(EditorSong.PreviewVideoPath), relativePath, true));
	}

	private void ClearPreviewVideoFile()
	{
		if (!string.IsNullOrEmpty(EditorSong.PreviewVideoPath))
			ActionQueue.Instance.Do(
				new ActionSetObjectFieldOrPropertyReference<string>(EditorSong, nameof(EditorSong.PreviewVideoPath), "", true));
	}

	private void SetPreviewStartFromCurrentTime()
	{
		var startTime = Math.Max(0.0, Editor.GetPosition().SongTime);
		ActionQueue.Instance.Do(
			new ActionSetObjectFieldOrPropertyValue<double>(EditorSong, nameof(EditorSong.SampleStart), startTime, true));
	}

	private void JumpToPreviewStart()
	{
		Editor.SetSongTime(EditorSong.SampleStart);
	}

	private void SetPreviewEndFromCurrentTime()
	{
		var currentTime = Math.Max(0.0, Editor.GetPosition().SongTime);
		var startTime = EditorSong.SampleStart;
		var sampleLength = Math.Max(0.0, currentTime - startTime);
		ActionQueue.Instance.Do(
			new ActionSetObjectFieldOrPropertyValue<double>(EditorSong, nameof(EditorSong.SampleLength), sampleLength, true));
	}

	private void JumpToPreviewEnd()
	{
		Editor.SetSongTime(EditorSong.SampleStart + EditorSong.SampleLength);
	}

	private void SetLastSecondHintFromCurrentTime()
	{
		var currentTime = Math.Max(0.0, Editor.GetPosition().SongTime);
		ActionQueue.Instance.Do(
			new ActionSetObjectFieldOrPropertyValue<double>(EditorSong, nameof(EditorSong.LastSecondHint), currentTime, true));
	}

	private void JumpToLastSecondHint()
	{
		Editor.SetSongTime(EditorSong.LastSecondHint);
	}

	private void ApplyItgSongOffset()
	{
		var multiple = new ActionMultiple();
		multiple.EnqueueAndDo(new ActionSetObjectFieldOrPropertyValue<double>(EditorSong, nameof(EditorSong.MusicOffset),
			EditorSong.MusicOffset + SMCommon.ItgOffset, true));
		multiple.EnqueueAndDo(
			new ActionSetObjectFieldOrPropertyValue<double>(EditorSong, nameof(EditorSong.SyncOffset), SMCommon.ItgOffset, true));
		ActionQueue.Instance.EnqueueWithoutDoing(multiple);
	}

	private void SetSyncItg()
	{
		ActionQueue.Instance.Do(
			new ActionSetObjectFieldOrPropertyValue<double>(EditorSong, nameof(EditorSong.SyncOffset), SMCommon.ItgOffset, true));
	}

	private void SetSyncDdr()
	{
		ActionQueue.Instance.Do(
			new ActionSetObjectFieldOrPropertyValue<double>(EditorSong, nameof(EditorSong.SyncOffset), SMCommon.NullOffset,
				true));
	}
}

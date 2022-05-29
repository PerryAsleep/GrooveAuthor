using System;
using System.Collections.Generic;
using System.Text;
using Fumen;
using Fumen.ChartDefinition;
using Fumen.Converters;
using Microsoft.Xna.Framework.Graphics;
using static StepManiaEditor.Utils;

namespace StepManiaEditor
{
	public enum Selectable
	{
		YES,
		NO,
		ROULETTE,
		ES,
		OMES
	}

	public enum DisplayTempoMode
	{
		Random,
		Specified,
		Actual
	}

	/// <summary>
	/// Small class to hold a Texture for a song or chart property that
	/// represents a file path to an image asset.
	/// </summary>
	public class EditorImageData
	{
		private string FileDirectory;
		private EditorTexture Texture;
		private string PathInteral;

		/// <summary>
		/// Path property.
		/// On set, begins an asynchronous load of the image asset specified to the Texture.
		/// </summary>
		public string Path
		{
			get => PathInteral;
			set
			{
				PathInteral = value ?? "";
				if (!string.IsNullOrEmpty(PathInteral))
					Texture?.LoadAsync(Fumen.Path.Combine(FileDirectory, PathInteral));
				else
					Texture?.UnloadAsync();
			}
		}

		/// <summary>
		/// Constructor.
		/// When constructed through this method, no Texture will be used.
		/// </summary>
		public EditorImageData(string path)
		{
			Path = path;
		}

		/// <summary>
		/// Constructor.
		/// When constructed through this method, a Texture will be used and loaded asynchronously
		/// whenever the Path changes.
		/// </summary>
		public EditorImageData(
			string fileDirectory,
			GraphicsDevice graphicsDevice,
			ImGuiRenderer imGuiRenderer,
			uint width,
			uint height,
			string path)
		{
			FileDirectory = fileDirectory;
			Texture = new EditorTexture(graphicsDevice, imGuiRenderer, width, height);
			Path = path;
		}

		public EditorTexture GetTexture()
		{
			return Texture;
		}
	}

	public class EditorChartTimingData
	{
		//song_tag_handlers["STOPS"]= &SetSongStops;
		//song_tag_handlers["DELAYS"]= &SetSongDelays;
		//song_tag_handlers["BPMS"]= &SetSongBPMs;
		//song_tag_handlers["WARPS"]= &SetSongWarps;
		//song_tag_handlers["LABELS"]= &SetSongLabels;
		//song_tag_handlers["TIMESIGNATURES"]= &SetSongTimeSignatures;
		//song_tag_handlers["TICKCOUNTS"]= &SetSongTickCounts;
		//song_tag_handlers["COMBOS"]= &SetSongCombos;
		//song_tag_handlers["SPEEDS"]= &SetSongSpeeds;
		//song_tag_handlers["SCROLLS"]= &SetSongScrolls;
		//song_tag_handlers["FAKES"]= &SetSongFakes;
	}

	public class EditorSong
	{
		private Editor Editor;
		public string FileDirectory;
		public string FileName;

		public string Title;
		public string TitleTransliteration;
		public string Subtitle;
		public string SubtitleTransliteration;
		public string Artist;
		public string ArtistTransliteration;

		public string Genre;
		public string Origin;
		public string Credit;

		public EditorImageData Banner;
		public EditorImageData Background;
		public EditorImageData Jacket;
		public EditorImageData CDImage;
		public EditorImageData DiscImage;
		public EditorImageData CDTitle;

		public string LyricsPath;
		public string PreviewVideoPath;

		private string MusicPathInternal;

		public string MusicPath
		{
			get => MusicPathInternal;
			set
			{
				MusicPathInternal = value ?? "";
				Editor.OnSongMusicChanged();
			}
		}

		private string MusicPreviewPathInternal;

		public string MusicPreviewPath
		{
			get => MusicPreviewPathInternal;
			set
			{
				MusicPreviewPathInternal = value ?? "";
				Editor.OnSongMusicPreviewChanged();
			}
		}

		public double MusicOffset;

		public double
			SyncOffset; // TODO: I want a variable so that you can use the 9ms offset but also have the waveform line up.

		//Intentionally not set.
		//INSTRUMENTTRACK
		//MUSICLENGTH
		//ANIMATIONS
		//BGCHANGES
		//FGCHANGES
		//KEYSOUNDS
		//ATTACKS

		public double LastSecondHint;

		public double SampleStart;
		public double SampleLength;

		public DisplayTempoMode DisplayTempo;
		public double DisplayTempoMin;
		public double DisplayTempoMax;

		public Selectable Selectable = Selectable.YES;
		

		public EditorSong(
			Editor editor,
			string fileDirectory,
			Song song,
			GraphicsDevice graphicsDevice,
			ImGuiRenderer imGuiRenderer)
		{
			Editor = editor;
			FileDirectory = fileDirectory;

			Title = song.Title ?? "";
			TitleTransliteration = song.TitleTransliteration ?? "";
			Subtitle = song.SubTitle ?? "";
			SubtitleTransliteration = song.SubTitleTransliteration ?? "";
			Artist = song.Artist ?? "";
			ArtistTransliteration = song.ArtistTransliteration ?? "";
			Genre = song.Genre ?? "";
			song.Extras.TryGetExtra(SMCommon.TagOrigin, out Origin, true);
			Origin ??= "";
			song.Extras.TryGetExtra(SMCommon.TagCredit, out Credit, true);
			Credit ??= "";

			Banner = new EditorImageData(FileDirectory, graphicsDevice, imGuiRenderer, BannerWidth, BannerHeight,
				song.SongSelectImage);
			string tempStr;
			song.Extras.TryGetExtra(SMCommon.TagBackground, out tempStr, true);
			Background = new EditorImageData(tempStr);
			song.Extras.TryGetExtra(SMCommon.TagJacket, out tempStr, true);
			Jacket = new EditorImageData(tempStr);
			song.Extras.TryGetExtra(SMCommon.TagCDImage, out tempStr, true);
			CDImage = new EditorImageData(tempStr);
			song.Extras.TryGetExtra(SMCommon.TagDiscImage, out tempStr, true);
			DiscImage = new EditorImageData(tempStr);
			song.Extras.TryGetExtra(SMCommon.TagCDTitle, out tempStr, true);
			CDTitle = new EditorImageData(FileDirectory, graphicsDevice, imGuiRenderer, CDTitleWidth, CDTitleHeight, tempStr);

			song.Extras.TryGetExtra(SMCommon.TagLyricsPath, out LyricsPath, true);
			LyricsPath ??= "";
			song.Extras.TryGetExtra(SMCommon.TagPreviewVid, out PreviewVideoPath, true);
			PreviewVideoPath ??= "";

			song.Extras.TryGetExtra(SMCommon.TagMusic, out string musicPath, true);
			MusicPath = musicPath;

			MusicPreviewPath = song.PreviewMusicFile ?? "";
			song.Extras.TryGetExtra(SMCommon.TagOffset, out MusicOffset, true);
			song.Extras.TryGetExtra(SMCommon.TagLastSecondHint, out LastSecondHint, true);
			if (LastSecondHint <= 0.0)
			{
				// TODO: When the last beat hint is set we need to use the song's timing data
				// to determine the last second hint.
				if (song.Extras.TryGetExtra(SMCommon.TagLastBeatHint, out double lastBeatHint, true))
				{
				}
			}

			song.Extras.TryGetExtra(SMCommon.TagSampleStart, out SampleStart, true);
			song.Extras.TryGetExtra(SMCommon.TagSampleLength, out SampleLength, true);

			DisplayTempo = DisplayTempoMode.Actual;
			song.Extras.TryGetExtra(SMCommon.TagDisplayBPM, out string displayTempoString, true);
			if (!string.IsNullOrEmpty(displayTempoString))
			{
				var parsed = false;
				if (displayTempoString == "*")
				{
					parsed = true;
					DisplayTempo = DisplayTempoMode.Random;
				}
				else
				{
					var parts = displayTempoString.Split(MSDFile.ParamMarker);
					if (parts.Length == 1)
					{
						if (double.TryParse(parts[0], out DisplayTempoMin))
						{
							parsed = true;
							DisplayTempoMax = DisplayTempoMin;
							DisplayTempo = DisplayTempoMode.Specified;
						}
					}
					else if (parts.Length == 2)
					{
						if (double.TryParse(parts[0], out DisplayTempoMin) && double.TryParse(parts[1], out DisplayTempoMax))
						{
							parsed = true;
							DisplayTempo = DisplayTempoMode.Specified;
						}
					}
				}

				if (!parsed)
				{
					Logger.Warn($"Failed to parse Song {SMCommon.TagDisplayBPM} value: '{displayTempoString}'.");
				}
			}

			song.Extras.TryGetExtra(SMCommon.TagSelectable, out string selectableString, true);
			if (!string.IsNullOrEmpty(selectableString))
			{
				if (!Enum.TryParse(selectableString, true, out Selectable))
				{
					Selectable = Selectable.YES;
					Logger.Warn($"Failed to parse Song {SMCommon.TagSelectable} value: '{selectableString}'.");
				}
			}

			//foreach (var chart in song.Charts)
			//{
			//	Charts.Add(chart.Type, new EditorChart(chart));
			//}
		}

		public Song SaveToSong()
		{
			Song song = new Song();
			song.Title = Title;
			song.TitleTransliteration = TitleTransliteration;
			song.SubTitle = Subtitle;
			song.SubTitleTransliteration = SubtitleTransliteration;
			song.Artist = Artist;
			song.ArtistTransliteration = ArtistTransliteration;
			song.Genre = Genre;
			song.Extras.AddSourceExtra(SMCommon.TagOrigin, Origin);
			song.Extras.AddSourceExtra(SMCommon.TagCredit, Credit);
			song.SongSelectImage = Banner.Path;
			song.Extras.AddSourceExtra(SMCommon.TagBackground, Background.Path);
			song.Extras.AddSourceExtra(SMCommon.TagJacket, Jacket.Path);
			song.Extras.AddSourceExtra(SMCommon.TagCDImage, CDImage.Path);
			song.Extras.AddSourceExtra(SMCommon.TagDiscImage, DiscImage.Path);
			song.Extras.AddSourceExtra(SMCommon.TagCDTitle, CDTitle.Path);
			song.Extras.AddSourceExtra(SMCommon.TagLyricsPath, LyricsPath);
			song.Extras.AddSourceExtra(SMCommon.TagPreviewVid, PreviewVideoPath);

			song.Extras.AddSourceExtra(SMCommon.TagMusic, MusicPath);
			song.PreviewMusicFile = MusicPreviewPath;
			song.Extras.AddSourceExtra(SMCommon.TagOffset, MusicOffset);
			song.Extras.AddSourceExtra(SMCommon.TagLastSecondHint, LastSecondHint);
			song.Extras.AddSourceExtra(SMCommon.TagSampleStart, SampleStart);
			song.Extras.AddSourceExtra(SMCommon.TagSampleLength, SampleLength);

			switch (DisplayTempo)
			{
				case DisplayTempoMode.Random:
					song.Extras.AddSourceExtra(SMCommon.TagDisplayBPM, "*");
					break;
				case DisplayTempoMode.Specified:
					if (DisplayTempoMin != DisplayTempoMax)
						song.Extras.AddSourceExtra(SMCommon.TagDisplayBPM,
							$"{DisplayTempoMin:SMDoubleFormat}:{DisplayTempoMax:SMDoubleFormat}");
					else
						song.Extras.AddSourceExtra(SMCommon.TagDisplayBPM, $"{DisplayTempoMin:SMDoubleFormat}");
					break;
				case DisplayTempoMode.Actual:
					song.Extras.AddSourceExtra(SMCommon.TagDisplayBPM, "");
					break;
			}

			song.Extras.AddSourceExtra(SMCommon.TagSelectable, Selectable.ToString());

			return song;
		}
	}

	public class EditorChart
	{
		public EditorChart(Chart chart)
		{

		}
	}
}

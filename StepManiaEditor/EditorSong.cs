using System;
using System.Collections.Generic;
using System.Linq;
using Fumen;
using Fumen.ChartDefinition;
using Fumen.Converters;
using Microsoft.Xna.Framework.Graphics;
using static StepManiaEditor.Utils;
using static Fumen.Utils;

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
		private string PathInternal = "";

		/// <summary>
		/// Path property.
		/// On set, begins an asynchronous load of the image asset specified to the Texture.
		/// </summary>
		public string Path
		{
			get => PathInternal;
			set
			{
				var newValue = value ?? "";
				if (PathInternal == newValue)
					return;

				PathInternal = newValue;
				if (!string.IsNullOrEmpty(PathInternal))
					Texture?.LoadAsync(Fumen.Path.Combine(FileDirectory, PathInternal));
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

	public class DisplayTempo
	{
		public DisplayTempoMode Mode;
		public double SpecifiedTempoMin;
		public double SpecifiedTempoMax;

		// Not serialized. Used for UI controls to avoid having to enter both a min and a max
		// when just wanted one tempo.
		public bool ShouldAllowEditsOfMax = true;

		public DisplayTempo()
		{

		}

		public DisplayTempo(DisplayTempoMode mode, double min, double max)
		{
			Mode = mode;
			SpecifiedTempoMin = min;
			SpecifiedTempoMax = max;
			ShouldAllowEditsOfMax = !SpecifiedTempoMin.DoubleEquals(SpecifiedTempoMax);
		}

		public DisplayTempo(DisplayTempo other)
		{
			Mode = other.Mode;
			SpecifiedTempoMin = other.SpecifiedTempoMin;
			SpecifiedTempoMax = other.SpecifiedTempoMax;
			ShouldAllowEditsOfMax = other.ShouldAllowEditsOfMax;
		}

		public void FromString(string displayTempoString)
		{
			Mode = DisplayTempoMode.Actual;
			SpecifiedTempoMin = 0.0;
			SpecifiedTempoMax = 0.0;

			if (string.IsNullOrEmpty(displayTempoString))
				return;
			
			var parsed = false;
			if (displayTempoString == "*")
			{
				parsed = true;
				Mode = DisplayTempoMode.Random;
			}
			else
			{
				var parts = displayTempoString.Split(MSDFile.ParamMarker);
				if (parts.Length == 1)
				{
					if (double.TryParse(parts[0], out SpecifiedTempoMin))
					{
						parsed = true;
						SpecifiedTempoMax = SpecifiedTempoMin;
						Mode = DisplayTempoMode.Specified;
						ShouldAllowEditsOfMax = false;
					}
				}
				else if (parts.Length == 2)
				{
					if (double.TryParse(parts[0], out SpecifiedTempoMin) && double.TryParse(parts[1], out SpecifiedTempoMax))
					{
						parsed = true;
						Mode = DisplayTempoMode.Specified;
						ShouldAllowEditsOfMax = !SpecifiedTempoMin.DoubleEquals(SpecifiedTempoMax);
					}
				}
			}

			if (!parsed)
			{
				Logger.Warn($"Failed to parse {SMCommon.TagDisplayBPM} value: '{displayTempoString}'.");
			}
		}

		public override string ToString()
		{
			switch (Mode)
			{
				case DisplayTempoMode.Random:
					return "*";
				case DisplayTempoMode.Specified:
					if (!SpecifiedTempoMin.DoubleEquals(SpecifiedTempoMax))
						return $"{SpecifiedTempoMin.ToString(SMCommon.SMDoubleFormat)}:{SpecifiedTempoMax.ToString(SMCommon.SMDoubleFormat)}";
					return SpecifiedTempoMin.ToString(SMCommon.SMDoubleFormat);
				case DisplayTempoMode.Actual:
					return "";
			}
			return "";
		}
	}

	public class EditorSong
	{
		private Editor Editor;

		private Extras OriginalSongExtras;

		public Dictionary<SMCommon.ChartType, List<EditorChart>> Charts = new Dictionary<SMCommon.ChartType, List<EditorChart>>();
		public List<Chart> UnsupportedCharts = new List<Chart>();

		public string FileDirectory;
		public string FileName;
		public string FileFullPath;
		public FileFormat FileFormat;

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

		private double MusicOffsetInternal;
		public double MusicOffset
		{
			get => MusicOffsetInternal;
			set
			{
				MusicOffsetInternal = value;
				Editor.OnMusicOffsetChanged();
			}
		}

		public double SyncOffset; // TODO: I want a variable so that you can use the 9ms offset but also have the waveform line up.

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

		public Selectable Selectable = Selectable.YES;


		public EditorSong(
			Editor editor,
			GraphicsDevice graphicsDevice,
			ImGuiRenderer imGuiRenderer)
		{
			Editor = editor;
		}

		public EditorSong(
			Editor editor,
			string fullFilePath,
			Song song,
			GraphicsDevice graphicsDevice,
			ImGuiRenderer imGuiRenderer)
		{
			Editor = editor;

			SetFullFilePath(fullFilePath);
			
			OriginalSongExtras = song.Extras;

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
			song.Extras.TryGetExtra(SMCommon.TagOffset, out double musicOffset, true);
			MusicOffset = musicOffset;
			song.Extras.TryGetExtra(SMCommon.TagLastSecondHint, out LastSecondHint, true);
			if (LastSecondHint <= 0.0)
			{
				// TODO: When the last beat hint is set we need to use the song's timing data
				// to determine the last second hint.
				if (song.Extras.TryGetExtra(SMCommon.TagLastBeatHint, out double lastBeatHint, true))
				{
				}
			}

			SampleStart = song.PreviewSampleStart;
			SampleLength = song.PreviewSampleLength;

			var hasDisplayTempo = song.Extras.TryGetExtra(SMCommon.TagDisplayBPM, out object _, true);
			DisplayTempo displayTempo = null;
			if (hasDisplayTempo)
			{
				displayTempo = new DisplayTempo();
				displayTempo.FromString(SMCommon.GetDisplayBPMStringFromSourceExtrasList(song.Extras, null));
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

			foreach (var chart in song.Charts)
			{
				if (!Editor.IsChartSupported(chart))
				{
					UnsupportedCharts.Add(chart);
					continue;
				}

				var editorChart = new EditorChart(Editor, this, chart);
				if (!Charts.ContainsKey(editorChart.ChartType))
					Charts.Add(editorChart.ChartType, new List<EditorChart>());
				Charts[editorChart.ChartType].Add(editorChart);

				if (hasDisplayTempo && !editorChart.HasDisplayTempoFromChart)
				{
					editorChart.CopyDisplayTempo(displayTempo);
				}
			}
		}

		public void SetFullFilePath(string fullFilePath)
		{
			FileFullPath = fullFilePath;
			FileName = System.IO.Path.GetFileName(fullFilePath);
			FileDirectory = System.IO.Path.GetDirectoryName(fullFilePath);
			FileFormat = FileFormat.GetFileFormatByExtension(System.IO.Path.GetExtension(fullFilePath));
		}

		public Song SaveToSong()
		{
			Song song = new Song();

			song.Extras = new Extras(OriginalSongExtras);
			// TODO: Remove timing data?

			song.Title = Title;
			song.TitleTransliteration = TitleTransliteration;
			song.SubTitle = Subtitle;
			song.SubTitleTransliteration = SubtitleTransliteration;
			song.Artist = Artist;
			song.ArtistTransliteration = ArtistTransliteration;
			song.Genre = Genre;
			song.Extras.AddDestExtra(SMCommon.TagOrigin, Origin, true);
			song.Extras.AddDestExtra(SMCommon.TagCredit, Credit, true);
			song.SongSelectImage = Banner.Path;
			song.Extras.AddDestExtra(SMCommon.TagBackground, Background.Path, true);
			song.Extras.AddDestExtra(SMCommon.TagJacket, Jacket.Path, true);
			song.Extras.AddDestExtra(SMCommon.TagCDImage, CDImage.Path, true);
			song.Extras.AddDestExtra(SMCommon.TagDiscImage, DiscImage.Path, true);
			song.Extras.AddDestExtra(SMCommon.TagCDTitle, CDTitle.Path, true);
			song.Extras.AddDestExtra(SMCommon.TagLyricsPath, LyricsPath, true);
			song.Extras.AddDestExtra(SMCommon.TagPreviewVid, PreviewVideoPath, true);

			song.Extras.AddDestExtra(SMCommon.TagMusic, MusicPath, true);
			song.PreviewMusicFile = MusicPreviewPath;
			song.Extras.AddDestExtra(SMCommon.TagOffset, MusicOffset, true);
			song.Extras.AddDestExtra(SMCommon.TagLastSecondHint, LastSecondHint, true);
			song.PreviewSampleStart = SampleStart;
			song.PreviewSampleLength = SampleLength;

			// Do not add the display BPM.
			// We want to use the charts' display BPMs.
			//song.Extras.AddDestExtra(SMCommon.TagDisplayBPM, DisplayTempo.ToString(), true);

			song.Extras.AddDestExtra(SMCommon.TagSelectable, Selectable.ToString(), true);

			foreach (var editorChartsForChartType in Charts)
			{
				foreach (var editorChart in editorChartsForChartType.Value)
				{
					song.Charts.Add(editorChart.SaveToChart());
				}
			}
			foreach (var unsupportedChart in UnsupportedCharts)
			{
				song.Charts.Add(unsupportedChart);
			}

			return song;
		}
	}

	public class EditorChart
	{
		private Editor Editor;
		private Extras OriginalChartExtras;

		public EditorSong EditorSong;

		public SMCommon.ChartType ChartType;
		public SMCommon.ChartDifficultyType ChartDifficultyType;
		public int Rating;
		public string Name;
		public string Description;
		public string Style;
		public string Credit;

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

		public bool UsesChartMusicOffsetInternal;
		public bool UsesChartMusicOffset
		{
			get => UsesChartMusicOffsetInternal;
			set
			{
				UsesChartMusicOffsetInternal = value;
				Editor.OnMusicOffsetChanged();
			}
		}

		private double MusicOffsetInternal;
		public double MusicOffset
		{
			get => MusicOffsetInternal;
			set
			{
				MusicOffsetInternal = value;
				Editor.OnMusicOffsetChanged();
			}
		}

		public bool HasDisplayTempoFromChart;
		public DisplayTempo DisplayTempo = new DisplayTempo();

		// TODO: RADARVALUES?

		public RedBlackTree<EditorEvent> EditorEvents;

		public RedBlackTree<EditorRateAlteringEvent> RateAlteringEventsBySongTime;
		public RedBlackTree<EditorRateAlteringEvent> RateAlteringEventsByRow;
		public RedBlackTree<EditorInterpolatedRateAlteringEvent> InterpolatedScrollRateEvents;

		public double MostCommonTempo;
		public double MinTempo;
		public double MaxTempo;

		public readonly int NumInputs;
		public readonly int NumPlayers;

		public EditorChart(Editor editor, EditorSong editorSong, Chart chart)
		{
			Editor = editor;
			OriginalChartExtras = chart.Extras;
			EditorSong = editorSong;

			SMCommon.TryGetChartType(chart.Type, out ChartType);
			Enum.TryParse(chart.DifficultyType, out ChartDifficultyType);
			Rating = (int)chart.DifficultyRating;

			NumInputs = SMCommon.Properties[(int)ChartType].NumInputs;
			NumPlayers = SMCommon.Properties[(int)ChartType].NumPlayers;

			chart.Extras.TryGetExtra(SMCommon.TagChartName, out Name, true);
			Name ??= "";
			Description = chart.Description ?? "";
			chart.Extras.TryGetExtra(SMCommon.TagChartStyle, out Style, true);	// Pad or Keyboard
			Style ??= "";
			Credit = chart.Author ?? "";
			chart.Extras.TryGetExtra(SMCommon.TagMusic, out string musicPath, true);
			MusicPath = musicPath;
			UsesChartMusicOffset = chart.Extras.TryGetExtra(SMCommon.TagOffset, out double musicOffset, true);
			if (UsesChartMusicOffset)
				MusicOffset = musicOffset;

			HasDisplayTempoFromChart = !string.IsNullOrEmpty(chart.Tempo);
			DisplayTempo.FromString(chart.Tempo);

			// TODO: I wonder if there is an optimization to not do all the tree parsing for inactive charts.
			SetUpEditorEvents(chart);
		}

		public Chart SaveToChart()
		{
			Chart chart = new Chart();
			chart.Extras = new Extras(OriginalChartExtras);

			chart.Type = SMCommon.ChartTypeString(ChartType);
			chart.DifficultyType = ChartDifficultyType.ToString();
			chart.NumInputs = NumInputs;
			chart.NumPlayers = NumPlayers;
			chart.DifficultyRating = Rating;
			chart.Extras.AddDestExtra(SMCommon.TagChartName, Name);
			chart.Description = Description;
			chart.Extras.AddDestExtra(SMCommon.TagChartStyle, Style);
			chart.Author = Credit;
			chart.Extras.AddDestExtra(SMCommon.TagMusic, MusicPath);
			if (UsesChartMusicOffset)
				chart.Extras.AddDestExtra(SMCommon.TagOffset, MusicOffset);
			//TODO: Else use song?
			chart.Tempo = DisplayTempo.ToString();

			var layer = new Layer();
			foreach (var editorEvent in EditorEvents)
			{
				layer.Events.Add(editorEvent.GetEvent());
			}
			layer.Events.Sort(new SMCommon.SMEventComparer());
			chart.Layers.Add(layer);

			return chart;
		}

		public void CopyDisplayTempo(DisplayTempo displayTempo)
		{
			DisplayTempo = new DisplayTempo(displayTempo);
		}

		private void SetUpEditorEvents(Chart chart)
		{
			var editorEvents = new RedBlackTree<EditorEvent>();
			var rateAlteringEventsBySongTime = new RedBlackTree<EditorRateAlteringEvent>(EditorRateAlteringEvent.SortSongTime());
			var rateAlteringEventsByRow = new RedBlackTree<EditorRateAlteringEvent>(EditorRateAlteringEvent.SortRow());
			var interpolatedScrollRateEvents = new RedBlackTree<EditorInterpolatedRateAlteringEvent>();

			var lastHoldStarts = new EditorHoldStartNoteEvent[NumInputs];
			var lastScrollRateInterpolationValue = 1.0;
			var firstInterpolatedScrollRate = true;
			var firstTick = true;
			var firstMultipliersEvent = true;

			for (var eventIndex = 0; eventIndex < chart.Layers[0].Events.Count; eventIndex++)
			{
				var chartEvent = chart.Layers[0].Events[eventIndex];

				var editorEvent = EditorEvent.CreateEvent(this, chartEvent);
				if (editorEvent != null)
					editorEvents.Insert(editorEvent);

				if (editorEvent is EditorHoldStartNoteEvent hs)
				{
					lastHoldStarts[hs.GetLane()] = hs;
				}
				else if (editorEvent is EditorHoldEndNoteEvent he)
				{
					var start = lastHoldStarts[he.GetLane()];
					he.SetHoldStartNote(start);
					start.SetHoldEndNote(he);
				}
				else if (editorEvent is EditorRateAlteringEvent rae)
				{
					rateAlteringEventsBySongTime.Insert(rae);
					rateAlteringEventsByRow.Insert(rae);
				}
				else if (editorEvent is EditorInterpolatedRateAlteringEvent irae)
				{
					if (chartEvent is ScrollRateInterpolation scrollRateInterpolation)
					{
						irae.Row = scrollRateInterpolation.IntegerPosition;
						irae.SongTime = scrollRateInterpolation.TimeMicros;
						irae.PreviousScrollRate = lastScrollRateInterpolationValue;
						irae.CanBeDeleted = !firstInterpolatedScrollRate;
						interpolatedScrollRateEvents.Insert(irae);
						lastScrollRateInterpolationValue = scrollRateInterpolation.Rate;

						firstInterpolatedScrollRate = false;
					}
				}
				else if (editorEvent is EditorTickCountEvent tce)
				{
					tce.CanBeDeleted = !firstTick;
					firstTick = false;
				}
				else if (editorEvent is EditorMultipliersEvent me)
				{
					me.CanBeDeleted = !firstMultipliersEvent;
					firstMultipliersEvent = false;
				}
			}

			EditorEvents = editorEvents;
			RateAlteringEventsBySongTime = rateAlteringEventsBySongTime;
			RateAlteringEventsByRow = rateAlteringEventsByRow;
			InterpolatedScrollRateEvents = interpolatedScrollRateEvents;
			
			CleanRateAlteringEvents();
		}

		private void CleanRateAlteringEvents()
		{
			var lastScrollRate = 1.0;
			var lastTempo = 1.0;
			var firstTempo = true;
			var firstTimeSignature = true;
			var firstScrollRate = true;
			TimeSignature lastTimeSignature = null;
			var timePerTempo = new Dictionary<double, long>();
			var lastTempoChangeTime = 0L;
			var minTempo = double.MaxValue;
			var maxTempo = double.MinValue;

			EditorRateAlteringEvent previousEvent = new EditorDummyRateAlteringEvent(this, null)
			{
				Row = 0,
				SongTime = 0.0,
				RowsPerSecond = 0.0,
				SecondsPerRow = 0.0
			};

			List<EditorRateAlteringEvent> previousEvents = new List<EditorRateAlteringEvent>();

			foreach (var rae in RateAlteringEventsByRow)
			{
				var chartEvent = rae.GetEvent();
				if (chartEvent is Tempo tc)
				{
					var rowsSincePrevious = chartEvent.IntegerPosition - previousEvent.Row;
					var secondsSincePrevious = rowsSincePrevious * previousEvent.SecondsPerRow;

					var newSecondsPerRow = 60.0 / tc.TempoBPM / (double)SMCommon.MaxValidDenominator;
					var newRowsPerSecond = 1.0 / newSecondsPerRow;

					// Update any events which precede the first tempo so they can have accurate rates.
					// This is useful for determining spacing prior to the first event
					if (firstTempo)
					{
						foreach (var previousRateAlteringEvent in previousEvents)
						{
							previousRateAlteringEvent.RowsPerSecond = newRowsPerSecond;
							previousRateAlteringEvent.SecondsPerRow = newSecondsPerRow;
							previousRateAlteringEvent.Tempo = tc.TempoBPM;
						}
					}

					minTempo = Math.Min(minTempo, tc.TempoBPM);
					maxTempo = Math.Max(maxTempo, tc.TempoBPM);

					rae.Row = chartEvent.IntegerPosition;
					rae.RowForFollowingEvents = chartEvent.IntegerPosition;
					rae.SongTime = previousEvent.SongTimeForFollowingEvents + secondsSincePrevious;
					rae.SongTimeForFollowingEvents = previousEvent.SongTimeForFollowingEvents + secondsSincePrevious;
					rae.RowsPerSecond = newRowsPerSecond;
					rae.SecondsPerRow = newSecondsPerRow;
					rae.ScrollRate = lastScrollRate;
					rae.Tempo = tc.TempoBPM;
					rae.LastTimeSignature = lastTimeSignature;
					rae.CanBeDeleted = !firstTempo;

					if (!firstTempo)
					{
						timePerTempo.TryGetValue(lastTempo, out var currentTempoTime);
						timePerTempo[lastTempo] = currentTempoTime + tc.TimeMicros - lastTempoChangeTime;
						lastTempoChangeTime = tc.TimeMicros;
					}

					previousEvent = rae;
					lastTempo = tc.TempoBPM;
					firstTempo = false;
				}
				else if (chartEvent is Stop stop)
				{
					var rowsSincePrevious = chartEvent.IntegerPosition - previousEvent.Row;
					var secondsSincePrevious = rowsSincePrevious * previousEvent.SecondsPerRow;

					rae.Row = chartEvent.IntegerPosition;
					rae.RowForFollowingEvents = chartEvent.IntegerPosition;
					rae.SongTime = previousEvent.SongTimeForFollowingEvents + secondsSincePrevious;
					rae.SongTimeForFollowingEvents = previousEvent.SongTimeForFollowingEvents + secondsSincePrevious +
													 ToSeconds(stop.LengthMicros);
					rae.RowsPerSecond = previousEvent.RowsPerSecond;
					rae.SecondsPerRow = previousEvent.SecondsPerRow;
					rae.ScrollRate = lastScrollRate;
					rae.Tempo = lastTempo;
					rae.LastTimeSignature = lastTimeSignature;
					rae.CanBeDeleted = true;

					previousEvent = rae;
				}
				else if (chartEvent is Warp warp)
				{
					var rowsSincePrevious = chartEvent.IntegerPosition - previousEvent.Row;
					var secondsSincePrevious = rowsSincePrevious * previousEvent.SecondsPerRow;

					rae.Row = chartEvent.IntegerPosition;
					rae.RowForFollowingEvents = chartEvent.IntegerPosition + warp.LengthIntegerPosition;
					rae.SongTime = previousEvent.SongTimeForFollowingEvents + secondsSincePrevious;
					rae.SongTimeForFollowingEvents = previousEvent.SongTimeForFollowingEvents + secondsSincePrevious;
					rae.RowsPerSecond = previousEvent.RowsPerSecond;
					rae.SecondsPerRow = previousEvent.SecondsPerRow;
					rae.ScrollRate = lastScrollRate;
					rae.Tempo = lastTempo;
					rae.LastTimeSignature = lastTimeSignature;
					rae.CanBeDeleted = true;

					previousEvent = rae;
				}
				else if (chartEvent is ScrollRate scrollRate)
				{
					var rowsSincePrevious = chartEvent.IntegerPosition - previousEvent.Row;
					var secondsSincePrevious = rowsSincePrevious * previousEvent.SecondsPerRow;

					// Update any events which precede the first tempo so they can have accurate rates.
					// This is useful for determining spacing prior to the first event
					if (firstScrollRate)
					{
						foreach (var previousRateAlteringEvent in previousEvents)
						{
							previousRateAlteringEvent.ScrollRate = scrollRate.Rate;
						}
					}

					rae.Row = chartEvent.IntegerPosition;
					rae.RowForFollowingEvents = chartEvent.IntegerPosition;
					rae.SongTime = previousEvent.SongTimeForFollowingEvents + secondsSincePrevious;
					rae.SongTimeForFollowingEvents = previousEvent.SongTimeForFollowingEvents + secondsSincePrevious;
					rae.RowsPerSecond = previousEvent.RowsPerSecond;
					rae.SecondsPerRow = previousEvent.SecondsPerRow;
					rae.ScrollRate = scrollRate.Rate;
					rae.Tempo = lastTempo;
					rae.LastTimeSignature = lastTimeSignature;
					rae.CanBeDeleted = !firstScrollRate;

					previousEvent = rae;
					lastScrollRate = scrollRate.Rate;
					firstScrollRate = false;
				}
				else if (chartEvent is TimeSignature timeSignature)
				{
					var rowsSincePrevious = chartEvent.IntegerPosition - previousEvent.Row;
					var secondsSincePrevious = rowsSincePrevious * previousEvent.SecondsPerRow;

					rae.Row = chartEvent.IntegerPosition;
					rae.RowForFollowingEvents = chartEvent.IntegerPosition;
					rae.SongTime = previousEvent.SongTimeForFollowingEvents + secondsSincePrevious;
					rae.SongTimeForFollowingEvents = previousEvent.SongTimeForFollowingEvents + secondsSincePrevious;
					rae.RowsPerSecond = previousEvent.RowsPerSecond;
					rae.SecondsPerRow = previousEvent.SecondsPerRow;
					rae.ScrollRate = lastScrollRate;
					rae.Tempo = lastTempo;
					rae.LastTimeSignature = timeSignature;
					rae.CanBeDeleted = !firstTimeSignature;

					lastTimeSignature = timeSignature;
					firstTimeSignature = false;
				}

				previousEvents.Add(rae);
			}

			if (previousEvent.GetEvent() != null)
			{
				timePerTempo.TryGetValue(lastTempo, out var lastTempoTime);
				timePerTempo[lastTempo] = lastTempoTime + previousEvent.GetEvent().TimeMicros - lastTempoChangeTime;
			}

			var longestTempoTime = -1L;
			var mostCommonTempo = 0.0;
			foreach (var kvp in timePerTempo)
			{
				if (kvp.Value > longestTempoTime)
				{
					longestTempoTime = kvp.Value;
					mostCommonTempo = kvp.Key;
				}
			}

			MostCommonTempo = mostCommonTempo;
			MinTempo = minTempo;
			MaxTempo = maxTempo;
		}

		private void UpdateNotePositions()
		{
			SMCommon.SetEventTimeMicrosAndMetricPositionsFromRows(EditorEvents.Select(e => e.GetEvent()));
		}

		public bool TryGetChartPositionFromTime(double chartTime, ref double chartPosition)
		{
			var rateEvent = GetActiveRateAlteringEventForTime(chartTime, false);
			if (rateEvent == null)
				return false;
			
			if (chartTime >= rateEvent.SongTime && chartTime < rateEvent.SongTimeForFollowingEvents)
				chartPosition = rateEvent.Row;
			else
				chartPosition = rateEvent.Row + rateEvent.RowsPerSecond * (chartTime - rateEvent.SongTimeForFollowingEvents);
			return true;
		}

		public EditorRateAlteringEvent GetActiveRateAlteringEventForTime(double chartTime, bool allowEqualTo = true)
		{
			if (RateAlteringEventsBySongTime == null)
				return null;

			// Given the current song time, get the greatest preceding event which alters the rate of rows to time.
			var enumerator = RateAlteringEventsBySongTime.FindGreatestPreceding(
				new EditorDummyRateAlteringEvent(this, null) { SongTime = chartTime },
				allowEqualTo);
			// If there is no preceding event (e.g. SongTime is negative), use the first event.
			if (enumerator == null)
				enumerator = RateAlteringEventsBySongTime.GetRedBlackTreeEnumerator();
			// If there is still no event then the Chart is misconfigured as it must have at least a Tempo event.
			if (enumerator == null)
				return null;

			// Update the ChartPosition based on the cached rate information.
			enumerator.MoveNext();
			return enumerator.Current;
		}

		public bool TryGetTimeFromChartPosition(double chartPosition, ref double chartTime)
		{
			var rateEvent = GetActiveRateAlteringEventForPosition(chartPosition, false);
			if (rateEvent == null)
				return false;
			chartTime = rateEvent.SongTimeForFollowingEvents + rateEvent.SecondsPerRow * (chartPosition - rateEvent.Row);
			return true;
		}

		public EditorRateAlteringEvent GetActiveRateAlteringEventForPosition(double chartPosition, bool allowEqualTo = true)
		{
			if (RateAlteringEventsByRow == null)
				return null;

			// Given the current song time, get the greatest preceding event which alters the rate of rows to time.
			var enumerator = RateAlteringEventsByRow.FindGreatestPreceding(
				new EditorDummyRateAlteringEvent(this, null) { Row = chartPosition },
				allowEqualTo);
			// If there is no preceding event (e.g. ChartPosition is negative), use the first event.
			if (enumerator == null)
				enumerator = RateAlteringEventsByRow.GetRedBlackTreeEnumerator();
			// If there is still no event then the Chart is misconfigured as it must have at least a Tempo event.
			if (enumerator == null)
				return null;

			enumerator.MoveNext();
			return enumerator.Current;
		}

		public EditorEvent FindNoteAt(int row, int lane, bool ignoreNotesBeingEdited)
		{
			var pos = new EditorTapNoteEvent(this, new LaneTapNote
			{
				Lane = lane,
				IntegerPosition = row
			});

			// Find the greatest preceding event, including events equal to the given position.
			var best = EditorEvents.FindGreatestPreceding(pos, true);
			if (best == null)
				return null;
			
			// Scan forward to the last note in the row to make sure we consider all notes this row.
			while (best.MoveNext())
			{
				if (best.Current.GetRow() > row)
				{
					best.MovePrev();
					break;
				}
			}
			if (best.Current == null)
				best.MovePrev();

			// Scan backwards finding a note in the given lane and row, or a hold
			// which starts before the given now but ends at or after it.
			do
			{
				if (best.Current.GetLane() != lane)
					continue;
				if (ignoreNotesBeingEdited && best.Current.IsBeingEdited())
					continue;
				if (best.Current.GetRow() == row)
					return best.Current;
				if (!(best.Current is EditorHoldStartNoteEvent hsn))
					return null;
				return hsn.GetHoldEndNote().GetRow() >= row ? best.Current : null;
			} while (best.MovePrev());

			return null;
		}

		/// <summary>
		/// Given a chart position, returns the previous note in every lane where the note is either
		/// a tap, hold start, or hold end.
		/// </summary>
		public EditorEvent[] GetPreviousInputNotes(double chartPosition)
		{
			var nextNotes = new EditorEvent[NumInputs];
			var numFound = 0;

			// Get an enumerator to the next note.
			var pos = new EditorTapNoteEvent(this, new LaneTapNote
			{
				Lane = 0,
				IntegerPosition = (int)chartPosition
			});
			var enumerator = EditorEvents.FindLeastFollowing(pos, true);
			if (enumerator == null)
				enumerator = EditorEvents.FindGreatestPreceding(pos);
			if (enumerator == null)
				return nextNotes;

			// Scan backwards until we have collected every next note.
			while (enumerator.MovePrev())
			{
				var c = enumerator.Current;
				if (c.GetRow() >= chartPosition || nextNotes[c.GetLane()] != null)
				{
					continue;
				}

				if (c.GetEvent() is LaneTapNote
				    || c.GetEvent() is LaneHoldStartNote
				    || c.GetEvent() is LaneHoldEndNote)
				{
					nextNotes[c.GetLane()] = c;
					numFound++;
					if (numFound == NumInputs)
						break;
				}
			}

			return nextNotes;
		}

		/// <summary>
		/// Given a chart position, returns the next note in every lane where the note is either
		/// a tap, hold start, or hold end.
		/// </summary>
		public EditorEvent[] GetNextInputNotes(double chartPosition)
		{
			var nextNotes = new EditorEvent[NumInputs];
			var numFound = 0;

			// Get an enumerator to the next note.
			var pos = new EditorTapNoteEvent(this, new LaneTapNote
			{
				Lane = 0,
				IntegerPosition = (int)chartPosition
			});
			var enumerator = EditorEvents.FindGreatestPreceding(pos, true);
			if (enumerator == null)
				enumerator = EditorEvents.FindLeastFollowing(pos);
			if (enumerator == null)
				return nextNotes;

			// Scan forward until we have collected every next note.
			while (enumerator.MoveNext())
			{
				var c = enumerator.Current;
				if (c.GetRow() <= chartPosition || c.GetLane() < 0 || nextNotes[c.GetLane()] != null)
				{
					continue;
				}

				if (c.GetEvent() is LaneTapNote
				    || c.GetEvent() is LaneHoldStartNote
				    || c.GetEvent() is LaneHoldEndNote)
				{
					nextNotes[c.GetLane()] = c;
					numFound++;
					if (numFound == NumInputs)
						break;
				}
			}

			return nextNotes;
		}

		public void OnRateAlteringEventModified(EditorRateAlteringEvent rae)
		{
			// TODO: Can this be optimized?
			CleanRateAlteringEvents();
			UpdateNotePositions();
		}

		public void OnInterpolatedRateAlteringEventModified(EditorInterpolatedRateAlteringEvent irae)
		{
			var e = InterpolatedScrollRateEvents.Find(irae);
			if (e != null)
			{
				e.MoveNext();
				if (e.MoveNext())
				{
					var next = e.Current;
					next.PreviousScrollRate = irae.ScrollRateInterpolationEvent.Rate;
				}
			}
		}

		public void DeleteEvent(EditorEvent editorEvent)
		{
			EditorEvents.Delete(editorEvent);

			if (editorEvent is EditorRateAlteringEvent rae)
			{
				RateAlteringEventsByRow.Delete(rae);
				RateAlteringEventsBySongTime.Delete(rae);
				// TODO: Can this be optimized?
				CleanRateAlteringEvents();
				UpdateNotePositions();
			}

			else if (editorEvent is EditorInterpolatedRateAlteringEvent irae)
			{
				var e = InterpolatedScrollRateEvents.Find(irae);
				if (e != null)
				{
					e.MoveNext();
					if (e.MoveNext())
					{
						var next = e.Current;
						if (e.MovePrev())
						{
							if (e.MovePrev())
							{
								var prev = e.Current;
								next.PreviousScrollRate = prev.ScrollRateInterpolationEvent.Rate;
							}
							e.MoveNext();
						}
						e.MoveNext();
					}
					e.MovePrev();
					e.Delete();
				}
			}
		}

		public void AddEvent(EditorEvent editorEvent)
		{
			EditorEvents.Insert(editorEvent);

			if (editorEvent is EditorRateAlteringEvent rae)
			{
				RateAlteringEventsByRow.Insert(rae);
				RateAlteringEventsBySongTime.Insert(rae);
				// TODO: Can this be optimized?
				CleanRateAlteringEvents();
				UpdateNotePositions();
			}

			else if (editorEvent is EditorInterpolatedRateAlteringEvent irae)
			{
				var e = InterpolatedScrollRateEvents.Insert(irae);
				if (e != null)
				{
					e.MoveNext();
					if (e.MoveNext())
					{
						var next = e.Current;
						next.PreviousScrollRate = irae.ScrollRateInterpolationEvent.Rate;
						if (e.MovePrev())
						{
							if (e.MovePrev())
							{
								var prev = e.Current;
								irae.PreviousScrollRate = prev.ScrollRateInterpolationEvent.Rate;
							}
						}
					}
				}
			}
		}

		public double GetStartTime(bool withOffset)
		{
			return withOffset ? -GetMusicOffset() : 0.0;
		}

		public double GetMusicOffset()
		{
			if (UsesChartMusicOffset)
				return MusicOffset;
			return EditorSong.MusicOffset;
		}

		public double GetEndTime(bool withOffset)
		{
			var lastEvent = EditorEvents.Last();
			var endTime = 0.0;
			if (lastEvent.MoveNext())
				endTime = ToSeconds(lastEvent.Current.GetEvent().TimeMicros);
			endTime = Math.Max(endTime, EditorSong.LastSecondHint);
			if (withOffset)
				endTime -= GetMusicOffset();
			return endTime;
		}

		public double GetEndPosition()
		{
			var lastEvent = EditorEvents.Last();
			if (lastEvent.MoveNext())
				return lastEvent.Current.GetEvent().IntegerPosition;
			return 0;
		}

		//steps_tag_handlers["BPMS"] = &SetStepsBPMs;
		//steps_tag_handlers["STOPS"] = &SetStepsStops;
		//steps_tag_handlers["DELAYS"] = &SetStepsDelays;
		//steps_tag_handlers["TIMESIGNATURES"] = &SetStepsTimeSignatures;
		//steps_tag_handlers["TICKCOUNTS"] = &SetStepsTickCounts;
		//steps_tag_handlers["COMBOS"] = &SetStepsCombos;
		//steps_tag_handlers["WARPS"] = &SetStepsWarps;
		//steps_tag_handlers["SPEEDS"] = &SetStepsSpeeds;
		//steps_tag_handlers["SCROLLS"] = &SetStepsScrolls;
		//steps_tag_handlers["FAKES"] = &SetStepsFakes;
		//steps_tag_handlers["LABELS"] = &SetStepsLabels;
		///* If this is called, the chart does not use the same attacks
		// * as the Song's timing. No other changes are required. */
		//steps_tag_handlers["ATTACKS"] = &SetStepsAttacks;

	}
}

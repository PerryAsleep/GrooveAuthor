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

		public DisplayTempo()
		{

		}

		public DisplayTempo(DisplayTempo other)
		{
			Mode = other.Mode;
			SpecifiedTempoMin = other.SpecifiedTempoMin;
			SpecifiedTempoMax = other.SpecifiedTempoMax;
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
					}
				}
				else if (parts.Length == 2)
				{
					if (double.TryParse(parts[0], out SpecifiedTempoMin) && double.TryParse(parts[1], out SpecifiedTempoMax))
					{
						parsed = true;
						Mode = DisplayTempoMode.Specified;
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
						return $"{SpecifiedTempoMin:SMDoubleFormat}:{SpecifiedTempoMax:SMDoubleFormat}";
					return $"{SpecifiedTempoMin:SMDoubleFormat}";
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
			string fileDirectory,
			Song song,
			GraphicsDevice graphicsDevice,
			ImGuiRenderer imGuiRenderer)
		{
			Editor = editor;
			FileDirectory = fileDirectory;
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

			var hasDisplayTempo = song.Extras.TryGetExtra(SMCommon.TagDisplayBPM, out string displayTempoString, true);
			DisplayTempo displayTempo = null;
			if (hasDisplayTempo)
			{
				displayTempo = new DisplayTempo();
				displayTempo.FromString(displayTempoString);
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

				var editorChart = new EditorChart(Editor, chart);
				if (!Charts.ContainsKey(editorChart.ChartType))
					Charts.Add(editorChart.ChartType, new List<EditorChart>());
				Charts[editorChart.ChartType].Add(editorChart);

				if (hasDisplayTempo && !editorChart.HasDisplayTempoFromChart)
				{
					editorChart.CopyDisplayTempo(displayTempo);
				}
			}
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
			song.Extras.AddSourceExtra(SMCommon.TagOrigin, Origin, true);
			song.Extras.AddSourceExtra(SMCommon.TagCredit, Credit, true);
			song.SongSelectImage = Banner.Path;
			song.Extras.AddSourceExtra(SMCommon.TagBackground, Background.Path, true);
			song.Extras.AddSourceExtra(SMCommon.TagJacket, Jacket.Path, true);
			song.Extras.AddSourceExtra(SMCommon.TagCDImage, CDImage.Path, true);
			song.Extras.AddSourceExtra(SMCommon.TagDiscImage, DiscImage.Path, true);
			song.Extras.AddSourceExtra(SMCommon.TagCDTitle, CDTitle.Path, true);
			song.Extras.AddSourceExtra(SMCommon.TagLyricsPath, LyricsPath, true);
			song.Extras.AddSourceExtra(SMCommon.TagPreviewVid, PreviewVideoPath, true);

			song.Extras.AddSourceExtra(SMCommon.TagMusic, MusicPath, true);
			song.PreviewMusicFile = MusicPreviewPath;
			song.Extras.AddSourceExtra(SMCommon.TagOffset, MusicOffset, true);
			song.Extras.AddSourceExtra(SMCommon.TagLastSecondHint, LastSecondHint, true);
			song.Extras.AddSourceExtra(SMCommon.TagSampleStart, SampleStart, true);
			song.Extras.AddSourceExtra(SMCommon.TagSampleLength, SampleLength, true);

			// Do not add the display BPM.
			// We want to use the charts' display BPMs.
			//song.Extras.AddSourceExtra(SMCommon.TagDisplayBPM, DisplayTempo.ToString(), true);
			
			song.Extras.AddSourceExtra(SMCommon.TagSelectable, Selectable.ToString(), true);

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

		public bool UsesChartMusicOffset = false;
		public double MusicOffset;

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


		public int NumInputs;
		public int NumPlayers;


		public EditorChart(Editor editor, Chart chart)
		{
			Editor = editor;
			OriginalChartExtras = chart.Extras;

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
			chart.Extras.TryGetExtra(SMCommon.TagCredit, out Credit, true);
			Credit ??= "";
			chart.Extras.TryGetExtra(SMCommon.TagMusic, out string musicPath, true);
			MusicPath = musicPath;
			UsesChartMusicOffset = chart.Extras.TryGetExtra(SMCommon.TagOffset, out MusicOffset, true);

			HasDisplayTempoFromChart = chart.Extras.TryGetExtra(SMCommon.TagDisplayBPM, out string displayTempoString, true);
			DisplayTempo.FromString(displayTempoString);

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
			chart.Extras.AddSourceExtra(SMCommon.TagChartName, Name);
			chart.Description = Description;
			chart.Extras.AddSourceExtra(SMCommon.TagChartStyle, Style);
			chart.Extras.AddSourceExtra(SMCommon.TagCredit, Credit);
			chart.Extras.AddSourceExtra(SMCommon.TagMusic, MusicPath);
			if (UsesChartMusicOffset)
				chart.Extras.AddSourceExtra(SMCommon.TagOffset, MusicOffset);
			chart.Extras.AddSourceExtra(SMCommon.TagDisplayBPM, DisplayTempo.ToString());

			// TODO: Notes
			var layer = new Layer();

			//layer.Events.Add();
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

			EditorRateAlteringEvent previousEvent = new EditorRateAlteringEvent
			{
				Row = 0,
				SongTime = 0.0,
				RowsPerSecond = 0.0,
				SecondsPerRow = 0.0
			};

			var lastHoldStarts = new EditorHoldStartNote[NumInputs];
			var lastScrollRateInterpolationValue = 1.0;
			var lastScrollRate = 1.0;
			var lastTempo = 1.0;
			var firstTempo = true;
			var firstScrollRate = true;
			TimeSignature lastTimeSignature = null;
			var timePerTempo = new Dictionary<double, long>();
			var lastTempoChangeTime = 0L;
			var minTempo = double.MaxValue;
			var maxTempo = double.MinValue;
			for (var eventIndex = 0; eventIndex < chart.Layers[0].Events.Count; eventIndex++)
			{
				var chartEvent = chart.Layers[0].Events[eventIndex];

				var editorEvent = EditorEvent.CreateEvent(chartEvent);
				if (editorEvent != null)
					editorEvents.Insert(editorEvent);

				if (editorEvent is EditorHoldStartNote hs)
				{
					lastHoldStarts[hs.GetLane()] = hs;
				}
				else if (editorEvent is EditorHoldEndNote he)
				{
					var start = lastHoldStarts[he.GetLane()];
					he.SetHoldStartNote(start);
					start.SetHoldEndNote(he);
				}

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
						foreach (var previousRateAlteringEvent in rateAlteringEventsBySongTime)
						{
							previousRateAlteringEvent.RowsPerSecond = newRowsPerSecond;
							previousRateAlteringEvent.SecondsPerRow = newSecondsPerRow;
							previousRateAlteringEvent.Tempo = tc.TempoBPM;
						}
					}

					minTempo = Math.Min(minTempo, tc.TempoBPM);
					maxTempo = Math.Min(maxTempo, tc.TempoBPM);

					var newEvent = new EditorRateAlteringEvent
					{
						Row = chartEvent.IntegerPosition,
						RowForFollowingEvents = chartEvent.IntegerPosition,
						ChartEvent = chartEvent,
						SongTime = previousEvent.SongTimeForFollowingEvents + secondsSincePrevious,
						SongTimeForFollowingEvents = previousEvent.SongTimeForFollowingEvents + secondsSincePrevious,
						RowsPerSecond = newRowsPerSecond,
						SecondsPerRow = newSecondsPerRow,
						ScrollRate = lastScrollRate,
						Tempo = tc.TempoBPM,
						LastTimeSignature = lastTimeSignature,
					};
					rateAlteringEventsBySongTime.Insert(newEvent);
					rateAlteringEventsByRow.Insert(newEvent);

					if (!firstTempo)
					{
						timePerTempo.TryGetValue(lastTempo, out var currentTempoTime);
						timePerTempo[lastTempo] = currentTempoTime + tc.TimeMicros - lastTempoChangeTime;
						lastTempoChangeTime = tc.TimeMicros;
					}

					previousEvent = newEvent;
					lastTempo = tc.TempoBPM;
					firstTempo = false;
				}
				else if (chartEvent is Stop stop)
				{
					var rowsSincePrevious = chartEvent.IntegerPosition - previousEvent.Row;
					var secondsSincePrevious = rowsSincePrevious * previousEvent.SecondsPerRow;

					var newEvent = new EditorRateAlteringEvent
					{
						Row = chartEvent.IntegerPosition,
						RowForFollowingEvents = chartEvent.IntegerPosition,
						ChartEvent = chartEvent,
						SongTime = previousEvent.SongTimeForFollowingEvents + secondsSincePrevious,
						SongTimeForFollowingEvents = previousEvent.SongTimeForFollowingEvents + secondsSincePrevious +
													 (stop.LengthMicros / 1000000.0),
						RowsPerSecond = previousEvent.RowsPerSecond,
						SecondsPerRow = previousEvent.SecondsPerRow,
						ScrollRate = lastScrollRate,
						Tempo = lastTempo,
						LastTimeSignature = lastTimeSignature,
					};
					rateAlteringEventsBySongTime.Insert(newEvent);
					rateAlteringEventsByRow.Insert(newEvent);
					previousEvent = newEvent;
				}
				else if (chartEvent is Warp warp)
				{
					var rowsSincePrevious = chartEvent.IntegerPosition - previousEvent.Row;
					var secondsSincePrevious = rowsSincePrevious * previousEvent.SecondsPerRow;

					var newEvent = new EditorRateAlteringEvent
					{
						Row = chartEvent.IntegerPosition,
						RowForFollowingEvents = chartEvent.IntegerPosition + warp.LengthIntegerPosition,
						ChartEvent = chartEvent,
						SongTime = previousEvent.SongTimeForFollowingEvents + secondsSincePrevious,
						SongTimeForFollowingEvents = previousEvent.SongTimeForFollowingEvents + secondsSincePrevious,
						RowsPerSecond = previousEvent.RowsPerSecond,
						SecondsPerRow = previousEvent.SecondsPerRow,
						ScrollRate = lastScrollRate,
						Tempo = lastTempo,
						LastTimeSignature = lastTimeSignature,
					};
					rateAlteringEventsBySongTime.Insert(newEvent);
					rateAlteringEventsByRow.Insert(newEvent);
					previousEvent = newEvent;
				}
				else if (chartEvent is ScrollRate scrollRate)
				{
					var rowsSincePrevious = chartEvent.IntegerPosition - previousEvent.Row;
					var secondsSincePrevious = rowsSincePrevious * previousEvent.SecondsPerRow;

					// Update any events which precede the first tempo so they can have accurate rates.
					// This is useful for determining spacing prior to the first event
					if (firstScrollRate)
					{
						foreach (var previousRateAlteringEvent in rateAlteringEventsBySongTime)
						{
							previousRateAlteringEvent.ScrollRate = scrollRate.Rate;
						}
					}

					var newEvent = new EditorRateAlteringEvent
					{
						Row = chartEvent.IntegerPosition,
						RowForFollowingEvents = chartEvent.IntegerPosition,
						ChartEvent = chartEvent,
						SongTime = previousEvent.SongTimeForFollowingEvents + secondsSincePrevious,
						SongTimeForFollowingEvents = previousEvent.SongTimeForFollowingEvents + secondsSincePrevious,
						RowsPerSecond = previousEvent.RowsPerSecond,
						SecondsPerRow = previousEvent.SecondsPerRow,
						ScrollRate = scrollRate.Rate,
						Tempo = lastTempo,
						LastTimeSignature = lastTimeSignature,
					};
					rateAlteringEventsBySongTime.Insert(newEvent);
					rateAlteringEventsByRow.Insert(newEvent);
					previousEvent = newEvent;
					lastScrollRate = scrollRate.Rate;
					firstScrollRate = false;
				}
				else if (chartEvent is ScrollRateInterpolation scrollRateInterpolation)
				{
					var newEvent = new EditorInterpolatedRateAlteringEvent
					{
						Row = scrollRateInterpolation.IntegerPosition,
						SongTime = scrollRateInterpolation.TimeMicros,
						ChartEvent = scrollRateInterpolation,
						PreviousScrollRate = lastScrollRateInterpolationValue
					};
					interpolatedScrollRateEvents.Insert(newEvent);
					lastScrollRateInterpolationValue = scrollRateInterpolation.Rate;
				}
				else if (chartEvent is TimeSignature timeSignature)
				{
					var rowsSincePrevious = chartEvent.IntegerPosition - previousEvent.Row;
					var secondsSincePrevious = rowsSincePrevious * previousEvent.SecondsPerRow;

					var newEvent = new EditorRateAlteringEvent
					{
						Row = chartEvent.IntegerPosition,
						RowForFollowingEvents = chartEvent.IntegerPosition,
						ChartEvent = chartEvent,
						SongTime = previousEvent.SongTimeForFollowingEvents + secondsSincePrevious,
						SongTimeForFollowingEvents = previousEvent.SongTimeForFollowingEvents + secondsSincePrevious,
						RowsPerSecond = previousEvent.RowsPerSecond,
						SecondsPerRow = previousEvent.SecondsPerRow,
						ScrollRate = lastScrollRate,
						Tempo = lastTempo,
						LastTimeSignature = timeSignature,
					};
					rateAlteringEventsBySongTime.Insert(newEvent);
					rateAlteringEventsByRow.Insert(newEvent);
					lastTimeSignature = timeSignature;
				}
			}

			if (previousEvent.ChartEvent != null)
			{
				timePerTempo.TryGetValue(lastTempo, out var lastTempoTime);
				timePerTempo[lastTempo] = lastTempoTime + previousEvent.ChartEvent.TimeMicros - lastTempoChangeTime;
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

			EditorEvents = editorEvents;
			RateAlteringEventsBySongTime = rateAlteringEventsBySongTime;
			RateAlteringEventsByRow = rateAlteringEventsByRow;
			InterpolatedScrollRateEvents = interpolatedScrollRateEvents;
			MostCommonTempo = mostCommonTempo;
			MinTempo = minTempo;
			MaxTempo = maxTempo;
		}

		public bool TryGetChartPositionFromTime(double songTime, ref double chartPosition)
		{
			if (RateAlteringEventsBySongTime == null)
				return false;

			// Given the current song time, get the greatest preceding event which alters the rate of rows to time.
			var enumerator =
				RateAlteringEventsBySongTime.FindGreatestPreceding(new EditorRateAlteringEvent { SongTime = songTime });
			// If there is no preceding event (e.g. SongTime is negative), use the first event.
			if (enumerator == null)
				enumerator = RateAlteringEventsBySongTime.GetEnumerator();
			// If there is still no event then the Chart is misconfigured as it must have at least a Tempo event.
			if (enumerator == null)
				return false;

			// Update the ChartPosition based on the cached rate information.
			enumerator.MoveNext();
			var rateEvent = enumerator.Current;
			if (songTime >= rateEvent.SongTime && songTime < rateEvent.SongTimeForFollowingEvents)
				chartPosition = rateEvent.Row;
			else
				chartPosition = rateEvent.Row + rateEvent.RowsPerSecond * (songTime - rateEvent.SongTimeForFollowingEvents);
			return true;
		}
		
		public bool TryGetTimeFromChartPosition(double chartPosition, ref double songTime)
		{
			if (RateAlteringEventsByRow == null)
				return false;

			// Given the current song time, get the greatest preceding event which alters the rate of rows to time.
			var enumerator = RateAlteringEventsByRow.FindGreatestPreceding(new EditorRateAlteringEvent { Row = chartPosition });
			// If there is no preceding event (e.g. ChartPosition is negative), use the first event.
			if (enumerator == null)
				enumerator = RateAlteringEventsByRow.GetEnumerator();
			// If there is still no event then the Chart is misconfigured as it must have at least a Tempo event.
			if (enumerator == null)
				return false;

			// Update the Song time based on the cached rate information.
			// TODO: Need to take into account Stops vs Delays?
			enumerator.MoveNext();
			var rateEvent = enumerator.Current;
			songTime = rateEvent.SongTimeForFollowingEvents + rateEvent.SecondsPerRow * (chartPosition - rateEvent.Row);
			return true;
		}

		public RedBlackTree<EditorEvent>.Enumerator FindEvent(EditorEvent pos)
		{
			var best = EditorEvents.FindGreatestPreceding(pos);
			if (best == null)
				best = EditorEvents.FindLeastFollowing(pos);
			if (best == null)
				return null;

			// Scan backwards until we have checked every lane for a long note which may
			// be extending through the given start row.
			var lanesChecked = new bool[NumInputs];
			var numLanesChecked = 0;
			var current = new RedBlackTree<EditorEvent>.Enumerator(best);
			while (current.MovePrev() && numLanesChecked < NumInputs)
			{
				var e = current.Current;
				var lane = e.GetLane();
				if (lane >= 0)
				{
					if (lanesChecked[lane])
					{
						lanesChecked[lane] = true;
						numLanesChecked++;

						if (e.GetRow() + e.GetLength() > pos.GetRow())
							best = new RedBlackTree<EditorEvent>.Enumerator(current);
					}
				}
			}

			return best;
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

using System;
using System.Collections.Generic;
using System.Linq;
using Fumen;
using Fumen.ChartDefinition;
using Fumen.Converters;
using Microsoft.Xna.Framework.Graphics;
using static StepManiaEditor.Utils;
using static Fumen.Utils;
using static Fumen.Converters.SMCommon;
using System.Runtime.InteropServices;

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
			Mode = DisplayTempoMode.Actual;
			SpecifiedTempoMin = 0.0;
			SpecifiedTempoMax = 0.0;
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
				Logger.Warn($"Failed to parse {TagDisplayBPM} value: '{displayTempoString}'.");
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
						return $"{SpecifiedTempoMin.ToString(SMDoubleFormat)}:{SpecifiedTempoMax.ToString(SMDoubleFormat)}";
					return SpecifiedTempoMin.ToString(SMDoubleFormat);
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

		public Dictionary<ChartType, List<EditorChart>> Charts = new Dictionary<ChartType, List<EditorChart>>();
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
				Editor.OnSongMusicChanged(this);
			}
		}

		private string MusicPreviewPathInternal;

		public string MusicPreviewPath
		{
			get => MusicPreviewPathInternal;
			set
			{
				MusicPreviewPathInternal = value ?? "";
				Editor.OnSongMusicPreviewChanged(this);
			}
		}

		private double MusicOffsetInternal;
		public double MusicOffset
		{
			get => MusicOffsetInternal;
			set
			{
				MusicOffsetInternal = value;
				Editor.OnSongMusicOffsetChanged(this);
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
			song.Extras.TryGetExtra(TagOrigin, out Origin, true);
			Origin ??= "";
			song.Extras.TryGetExtra(TagCredit, out Credit, true);
			Credit ??= "";

			Banner = new EditorImageData(FileDirectory, graphicsDevice, imGuiRenderer, BannerWidth, BannerHeight,
				song.SongSelectImage);
			string tempStr;
			song.Extras.TryGetExtra(TagBackground, out tempStr, true);
			Background = new EditorImageData(tempStr);
			song.Extras.TryGetExtra(TagJacket, out tempStr, true);
			Jacket = new EditorImageData(tempStr);
			song.Extras.TryGetExtra(TagCDImage, out tempStr, true);
			CDImage = new EditorImageData(tempStr);
			song.Extras.TryGetExtra(TagDiscImage, out tempStr, true);
			DiscImage = new EditorImageData(tempStr);
			song.Extras.TryGetExtra(TagCDTitle, out tempStr, true);
			CDTitle = new EditorImageData(FileDirectory, graphicsDevice, imGuiRenderer, CDTitleWidth, CDTitleHeight, tempStr);

			song.Extras.TryGetExtra(TagLyricsPath, out LyricsPath, true);
			LyricsPath ??= "";
			song.Extras.TryGetExtra(TagPreviewVid, out PreviewVideoPath, true);
			PreviewVideoPath ??= "";

			song.Extras.TryGetExtra(TagMusic, out string musicPath, true);
			MusicPath = musicPath;

			MusicPreviewPath = song.PreviewMusicFile ?? "";
			song.Extras.TryGetExtra(TagOffset, out double musicOffset, true);
			MusicOffset = musicOffset;
			song.Extras.TryGetExtra(TagLastSecondHint, out LastSecondHint, true);
			if (LastSecondHint <= 0.0)
			{
				// TODO: When the last beat hint is set we need to use the song's timing data
				// to determine the last second hint.
				if (song.Extras.TryGetExtra(TagLastBeatHint, out double lastBeatHint, true))
				{
				}
			}

			SampleStart = song.PreviewSampleStart;
			SampleLength = song.PreviewSampleLength;

			var hasDisplayTempo = song.Extras.TryGetExtra(TagDisplayBPM, out object _, true);
			DisplayTempo displayTempo = null;
			if (hasDisplayTempo)
			{
				displayTempo = new DisplayTempo();
				displayTempo.FromString(GetDisplayBPMStringFromSourceExtrasList(song.Extras, null));
			}

			song.Extras.TryGetExtra(TagSelectable, out string selectableString, true);
			if (!string.IsNullOrEmpty(selectableString))
			{
				if (!Enum.TryParse(selectableString, true, out Selectable))
				{
					Selectable = Selectable.YES;
					Logger.Warn($"Failed to parse Song {TagSelectable} value: '{selectableString}'.");
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

			UpdateChartSort();
		}

		public EditorChart AddChart(ChartType chartType)
		{
			var chart = new EditorChart(Editor, this, chartType);
			if (!Charts.ContainsKey(chartType))
				Charts.Add(chartType, new List<EditorChart>());
			Charts[chartType].Add(chart);
			UpdateChartSort();
			return chart;
		}

		public EditorChart AddChart(EditorChart chart)
		{
			if (!Charts.ContainsKey(chart.ChartType))
				Charts.Add(chart.ChartType, new List<EditorChart>());
			Charts[chart.ChartType].Add(chart);
			UpdateChartSort();
			return chart;
		}

		public void DeleteChart(EditorChart chart)
		{
			if (!Charts.ContainsKey(chart.ChartType))
				return;
			Charts[chart.ChartType].Remove(chart);
			if (Charts[chart.ChartType].Count == 0)
				Charts.Remove(chart.ChartType);
			UpdateChartSort();
		}

		public void UpdateChartSort()
		{
			foreach (var kvp in Charts)
			{
				kvp.Value.Sort(new ChartComparer());
			}
		}

		public void SetFullFilePath(string fullFilePath)
		{
			FileFullPath = fullFilePath;
			FileName = System.IO.Path.GetFileName(fullFilePath);
			FileDirectory = System.IO.Path.GetDirectoryName(fullFilePath);
			FileFormat = FileFormat.GetFileFormatByExtension(System.IO.Path.GetExtension(fullFilePath));
		}

		public double GetBestChartStartingTempo()
		{
			var histogram = new Dictionary<double, int>();
			foreach(var kvp in Charts)
			{
				foreach(var chart in kvp.Value)
				{
					var tempo = chart.GetStartingTempo();
					if (histogram.TryGetValue(tempo, out var count))
					{
						histogram[tempo] = count + 1;
					}
					else
					{
						histogram.Add(tempo, 1);
					}
				}
			}
			if (histogram.Count > 0)
			{
				return histogram.Aggregate((x, y) => x.Value > y.Value ? x : y).Key;
			}

			return EditorChart.DefaultTempo;
		}

		public Fraction GetBestChartStartingTimeSignature()
		{
			var histogram = new Dictionary<Fraction, int>();
			foreach (var kvp in Charts)
			{
				foreach (var chart in kvp.Value)
				{
					var timeSignature = chart.GetStartingTimeSignature();
					if (histogram.TryGetValue(timeSignature, out var count))
					{
						histogram[timeSignature] = count + 1;
					}
					else
					{
						histogram.Add(timeSignature, 1);
					}
				}
			}
			if (histogram.Count > 0)
			{
				return histogram.Aggregate((x, y) => x.Value > y.Value ? x : y).Key;
			}

			return EditorChart.DefaultTimeSignature;
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
			song.Extras.AddDestExtra(TagOrigin, Origin, true);
			song.Extras.AddDestExtra(TagCredit, Credit, true);
			song.SongSelectImage = Banner.Path;
			song.Extras.AddDestExtra(TagBackground, Background.Path, true);
			song.Extras.AddDestExtra(TagJacket, Jacket.Path, true);
			song.Extras.AddDestExtra(TagCDImage, CDImage.Path, true);
			song.Extras.AddDestExtra(TagDiscImage, DiscImage.Path, true);
			song.Extras.AddDestExtra(TagCDTitle, CDTitle.Path, true);
			song.Extras.AddDestExtra(TagLyricsPath, LyricsPath, true);
			song.Extras.AddDestExtra(TagPreviewVid, PreviewVideoPath, true);

			song.Extras.AddDestExtra(TagMusic, MusicPath, true);
			song.PreviewMusicFile = MusicPreviewPath;
			song.Extras.AddDestExtra(TagOffset, MusicOffset, true);
			song.Extras.AddDestExtra(TagLastSecondHint, LastSecondHint, true);
			song.PreviewSampleStart = SampleStart;
			song.PreviewSampleLength = SampleLength;

			// Do not add the display BPM.
			// We want to use the charts' display BPMs.
			//song.Extras.AddDestExtra(TagDisplayBPM, DisplayTempo.ToString(), true);

			song.Extras.AddDestExtra(TagSelectable, Selectable.ToString(), true);

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
		public static double DefaultTempo = 120.0;
		public static Fraction DefaultTimeSignature = new Fraction(4, 4);
		public static double DefaultScrollRate = 1.0;
		public static int DefaultTickCount = 4;
		public static int DefaultHitMultiplier = 1;
		public static int DefaultMissMultiplier = 1;
		public static int DefaultRating = 1;

		private Editor Editor;
		private Extras OriginalChartExtras;

		public EditorSong EditorSong;

		public readonly ChartType ChartType;

		private ChartDifficultyType ChartDifficultyTypeInternal;
		public ChartDifficultyType ChartDifficultyType
		{
			get => ChartDifficultyTypeInternal;
			set
			{
				ChartDifficultyTypeInternal = value;
				Editor.OnChartDifficultyTypeChanged(this);
			}
		}

		private int RatingInternal;
		public int Rating
		{
			get => RatingInternal;
			set
			{
				RatingInternal = value;
				Editor.OnChartRatingChanged(this);
			}
		}

		private string NameInternal;
		public string Name
		{
			get => NameInternal;
			set
			{
				NameInternal = value;
				Editor.OnChartNameChanged(this);
			}
		}

		private string DescriptionInternal;
		public string Description
		{
			get => DescriptionInternal;
			set
			{
				DescriptionInternal = value;
				Editor.OnChartDescriptionChanged(this);
			}
		}

		public string Style;
		public string Credit;

		private string MusicPathInternal;
		public string MusicPath
		{
			get => MusicPathInternal;
			set
			{
				MusicPathInternal = value ?? "";
				Editor.OnChartMusicChanged(this);
			}
		}

		public bool UsesChartMusicOffsetInternal;
		public bool UsesChartMusicOffset
		{
			get => UsesChartMusicOffsetInternal;
			set
			{
				UsesChartMusicOffsetInternal = value;
				Editor.OnChartMusicOffsetChanged(this);
			}
		}

		private double MusicOffsetInternal;
		public double MusicOffset
		{
			get => MusicOffsetInternal;
			set
			{
				MusicOffsetInternal = value;
				Editor.OnChartMusicOffsetChanged(this);
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

			TryGetChartType(chart.Type, out ChartType);
			if (Enum.TryParse(chart.DifficultyType, out ChartDifficultyType parsedChartDifficultyType))
				ChartDifficultyType = parsedChartDifficultyType;
			Rating = (int)chart.DifficultyRating;

			NumInputs = Properties[(int)ChartType].NumInputs;
			NumPlayers = Properties[(int)ChartType].NumPlayers;

			chart.Extras.TryGetExtra(TagChartName, out string parsedName, true);
			Name = parsedName == null ? "" : parsedName;
			Description = chart.Description ?? "";
			chart.Extras.TryGetExtra(TagChartStyle, out Style, true);	// Pad or Keyboard
			Style ??= "";
			Credit = chart.Author ?? "";
			chart.Extras.TryGetExtra(TagMusic, out string musicPath, true);
			MusicPath = musicPath;
			UsesChartMusicOffset = chart.Extras.TryGetExtra(TagOffset, out double musicOffset, true);
			if (UsesChartMusicOffset)
				MusicOffset = musicOffset;

			HasDisplayTempoFromChart = !string.IsNullOrEmpty(chart.Tempo);
			DisplayTempo.FromString(chart.Tempo);

			// TODO: I wonder if there is an optimization to not do all the tree parsing for inactive charts.
			SetUpEditorEvents(chart);
		}

		public EditorChart(Editor editor, EditorSong editorSong, ChartType chartType)
		{
			Editor = editor;
			EditorSong = editorSong;
			ChartType = chartType;

			NumInputs = Properties[(int)ChartType].NumInputs;
			NumPlayers = Properties[(int)ChartType].NumPlayers;

			Name = "";
			Description = "";
			Style = "";
			Credit = "";
			MusicPath = "";
			UsesChartMusicOffset = false;
			HasDisplayTempoFromChart = false;

			Rating = DefaultRating;

			var tempChart = new Chart();
			var tempLayer = new Layer();
			tempLayer.Events.Add(new TimeSignature(editorSong.GetBestChartStartingTimeSignature())
			{
				IntegerPosition = 0,
				MetricPosition = new MetricPosition(0, 0),
			});
			tempLayer.Events.Add(new Tempo(editorSong.GetBestChartStartingTempo())
			{
				IntegerPosition = 0,
				MetricPosition = new MetricPosition(0, 0),
			});
			tempLayer.Events.Add(new ScrollRate(DefaultScrollRate)
			{
				IntegerPosition = 0,
				MetricPosition = new MetricPosition(0, 0),
			});
			tempLayer.Events.Add(new ScrollRateInterpolation(DefaultScrollRate, 0, 0L, false)
			{
				IntegerPosition = 0,
				MetricPosition = new MetricPosition(0, 0),
			});
			tempLayer.Events.Add(new TickCount(DefaultTickCount)
			{
				IntegerPosition = 0,
				MetricPosition = new MetricPosition(0, 0),
			});
			tempLayer.Events.Add(new Multipliers(DefaultHitMultiplier, DefaultMissMultiplier)
			{
				IntegerPosition = 0,
				MetricPosition = new MetricPosition(0, 0),
			});
			tempChart.Layers.Add(tempLayer);
			SetUpEditorEvents(tempChart);
		}

		public Chart SaveToChart()
		{
			Chart chart = new Chart();
			chart.Extras = new Extras(OriginalChartExtras);

			chart.Type = ChartTypeString(ChartType);
			chart.DifficultyType = ChartDifficultyType.ToString();
			chart.NumInputs = NumInputs;
			chart.NumPlayers = NumPlayers;
			chart.DifficultyRating = Rating;
			chart.Extras.AddDestExtra(TagChartName, Name);
			chart.Description = Description;
			chart.Extras.AddDestExtra(TagChartStyle, Style);
			chart.Author = Credit;
			chart.Extras.AddDestExtra(TagMusic, MusicPath);
			if (UsesChartMusicOffset)
				chart.Extras.AddDestExtra(TagOffset, MusicOffset);
			//TODO: Else use song?
			chart.Tempo = DisplayTempo.ToString();

			var layer = new Layer();
			foreach (var editorEvent in EditorEvents)
			{
				layer.Events.Add(editorEvent.GetEvent());
			}
			layer.Events.Sort(new SMEventComparer());
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
						// For the first scroll rate event, set the previous rate to the first rate so we use the
						// first scroll rate when consider positions and times before 0.0. See also
						// OnInterpolatedRateAlteringEventModified.
						irae.PreviousScrollRate = firstInterpolatedScrollRate ? scrollRateInterpolation.Rate : lastScrollRateInterpolationValue;
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
					var rowsSincePrevious = Math.Max(0, chartEvent.IntegerPosition - previousEvent.RowForFollowingEvents);
					var secondsSincePrevious = rowsSincePrevious * previousEvent.SecondsPerRow;

					var newSecondsPerRow = 60.0 / tc.TempoBPM / (double)MaxValidDenominator;
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
					var rowsSincePrevious = Math.Max(0, chartEvent.IntegerPosition - previousEvent.RowForFollowingEvents);
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
					var rowsSincePrevious = Math.Max(0, chartEvent.IntegerPosition - previousEvent.RowForFollowingEvents);
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
					var rowsSincePrevious = Math.Max(0, chartEvent.IntegerPosition - previousEvent.RowForFollowingEvents);
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
					var rowsSincePrevious = Math.Max(0, chartEvent.IntegerPosition - previousEvent.RowForFollowingEvents);
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
			SetEventTimeMicrosAndMetricPositionsFromRows(EditorEvents.Select(e => e.GetEvent()));
		}

		public bool TryGetChartPositionFromTime(double chartTime, ref double chartPosition)
		{
			var rateEvent = GetActiveRateAlteringEventForTime(chartTime, false);
			if (rateEvent == null)
				return false;

			// Cap the relative time to 0.0 so warps function properly, except when the chart time is
			// before 0.0.
			var relativeTime = chartTime - rateEvent.SongTimeForFollowingEvents;
			if (chartTime > 0.0 && relativeTime < 0.0)
				relativeTime = 0.0;
			chartPosition = rateEvent.RowForFollowingEvents + rateEvent.RowsPerSecond * relativeTime;
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

			// Cap the relative position to 0.0 so warps function properly, except when the chart time is
			// before 0.0.
			var relativePosition = chartPosition - rateEvent.RowForFollowingEvents;
			if (chartPosition > 0.0 && relativePosition < 0)
				relativePosition = 0.0;
			chartTime = rateEvent.SongTimeForFollowingEvents + rateEvent.SecondsPerRow * relativePosition;
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

				// If this is the first event, set its PreviousScrollRate as well so when we consider times
				// and positions before 0.0 we use the first scroll rate.
				// See also SetUpEditorEvents.
				var first = !e.MovePrev();
				e.MoveNext();
				if (first)
				{
					e.Current.PreviousScrollRate = irae.ScrollRateInterpolationEvent.Rate;
				}

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

		public double GetStartingTempo()
		{
			var rae = GetActiveRateAlteringEventForPosition(0.0);
			return rae?.Tempo ?? DefaultTempo;
		}

		public Fraction GetStartingTimeSignature()
		{
			var rae = GetActiveRateAlteringEventForPosition(0.0);
			return rae?.LastTimeSignature.Signature ?? DefaultTimeSignature;
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

	/// <summary>
	/// Custom Comparer for Charts.
	/// </summary>
	public class ChartComparer : IComparer<EditorChart>
	{
		private static readonly Dictionary<ChartType, int> ChartTypeOrder = new Dictionary<ChartType, int>
		{
			{ ChartType.dance_single, 0 },
			{ ChartType.dance_double, 1 },
			{ ChartType.dance_couple, 2 },
			{ ChartType.dance_routine, 3 },
			{ ChartType.dance_solo, 4 },
			{ ChartType.dance_threepanel, 5 },

			{ ChartType.pump_single, 6 },
			{ ChartType.pump_halfdouble, 7 },
			{ ChartType.pump_double, 8 },
			{ ChartType.pump_couple, 9 },
			{ ChartType.pump_routine, 10 },

			{ ChartType.smx_beginner, 11 },
			{ ChartType.smx_single, 12 },
			{ ChartType.smx_dual, 13 },
			{ ChartType.smx_full, 14 },
			{ ChartType.smx_team, 15 },
		};

		private static int StringCompare(string s1, string s2)
		{
			var s1Null = string.IsNullOrEmpty(s1);
			var s2Null = string.IsNullOrEmpty(s2);
			if (s1Null != s2Null)
				return s1Null ? 1 : -1;
			if (s1Null)
				return 0;
			return s1.CompareTo(s2);
		}

		public static int Compare(EditorChart c1, EditorChart c2)
		{
			if (null == c1 && null == c2)
				return 0;
			if (null == c1)
				return 1;
			if (null == c2)
				return -1;

			// Compare by ChartType
			var comparison = 0;
			var c1HasCharTypeOrder = ChartTypeOrder.TryGetValue(c1.ChartType, out int c1Order);
			var c2HasCharTypeOrder = ChartTypeOrder.TryGetValue(c2.ChartType, out int c2Order);
			if (c1HasCharTypeOrder != c2HasCharTypeOrder)
			{
				return c1HasCharTypeOrder ? -1 : 1;
			}
			if (c1HasCharTypeOrder)
			{
				comparison = c1Order - c2Order;
				if (comparison != 0)
					return comparison;
			}

			// Compare by DifficultyType
			comparison = c1.ChartDifficultyType - c2.ChartDifficultyType;
			if (comparison != 0)
				return comparison;

			// Compare by Rating
			comparison = c1.Rating - c2.Rating;
			if (comparison != 0)
				return comparison;

			comparison = StringCompare(c1.Name, c2.Name);
			if (comparison != 0)
				return comparison;

			comparison = StringCompare(c1.Description, c2.Description);
			if (comparison != 0)
				return comparison;

			// TODO: This should use note count not event count.
			return c1.EditorEvents.Count - c2.EditorEvents.Count;
		}

		int IComparer<EditorChart>.Compare(EditorChart c1, EditorChart c2)
		{
			return Compare(c1, c2);
		}
	}
}

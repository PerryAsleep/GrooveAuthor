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
using FMOD;
using static System.Diagnostics.Debug;

namespace StepManiaEditor
{
	internal enum Selectable
	{
		YES,
		NO,
		ROULETTE,
		ES,
		OMES
	}

	internal enum DisplayTempoMode
	{
		Random,
		Specified,
		Actual
	}

	/// <summary>
	/// Small class to hold a Texture for a song or chart property that
	/// represents a file path to an image asset.
	/// </summary>
	internal sealed class EditorImageData
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
			string path,
			bool cacheTextureColor)
		{
			FileDirectory = fileDirectory;
			Texture = new EditorTexture(graphicsDevice, imGuiRenderer, width, height, cacheTextureColor);
			Path = path;
		}

		public EditorTexture GetTexture()
		{
			return Texture;
		}
	}

	internal class EditorChartTimingData
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

	internal sealed class DisplayTempo
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

	internal sealed class EditorSong
	{
		private Editor Editor;

		private Extras OriginalSongExtras;

		public Dictionary<ChartType, List<EditorChart>> Charts = new Dictionary<ChartType, List<EditorChart>>();
		public List<Chart> UnsupportedCharts = new List<Chart>();

		public string FileDirectory;
		public string FileName;
		public string FileFullPath;
		public FileFormat FileFormat;

		public string Title = "";
		public string TitleTransliteration = "";
		public string Subtitle = "";
		public string SubtitleTransliteration = "";
		public string Artist = "";
		public string ArtistTransliteration = "";

		public string Genre = "";
		public string Origin = "";
		public string Credit = "";

		public EditorImageData Banner;
		public EditorImageData Background;
		public EditorImageData Jacket;
		public EditorImageData CDImage;
		public EditorImageData DiscImage;
		public EditorImageData CDTitle;

		public string LyricsPath = "";
		public string PreviewVideoPath = "";

		private string MusicPathInternal = "";

		public string MusicPath
		{
			get => MusicPathInternal;
			set
			{
				MusicPathInternal = value ?? "";
				Editor.OnSongMusicChanged(this);
			}
		}

		private string MusicPreviewPathInternal = "";

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
				DeletePreviewEvents();
				MusicOffsetInternal = value;
				AddPreviewEvents();
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

		private double LastSecondHintInternal;
		public double LastSecondHint;


		private double SampleStartInternal;
		public double SampleStart
		{
			get => SampleStartInternal;
			set
			{
				if (!SampleStartInternal.DoubleEquals(value))
				{
					DeletePreviewEvents();
					SampleStartInternal = value;
					AddPreviewEvents();
				}
				SampleStartInternal = value;
			}
		}
		public double SampleLength;

		public Selectable Selectable = Selectable.YES;


		public EditorSong(
			Editor editor,
			GraphicsDevice graphicsDevice,
			ImGuiRenderer imGuiRenderer)
		{
			Editor = editor;

			Banner = new EditorImageData(FileDirectory, graphicsDevice, imGuiRenderer, (uint)GetBannerWidth(), (uint)GetBannerHeight(), null, false);
			Background = new EditorImageData(FileDirectory, graphicsDevice, imGuiRenderer, (uint)GetBackgroundWidth(), (uint)GetBackgroundHeight(), null, true);
			Jacket = new EditorImageData(null);
			CDImage = new EditorImageData(null);
			DiscImage = new EditorImageData(null);
			CDTitle = new EditorImageData(FileDirectory, graphicsDevice, imGuiRenderer, (uint)GetCDTitleWidth(), (uint)GetCDTitleHeight(), null, false);

			MusicPath = "";
			MusicPreviewPath = "";
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

			Banner = new EditorImageData(FileDirectory, graphicsDevice, imGuiRenderer, (uint)GetBannerWidth(), (uint)GetBannerHeight(), song.SongSelectImage, false);
			string tempStr;
			song.Extras.TryGetExtra(TagBackground, out tempStr, true);
			Background = new EditorImageData(FileDirectory, graphicsDevice, imGuiRenderer, (uint)GetBackgroundWidth(), (uint)GetBackgroundHeight(), tempStr, true);
			song.Extras.TryGetExtra(TagJacket, out tempStr, true);
			Jacket = new EditorImageData(tempStr);
			song.Extras.TryGetExtra(TagCDImage, out tempStr, true);
			CDImage = new EditorImageData(tempStr);
			song.Extras.TryGetExtra(TagDiscImage, out tempStr, true);
			DiscImage = new EditorImageData(tempStr);
			song.Extras.TryGetExtra(TagCDTitle, out tempStr, true);
			CDTitle = new EditorImageData(FileDirectory, graphicsDevice, imGuiRenderer, (uint)GetCDTitleWidth(), (uint)GetCDTitleHeight(), tempStr, false);

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

		public bool IsUsingSongForPreview()
		{
			return string.IsNullOrEmpty(MusicPreviewPath);
		}

		private void DeletePreviewEvents()
		{
			foreach (var kvp in Charts)
			{
				foreach (var chart in kvp.Value)
				{
					chart.DeletePreviewEvent();
				}
			}
		}

		private void AddPreviewEvents()
		{
			foreach (var kvp in Charts)
			{
				foreach (var chart in kvp.Value)
				{
					chart.AddPreviewEvent();
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

	internal sealed class EditorChart
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

		private bool UsesChartMusicOffsetInternal;
		public bool UsesChartMusicOffset
		{
			get => UsesChartMusicOffsetInternal;
			set
			{
				if (UsesChartMusicOffsetInternal != value)
				{
					DeletePreviewEvent();
					UsesChartMusicOffsetInternal = value;
					AddPreviewEvent();
					Editor.OnChartMusicOffsetChanged(this);
				}
			}
		}

		private double MusicOffsetInternal;
		public double MusicOffset
		{
			get => MusicOffsetInternal;
			set
			{
				if (MusicOffsetInternal != value)
				{
					DeletePreviewEvent();
					MusicOffsetInternal = value;
					AddPreviewEvent();
					Editor.OnChartMusicOffsetChanged(this);
				}
			}
		}

		public bool HasDisplayTempoFromChart;
		public DisplayTempo DisplayTempo = new DisplayTempo();

		// TODO: RADARVALUES?

		public EventTree EditorEvents;
		public EventTree MiscEvents;
		public RateAlteringEventTree RateAlteringEvents;
		public RedBlackTree<EditorInterpolatedRateAlteringEvent> InterpolatedScrollRateEvents;
		private RedBlackTree<EditorStopEvent> Stops;
		private RedBlackTree<EditorDelayEvent> Delays;
		private RedBlackTree<EditorFakeSegmentEvent> Fakes;
		private RedBlackTree<EditorWarpEvent> Warps;
		private EditorPreviewRegionEvent PreviewEvent;

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
			UsesChartMusicOffsetInternal = chart.Extras.TryGetExtra(TagOffset, out double musicOffset, true);
			if (UsesChartMusicOffset)
				MusicOffsetInternal = musicOffset;

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
			var editorEvents = new EventTree(this);
			var rateAlteringEvents = new RateAlteringEventTree(this);
			var interpolatedScrollRateEvents = new RedBlackTree<EditorInterpolatedRateAlteringEvent>();
			var stops = new RedBlackTree<EditorStopEvent>();
			var delays = new RedBlackTree<EditorDelayEvent>();
			var fakes = new RedBlackTree<EditorFakeSegmentEvent>();
			var warps = new RedBlackTree<EditorWarpEvent>();
			var miscEvents = new EventTree(this);

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
				else if (editorEvent is EditorFakeSegmentEvent fse)
				{
					fakes.Insert(fse);
				}
				else if (editorEvent is EditorRateAlteringEvent rae)
				{
					rateAlteringEvents.Insert(rae);

					if (rae is EditorStopEvent se)
					{
						stops.Insert(se);
					}
					else if (rae is EditorDelayEvent de)
					{
						delays.Insert(de);
					}
					else if (rae is EditorWarpEvent we)
					{
						warps.Insert(we);
					}
				}
				else if (editorEvent is EditorInterpolatedRateAlteringEvent irae)
				{
					if (chartEvent is ScrollRateInterpolation scrollRateInterpolation)
					{
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

				if (editorEvent.IsMiscEvent())
					miscEvents.Insert(editorEvent);
			}

			EditorEvents = editorEvents;
			RateAlteringEvents = rateAlteringEvents;
			InterpolatedScrollRateEvents = interpolatedScrollRateEvents;
			Stops = stops;
			Delays = delays;
			Fakes = fakes;
			Warps = warps;
			MiscEvents = miscEvents;

			CleanRateAlteringEvents();

			// Create events that are not derived from the Chart's Events.
			AddPreviewEvent();
		}

		/// <summary>
		/// Updates all EditorRateAlteringEvents rate tracking values.
		/// This may result in TimeSignatures being deleted if they no longer fall on measure boundaries.
		/// </summary>
		/// <returns>List of all EditorEvents which were deleted as a result.</returns>
		private List<EditorEvent> CleanRateAlteringEvents()
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
			
			var warpRowsRemaining = 0;
			var stopTimeRemaining = 0.0;
			var canBeDeleted = true;
			var lastRowsPerSecond = 1.0;
			var lastSecondsPerRow = 1.0;

			EditorRateAlteringEvent previousEvent = null;
			var firstEnumerator = RateAlteringEvents.First();
			if (firstEnumerator != null)
			{
				firstEnumerator.MoveNext();
				previousEvent = firstEnumerator.Current;
			}

			List<EditorRateAlteringEvent> previousEvents = new List<EditorRateAlteringEvent>();
			List<EditorEvent> invalidTimeSignatures = new List<EditorEvent>();

			// TODO: Check handling of negative Tempo warps.

			foreach (var rae in RateAlteringEvents)
			{
				var chartEvent = rae.GetEvent();

				// Adjust warp rows remaining.
				warpRowsRemaining = Math.Max(0, warpRowsRemaining - (chartEvent.IntegerPosition - previousEvent.GetRow()));
				// Adjust stop timing remaining.
				if (stopTimeRemaining != 0.0)
				{
					// In most cases with a non zero stop time remaining, the stop time remaining is positive.
					// In those cases, the following events have already been adjusted such that their time
					// takes into account the stop time, and they should have 0.0 for their stop time remaining.
					// For negative stops however, we need to keep incrementing the stop time remaining until it
					// hits 0.0. To do this we need to add the time which would have elapsed between the last
					// event and this event if there were no stop. This is derived from their row difference
					// and the seconds per row.
					var rowsSincePrevious = chartEvent.IntegerPosition - previousEvent.GetRow();
					var stopTimeSincePrevious = rowsSincePrevious * lastSecondsPerRow;
					stopTimeRemaining = Math.Min(0.0, stopTimeRemaining + stopTimeSincePrevious);
				}

				if (chartEvent is Tempo tc)
				{
					lastSecondsPerRow = 60.0 / tc.TempoBPM / MaxValidDenominator;
					lastRowsPerSecond = 1.0 / lastSecondsPerRow;

					// Update any events which precede the first tempo so they can have accurate rates.
					// This is useful for determining spacing prior to the first event
					if (firstTempo)
					{
						foreach (var previousRateAlteringEvent in previousEvents)
						{
							previousRateAlteringEvent.UpdateTempo(tc.TempoBPM, lastRowsPerSecond, lastSecondsPerRow);
						}
					}

					minTempo = Math.Min(minTempo, tc.TempoBPM);
					maxTempo = Math.Max(maxTempo, tc.TempoBPM);

					canBeDeleted = !firstTempo;

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
					// Add to the stop time rather than replace it because overlapping
					// negative stops stack in Stepmania.
					stopTimeRemaining += ToSeconds(stop.LengthMicros);
					canBeDeleted = true;
				}
				else if (chartEvent is Warp warp)
				{
					// Intentionally do not stack warps to match Stepmania behavior.
					warpRowsRemaining = Math.Max(warpRowsRemaining, warp.LengthIntegerPosition);
					canBeDeleted = true;
				}
				else if (chartEvent is ScrollRate scrollRate)
				{
					lastScrollRate = scrollRate.Rate;

					// Update any events which precede the first tempo so they can have accurate rates.
					// This is useful for determining spacing prior to the first event
					if (firstScrollRate)
					{
						foreach (var previousRateAlteringEvent in previousEvents)
						{
							previousRateAlteringEvent.UpdateScrollRate(lastScrollRate);
						}
					}

					canBeDeleted = !firstScrollRate;

					firstScrollRate = false;
				}
				else if (chartEvent is TimeSignature timeSignature)
				{
					// Ensure that the time signature falls on a measure boundary.
					// Due to deleting events it may be the case that time signatures are
					// no longer valid and they need to be removed.
					if ((firstTimeSignature && chartEvent.IntegerPosition != 0)
						|| (!firstTimeSignature && chartEvent.IntegerPosition != GetNearestMeasureBoundaryRow(lastTimeSignature, chartEvent.IntegerPosition)))
					{
						invalidTimeSignatures.Add(rae);
						continue;
					}

					canBeDeleted = !firstTimeSignature;

					lastTimeSignature = timeSignature;
					firstTimeSignature = false;
				}

				rae.Init(
					warpRowsRemaining,
					stopTimeRemaining,
					lastScrollRate,
					lastTempo,
					lastRowsPerSecond,
					lastSecondsPerRow,
					lastTimeSignature,
					canBeDeleted);

				previousEvent = rae;
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

			if (invalidTimeSignatures.Count > 0)
			{
				DeleteEvents(invalidTimeSignatures);
			}

			return invalidTimeSignatures;
		}

		private List<EditorEvent> UpdateEventTimingData()
		{
			// First, delete any events which do not correspond to Stepmania chart events.
			// These events may sort to a different relative position based on rate altering
			// event changes. For example, if a stop is extended, that may change the position
			// of the preview since it always occurs at an absolute, with a derived position.
			// We will re-add these events after updating the normal events.
			DeletePreviewEvent();

			// Now, update all time values for all normal notes that correspond to Stepmania chart
			// events. Any of these events, even when added or removed, cannot change the relative
			// order of other such events. As such, we do not need to sort EditorEvents again.
			SetEventTimeMicrosAndMetricPositionsFromRows(EditorEvents.Select(e => e.GetEvent()));
			
			// Now, update all the rate altering events using the updated times. It is possible that
			// this may result in some events being deleted. The only time this can happen is when
			// deleting a time signature that then invalidates a future time signature. This will
			// not invalidate note times or positions.
			var deletedEvents = CleanRateAlteringEvents();

			// Finally, re-add any events we deleted above. When re-adding them, we will derive
			// their positions again using the update timing information.
			AddPreviewEvent();

			return deletedEvents;
		}

		public void DeletePreviewEvent()
		{
			if (PreviewEvent != null)
				DeleteEvent(PreviewEvent);
		}

		public void AddPreviewEvent()
		{
			if (!EditorSong.IsUsingSongForPreview())
				return;
			double previewChartTime = EditorSong.SampleStart + GetMusicOffset();
			double chartPosition = 0.0;
			TryGetChartPositionFromTime(previewChartTime, ref chartPosition);
			PreviewEvent = new EditorPreviewRegionEvent(this, chartPosition);
			AddEvent(PreviewEvent);
		}

		public bool TryGetChartPositionFromTime(double chartTime, ref double chartPosition)
		{
			var rateEvent = FindActiveRateAlteringEventForTime(chartTime, false);
			if (rateEvent == null)
				return false;
			chartPosition = rateEvent.GetChartPositionFromTime(chartTime);
			return true;
		}

		public List<IChartRegion> GetRegionsOverlapping(int row, double chartTime)
		{
			var regions = new List<IChartRegion>();
			var stop = GetStopEventOverlapping(row, chartTime);
			if (stop != null)
				regions.Add(stop);
			var delay = GetDelayEventOverlapping(row, chartTime);
			if (delay != null)
				regions.Add(delay);
			var fake = GetFakeSegmentEventOverlapping(row, chartTime);
			if (fake != null)
				regions.Add(fake);
			var warp = GetWarpEventOverlapping(row, chartTime);
			if (warp != null)
				regions.Add(warp);
			if (PreviewEvent.GetChartTime() <= chartTime && PreviewEvent.GetChartTime() + PreviewEvent.GetRegionDuration() >= chartTime)
				regions.Add(PreviewEvent);
			return regions;
		}

		private EditorStopEvent GetStopEventOverlapping(int row, double chartTime)
		{
			if (Stops == null)
				return null;

			var enumerator = Stops.FindGreatestPreceding(new EditorDummyStopEvent(this, row, chartTime));
			if (enumerator == null)
				return null;
			enumerator.MoveNext();
			if (enumerator.Current.GetChartTime() + enumerator.Current.DoubleValue >= chartTime)
				return enumerator.Current;

			return null;
		}

		private EditorDelayEvent GetDelayEventOverlapping(int row, double chartTime)
		{
			if (Delays == null)
				return null;

			var enumerator = Delays.FindGreatestPreceding(new EditorDummyDelayEvent(this, row, chartTime));
			if (enumerator == null)
				return null;
			enumerator.MoveNext();
			if (enumerator.Current.GetChartTime() + enumerator.Current.DoubleValue >= chartTime)
				return enumerator.Current;

			return null;
		}

		private EditorFakeSegmentEvent GetFakeSegmentEventOverlapping(int row, double chartTime)
		{
			if (Fakes == null)
				return null;

			var enumerator = Fakes.FindGreatestPreceding(new EditorDummyFakeSegmentEvent(this, row, chartTime));
			if (enumerator == null)
				return null;
			enumerator.MoveNext();
			if (enumerator.Current.GetChartTime() + enumerator.Current.DoubleValue >= chartTime)
				return enumerator.Current;

			return null;
		}

		private EditorWarpEvent GetWarpEventOverlapping(int row, double chartTime)
		{
			if (Warps == null)
				return null;

			var enumerator = Warps.FindGreatestPreceding(new EditorDummyWarpEvent(this, row, chartTime));
			if (enumerator == null)
				return null;
			enumerator.MoveNext();
			if (enumerator.Current.GetRow() + enumerator.Current.IntValue >= row)
				return enumerator.Current;

			return null;
		}

		public EditorRateAlteringEvent FindActiveRateAlteringEventForTime(double chartTime, bool allowEqualTo = true)
		{
			if (RateAlteringEvents == null)
				return null;

			// Given the current song time, get the greatest preceding event which alters the rate of rows to time.
			var enumerator = RateAlteringEvents.FindGreatestPreceding(
				new EditorDummyRateAlteringEventWithTime(this, chartTime), allowEqualTo);
			// If there is no preceding event (e.g. SongTime is negative), use the first event.
			if (enumerator == null)
				enumerator = RateAlteringEvents.GetRedBlackTreeEnumerator();
			// If there is still no event then the Chart is misconfigured as it must have at least a Tempo event.
			if (enumerator == null)
				return null;

			// Update the ChartPosition based on the cached rate information.
			enumerator.MoveNext();
			return enumerator.Current;
		}

		public bool TryGetTimeFromChartPosition(double chartPosition, ref double chartTime)
		{
			var rateEvent = FindActiveRateAlteringEventForPosition(chartPosition, false);
			if (rateEvent == null)
				return false;
			chartTime = rateEvent.GetChartTimeFromPosition(chartPosition);
			return true;
		}

		public EditorRateAlteringEvent FindActiveRateAlteringEventForPosition(double chartPosition, bool allowEqualTo = true)
		{
			if (RateAlteringEvents == null)
				return null;

			// Given the current song time, get the greatest preceding event which alters the rate of rows to time.
			var enumerator = RateAlteringEvents.FindGreatestPreceding(
				new EditorDummyRateAlteringEventWithRow(this, chartPosition), allowEqualTo);
			// If there is no preceding event (e.g. ChartPosition is negative), use the first event.
			if (enumerator == null)
				enumerator = RateAlteringEvents.GetRedBlackTreeEnumerator();
			// If there is still no event then the Chart is misconfigured as it must have at least a Tempo event.
			if (enumerator == null)
				return null;

			enumerator.MoveNext();
			return enumerator.Current;
		}

		public int GetNearestMeasureBoundaryRow(int row)
		{
			var rae = FindActiveRateAlteringEventForPosition(row);
			if (rae == null)
				return 0;
			return GetNearestMeasureBoundaryRow(rae.GetTimeSignature(), row);
		}

		private int GetNearestMeasureBoundaryRow(TimeSignature lastTimeSignature, int row)
		{
			var timeSignatureRow = lastTimeSignature.IntegerPosition;
			var beatsPerMeasure = lastTimeSignature.Signature.Numerator;
			var rowsPerBeat = (MaxValidDenominator * NumBeatsPerMeasure * beatsPerMeasure)
							  / lastTimeSignature.Signature.Denominator / beatsPerMeasure;
			var rowsPerMeasure = rowsPerBeat * beatsPerMeasure;
			var previousMeasureRow = timeSignatureRow + ((row - timeSignatureRow) / rowsPerMeasure) * rowsPerMeasure;
			var nextMeasureRow = previousMeasureRow + rowsPerMeasure;
			if (row - previousMeasureRow < nextMeasureRow - row)
				return previousMeasureRow;
			return nextMeasureRow;
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

		/// <summary>
		/// Called when an EditorStopEvent's length is modified.
		/// </summary>
		public void OnStopLengthModified(EditorStopEvent stop, long newLengthMicros)
		{
			// Unfortunately, Stepmania treats negative stops as occurring after notes at the same position
			// and positive notes as occuring before notes at the same position. This means that altering the
			// sign will alter how notes are sorted, which means we need to remove the stop and re-add it in
			// order for the EventTree to sort properly.
			var signChanged = (stop.StopEvent.LengthMicros < 0) != (newLengthMicros < 0);
			if (signChanged)
				DeleteEvent(stop);
			stop.StopEvent.LengthMicros = newLengthMicros;
			if (signChanged)
				AddEvent(stop);

			// Handle updating timing data.
			UpdateEventTimingData();
		}

		/// <summary>
		/// Called when an EditorRateAlteringEvent's properties are modified.
		/// </summary>
		public void OnRateAlteringEventModified(EditorRateAlteringEvent rae)
		{
			UpdateEventTimingData();
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

		/// <summary>
		/// Deletes the given EditorEvent.
		/// This may result in more events being deleted than the ones provided.
		/// </summary>
		/// <param name="editorEvent">EditorEvent to delete.</param>
		/// <returns>List of all deleted EditorEvents</returns>
		public List<EditorEvent> DeleteEvent(EditorEvent editorEvent)
		{
			return DeleteEvents(new List<EditorEvent>() { editorEvent });
		}

		/// <summary>
		/// Deletes the given EditorEvents.
		/// This may result in more events being deleted than the ones provided.
		/// </summary>
		/// <param name="editorEvents">List of all EditorEvents to delete.</param>
		/// <returns>List of all deleted EditorEvents</returns>
		public List<EditorEvent> DeleteEvents(List<EditorEvent> editorEvents)
		{
			List<EditorEvent> allDeletedEvents = new List<EditorEvent>();
			allDeletedEvents.AddRange(editorEvents);

			var deleted = false;
			var rateDirty = false;
			foreach (var editorEvent in editorEvents)
			{
				deleted = EditorEvents.Delete(editorEvent);
				if (!deleted)
				{
					Assert(deleted);
				}

				if (editorEvent.IsMiscEvent())
				{
					deleted = MiscEvents.Delete(editorEvent);
					Assert(deleted);
				}

				if (editorEvent is EditorFakeSegmentEvent fse)
				{
					deleted = Fakes.Delete(fse);
					Assert(deleted);
				}

				else if (editorEvent is EditorRateAlteringEvent rae)
				{
					RateAlteringEvents.Delete(rae);

					if (rae is EditorStopEvent se)
					{
						deleted = Stops.Delete(se);
						Assert(deleted);
					}
					else if (rae is EditorDelayEvent de)
					{
						deleted = Delays.Delete(de);
						Assert(deleted);
					}
					else if (rae is EditorWarpEvent we)
					{
						deleted = Warps.Delete(we);
						Assert(deleted);
					}

					rateDirty = true;
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

			if (rateDirty)
			{
				allDeletedEvents.AddRange(UpdateEventTimingData());
			}

			Editor.OnEventsDeleted();

			return allDeletedEvents;
		}

		public void AddEvent(EditorEvent editorEvent)
		{
			AddEvents(new List<EditorEvent> { editorEvent });
		}

		public void AddEvents(List<EditorEvent> editorEvents)
		{
			var rateDirty = false;
			foreach (var editorEvent in editorEvents)
			{
				EditorEvents.Insert(editorEvent);
				if (editorEvent.IsMiscEvent())
					MiscEvents.Insert(editorEvent);

				if (editorEvent is EditorFakeSegmentEvent fse)
				{
					Fakes.Insert(fse);
				}

				else if (editorEvent is EditorRateAlteringEvent rae)
				{
					RateAlteringEvents.Insert(rae);

					if (rae is EditorStopEvent se)
					{
						Stops.Insert(se);
					}
					else if (rae is EditorDelayEvent de)
					{
						Delays.Insert(de);
					}
					else if (rae is EditorWarpEvent we)
					{
						Warps.Insert(we);
					}

					rateDirty = true;
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
			if (rateDirty)
			{
				UpdateEventTimingData();
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
			var rae = FindActiveRateAlteringEventForPosition(0.0);
			return rae?.GetTempo() ?? DefaultTempo;
		}

		public Fraction GetStartingTimeSignature()
		{
			var rae = FindActiveRateAlteringEventForPosition(0.0);
			return rae?.GetTimeSignature().Signature ?? DefaultTimeSignature;
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
	internal sealed class ChartComparer : IComparer<EditorChart>
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

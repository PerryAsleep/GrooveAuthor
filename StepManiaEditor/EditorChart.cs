﻿using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Fumen;
using Fumen.ChartDefinition;
using StepManiaEditor.AutogenConfig;
using StepManiaEditor.EditorEvents.Containers;
using static StepManiaEditor.Utils;
using static Fumen.Converters.SMCommon;
using static System.Diagnostics.Debug;
using static StepManiaLibrary.Constants;
using static StepManiaEditor.EditorSong;

namespace StepManiaEditor;

/// <summary>
/// Editor representation of a Stepmania chart.
/// An EditorChart is owned by an EditorSong.
/// 
/// EditorChart is not thread-safe. Some actions, like saving, are asynchronous. While asynchronous actions
/// are running edits are forbidden. Call CanBeEdited to determine if the EditorChart can be edited or not.
/// 
/// It is expected that Update is called once per frame.
/// </summary>
internal sealed class EditorChart : Notifier<EditorChart>, Fumen.IObserver<WorkQueue>
{
	/// <summary>
	/// Data saved in the song file as a custom data chunk of Editor-specific data at the Chart level.
	/// </summary>
	private class CustomSaveDataV1
	{
		public double MusicOffset;
		public bool ShouldUseChartMusicOffset;
		public Guid ExpressedChartConfig = ExpressedChartConfigManager.DefaultExpressedChartDynamicConfigGuid;
		public Dictionary<int, EditorPatternEvent.Definition> Patterns;

		/// <summary>
		/// The max player count for the chart.
		/// Save the index because most charts are for 1 player and writing 0 lets us
		/// easily omit the value from serialization.
		/// </summary>
		[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
		public int MaxPlayerIndex;
	}

	/// <summary>
	/// Version of custom data saved to the Chart.
	/// </summary>
	private const int CustomSaveDataVersion = 1;

	public const string NotificationCanEditChanged = "CanEditChanged";
	public const string NotificationDifficultyTypeChanged = "DifficultyTypeChanged";
	public const string NotificationRatingChanged = "RatingChanged";
	public const string NotificationNameChanged = "NameChanged";
	public const string NotificationDescriptionChanged = "DescriptionChanged";
	public const string NotificationMusicChanged = "MusicChanged";
	public const string NotificationMusicOffsetChanged = "MusicOffsetChanged";
	public const string NotificationEventAdded = "EventAdded";
	public const string NotificationEventsAdded = "EventsAdded";
	public const string NotificationEventDeleted = "EventDeleted";
	public const string NotificationEventsDeleted = "EventsDeleted";
	public const string NotificationEventsMoveStart = "EventsMoveStart";
	public const string NotificationEventsMoveEnd = "EventsMoveEnd";
	public const string NotificationAttackRequestEdit = "AttackRequestEdit";
	public const string NotificationPatternRequestEdit = "PatternRequestEdit";
	public const string NotificationTimingChanged = "TimingChanged";
	public const string NotificationMaxPlayersChanged = "MaxPlayersChanged";

	public const double DefaultTempo = 120.0;
	public static readonly Fraction DefaultTimeSignature = new(4, 4);
	public const double DefaultScrollRate = 1.0;
	public const int DefaultTickCount = 4;
	public const int DefaultHitMultiplier = 1;
	public const int DefaultMissMultiplier = 1;
	public const int DefaultRating = 1;

	private const string TagCustomChartData = "ChartData";
	private const string TagCustomChartDataVersion = "ChartDataVersion";

	/// <summary>
	/// Options for serializing and deserializing custom Chart data.
	/// </summary>
	private static readonly JsonSerializerOptions CustomSaveDataSerializationOptions = new()
	{
		Converters =
		{
			new JsonStringEnumConverter(JsonNamingPolicy.CamelCase),
		},
		ReadCommentHandling = JsonCommentHandling.Skip,
		IncludeFields = true,
	};

	/// <summary>
	/// Guid to uniquely identify this Chart;
	/// </summary>
	private readonly Guid Id;

	/// <summary>
	/// Index of this Chart within its song.
	/// </summary>
	private int IndexInSong;

	/// <summary>
	/// WorkQueue for long-running tasks like saving.
	/// </summary>
	private readonly WorkQueue WorkQueue;

	/// <summary>
	/// Extras from the original Chart.
	/// These are saved off into this member so they can be saved back out.
	/// </summary>
	private readonly Extras OriginalChartExtras;

	/// <summary>
	/// EditorSong which owns this EditorChart.
	/// </summary>
	private readonly EditorSong EditorSong;

	/// <summary>
	/// Tree of all EditorEvents.
	/// </summary>
	private EventTree EditorEvents;

	/// <summary>
	/// Tree of all EditorHoldNoteEvents.
	/// </summary>
	private EventTree Holds;

	/// <summary>
	/// Tree of all miscellaneous EditorEvents. See IsMiscEvent.
	/// </summary>
	private EventTree MiscEvents;

	/// <summary>
	/// Tree of all EditorLabelEvents.
	/// </summary>
	private EventTree Labels;

	/// <summary>
	/// Tree of all EditorAttackEvents.
	/// </summary>
	private EventTree Attacks;

	/// <summary>
	/// Tree of all EditorTickCountEvents.
	/// </summary>
	private EventTree TickCounts;

	/// <summary>
	/// Tree of all EditorMultiplierEvents.
	/// </summary>
	private EventTree Multipliers;

	/// <summary>
	/// Tree of all EditorRateAlteringEvents.
	/// </summary>
	private RateAlteringEventTree RateAlteringEvents;

	/// <summary>
	/// Tree of all EditorInterpolatedRateAlteringEvents.
	/// </summary>
	private InterpolatedRateAlteringEventTree InterpolatedScrollRateEvents;

	/// <summary>
	/// IntervalTree of all EditorStopEvents by time. Stop lengths are in time.
	/// </summary>
	private EventIntervalTree<EditorStopEvent> Stops;

	/// <summary>
	/// IntervalTree of all EditorDelayEvents by time. Delay lengths are in time.
	/// </summary>
	private EventIntervalTree<EditorDelayEvent> Delays;

	/// <summary>
	/// IntervalTree of all EditorFakeSegmentEvent by time. Fake lengths are in time.
	/// </summary>
	private EventIntervalTree<EditorFakeSegmentEvent> Fakes;

	/// <summary>
	/// IntervalTree of all EditorWarpEvents by row.
	/// </summary>
	private EventIntervalTree<EditorWarpEvent> Warps;

	/// <summary>
	/// IntervalTree of all EditorPatternEvents by row.
	/// </summary>
	private EventIntervalTree<EditorPatternEvent> Patterns;

	/// <summary>
	/// The EditorSelectedRowsEvent.
	/// </summary>
	private EditorSelectedRowsEvent SelectedRows;

	/// <summary>
	/// The start row when performing a row-based selection.
	/// </summary>
	private int SelectedRowsStartRow;

	/// <summary>
	/// The EditorPreviewRegionEvent.
	/// </summary>
	private EditorPreviewRegionEvent PreviewEvent;

	/// <summary>
	/// The EditorLastSecondHintEvent.
	/// </summary>
	private EditorLastSecondHintEvent LastSecondHintEvent;

	/// <summary>
	/// Whether this EditorChart uses a distinct display tempo from the EditorSong.
	/// </summary>
	private readonly bool DisplayTempoFromChart;

	/// <summary>
	/// Cached most common tempo in bpm.
	/// </summary>
	private double MostCommonTempo;

	/// <summary>
	/// Cached minimum tempo in bpm.
	/// </summary>
	private double MinTempo;

	/// <summary>
	/// Cached maximum tempo in bpm;
	/// </summary>
	private double MaxTempo;

	/// <summary>
	/// Number of inputs.
	/// </summary>
	public readonly int NumInputs;

	/// <summary>
	/// Default number of players for this type of chart.
	/// </summary>
	public readonly int DefaultNumPlayers;

	/// <summary>
	/// Cached step totals.
	/// </summary>
	private readonly StepTotals StepTotals;

	#region Properties

	public ChartType ChartType => ChartTypeInternal;
	private readonly ChartType ChartTypeInternal;

	public ChartDifficultyType ChartDifficultyType
	{
		get => ChartDifficultyTypeInternal;
		set
		{
			Assert(CanBeEdited());
			if (!CanBeEdited())
				return;
			if (ChartDifficultyTypeInternal == value)
				return;
			ChartDifficultyTypeInternal = value;
			Notify(NotificationDifficultyTypeChanged, this);
		}
	}

	private ChartDifficultyType ChartDifficultyTypeInternal;

	public int Rating
	{
		get => RatingInternal;
		set
		{
			Assert(CanBeEdited());
			if (!CanBeEdited())
				return;
			if (RatingInternal == value)
				return;
			RatingInternal = value;
			Notify(NotificationRatingChanged, this);
		}
	}

	private int RatingInternal;

	public string Name
	{
		get => NameInternal;
		set
		{
			Assert(CanBeEdited());
			if (!CanBeEdited())
				return;
			if (NameInternal == value)
				return;
			NameInternal = value;
			Notify(NotificationNameChanged, this);
		}
	}

	private string NameInternal;

	public string Description
	{
		get => DescriptionInternal;
		set
		{
			Assert(CanBeEdited());
			if (!CanBeEdited())
				return;
			if (DescriptionInternal == value)
				return;
			DescriptionInternal = value;
			Notify(NotificationDescriptionChanged, this);
		}
	}

	private string DescriptionInternal;

	public string Style
	{
		get => StyleInternal;
		set
		{
			Assert(CanBeEdited());
			if (!CanBeEdited())
				return;
			StyleInternal = value;
		}
	}

	private string StyleInternal;

	public string Credit
	{
		get => CreditInternal;
		set
		{
			Assert(CanBeEdited());
			if (!CanBeEdited())
				return;
			CreditInternal = value;
		}
	}

	private string CreditInternal;

	public string MusicPath
	{
		get => MusicPathInternal;
		set
		{
			Assert(CanBeEdited());
			if (!CanBeEdited())
				return;
			var newMusicPath = value ?? "";
			if (MusicPath == newMusicPath)
				return;
			MusicPathInternal = newMusicPath;
			Notify(NotificationMusicChanged, this);
		}
	}

	private string MusicPathInternal;

	public bool UsesChartMusicOffset
	{
		get => UsesChartMusicOffsetInternal;
		set
		{
			Assert(CanBeEdited());
			if (!CanBeEdited())
				return;
			if (UsesChartMusicOffsetInternal != value)
			{
				var deleted = DeletePreviewEvent();
				UsesChartMusicOffsetInternal = value;
				if (deleted)
					AddPreviewEvent();
				Notify(NotificationMusicOffsetChanged, this);
			}
		}
	}

	private bool UsesChartMusicOffsetInternal;

	public double MusicOffset
	{
		get => MusicOffsetInternal;
		set
		{
			Assert(CanBeEdited());
			if (!CanBeEdited())
				return;
			if (!MusicOffsetInternal.DoubleEquals(value))
			{
				var deleted = DeletePreviewEvent();
				MusicOffsetInternal = value;
				if (deleted)
					AddPreviewEvent();
				Notify(NotificationMusicOffsetChanged, this);
			}
		}
	}

	private double MusicOffsetInternal;

	public DisplayTempoMode DisplayTempoMode
	{
		get => DisplayTempo.Mode;
		set
		{
			Assert(CanBeEdited());
			if (!CanBeEdited())
				return;
			DisplayTempo.Mode = value;
		}
	}

	public double DisplayTempoSpecifiedTempoMin
	{
		get => DisplayTempo.SpecifiedTempoMin;
		set
		{
			Assert(CanBeEdited());
			if (!CanBeEdited())
				return;
			DisplayTempo.SpecifiedTempoMin = value;
		}
	}

	public double DisplayTempoSpecifiedTempoMax
	{
		get => DisplayTempo.SpecifiedTempoMax;
		set
		{
			Assert(CanBeEdited());
			if (!CanBeEdited())
				return;
			DisplayTempo.SpecifiedTempoMax = value;
		}
	}

	public bool DisplayTempoShouldAllowEditsOfMax
	{
		get => DisplayTempo.ShouldAllowEditsOfMax;
		set
		{
			Assert(CanBeEdited());
			if (!CanBeEdited())
				return;
			DisplayTempo.ShouldAllowEditsOfMax = value;
		}
	}

	private DisplayTempo DisplayTempo = new();

	public Guid ExpressedChartConfig
	{
		get => ExpressedChartConfigInternal;
		set
		{
			Assert(CanBeEdited());
			if (!CanBeEdited())
				return;
			if (ExpressedChartConfigManager.Instance.GetConfig(value) != null)
				ExpressedChartConfigInternal = value;
		}
	}

	private Guid ExpressedChartConfigInternal;

	public int MaxPlayers
	{
		get => MaxPlayersInternal;
		set
		{
			Assert(CanBeEdited());
			if (!CanBeEdited())
				return;

			if (SupportsVariableNumberOfPlayers())
			{
				var newMaxPlayers = Math.Max(value, Math.Max(1, StepTotals.GetNumPlayersWithNotes()));
				if (MaxPlayersInternal != newMaxPlayers)
				{
					MaxPlayersInternal = newMaxPlayers;
					Notify(NotificationMaxPlayersChanged, this);
				}
			}
			else
			{
				// Ignore the input value. This chart does not support a variable number of players.
				// The number of players can only ever be the specified number for the chart type.
				if (MaxPlayersInternal != DefaultNumPlayers)
				{
					MaxPlayersInternal = DefaultNumPlayers;
					Notify(NotificationMaxPlayersChanged, this);
				}
			}
		}
	}

	private int MaxPlayersInternal;

	#endregion Properties

	#region Constructors

	/// <summary>
	/// Construct an EditorChart from the given Stepmania Chart.
	/// Can throw an exception if the Chart is malformed.
	/// </summary>
	/// <param name="editorSong">Parent EditorSong.</param>
	/// <param name="chart">Chart to use.</param>
	public EditorChart(EditorSong editorSong, Chart chart)
	{
		Id = Guid.NewGuid();
		WorkQueue = new WorkQueue();
		WorkQueue.AddObserver(this);
		ExpressedChartConfigInternal = ExpressedChartConfigManager.DefaultExpressedChartDynamicConfigGuid;

		OriginalChartExtras = chart.Extras;
		EditorSong = editorSong;

		TryGetChartType(chart.Type, out ChartTypeInternal);
		if (Enum.TryParse(chart.DifficultyType, out ChartDifficultyType parsedChartDifficultyType))
			ChartDifficultyTypeInternal = parsedChartDifficultyType;
		RatingInternal = (int)chart.DifficultyRating;

		var chartProperties = GetChartProperties(ChartType);
		NumInputs = chartProperties.GetNumInputs();
		StepTotals = new StepTotals(this);
		DefaultNumPlayers = chartProperties.GetNumPlayers();
		MaxPlayers = DefaultNumPlayers;

		chart.Extras.TryGetExtra(TagChartName, out string parsedName, true);
		NameInternal = parsedName ?? "";
		DescriptionInternal = chart.Description ?? "";
		chart.Extras.TryGetExtra(TagChartStyle, out StyleInternal, true); // Pad or Keyboard
		StyleInternal ??= "";
		CreditInternal = chart.Author ?? "";
		chart.Extras.TryGetExtra(TagMusic, out string musicPath, true);
		MusicPathInternal = musicPath;

		// Only set this chart to be using an explicit offset if it both has an offset
		// and that offset is different from the song's offset. It is common for charts
		// to have explicit offsets that are copies of the song's, and in those cases we
		// don't want to treat the chart as overriding the song.
		var chartHasExplicitMusicOffset = chart.Extras.TryGetExtra(TagOffset, out double musicOffset, true);
		if (chartHasExplicitMusicOffset)
		{
			if (!musicOffset.DoubleEquals(editorSong.MusicOffset))
			{
				UsesChartMusicOffsetInternal = true;
				MusicOffsetInternal = musicOffset;
			}
		}

		DisplayTempoFromChart = chart.Extras.TryGetExtra(TagDisplayBPM, out string chartDisplayBpm, true);
		if (DisplayTempoFromChart)
			DisplayTempo.FromString(chartDisplayBpm);

		SetUpEditorEvents(chart);

		DeserializeCustomChartData(chart);

		// Construct StepDensity after setting up EditorEvents.
		StepTotals.InitializeStepDensity();
	}

	/// <summary>
	/// Construct an EditorChart of the given ChartType.
	/// Will create the minimum set of needed events for a valid chart.
	/// </summary>
	/// <param name="editorSong">Parent EditorSong.</param>
	/// <param name="chartType">ChartType to create.</param>
	public EditorChart(EditorSong editorSong, ChartType chartType)
	{
		Id = Guid.NewGuid();
		WorkQueue = new WorkQueue();
		WorkQueue.AddObserver(this);

		ExpressedChartConfigInternal = ExpressedChartConfigManager.DefaultExpressedChartDynamicConfigGuid;

		EditorSong = editorSong;
		ChartTypeInternal = chartType;

		var chartProperties = GetChartProperties(ChartType);
		NumInputs = chartProperties.GetNumInputs();
		StepTotals = new StepTotals(this);
		DefaultNumPlayers = chartProperties.GetNumPlayers();
		MaxPlayers = DefaultNumPlayers;

		Name = "";
		Description = "";
		Style = "";
		Credit = "";
		MusicPath = "";
		UsesChartMusicOffset = false;
		DisplayTempoFromChart = false;

		Rating = DefaultRating;

		var tempChart = new Chart();
		var tempLayer = new Layer();
		tempLayer.Events.Add(CreateDefaultTimeSignature(EditorSong));
		tempLayer.Events.Add(CreateDefaultTempo(EditorSong));

		var isSmFileType = editorSong.GetFileFormat() != null && editorSong.GetFileFormat().Type == FileFormatType.SM;
		if (!isSmFileType)
		{
			tempLayer.Events.Add(CreateDefaultScrollRate());
			tempLayer.Events.Add(CreateDefaultScrollRateInterpolation());
			tempLayer.Events.Add(CreateDefaultTickCount());
			tempLayer.Events.Add(CreateDefaultMultipliers());
		}

		tempChart.Layers.Add(tempLayer);
		SetUpEditorEvents(tempChart);

		// Construct StepDensity after setting up EditorEvents.
		StepTotals.InitializeStepDensity();
	}

	/// <summary>
	/// Copy Constructor.
	/// Will clone the given EditorChart's events.
	/// </summary>
	/// <param name="other">Other EditorChart to clone.</param>
	public EditorChart(EditorChart other)
	{
		Id = Guid.NewGuid();
		WorkQueue = new WorkQueue();
		WorkQueue.AddObserver(this);

		ExpressedChartConfigInternal = other.ExpressedChartConfigInternal;

		EditorSong = other.EditorSong;
		ChartTypeInternal = other.ChartTypeInternal;

		NumInputs = other.NumInputs;
		StepTotals = new StepTotals(this);
		DefaultNumPlayers = other.DefaultNumPlayers;
		MaxPlayers = other.MaxPlayers;

		Name = other.Name;
		Description = other.Description;
		Style = other.Style;
		Credit = other.Credit;
		MusicPath = other.MusicPath;
		UsesChartMusicOffset = other.UsesChartMusicOffsetInternal;
		DisplayTempoFromChart = other.DisplayTempoFromChart;
		ChartDifficultyTypeInternal = other.ChartDifficultyTypeInternal;

		Rating = other.Rating;

		SetUpEditorEvents(other);

		// Construct StepDensity after setting up EditorEvents.
		StepTotals.InitializeStepDensity();
	}

	/// <summary>
	/// Sets up this EditorChart's EditorEvent data structures from a Stepmania Chart.
	/// </summary>
	/// <param name="chart">Stepmania Chart to use for creating EditorEvents.</param>
	private void SetUpEditorEvents(Chart chart)
	{
		var editorEvents = new EventTree(this);
		var holds = new EventTree(this);
		var rateAlteringEvents = new RateAlteringEventTree(this);
		var interpolatedScrollRateEvents = new InterpolatedRateAlteringEventTree(this);
		var miscEvents = new EventTree(this);
		var labels = new EventTree(this);
		var attacks = new EventTree(this);
		var tickCounts = new EventTree(this);
		var multipliers = new EventTree(this);
		var maxPlayer = 1;

		var pendingHoldStarts = new LaneHoldStartNote[NumInputs];
		var lastScrollRateInterpolationValue = 1.0;
		var firstInterpolatedScrollRate = true;

		// We expect there to be events of these types at row 0.
		// If we don't find them, we will create defaults.
		var foundFirstTimeSignature = false;
		var foundFirstTempo = false;

		// Helper method for adding an EditorEvent to this method's local data structures.
		void AddLocalEvent(Event chartEvent, EditorEvent editorEvent)
		{
			// Remove all extras. We do not want any extra information from the events to be used
			// when we save. Since we copy/paste events this could result in position information
			// from an old event overriding the new position on a pasted event.
			chartEvent?.Extras.Clear();

			if (editorEvent != null)
				editorEvents.Insert(editorEvent);

			StepTotals.OnEventAdded(editorEvent);

			switch (editorEvent)
			{
				case EditorRateAlteringEvent rae:
					rateAlteringEvents.Insert(rae);
					break;
				case EditorInterpolatedRateAlteringEvent irae:
				{
					if (chartEvent is ScrollRateInterpolation scrollRateInterpolation)
					{
						// For the first scroll rate event, set the previous rate to the first rate so we use the
						// first scroll rate when consider positions and times before 0.0. See also
						// OnInterpolatedRateAlteringEventModified.
						irae.PreviousScrollRate = firstInterpolatedScrollRate
							? scrollRateInterpolation.Rate
							: lastScrollRateInterpolationValue;
						interpolatedScrollRateEvents.Insert(irae);
						lastScrollRateInterpolationValue = scrollRateInterpolation.Rate;

						firstInterpolatedScrollRate = false;
					}

					break;
				}
			}

			if (editorEvent != null && editorEvent.IsMiscEvent())
				miscEvents.Insert(editorEvent);

			editorEvent?.OnAddedToChart();
		}

		// Loop over every Event, creating EditorEvents from them.
		for (var eventIndex = 0; eventIndex < chart.Layers[0].Events.Count; eventIndex++)
		{
			var chartEvent = chart.Layers[0].Events[eventIndex];
			EditorEvent editorEvent;

			if (chartEvent is Note n)
				maxPlayer = Math.Max(maxPlayer, n.Player);

			switch (chartEvent)
			{
				case TimeSignature ts:
				{
					if (!foundFirstTimeSignature && ts.IntegerPosition == 0)
						foundFirstTimeSignature = true;
					editorEvent = EditorEvent.CreateEvent(EventConfig.CreateConfig(this, chartEvent, false));
					break;
				}
				case Tempo t:
				{
					if (!foundFirstTempo && t.IntegerPosition == 0)
						foundFirstTempo = true;
					if (t.TempoBPM < EditorTempoEvent.MinTempo)
					{
						LogWarn(
							$"Tempo {t.TempoBPM} at row {t.IntegerPosition} is below the minimum tempo of {EditorTempoEvent.MinTempo}. Clamping to {EditorTempoEvent.MinTempo}.");
						t.TempoBPM = EditorTempoEvent.MinTempo;
					}

					editorEvent = EditorEvent.CreateEvent(EventConfig.CreateConfig(this, chartEvent, false));
					break;
				}
				case LaneHoldStartNote hsn:
				{
					pendingHoldStarts[hsn.Lane] = hsn;
					continue;
				}
				case LaneHoldEndNote hen:
				{
					editorEvent =
						EditorEvent.CreateEvent(EventConfig.CreateHoldConfig(this, pendingHoldStarts[hen.Lane], hen, false));
					pendingHoldStarts[hen.Lane] = null;
					holds.Insert(editorEvent);
					break;
				}
				case Label:
				{
					editorEvent = EditorEvent.CreateEvent(EventConfig.CreateConfig(this, chartEvent, false));
					labels.Insert(editorEvent);
					break;
				}
				case FakeSegment f:
				{
					if (f.LengthIntegerPosition < EditorFakeSegmentEvent.MinFakeSegmentLength)
					{
						LogWarn(
							$"Fake Segment {f.LengthIntegerPosition} at row {f.IntegerPosition} is below the minimum length of {EditorFakeSegmentEvent.MinFakeSegmentLength} rows. Clamping to {EditorFakeSegmentEvent.MinFakeSegmentLength}.");
						f.LengthIntegerPosition = EditorFakeSegmentEvent.MinFakeSegmentLength;
					}

					editorEvent = EditorEvent.CreateEvent(EventConfig.CreateConfig(this, chartEvent, false));
					break;
				}
				case Attack:
				{
					editorEvent = EditorEvent.CreateEvent(EventConfig.CreateConfig(this, chartEvent, false));
					attacks.Insert(editorEvent);
					break;
				}
				case TickCount:
				{
					editorEvent = EditorEvent.CreateEvent(EventConfig.CreateConfig(this, chartEvent, false));
					tickCounts.Insert(editorEvent);
					break;
				}
				case Fumen.ChartDefinition.Multipliers:
				{
					editorEvent = EditorEvent.CreateEvent(EventConfig.CreateConfig(this, chartEvent, false));
					multipliers.Insert(editorEvent);
					break;
				}
				default:
				{
					editorEvent = EditorEvent.CreateEvent(EventConfig.CreateConfig(this, chartEvent, false));
					break;
				}
			}

			AddLocalEvent(chartEvent, editorEvent);
		}

		// Ensure needed first events are present.
		var modifiedChart = false;
		{
			// The StepManiaLibrary loading ensures that the chart has valid Time Signatures.
			// This should never happen, but if it does throw an exception. Adding a default Time Signature is non-trivial.
			if (!foundFirstTimeSignature)
			{
				throw new Exception("No Time Signature found at row 0.");
			}

			if (!foundFirstTempo)
			{
				var chartEvent = CreateDefaultTempo(EditorSong);
				var editorEvent = EditorEvent.CreateEvent(EventConfig.CreateConfig(this, chartEvent, false));
				LogWarn($"No Tempo found at row 0. Adding {chartEvent.TempoBPM} BPM Tempo at row 0.");
				AddLocalEvent(chartEvent, editorEvent);
				modifiedChart = true;
			}
		}

		EditorEvents = editorEvents;
		Holds = holds;
		RateAlteringEvents = rateAlteringEvents;
		InterpolatedScrollRateEvents = interpolatedScrollRateEvents;
		MiscEvents = miscEvents;
		Labels = labels;
		Attacks = attacks;
		TickCounts = tickCounts;
		Multipliers = multipliers;

		if (maxPlayer + 1 > MaxPlayers)
			MaxPlayers = maxPlayer + 1;

		// TODO: Optimize.
		// Ideally we only need to do one loop over all the notes. But that means either duplicating much of
		// the complicated logic around row-dependent data, or figuring out a way to neatly capture it in one
		// space without needing a second loop. For now, just refresh all timing data. This ensures that complicated
		// row dependent data like time signature coloring and fake determination are correct.
		RefreshEventTimingData();

		// Create events that are not derived from the Chart's Events.
		AddPreviewEvent();
		AddLastSecondHintEvent();

		if (modifiedChart)
		{
			ActionQueue.Instance.SetHasUnsavedChanges();
		}
	}

	/// <summary>
	/// Sets up this EditorChart's EditorEvent data structures from another EditorChart.
	/// </summary>
	/// <param name="other">Other EditorChart to use for copying EditorEvents.</param>
	private void SetUpEditorEvents(EditorChart other)
	{
		var events = new List<EditorEvent>();
		foreach (var chartEvent in other.GetEvents())
		{
			if (chartEvent is EditorSelectedRowsEvent)
				continue;
			events.Add(chartEvent.Clone(this));
		}

		var editorEvents = new EventTree(this);
		var holds = new EventTree(this);
		var rateAlteringEvents = new RateAlteringEventTree(this);
		var interpolatedScrollRateEvents = new InterpolatedRateAlteringEventTree(this);
		var miscEvents = new EventTree(this);
		var labels = new EventTree(this);
		var attacks = new EventTree(this);
		var tickCounts = new EventTree(this);
		var multipliers = new EventTree(this);

		foreach (var editorEvent in events)
		{
			editorEvents.Insert(editorEvent);
			StepTotals.OnEventAdded(editorEvent);
			if (editorEvent is EditorRateAlteringEvent rae)
				rateAlteringEvents.Insert(rae);
			if (editorEvent.IsMiscEvent())
				miscEvents.Insert(editorEvent);
			if (editorEvent is EditorPreviewRegionEvent pe)
				PreviewEvent = pe;
			if (editorEvent is EditorLastSecondHintEvent lse)
				LastSecondHintEvent = lse;
			if (editorEvent is EditorLabelEvent)
				labels.Insert(editorEvent);
			if (editorEvent is EditorAttackEvent)
				attacks.Insert(editorEvent);
			if (editorEvent is EditorTickCountEvent)
				tickCounts.Insert(editorEvent);
			if (editorEvent is EditorMultipliersEvent)
				multipliers.Insert(editorEvent);
			editorEvent.OnAddedToChart();
		}

		EditorEvents = editorEvents;
		Holds = holds;
		RateAlteringEvents = rateAlteringEvents;
		InterpolatedScrollRateEvents = interpolatedScrollRateEvents;
		MiscEvents = miscEvents;
		Labels = labels;
		Attacks = attacks;
		TickCounts = tickCounts;
		Multipliers = multipliers;

		RefreshIntervals();
		RefreshRateAlteringEvents();
	}

	private static TimeSignature CreateDefaultTimeSignature(EditorSong editorSong)
	{
		return new TimeSignature(editorSong.GetBestChartStartingTimeSignature(), 0);
	}

	private static Tempo CreateDefaultTempo(EditorSong editorSong)
	{
		return new Tempo(editorSong.GetBestChartStartingTempo());
	}

	private static ScrollRate CreateDefaultScrollRate()
	{
		return new ScrollRate(DefaultScrollRate);
	}

	private static ScrollRateInterpolation CreateDefaultScrollRateInterpolation()
	{
		return new ScrollRateInterpolation(DefaultScrollRate, 0, 0.0, false);
	}

	private static TickCount CreateDefaultTickCount()
	{
		return new TickCount(DefaultTickCount);
	}

	private static Multipliers CreateDefaultMultipliers()
	{
		return new Multipliers(DefaultHitMultiplier, DefaultMissMultiplier);
	}

	#endregion Constructors

	#region Clean-up

	public void RemoveObservers()
	{
		WorkQueue.RemoveObserver(this);
		StepTotals?.RemoveObservers();
	}

	#endregion Clean-up

	#region Idenfitication

	public Guid GetGuid()
	{
		return Id;
	}

	public int GetIndexInSong()
	{
		return IndexInSong;
	}

	public void SetIndexInSong(int indexInSong)
	{
		IndexInSong = indexInSong;
	}

	#endregion Identification

	#region Accessors

	public IReadOnlyEventTree GetEvents()
	{
		return EditorEvents;
	}

	public IReadOnlyEventTree GetHolds()
	{
		return Holds;
	}

	public IReadOnlyEventTree GetMiscEvents()
	{
		return MiscEvents;
	}

	public IReadOnlyEventTree GetLabels()
	{
		return Labels;
	}

	public IReadOnlyEventTree GetAttacks()
	{
		return Attacks;
	}

	public IReadOnlyEventTree GetTickCounts()
	{
		return TickCounts;
	}

	public IReadOnlyEventTree GetMultipliers()
	{
		return Multipliers;
	}

	public IReadOnlyRateAlteringEventTree GetRateAlteringEvents()
	{
		return RateAlteringEvents;
	}

	public IReadOnlyInterpolatedRateAlteringEventTree GetInterpolatedScrollRateEvents()
	{
		return InterpolatedScrollRateEvents;
	}

	public IReadOnlyEventIntervalTree<EditorStopEvent> GetStops()
	{
		return Stops;
	}

	public IReadOnlyEventIntervalTree<EditorDelayEvent> GetDelays()
	{
		return Delays;
	}

	public IReadOnlyEventIntervalTree<EditorFakeSegmentEvent> GetFakes()
	{
		return Fakes;
	}

	public IReadOnlyEventIntervalTree<EditorWarpEvent> GetWarps()
	{
		return Warps;
	}

	public IReadOnlyEventIntervalTree<EditorPatternEvent> GetPatterns()
	{
		return Patterns;
	}

	public EditorPreviewRegionEvent GetPreview()
	{
		return PreviewEvent;
	}

	public EditorSong GetEditorSong()
	{
		return EditorSong;
	}

	public List<EditorChart> GetAllOtherEditorCharts()
	{
		var allOtherCharts = new List<EditorChart>();
		foreach (var songChart in EditorSong!.GetCharts())
		{
			if (songChart == this)
				continue;
			allOtherCharts.Add(songChart);
		}

		return allOtherCharts;
	}

	public double GetMusicOffset()
	{
		if (UsesChartMusicOffset)
			return MusicOffset;
		return EditorSong.MusicOffset;
	}

	public double GetStartingTempo()
	{
		var rae = RateAlteringEvents?.FindActiveRateAlteringEventForPosition(0.0);
		return rae?.GetTempo() ?? DefaultTempo;
	}

	public bool HasDisplayTempoFromChart()
	{
		return DisplayTempoFromChart;
	}

	public double GetMostCommonTempo()
	{
		return MostCommonTempo;
	}

	public double GetMinTempo()
	{
		return MinTempo;
	}

	public double GetMaxTempo()
	{
		return MaxTempo;
	}

	public Fraction GetStartingTimeSignature()
	{
		var rae = RateAlteringEvents?.FindActiveRateAlteringEventForPosition(0.0);
		return rae?.GetTimeSignature().GetSignature() ?? DefaultTimeSignature;
	}

	public string GetShortName()
	{
		return $"{ImGuiUtils.GetPrettyEnumString(ChartType)} {ImGuiUtils.GetPrettyEnumString(ChartDifficultyType)}";
	}

	public string GetDescriptiveName()
	{
		if (string.IsNullOrEmpty(Description))
			return
				$"{ImGuiUtils.GetPrettyEnumString(ChartType)} {ImGuiUtils.GetPrettyEnumString(ChartDifficultyType)} [{Rating}]";
		return
			$"{ImGuiUtils.GetPrettyEnumString(ChartType)} {ImGuiUtils.GetPrettyEnumString(ChartDifficultyType)} [{Rating}] {Description}";
	}

	public bool HasPatterns()
	{
		return Patterns?.GetCount() > 0;
	}

	public StepDensity GetStepDensity()
	{
		return StepTotals.GetStepDensity();
	}

	public bool IsMultiPlayer()
	{
		return DefaultNumPlayers > 1;
	}

	public bool SupportsVariableNumberOfPlayers()
	{
		return GetChartProperties(ChartType).GetSupportsVariableNumberOfPlayers();
	}

	#endregion Accessors

	#region Timing Updates

	/// <summary>
	/// Updates all EditorRateAlteringEvents rate tracking values.
	/// This assumes the EditorRateAlteringEvents have correct row and time values.
	/// </summary>
	private void RefreshRateAlteringEvents()
	{
		// It is possible for there to be no rate altering events in the middle of undoing a paste of events
		// which include all rate altering events for a chart. This a momentary state which will correct itself
		// as the paste completes and its side effects are resolved and the original rate altering events are
		// re-added.
		if (RateAlteringEvents.GetCount() == 0)
			return;

		var lastScrollRate = 1.0;
		var lastTempo = 1.0;
		var firstTempo = true;
		var firstTimeSignature = true;
		var firstScrollRate = true;
		EditorTimeSignatureEvent lastTimeSignature = null;
		var timePerTempo = new Dictionary<double, double>();
		var lastTempoChangeTime = 0.0;
		var minTempo = double.MaxValue;
		var maxTempo = double.MinValue;

		var warpRowsRemaining = 0;
		var stopTimeRemaining = 0.0;
		var lastRowsPerSecond = 1.0;
		var lastSecondsPerRow = 1.0;

		EditorRateAlteringEvent previousEvent = null;
		var firstEnumerator = RateAlteringEvents.First();
		if (firstEnumerator != null)
		{
			firstEnumerator.MoveNext();
			previousEvent = firstEnumerator.Current;
		}

		var previousEvents = new List<EditorRateAlteringEvent>();

		foreach (var rae in RateAlteringEvents)
		{
			// Adjust warp rows remaining.
			// ReSharper disable once PossibleNullReferenceException
			warpRowsRemaining = Math.Max(0, warpRowsRemaining - (rae.GetRow() - previousEvent.GetRow()));
			// Adjust stop timing remaining.
			if (stopTimeRemaining != 0.0)
			{
				// In most cases with a non-zero stop time remaining, the stop time remaining is positive.
				// In those cases, the following events have already been adjusted such that their time
				// takes into account the stop time, and they should have 0.0 for their stop time remaining.
				// For negative stops however, we need to keep incrementing the stop time remaining until it
				// hits 0.0. To do this we need to add the time which would have elapsed between the last
				// event and this event if there were no stop. This is derived from their row difference
				// and the seconds per row.
				var rowsSincePrevious = rae.GetRow() - previousEvent.GetRow();
				var stopTimeSincePrevious = rowsSincePrevious * lastSecondsPerRow;
				stopTimeRemaining = Math.Min(0.0, stopTimeRemaining + stopTimeSincePrevious);
			}

			var isPositionImmutable = false;

			switch (rae)
			{
				case EditorTempoEvent tc:
				{
					var bpm = tc.GetTempo();
					lastSecondsPerRow = tc.GetSecondsPerRow(MaxValidDenominator);
					lastRowsPerSecond = tc.GetRowsPerSecond(MaxValidDenominator);

					// Update any events which precede the first tempo so they can have accurate rates.
					// This is useful for determining spacing prior to the first event
					if (firstTempo)
					{
						foreach (var previousRateAlteringEvent in previousEvents)
						{
							previousRateAlteringEvent.UpdateTempo(bpm, lastRowsPerSecond, lastSecondsPerRow);
						}
					}

					minTempo = Math.Min(minTempo, bpm);
					maxTempo = Math.Max(maxTempo, bpm);

					isPositionImmutable = firstTempo;

					if (!firstTempo)
					{
						timePerTempo.TryGetValue(lastTempo, out var currentTempoTime);
						timePerTempo[lastTempo] = currentTempoTime + tc.GetChartTime() - lastTempoChangeTime;
						lastTempoChangeTime = tc.GetChartTime();
					}

					lastTempo = bpm;
					firstTempo = false;
					break;
				}
				case EditorDelayEvent delay:
				{
					// Add to the stop time rather than replace it because overlapping
					// negative stops stack in Stepmania.
					stopTimeRemaining += delay.GetDelayLengthSeconds();
					break;
				}
				case EditorStopEvent stop:
				{
					// Add to the stop time rather than replace it because overlapping
					// negative stops stack in Stepmania.
					stopTimeRemaining += stop.GetStopLengthSeconds();
					break;
				}
				case EditorWarpEvent warp:
				{
					// Intentionally do not stack warps to match Stepmania behavior.
					warpRowsRemaining = Math.Max(warpRowsRemaining, warp.GetWarpLengthRows());
					break;
				}
				case EditorScrollRateEvent scrollRate:
				{
					lastScrollRate = scrollRate.GetScrollRate();

					// Update any events which precede the first tempo so they can have accurate rates.
					// This is useful for determining spacing prior to the first event
					if (firstScrollRate)
					{
						foreach (var previousRateAlteringEvent in previousEvents)
						{
							previousRateAlteringEvent.UpdateScrollRate(lastScrollRate);
						}
					}

					firstScrollRate = false;
					break;
				}
				case EditorTimeSignatureEvent timeSignature:
				{
					// Update any events which precede the first time signature so they can have accurate
					// row-dependent data.
					if (firstTimeSignature)
					{
						foreach (var previousRateAlteringEvent in previousEvents)
						{
							previousRateAlteringEvent.UpdateLastTimeSignature(timeSignature);
						}
					}

					isPositionImmutable = firstTimeSignature;
					lastTimeSignature = timeSignature;
					firstTimeSignature = false;
					break;
				}
			}

			rae.Init(
				warpRowsRemaining,
				stopTimeRemaining,
				lastScrollRate,
				lastTempo,
				lastRowsPerSecond,
				lastSecondsPerRow,
				lastTimeSignature,
				isPositionImmutable);

			previousEvent = rae;
			previousEvents.Add(rae);
		}

		timePerTempo.TryGetValue(lastTempo, out var lastTempoTime);
		// ReSharper disable once PossibleNullReferenceException
		timePerTempo[lastTempo] = lastTempoTime + previousEvent.GetChartTime() - lastTempoChangeTime;

		var longestTempoTime = -1.0;
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

	private void RefreshIntervals()
	{
		var stops = new EventIntervalTree<EditorStopEvent>();
		var delays = new EventIntervalTree<EditorDelayEvent>();
		var fakes = new EventIntervalTree<EditorFakeSegmentEvent>();
		var warps = new EventIntervalTree<EditorWarpEvent>();
		var patterns = new EventIntervalTree<EditorPatternEvent>();

		foreach (var editorEvent in EditorEvents)
		{
			switch (editorEvent)
			{
				case EditorFakeSegmentEvent fse:
					fakes.Insert(fse, fse.GetChartPosition(), fse.GetEndChartPosition());
					break;
				case EditorStopEvent se:
					stops.Insert(se, se.GetChartTime(), Math.Max(se.GetChartTime(), se.GetEndChartTime()));
					break;
				case EditorDelayEvent de:
					delays.Insert(de, de.GetChartTime(), Math.Max(de.GetChartTime(), de.GetEndChartTime()));
					break;
				case EditorWarpEvent we:
					warps.Insert(we, we.GetChartPosition(), we.GetEndChartPosition());
					break;
				case EditorPatternEvent pe:
					patterns.Insert(pe, pe.GetChartPosition(), pe.GetEndChartPosition());
					break;
			}
		}

		Stops = stops;
		Delays = delays;
		Fakes = fakes;
		Warps = warps;
		Patterns = patterns;
	}

	/// <summary>
	/// Updates the times and other row dependent data in all EditorEvents in this EditorChart.
	/// If EditorRateAlteringEvents like stops are modified, they affect the timing of all following events.
	/// This function will ensure all Events have correct times and other row dependent data and that
	/// all events are sorted properly when a rate altering event is changed.
	/// </summary>
	/// <remarks>
	/// This method contains logic which duplicates much of the logic in StepManiaLibrary's
	/// SetEventTimeFromRows method. While isn't ideal to have the complicated logic
	/// around warp and stop time accrual in multiple places, we need to set some information that
	/// the library doesn't care about (note coloring data like each step's row relative to its
	/// starting measure and each step's time signature denominator). It's better to only do one
	/// pass and compute exactly what we need rather than trying leveraging the library function
	/// and then do a second pass for what we need.
	/// </remarks>
	/// <returns>List of all EditorEvents which were deleted as a result.</returns>
	private void RefreshEventTimingData()
	{
		// TODO: Remove Validation.
		EditorEvents.Validate();

		// First, delete any events which do not correspond to Stepmania chart events.
		// These events may sort to a different relative position based on rate altering
		// event changes. For example, if a stop is extended, that may change the position
		// of the preview since it always occurs at an absolute time, with a derived position.
		// We will re-add these events after updating the normal events.
		var deletedPreview = DeletePreviewEvent();
		var deletedLastSecondHint = DeleteLastSecondHintEvent();

		EditorEvents.Validate();

		// While we loop over all the events, reconstruct the intervals.
		var stops = new EventIntervalTree<EditorStopEvent>();
		var delays = new EventIntervalTree<EditorDelayEvent>();
		var fakes = new EventIntervalTree<EditorFakeSegmentEvent>();
		var warps = new EventIntervalTree<EditorWarpEvent>();
		var patterns = new EventIntervalTree<EditorPatternEvent>();

		var lastTempoChangeRow = 0;
		var lastTempoChangeTime = 0.0;
		EditorTempoEvent lastTempo = null;
		var lastTimeSigChangeRow = 0;
		var lastTimeSigDenominator = 0;
		var rowsPerBeat = MaxValidDenominator;
		var beatsPerMeasure = NumBeatsPerMeasure;
		var totalStopTimeSeconds = 0.0;
		var currentFakeEndRow = -1;
		var previousEventTimeSeconds = 0.0;

		var currentRow = -1;
		var eventsOnSameRow = new List<EditorEvent>();

		// Warps are unfortunately complicated.
		// Overlapping warps do not stack.
		// Warps are represented as rows / IntegerPosition, unlike Stops which use time.
		// We need to figure out how much time warps account for to update Event TimeSeconds.
		// But we cannot just do a pass to compute the time for all Warps and then sum them up
		// in a second pass since overlapping warps do not stack. We also can't just sum the time
		// between each event during a warp per loop since that would accrue rounding error.
		// So we need to use the logic of determining the time that has elapsed since the last
		// event which has altered the rate of beats that occurred during the warp. This time
		// is tracked in currentWarpTime below. When the rate changes, we commit currentWarpTime
		// to totalWarpTime.
		var warpingEndPosition = -1;
		var totalWarpTime = 0.0;
		var lastWarpBeatTimeChangeRow = -1;
		var lastTimeSigChangeMeasure = 0;

		// Note that overlapping negative Stops DO stack, even though Warps do not.
		// This means that a Chart with overlapping Warps when saved by StepMania will produce
		// a ssc and sm file that are not the same. The ssc file will have a shorter skipped range
		// and the two charts will be out of sync.

		var lastNegativeStopTime = -1.0;
		foreach (var editorEvent in EditorEvents)
		{
			if (editorEvent == null)
				continue;

			var row = editorEvent.GetRow();

			if (row != currentRow)
			{
				currentRow = row;
				eventsOnSameRow.Clear();
			}

			eventsOnSameRow.Add(editorEvent);

			var beatRelativeToLastTimeSigChange = (row - lastTimeSigChangeRow) / rowsPerBeat;
			var measureRelativeToLastTimeSigChange = beatRelativeToLastTimeSigChange / beatsPerMeasure;
			var measureStartRowRelativeToTimeSigChange = measureRelativeToLastTimeSigChange * rowsPerBeat * beatsPerMeasure;
			var absoluteMeasure = lastTimeSigChangeMeasure + measureRelativeToLastTimeSigChange;
			var timeRelativeToLastTempoChange = lastTempo == null
				? 0.0
				: (row - lastTempoChangeRow) * lastTempo.GetSecondsPerRow(MaxValidDenominator);
			var absoluteTime = lastTempoChangeTime + timeRelativeToLastTempoChange;
			var rowRelativeToMeasureStart = (short)(row - lastTimeSigChangeRow - measureStartRowRelativeToTimeSigChange);

			// Handle a currently running warp.
			var currentWarpTime = 0.0;
			if (warpingEndPosition != -1)
			{
				// Figure out the amount of time elapsed during the current warp since the last event
				// which altered the rate of time during this warp.
				var endPosition = Math.Min(row, warpingEndPosition);
				currentWarpTime = lastTempo == null
					? 0.0
					: (endPosition - lastWarpBeatTimeChangeRow) * lastTempo.GetSecondsPerRow(MaxValidDenominator);

				// Warp section is complete.
				if (row >= warpingEndPosition)
				{
					// Clear variables used to track warp time.
					warpingEndPosition = -1;
					lastWarpBeatTimeChangeRow = -1;

					// Commit the current running warp time to the total warp time.
					totalWarpTime += currentWarpTime;
					currentWarpTime = 0.0;
				}
			}

			var eventTime = absoluteTime - currentWarpTime - totalWarpTime + totalStopTimeSeconds;
			var warpedOverDueToNegativeStop = false;

			// In the case of negative stop warps, we need to clamp the time of an event so it does not
			// precede events which have lower rows.
			if (eventTime < previousEventTimeSeconds)
			{
				eventTime = previousEventTimeSeconds;
				if (eventTime <= lastNegativeStopTime)
				{
					warpedOverDueToNegativeStop = true;
				}
			}

			var fakeDueToRow = warpingEndPosition != -1 || warpedOverDueToNegativeStop || currentFakeEndRow > row;
			editorEvent.SetRowDependencies(eventTime, rowRelativeToMeasureStart, (short)lastTimeSigDenominator, fakeDueToRow);
			previousEventTimeSeconds = eventTime;

			switch (editorEvent)
			{
				// Stop handling. Just accrue more stop time.
				case EditorStopEvent stop:
				{
					// Accrue Stop time whether it is positive or negative.
					// Do not worry about overlapping negative stops as they stack in StepMania.
					var stopLengthSeconds = stop.GetStopLengthSeconds();
					totalStopTimeSeconds += stopLengthSeconds;

					// Handle negative stops.
					if (stopLengthSeconds < 0.0)
					{
						// To preserve Stepmania behavior Stops follow steps, but notes on the same lane as a
						// negative stop are warped over unless there is a delay present.
						// When we find a negative stop we need to scan backwards and set the preceding steps
						// on the same row to be warped over, unless there is a delay present.
						var coincidentDelay = false;
						foreach (var coincidentEvent in eventsOnSameRow)
						{
							if (coincidentEvent is EditorDelayEvent)
							{
								coincidentDelay = true;
								break;
							}
						}

						if (!coincidentDelay)
						{
							foreach (var eventOnSameRow in eventsOnSameRow)
								eventOnSameRow.SetIsFakeDueToRow(true);
						}

						lastNegativeStopTime = eventTime;
					}

					stops.Insert(stop, stop.GetChartTime(), Math.Max(stop.GetChartTime(), stop.GetEndChartTime()));
					break;
				}
				case EditorDelayEvent delay:
				{
					// Accrue Stop time whether it is positive or negative.
					// Do not worry about overlapping negative stops as they stack in StepMania.
					totalStopTimeSeconds += delay.GetDelayLengthSeconds();

					delays.Insert(delay, delay.GetChartTime(), Math.Max(delay.GetChartTime(), delay.GetEndChartTime()));
					break;
				}
				case EditorFakeSegmentEvent fakeSegment:
				{
					// If a fake overlaps a previous fake but ends before that previous fake ends, it
					// results in the previous fake terminating early. This matches Stepmania behavior.
					currentFakeEndRow = row + fakeSegment.GetFakeLengthRows();
					fakes.Insert(fakeSegment, fakeSegment.GetChartPosition(), fakeSegment.GetEndChartPosition());
					break;
				}
				// Warp handling. Update warp start and stop rows so we can compute the warp time.
				case EditorWarpEvent warp:
				{
					// If there is a currently running warp, just extend the Warp.
					warpingEndPosition = Math.Max(warpingEndPosition, row + warp.GetWarpLengthRows());
					if (lastWarpBeatTimeChangeRow == -1)
						lastWarpBeatTimeChangeRow = row;

					// To preserve Stepmania behavior Stops follow steps and Warps follow Stops.
					// But notes on the same lane as a Warp are warped over unless a stop or delay is present.
					// When we find a warp we need to scan backwards and set the preceding steps on the same
					// row to be warped over, unless a stop or delay is present.
					var coincidentStop = false;
					foreach (var coincidentEvent in eventsOnSameRow)
					{
						if (coincidentEvent is EditorStopEvent || coincidentEvent is EditorDelayEvent)
						{
							coincidentStop = true;
							break;
						}
					}

					if (!coincidentStop)
					{
						foreach (var coincidentEvent in eventsOnSameRow)
						{
							coincidentEvent.SetIsFakeDueToRow(true);
						}
					}

					warps.Insert(warp, warp.GetChartPosition(), warp.GetEndChartPosition());
					break;
				}
				// Time Signature change. Update time signature and beat time tracking.
				case EditorTimeSignatureEvent ts:
				{
					var timeSignature = ts.GetSignature();

					// We allow time signatures to occur on any row, even if that cuts off a previous measure.
					// Check for this scenario and increment the measure if needed as time signatures should always
					// start a new measure.
					if (rowRelativeToMeasureStart != 0)
					{
						absoluteMeasure++;
						rowRelativeToMeasureStart = 0;
					}

					editorEvent.SetRowDependencies(eventTime, rowRelativeToMeasureStart, (short)timeSignature.Denominator,
						fakeDueToRow);

					lastTimeSigChangeRow = row;
					lastTimeSigChangeMeasure = absoluteMeasure;
					beatsPerMeasure = timeSignature.Numerator;
					lastTimeSigDenominator = timeSignature.Denominator;
					rowsPerBeat = MaxValidDenominator * NumBeatsPerMeasure / timeSignature.Denominator;

					// If this alteration in beat time occurs during a warp, update our warp tracking variables.
					if (warpingEndPosition != -1)
					{
						totalWarpTime += currentWarpTime;
						lastWarpBeatTimeChangeRow = row;
					}

					// Set the measure on the time signature event.
					ts.Measure = absoluteMeasure;

					break;
				}
				// Tempo change. Update beat time tracking.
				case EditorTempoEvent tc:
				{
					lastTempo = tc;
					lastTempoChangeRow = row;
					lastTempoChangeTime = absoluteTime;

					// If this alteration in beat time occurs during a warp, update our warp tracking variables.
					if (warpingEndPosition != -1)
					{
						totalWarpTime += currentWarpTime;
						lastWarpBeatTimeChangeRow = row;
					}

					break;
				}
				case EditorPatternEvent pe:
				{
					patterns.Insert(pe, pe.GetChartPosition(), pe.GetEndChartPosition());
					break;
				}
			}
		}

		EditorEvents.Validate();

		// Now, update all the rate altering events using the updated times.
		RefreshRateAlteringEvents();

		EditorEvents.Validate();

		// Some events have length to them and their end values depend on rate altering events.
		// We cannot set them during the loop above because there may be unprocessed rate altering
		// events between their start and end, and their ends are not distinct events that will
		// be looped over. Process them here now that all the rate altering events are updated.
		foreach (var hold in Holds)
			((EditorHoldNoteEvent)hold).RefreshHoldEndTime();
		foreach (var stop in stops)
			stop.RefreshEndChartPosition();

		EditorEvents.Validate();

		// Finally, re-add any events we deleted above. When re-adding them, we will derive
		// their positions again using the updated timing information.
		if (deletedLastSecondHint)
			AddLastSecondHintEvent();
		if (deletedPreview)
			AddPreviewEvent();

		EditorEvents.Validate();

		// Update the intervals.
		Stops = stops;
		Delays = delays;
		Fakes = fakes;
		Warps = warps;
		Patterns = patterns;

		EditorEvents.Validate();

		Notify(NotificationTimingChanged, this);
	}

	#endregion Timing Updates

	#region Time-Based Event Shifting

	/// <summary>
	/// Deletes the EditorPreviewRegionEvent.
	/// When modifying properties that affect the song time, the preview region sort may change
	/// relative to other events. We therefore need to delete these events then re-add them.
	/// This method would ideally be private with EditorChart declaring EditorSong a friend class.
	/// </summary>
	public bool DeletePreviewEvent()
	{
		Assert(CanBeEdited());
		if (!CanBeEdited())
			return false;

		if (PreviewEvent == null)
			return false;
		var previewEnum = EditorEvents.Find(PreviewEvent);
		if (previewEnum == null || !previewEnum.MoveNext())
			return false;
		DeleteEvent(PreviewEvent);
		return true;
	}

	/// <summary>
	/// Adds the EditorPreviewRegionEvent.
	/// When modifying properties that affect the song time, the preview region sort may change
	/// relative to other events. We therefore need to delete these events then re-add them.
	/// This method would ideally be private with EditorChart declaring EditorSong a friend class.
	/// </summary>
	public void AddPreviewEvent()
	{
		Assert(CanBeEdited());
		if (!CanBeEdited())
			return;

		if (!EditorSong.IsUsingSongForPreview())
			return;
		var previewChartTime = EditorPosition.GetChartTimeFromSongTime(this, EditorSong.SampleStart);
		PreviewEvent = (EditorPreviewRegionEvent)EditorEvent.CreateEvent(EventConfig.CreatePreviewConfig(this, previewChartTime));
		PreviewEvent.RefreshEndChartPosition();
		AddEvent(PreviewEvent);
	}

	/// <summary>
	/// Deletes the EditorLastSecondHintEvent.
	/// When modifying the last second hint value, the EditorLastSecondHintEvent event sort may change
	/// relative to other events. We therefore need to delete these events then re-add them.
	/// This method would ideally be private with EditorChart declaring EditorSong a friend class.
	/// </summary>
	public bool DeleteLastSecondHintEvent()
	{
		Assert(CanBeEdited());
		if (!CanBeEdited())
			return false;

		if (LastSecondHintEvent == null)
			return false;
		var lastSecondHintEnum = EditorEvents.Find(LastSecondHintEvent);
		if (lastSecondHintEnum == null || !lastSecondHintEnum.MoveNext())
			return false;
		DeleteEvent(LastSecondHintEvent);
		return true;
	}

	/// <summary>
	/// Adds the EditorLastSecondHintEvent.
	/// When modifying the last second hint value, the EditorLastSecondHintEvent event sort may change
	/// relative to other events. We therefore need to delete these events then re-add them.
	/// This method would ideally be private with EditorChart declaring EditorSong a friend class.
	/// </summary>
	public void AddLastSecondHintEvent()
	{
		Assert(CanBeEdited());
		if (!CanBeEdited())
			return;

		if (EditorSong.LastSecondHint <= 0.0)
			return;
		var lastSecondHintChartTime = EditorPosition.GetChartTimeFromSongTime(this, EditorSong.LastSecondHint);
		LastSecondHintEvent =
			(EditorLastSecondHintEvent)EditorEvent.CreateEvent(
				EventConfig.CreateLastSecondHintConfig(this, lastSecondHintChartTime));
		AddEvent(LastSecondHintEvent);
	}

	#endregion Time-Based Event Shifting

	#region Measure Determination

	/// <summary>
	/// Gets the measure number for an EditorEvent based on the TimeSignature Events in the chart.
	/// </summary>
	/// <param name="editorEvent">EditorEvent in question.</param>
	/// <returns>Measure number.</returns>
	public double GetMeasureForEvent(EditorEvent editorEvent)
	{
		return GetMeasure(RateAlteringEvents?.FindActiveRateAlteringEvent(editorEvent), editorEvent.GetChartPosition());
	}

	/// <summary>
	/// Gets the measure number for a chart position based on the TimeSignature Events in the chart.
	/// </summary>
	/// <param name="chartPosition">Chart position in question.</param>
	/// <returns>Measure number.</returns>
	public double GetMeasureForChartPosition(double chartPosition)
	{
		return GetMeasure(RateAlteringEvents?.FindActiveRateAlteringEventForPosition(chartPosition), chartPosition);
	}

	/// <summary>
	/// Gets the measure number for a chart position based on the TimeSignature Events in the chart.
	/// </summary>
	/// <param name="rateEvent">The active rate altering event for the given chart position.</param>
	/// <param name="chartPosition">Chart position in question.</param>
	/// <returns>Measure number.</returns>
	private static double GetMeasure(EditorRateAlteringEvent rateEvent, double chartPosition)
	{
		if (rateEvent == null)
			return 0.0;
		var timeSigEvent = rateEvent.GetTimeSignature();
		var rowDifference = chartPosition - timeSigEvent.GetRow();
		var rowsPerMeasure = timeSigEvent.GetNumerator() *
		                     (MaxValidDenominator * NumBeatsPerMeasure / timeSigEvent.GetDenominator());
		var measures = rowDifference / rowsPerMeasure;
		measures += timeSigEvent.Measure;
		return measures;
	}

	/// <summary>
	/// Gets the chart position for the given measure based on the TimeSignature Events in the chart.
	/// </summary>
	/// <param name="measure">Measure number in question.</param>
	/// <returns>Chart position of the measure.</returns>
	public double GetChartPositionForMeasure(int measure)
	{
		// We need to search in order to turn a measure into a row.
		// Do a linear walk of the rate altering events.
		// Most charts have very few rate altering events.
		// Needing to get the position from the measure is a very uncommon use case.
		var rateEventEnumerator = RateAlteringEvents?.FindActiveRateAlteringEventEnumeratorForPosition(0.0);
		if (rateEventEnumerator == null)
			return 0.0;
		rateEventEnumerator.MoveNext();

		var precedingTimeSignature = rateEventEnumerator.Current!.GetTimeSignature();
		while (measure > precedingTimeSignature.Measure)
		{
			if (!rateEventEnumerator.MoveNext())
			{
				break;
			}

			var atEnd = false;
			while (ReferenceEquals(rateEventEnumerator.Current.GetTimeSignature(), precedingTimeSignature))
			{
				if (!rateEventEnumerator.MoveNext())
				{
					atEnd = true;
					break;
				}
			}

			if (atEnd)
				break;

			var nextTimeSignature = rateEventEnumerator.Current.GetTimeSignature();
			if (measure < nextTimeSignature.Measure)
			{
				break;
			}

			precedingTimeSignature = nextTimeSignature;
		}

		var precedingTimeSignatureEventMeasure = precedingTimeSignature.Measure;
		var rowsPerMeasure = precedingTimeSignature.GetNumerator() *
		                     (MaxValidDenominator * NumBeatsPerMeasure / precedingTimeSignature.GetDenominator());
		return precedingTimeSignature.GetRow() + (measure - precedingTimeSignatureEventMeasure) * rowsPerMeasure;
	}

	/// <summary>
	/// Gets the measure number for a new EditorTimeSignatureEvent to be created at the given row.
	/// </summary>
	/// <param name="row">Row to create the new EditorTimeSignatureEvent.</param>
	/// <returns>Measure number of the new EditorTimeSignatureEvent.</returns>
	public int GetMeasureForNewTimeSignatureAtRow(int row)
	{
		var enumerator = RateAlteringEvents.FindActiveRateAlteringEventEnumeratorForPosition(row);
		enumerator.MoveNext();
		var activeTimeSignature = enumerator.Current!.GetTimeSignature();
		var rowsPerMeasure = activeTimeSignature.GetRowsPerMeasure();
		var relativeRows = row - activeTimeSignature.GetRow();
		var relativeMeasures = relativeRows / rowsPerMeasure;
		// If the desired row for the new time signature does not fall on a measure boundary then
		// add one since the division above is only precise for measure boundaries and will be one
		// under for measures created off boundaries.
		if (relativeRows % rowsPerMeasure != 0)
			relativeMeasures++;
		return activeTimeSignature.Measure + relativeMeasures;
	}

	#endregion Measure Determination

	#region Position And Time Determination

	/// <summary>
	/// Gets the chart position for a chart time.
	/// If an EditorEvent or Stepmania Event is known, prefer using TryGetTimeOfEvent as those
	/// methods will take into account relative sort order of simultaneous events.
	/// This method will assume that the chart time in question precedes any rate altering event
	/// with equal time.
	/// </summary>
	/// <param name="chartTime">Chart time in question.</param>
	/// <param name="chartPosition">Chart position to set.</param>
	/// <returns>Whether or not a chart position was determined.</returns>
	public bool TryGetChartPositionFromTime(double chartTime, ref double chartPosition)
	{
		var rateEvent = RateAlteringEvents?.FindActiveRateAlteringEventForTime(chartTime, false);
		if (rateEvent == null)
			return false;
		chartPosition = rateEvent.GetChartPositionFromTime(chartTime);
		return true;
	}

	/// <summary>
	/// Gets the chart time for a chart position.
	/// If an EditorEvent or Stepmania Event is known, prefer using TryGetTimeOfEvent as those
	/// methods will take into account relative sort order of simultaneous events.
	/// This method will assume that the chart position in question precedes any rate altering event
	/// with equal position.
	/// </summary>
	/// <param name="chartPosition">Chart position in question.</param>
	/// <param name="chartTime">Chart time to set.</param>
	/// <returns>Whether or not a chart time was determined.</returns>
	public bool TryGetTimeFromChartPosition(double chartPosition, ref double chartTime)
	{
		var rateEvent = RateAlteringEvents?.FindActiveRateAlteringEventForPosition(chartPosition, false);
		if (rateEvent == null)
			return false;
		chartTime = rateEvent.GetChartTimeFromPosition(chartPosition);
		return true;
	}

	/// <summary>
	/// Gets the chart time of an EditorEvent based on its row and event type and when that will
	/// occur in the chart based on the chart's rate altering events.
	/// If an EditorEvent is known and time needs to be recomputed this is the best method
	/// for determining the time, as opposed to TryGetTimeFromChartPosition which doesn't
	/// take into account the event's type.
	/// </summary>
	/// <param name="chartEvent">The EditorEvent to find the time of.</param>
	/// <param name="chartTime">The time to be set.</param>
	/// <returns>True if the time could be determined and false otherwise.</returns>
	public bool TryGetTimeOfEvent(EditorEvent chartEvent, ref double chartTime)
	{
		var rateEvent = RateAlteringEvents?.FindActiveRateAlteringEvent(chartEvent);
		if (rateEvent == null)
			return false;
		chartTime = rateEvent.GetChartTimeFromPosition(chartEvent.GetChartPosition());
		return true;
	}

	/// <summary>
	/// Gets the chart time of a Stepmania Event based on its row and event type and when that will
	/// occur in the chart based on the chart's rate altering events.
	/// If this Event has an EditorEvent associated with it, prefer using the TryGetTimeOfEvent
	/// implementation which takes an EditorEvent. Failing that, prefer this method over
	/// TryGetTimeFromChartPosition which doesn't take into account the event's type.
	/// </summary>
	/// <param name="smEvent">The Stepmania Event to find the time of.</param>
	/// <param name="chartTime">The time to be set.</param>
	/// <returns>True if the time could be determined and false otherwise.</returns>
	public bool TryGetTimeOfEvent(Event smEvent, ref double chartTime)
	{
		var rateEvent = RateAlteringEvents?.FindActiveRateAlteringEvent(smEvent);
		if (rateEvent == null)
			return false;
		chartTime = rateEvent.GetChartTimeFromPosition(smEvent.IntegerPosition);
		return true;
	}

	/// <summary>
	/// Gets the chart time of the start of the chart.
	/// </summary>
	/// <returns>Chart time of the start of the chart.</returns>
	public double GetStartChartTime()
	{
		return 0.0;
	}

	/// <summary>
	/// Gets the chart time of the end of the chart.
	/// </summary>
	/// <returns>Chart time of the end of the chart.</returns>
	public double GetEndChartTime()
	{
		var lastEvent = EditorEvents.Last();
		var endTime = 0.0;
		if (lastEvent.MoveNext())
		{
			// Do not include the preview as counting towards the song ending.
			if (lastEvent.Current is EditorPreviewRegionEvent)
			{
				if (lastEvent.MovePrev())
				{
					endTime = lastEvent.Current.GetEndChartTime();
				}
			}
			else
			{
				endTime = lastEvent.Current!.GetEndChartTime();
			}
		}

		return endTime;
	}

	/// <summary>
	/// Gets the chart position of the end of the chart.
	/// </summary>
	/// <returns>Chart position of the end of the chart.</returns>
	public double GetEndPosition()
	{
		var lastEvent = EditorEvents.Last();
		var endPosition = 0.0;
		if (lastEvent.MoveNext())
		{
			// Do not include the preview as counting towards the song ending.
			if (lastEvent.Current is EditorPreviewRegionEvent)
			{
				if (lastEvent.MovePrev())
				{
					endPosition = lastEvent.Current.GetEndRow();
				}
			}
			else
			{
				endPosition = lastEvent.Current!.GetEndRow();
			}
		}

		return endPosition;
	}

	#endregion Position And Time Determination

	#region Finding Overlapping Events

	public List<IChartRegion> GetRegionsOverlapping(double chartPosition, double chartTime)
	{
		var regionEvents = new List<EditorEvent>();
		var stops = GetStopEventsOverlapping(chartTime);
		if (stops != null)
			regionEvents.AddRange(stops);
		var delays = GetDelayEventOverlapping(chartTime);
		if (delays != null)
			regionEvents.AddRange(delays);
		var fakes = GetFakeSegmentEventOverlapping(chartPosition);
		if (fakes != null)
			regionEvents.AddRange(fakes);
		var warps = GetWarpEventOverlapping(chartPosition);
		if (warps != null)
			regionEvents.AddRange(warps);
		if (PreviewEvent.GetChartTime() <= chartTime && PreviewEvent.GetEndChartTime() >= chartTime)
			regionEvents.Add(PreviewEvent);
		var patterns = GetPatternEventsOverlapping(chartPosition);
		if (patterns?.Count > 0)
			regionEvents.AddRange(patterns);
		if (SelectedRows != null && chartPosition >= SelectedRows.GetRow() && chartPosition <= SelectedRows.GetEndRow())
			regionEvents.Add(SelectedRows);
		regionEvents.Sort();
		var regions = new List<IChartRegion>();
		foreach (var regionEvent in regionEvents)
			regions.Add((IChartRegion)regionEvent);
		return regions;
	}

	private List<EditorStopEvent> GetStopEventsOverlapping(double chartTime)
	{
		return Stops?.FindAllOverlapping(chartTime);
	}

	private List<EditorDelayEvent> GetDelayEventOverlapping(double chartTime)
	{
		return Delays?.FindAllOverlapping(chartTime);
	}

	private List<EditorFakeSegmentEvent> GetFakeSegmentEventOverlapping(double chartPosition)
	{
		return Fakes?.FindAllOverlapping(chartPosition);
	}

	private List<EditorWarpEvent> GetWarpEventOverlapping(double chartPosition)
	{
		return Warps?.FindAllOverlapping(chartPosition);
	}

	private List<EditorPatternEvent> GetPatternEventsOverlapping(double chartPosition)
	{
		return Patterns?.FindAllOverlapping(chartPosition);
	}

	/// <summary>
	/// Gets all the holds overlapping the given chart position.
	/// Does not include holds being edited.
	/// </summary>
	/// <param name="chartPosition">Chart position to find overlapping holds for.</param>
	/// <param name="explicitEnumerator">
	/// Optional enumerator to copy for scanning. If not provided one will be created using
	/// the given chartPosition. This parameter is exposed as a performance optimization since
	/// we often have an enumerator in the correct spot.
	/// </param>
	/// <returns>
	/// All holds overlapping the given position. The length of the array is the Chart's
	/// NumInputs. If a hold is not overlapping the given position for a given lane then
	/// that entry in the array will be null. Otherwise, it will be the EditorHoldNoteEvent
	/// which overlaps.
	/// </returns>
	public EditorHoldNoteEvent[] GetHoldsOverlappingPosition(double chartPosition,
		IReadOnlyRedBlackTree<EditorEvent>.IReadOnlyRedBlackTreeEnumerator explicitEnumerator = null)
	{
		var holds = new EditorHoldNoteEvent[NumInputs];

		IReadOnlyRedBlackTree<EditorEvent>.IReadOnlyRedBlackTreeEnumerator enumerator;
		if (explicitEnumerator != null)
			enumerator = explicitEnumerator.Clone();
		else
			enumerator = EditorEvents.FindGreatestAtOrBeforeChartPosition(chartPosition);
		if (enumerator == null)
			return holds;

		var numLanesChecked = 0;
		var lanesChecked = new bool[NumInputs];
		while (enumerator.MovePrev() && numLanesChecked < NumInputs)
		{
			var e = enumerator.Current;
			if (e!.IsBeingEdited())
				continue;
			var lane = e.GetLane();
			if (lane >= 0)
			{
				if (!lanesChecked[lane])
				{
					lanesChecked[lane] = true;
					numLanesChecked++;

					if (e.GetRow() <= chartPosition && e.GetRow() + e.GetRowDuration() >= chartPosition &&
					    e is EditorHoldNoteEvent hn)
						holds[lane] = hn;
				}
			}
		}

		return holds;
	}

	/// <summary>
	/// Gets all the holds overlapping the given chart time.
	/// Does not include holds being edited.
	/// </summary>
	/// <param name="chartTime">Chart time to find overlapping holds for.</param>
	/// <param name="explicitEnumerator">
	/// Optional enumerator to copy for scanning. If not provided one will be created using
	/// the given chartTime. This parameter is exposed as a performance optimization since
	/// we often have an enumerator in the correct spot.
	/// </param>
	/// <returns>
	/// All holds overlapping the given time. The length of the array is the Chart's
	/// NumInputs. If a hold is not overlapping the given time for a given lane then
	/// that entry in the array will be null. Otherwise, it will be the EditorHoldNoteEvent
	/// which overlaps.
	/// </returns>
	public EditorHoldNoteEvent[] GetHoldsOverlappingTime(double chartTime,
		IReadOnlyRedBlackTree<EditorEvent>.IReadOnlyRedBlackTreeEnumerator explicitEnumerator = null)
	{
		var holds = new EditorHoldNoteEvent[NumInputs];

		IReadOnlyRedBlackTree<EditorEvent>.IReadOnlyRedBlackTreeEnumerator enumerator;
		if (explicitEnumerator != null)
			enumerator = explicitEnumerator.Clone();
		else
			enumerator = EditorEvents.FindGreatestAtOrBeforeChartTime(chartTime);
		if (enumerator == null)
			return holds;

		var numLanesChecked = 0;
		var lanesChecked = new bool[NumInputs];
		while (enumerator.MovePrev() && numLanesChecked < NumInputs)
		{
			var e = enumerator.Current;
			if (e!.IsBeingEdited())
				continue;
			var lane = e.GetLane();
			if (lane >= 0)
			{
				if (!lanesChecked[lane])
				{
					lanesChecked[lane] = true;
					numLanesChecked++;

					if (e.GetChartTime() <= chartTime && e.GetEndChartTime() >= chartTime && e is EditorHoldNoteEvent hn)
						holds[lane] = hn;
				}
			}
		}

		return holds;
	}

	/// <summary>
	/// Determines whether or not the given EditorEvent is positioned such that it should
	/// be considered fake.
	/// </summary>
	/// <param name="editorEvent">EditorEvent in question.</param>
	/// <returns>
	/// True if the event should be considered fake due to its position and false otherwise.
	/// </returns>
	public bool IsEventInFake(EditorEvent editorEvent)
	{
		var row = editorEvent.GetRow();
		var chartPosition = editorEvent.GetChartPosition();
		var time = editorEvent.GetChartTime();

		// An event in a fake region is a fake.
		var overlappingFakes = Fakes.FindAllOverlapping(chartPosition, true, false);
		if (overlappingFakes.Count > 0)
		{
			var inFakeRegion = true;

			// If a fake overlaps a previous fake but ends before that previous fake ends, it
			// results in the previous fake terminating early. This matches Stepmania behavior.
			// This means that even if a fake region overlaps a step, that step may not be a fake.
			// We need to scan forward to see if any other fakes start after the first overlapping
			// fake and terminate it early.
			var earliestOverlappingFake = overlappingFakes[0];
			var fakeEnumerator = Fakes.Find(earliestOverlappingFake, earliestOverlappingFake.GetChartPosition(),
				earliestOverlappingFake.GetEndChartPosition());
			fakeEnumerator.MoveNext();
			while (fakeEnumerator.IsCurrentValid())
			{
				var currentFake = fakeEnumerator.Current!;
				if (currentFake.GetChartPosition() > chartPosition)
					break;
				inFakeRegion = currentFake.GetChartPosition() <= chartPosition &&
				               currentFake.GetEndChartPosition() > chartPosition;
				fakeEnumerator.MoveNext();
			}

			if (inFakeRegion)
				return true;
		}

		// An event in a warp region is a fake.
		var overlappingWarps = Warps.FindAllOverlapping(chartPosition, true, false);
		foreach (var warp in overlappingWarps)
		{
			// Warps which are coincident with stops or delays do not cause simultaneous notes to be warped over.
			if (warp.GetRow() == row)
			{
				var simultaneousStop = RateAlteringEvents.FindEventAtRow<EditorStopEvent>(row);
				if (simultaneousStop != null)
					continue;
				var simultaneousDelay = RateAlteringEvents.FindEventAtRow<EditorDelayEvent>(row);
				if (simultaneousDelay != null)
					continue;
				return true;
			}

			return true;
		}

		// An event in a negative stop region is a fake.
		var precedingStopEnumerator = Stops.FindGreatestPreceding(time, true);
		while (precedingStopEnumerator != null && precedingStopEnumerator.MovePrev())
		{
			if (time > precedingStopEnumerator.Current!.GetChartTime())
				return false;

			// This event's time is the same as its preceding stop. This can be due to it occurring at the
			// same time as a normal stop or due to being warped over due to negative stops. Since stops
			// stack we cannot tell which scenario we are in without backing up to see if we are at the
			// same time as a preceding negative stop.
			if (precedingStopEnumerator.Current.GetStopLengthSeconds() < 0.0)
			{
				// If this negative stop is coincident with a delay then it does not cause simultaneous
				// steps to be warped over.
				if (precedingStopEnumerator.Current.GetRow() == row)
				{
					var coincidentDelay = RateAlteringEvents.FindEventAtRow<EditorDelayEvent>(row);
					if (coincidentDelay != null)
						return false;
				}

				// Negative stop ends are not inclusive. We need to know if the row of the event is
				// exactly equal to the end of the negative stop.
				if (chartPosition >= precedingStopEnumerator.Current.GetEndChartPosition())
					return false;
				return true;
			}
		}

		return false;
	}

	#endregion Finding Overlapping Events

	#region EditorEvent Modification Callbacks

	/// <summary>
	/// Called to update an EditorStopEvent's time.
	/// The EditorChart needs to be responsible for updating Stop time as it can result in the
	/// Stop changing its relative position to other notes.
	/// </summary>
	public void UpdateStopTime(EditorStopEvent stop, double newTime, ref double stopTime)
	{
		Assert(CanBeEdited());
		if (!CanBeEdited())
			return;

		// Negative stops are sorted differently than positive stops.
		var signChanged = stopTime < 0.0 != newTime < 0;
		if (signChanged)
		{
			DeleteEvent(stop);
		}
		else
		{
			var deleted = Stops.Delete(stop, stop.GetChartTime(), Math.Max(stop.GetChartTime(), stop.GetEndChartTime()));
			Assert(deleted);
		}

		stopTime = newTime;

		if (signChanged)
		{
			AddEvent(stop);
		}
		else
		{
			Stops.Insert(stop, stop.GetChartTime(), Math.Max(stop.GetChartTime(), stop.GetEndChartTime()));
		}

		// Stops affect timing data.
		RefreshEventTimingData();
	}

	/// <summary>
	/// Called when an EditorDelayEvent's time is modified.
	/// </summary>
	public void UpdateDelayTime(EditorDelayEvent delay, double newTime, ref double delayTime)
	{
		Assert(CanBeEdited());
		if (!CanBeEdited())
			return;

		// Negative delays are sorted differently than positive delays.
		var signChanged = delayTime < 0.0 != newTime < 0;
		if (signChanged)
		{
			DeleteEvent(delay);
		}
		else
		{
			var deleted = Delays.Delete(delay, delay.GetChartTime(), Math.Max(delay.GetChartTime(), delay.GetEndChartTime()));
			Assert(deleted);
		}

		delayTime = newTime;

		if (signChanged)
		{
			AddEvent(delay);
		}
		else
		{
			Delays.Insert(delay, delay.GetChartTime(), Math.Max(delay.GetChartTime(), delay.GetEndChartTime()));
		}

		// Delays affect timing data.
		RefreshEventTimingData();
	}

	/// <summary>
	/// Called when an EditorWarpEvent's length is modified.
	/// </summary>
	public void OnWarpLengthModified(EditorWarpEvent warp, double oldEndPosition, double newEndPosition)
	{
		Assert(CanBeEdited());
		if (!CanBeEdited())
			return;

		// Update the IntervalTree holding this Warp.
		var deleted = Warps.Delete(warp, warp.GetChartPosition(), oldEndPosition);
		Assert(deleted);
		Warps.Insert(warp, warp.GetChartPosition(), newEndPosition);

		// Warps affect timing data.
		RefreshEventTimingData();
	}

	/// <summary>
	/// Called when an EditorFakeSegmentEvent's length is modified.
	/// </summary>
	public void OnFakeSegmentLengthModified(EditorFakeSegmentEvent fake, double oldEndPosition, double newEndPosition)
	{
		Assert(CanBeEdited());
		if (!CanBeEdited())
			return;

		// Update the IntervalTree holding this Fake.
		var deleted = Fakes.Delete(fake, fake.GetChartPosition(), oldEndPosition);
		Assert(deleted);
		Fakes.Insert(fake, fake.GetChartPosition(), newEndPosition);

		// Fake segments affect row-dependent fake data.
		RefreshEventTimingData();
	}

	/// <summary>
	/// Called when an EditorScrollRateEvent's rate is modified.
	/// </summary>
	public void OnScrollRateModified(EditorScrollRateEvent _)
	{
		Assert(CanBeEdited());
		if (!CanBeEdited())
			return;

		// Scroll rates affect timing data.
		RefreshEventTimingData();
	}

	/// <summary>
	/// Called when an EditorTempoEvent's tempo is modified.
	/// </summary>
	public void OnTempoModified(EditorTempoEvent _)
	{
		Assert(CanBeEdited());
		if (!CanBeEdited())
			return;

		// Tempos rates affect timing data.
		RefreshEventTimingData();
	}

	/// <summary>
	/// Called when an EditorTimeSignatureEvent's signature is modified.
	/// </summary>
	public void OnTimeSignatureModified(EditorTimeSignatureEvent _)
	{
		Assert(CanBeEdited());
		if (!CanBeEdited())
			return;

		// Time signatures affect row-dependent coloration.
		RefreshEventTimingData();
	}

	/// <summary>
	/// Called when an EditorInterpolatedRateAlteringEvent's properties are modified.
	/// </summary>
	public void OnInterpolatedRateAlteringEventModified(EditorInterpolatedRateAlteringEvent irae)
	{
		Assert(CanBeEdited());
		if (!CanBeEdited())
			return;

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
				e.Current!.PreviousScrollRate = irae.GetRate();
			}

			if (e.MoveNext())
			{
				var next = e.Current;
				next!.PreviousScrollRate = irae.GetRate();
			}
		}
	}

	public void OnAttackEventRequestEdit(EditorAttackEvent eaa)
	{
		Notify(NotificationAttackRequestEdit, this, eaa);
	}

	/// <summary>
	/// Called when an EditorPatternEvent's length is modified.
	/// </summary>
	public void OnPatternLengthModified(EditorPatternEvent pattern, double oldEndPosition, double newEndPosition)
	{
		Assert(CanBeEdited());
		if (!CanBeEdited())
			return;

		// Update the IntervalTree holding the EditorPatternEvents.
		var deleted = Patterns.Delete(pattern, pattern.GetChartPosition(), oldEndPosition);
		Assert(deleted);
		Patterns.Insert(pattern, pattern.GetChartPosition(), newEndPosition);
	}

	public void OnPatternEventRequestEdit(EditorPatternEvent epa)
	{
		Notify(NotificationPatternRequestEdit, this, epa);
	}

	public void OnHoldTypeChanged(EditorHoldNoteEvent hold)
	{
		if (hold.IsAddedToChart())
			StepTotals.OnHoldTypeChanged(hold);
	}

	public void OnFakeChanged(EditorEvent editorEvent)
	{
		if (editorEvent.IsAddedToChart())
			StepTotals.OnFakeTypeChanged(editorEvent);
	}

	#endregion EditorEvent Modification Callbacks

	#region Adding and Deleting EditorEvents

	/// <summary>
	/// Deletes the given EditorEvent.
	/// </summary>
	/// <param name="editorEvent">EditorEvent to delete.</param>
	public void DeleteEvent(EditorEvent editorEvent)
	{
		Assert(CanBeEdited());
		if (!CanBeEdited())
			return;

		// Delete the event.
		var rowDependentDataDirty = DeleteEventInternal(editorEvent);

		// Perform post-delete operations.
		if (rowDependentDataDirty)
			RefreshEventTimingData();
		StepTotals.CommitAddsAndDeletesToStepDensity();
		Notify(NotificationEventDeleted, this, editorEvent);
	}

	/// <summary>
	/// Deletes the given EditorEvents.
	/// </summary>
	/// <param name="editorEvents">List of all EditorEvents to delete.</param>
	public void DeleteEvents(List<EditorEvent> editorEvents)
	{
		Assert(CanBeEdited());
		if (!CanBeEdited())
			return;

		// Delete the events.
		var rowDependentDataDirty = false;
		foreach (var editorEvent in editorEvents)
		{
			rowDependentDataDirty = DeleteEventInternal(editorEvent) || rowDependentDataDirty;
		}

		// Perform post-delete operations.
		if (rowDependentDataDirty)
			RefreshEventTimingData();
		StepTotals.CommitAddsAndDeletesToStepDensity();
		Notify(NotificationEventsDeleted, this, editorEvents);
	}

	/// <summary>
	/// Internal method for deleting an individual EditorEvent.
	/// Does not perform operations which can be done in bulk when deleting multiple events like
	/// refreshing timing data, updating the step density, and notifying listeners of deletions.
	/// </summary>
	/// <param name="editorEvent">EditorEvent to delete.</param>
	/// <returns>True if row-dependent data is dirty and false otherwise.</returns>
	private bool DeleteEventInternal(EditorEvent editorEvent)
	{
		StepTotals.OnEventDeleted(editorEvent);
		var deleted = EditorEvents.Delete(editorEvent);
		Assert(deleted);

		if (editorEvent.IsMiscEvent())
		{
			deleted = MiscEvents.Delete(editorEvent);
			Assert(deleted);
		}

		if (editorEvent is EditorLabelEvent)
		{
			deleted = Labels.Delete(editorEvent);
			Assert(deleted);
		}

		if (editorEvent is EditorAttackEvent)
		{
			deleted = Attacks.Delete(editorEvent);
			Assert(deleted);
		}

		if (editorEvent is EditorTickCountEvent)
		{
			deleted = TickCounts.Delete(editorEvent);
			Assert(deleted);
		}

		if (editorEvent is EditorMultipliersEvent)
		{
			deleted = Multipliers.Delete(editorEvent);
			Assert(deleted);
		}

		var rowDependentDataDirty = false;
		switch (editorEvent)
		{
			case EditorFakeSegmentEvent fse:
				deleted = Fakes.Delete(fse, fse.GetChartPosition(), fse.GetEndChartPosition());
				Assert(deleted);
				rowDependentDataDirty = true;
				break;
			case EditorRateAlteringEvent rae:
			{
				deleted = RateAlteringEvents.Delete(rae);
				Assert(deleted);

				switch (rae)
				{
					case EditorStopEvent se:
						deleted = Stops.Delete(se, se.GetChartTime(), Math.Max(se.GetChartTime(), se.GetEndChartTime()));
						Assert(deleted);
						break;
					case EditorDelayEvent de:
						deleted = Delays.Delete(de, de.GetChartTime(), Math.Max(de.GetChartTime(), de.GetEndChartTime()));
						Assert(deleted);
						break;
					case EditorWarpEvent we:
						deleted = Warps.Delete(we, we.GetChartPosition(), we.GetEndChartPosition());
						Assert(deleted);
						break;
				}

				rowDependentDataDirty = true;
				break;
			}
			case EditorInterpolatedRateAlteringEvent irae:
			{
				var e = InterpolatedScrollRateEvents.FindMutable(irae);
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
								next!.PreviousScrollRate = prev!.GetRate();
							}

							e.MoveNext();
						}

						e.MoveNext();
					}

					e.MovePrev();
					e.Delete();
				}

				break;
			}
			case EditorPatternEvent pe:
			{
				deleted = Patterns.Delete(pe, pe.GetChartPosition(), pe.GetEndChartPosition());
				Assert(deleted);
				break;
			}
		}

		editorEvent.OnRemovedFromChart();
		return rowDependentDataDirty;
	}

	/// <summary>
	/// Adds the given EditorEvent to the chart.
	/// Performs no checking that the given event is valid for the chart.
	/// For example, two tap notes cannot exist at the same time in the same line.
	/// This method will not prevent this from occurring.
	/// This method will ensure the timing data for all notes is correct.
	/// </summary>
	/// <param name="editorEvent">EditorEvent to add.</param>
	public void AddEvent(EditorEvent editorEvent)
	{
		Assert(CanBeEdited());
		if (!CanBeEdited())
			return;

		// Add the event.
		AddEventInternal(editorEvent);

		// Perform post-add operations.
		StepTotals.CommitAddsAndDeletesToStepDensity();
		Notify(NotificationEventAdded, this, editorEvent);
	}

	/// <summary>
	/// Adds the given EditorEvents to the chart.
	/// Performs no checking that the given events are valid for the chart.
	/// For example, two tap notes cannot exist at the same time in the same lane.
	/// This method will not prevent this from occurring.
	/// This method will ensure the timing data for all notes is correct.
	/// </summary>
	/// <param name="editorEvents">EditorEvents to add.</param>
	public void AddEvents(List<EditorEvent> editorEvents)
	{
		Assert(CanBeEdited());
		if (!CanBeEdited())
			return;

		// Add the events.
		foreach (var editorEvent in editorEvents)
			AddEventInternal(editorEvent);

		// Perform post-add operations.
		StepTotals.CommitAddsAndDeletesToStepDensity();
		Notify(NotificationEventsAdded, this, editorEvents);
	}

	/// <summary>
	/// Internal method for adding an individual EditorEvent.
	/// Does not perform operations which can be done in bulk when adding multiple events like
	/// updating the step density and notifying listeners of additions.
	/// </summary>
	/// <param name="editorEvent">EditorEvent to add.</param>
	private void AddEventInternal(EditorEvent editorEvent)
	{
		var rowDependentDataDirty = false;

		StepTotals.OnEventAdded(editorEvent);
		EditorEvents.Insert(editorEvent);
		if (editorEvent.IsMiscEvent())
			MiscEvents.Insert(editorEvent);
		if (editorEvent is EditorLabelEvent)
			Labels.Insert(editorEvent);
		if (editorEvent is EditorAttackEvent)
			Attacks.Insert(editorEvent);
		if (editorEvent is EditorTickCountEvent)
			TickCounts.Insert(editorEvent);
		if (editorEvent is EditorMultipliersEvent)
			Multipliers.Insert(editorEvent);

		switch (editorEvent)
		{
			case EditorFakeSegmentEvent fse:
				Fakes.Insert(fse, fse.GetChartPosition(), fse.GetEndChartPosition());
				rowDependentDataDirty = true;
				break;
			case EditorRateAlteringEvent rae:
			{
				RateAlteringEvents.Insert(rae);

				switch (rae)
				{
					case EditorStopEvent se:
						Stops.Insert(se, se.GetChartTime(), Math.Max(se.GetChartTime(), se.GetEndChartTime()));
						break;
					case EditorDelayEvent de:
						Delays.Insert(de, de.GetChartTime(), Math.Max(de.GetChartTime(), de.GetEndChartTime()));
						break;
					case EditorWarpEvent we:
						Warps.Insert(we, we.GetChartPosition(), we.GetEndChartPosition());
						break;
				}

				rowDependentDataDirty = true;
				break;
			}
			case EditorInterpolatedRateAlteringEvent irae:
			{
				var e = InterpolatedScrollRateEvents.Insert(irae);
				if (e != null)
				{
					e.MoveNext();
					if (e.MoveNext())
					{
						var next = e.Current;
						next!.PreviousScrollRate = irae.GetRate();
					}

					if (e.MovePrev())
					{
						if (e.MovePrev())
						{
							var prev = e.Current;
							irae.PreviousScrollRate = prev!.GetRate();
						}
					}
				}

				break;
			}
			case EditorPatternEvent pe:
			{
				Patterns.Insert(pe, pe.GetChartPosition(), pe.GetEndChartPosition());
				break;
			}
		}

		editorEvent.OnAddedToChart();

		// TODO: Optimize.
		// When deleting a re-adding many rate altering events this causes a hitch.
		// We can't just call RefreshEventTimingData once at the end of the loop because
		// note within the song may have their positions altered relative to individual
		// rate altering event notes such that calling SetEventTimeFromRows
		// once at the end re-sorts them based on time differences.
		// To optimize this we could update events only up until the next rate altering event
		// rather than going to the end of the chart each time. For an old style gimmick chart
		// this would be a big perf win.
		// Moving many rate altering events together is not a frequent operation.

		if (rowDependentDataDirty)
		{
			RefreshEventTimingData();
		}
	}

	/// <summary>
	/// Adds the given events and ensures the chart is in a consistent state afterwards
	/// by forcibly removing any events which conflict with the events to be added. This
	/// may result in modifications like shortening holds or converting a hold to a tap
	/// which requires deleting and then adding a modified event or events. Any events
	/// which were deleted or added as side effects of adding the given events will be
	/// returned.
	/// This method expects that the given events are valid with respect to each other
	/// (for example, no overlapping taps in the given events) and are valid at their
	/// positions (for example, no time signatures at invalid rows).
	/// This method expects that the given events are sorted.
	/// Callers MUST call ForceAddEventsComplete after calls ForceAddEvents.
	/// </summary>
	/// <param name="events">Events to add.</param>
	/// <returns>
	/// Tuple where the first element is a list of events which were added as a side effect
	/// of adding the given events and the second element is a list of events which were
	/// deleted as a side effect of adding the given events.
	/// </returns>
	public (List<EditorEvent>, List<EditorEvent>) ForceAddEvents(List<EditorEvent> events)
	{
		Assert(CanBeEdited());
		if (!CanBeEdited())
			return (null, null);

		List<EditorEvent> sideEffectAddedEvents = null;
		List<EditorEvent> sideEffectDeletedEvents = null;

		var rateDirty = false;
		foreach (var editorEvent in events)
			ForceAddEventInternal(editorEvent, ref rateDirty, ref sideEffectAddedEvents, ref sideEffectDeletedEvents);

		// Do not update StepTotals or notify listeners yet.
		// Do this after all events have been added.
		//StepTotals.CommitAddsAndDeletesToStepDensity();
		//Notify(NotificationEventsAdded, this, events);

		return (sideEffectAddedEvents, sideEffectDeletedEvents);
	}

	/// <summary>
	/// Adds the given event and ensures the chart is in a consistent state afterwards
	/// by forcibly removing any events which conflict with the event to be added. This
	/// may result in modifications like shortening holds or converting a hold to a tap
	/// which requires deleting and then adding a modified event or events. Any events
	/// which were deleted or added as side effects of adding the given event will be
	/// returned.
	/// Callers MUST call ForceAddEventsComplete after calls ForceAddEvents.
	/// </summary>
	/// <param name="editorEvent">Event to add.</param>
	/// <returns>
	/// Tuple where the first element is a list of events which were added as a side effect
	/// of adding the given events and the second element is a list of events which were
	/// deleted as a side effect of adding the given events.
	/// </returns>
	public (List<EditorEvent>, List<EditorEvent>) ForceAddEvent(EditorEvent editorEvent)
	{
		Assert(CanBeEdited());
		if (!CanBeEdited())
			return (null, null);

		List<EditorEvent> sideEffectAddedEvents = null;
		List<EditorEvent> sideEffectDeletedEvents = null;

		var rateDirty = false;
		ForceAddEventInternal(editorEvent, ref rateDirty, ref sideEffectAddedEvents, ref sideEffectDeletedEvents);

		// Do not update StepTotals or notify listeners yet.
		// Do this after all events have been added.
		//StepTotals.CommitAddsAndDeletesToStepDensity();
		//Notify(NotificationEventAdded, this, editorEvent);

		return (sideEffectAddedEvents, sideEffectDeletedEvents);
	}

	/// <summary>
	/// Finish force adding notes.
	/// This is implemented as a separate callable function to be called after ForceAddEvent and
	/// ForceAddEvents as a performance optimization.
	/// </summary>
	/// <param name="events">All events which were added.</param>
	public void ForceAddEventsComplete(List<EditorEvent> events)
	{
		StepTotals.CommitAddsAndDeletesToStepDensity();
		Notify(NotificationEventsAdded, this, events);
	}

	private void ForceAddEventInternal(
		EditorEvent editorEvent,
		ref bool rateDirty,
		ref List<EditorEvent> sideEffectAddedEvents,
		ref List<EditorEvent> sideEffectDeletedEvents)
	{
		var lane = editorEvent.GetLane();

		// If this event is a tap, delete any note which starts at the same time in the same lane.
		if (lane != InvalidArrowIndex)
		{
			var row = editorEvent.GetRow();
			var existingNote = EditorEvents.FindNoteAt(row, lane, true);

			// If there is a note at this position, or extending through this position.
			if (existingNote != null)
			{
				// If the existing note is at the same row as the new note, delete it.
				if (existingNote.GetRow() == row)
				{
					DeleteEvent(existingNote);
					sideEffectDeletedEvents ??= [];
					sideEffectDeletedEvents.Add(existingNote);
				}

				// The existing note is a hold which extends through the new note.
				else if (existingNote.GetRow() < row && existingNote.GetEndRow() >= row &&
				         existingNote is EditorHoldNoteEvent existingHold)
				{
					// Reduce the length.
					var newExistingHoldEndRow = editorEvent.GetRow() - MaxValidDenominator / 4;

					// In either case below, delete the existing hold note and replace it with a new hold or a tap.
					// We could reduce the hold length in place, but then we would need to surface that alteration to the caller
					// so they can undo it. It's simpler for now to just remove it and add a new one.
					DeleteEvent(existingNote);
					sideEffectDeletedEvents ??= [];
					sideEffectDeletedEvents.Add(existingNote);

					// If the reduction in length is below the min length for a hold, replace it with a tap.
					if (newExistingHoldEndRow <= existingNote.GetRow())
					{
						var replacementEvent = EditorEvent.CreateEvent(EventConfig.CreateTapConfig(existingNote));
						AddEvent(replacementEvent);
						sideEffectAddedEvents ??= [];
						sideEffectAddedEvents.Add(replacementEvent);
					}

					// Otherwise, reduce the length by deleting the old hold and adding a new hold.
					else
					{
						// We need to recompute the hold end time, so don't provide any explicit times.
						var replacementEvent = EditorEvent.CreateEvent(EventConfig.CreateHoldConfig(this,
							existingNote.GetRow(), existingNote.GetLane(), existingNote.GetPlayer(),
							newExistingHoldEndRow - existingNote.GetRow(),
							existingHold.IsRoll()));
						AddEvent(replacementEvent);
						sideEffectAddedEvents ??= [];
						sideEffectAddedEvents.Add(replacementEvent);
					}
				}
			}

			// If this event is a hold note, delete any note which overlaps the hold.
			var len = editorEvent.GetRowDuration();
			if (len > 0)
			{
				var enumerator = EditorEvents.FindBestByPosition(row);
				var overlappedNotes = new List<EditorEvent>();
				while (enumerator != null && enumerator.MoveNext())
				{
					var c = enumerator.Current;
					if (c!.GetRow() < row)
						continue;
					if (c.GetLane() != lane)
						continue;
					if (c.GetRow() > row + len)
						break;
					overlappedNotes.Add(c);
				}

				if (overlappedNotes.Count > 0)
				{
					DeleteEvents(overlappedNotes);
					sideEffectDeletedEvents ??= [];
					sideEffectDeletedEvents.AddRange(overlappedNotes);
				}
			}
		}

		// Misc event with no lane.
		else
		{
			// If the same kind of event exists at this row, delete it.
			var eventsAtRow = EditorEvents.FindEventsAtRow(editorEvent.GetRow());
			foreach (var potentialConflictingEvent in eventsAtRow)
			{
				if (editorEvent.GetType() == potentialConflictingEvent.GetType())
				{
					DeleteEvent(potentialConflictingEvent);
					sideEffectDeletedEvents ??= [];
					sideEffectDeletedEvents.Add(potentialConflictingEvent);

					// Determine if the side effect of deleting the conflicting note would
					// affect the rate.
					if (!rateDirty)
					{
						foreach (var deletedEvent in sideEffectDeletedEvents)
						{
							if (deletedEvent is EditorRateAlteringEvent)
							{
								rateDirty = true;
								break;
							}
						}
					}
				}
			}
		}

		// By trying to force add this note, we altered the rate. This can happen for
		// example when forcing a stop to get added over another stop. This will affect
		// the time of the events being force added, so we need to recompute their times.
		if (rateDirty)
		{
			editorEvent.RefreshRowDependencies();
		}

		// Now that all conflicting notes are deleted or adjusted, add this note.
		AddEventInternal(editorEvent);
	}

	/// <summary>
	/// Attempts to move an event from its current position to a new position.
	/// This will not move events if the new row is invalid.
	/// This will not move events if they would conflict with other events.
	/// This will not move events which could cause other events to be deleted.
	/// This will not move hold events.
	/// </summary>
	/// <param name="editorEvent">EditorEvent to move.</param>
	/// <param name="newRow">New row to move the event to.</param>
	/// <returns>True if the event was moved successfully and false otherwise.</returns>
	public bool MoveEvent(EditorEvent editorEvent, int newRow)
	{
		Assert(CanBeEdited());
		if (!CanBeEdited())
			return false;

		if (!CanEventBeMovedToRow(editorEvent, newRow))
			return false;

		Notify(NotificationEventsMoveStart, this, editorEvent);
		DeleteEvent(editorEvent);
		editorEvent.SetRow(newRow);
		AddEvent(editorEvent);
		Notify(NotificationEventsMoveEnd, this, editorEvent);
		return true;
	}

	/// <summary>
	/// Returns whether or not the given row is a valid row for the given EditorEvent
	/// to exist at regardless of other EditorEvents.
	/// </summary>
	/// <param name="editorEvent">EditorEvent in question.</param>
	/// <param name="row">Row in question.</param>
	/// <returns>True if the given EditorEvent can exist at the given row and false otherwise.</returns>
	public bool CanEventExistAtRow(EditorEvent editorEvent, int row)
	{
		if (row < 0)
			return false;
		var lane = editorEvent.GetLane();
		if ((lane != InvalidArrowIndex && lane < 0) || lane >= NumInputs)
			return false;
		if (IsMultiPlayer() && editorEvent is EditorPatternEvent)
			return false;
		return true;
	}

	/// <summary>
	/// Returns whether or not the given EditorEvent can be moved to the given row.
	/// This takes into account other EditorEvents which might conflict with the new row.
	/// This is intended for moving miscellaneous EditorEvents without lanes.
	/// Not all EditorEvents are supported and this will err on returning false.
	/// </summary>
	/// <param name="editorEvent">EditorEvent to move.</param>
	/// <param name="row">New row for the event.</param>
	/// <returns>True if the given EditorEvent can be moved to the new row and false otherwise.</returns>
	public bool CanEventBeMovedToRow(EditorEvent editorEvent, int row)
	{
		// The row must be a valid row for this event to exist at regardless of note conflicts.
		if (!CanEventExistAtRow(editorEvent, row))
			return false;

		// If the move keeps it at the same location, it is allowed.
		if (row == editorEvent.GetRow())
			return true;

		// If there is already an event of the same type at this row then it is not allowed.
		var eventsAtRow = EditorEvents.FindEventsAtRow(row);
		foreach (var e in eventsAtRow)
		{
			if (e.GetType() == editorEvent.GetType() && e.GetLane() == editorEvent.GetLane())
				return false;
		}

		// If this event is a lane note then it can't overlap a hold.
		if (editorEvent.IsLaneNote())
		{
			var potentiallyOverlappingHolds = GetHoldsOverlappingPosition(row);
			foreach (var hold in potentiallyOverlappingHolds)
			{
				if (hold.GetLane() == editorEvent.GetLane())
					return false;
			}
		}

		// TODO: Support movement of holds?
		// If this event is a hold, then the new position can't overlap other notes in the same lane.
		if (editorEvent is EditorHoldNoteEvent)
			return false;

		return true;
	}

	#endregion Adding and Deleting EditorEvents

	#region Cached Data

	public IReadOnlyStepTotals GetStepTotals()
	{
		return StepTotals;
	}

	public string GetStreamBreakdown()
	{
		return StepTotals.GetStepDensity().GetStreamBreakdown();
	}

	#endregion Cached Data

	#region Misc

	/// <summary>
	/// Gets the music file which should be used for this Chart.
	/// This may be defined on the Song.
	/// </summary>
	/// <returns>The music file which should be used for this Chart</returns>
	public string GetMusicFileToUseForChart()
	{
		var musicFile = MusicPath;
		if (string.IsNullOrEmpty(musicFile))
			musicFile = EditorSong?.MusicPath;
		return musicFile;
	}

	public void CopyDisplayTempo(DisplayTempo displayTempo)
	{
		DisplayTempo = new DisplayTempo(displayTempo);
	}

	public bool CanBeEdited()
	{
		// The Chart cannot be edited if work is queued up.
		// The exception to that is if that work itself is synchronous as it means the edit
		// is coming from that enqueued work.
		if (WorkQueue.IsRunningSynchronousWork())
			return true;
		return WorkQueue.IsEmpty();
	}

	public void Update()
	{
		WorkQueue.Update();
	}

	public bool SupportsAutogenFeatures()
	{
		return !IsMultiPlayer();
	}

	#endregion Misc

	#region Selected Rows

	public void BeginRowSelection(EditorPosition position)
	{
		if (!CanBeEdited())
			return;
		if (SelectedRows != null)
			EndRowSelection();

		SelectedRows = (EditorSelectedRowsEvent)EditorEvent.CreateEvent(EventConfig.CreateSelectedRowsConfig(this));
		SelectedRowsStartRow = position.GetNearestRow();
		SelectedRows.SetSelectionRows(SelectedRowsStartRow, SelectedRowsStartRow);
		AddEvent(SelectedRows);
	}

	public void UpdateRowSelection(EditorPosition position)
	{
		if (!CanBeEdited() || SelectedRows == null)
			return;
		var startRow = SelectedRowsStartRow;
		var endRow = position.GetNearestRow();
		if (startRow > endRow)
			(startRow, endRow) = (endRow, startRow);
		DeleteEvent(SelectedRows);
		SelectedRows = (EditorSelectedRowsEvent)EditorEvent.CreateEvent(EventConfig.CreateSelectedRowsConfig(this));
		SelectedRows.SetSelectionRows(startRow, endRow);
		AddEvent(SelectedRows);
	}

	public void EndRowSelection()
	{
		if (!CanBeEdited())
			return;
		if (SelectedRows != null)
			DeleteEvent(SelectedRows);
		SelectedRows = null;
	}

	public EditorSelectedRowsEvent GetRowSelection()
	{
		return SelectedRows;
	}

	#endregion Selected Rows

	#region Pattern Helpers

	/// <summary>
	/// Gets the StepMania Events from this chart that affect timing. See SMCommon.DoesEventAffectTiming.
	/// </summary>
	/// <returns>The StepMania Events from this chart that affect timing.</returns>
	public List<Event> GetSmTimingEvents()
	{
		var smTimingEvents = new List<Event>();
		foreach (var rateAlteringEvent in RateAlteringEvents)
		{
			var smEvent = rateAlteringEvent.GetEvent();
			if (DoesEventAffectTiming(smEvent))
				smTimingEvents.Add(smEvent);
		}

		smTimingEvents.Sort(new SMEventComparer());
		return smTimingEvents;
	}

	/// <summary>
	/// Gets the StepMania events from the chart within a given range that do not affect timing.
	/// Will include holds which start before the start of the range but overlap it.
	/// Will include holds which start in the range but end after it.
	/// Will exclude Pattern events.
	/// These events are not guaranteed to be sorted.
	/// </summary>
	/// <param name="startRowInclusive">Inclusive start row of range.</param>
	/// <param name="endRowInclusive">Inclusive end row of range.</param>
	/// <returns>List of Stepmania Events this EditorChart represents.</returns>
	public (List<Event>, List<EditorEvent>) GetEventsInRangeForPattern(int startRowInclusive, int endRowInclusive)
	{
		var smEvents = new List<Event>();
		var editorEvents = new List<EditorEvent>();
		if (endRowInclusive < startRowInclusive)
			return (smEvents, editorEvents);

		// Check for holds which overlap the start of the range.
		var overlappingHolds = GetHoldsOverlappingPosition(startRowInclusive);
		for (var lane = 0; lane < overlappingHolds.Length; lane++)
		{
			var hold = overlappingHolds[lane];
			if (hold == null)
				continue;
			editorEvents.Add(hold);
			var smEvent = hold.GetEvent();
			if (smEvent != null)
				smEvents.Add(smEvent);
			smEvent = hold.GetAdditionalEvent();
			if (smEvent != null)
				smEvents.Add(smEvent);
		}

		// Check for events within the range.
		var enumerator = EditorEvents.FindLeastAtOrAfterChartPosition(startRowInclusive);
		if (enumerator != null)
		{
			while (enumerator.MoveNext())
			{
				var editorEvent = enumerator.Current;
				// If we have advanced beyond the end of the range we are done.
				var row = editorEvent!.GetRow();
				if (row > endRowInclusive)
					break;

				// Skip holds that start at the start of the range as we have captured them above.
				if (row == startRowInclusive && editorEvent is EditorHoldNoteEvent)
					continue;

				// Don't consider pattern events.
				if (editorEvent is EditorPatternEvent)
					continue;

				// Check the StepMania events for this EditorEvent.
				var addedAny = false;
				var smEvent = editorEvent.GetEvent();
				if (smEvent != null && !DoesEventAffectTiming(smEvent))
				{
					smEvents.Add(smEvent);
					addedAny = true;
				}

				smEvent = editorEvent.GetAdditionalEvent();
				if (smEvent != null && !DoesEventAffectTiming(smEvent))
				{
					smEvents.Add(smEvent);
					addedAny = true;
				}

				// Add the EditorEvent if we added any of the StepMania events.
				if (addedAny)
					editorEvents.Add(editorEvent);
			}
		}

		return (smEvents, editorEvents);
	}

	#endregion Pattern Helpers

	#region IObserver

	public void OnNotify(string eventId, WorkQueue notifier, object payload)
	{
		switch (eventId)
		{
			case WorkQueue.NotificationWorking:
				Notify(NotificationCanEditChanged, this);
				break;
			case WorkQueue.NotificationWorkComplete:
				Notify(NotificationCanEditChanged, this);
				break;
		}
	}

	#endregion IObserver

	#region Saving

	/// <summary>
	/// Performs a series of checks prior to saving to ensure this chart has no incompatibilities with
	/// the given SaveParameters. Will log warnings and errors based on any incompatibilities. This
	/// function only considers the current chart in isolation.
	/// </summary>
	/// <param name="saveParameters">SaveParameters used for saving.</param>
	/// <returns>True if this chart can be saved and false otherwise.</returns>
	public bool PerformPreSaveChecks(SaveParameters saveParameters)
	{
		var canBeSaved = true;

		// Perform format-agnostic checks.
		if (saveParameters.OmitCustomSaveData)
		{
			if (Patterns.GetCount() > 0)
			{
				LogWarn("Chart has Patterns. These will be deleted when saving because \"Remove Custom Save Data\" is selected.");
			}
		}

		// Step F2 routine checks.
		if (saveParameters.UseStepF2ForPumpRoutine && ChartType == ChartType.pump_routine)
		{
			// Player count.
			if (MaxPlayers > StepF2MaxPlayers)
			{
				LogError(
					$"Pump Routine Chart has {MaxPlayers} Players but StepF2 only supports {StepF2MaxPlayers} Players and \"Use StepF2 Format for Pump Routine\" is selected."
					+ " Remove steps for unsupported players and reduce the Players in the Chart Properties window, or save without \"Use StepF2 Format for Pump Routine\".");
				canBeSaved = false;
			}

			// Rolls, lifts, and per-player mines.
			// This O(N) scan is bad but also using StepF2 format and saving pump-routine is rare.
			var hasRolls = false;
			var hasLifts = false;
			var hasNonP1Mines = false;
			foreach (var chartEvent in EditorEvents)
			{
				if (!hasRolls && chartEvent is EditorHoldNoteEvent hn && hn.IsRoll())
					hasRolls = true;
				if (!hasLifts && chartEvent is EditorLiftNoteEvent)
					hasLifts = true;
				if (!hasNonP1Mines && chartEvent is EditorMineNoteEvent m && m.GetPlayer() > 0)
					hasNonP1Mines = true;
			}

			if (hasRolls)
			{
				LogError(
					"Pump Routine Chart has Rolls but StepF2 does not support Rolls and \"Use StepF2 Format for Pump Routine\" is selected."
					+ " Remove all Rolls or save without \"Use StepF2 Format for Pump Routine\".");
				canBeSaved = false;
			}

			if (hasLifts)
			{
				LogError(
					"Pump Routine Chart has Lifts but StepF2 does not support Lifts and \"Use StepF2 Format for Pump Routine\" is selected."
					+ " Remove all Lifts or save without \"Use StepF2 Format for Pump Routine\".");
				canBeSaved = false;
			}

			if (hasNonP1Mines)
			{
				LogWarn(
					"Pump Routine Chart has per-player Mines but StepF2 does not support per-player Mines and \"Use StepF2 Format for Pump Routine\" is selected."
					+ " All Mines will be saved as P1 Mines.");
			}
		}

		// Perform format-specific checks.
		switch (saveParameters.FileType)
		{
			case FileFormatType.SM:
			{
				// Warps affect timing and their presence would result in an incorrectly timed sm chart.
				if (Warps.GetCount() > 0)
				{
					LogError(
						$"Chart has Warps. Stepmania ignores Warps in {FileFormatType.SM} files. Consider using negative Stops.");
					canBeSaved = false;
				}

				// Fake Segments aren't supported but can be ignored.
				if (Fakes.GetCount() > 0)
				{
					LogWarn($"Chart has Fake Regions. Fake Regions are not compatible with {FileFormatType.SM} files.");
				}

				// Labels aren't supported but can be ignored.
				if (Labels.GetCount() > 0)
				{
					LogWarn($"Chart has Labels. Labels are not compatible with {FileFormatType.SM} files.");
				}

				// Multipliers aren't supported but can be ignored.
				if (StepTotals.GetMultipliersCount() > 0)
				{
					var hasIncompatibleMultipliers = true;
					if (StepTotals.GetMultipliersCount() == 1)
					{
						foreach (var editorEvent in EditorEvents)
						{
							if (editorEvent.GetRow() > 0)
								break;
							if (editorEvent is EditorMultipliersEvent m)
							{
								if (m.GetRow() == 0 && m.GetHitMultiplier() == DefaultHitMultiplier &&
								    m.GetHitMultiplier() == DefaultMissMultiplier)
								{
									hasIncompatibleMultipliers = false;
									break;
								}
							}
						}
					}

					if (hasIncompatibleMultipliers)
					{
						LogWarn(
							$"Chart has Combo Multipliers. Combo Multipliers are not compatible with {FileFormatType.SM} files.");
					}
				}

				// Scroll rate events aren't supported but can be ignored.
				foreach (var rateAlteringEvent in RateAlteringEvents)
				{
					if (rateAlteringEvent is EditorScrollRateEvent sre)
					{
						// Ignore the default scroll rate event.
						if (sre.GetRow() == 0 && sre.GetScrollRate().DoubleEquals(DefaultScrollRate))
						{
							continue;
						}

						LogWarn($"Chart has Scroll Rates. Scroll Rates are not compatible with {FileFormatType.SM} files.");
						break;
					}
				}

				// Interpolated scroll rate events aren't supported but can be ignored.
				foreach (var irae in InterpolatedScrollRateEvents)
				{
					// Ignore the default interpolated scroll rate event.
					if (irae.GetRow() == 0 && irae.GetRate().DoubleEquals(DefaultScrollRate) && irae.IsInstant())
					{
						continue;
					}

					LogWarn(
						$"Chart has Interpolated Scroll Rates. Interpolated Scroll Rates are not compatible with {FileFormatType.SM} files.");
					break;
				}

				break;
			}
			case FileFormatType.SSC:
			{
				// Negative stops are ignored in the ssc format.
				foreach (var stop in Stops)
				{
					if (stop.GetStopLengthSeconds() < 0.0f)
					{
						LogError(
							$"Chart has negative Stops. Stepmania ignores negative Stops in {FileFormatType.SSC} files. Consider using Warps.");
						canBeSaved = false;
						break;
					}
				}

				break;
			}
		}

		return canBeSaved;
	}

	/// <summary>
	/// Returns whether this EditorChart's timing and scroll events match those from the given EditorChart.
	/// Logs warnings on any mismatches.
	/// Specifically, this function checks:
	///  - Music offset
	///  - Display tempo
	///  - Stops, Delays, Scroll Rates, Interpolated Scroll Rates, Tempos, Time Signatures, Warps
	/// </summary>
	/// <param name="other">EditorChart to compare this EditorChart to.</param>
	/// <returns>
	/// True if this EditorChart's timing and scroll events match those from the given other EditorChart and false otherwise.
	/// </returns>
	public bool DoTimingAndScrollEventsMatch(EditorChart other)
	{
		if (other == null)
			return false;

		var match = true;

		// Charts must use the same music offset.
		var musicOffset = GetMusicOffset();
		var otherMusicOffset = other.GetMusicOffset();
		if (!musicOffset.DoubleEquals(otherMusicOffset))
		{
			LogWarn(
				$"Music offset ({musicOffset}) does not match offset ({otherMusicOffset}) from {other.GetDescriptiveName()}.");
			match = false;
		}

		// Charts must have the same display bpm.
		if (!DisplayTempo.Matches(other.DisplayTempo))
		{
			LogWarn(
				$"Display tempo ({DisplayTempo}) does not match display tempo ({other.DisplayTempo}) from {other.GetDescriptiveName()}.");
			match = false;
		}

		// All rate altering events between charts must match.
		var rateAlteringEventsMatch = true;
		var numRateAlteringEvents = RateAlteringEvents.GetCount();
		if (numRateAlteringEvents != other.RateAlteringEvents.GetCount())
		{
			rateAlteringEventsMatch = false;
			match = false;
		}

		if (match && numRateAlteringEvents > 0)
		{
			var enumerator = RateAlteringEvents.First();
			var otherEnumerator = other.RateAlteringEvents.First();
			while (enumerator.MoveNext() && otherEnumerator.MoveNext())
			{
				var chartEvent = enumerator.Current;
				var otherChartEvent = otherEnumerator.Current;
				if (!chartEvent!.Matches(otherChartEvent))
				{
					rateAlteringEventsMatch = false;
					match = false;
				}
			}
		}

		// All interpolated scroll rate events between charts must match.
		var numInterpolatedScrollRateEvents = InterpolatedScrollRateEvents.GetCount();
		if (numInterpolatedScrollRateEvents != other.InterpolatedScrollRateEvents.GetCount())
		{
			rateAlteringEventsMatch = false;
			match = false;
		}

		if (match && numInterpolatedScrollRateEvents > 0)
		{
			var enumerator = InterpolatedScrollRateEvents.First();
			var otherEnumerator = other.InterpolatedScrollRateEvents.First();
			while (enumerator.MoveNext() && otherEnumerator.MoveNext())
			{
				var chartEvent = enumerator.Current;
				var otherChartEvent = otherEnumerator.Current;
				if (!chartEvent!.Matches(otherChartEvent))
				{
					rateAlteringEventsMatch = false;
					match = false;
				}
			}
		}

		if (!rateAlteringEventsMatch)
		{
			LogWarn($"Timing and scroll events do not match events from {other.GetDescriptiveName()}.");
		}

		return match;
	}

	/// <summary>
	/// Returns whether this EditorChart's Attacks match those from the given EditorChart.
	/// Logs warnings if they do not match.
	/// </summary>
	/// <param name="other">EditorChart to compare this EditorChart to.</param>
	/// <returns>
	/// True if this EditorChart's attacks match those from the given other EditorChart and false otherwise.
	/// </returns>
	public bool DoAttacksMatch(EditorChart other)
	{
		if ((Attacks?.GetCount() ?? 0) != (other.Attacks?.GetCount() ?? 0))
		{
			LogWarn($"Attacks do not match attacks from {other.GetDescriptiveName()}.");
			return false;
		}

		if (Attacks == null || Attacks.GetCount() == 0)
			return true;

		using var enumerator = Attacks.GetEnumerator();
		using var otherEnumerator = other.Attacks!.GetEnumerator();
		while (enumerator.MoveNext() && otherEnumerator.MoveNext())
		{
			var attack = (EditorAttackEvent)enumerator.Current;
			var otherAttack = (EditorAttackEvent)otherEnumerator.Current;
			if (!attack!.Matches(otherAttack))
			{
				LogWarn($"Attacks do not match attacks from {other.GetDescriptiveName()}.");
				return false;
			}
		}

		return true;
	}

	/// <summary>
	/// Returns whether this EditorChart's TickCounts match those from the given EditorChart.
	/// Logs warnings if they do not match.
	/// </summary>
	/// <param name="other">EditorChart to compare this EditorChart to.</param>
	/// <returns>
	/// True if this EditorChart's tick counts match those from the given other EditorChart and false otherwise.
	/// </returns>
	public bool DoTickCountsMatch(EditorChart other)
	{
		if ((TickCounts?.GetCount() ?? 0) != (other.TickCounts?.GetCount() ?? 0))
		{
			LogWarn($"Tick counts do not match tick counts from {other.GetDescriptiveName()}.");
			return false;
		}

		if (TickCounts == null || TickCounts.GetCount() == 0)
			return true;

		using var enumerator = TickCounts.GetEnumerator();
		using var otherEnumerator = other.TickCounts!.GetEnumerator();
		while (enumerator.MoveNext() && otherEnumerator.MoveNext())
		{
			var tickCount = (EditorTickCountEvent)enumerator.Current;
			var otherTickCount = (EditorTickCountEvent)otherEnumerator.Current;
			if (!tickCount!.Matches(otherTickCount))
			{
				LogWarn($"Tick counts do not match tick counts from {other.GetDescriptiveName()}.");
				return false;
			}
		}

		return true;
	}

	/// <summary>
	/// Returns whether this EditorChart's Labels match those from the given EditorChart.
	/// Logs warnings if they do not match.
	/// </summary>
	/// <param name="other">EditorChart to compare this EditorChart to.</param>
	/// <returns>
	/// True if this EditorChart's labels match those from the given other EditorChart and false otherwise.
	/// </returns>
	public bool DoLabelsMatch(EditorChart other)
	{
		if ((Labels?.GetCount() ?? 0) != (other.Labels?.GetCount() ?? 0))
		{
			LogWarn($"Labels do not match labels from {other.GetDescriptiveName()}.");
			return false;
		}

		if (Labels == null || Labels.GetCount() == 0)
			return true;

		using var enumerator = Labels.GetEnumerator();
		using var otherEnumerator = other.Labels!.GetEnumerator();
		while (enumerator.MoveNext() && otherEnumerator.MoveNext())
		{
			var label = (EditorLabelEvent)enumerator.Current;
			var otherLabel = (EditorLabelEvent)otherEnumerator.Current;
			if (!label!.Matches(otherLabel))
			{
				LogWarn($"Labels do not match labels from {other.GetDescriptiveName()}.");
				return false;
			}
		}

		return true;
	}

	/// <summary>
	/// Returns whether this EditorChart's Fake segments match those from the given EditorChart.
	/// Logs warnings if they do not match.
	/// </summary>
	/// <param name="other">EditorChart to compare this EditorChart to.</param>
	/// <returns>
	/// True if this EditorChart's fake segments match those from the given other EditorChart and false otherwise.
	/// </returns>
	public bool DoFakeSegmentsMatch(EditorChart other)
	{
		if ((Fakes?.GetCount() ?? 0) != (other.Fakes?.GetCount() ?? 0))
		{
			LogWarn($"Fake segments do not match fake segments from {other.GetDescriptiveName()}.");
			return false;
		}

		if (Fakes == null || Fakes.GetCount() == 0)
			return true;

		using var enumerator = Fakes.GetEnumerator();
		using var otherEnumerator = other.Fakes!.GetEnumerator();
		while (enumerator.MoveNext() && otherEnumerator.MoveNext())
		{
			var fakeSegment = enumerator.Current;
			var otherFakeSegment = otherEnumerator.Current;
			if (!fakeSegment!.Matches(otherFakeSegment))
			{
				LogWarn($"Fake segments do not match fake segments from {other.GetDescriptiveName()}.");
				return false;
			}
		}

		return true;
	}

	/// <summary>
	/// Returns whether this EditorChart's multipliers match those from the given EditorChart.
	/// Logs warnings if they do not match.
	/// </summary>
	/// <param name="other">EditorChart to compare this EditorChart to.</param>
	/// <returns>
	/// True if this EditorChart's multipliers match those from the given other EditorChart and false otherwise.
	/// </returns>
	public bool DoMultipliersMatch(EditorChart other)
	{
		if ((Multipliers?.GetCount() ?? 0) != (other.Multipliers?.GetCount() ?? 0))
		{
			LogWarn($"Multipliers do not match multipliers from {other.GetDescriptiveName()}.");
			return false;
		}

		if (Multipliers == null || Multipliers.GetCount() == 0)
			return true;

		using var enumerator = Multipliers.GetEnumerator();
		using var otherEnumerator = other.Multipliers!.GetEnumerator();
		while (enumerator.MoveNext() && otherEnumerator.MoveNext())
		{
			var multipliers = (EditorMultipliersEvent)enumerator.Current;
			var otherMultipliers = (EditorMultipliersEvent)otherEnumerator.Current;
			if (!multipliers!.Matches(otherMultipliers))
			{
				LogWarn($"Multipliers do not match multipliers from {other.GetDescriptiveName()}.");
				return false;
			}
		}

		return true;
	}

	/// <summary>
	/// Generates a list of Events from this EditorChart's EditorEvents.
	/// The list will be sorted appropriately for Stepmania.
	/// </summary>
	/// <returns>List of Stepmania Events this EditorChart represents.</returns>
	public List<Event> GenerateSmEvents()
	{
		var smEvents = new List<Event>();
		foreach (var editorEvent in EditorEvents)
		{
			// Do not include events which aren't normal Stepmania events.
			var smEvent = editorEvent.GetEvent();
			if (smEvent != null && smEvent is not Pattern)
				smEvents.Add(smEvent);
			smEvent = editorEvent.GetAdditionalEvent();
			if (smEvent != null && smEvent is not Pattern)
				smEvents.Add(smEvent);
		}

		smEvents.Sort(new SMEventComparer());
		return smEvents;
	}

	public void SaveToChart(SaveParameters saveParameters, Action<Chart, Dictionary<string, string>> callback)
	{
		var chart = new Chart();
		var customProperties = saveParameters.OmitCustomSaveData ? null : new Dictionary<string, string>();

		// Enqueue a task to save this EditorChart to a Chart.
		// Run this on the main thread so the WorkQueue notifications are processed on the main thread.
		MainThreadDispatcher.RunOnMainThread(() =>
			WorkQueue.Enqueue(new Task(() =>
				{
					chart.Extras = new Extras(OriginalChartExtras);

					chart.Type = ChartTypeString(ChartType);
					chart.DifficultyType = ChartDifficultyType.ToString();
					chart.NumInputs = NumInputs;
					chart.NumPlayers = MaxPlayers;
					chart.DifficultyRating = Rating;
					chart.Extras.AddDestExtra(TagMusic, MusicPath, true);
					chart.Tempo = DisplayTempo.ToString();

					if (saveParameters.AnonymizeSaveData)
					{
						chart.Description = null;
						chart.Author = null;
						chart.Extras.RemoveSourceExtra(TagChartName);
						chart.Extras.RemoveSourceExtra(TagChartStyle);
					}
					else
					{
						chart.Description = Description;
						chart.Author = Credit;
						chart.Extras.AddDestExtra(TagChartName, Name, true);
						chart.Extras.AddDestExtra(TagChartStyle, Style, true);
					}

					// Always set the chart's music offset. Clear any existing extra tag that may be stale.
					chart.ChartOffsetFromMusic = GetMusicOffset();
					chart.Extras.RemoveSourceExtra(TagOffset);

					if (!saveParameters.OmitCustomSaveData)
						SerializeCustomChartData(customProperties);

					var layer = new Layer
					{
						Events = GenerateSmEvents(),
					};
					chart.Layers.Add(layer);
				}),
				// When complete, call the given callback with the saved data.
				() => callback(chart, customProperties)));
	}

	#endregion Saving

	#region Custom Data Serialization

	/// <summary>
	/// Serialize custom data into the given dictionary
	/// </summary>
	/// <param name="customChartProperties">Dictionary of custom song properties to serialize into.</param>
	private void SerializeCustomChartData(Dictionary<string, string> customChartProperties)
	{
		// Serialize the custom data.
		var patterns = new Dictionary<int, EditorPatternEvent.Definition>();
		foreach (var pattern in Patterns)
		{
			patterns[pattern.GetRow()] = pattern.GetDefinition();
		}

		var customSaveData = new CustomSaveDataV1
		{
			MusicOffset = MusicOffset,
			ShouldUseChartMusicOffset = UsesChartMusicOffset,
			ExpressedChartConfig = ExpressedChartConfig,
			Patterns = patterns,
			MaxPlayerIndex = MaxPlayersInternal - 1,
		};
		var jsonString = JsonSerializer.Serialize(customSaveData, CustomSaveDataSerializationOptions);

		// Save the serialized json and version.
		customChartProperties.Add(GetCustomPropertyName(TagCustomChartDataVersion), CustomSaveDataVersion.ToString());
		customChartProperties.Add(GetCustomPropertyName(TagCustomChartData), jsonString);
	}

	/// <summary>
	/// Deserialize custom data stored on the given Song into this EditorSong.
	/// </summary>
	/// <param name="chart">Chart to deserialize custom data from.</param>
	private void DeserializeCustomChartData(Chart chart)
	{
		var versionTag = GetCustomPropertyName(TagCustomChartDataVersion);
		var dataTag = GetCustomPropertyName(TagCustomChartData);

		// Get the version and the serialized custom data.
		if (!chart.Extras.TryGetExtra(versionTag, out string versionString, true))
			return;
		if (!int.TryParse(versionString, out var version))
			return;
		if (!chart.Extras.TryGetExtra(dataTag, out string customSaveDataString, true))
			return;

		// Deserialized the data based on the version.
		switch (version)
		{
			case 1:
			{
				DeserializeV1CustomData(customSaveDataString);
				break;
			}
			default:
			{
				LogWarn($"Unsupported {versionTag}: {version}.");
				break;
			}
		}
	}

	/// <summary>
	/// Deserialize custom data from a serialized string of CustomSaveDataV1 data.
	/// </summary>
	/// <param name="customDataString">Serialized string of CustomSaveDataV1 data.</param>
	/// <returns>True if deserialization was successful and false otherwise.</returns>
	private bool DeserializeV1CustomData(string customDataString)
	{
		try
		{
			var customSaveData =
				JsonSerializer.Deserialize<CustomSaveDataV1>(customDataString, CustomSaveDataSerializationOptions);

			MusicOffset = customSaveData.MusicOffset;
			UsesChartMusicOffset = customSaveData.ShouldUseChartMusicOffset;
			ExpressedChartConfig = customSaveData.ExpressedChartConfig;
			MaxPlayers = customSaveData.MaxPlayerIndex + 1;

			// Add pattern events.
			var alteredPatterns = false;
			if (customSaveData.Patterns != null)
			{
				foreach (var kvp in customSaveData.Patterns)
				{
					// Validate row.
					var row = kvp.Key;
					if (row < 0)
					{
						alteredPatterns = true;
						LogWarn($"Pattern at invalid row {row}. Ignoring this pattern.");
						continue;
					}

					// Validate definition.
					var definition = kvp.Value;
					if (definition.Length < 0)
					{
						alteredPatterns = true;
						LogWarn($"Pattern at row {row} has an invalid length of {definition.Length}. Ignoring this pattern.");
						continue;
					}

					if (PatternConfigManager.Instance.GetConfig(definition.PatternConfigGuid) == null)
					{
						alteredPatterns = true;
						LogWarn(
							$"Pattern at row {row} uses unknown pattern config with guid {definition.PatternConfigGuid}." +
							$" Updating this pattern to use {PatternConfigManager.DefaultPatternConfigSixteenthsName}.");
						definition.PatternConfigGuid = PatternConfigManager.DefaultPatternConfigSixteenthsGuid;
					}

					if (PerformedChartConfigManager.Instance.GetConfig(definition.PerformedChartConfigGuid) == null)
					{
						alteredPatterns = true;
						LogWarn(
							$"Pattern at row {row} uses unknown performed chart config with guid {definition.PerformedChartConfigGuid}." +
							$" Updating this pattern to use {PerformedChartConfigManager.DefaultPerformedChartPatternBalancedConfigName}.");
						definition.PerformedChartConfigGuid =
							PerformedChartConfigManager.DefaultPerformedChartPatternBalancedGuid;
					}

					// Add Pattern event.
					var pattern = (EditorPatternEvent)EditorEvent.CreateEvent(EventConfig.CreatePatternConfig(this, row));
					pattern.SetDefinition(definition);
					AddEvent(pattern);
				}
			}

			if (alteredPatterns)
			{
				ActionQueue.Instance.SetHasUnsavedChanges();
			}

			return true;
		}
		catch (Exception e)
		{
			LogWarn($"Failed to deserialize {GetCustomPropertyName(TagCustomChartData)} value: \"{customDataString}\". {e}");
		}

		return false;
	}

	#endregion Custom Data Serialization

	#region Logging

	private void LogWarn(string message)
	{
		Logger.Warn($"[{GetDescriptiveName()}] {message}");
	}

	private void LogError(string message)
	{
		Logger.Error($"[{GetDescriptiveName()}] {message}");
	}

	#endregion Logging
}

/// <summary>
/// Custom Comparer for Charts.
/// </summary>
internal sealed class ChartComparer : IComparer<EditorChart>
{
	private static readonly Dictionary<ChartType, int> ChartTypeOrder = new()
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
		return string.Compare(s1, s2, StringComparison.CurrentCulture);
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
		int comparison;
		var c1HasCharTypeOrder = ChartTypeOrder.TryGetValue(c1.ChartType, out var c1Order);
		var c2HasCharTypeOrder = ChartTypeOrder.TryGetValue(c2.ChartType, out var c2Order);
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

		comparison = c1.ChartDifficultyType - c2.ChartDifficultyType;
		if (comparison != 0)
			return comparison;
		comparison = c1.Rating - c2.Rating;
		if (comparison != 0)
			return comparison;
		comparison = StringCompare(c1.Name, c2.Name);
		if (comparison != 0)
			return comparison;
		comparison = StringCompare(c1.Description, c2.Description);
		if (comparison != 0)
			return comparison;
		comparison = c1.GetStepTotals().GetStepCount() - c2.GetStepTotals().GetStepCount();
		if (comparison != 0)
			return comparison;
		return c1.GetGuid().CompareTo(c2.GetGuid());
	}

	int IComparer<EditorChart>.Compare(EditorChart c1, EditorChart c2)
	{
		return Compare(c1, c2);
	}
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Fumen;
using Fumen.ChartDefinition;
using StepManiaEditor.AutogenConfig;
using static StepManiaEditor.Utils;
using static Fumen.Converters.SMCommon;
using static System.Diagnostics.Debug;
using static StepManiaLibrary.Constants;

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
	public const string NotificationEventsAdded = "EventsAdded";
	public const string NotificationEventsDeleted = "EventsDeleted";
	public const string NotificationEventsMoveStart = "EventsMoveStart";
	public const string NotificationEventsMoveEnd = "EventsMoveEnd";
	public const string NotificationPatternRequestEdit = "PatternRequestEdit";

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
	/// WorkQueue for long running tasks like saving.
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

	// TODO: Need a read-only interface for these trees for public exposure.

	/// <summary>
	/// Tree of all EditorEvents.
	/// </summary>
	public EventTree EditorEvents;

	/// <summary>
	/// Tree of all EditorHoldNoteEvents.
	/// </summary>
	public EventTree Holds;

	/// <summary>
	/// Tree of all miscellaneous EditorEvents. See IsMiscEvent.
	/// </summary>
	public EventTree MiscEvents;

	/// <summary>
	/// Tree of all EditorRateAlteringEvents.
	/// </summary>
	public RateAlteringEventTree RateAlteringEvents;

	/// <summary>
	/// Tree of all EditorInterpolatedRateAlteringEvents.
	/// </summary>
	public RedBlackTree<EditorInterpolatedRateAlteringEvent> InterpolatedScrollRateEvents;

	/// <summary>
	/// IntervalTree of all EditorStopEvents by time. Stop lengths are in time.
	/// </summary>
	private IntervalTree<double, EditorStopEvent> Stops;

	/// <summary>
	/// IntervalTree of all EditorDelayEvents by time. Delay lengths are in time.
	/// </summary>
	private IntervalTree<double, EditorDelayEvent> Delays;

	/// <summary>
	/// IntervalTree of all EditorFakeSegmentEvent by time. Fake lengths are in time.
	/// </summary>
	private IntervalTree<double, EditorFakeSegmentEvent> Fakes;

	/// <summary>
	/// IntervalTree of all EditorWarpEvents by row.
	/// </summary>
	private IntervalTree<double, EditorWarpEvent> Warps;

	/// <summary>
	/// IntervalTree of all EditorPatternEvents by row.
	/// </summary>
	private IntervalTree<double, EditorPatternEvent> Patterns;

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
	/// Number of players.
	/// </summary>
	public readonly int NumPlayers;

	/// <summary>
	/// Total step counts by lane for this EditorChart.
	/// </summary>
	private readonly int[] StepCountsByLane;

	/// <summary>
	/// Total step count for this EditorChart.
	/// </summary>
	private int StepCount;

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
			MusicPathInternal = value ?? "";
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

	#endregion Properties

	#region Constructors

	public EditorChart(EditorSong editorSong, Chart chart, Fumen.IObserver<EditorChart> observer)
	{
		if (observer != null)
			AddObserver(observer);

		WorkQueue = new WorkQueue();

		ExpressedChartConfigInternal = ExpressedChartConfigManager.DefaultExpressedChartDynamicConfigGuid;

		OriginalChartExtras = chart.Extras;
		EditorSong = editorSong;

		TryGetChartType(chart.Type, out ChartTypeInternal);
		if (Enum.TryParse(chart.DifficultyType, out ChartDifficultyType parsedChartDifficultyType))
			ChartDifficultyTypeInternal = parsedChartDifficultyType;
		RatingInternal = (int)chart.DifficultyRating;

		var chartProperties = GetChartProperties(ChartType);
		NumInputs = chartProperties.GetNumInputs();
		NumPlayers = chartProperties.GetNumPlayers();

		chart.Extras.TryGetExtra(TagChartName, out string parsedName, true);
		NameInternal = parsedName ?? "";
		DescriptionInternal = chart.Description ?? "";
		chart.Extras.TryGetExtra(TagChartStyle, out StyleInternal, true); // Pad or Keyboard
		StyleInternal ??= "";
		CreditInternal = chart.Author ?? "";
		chart.Extras.TryGetExtra(TagMusic, out string musicPath, true);
		MusicPathInternal = musicPath;
		UsesChartMusicOffsetInternal = chart.Extras.TryGetExtra(TagOffset, out double musicOffset, true);
		if (UsesChartMusicOffset)
			MusicOffsetInternal = musicOffset;

		DisplayTempoFromChart = !string.IsNullOrEmpty(chart.Tempo);
		DisplayTempo.FromString(chart.Tempo);

		StepCount = 0;
		StepCountsByLane = new int[NumInputs];
		for (var a = 0; a < NumInputs; a++)
			StepCountsByLane[a] = 0;

		// TODO: I wonder if there is an optimization to not do all the tree parsing for inactive charts.
		SetUpEditorEvents(chart);

		DeserializeCustomChartData(chart);
	}

	public EditorChart(EditorSong editorSong, ChartType chartType, Fumen.IObserver<EditorChart> observer)
	{
		if (observer != null)
			AddObserver(observer);

		WorkQueue = new WorkQueue();

		ExpressedChartConfigInternal = ExpressedChartConfigManager.DefaultExpressedChartDynamicConfigGuid;

		EditorSong = editorSong;
		ChartTypeInternal = chartType;

		var chartProperties = GetChartProperties(ChartType);
		NumInputs = chartProperties.GetNumInputs();
		NumPlayers = chartProperties.GetNumPlayers();

		Name = "";
		Description = "";
		Style = "";
		Credit = "";
		MusicPath = "";
		UsesChartMusicOffset = false;
		DisplayTempoFromChart = false;

		Rating = DefaultRating;

		StepCount = 0;
		StepCountsByLane = new int[NumInputs];
		for (var a = 0; a < NumInputs; a++)
			StepCountsByLane[a] = 0;

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
		tempLayer.Events.Add(new ScrollRateInterpolation(DefaultScrollRate, 0, 0.0, false)
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

	private void SetUpEditorEvents(Chart chart)
	{
		var editorEvents = new EventTree(this);
		var holds = new EventTree(this);
		var rateAlteringEvents = new RateAlteringEventTree(this);
		var interpolatedScrollRateEvents = new RedBlackTree<EditorInterpolatedRateAlteringEvent>();
		var miscEvents = new EventTree(this);

		var pendingHoldStarts = new LaneHoldStartNote[NumInputs];
		var lastScrollRateInterpolationValue = 1.0;
		var firstInterpolatedScrollRate = true;
		var firstTick = true;
		var firstMultipliersEvent = true;

		for (var eventIndex = 0; eventIndex < chart.Layers[0].Events.Count; eventIndex++)
		{
			var chartEvent = chart.Layers[0].Events[eventIndex];
			EditorEvent editorEvent;

			switch (chartEvent)
			{
				case LaneHoldStartNote hsn:
					pendingHoldStarts[hsn.Lane] = hsn;
					continue;
				case LaneHoldEndNote hen:
					editorEvent = EditorEvent.CreateEvent(EventConfig.CreateHoldConfig(this, pendingHoldStarts[hen.Lane], hen));
					pendingHoldStarts[hen.Lane] = null;
					holds.Insert(editorEvent);
					break;
				default:
					editorEvent = EditorEvent.CreateEvent(EventConfig.CreateConfig(this, chartEvent));
					break;
			}

			if (editorEvent != null)
				editorEvents.Insert(editorEvent);

			UpdateCachedDataForAddedEvent(editorEvent);

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
						irae.IsPositionImmutable = firstInterpolatedScrollRate;
						interpolatedScrollRateEvents.Insert(irae);
						lastScrollRateInterpolationValue = scrollRateInterpolation.Rate;

						firstInterpolatedScrollRate = false;
					}

					break;
				}
				case EditorTickCountEvent tce:
					tce.IsPositionImmutable = firstTick;
					firstTick = false;
					break;
				case EditorMultipliersEvent me:
					me.IsPositionImmutable = firstMultipliersEvent;
					firstMultipliersEvent = false;
					break;
			}

			if (editorEvent != null && editorEvent.IsMiscEvent())
				miscEvents.Insert(editorEvent);

			editorEvent?.OnAddedToChart();
		}

		EditorEvents = editorEvents;
		Holds = holds;
		RateAlteringEvents = rateAlteringEvents;
		InterpolatedScrollRateEvents = interpolatedScrollRateEvents;
		MiscEvents = miscEvents;

		RefreshIntervals();

		CleanRateAlteringEvents();

		// Create events that are not derived from the Chart's Events.
		AddPreviewEvent();
		AddLastSecondHintEvent();
	}

	#endregion Constructors

	#region Accessors

	public EditorSong GetEditorSong()
	{
		return EditorSong;
	}

	public double GetMusicOffset()
	{
		if (UsesChartMusicOffset)
			return MusicOffset;
		return EditorSong.MusicOffset;
	}

	public double GetStartingTempo()
	{
		var rae = FindActiveRateAlteringEventForPosition(0.0);
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
		var rae = FindActiveRateAlteringEventForPosition(0.0);
		return rae?.GetTimeSignature().Signature ?? DefaultTimeSignature;
	}

	public string GetDescriptiveName()
	{
		return
			$"{ImGuiUtils.GetPrettyEnumString(ChartType)} {ImGuiUtils.GetPrettyEnumString(ChartDifficultyType)} [{Rating}] {Description}";
	}

	public IEnumerable<EditorPatternEvent> GetPatterns()
	{
		return Patterns;
	}

	public bool HasPatterns()
	{
		return Patterns?.Count > 0;
	}

	#endregion Accessors

	#region Timing Updates

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
		var timePerTempo = new Dictionary<double, double>();
		var lastTempoChangeTime = 0.0;
		var minTempo = double.MaxValue;
		var maxTempo = double.MinValue;

		var warpRowsRemaining = 0;
		var stopTimeRemaining = 0.0;
		var isPositionImmutable = false;
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
		var invalidTimeSignatures = new List<EditorEvent>();

		// TODO: Check handling of negative Tempo warps.

		foreach (var rae in RateAlteringEvents)
		{
			// All rate altering events have only one event associated with them
			Assert(rae.GetEvents().Count == 1);
			var chartEvent = rae.GetFirstEvent();

			// Adjust warp rows remaining.
			// ReSharper disable once PossibleNullReferenceException
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

			switch (chartEvent)
			{
				case Tempo tc:
				{
					lastSecondsPerRow = tc.GetSecondsPerRow(MaxValidDenominator);
					lastRowsPerSecond = tc.GetRowsPerSecond(MaxValidDenominator);

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

					isPositionImmutable = firstTempo;

					if (!firstTempo)
					{
						timePerTempo.TryGetValue(lastTempo, out var currentTempoTime);
						timePerTempo[lastTempo] = currentTempoTime + tc.TimeSeconds - lastTempoChangeTime;
						lastTempoChangeTime = tc.TimeSeconds;
					}

					lastTempo = tc.TempoBPM;
					firstTempo = false;
					break;
				}
				case Stop stop:
				{
					// Add to the stop time rather than replace it because overlapping
					// negative stops stack in Stepmania.
					stopTimeRemaining += stop.LengthSeconds;
					isPositionImmutable = false;
					break;
				}
				case Warp warp:
				{
					// Intentionally do not stack warps to match Stepmania behavior.
					warpRowsRemaining = Math.Max(warpRowsRemaining, warp.LengthIntegerPosition);
					isPositionImmutable = false;
					break;
				}
				case ScrollRate scrollRate:
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

					isPositionImmutable = firstScrollRate;

					firstScrollRate = false;
					break;
				}
				case TimeSignature timeSignature:
				{
					// Ensure that the time signature falls on a measure boundary.
					// Due to deleting events it may be the case that time signatures are
					// no longer valid and they need to be removed.
					if ((firstTimeSignature && chartEvent.IntegerPosition != 0)
					    || (!firstTimeSignature && chartEvent.IntegerPosition !=
						    GetNearestMeasureBoundaryRow(lastTimeSignature, chartEvent.IntegerPosition)))
					{
						invalidTimeSignatures.Add(rae);
						continue;
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

		if (invalidTimeSignatures.Count > 0)
		{
			DeleteEvents(invalidTimeSignatures);
		}

		return invalidTimeSignatures;
	}

	private void RefreshIntervals()
	{
		var stops = new IntervalTree<double, EditorStopEvent>();
		var delays = new IntervalTree<double, EditorDelayEvent>();
		var fakes = new IntervalTree<double, EditorFakeSegmentEvent>();
		var warps = new IntervalTree<double, EditorWarpEvent>();
		var patterns = new IntervalTree<double, EditorPatternEvent>();

		foreach (var editorEvent in EditorEvents)
		{
			switch (editorEvent)
			{
				case EditorFakeSegmentEvent fse:
					fakes.Insert(fse, fse.GetChartTime(), fse.GetEndChartTime());
					break;
				case EditorStopEvent se:
					stops.Insert(se, se.GetChartTime(), se.GetEndChartTime());
					break;
				case EditorDelayEvent de:
					delays.Insert(de, de.GetChartTime(), de.GetEndChartTime());
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
	/// Updates the TimeSeconds and MetricPosition values of all Events in this EditorChart.
	/// If EditorRateAlteringEvents like stops are modified, they affect the timing of all following events.
	/// This function will ensure all Events have correct TimeSeconds and MetricPosition values and
	/// all events are sorted properly when a rate altering event is changed.
	/// </summary>
	/// <returns></returns>
	private List<EditorEvent> UpdateEventTimingData()
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

		// Now, update all time values for all normal notes that correspond to Stepmania chart
		// events. Any of these events, even when added or removed, cannot change the relative
		// order of other such events. As such, we do not need to sort EditorEvents again.
		SetEventTimeAndMetricPositionsFromRows(EditorEvents.Select(e => e.GetFirstEvent()));

		EditorEvents.Validate();

		// Now, update all the rate altering events using the updated times. It is possible that
		// this may result in some events being deleted. The only time this can happen is when
		// deleting a time signature that then invalidates a future time signature. This will
		// not invalidate note times or positions.
		var deletedEvents = CleanRateAlteringEvents();

		// Since holds are treated as one event in the editor and two events in stepmania, we need
		// to manually update the times for the hold ends since they were not included in the previous
		// call to SetEventTimeAndMetricPositionsFromRows to update timing. This needs to be done
		// after we clean the rate altering events because setting their times rely on values cached
		// from performing the clean, like SecondsPerRow, RowsPerSecond, etc.
		foreach (var hold in Holds)
			((EditorHoldNoteEvent)hold).RefreshHoldEndTime();

		EditorEvents.Validate();

		// Finally, re-add any events we deleted above. When re-adding them, we will derive
		// their positions again using the updated timing information.
		if (deletedLastSecondHint)
			AddLastSecondHintEvent();
		if (deletedPreview)
			AddPreviewEvent();

		// Reconstruct Intervals.
		RefreshIntervals();

		EditorEvents.Validate();

		return deletedEvents;
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
		var chartPosition = 0.0;
		TryGetChartPositionFromTime(previewChartTime, ref chartPosition);
		PreviewEvent = new EditorPreviewRegionEvent(this, chartPosition);
		AddEvent(PreviewEvent);
	}

	/// <summary>
	/// Deletes all EditorLastSecondHintEvent.
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
		var chartPosition = 0.0;
		TryGetChartPositionFromTime(EditorSong.LastSecondHint, ref chartPosition);
		LastSecondHintEvent = new EditorLastSecondHintEvent(this, chartPosition);
		AddEvent(LastSecondHintEvent);
	}

	#endregion Time-Based Event Shifting

	#region Position And Time Determination

	public double GetMeasureForChartPosition(double chartPosition)
	{
		var rateEvent = FindActiveRateAlteringEventForPosition(chartPosition);
		if (rateEvent == null)
			return 0.0;
		var timeSigEvent = rateEvent.GetTimeSignature();
		var rowDifference = chartPosition - timeSigEvent.IntegerPosition;
		var rowsPerMeasure = timeSigEvent.Signature.Numerator *
		                     (MaxValidDenominator * NumBeatsPerMeasure / timeSigEvent.Signature.Denominator);
		var measures = rowDifference / rowsPerMeasure;
		measures += timeSigEvent.MetricPosition.Measure;
		return measures;
	}

	public double GetChartPositionForMeasure(int measure)
	{
		// We need to search in order to turn a measure into a row.
		// Do a linear walk of the rate altering events.
		// Most charts have very few rate altering events.
		// Needing to get the position from the measure is a very uncommon use case.
		var rateEventEnumerator = FindActiveRateAlteringEventEnumeratorForPosition(0.0);
		if (rateEventEnumerator == null)
			return 0.0;

		var precedingTimeSignature = rateEventEnumerator.Current!.GetTimeSignature();
		while (measure > precedingTimeSignature.MetricPosition.Measure)
		{
			if (!rateEventEnumerator.MoveNext())
			{
				break;
			}

			var atEnd = false;
			while (rateEventEnumerator.Current.GetTimeSignature() == precedingTimeSignature)
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
			if (measure < nextTimeSignature.MetricPosition.Measure)
			{
				break;
			}

			precedingTimeSignature = nextTimeSignature;
		}

		var precedingTimeSignatureEventMeasure = precedingTimeSignature.MetricPosition.Measure;
		var rowsPerMeasure = precedingTimeSignature.Signature.Numerator *
		                     (MaxValidDenominator * NumBeatsPerMeasure / precedingTimeSignature.Signature.Denominator);
		return precedingTimeSignature.IntegerPosition + (measure - precedingTimeSignatureEventMeasure) * rowsPerMeasure;
	}

	public bool TryGetChartPositionFromTime(double chartTime, ref double chartPosition)
	{
		var rateEvent = FindActiveRateAlteringEventForTime(chartTime, false);
		if (rateEvent == null)
			return false;
		chartPosition = rateEvent.GetChartPositionFromTime(chartTime);
		return true;
	}

	public bool TryGetTimeFromChartPosition(double chartPosition, ref double chartTime)
	{
		var rateEvent = FindActiveRateAlteringEventForPosition(chartPosition, false);
		if (rateEvent == null)
			return false;
		chartTime = rateEvent.GetChartTimeFromPosition(chartPosition);
		return true;
	}

	public bool IsRowOnMeasureBoundary(int row)
	{
		return row == GetNearestMeasureBoundaryRow(row);
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
		var rowsPerBeat = MaxValidDenominator * NumBeatsPerMeasure * beatsPerMeasure
		                  / lastTimeSignature.Signature.Denominator / beatsPerMeasure;
		var rowsPerMeasure = rowsPerBeat * beatsPerMeasure;
		var previousMeasureRow = timeSignatureRow + (row - timeSignatureRow) / rowsPerMeasure * rowsPerMeasure;
		var nextMeasureRow = previousMeasureRow + rowsPerMeasure;
		if (row - previousMeasureRow < nextMeasureRow - row)
			return previousMeasureRow;
		return nextMeasureRow;
	}

	public double GetStartChartTime()
	{
		return 0.0;
	}

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

		return Math.Max(endTime, EditorSong.LastSecondHint);
	}

	public double GetEndPosition()
	{
		var lastEvent = EditorEvents.Last();
		var endPosition = 0.0;
		if (lastEvent.MoveNext() && lastEvent.Current != null)
			endPosition = lastEvent.Current.GetEndRow();

		if (EditorSong.LastSecondHint > 0.0)
		{
			var lastSecondChartPosition = 0.0;
			if (TryGetChartPositionFromTime(EditorSong.LastSecondHint, ref lastSecondChartPosition))
			{
				endPosition = Math.Max(lastSecondChartPosition, endPosition);
			}
		}

		return endPosition;
	}

	#endregion Position And Time Determination

	#region Finding EditorEvents

	public List<IChartRegion> GetRegionsOverlapping(double chartPosition, double chartTime)
	{
		var regions = new List<IChartRegion>();
		var stops = GetStopEventsOverlapping(chartTime);
		if (stops != null)
			regions.AddRange(stops);
		var delays = GetDelayEventOverlapping(chartTime);
		if (delays != null)
			regions.AddRange(delays);
		var fakes = GetFakeSegmentEventOverlapping(chartTime);
		if (fakes != null)
			regions.AddRange(fakes);
		var warps = GetWarpEventOverlapping(chartPosition);
		if (warps != null)
			regions.AddRange(warps);
		if (PreviewEvent.GetChartTime() <= chartTime &&
		    PreviewEvent.GetChartTime() + PreviewEvent.GetRegionDuration() >= chartTime)
			regions.Add(PreviewEvent);
		var patterns = GetPatternEventsOverlapping(chartPosition);
		if (patterns?.Count > 0)
			regions.AddRange(patterns);
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

	private List<EditorFakeSegmentEvent> GetFakeSegmentEventOverlapping(double chartTime)
	{
		return Fakes?.FindAllOverlapping(chartTime);
	}

	private List<EditorWarpEvent> GetWarpEventOverlapping(double chartPosition)
	{
		return Warps?.FindAllOverlapping(chartPosition);
	}

	private List<EditorPatternEvent> GetPatternEventsOverlapping(double chartPosition)
	{
		return Patterns?.FindAllOverlapping(chartPosition);
	}

	private RedBlackTree<EditorRateAlteringEvent>.IRedBlackTreeEnumerator FindActiveRateAlteringEventEnumeratorForTime(
		double chartTime, bool allowEqualTo = true)
	{
		if (RateAlteringEvents == null)
			return null;

		// Given the current song time, get the greatest preceding event which alters the rate of rows to time.
		var pos = (EditorRateAlteringEvent)EditorEvent.CreateEvent(
			EventConfig.CreateSearchEventConfigWithOnlyTime(this, chartTime));
		var enumerator = RateAlteringEvents.FindGreatestPreceding(pos, allowEqualTo);
		// If there is no preceding event (e.g. SongTime is negative), use the first event.
		// ReSharper disable once ConvertIfStatementToNullCoalescingExpression
		if (enumerator == null)
			enumerator = RateAlteringEvents.First();
		// If there is still no event then the Chart is misconfigured as it must have at least a Tempo event.
		if (enumerator == null)
			return null;

		// Update the ChartPosition based on the cached rate information.
		enumerator.MoveNext();
		return enumerator;
	}

	public EditorRateAlteringEvent FindActiveRateAlteringEventForTime(double chartTime, bool allowEqualTo = true)
	{
		var enumerator = FindActiveRateAlteringEventEnumeratorForTime(chartTime, allowEqualTo);
		return enumerator?.Current;
	}

	private RedBlackTree<EditorRateAlteringEvent>.IRedBlackTreeEnumerator FindActiveRateAlteringEventEnumeratorForPosition(
		double chartPosition, bool allowEqualTo = true)
	{
		if (RateAlteringEvents == null)
			return null;

		// Given the current song time, get the greatest preceding event which alters the rate of rows to time.
		var pos = (EditorRateAlteringEvent)EditorEvent.CreateEvent(
			EventConfig.CreateSearchEventConfigWithOnlyRow(this, chartPosition));
		var enumerator = RateAlteringEvents.FindGreatestPreceding(pos, allowEqualTo);
		// If there is no preceding event (e.g. ChartPosition is negative), use the first event.
		// ReSharper disable once ConvertIfStatementToNullCoalescingExpression
		if (enumerator == null)
			enumerator = RateAlteringEvents.First();
		// If there is still no event then the Chart is misconfigured as it must have at least a Tempo event.
		if (enumerator == null)
			return null;

		enumerator.MoveNext();
		return enumerator;
	}

	public EditorRateAlteringEvent FindActiveRateAlteringEventForPosition(double chartPosition, bool allowEqualTo = true)
	{
		var enumerator = FindActiveRateAlteringEventEnumeratorForPosition(chartPosition, allowEqualTo);
		return enumerator?.Current;
	}

	/// <summary>
	/// Given a chart position, returns the next EditorEvent per lane that is relevant for
	/// simulating input. The results are returned as an array where the index is the lane
	/// and the element at each index is a tuple where the first item is the row of the event
	/// and the second item is the event. The events which are relevant for simulating
	/// input are taps (EditorTapNoteEvent), hold downs (EditorHoldNoteEvent) and hold releases
	/// (null). No EditorEvent corresponds to a hold release, so null is returned instead.
	/// </summary>
	public (int, EditorEvent)[] GetNextInputs(double chartPosition)
	{
		var nextNotes = new (int, EditorEvent)[NumInputs];
		for (var i = 0; i < NumInputs; i++)
			nextNotes[i] = (-1, null);
		var numFound = 0;

		// First, scan backwards to find all holds which may be overlapping.
		// Holds may end after the given chart position which started before it.
		var overlappingHolds = GetHoldsOverlapping(chartPosition);
		for (var i = 0; i < NumInputs; i++)
		{
			var hold = overlappingHolds[i];
			if (hold == null)
				continue;
			if (hold.GetRow() >= chartPosition)
				nextNotes[i] = (hold.GetRow(), overlappingHolds[i]);
			else
				nextNotes[i] = (hold.GetEndRow(), null);
			numFound++;
		}

		// Scan forward until we have collected a note for every lane.
		var enumerator = EditorEvents.FindBestByPosition(chartPosition);
		if (enumerator == null)
			return nextNotes;
		while (enumerator.MoveNext() && numFound < NumInputs)
		{
			var c = enumerator.Current;

			if (c!.GetLane() == InvalidArrowIndex || nextNotes[c.GetLane()].Item1 >= 0)
			{
				continue;
			}

			if (!(c is EditorTapNoteEvent || c is EditorHoldNoteEvent))
			{
				continue;
			}

			if (c.GetRow() < chartPosition && c.GetEndRow() >= chartPosition)
			{
				nextNotes[c.GetLane()] = (c.GetEndRow(), null);
				numFound++;
			}

			else if (c.GetRow() >= chartPosition)
			{
				nextNotes[c.GetLane()] = (c.GetRow(), c);
				numFound++;
			}
		}

		return nextNotes;
	}

	/// <summary>
	/// Gets all the holds overlapping the given chart position.
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
	/// that entry in the array will be null. Otherwise it will be the EditorHoldNoteEvent
	/// which overlaps.
	/// </returns>
	public EditorHoldNoteEvent[] GetHoldsOverlapping(double chartPosition,
		RedBlackTree<EditorEvent>.IRedBlackTreeEnumerator explicitEnumerator = null)
	{
		var holds = new EditorHoldNoteEvent[NumInputs];

		RedBlackTree<EditorEvent>.IRedBlackTreeEnumerator enumerator;
		if (explicitEnumerator != null)
			enumerator = explicitEnumerator.Clone();
		else
			enumerator = EditorEvents.FindBestByPosition(chartPosition);
		if (enumerator == null)
			return holds;

		var numLanesChecked = 0;
		var lanesChecked = new bool[NumInputs];
		while (enumerator.MovePrev() && numLanesChecked < NumInputs)
		{
			var e = enumerator.Current;
			var lane = e!.GetLane();
			if (lane >= 0)
			{
				if (!lanesChecked[lane])
				{
					lanesChecked[lane] = true;
					numLanesChecked++;

					if (e.GetRow() <= chartPosition && e.GetRow() + e.GetLength() >= chartPosition && e is EditorHoldNoteEvent hn)
						holds[lane] = hn;
				}
			}
		}

		return holds;
	}

	#endregion Finding EditorEvents

	#region EditorEvent Modification Callbacks

	/// <summary>
	/// Called to update an EditorStopEvent's time.
	/// The EditorChart needs to be responsible for updating Stop time as it can result in the
	/// Stop changing its relative position to other notes.
	/// </summary>
	public void UpdateStopTime(EditorStopEvent stop, double newTime)
	{
		Assert(CanBeEdited());
		if (!CanBeEdited())
			return;

		// Unfortunately, Stepmania treats negative stops as occurring after notes at the same position
		// and positive notes as occurring before notes at the same position. This means that altering the
		// sign will alter how notes are sorted, which means we need to remove the stop and re-add it in
		// order for the EventTree to sort properly.
		// If the sign doesn't change, we still need to update the IntervalTree holding the Stops.
		var signChanged = stop.StopEvent.LengthSeconds < 0.0 != newTime < 0;
		if (signChanged)
		{
			DeleteEvent(stop);
		}
		else
		{
			var deleted = Stops.Delete(stop.GetChartTime(), stop.GetEndChartTime());
			Assert(deleted);
		}

		stop.StopEvent.LengthSeconds = newTime;

		if (signChanged)
		{
			AddEvent(stop);
		}
		else
		{
			Stops.Insert(stop, stop.GetChartTime(), stop.GetEndChartTime());
		}

		// Stops affect timing data.
		UpdateEventTimingData();
	}

	/// <summary>
	/// Called when an EditorDelayEvent's time is modified.
	/// </summary>
	public void OnDelayTimeModified(EditorDelayEvent delay, double oldEndTime, double newEndTime)
	{
		Assert(CanBeEdited());
		if (!CanBeEdited())
			return;

		// Update the IntervalTree holding this Delay.
		var deleted = Delays.Delete(delay.GetChartTime(), oldEndTime);
		Assert(deleted);
		Delays.Insert(delay, delay.GetChartTime(), newEndTime);

		// Delays affect timing data.
		UpdateEventTimingData();
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
		var deleted = Warps.Delete(warp.GetChartPosition(), oldEndPosition);
		Assert(deleted);
		Warps.Insert(warp, warp.GetChartPosition(), newEndPosition);

		// Warps affect timing data.
		UpdateEventTimingData();
	}

	/// <summary>
	/// Called when an EditorFakeSegmentEvent's time is modified.
	/// </summary>
	public void OnFakeSegmentTimeModified(EditorFakeSegmentEvent fake, double oldEndTime, double newEndTime)
	{
		Assert(CanBeEdited());
		if (!CanBeEdited())
			return;

		// Update the IntervalTree holding this Fake.
		var deleted = Fakes.Delete(fake.GetChartTime(), oldEndTime);
		Assert(deleted);
		Fakes.Insert(fake, fake.GetChartTime(), newEndTime);
	}

	/// <summary>
	/// Called when an EditorScrollRateEvent's rate is modified.
	/// </summary>
	public void OnScrollRateModified(EditorScrollRateEvent _)
	{
		Assert(CanBeEdited());
		if (!CanBeEdited())
			return;

		UpdateEventTimingData();
	}

	/// <summary>
	/// Called when an EditorTempoEvent's tempo is modified.
	/// </summary>
	public void OnTempoModified(EditorTempoEvent _)
	{
		Assert(CanBeEdited());
		if (!CanBeEdited())
			return;

		UpdateEventTimingData();
	}

	/// <summary>
	/// Called when an EditorTimeSignatureEvent's signature is modified.
	/// </summary>
	public void OnTimeSignatureModified(EditorTimeSignatureEvent _)
	{
		Assert(CanBeEdited());
		if (!CanBeEdited())
			return;

		UpdateEventTimingData();
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
				e.Current!.PreviousScrollRate = irae.ScrollRateInterpolationEvent.Rate;
			}

			if (e.MoveNext())
			{
				var next = e.Current;
				next!.PreviousScrollRate = irae.ScrollRateInterpolationEvent.Rate;
			}
		}
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
		var deleted = Patterns.Delete(pattern.GetChartPosition(), oldEndPosition);
		Assert(deleted);
		Patterns.Insert(pattern, pattern.GetChartPosition(), newEndPosition);
	}

	public void OnPatternEventRequestEdit(EditorPatternEvent epa)
	{
		Notify(NotificationPatternRequestEdit, this, epa);
	}

	#endregion EditorEvent Modification Callbacks

	#region Adding and Deleting EditorEvents

	/// <summary>
	/// Deletes the given EditorEvent.
	/// This may result in more events being deleted than the ones provided.
	/// </summary>
	/// <param name="editorEvent">EditorEvent to delete.</param>
	/// <returns>List of all deleted EditorEvents</returns>
	public List<EditorEvent> DeleteEvent(EditorEvent editorEvent)
	{
		Assert(CanBeEdited());
		if (!CanBeEdited())
			return new List<EditorEvent>();

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
		Assert(CanBeEdited());
		if (!CanBeEdited())
			return new List<EditorEvent>();

		var allDeletedEvents = new List<EditorEvent>();
		allDeletedEvents.AddRange(editorEvents);

		var rateDirty = false;
		foreach (var editorEvent in editorEvents)
		{
			UpdateCachedDataForDeletedEvent(editorEvent);
			var deleted = EditorEvents.Delete(editorEvent);
			Assert(deleted);

			if (editorEvent.IsMiscEvent())
			{
				deleted = MiscEvents.Delete(editorEvent);
				Assert(deleted);
			}

			switch (editorEvent)
			{
				case EditorFakeSegmentEvent fse:
					deleted = Fakes.Delete(fse.GetChartTime(), fse.GetEndChartTime());
					Assert(deleted);
					break;
				case EditorRateAlteringEvent rae:
				{
					deleted = RateAlteringEvents.Delete(rae);
					Assert(deleted);

					switch (rae)
					{
						case EditorStopEvent se:
							deleted = Stops.Delete(se.GetChartTime(), se.GetEndChartTime());
							Assert(deleted);
							break;
						case EditorDelayEvent de:
							deleted = Delays.Delete(de.GetChartTime(), de.GetEndChartTime());
							Assert(deleted);
							break;
						case EditorWarpEvent we:
							deleted = Warps.Delete(we.GetChartPosition(), we.GetEndChartPosition());
							Assert(deleted);
							break;
					}

					rateDirty = true;
					break;
				}
				case EditorInterpolatedRateAlteringEvent irae:
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
									next!.PreviousScrollRate = prev!.ScrollRateInterpolationEvent.Rate;
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
					deleted = Patterns.Delete(pe.GetChartPosition(), pe.GetEndChartPosition());
					Assert(deleted);
					break;
				}
			}

			editorEvent.OnRemovedFromChart();
		}

		if (rateDirty)
		{
			allDeletedEvents.AddRange(UpdateEventTimingData());
		}

		Notify(NotificationEventsDeleted, this, allDeletedEvents);

		return allDeletedEvents;
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

		AddEvents(new List<EditorEvent> { editorEvent });
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

		foreach (var editorEvent in editorEvents)
		{
			var rateDirty = false;
			UpdateCachedDataForAddedEvent(editorEvent);
			EditorEvents.Insert(editorEvent);
			if (editorEvent.IsMiscEvent())
				MiscEvents.Insert(editorEvent);

			switch (editorEvent)
			{
				case EditorFakeSegmentEvent fse:
					Fakes.Insert(fse, fse.GetChartTime(), fse.GetEndChartTime());
					break;
				case EditorRateAlteringEvent rae:
				{
					RateAlteringEvents.Insert(rae);

					switch (rae)
					{
						case EditorStopEvent se:
							Stops.Insert(se, se.GetChartTime(), se.GetEndChartTime());
							break;
						case EditorDelayEvent de:
							Delays.Insert(de, de.GetChartTime(), de.GetEndChartTime());
							break;
						case EditorWarpEvent we:
							Warps.Insert(we, we.GetChartPosition(), we.GetEndChartPosition());
							break;
					}

					rateDirty = true;
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
							next!.PreviousScrollRate = irae.ScrollRateInterpolationEvent.Rate;
							if (e.MovePrev())
							{
								if (e.MovePrev())
								{
									var prev = e.Current;
									irae.PreviousScrollRate = prev!.ScrollRateInterpolationEvent.Rate;
								}
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
			// We can't just call UpdateEventTimingData once at the end of the loop because
			// note within the song may have their positions altered relative to individual
			// rate altering event notes such that calling SetEventTimeAndMetricPositionsFromRows
			// once at the end re-sorts them based on time differences.
			// To optimize this we could update events only up until the next rate altering event
			// rather than going to the end of the chart each time. For a old style gimmick chart
			// this would be a big perf win.
			// Moving many rate altering events together is not a frequent operation.

			if (rateDirty)
			{
				UpdateEventTimingData();
			}
		}

		Notify(NotificationEventsAdded, this, editorEvents);
	}

	/// <summary>
	/// Adds the given events and ensures the chart is in a consistent state afterwards
	/// by forcibly removing any events which conflict with the events to be added. This
	/// may result in modifications like shortening holds or converting a hold to a tap
	/// which require deleting and then adding a modified event or events. Any events
	/// which were deleted or added as side effects of adding the given events will be
	/// returned.
	/// This method expects that the given events are valid with respect to each other
	/// (for example, no overlapping taps in the the given events) and are valid at their
	/// positions (for example, no time signatures at invalid rows).
	/// </summary>
	/// <param name="events">Events to add.</param>
	/// <returns>
	/// Tuple where the first element is a list of events which were added as a side effect
	/// of adding the given events and the second element is a list of events which were
	/// deleted as a side effect of adding the given events.
	/// </returns>
	public (List<EditorEvent>, List<EditorEvent>) ForceAddEvents(List<EditorEvent> events)
	{
		var sideEffectAddedEvents = new List<EditorEvent>();
		var sideEffectDeletedEvents = new List<EditorEvent>();

		Assert(CanBeEdited());
		if (!CanBeEdited())
			return (sideEffectAddedEvents, sideEffectDeletedEvents);

		foreach (var editorEvent in events)
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
						sideEffectDeletedEvents.AddRange(DeleteEvent(existingNote));
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
						sideEffectDeletedEvents.AddRange(DeleteEvent(existingNote));

						// If the reduction in length is below the min length for a hold, replace it with a tap.
						if (newExistingHoldEndRow <= existingNote.GetRow())
						{
							var replacementEvent = EditorEvent.CreateEvent(EventConfig.CreateTapConfig(this,
								existingNote.GetRow(), existingNote.GetChartTime(), lane));
							AddEvent(replacementEvent);
							sideEffectAddedEvents.Add(replacementEvent);
						}

						// Otherwise, reduce the length by deleting the old hold and adding a new hold.
						else
						{
							var replacementEvent = EditorHoldNoteEvent.CreateHold(
								this, lane, existingNote.GetRow(), newExistingHoldEndRow - existingNote.GetRow(),
								existingHold.IsRoll());
							AddEvent(replacementEvent);
							sideEffectAddedEvents.Add(replacementEvent);
						}
					}
				}

				// If this event is a hold note, delete any note which overlaps the hold.
				var len = editorEvent.GetLength();
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
						sideEffectDeletedEvents.AddRange(DeleteEvents(overlappedNotes));
				}
			}

			// Misc event with no lane.
			else
			{
				// If the same kind of event exists at this row, delete it.
				var enumerator = EditorEvents.Find(editorEvent);
				if (enumerator != null && enumerator.MoveNext())
				{
					sideEffectDeletedEvents.AddRange(DeleteEvent(enumerator.Current));
				}
			}

			// Now that all conflicting notes are deleted or adjusted, add this note.
			AddEvent(editorEvent);
		}

		return (sideEffectAddedEvents, sideEffectDeletedEvents);
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
		var deletedEvents = DeleteEvent(editorEvent);
		editorEvent.SetNewPosition(newRow);
		AddEvent(editorEvent);
		deletedEvents.Remove(editorEvent);
		Assert(deletedEvents.Count == 0);
		Notify(NotificationEventsMoveEnd, this, editorEvent);

		return true;
	}

	/// <summary>
	/// Returns whether the given EditorEvent has the potential to cause extra EditorEvent deletions
	/// if it were to be moved.
	/// </summary>
	/// <param name="editorEvent">EditorEvent in question.</param>
	/// <returns>
	/// True of the given EditorEvent has the potential to cause extra EditorEvent deletions if it were
	/// to be moved and false otherwise.
	/// </returns>
	public static bool CanEventResultInExtraDeletionsWhenMoved(EditorEvent editorEvent)
	{
		return editorEvent is EditorTimeSignatureEvent;
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

		// Do not allow time signatures to move to non-measure boundaries.
		if (editorEvent is EditorTimeSignatureEvent && !IsRowOnMeasureBoundary(row))
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

		// Don't allow moving events which have the potential to cause extra deletions.
		if (CanEventResultInExtraDeletionsWhenMoved(editorEvent))
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
		if (editorEvent is EditorTapNoteEvent
		    || editorEvent is EditorHoldNoteEvent
		    || editorEvent is EditorMineNoteEvent)
		{
			var potentiallyOverlappingHolds = GetHoldsOverlapping(row);
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

		// TODO: Support movement of time signature events?
		// If this event is a time signature, then the new position must be on a measure boundary
		// and future events must continue to fall on a time signature boundary.
		// For now, disallow this kind of movement due to its complexity.
		if (editorEvent is EditorTimeSignatureEvent)
			return false;

		return true;
	}

	#endregion Adding and Deleting EditorEvents

	#region Cached Data

	private void UpdateCachedDataForAddedEvent(EditorEvent editorEvent)
	{
		if (editorEvent is EditorTapNoteEvent or EditorHoldNoteEvent)
		{
			StepCount++;
			StepCountsByLane[editorEvent.GetLane()]++;
		}
	}

	private void UpdateCachedDataForDeletedEvent(EditorEvent editorEvent)
	{
		if (editorEvent is EditorTapNoteEvent or EditorHoldNoteEvent)
		{
			StepCount--;
			StepCountsByLane[editorEvent.GetLane()]--;
		}
	}

	public int GetStepCount()
	{
		return StepCount;
	}

	public int[] GetStepCountByLane()
	{
		return StepCountsByLane;
	}

	#endregion Cached Data

	#region Misc

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

	#endregion Misc

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
	/// Generates a list of Events from this EditorChart's EditorEvents.
	/// The list will be sorted appropriately for Stepmania.
	/// </summary>
	/// <returns>List of Stepmania Events this EditorChart represents.</returns>
	public List<Event> GenerateSmEvents()
	{
		var smEvents = new List<Event>();
		foreach (var editorEvent in EditorEvents)
		{
			var events = editorEvent.GetEvents();
			for (var i = 0; i < events.Count; i++)
			{
				if (events[i] != null
				    // Do not include events which aren't normal Stepmania events.
				    && events[i] is not Pattern)
				{
					smEvents.Add(events[i]);
				}
			}
		}

		smEvents.Sort(new SMEventComparer());
		return smEvents;
	}

	public void SaveToChart(Action<Chart, Dictionary<string, string>> callback)
	{
		var chart = new Chart();
		var customProperties = new Dictionary<string, string>();

		// Enqueue a task to save this EditorChart to a Chart.
		WorkQueue.Enqueue(new Task(() =>
			{
				chart.Extras = new Extras(OriginalChartExtras);
				chart.Type = ChartTypeString(ChartType);
				chart.DifficultyType = ChartDifficultyType.ToString();
				chart.NumInputs = NumInputs;
				chart.NumPlayers = NumPlayers;
				chart.DifficultyRating = Rating;
				chart.Extras.AddDestExtra(TagChartName, Name, true);
				chart.Description = Description;
				chart.Extras.AddDestExtra(TagChartStyle, Style, true);
				chart.Author = Credit;
				chart.Extras.AddDestExtra(TagMusic, MusicPath, true);
				if (UsesChartMusicOffset)
					chart.Extras.AddDestExtra(TagOffset, MusicOffset, true);
				else
					chart.Extras.RemoveSourceExtra(TagOffset);
				chart.Tempo = DisplayTempo.ToString();

				SerializeCustomChartData(customProperties);

				var layer = new Layer
				{
					Events = GenerateSmEvents(),
				};
				chart.Layers.Add(layer);
			}),
			// When complete, call the given callback with the saved data.
			() => callback(chart, customProperties));
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
				Logger.Warn($"Unsupported {versionTag}: {version}.");
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
						Logger.Warn($"Pattern at invalid row {row}. Ignoring this pattern.");
						continue;
					}

					// Validate definition.
					var definition = kvp.Value;
					if (definition.Length < 0)
					{
						alteredPatterns = true;
						Logger.Warn($"Pattern at row {row} has an invalid length of {definition.Length}. Ignoring this pattern.");
						continue;
					}

					if (PatternConfigManager.Instance.GetConfig(definition.PatternConfigGuid) == null)
					{
						alteredPatterns = true;
						Logger.Warn(
							$"Pattern at row {row} uses unknown pattern config with guid {definition.PatternConfigGuid}." +
							$" Updating this pattern to use {PatternConfigManager.DefaultPatternConfigSixteenthsName}.");
						definition.PatternConfigGuid = PatternConfigManager.DefaultPatternConfigSixteenthsGuid;
					}

					if (PerformedChartConfigManager.Instance.GetConfig(definition.PerformedChartConfigGuid) == null)
					{
						alteredPatterns = true;
						Logger.Warn(
							$"Pattern at row {row} uses unknown performed chart config with guid {definition.PerformedChartConfigGuid}." +
							$" Updating this pattern to use {PerformedChartConfigManager.DefaultPerformedChartConfigName}.");
						definition.PerformedChartConfigGuid = PerformedChartConfigManager.DefaultPerformedChartConfigGuid;
					}

					// Add Pattern event.
					var chartTime = 0.0;
					TryGetTimeFromChartPosition(row, ref chartTime);
					var eventConfig = EventConfig.CreatePatternConfig(this, row, chartTime);
					var pattern = new EditorPatternEvent(eventConfig, definition);
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
			Logger.Warn($"Failed to deserialize {GetCustomPropertyName(TagCustomChartData)} value: \"{customDataString}\". {e}");
		}

		return false;
	}

	#endregion Custom Data Serialization
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

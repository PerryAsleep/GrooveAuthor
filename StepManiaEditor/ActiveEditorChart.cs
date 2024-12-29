using System;
using System.Collections.Generic;
using Fumen;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGameExtensions;
using static StepManiaEditor.Editor;
using static StepManiaEditor.Utils;
using static Fumen.Converters.SMCommon;
using static StepManiaEditor.ImGuiUtils;

namespace StepManiaEditor;

/// <summary>
/// An active EditorChart. Active charts are rendered and can be focused.
/// ActiveEditorChart handles Editor-specific functionality for a visible EditorChart
/// including input handling, autoplay, and rendering.
/// </summary>
internal sealed class ActiveEditorChart
{
	private readonly Editor Editor;
	private readonly EditorChart Chart;
	private readonly IReadOnlyZoomManager ZoomManager;
	private readonly IReadOnlyTextureAtlas TextureAtlas;
	private readonly IReadOnlyKeyCommandManager KeyCommandManager;
	private EventSpacingHelper SpacingHelper;
	private readonly List<EditorEvent> VisibleEvents = new();
	private readonly List<EditorMarkerEvent> VisibleMarkers = new();
	private readonly List<IChartRegion> VisibleRegions = new();
	private readonly HashSet<IChartRegion> RegionsOverlappingStart = new();
	private readonly SelectedRegion SelectedRegion = new();
	private readonly Selection Selection = new();
	private EditorPatternEvent LastSelectedPatternEvent;
	private readonly List<EditorEvent> MovingNotes = new();
	private readonly Receptor[] Receptors;
	private readonly LaneEditState[] LaneEditStates;
	private readonly AutoPlayer AutoPlayer;
	private readonly ArrowGraphicManager ArrowGraphicManager;
	private readonly MiscEventWidgetLayoutManager MiscEventWidgetLayoutManager = new();
	private double WaveFormPPS = 1.0;
	private bool ChartHasDedicatedTab;
	private bool ChartIsFocused;
	private int FocalPointScreenSpaceX;
	private int FocalPointScreenSpaceY;
	private readonly UIChartHeader Header;

	/// <summary>
	/// Position. Ideally this would be private but position is tied heavily to systems managed by the Editor including
	/// input, music playback, and other charts.
	/// </summary>
	public EditorPosition Position;

	public ActiveEditorChart(
		Editor editor,
		EditorChart chart,
		IReadOnlyZoomManager zoomManager,
		IReadOnlyTextureAtlas textureAtlas,
		IReadOnlyKeyCommandManager keyCommandManager)
	{
		Editor = editor;
		Chart = chart;
		ZoomManager = zoomManager;
		TextureAtlas = textureAtlas;
		KeyCommandManager = keyCommandManager;

		ArrowGraphicManager = ArrowGraphicManager.CreateArrowGraphicManager(Chart.ChartType);
		var laneEditStates = new LaneEditState[Chart.NumInputs];
		var receptors = new Receptor[Chart.NumInputs];
		for (var i = 0; i < Chart.NumInputs; i++)
		{
			laneEditStates[i] = new LaneEditState();
			receptors[i] = new Receptor(i, ArrowGraphicManager, Chart);
		}

		Receptors = receptors;
		AutoPlayer = new AutoPlayer(Chart, Receptors);
		LaneEditStates = laneEditStates;

		Position = new EditorPosition(OnPositionChanged, Chart);

		Header = new UIChartHeader(Editor, this);
	}

	public void Clear()
	{
		SetFocused(false);
		ChartHasDedicatedTab = false;
	}

	#region Focal Point

	public void SetFocalPoint(int screenSpaceX, int screenSpaceY)
	{
		FocalPointScreenSpaceX = screenSpaceX;
		FocalPointScreenSpaceY = screenSpaceY;
	}

	public Vector2 GetFocalPoint()
	{
		return new Vector2(FocalPointScreenSpaceX, FocalPointScreenSpaceY);
	}

	public Rectangle GetFullChartScreenSpaceArea()
	{
		Editor.GetChartAreaInScreenSpace(out var chartArea);
		chartArea.X = GetScreenSpaceXOfFullChartAreaStart();
		chartArea.Width = GetChartScreenSpaceWidth();
		return chartArea;
	}

	public int GetFocalPointX()
	{
		return FocalPointScreenSpaceX;
	}

	public int GetFocalPointY()
	{
		return FocalPointScreenSpaceY;
	}

	public int GetScreenSpaceXOfFullChartAreaStart()
	{
		return FocalPointScreenSpaceX - (GetLaneAndWaveFormAreaWidth() >> 1) -
		       GetRelativeXPositionOfLanesAndWaveFormFromChartArea();
	}

	public int GetScreenSpaceXOfFullChartAreaEnd()
	{
		return GetScreenSpaceXOfFullChartAreaStart() + GetChartScreenSpaceWidth();
	}

	public int GetScreenSpaceXOfLanesStart()
	{
		return FocalPointScreenSpaceX - (GetLaneAreaWidth() >> 1) - GetRelativeXPositionOfLanesAndWaveFormFromChartArea();
	}

	public int GetScreenSpaceXOfMiscEventsStart()
	{
		var x = GetScreenSpaceXOfLanesStart();
		if (ShouldDrawMiscEvents())
			x -= Preferences.Instance.PreferencesOptions.MiscEventAreaWidth;
		return x;
	}

	public int GetScreenSpaceXOfMiscEventsStartWithCurrentScale()
	{
		var x = GetScreenSpaceXOfLanesStartWithCurrentScale();
		if (ShouldDrawMiscEvents())
			x -= Preferences.Instance.PreferencesOptions.MiscEventAreaWidth;
		return x;
	}

	public int GetScreenSpaceXOfLanesStartWithCurrentScale()
	{
		return FocalPointScreenSpaceX - (GetLaneAreaWidthWithCurrentScale() >> 1) -
		       GetRelativeXPositionOfLanesAndWaveFormFromChartArea();
	}

	public int GetScreenSpaceXOfLaneAndWaveFormStart()
	{
		return FocalPointScreenSpaceX - (GetLaneAndWaveFormAreaWidth() >> 1) -
		       GetRelativeXPositionOfLanesAndWaveFormFromChartArea();
	}

	public int GetScreenSpaceXOfLaneAndWaveFormStartWithCurrentScale()
	{
		return FocalPointScreenSpaceX - (GetLaneAndWaveFormAreaWidthWithCurrentScale() >> 1) -
		       GetRelativeXPositionOfLanesAndWaveFormFromChartArea();
	}

	public int GetScreenSpaceXOfLanesEnd()
	{
		return FocalPointScreenSpaceX + (GetLaneAreaWidth() >> 1);
	}

	public int GetScreenSpaceXOfLanesEndWithCurrentScale()
	{
		return FocalPointScreenSpaceX + (GetLaneAreaWidthWithCurrentScale() >> 1);
	}

	public int GetScreenSpaceXOfLanesAndWaveFormEnd()
	{
		return FocalPointScreenSpaceX + (GetLaneAndWaveFormAreaWidth() >> 1);
	}

	public int GetScreenSpaceXOfMiscEventsEnd()
	{
		var x = GetScreenSpaceXOfLanesAndWaveFormEnd();
		if (ShouldDrawMiscEvents())
		{
			x += GetRightMiscEventPadding();
			x += Preferences.Instance.PreferencesOptions.MiscEventAreaWidth;
		}

		return x;
	}

	public int GetScreenSpaceXOfLanesAndWaveFormEndWithCurrentScale()
	{
		return FocalPointScreenSpaceX + (GetLaneAndWaveFormAreaWidthWithCurrentScale() >> 1);
	}

	public int GetScreenSpaceXOfMiscEventsEndWithCurrentScale()
	{
		var x = GetScreenSpaceXOfLanesAndWaveFormEndWithCurrentScale();
		if (ShouldDrawMiscEvents())
		{
			x += GetRightMiscEventPadding();
			x += Preferences.Instance.PreferencesOptions.MiscEventAreaWidth;
		}

		return x;
	}

	public int GetChartScreenSpaceFocalPointX()
	{
		return FocalPointScreenSpaceX;
	}

	public int GetRelativeXPositionOfLanesAndWaveFormFromChartArea()
	{
		var miscEventPadding = 0;
		if (ShouldDrawMiscEvents())
		{
			// Add width for the misc events on the left.
			miscEventPadding += GetLeftMiscEventPadding();
			miscEventPadding += Preferences.Instance.PreferencesOptions.MiscEventAreaWidth;
		}

		var measureMarkerPadding = (int)(GetMeasureMarkerPadding() -
		                                 EditorMarkerEvent.GetNumberRelativeAnchorPos(ZoomManager.GetSizeZoom()));

		return GetActiveChartBoundaryWidth() + Math.Max(miscEventPadding, measureMarkerPadding);
	}

	public int GetLeftMiscEventPadding()
	{
		return (int)(GetSceneWidgetPadding() - EditorMarkerEvent.GetNumberRelativeAnchorPos(ZoomManager.GetSizeZoom()) +
		             GetMiscEventLeftSideMarkerNumberAllowance() *
		             EditorMarkerEvent.GetNumberAlpha(ZoomManager.GetSizeZoom()));
	}

	public int GetRightMiscEventPadding()
	{
		return GetSceneWidgetPadding();
	}

	public int GetLaneAreaWidth()
	{
		return Receptor.GetReceptorAreaWidth(ZoomManager.GetSizeCap(), TextureAtlas, ArrowGraphicManager, Chart);
	}

	public int GetLaneAreaWidthWithCurrentScale()
	{
		return Receptor.GetReceptorAreaWidth(ZoomManager.GetSizeZoom(), TextureAtlas, ArrowGraphicManager, Chart);
	}

	public int GetLaneAndWaveFormAreaWidth()
	{
		var width = GetLaneAreaWidth();

		// Some chart types are narrower than the waveform.
		// If we are rendering the waveform behind this chart, ensure we reserve enough space for it.
		if (IsFocused())
		{
			var p = Preferences.Instance.PreferencesWaveForm;
			if (p.ShowWaveForm && p.EnableWaveForm && !p.WaveFormScaleWidthToChart)
			{
				width = Math.Max(width, WaveFormTextureWidth);
			}
		}

		return width;
	}

	public int GetLaneAndWaveFormAreaWidthWithCurrentScale()
	{
		var width = GetLaneAreaWidthWithCurrentScale();

		// Some chart types are narrower than the waveform.
		// If we are rendering the waveform behind this chart, ensure we reserve enough space for it.
		if (IsFocused())
		{
			var p = Preferences.Instance.PreferencesWaveForm;
			if (p.ShowWaveForm && p.EnableWaveForm)
			{
				width = Math.Max(width, (int)Editor.GetWaveFormWidth());
			}
		}

		return width;
	}

	public int GetChartScreenSpaceWidth()
	{
		// Start with the receptor width.
		// Do not treat being zoomed out as affecting the area the chart should cover, but do take into
		// account size cap.
		var width = GetLaneAndWaveFormAreaWidth();

		// Add the area on the left for misc event and measure markers.
		width += GetRelativeXPositionOfLanesAndWaveFormFromChartArea();

		// Add the area on the right for misc events.
		var rightMiscEventAreaWidth = 0;
		if (ShouldDrawMiscEvents())
			rightMiscEventAreaWidth = GetRightMiscEventPadding() + Preferences.Instance.PreferencesOptions.MiscEventAreaWidth;

		// Add width for elements which are only enabled for the focused chart.
		var scrollBarAreaWidth = 0;
		if (IsFocused())
		{
			// Add width for the MiniMap if it is mounted to the focused chart.
			var mm = Preferences.Instance.PreferencesMiniMap;
			if (mm.ShowMiniMap)
			{
				switch (mm.MiniMapPosition)
				{
					// Regardless of scaling, add width for the MiniMap if it is mounted to the focused chart.
					case MiniMap.Position.FocusedChartWithScaling:
					case MiniMap.Position.FocusedChartWithoutScaling:
						scrollBarAreaWidth = Math.Max(scrollBarAreaWidth,
							mm.GetPositionOffsetUiScaled() + mm.GetMiniMapWidthScaled());
						break;
				}
			}

			// Add width for the Density Graph if it is mounted to the focused chart.
			var dg = Preferences.Instance.PreferencesDensityGraph;
			if (dg.ShowDensityGraph)
			{
				switch (dg.DensityGraphPositionValue)
				{
					// Regardless of scaling, add width for the Density Graph if it is mounted to the focused chart.
					case PreferencesDensityGraph.DensityGraphPosition.FocusedChartWithScaling:
					case PreferencesDensityGraph.DensityGraphPosition.FocusedChartWithoutScaling:
						scrollBarAreaWidth = Math.Max(scrollBarAreaWidth,
							dg.GetDensityGraphPositionOffsetUiScaled() + dg.GetDensityGraphHeightUiScaled());
						break;
				}
			}
		}

		width += rightMiscEventAreaWidth + scrollBarAreaWidth;

		width += GetActiveChartBoundaryWidth();

		return width;
	}

	#endregion Focal Point

	#region Misc

	private void OnPositionChanged()
	{
		UpdateLaneEditStatesFromPosition();
		Editor.OnActiveChartPositionChanged(this);
	}

	public bool HasDedicatedTab()
	{
		return ChartHasDedicatedTab;
	}

	public void SetDedicatedTab(bool hasDedicatedTab)
	{
		ChartHasDedicatedTab = hasDedicatedTab;
	}

	public bool IsFocused()
	{
		return ChartIsFocused;
	}

	public void SetFocused(bool focused)
	{
		ChartIsFocused = focused;
		if (!ChartIsFocused)
		{
			Selection.ClearSelectedEvents();
			foreach (var laneEditState in LaneEditStates)
			{
				laneEditState.Clear(true);
			}
		}
	}

	public EditorChart GetChart()
	{
		return Chart;
	}

	public bool ShouldDrawMiscEvents()
	{
		// Originally this was limited to only the focused chart.
		// Showing the misc events for all charts is better because it gives more information
		// and it makes the width of individual active charts less variable which makes switching
		// the focused chart less jarring.
		// The only negative to drawing the misc events for all charts is the additional width but
		// this seems like a fair price to pay.
		return true;
	}

	public bool IsVisible()
	{
		if (!Editor.GetChartAreaInScreenSpace(out var chartArea))
			return false;
		var start = GetScreenSpaceXOfFullChartAreaStart();
		if (start > chartArea.X + chartArea.Width)
			return false;
		if (start + GetChartScreenSpaceWidth() - 1 < chartArea.X)
			return false;
		return true;
	}

	#endregion Misc

	#region Accessors

	public Editor GetEditor()
	{
		return Editor;
	}

	public IReadOnlyList<EditorEvent> GetVisibleEvents()
	{
		return VisibleEvents;
	}

	public IReadOnlyList<EditorMarkerEvent> GetVisibleMarkers()
	{
		return VisibleMarkers;
	}

	public IReadOnlyList<IChartRegion> GetVisibleRegions()
	{
		return VisibleRegions;
	}

	public IReadOnlySelectedRegion GetSelectedRegion()
	{
		return SelectedRegion;
	}

	public double GetWaveFormPPS()
	{
		return WaveFormPPS;
	}

	public ArrowGraphicManager GetArrowGraphicManager()
	{
		return ArrowGraphicManager;
	}

	#endregion Accessors

	#region Chart Event Updates

	private int GetMaxMarkersToDrawPerFrame()
	{
		var numVisible = Math.Max(1, Editor.GetNumVisibleActiveCharts());
		return Preferences.Instance.PreferencesOptions.MaxMarkersToDraw / numVisible;
	}

	private int GetMaxEventsToDrawPerFrame()
	{
		var percentage = Chart.GetEvents().GetCount() / (double)Editor.GetNumEventsForAllVisibleActiveCharts();
		percentage = Math.Clamp(percentage, 0.0, 1.0);
		return (int)(Preferences.Instance.PreferencesOptions.MaxEventsToDraw * percentage);
	}

	private int GetMaxRateAlteringEventsToProcessPerFrame()
	{
		var percentage = Chart.GetRateAlteringEvents().GetCount() /
		                 (double)Editor.GetNumRateAlteringEventsForAllVisibleActiveCharts();
		percentage = Math.Clamp(percentage, 0.0, 1.0);
		var numEvents = (int)(Preferences.Instance.PreferencesOptions.MaxRateAlteringEventsToProcessPerFrame * percentage);
		// Always return at least a small number of events to correctly process the common
		// events at the start of a chart.
		return Math.Max(numEvents, 7);
	}

	/// <summary>
	/// Sets VisibleEvents, VisibleMarkers, and VisibleRegions to store the currently visible
	/// objects based on the current EditorPosition and the SpacingMode.
	/// Updates SelectedRegion.
	/// </summary>
	/// <remarks>
	/// Sets the WaveFormPPS.
	/// </remarks>
	public void UpdateChartEvents(int screenHeight)
	{
		if (Chart.GetEvents() == null)
			return;

		// Clear the current state of visible events
		VisibleEvents.Clear();
		VisibleMarkers.Clear();
		VisibleRegions.Clear();
		RegionsOverlappingStart.Clear();
		SelectedRegion.ClearPerFrameData();

		// Get an EventSpacingHelper to perform y calculations.
		SpacingHelper = EventSpacingHelper.GetSpacingHelper(Chart);

		var maxEventsToDraw = GetMaxEventsToDrawPerFrame();
		var maxRateAlteringEventsToProcess = GetMaxRateAlteringEventsToProcessPerFrame();
		var maxMarkersToDraw = GetMaxMarkersToDrawPerFrame();

		var noteEvents = new List<EditorEvent>();
		var numArrows = Chart.NumInputs;

		var spacingZoom = ZoomManager.GetSpacingZoom();
		var sizeZoom = ZoomManager.GetSizeZoom();

		// Determine graphic dimensions based on the zoom level.
		var (arrowW, arrowH) = GetArrowDimensions();
		var (holdCapTexture, _) = ArrowGraphicManager.GetHoldEndTexture(0, 0, false, false);
		var (_, holdCapTextureHeight) = TextureAtlas.GetDimensions(holdCapTexture);
		var holdCapHeight = holdCapTextureHeight * sizeZoom;
		if (ArrowGraphicManager.AreHoldCapsCentered())
			holdCapHeight *= 0.5;

		// Determine the starting x and y position in screen space.
		// Y extended slightly above the top of the screen so that we start drawing arrows
		// before their midpoints.
		var startPosX = FocalPointScreenSpaceX - numArrows * arrowW * 0.5;
		var startPosY = 0.0 - Math.Max(holdCapHeight, arrowH * 0.5);

		var noteAlpha = (float)Interpolation.Lerp(1.0, 0.0, NoteScaleToStartFading, NoteMinScale, sizeZoom);

		// Set up the MiscEventWidgetLayoutManager.
		var miscEventAlpha = (float)Interpolation.Lerp(1.0, 0.0, MiscEventScaleToStartFading, MiscEventMinScale, sizeZoom);
		BeginMiscEventWidgetLayoutManagerFrame();

		// TODO: Fix Negative Scrolls resulting in cutting off notes prematurely.
		// If a chart has negative scrolls then we technically need to render notes which come before
		// the chart position at the top of the screen.
		// More likely the most visible problem will be at the bottom of the screen where if we
		// were to detect the first note which falls below the bottom it would prevent us from
		// finding the next set of notes which might need to be rendered because they appear 
		// above.

		// Get the current time and position.
		var time = Position.ChartTime;
		var chartPosition = Position.ChartPosition;

		// Find the interpolated scroll rate to use as a multiplier.
		var interpolatedScrollRate = GetCurrentInterpolatedScrollRate();

		// Now, scroll up to the top of the screen so we can start processing events going downwards.
		// We know what time / pos we are drawing at the receptors, but not the rate to get to that time from the top
		// of the screen.
		// We need to find the greatest preceding rate event, and continue until it is beyond the start of the screen.
		// Then we need to find the greatest preceding notes by scanning upwards.
		// Once we find that note, we start iterating downwards while also keeping track of the rate events along the way.

		var rateEnumerator = Chart.GetRateAlteringEvents().FindBest(Position);
		if (rateEnumerator == null)
			return;

		// Scan upwards to find the earliest rate altering event that should be used to start rendering.
		var previousRateEventY = (double)FocalPointScreenSpaceY;
		var previousRateEventRow = chartPosition;
		var previousRateEventTime = time;
		EditorRateAlteringEvent rateEvent = null;
		while (previousRateEventY >= startPosY && rateEnumerator.MovePrev())
		{
			// On the rate altering event which is active for the current chart position,
			// Record the pixels per second to use for the WaveForm.
			if (rateEvent == null)
				SetWaveFormPps(rateEnumerator.Current, interpolatedScrollRate);

			rateEvent = rateEnumerator.Current;
			SpacingHelper.UpdatePpsAndPpr(rateEvent, interpolatedScrollRate, spacingZoom);
			previousRateEventY = SpacingHelper.GetY(rateEvent!.GetChartTime(), rateEvent.GetRow(), previousRateEventY,
				previousRateEventTime, previousRateEventRow);
			previousRateEventRow = rateEvent.GetRow();
			previousRateEventTime = rateEvent.GetChartTime();
		}

		// Now we know the position of first rate altering event to use.
		// We can now determine the chart time and position at the top of the screen.
		var (chartTimeAtTopOfScreen, chartPositionAtTopOfScreen) =
			SpacingHelper.GetChartTimeAndRow(startPosY, previousRateEventY, rateEvent!.GetChartTime(), rateEvent.GetRow());

		var beatMarkerRow = (int)chartPositionAtTopOfScreen;
		var beatMarkerLastRecordedRow = -1;
		var numRateAlteringEventsProcessed = 1;

		// Now that we know the position at the start of the screen we can find the first event to start rendering.
		var enumerator = Chart.GetEvents().FindBestByPosition(chartPositionAtTopOfScreen);
		if (enumerator == null)
			return;

		// Scan backwards until we have checked every lane for a long note which may
		// be extending through the given start row. We cannot add the end events yet because
		// we do not know at what position they will end until we scan down.
		var holdsNeedingToBeCompleted = new HashSet<EditorHoldNoteEvent>();
		var holdNotes = ScanBackwardsForHolds(enumerator, chartPositionAtTopOfScreen);
		foreach (var hn in holdNotes)
		{
			// This is technically incorrect.
			// We are using the rate altering event active at the screen, but there could be more
			// rate altering events between the top of the screen and the start of the hold.
			hn.SetDimensions(
				startPosX + hn.GetLane() * arrowW,
				SpacingHelper.GetY(hn, previousRateEventY) - arrowH * 0.5,
				arrowW,
				0.0, // we do not know the height yet.
				sizeZoom);
			noteEvents.Add(hn);

			holdsNeedingToBeCompleted.Add(hn);
		}

		var hasNextRateEvent = rateEnumerator.MoveNext();
		var nextRateEvent = hasNextRateEvent ? rateEnumerator.Current : null;

		var regionsNeedingToBeAdded = new List<IChartRegion>();
		var addedRegions = new HashSet<IChartRegion>();

		// Start any regions including the selected region.
		// This call will also check for completing regions within the current rate altering event.
		StartRegions(ref regionsNeedingToBeAdded, ref addedRegions, previousRateEventY, rateEvent, nextRateEvent, startPosX,
			numArrows * arrowW, chartTimeAtTopOfScreen, chartPositionAtTopOfScreen);
		// Check for completing holds within the current rate altering event.
		CheckForCompletingHolds(holdsNeedingToBeCompleted, previousRateEventY, nextRateEvent);

		// Now we can scan forward
		var reachedEndOfScreen = false;
		while (enumerator.MoveNext())
		{
			var e = enumerator.Current;

			// Check to see if we have crossed into a new rate altering event section
			if (nextRateEvent != null && e == nextRateEvent)
			{
				var rateEventY = SpacingHelper.GetY(e, previousRateEventY);

				// Add a misc widget for this rate event.
				if (ShouldDrawMiscEvents())
				{
					nextRateEvent.Alpha = miscEventAlpha;
					MiscEventWidgetLayoutManager.PositionEvent(nextRateEvent, rateEventY);
					noteEvents.Add(nextRateEvent);
				}

				// Add a region for this event if appropriate.
				if (nextRateEvent is IChartRegion region)
					AddRegion(region, false, ref regionsNeedingToBeAdded, ref addedRegions, previousRateEventY, nextRateEvent,
						startPosX,
						numArrows * arrowW);

				// Update beat markers for the section for the previous rate event.
				UpdateBeatMarkers(rateEvent, ref beatMarkerRow, ref beatMarkerLastRecordedRow, nextRateEvent, startPosX, sizeZoom,
					previousRateEventY, screenHeight, maxMarkersToDraw);

				// Update rate parameters.
				rateEvent = nextRateEvent;
				SpacingHelper.UpdatePpsAndPpr(rateEvent, interpolatedScrollRate, spacingZoom);
				previousRateEventY = rateEventY;

				// Advance next rate altering event.
				hasNextRateEvent = rateEnumerator.MoveNext();
				nextRateEvent = hasNextRateEvent ? rateEnumerator.Current : null;

				// Update any regions needing to be updated based on the new rate altering event.
				UpdateRegions(ref regionsNeedingToBeAdded, ref addedRegions, previousRateEventY, rateEvent, nextRateEvent);
				// Check for completing any holds needing to be completed within the new rate altering event.
				CheckForCompletingHolds(holdsNeedingToBeCompleted, previousRateEventY, nextRateEvent);

				numRateAlteringEventsProcessed++;
				continue;
			}

			// Determine y position.
			var y = SpacingHelper.GetY(e, previousRateEventY);
			var arrowY = y - arrowH * 0.5;

			// If we have advanced beyond the end of the screen we can finish.
			// An exception to this rule is if the current scroll rate is negative. We do not
			// want to end processing on a negative region, particularly for regions which end
			// beyond the end of the screen.
			if (arrowY > screenHeight && !SpacingHelper.IsScrollRateNegative())
			{
				reachedEndOfScreen = true;
				break;
			}

			// Record note.
			if (e!.IsLaneNote())
			{
				noteEvents.Add(e);
				e.SetDimensions(startPosX + e.GetLane() * arrowW, arrowY, arrowW, arrowH, sizeZoom);
				e.Alpha = noteAlpha;

				if (e is EditorHoldNoteEvent hn)
				{
					// Record that there is in an in-progress hold that will need to be ended.
					if (!CheckForCompletingHold(hn, previousRateEventY, nextRateEvent))
						holdsNeedingToBeCompleted.Add(hn);
				}
			}
			else
			{
				if (e!.IsMiscEvent())
				{
					if (ShouldDrawMiscEvents())
					{
						e.Alpha = miscEventAlpha;
						MiscEventWidgetLayoutManager.PositionEvent(e, y);
						noteEvents.Add(e);
					}
				}
				else
				{
					noteEvents.Add(e);
				}

				// Add a region for this event if appropriate.
				if (e is IChartRegion region)
					AddRegion(region, false, ref regionsNeedingToBeAdded, ref addedRegions, previousRateEventY, nextRateEvent,
						startPosX,
						numArrows * arrowW);
			}

			// If we have collected the maximum number of events per frame, stop processing.
			if (noteEvents.Count > maxEventsToDraw)
				break;
		}

		// Now we need to wrap up any holds which are still not yet complete.
		// We do not need to scan forward for more rate events.
		CheckForCompletingHolds(holdsNeedingToBeCompleted, previousRateEventY, null);

		// We also need to update beat markers beyond the final note.
		UpdateBeatMarkers(rateEvent, ref beatMarkerRow, ref beatMarkerLastRecordedRow, nextRateEvent, startPosX, sizeZoom,
			previousRateEventY, screenHeight, maxMarkersToDraw);

		// If the user is selecting a region and is zoomed out so far that we processed the maximum number of notes
		// per frame without finding both ends of the selected region, then keep iterating through rate altering events
		// to try and complete the selected region.
		if (!reachedEndOfScreen && SelectedRegion.IsActive())
		{
			while (nextRateEvent != null
			       && (!SelectedRegion.HasStartYBeenUpdatedThisFrame() || !SelectedRegion.HaveCurrentValuesBeenUpdatedThisFrame())
			       && numRateAlteringEventsProcessed < maxRateAlteringEventsToProcess)
			{
				var rateEventY = SpacingHelper.GetY(nextRateEvent, previousRateEventY);
				rateEvent = nextRateEvent;
				SpacingHelper.UpdatePpsAndPpr(rateEvent, interpolatedScrollRate, spacingZoom);
				previousRateEventY = rateEventY;

				// Advance to the next rate altering event.
				hasNextRateEvent = rateEnumerator.MoveNext();
				nextRateEvent = hasNextRateEvent ? rateEnumerator.Current : null;

				// Update any regions needing to be updated based on the new rate altering event.
				UpdateRegions(ref regionsNeedingToBeAdded, ref addedRegions, previousRateEventY, rateEvent, nextRateEvent);
				numRateAlteringEventsProcessed++;
			}
		}

		// Normal case of needing to complete regions which end beyond the bounds of the screen.
		EndRegions(ref regionsNeedingToBeAdded, ref addedRegions, previousRateEventY, rateEvent);

		// Sort regions by their z value.
		VisibleRegions.Sort((lhs, rhs) => lhs.GetRegionZ().CompareTo(rhs.GetRegionZ()));

		// Store the notes and holds so we can render them.
		VisibleEvents.AddRange(noteEvents);
	}

	private (double, double) GetArrowDimensions(bool scaled = true)
	{
		var (arrowTexture, _) = ArrowGraphicManager.GetArrowTexture(0, 0, false);
		(double arrowW, double arrowH) = TextureAtlas.GetDimensions(arrowTexture);
		if (scaled)
		{
			var sizeZoom = ZoomManager.GetSizeZoom();
			arrowW *= sizeZoom;
			arrowH *= sizeZoom;
		}

		return (arrowW, arrowH);
	}

	private double GetHoldCapHeight()
	{
		var (holdCapTexture, _) = ArrowGraphicManager.GetHoldEndTexture(0, 0, false, false);
		var (_, holdCapTextureHeight) = TextureAtlas.GetDimensions(holdCapTexture);
		var holdCapHeight = holdCapTextureHeight * ZoomManager.GetSizeZoom();
		if (ArrowGraphicManager.AreHoldCapsCentered())
			holdCapHeight *= 0.5;
		return holdCapHeight;
	}

	private void BeginMiscEventWidgetLayoutManagerFrame()
	{
		var startPosX = FocalPointScreenSpaceX - (GetLaneAreaWidthWithCurrentScale() >> 1);
		var endXPos = FocalPointScreenSpaceX + (GetLaneAreaWidthWithCurrentScale() >> 1);
		var lMiscWidgetPos = startPosX - GetLeftMiscEventPadding();
		var rMiscWidgetPos = endXPos + GetRightMiscEventPadding();
		MiscEventWidgetLayoutManager.BeginFrame(lMiscWidgetPos, rMiscWidgetPos,
			Preferences.Instance.PreferencesOptions.MiscEventAreaWidth);
	}

	/// <summary>
	/// Sets the pixels per second to use on the WaveFormRenderer.
	/// </summary>
	/// <param name="rateEvent">Current rate altering event.</param>
	/// <param name="interpolatedScrollRate">Current interpolated scroll rate.</param>
	/// <remarks>Helper for UpdateChartEvents.</remarks>
	private void SetWaveFormPps(EditorRateAlteringEvent rateEvent, double interpolatedScrollRate)
	{
		var pScroll = Preferences.Instance.PreferencesScroll;
		switch (pScroll.SpacingMode)
		{
			case SpacingMode.ConstantTime:
				WaveFormPPS = pScroll.TimeBasedPixelsPerSecond;
				break;
			case SpacingMode.ConstantRow:
				WaveFormPPS = pScroll.RowBasedPixelsPerRow * rateEvent.GetRowsPerSecond();
				if (pScroll.RowBasedWaveFormScrollMode == WaveFormScrollMode.MostCommonTempo)
					WaveFormPPS *= Chart.GetMostCommonTempo() / rateEvent.GetTempo();
				break;
			case SpacingMode.Variable:
				var tempo = Chart.GetMostCommonTempo();
				if (pScroll.RowBasedWaveFormScrollMode != WaveFormScrollMode.MostCommonTempo)
					tempo = rateEvent.GetTempo();
				var useRate = pScroll.RowBasedWaveFormScrollMode ==
				              WaveFormScrollMode.CurrentTempoAndRate;
				WaveFormPPS = pScroll.VariablePixelsPerSecondAtDefaultBPM
				              * (tempo / PreferencesScroll.DefaultVariablePixelsPerSecondAtDefaultBPM);
				if (useRate)
				{
					var rate = rateEvent.GetScrollRate() * interpolatedScrollRate;
					if (rate <= 0.0)
						rate = 1.0;
					WaveFormPPS *= rate;
				}

				break;
		}
	}

	/// <summary>
	/// Gets the current interpolated scroll rate to use for the active Chart.
	/// </summary>
	/// <remarks>Helper for UpdateChartEvents.</remarks>
	/// <returns>Interpolated scroll rate.</returns>
	private double GetCurrentInterpolatedScrollRate()
	{
		// Find the interpolated scroll rate to use as a multiplier.
		// The interpolated scroll rate to use is the value at the current exact time.
		if (Preferences.Instance.PreferencesScroll.SpacingMode == SpacingMode.Variable)
			return Chart.GetInterpolatedScrollRateEvents().FindScrollRate(Position);
		return 1.0;
	}

	private void AddRegion(
		IChartRegion region,
		bool overlapsStart,
		ref List<IChartRegion> regionsNeedingToBeAdded,
		ref HashSet<IChartRegion> addedRegions,
		double previousRateEventY,
		EditorRateAlteringEvent nextRateEvent,
		double x,
		double w)
	{
		if (region == null)
			return;
		if (regionsNeedingToBeAdded.Contains(region) || addedRegions.Contains(region))
			return;
		if (!SpacingHelper.DoesRegionHavePositiveDuration(region))
			return;
		region.SetRegionX(x);
		region.SetRegionY(SpacingHelper.GetRegionY(region, previousRateEventY));
		region.SetRegionW(w);
		regionsNeedingToBeAdded.Add(region);

		if (overlapsStart)
			RegionsOverlappingStart.Add(region);

		// This region may also complete during this rate altering event.
		CheckForCompletingRegions(ref regionsNeedingToBeAdded, ref addedRegions, previousRateEventY, nextRateEvent);
	}

	private void CheckForCompletingRegions(
		ref List<IChartRegion> regionsNeedingToBeAdded,
		ref HashSet<IChartRegion> addedRegions,
		double previousRateEventY,
		EditorRateAlteringEvent nextRateEvent)
	{
		var remainingRegionsNeededToBeAdded = new List<IChartRegion>();
		foreach (var region in regionsNeedingToBeAdded)
		{
			if (nextRateEvent == null || SpacingHelper.DoesRegionEndBeforeEvent(region, nextRateEvent))
			{
				var h = SpacingHelper.GetRegionH(region, previousRateEventY);

				// If when rendering the first rate altering event we have is something like stop, it will then cause regions
				// overlapping the start of the viewable area to have positive Y values even when they occur before the stop.
				// For the normal SpacingHelper math to work, we need to ensure that we do math based on off a preceding rate
				// altering event and not a following one. But for very long regions (like patterns), we don't really want to
				// be iterating backwards to find that event to get an accurate start time. Instead just ensure that any region
				// that actually starts above the screen doesn't render as starting below the top of the screen. This makes the
				// region bounds technically wrong, but they are wrong already due to similar issues in ending them when they
				// end beyond the end of the screen. The regions are used for rendering so this is acceptable.
				if (RegionsOverlappingStart.Contains(region))
				{
					var regionY = region.GetRegionY();
					if (regionY > 0.0)
					{
						region.SetRegionY(0.0);
						h += regionY;
					}

					RegionsOverlappingStart.Remove(region);
				}

				region.SetRegionH(h);
				VisibleRegions.Add(region);
				addedRegions.Add(region);
				continue;
			}

			remainingRegionsNeededToBeAdded.Add(region);
		}

		regionsNeedingToBeAdded = remainingRegionsNeededToBeAdded;
	}

	private void CheckForUpdatingSelectedRegionStartY(SelectedRegion selectedRegion, double previousRateEventY,
		EditorRateAlteringEvent rateEvent,
		EditorRateAlteringEvent nextRateEvent)
	{
		if (!selectedRegion.IsActive() || selectedRegion.HasStartYBeenUpdatedThisFrame())
			return;

		switch (Preferences.Instance.PreferencesScroll.SpacingMode)
		{
			case SpacingMode.ConstantTime:
			{
				if (selectedRegion.GetStartChartTime() < rateEvent.GetChartTime()
				    || nextRateEvent == null
				    || selectedRegion.GetStartChartTime() < nextRateEvent.GetChartTime())
				{
					selectedRegion.UpdatePerFrameDerivedStartY(SpacingHelper.GetY(selectedRegion.GetStartChartTime(),
						selectedRegion.GetStartChartPosition(), previousRateEventY, rateEvent.GetChartTime(),
						rateEvent.GetRow()));
				}

				break;
			}
			case SpacingMode.ConstantRow:
			case SpacingMode.Variable:
			{
				if (selectedRegion.GetStartChartPosition() < rateEvent.GetRow()
				    || nextRateEvent == null
				    || selectedRegion.GetStartChartPosition() < nextRateEvent.GetRow())
				{
					selectedRegion.UpdatePerFrameDerivedStartY(SpacingHelper.GetY(selectedRegion.GetStartChartTime(),
						selectedRegion.GetStartChartPosition(), previousRateEventY, rateEvent.GetChartTime(),
						rateEvent.GetRow()));
				}

				break;
			}
		}
	}

	private void CheckForUpdatingSelectedRegionCurrentValues(
		SelectedRegion selectedRegion,
		double previousRateEventY,
		EditorRateAlteringEvent rateEvent,
		EditorRateAlteringEvent nextRateEvent)
	{
		if (!selectedRegion.IsActive() || selectedRegion.HaveCurrentValuesBeenUpdatedThisFrame())
			return;

		if (selectedRegion.GetCurrentYInScreenSpace() < previousRateEventY
		    || nextRateEvent == null
		    || selectedRegion.GetCurrentYInScreenSpace() < SpacingHelper.GetY(nextRateEvent, previousRateEventY))
		{
			var (chartTime, chartPosition) = SpacingHelper.GetChartTimeAndRow(
				selectedRegion.GetCurrentYInScreenSpace(), previousRateEventY, rateEvent.GetChartTime(), rateEvent.GetRow());
			selectedRegion.UpdatePerFrameDerivedChartTimeAndPosition(chartTime, chartPosition);
		}
	}

	/// <summary>
	/// Handles starting and updating any pending regions at the start of the main tick loop
	/// when the first rate altering event is known.
	/// Pending regions include normal regions needing to be added to VisibleRegions,
	/// the preview region, and the SelectedRegion.
	/// </summary>
	/// <remarks>Helper for UpdateChartEvents.</remarks>
	private void StartRegions(
		ref List<IChartRegion> regionsNeedingToBeAdded,
		ref HashSet<IChartRegion> addedRegions,
		double previousRateEventY,
		EditorRateAlteringEvent rateEvent,
		EditorRateAlteringEvent nextRateEvent,
		double chartRegionX,
		double chartRegionW,
		double chartTimeAtTopOfScreen,
		double chartPositionAtTopOfScreen)
	{
		// Check for adding regions which extend through the top of the screen.
		var regions = Chart.GetRegionsOverlapping(chartPositionAtTopOfScreen, chartTimeAtTopOfScreen);
		foreach (var region in regions)
		{
			AddRegion(region, true, ref regionsNeedingToBeAdded, ref addedRegions, previousRateEventY, nextRateEvent,
				chartRegionX,
				chartRegionW);
		}

		// Check to see if any regions needing to be added will complete before the next rate altering event.
		CheckForCompletingRegions(ref regionsNeedingToBeAdded, ref addedRegions, previousRateEventY, nextRateEvent);

		// Check for updating the SelectedRegion.
		CheckForUpdatingSelectedRegionStartY(SelectedRegion, previousRateEventY, rateEvent, nextRateEvent);
		CheckForUpdatingSelectedRegionCurrentValues(SelectedRegion, previousRateEventY, rateEvent, nextRateEvent);
	}

	/// <summary>
	/// Handles updating any pending regions when the current rate altering event changes
	/// while processing events in the main tick loop.
	/// Pending regions include normal regions needing to be added to VisibleRegions,
	/// the preview region, and the SelectedRegion.
	/// </summary>
	/// <remarks>Helper for UpdateChartEvents.</remarks>
	private void UpdateRegions(
		ref List<IChartRegion> regionsNeedingToBeAdded,
		ref HashSet<IChartRegion> addedRegions,
		double previousRateEventY,
		EditorRateAlteringEvent rateEvent,
		EditorRateAlteringEvent nextRateEvent)
	{
		// Check to see if any regions needing to be added will complete before the next rate altering event.
		CheckForCompletingRegions(ref regionsNeedingToBeAdded, ref addedRegions, previousRateEventY, nextRateEvent);

		// Check for updating the SelectedRegion.
		CheckForUpdatingSelectedRegionStartY(SelectedRegion, previousRateEventY, rateEvent, nextRateEvent);
		CheckForUpdatingSelectedRegionCurrentValues(SelectedRegion, previousRateEventY, rateEvent, nextRateEvent);
	}

	/// <summary>
	/// Handles completing any pending regions this tick.
	/// Pending regions include normal regions needing to be added to VisibleRegions,
	/// and the SelectedRegion.
	/// </summary>
	/// <remarks>Helper for UpdateChartEvents.</remarks>
	private void EndRegions(
		ref List<IChartRegion> regionsNeedingToBeAdded,
		ref HashSet<IChartRegion> addedRegions,
		double previousRateEventY,
		EditorRateAlteringEvent rateEvent)
	{
		// We do not need to scan forward for more rate mods so we can use null for the next rate event.
		CheckForCompletingRegions(ref regionsNeedingToBeAdded, ref addedRegions, previousRateEventY, null);

		// Check for updating the SelectedRegion.
		CheckForUpdatingSelectedRegionStartY(SelectedRegion, previousRateEventY, rateEvent, null);
		CheckForUpdatingSelectedRegionCurrentValues(SelectedRegion, previousRateEventY, rateEvent, null);
	}

	/// <summary>
	/// Handles completing any pending holds when the current rate altering event changes
	/// while processing events in the main tick loop and holds end within the new rate
	/// altering event range.
	/// </summary>
	/// <remarks>Helper for UpdateChartEvents.</remarks>
	private void CheckForCompletingHolds(
		HashSet<EditorHoldNoteEvent> holds,
		double previousRateEventY,
		EditorRateAlteringEvent nextRateEvent)
	{
		if (holds.Count == 0)
			return;

		List<EditorHoldNoteEvent> holdsToRemove = null;
		foreach (var hold in holds)
		{
			if (CheckForCompletingHold(hold, previousRateEventY, nextRateEvent))
			{
				holdsToRemove ??= new List<EditorHoldNoteEvent>();
				holdsToRemove.Add(hold);
			}
		}

		if (holdsToRemove == null)
			return;

		foreach (var hold in holdsToRemove)
			holds.Remove(hold);
	}

	/// <summary>
	/// Handles completing a pending hold when the current rate altering event changes
	/// while processing events in the main tick loop and the hold ends within the new rate
	/// altering event range.
	/// </summary>
	/// <remarks>Helper for UpdateChartEvents.</remarks>
	private bool CheckForCompletingHold(
		EditorHoldNoteEvent hold,
		double previousRateEventY,
		EditorRateAlteringEvent nextRateEvent)
	{
		var holdEndEvent = hold.GetAdditionalEvent();
		if (nextRateEvent == null || EditorEvent.CompareEditorEventToSmEvent(nextRateEvent, holdEndEvent) > 0)
		{
			var holdEndRow = hold.GetRow() + hold.GetRowDuration();
			var holdEndY = SpacingHelper.GetYForRow(holdEndRow, previousRateEventY) + GetHoldCapHeight();
			hold.H = holdEndY - hold.Y;
			return true;
		}

		return false;
	}

	/// <summary>
	/// Helper method to update beat marker events.
	/// Adds new MarkerEvents to VisibleMarkers.
	/// Expected to be called in a loop over EditorRateAlteringEvents which encompass the visible area.
	/// </summary>
	/// <param name="currentRateEvent">
	/// The current EditorRateAlteringEvent.
	/// MarkerEvents will be filled for the region in this event up until the given
	/// nextRateEvent, or end of the visible area defined by the viewport's height.
	/// </param>
	/// <param name="currentRow">
	/// The current row to start with. This row may not be on a beat boundary. If it is not on a beat
	/// boundary then MarkerEvents will be added starting with the following beat.
	/// This parameter is passed by reference so the beat marker logic can maintain state about where
	/// it left off.
	/// </param>
	/// <param name="lastRecordedRow">
	/// The last row that this method recorded a beat for.
	/// This parameter is passed by reference so the beat marker logic can maintain state about where
	/// it left off.
	/// </param>
	/// <param name="nextRateEvent">
	/// The EditorRateAlteringEvent following currentRateEvent or null if no such event follows it.
	/// </param>
	/// <param name="x">X position in pixels to set on the MarkerEvents.</param>
	/// <param name="sizeZoom">Current zoom level to use for setting the width and scale of the MarkerEvents.</param>
	/// <param name="previousRateEventY">Y position of previous rate altering event.</param>
	/// <param name="screenHeight">Screen height in pixels.</param>
	/// <remarks>Helper for UpdateChartEvents.</remarks>
	private void UpdateBeatMarkers(
		EditorRateAlteringEvent currentRateEvent,
		ref int currentRow,
		ref int lastRecordedRow,
		EditorRateAlteringEvent nextRateEvent,
		double x,
		double sizeZoom,
		double previousRateEventY,
		int screenHeight,
		int maxMarkersToDraw)
	{
		if (sizeZoom < MeasureMarkerMinScale)
			return;
		if (VisibleMarkers.Count >= maxMarkersToDraw)
			return;

		var ts = currentRateEvent.GetTimeSignature();

		// Based on the current rate altering event, determine the beat spacing and snap the current row to a beat.
		var beatsPerMeasure = ts.GetNumerator();
		var rowsPerBeat = MaxValidDenominator * NumBeatsPerMeasure * beatsPerMeasure
		                  / ts.GetDenominator() / beatsPerMeasure;

		// Determine which integer measure and beat we are on. Clamped due to warps.
		var rowRelativeToTimeSignatureStart = Math.Max(0, currentRow - ts.GetRow());
		// We need to snap the row forward since we are starting with a row that might not be on a beat boundary.
		var beatRelativeToTimeSignatureStart = rowRelativeToTimeSignatureStart / rowsPerBeat;
		currentRow = ts.GetRow() + beatRelativeToTimeSignatureStart * rowsPerBeat;

		var markerWidth = Chart.NumInputs * MarkerTextureWidth * sizeZoom;

		while (true)
		{
			// When changing time signatures we don't want to render the same row twice,
			// so advance if we have already processed this row.
			// Also check to ensure that the current row is within range for the current rate event.
			// In some edge cases it may not be. For example, when we have finished but the last
			// rate altering event is negative so we consider one more rate altering event.
			if (currentRow == lastRecordedRow || currentRow < currentRateEvent.GetRow())
			{
				currentRow += rowsPerBeat;
				continue;
			}

			var y = SpacingHelper.GetYForRow(currentRow, previousRateEventY);

			// If advancing this beat forward moved us over the next rate altering event boundary, loop again.
			if (nextRateEvent != null && currentRow > nextRateEvent.GetRow())
			{
				currentRow = nextRateEvent.GetRow();
				return;
			}

			// If advancing moved beyond the end of the screen then we are done.
			if (y > screenHeight)
				return;

			// Determine if this marker is a measure marker instead of a beat marker.
			rowRelativeToTimeSignatureStart = currentRow - ts.GetRow();
			beatRelativeToTimeSignatureStart = rowRelativeToTimeSignatureStart / rowsPerBeat;
			var measureMarker = beatRelativeToTimeSignatureStart % beatsPerMeasure == 0;
			var measure = ts.Measure + beatRelativeToTimeSignatureStart / beatsPerMeasure;

			// If this row falls on a measure boundary for a new time signature at an unexpected row, treat it as a measure marker.
			if (nextRateEvent != null && currentRow == nextRateEvent.GetRow() &&
			    currentRow == nextRateEvent.GetTimeSignature().GetRow())
			{
				measureMarker = true;
				measure = nextRateEvent.GetTimeSignature().Measure;
			}

			// Record the marker.
			if (measureMarker || sizeZoom > BeatMarkerMinScale)
				VisibleMarkers.Add(new EditorMarkerEvent(x, y, markerWidth, 1, sizeZoom, measureMarker, measure));

			lastRecordedRow = currentRow;

			if (VisibleMarkers.Count >= maxMarkersToDraw)
				return;

			// Advance one beat.
			currentRow += rowsPerBeat;
		}
	}

	/// <summary>
	/// Given a chart position, scans backwards for hold notes which begin earlier and end later.
	/// </summary>
	/// <param name="enumerator">Enumerator to use for scanning backwards.</param>
	/// <param name="chartPosition">Chart position to use for checking.</param>
	/// <remarks>Helper for UpdateChartEvents.</remarks>
	/// <returns>List of EditorHoldStartNotes.</returns>
	public List<EditorHoldNoteEvent> ScanBackwardsForHolds(
		IReadOnlyRedBlackTree<EditorEvent>.IReadOnlyRedBlackTreeEnumerator enumerator,
		double chartPosition)
	{
		// Get all the holds overlapping the given position.
		var holdsPerLane = Chart.GetHoldsOverlappingPosition(chartPosition, enumerator);
		var holds = new List<EditorHoldNoteEvent>();
		foreach (var hold in holdsPerLane)
		{
			if (hold != null)
				holds.Add(hold);
		}

		// Add holds being edited.
		foreach (var editState in LaneEditStates)
		{
			if (!editState.IsActive())
				continue;
			if (!(editState.GetEventBeingEdited() is EditorHoldNoteEvent hn))
				continue;
			if (hn.GetRow() < chartPosition && hn.GetRow() + hn.GetRowDuration() > chartPosition)
				holds.Add(hn);
		}

		return holds;
	}

	/// <summary>
	/// Given a y position in screen space, return the corresponding chart time and row.
	/// This is O(log(N)) time complexity on the number of rate altering events in the chart
	/// plus an additional linear scan of rate altering events between the focal point and
	/// the given y position.
	/// </summary>
	/// <param name="desiredScreenSpaceY">Y position in screen space.</param>
	/// <returns>Tuple where the first value is the chart time and the second is the row.</returns>
	public (double, double) FindChartTimeAndRowForScreenSpaceY(int desiredScreenSpaceY)
	{
		// Set up a spacing helper with isolated state for searching for the time and row.
		var spacingHelper = EventSpacingHelper.GetSpacingHelper(Chart);

		double desiredY = desiredScreenSpaceY;

		// The only point where we know the screen space y position as well as the chart time and chart position
		// is at the focal point. We will use this as an anchor for scanning for the rate event to use for the
		// desired Y position. As we scan upwards or downwards through rate events we can keep track of the rate
		// event's Y position by calculating it from the previous rate event, and then finally calculate the
		// desired Y position's chart time and chart position from rate event's screen Y position and its rate
		// information.
		var focalPointChartTime = Position.ChartTime;
		var focalPointChartPosition = Position.ChartPosition;
		var focalPointYDouble = (double)FocalPointScreenSpaceY;
		var rateEnumerator = Chart.GetRateAlteringEvents().FindBest(Position);
		if (rateEnumerator == null)
			return (0.0, 0.0);
		rateEnumerator.MoveNext();

		var interpolatedScrollRate = GetCurrentInterpolatedScrollRate();
		var spacingZoom = ZoomManager.GetSpacingZoom();

		// Determine the active rate event's position and rate information.
		spacingHelper.UpdatePpsAndPpr(rateEnumerator.Current, interpolatedScrollRate, spacingZoom);
		var rateEventY = spacingHelper.GetY(rateEnumerator.Current, focalPointYDouble, focalPointChartTime,
			focalPointChartPosition);
		var rateChartTime = rateEnumerator.Current!.GetChartTime();
		var rateRow = rateEnumerator.Current.GetRow();

		// If the desired Y is above the focal point.
		if (desiredY < focalPointYDouble)
		{
			// Scan upwards until we find the rate event that is active for the desired Y.
			while (true)
			{
				// If the current rate event is above the focal point, or there is no preceding rate event,
				// then this is the rate event we should use for determining the chart time and row of the
				// desired position.
				if (rateEventY <= desiredY || !rateEnumerator.MovePrev())
					return spacingHelper.GetChartTimeAndRow(desiredY, rateEventY, rateChartTime, rateRow);

				// Otherwise, now that we have advance the rate enumerator to its preceding event, we can
				// update the the current rate event variables to check again next loop.
				spacingHelper.UpdatePpsAndPpr(rateEnumerator.Current, interpolatedScrollRate, spacingZoom);
				rateEventY = spacingHelper.GetY(rateEnumerator.Current, rateEventY, rateChartTime, rateRow);
				rateChartTime = rateEnumerator.Current.GetChartTime();
				rateRow = rateEnumerator.Current.GetRow();
			}
		}
		// If the desired Y is below the focal point.
		else if (desiredY > focalPointYDouble)
		{
			while (true)
			{
				// If there is no following rate event then the current rate event should be used for
				// determining the chart time and row of the desired position.
				if (!rateEnumerator.MoveNext())
					return spacingHelper.GetChartTimeAndRow(desiredY, rateEventY, rateChartTime, rateRow);

				// Otherwise, we need to determine the position of the next rate event. If it is beyond
				// the desired position then we have gone to far and we need to use the previous rate
				// information to determine the chart time and row of the desired position.
				rateEventY = spacingHelper.GetY(rateEnumerator.Current, rateEventY, rateChartTime, rateRow);
				spacingHelper.UpdatePpsAndPpr(rateEnumerator.Current, interpolatedScrollRate, spacingZoom);
				rateChartTime = rateEnumerator.Current.GetChartTime();
				rateRow = rateEnumerator.Current.GetRow();

				if (rateEventY >= desiredY)
					return spacingHelper.GetChartTimeAndRowFromPreviousRate(desiredY, rateEventY, rateChartTime, rateRow);
			}
		}

		// The desired Y is exactly at the focal point.
		return (focalPointChartTime, focalPointChartPosition);
	}

	#endregion Chart Event Updates

	#region Selection

	public IReadOnlySelection GetSelection()
	{
		return Selection;
	}

	public void SetSelectedEvents(List<EditorEvent> events)
	{
		Selection.SetSelectEvents(events);
	}

	public void OnDelete()
	{
		if (!Selection.HasSelectedEvents())
			return;

		var eventsToDelete = new List<EditorEvent>();
		foreach (var editorEvent in Selection.GetSelectedEvents())
		{
			if (!editorEvent.CanBeDeleted())
				continue;
			eventsToDelete.Add(editorEvent);
		}

		if (eventsToDelete.Count == 0)
			return;
		ActionQueue.Instance.Do(new ActionDeleteEditorEvents(eventsToDelete, false));
	}

	public void OnEventAdded(EditorEvent addedEvent)
	{
		// When adding notes, reset the AutoPlayer so it picks up the new state.
		AutoPlayer?.Stop();
	}

	public void OnEventsAdded(IReadOnlyList<EditorEvent> addedEvents)
	{
		// When adding notes, reset the AutoPlayer so it picks up the new state.
		AutoPlayer?.Stop();
	}

	public void OnSelectPattern(EditorPatternEvent pattern)
	{
		LastSelectedPatternEvent = pattern;
	}

	public void OnEventMoveStart(EditorEvent editorEvent)
	{
		MovingNotes.Add(editorEvent);
	}

	public void OnEventMoveEnd(EditorEvent editorEvent)
	{
		MovingNotes.Remove(editorEvent);
	}

	public void OnEventDeleted(EditorEvent deletedEvent, bool transformingSelectedNotes)
	{
		// When deleting notes, reset the AutoPlayer so it picks up the new state.
		AutoPlayer?.Stop();

		// Don't consider events which are deleted as part of a move.
		if (!MovingNotes.Contains(deletedEvent))
		{
			// If a selected note was deleted, deselect it.
			// When transforming notes we expect selected notes to be moved which requires
			// deleting them, then modifying them, and then re-adding them. We don't want
			// to deselect notes when they are moving.
			if (!transformingSelectedNotes)
				Selection.DeselectEvent(deletedEvent);

			// If an event was deleted that is in a member variable, remove the reference.
			if (ReferenceEquals(deletedEvent, LastSelectedPatternEvent))
				LastSelectedPatternEvent = null;
		}
	}

	public void OnEventsDeleted(IReadOnlyList<EditorEvent> deletedEvents, bool transformingSelectedNotes)
	{
		// When deleting notes, reset the AutoPlayer so it picks up the new state.
		AutoPlayer?.Stop();

		// Don't consider events which are deleted as part of a move.
		if (MovingNotes.Count > 0)
		{
			var deletedEventsToConsider = new List<EditorEvent>();
			foreach (var deletedEvent in deletedEvents)
			{
				if (!MovingNotes.Contains(deletedEvent))
				{
					deletedEventsToConsider.Add(deletedEvent);
				}
			}

			deletedEvents = deletedEventsToConsider;
		}

		foreach (var deletedEvent in deletedEvents)
		{
			// If a selected note was deleted, deselect it.
			// When transforming notes we expect selected notes to be moved which requires
			// deleting them, then modifying them, and then re-adding them. We don't want
			// to deselect notes when they are moving.
			if (!transformingSelectedNotes)
				Selection.DeselectEvent(deletedEvent);

			// If an event was deleted that is in a member variable, remove the reference.
			if (ReferenceEquals(deletedEvent, LastSelectedPatternEvent))
				LastSelectedPatternEvent = null;
		}
	}

	public void OnSelectAll()
	{
		OnSelectAll((e) => e.IsSelectableWithoutModifiers());
	}

	public void OnSelectAllAlt()
	{
		OnSelectAll((e) => e.IsSelectableWithModifiers());
	}

	public void OnSelectAllShift()
	{
		OnSelectAll((e) => e.IsSelectableWithoutModifiers() || e.IsSelectableWithModifiers());
	}

	public void OnSelectAll(Func<EditorEvent, bool> isSelectable)
	{
		EditorEvent lastEvent = null;
		foreach (var editorEvent in Chart.GetEvents())
		{
			if (isSelectable(editorEvent))
			{
				Selection.SelectEvent(editorEvent, false);
				lastEvent = editorEvent;
			}
		}

		if (lastEvent != null)
			Selection.SelectEvent(lastEvent, true);
	}

	public void UpdateSelectedRegion(double currentTime)
	{
		SelectedRegion.UpdateTime(currentTime);
	}

	public bool IsSelectedRegionActive()
	{
		return SelectedRegion.IsActive();
	}

	/// <summary>
	/// Finishes selecting a region with the mouse.
	/// </summary>
	public void FinishSelectedRegion()
	{
		if (Selection == null || !SelectedRegion.IsActive())
			return;

		var canSelectNotes = Preferences.Instance.PreferencesOptions.RenderNotes;
		var canSelectMiscEvents = Preferences.Instance.PreferencesOptions.RenderMiscEvents;
		var lastSelectedEvent = Selection.GetLastSelectedEvent();

		// Collect the newly selected notes.
		var newlySelectedEvents = new List<EditorEvent>();

		var alt = KeyCommandManager.IsAnyInputDown(Preferences.Instance.PreferencesKeyBinds.MouseSelectionAltBehavior);
		Func<EditorEvent, bool> isSelectable = alt
			? (e) => e.IsSelectableWithModifiers()
			: (e) => e.IsSelectableWithoutModifiers();

		var (_, arrowHeightUnscaled) = GetArrowDimensions(false);
		var halfArrowH = arrowHeightUnscaled * ZoomManager.GetSizeZoom() * 0.5;

		// For clicking, we want to select only one note. The latest note whose bounding rect
		// overlaps with the point that was clicked. The events are sorted but we cannot binary
		// search them because we only want to consider events which are in the same lane as
		// the click. A binary search won't consider every event so we may miss an event which
		// overlaps the click. However, the visible events list is limited in size such that it
		// small enough to iterate through when updating and rendering. A click happens rarely,
		// and when it does happen it happens at most once per frame, so iterating when clicking
		// is performant enough.
		var isClick = SelectedRegion.IsClick();
		if (isClick)
		{
			var (x, y) = SelectedRegion.GetCurrentScreenSpacePosition();
			EditorEvent best = null;
			foreach (var visibleEvent in VisibleEvents)
			{
				// Early out if we have searched beyond the selected y. Add an extra half arrow
				// height to this check so that short miscellaneous events do not cause us to
				// early out prematurely.
				if (visibleEvent.Y > y + halfArrowH)
					break;
				if (!canSelectNotes && !visibleEvent.IsMiscEvent())
					continue;
				if (!canSelectMiscEvents && visibleEvent.IsMiscEvent())
					continue;
				if (visibleEvent.DoesPointIntersect(x, y))
					best = visibleEvent;
			}

			if (best != null)
				newlySelectedEvents.Add(best);
		}

		// A region was selected, collect all notes in the selected region.
		else
		{
			var (minLane, maxLane) = GetSelectedLanes();

			var fullyOutsideLanes = maxLane < 0 || minLane >= Chart.NumInputs;
			var partiallyOutsideLanes = !fullyOutsideLanes && (minLane < 0 || maxLane >= Chart.NumInputs);
			var selectMiscEvents = alt || fullyOutsideLanes;

			// Select by time.
			if (Preferences.Instance.PreferencesScroll.SpacingMode == SpacingMode.ConstantTime)
			{
				var (minTime, maxTime) = SelectedRegion.GetSelectedChartTimeRange();

				// Select notes.
				if (!selectMiscEvents)
				{
					if (canSelectNotes)
					{
						// Adjust the time to account for the selection preference for how much of an event should be
						// within the selected region.
						var (adjustedMinTime, adjustedMaxTime) = AdjustSelectionTimeRange(minTime, maxTime, halfArrowH);
						if (adjustedMinTime < adjustedMaxTime)
						{
							var enumerator = Chart.GetEvents().FindLeastAfterChartTime(adjustedMinTime);
							while (enumerator.MoveNext())
							{
								if (enumerator.Current!.GetChartTime() > adjustedMaxTime)
									break;
								if (!isSelectable(enumerator.Current))
									continue;
								var lane = enumerator.Current.GetLane();
								if (lane < minLane || lane > maxLane)
									continue;
								newlySelectedEvents.Add(enumerator.Current);
							}
						}
					}

					// If nothing was selected and the selection was partially outside of the lanes, treat it as
					// an attempt to select misc events.
					if (newlySelectedEvents.Count == 0 && partiallyOutsideLanes)
						selectMiscEvents = true;
				}

				// Select misc events.
				if (selectMiscEvents && canSelectMiscEvents)
				{
					newlySelectedEvents.AddRange(SelectMiscEvents());
				}
			}

			// Select by chart position.
			else
			{
				var (minPosition, maxPosition) = SelectedRegion.GetSelectedChartPositionRange();

				// Select notes.
				if (!selectMiscEvents)
				{
					if (canSelectNotes)
					{
						// Adjust the position to account for the selection preference for how much of an event should be
						// within the selected region.
						var (adjustedMinPosition, adjustedMaxPosition) =
							AdjustSelectionPositionRange(minPosition, maxPosition, halfArrowH);
						if (adjustedMinPosition < adjustedMaxPosition)
						{
							var enumerator = Chart.GetEvents().FindLeastAfterChartPosition(adjustedMinPosition);
							while (enumerator != null && enumerator.MoveNext())
							{
								if (enumerator.Current!.GetRow() > adjustedMaxPosition)
									break;
								if (!isSelectable(enumerator.Current))
									continue;
								var lane = enumerator.Current.GetLane();
								if (lane < minLane || lane > maxLane)
									continue;
								newlySelectedEvents.Add(enumerator.Current);
							}
						}
					}

					// If nothing was selected and the selection was partially outside of the lanes, treat it as
					// an attempt to select misc events.
					if (newlySelectedEvents.Count == 0 && partiallyOutsideLanes)
						selectMiscEvents = true;
				}

				// Select misc events.
				if (selectMiscEvents && canSelectMiscEvents)
				{
					newlySelectedEvents.AddRange(SelectMiscEvents());
				}
			}
		}

		var ctrl = KeyCommandManager.IsAnyInputDown(Preferences.Instance.PreferencesKeyBinds.MouseSelectionControlBehavior);
		var shift = KeyCommandManager.IsAnyInputDown(Preferences.Instance.PreferencesKeyBinds.MouseSelectionShiftBehavior);

		// If holding shift, select everything from the previously selected note
		// to the newly selected notes, in addition to the newly selected notes.
		if (shift)
		{
			if (lastSelectedEvent != null && newlySelectedEvents.Count > 0)
			{
				IReadOnlyRedBlackTree<EditorEvent>.IReadOnlyRedBlackTreeEnumerator enumerator;
				EditorEvent end;
				var firstNewNote = newlySelectedEvents[0];
				if (firstNewNote.CompareTo(lastSelectedEvent) < 0)
				{
					enumerator = Chart.GetEvents().Find(firstNewNote);
					end = lastSelectedEvent;
				}
				else
				{
					enumerator = Chart.GetEvents().Find(lastSelectedEvent);
					end = firstNewNote;
				}

				if (enumerator != null)
				{
					var checkLane = Preferences.Instance.PreferencesSelection.RegionMode ==
					                PreferencesSelection.SelectionRegionMode.TimeOrPositionAndLane;
					var minLane = Math.Min(newlySelectedEvents[0].GetLane(),
						Math.Min(lastSelectedEvent.GetLane(), newlySelectedEvents[^1].GetLane()));
					var maxLane = Math.Max(newlySelectedEvents[0].GetLane(),
						Math.Max(lastSelectedEvent.GetLane(), newlySelectedEvents[^1].GetLane()));

					while (enumerator.MoveNext())
					{
						var last = enumerator.Current == end;
						if (isSelectable(enumerator.Current))
						{
							if (!checkLane ||
							    (enumerator.Current!.GetLane() >= minLane && enumerator.Current.GetLane() <= maxLane))
							{
								Selection.SelectEvent(enumerator.Current, last);
							}
						}

						if (last)
							break;
					}
				}
			}

			// Select the newly selected notes.
			for (var i = 0; i < newlySelectedEvents.Count; i++)
			{
				Selection.SelectEvent(newlySelectedEvents[i], i == newlySelectedEvents.Count - 1);
			}
		}

		// If holding control, toggle the selected notes.
		else if (ctrl)
		{
			for (var i = 0; i < newlySelectedEvents.Count; i++)
			{
				if (newlySelectedEvents[i].IsSelected())
					Selection.DeselectEvent(newlySelectedEvents[i]);
				else
					Selection.SelectEvent(newlySelectedEvents[i], i == newlySelectedEvents.Count - 1);
			}
		}

		// If holding no modifier key, deselect everything and select the newly
		// selected notes.
		else
		{
			Selection.ClearSelectedEvents();
			for (var i = 0; i < newlySelectedEvents.Count; i++)
			{
				Selection.SelectEvent(newlySelectedEvents[i], i == newlySelectedEvents.Count - 1);
			}
		}

		SelectedRegion.Stop();
	}

	/// <summary>
	/// Returns all the miscellaneous EditorEvents which fall within the SelectedRegion.
	/// Helper for FinishSelectedRegion.
	/// </summary>
	/// <returns>List of all miscellaneous EditorEvents which fall within the SelectedRegion.</returns>
	private List<EditorEvent> SelectMiscEvents()
	{
		// Misc events may be offset from their row / time by screen space y values.
		// As a result, searching for events that fall within the selection's time or row range is insufficient.
		// We need to extend the search such that misc events which occur earlier or later but would render
		// within the selection are captured. This requires searching because they are offset by screen space
		// values and we do not know the screen space position of off screen events without scanning up or down from
		// the focal point using rate altering events. The function performs two scans starting at the focal point,
		// one up and one down.

		var newlySelectedEvents = new List<EditorEvent>();
		var miscEventsToConsider = new HashSet<EditorEvent>();

		// Initialize a frame for the MiscEventWidgetLayoutManager. We will use it to position misc events
		// so we can compare their bounds to the selected region's bounds.
		BeginMiscEventWidgetLayoutManagerFrame();

		// Set up a spacing helper with isolated state for searching for the time and row.
		var spacingHelper = EventSpacingHelper.GetSpacingHelper(Chart);

		// The only point where we know the screen space y position as well as the chart time and chart position
		// is at the focal point. We will use this as an anchor. We can find the rate altering event which is active
		// at the focal point, and then perform two scans with it, one up and one down, to find all the misc events
		// which are potentially in bounds for the selected region.
		var activeRateEnumerator = Chart.GetRateAlteringEvents().FindBest(Position);
		if (activeRateEnumerator == null)
			return newlySelectedEvents;
		activeRateEnumerator.MoveNext();

		// Get the selected region time and row bounds.
		var regionStartTime = SelectedRegion.GetStartChartTime();
		var regionEndTime = SelectedRegion.GetCurrentChartTime();
		if (regionStartTime > regionEndTime)
			(regionStartTime, regionEndTime) = (regionEndTime, regionStartTime);
		var regionStartRow = SelectedRegion.GetStartChartPosition();
		var regionEndRow = SelectedRegion.GetCurrentChartPosition();
		if (regionStartRow > regionEndRow)
			(regionStartRow, regionEndRow) = (regionEndRow, regionStartRow);

		// Region y pixel values of the region to determine.
		double? selectedRegionStartYScreenSpacePos = null;
		double? selectedRegionEndYScreenSpacePos = null;

		// Scan.
		ScanUpForSelectedMiscEvents(spacingHelper, activeRateEnumerator, miscEventsToConsider,
			regionStartTime, regionEndTime, regionStartRow, regionEndRow, ref selectedRegionStartYScreenSpacePos,
			ref selectedRegionEndYScreenSpacePos);
		ScanDownForSelectedMiscEvents(spacingHelper, activeRateEnumerator, miscEventsToConsider,
			regionStartTime, regionEndTime, regionStartRow, regionEndRow, ref selectedRegionStartYScreenSpacePos,
			ref selectedRegionEndYScreenSpacePos);

		// Check which potential misc events fall within the selected pixel range.
		if (selectedRegionStartYScreenSpacePos == null || selectedRegionEndYScreenSpacePos == null)
			return newlySelectedEvents;
		var (selectedRegionStartX, selectedRegionEndX) = SelectedRegion.GetSelectedXScreenSpaceRange();
		foreach (var miscEvent in miscEventsToConsider)
		{
			if (DoesMiscEventFallWithinRange(miscEvent, selectedRegionStartX, (double)selectedRegionStartYScreenSpacePos,
				    selectedRegionEndX, (double)selectedRegionEndYScreenSpacePos))
			{
				newlySelectedEvents.Add(miscEvent);
			}
		}

		return newlySelectedEvents;
	}

	/// <summary>
	/// Helper for SelectMiscEvents.
	/// </summary>
	private void ScanUpForSelectedMiscEvents(
		EventSpacingHelper spacingHelper,
		IReadOnlyRedBlackTree<EditorRateAlteringEvent>.IReadOnlyRedBlackTreeEnumerator activeRateEnumerator,
		HashSet<EditorEvent> miscEventsToConsider,
		double regionStartTime,
		double regionEndTime,
		double regionStartRow,
		double regionEndRow,
		ref double? selectedRegionStartYScreenSpacePos,
		ref double? selectedRegionEndYScreenSpacePos)
	{
		var interpolatedScrollRate = GetCurrentInterpolatedScrollRate();
		var spacingZoom = ZoomManager.GetSpacingZoom();
		var miscEvents = Chart.GetMiscEvents();
		var checkRegionByTime = Preferences.Instance.PreferencesScroll.SpacingMode == SpacingMode.ConstantTime;
		var focalPointChartTime = Position.ChartTime;
		var focalPointChartPosition = Position.ChartPosition;
		var focalPointYDouble = (double)FocalPointScreenSpaceY;

		// Scan up.
		var rateEnumerator = activeRateEnumerator.Clone();
		spacingHelper.UpdatePpsAndPpr(rateEnumerator.Current, interpolatedScrollRate, spacingZoom);
		var rateEventY = spacingHelper.GetY(rateEnumerator.Current, focalPointYDouble, focalPointChartTime,
			focalPointChartPosition);
		var rateChartTime = rateEnumerator.Current!.GetChartTime();
		var rateRow = rateEnumerator.Current.GetRow();
		IReadOnlyRedBlackTree<EditorEvent>.IReadOnlyRedBlackTreeEnumerator miscEventEnumerator;
		if (rateEnumerator.Current.IsMiscEvent())
			miscEventEnumerator = miscEvents.Find(rateEnumerator.Current);
		else
			miscEventEnumerator = miscEvents.FindGreatestPreceding(rateEnumerator.Current);
		miscEventEnumerator.MoveNext();
		var scannedBeyondRegionBoundaryEnoughToCaptureMiscEvents = false;
		var regionStartIsInScanRange = checkRegionByTime ? regionStartTime <= rateChartTime : regionStartRow <= rateRow;
		var firstRateEventY = rateEventY;
		while (true)
		{
			// Early out.
			if (scannedBeyondRegionBoundaryEnoughToCaptureMiscEvents)
				break;

			// Check the chunk from the previous rate event.
			if (rateEnumerator.MovePrev())
			{
				spacingHelper.UpdatePpsAndPpr(rateEnumerator.Current, interpolatedScrollRate, spacingZoom);
				var prevRateChartTime = rateEnumerator.Current!.GetChartTime();
				var prevRateRow = rateEnumerator.Current.GetRow();
				var previousRateEventY = spacingHelper.GetY(prevRateChartTime, prevRateRow, rateEventY, rateChartTime, rateRow);

				// Check region start.
				if (selectedRegionStartYScreenSpacePos == null
				    && ((checkRegionByTime && regionStartTime >= prevRateChartTime && regionStartTime <= rateChartTime)
				        || (!checkRegionByTime && regionStartRow >= prevRateRow && regionStartRow <= rateRow)))
				{
					selectedRegionStartYScreenSpacePos = spacingHelper.GetY(regionStartTime, regionStartRow, previousRateEventY,
						prevRateChartTime, prevRateRow);
				}

				// Check region end.
				if (selectedRegionEndYScreenSpacePos == null
				    && regionEndTime >= prevRateChartTime
				    && regionEndTime <= rateChartTime)
				{
					selectedRegionEndYScreenSpacePos = spacingHelper.GetY(regionEndTime, regionEndRow, previousRateEventY,
						prevRateChartTime, prevRateRow);
				}

				// Check misc events.
				while (!scannedBeyondRegionBoundaryEnoughToCaptureMiscEvents && miscEventEnumerator.IsCurrentValid())
				{
					var chartTime = miscEventEnumerator.Current!.GetChartTime();
					var row = miscEventEnumerator.Current.GetChartPosition();

					if ((checkRegionByTime && chartTime >= prevRateChartTime && chartTime <= rateChartTime)
					    || (!checkRegionByTime && row >= prevRateRow && row <= rateRow))
					{
						// Compute the Y of the misc event and add it to our set of misc events to consider.
						var miscEventRowY =
							spacingHelper.GetY(chartTime, row, previousRateEventY, prevRateChartTime, prevRateRow);
						MiscEventWidgetLayoutManager.PositionEvent(miscEventEnumerator.Current, miscEventRowY);
						miscEventsToConsider.Add(miscEventEnumerator.Current);
						miscEventEnumerator.MovePrev();

						CheckForSettingScanBeyondBoundaryFlagForScanningUp(selectedRegionStartYScreenSpacePos,
							ref scannedBeyondRegionBoundaryEnoughToCaptureMiscEvents, miscEventRowY, regionStartIsInScanRange,
							firstRateEventY);
					}
					else
					{
						break;
					}
				}

				rateChartTime = prevRateChartTime;
				rateRow = prevRateRow;
				rateEventY = previousRateEventY;
			}

			// No previous rate event, scan up indefinitely.
			else
			{
				// Check region start.
				if (selectedRegionStartYScreenSpacePos == null && (
					    (checkRegionByTime && regionStartTime <= rateChartTime)
					    || (!checkRegionByTime && regionStartRow <= rateRow)))
				{
					selectedRegionStartYScreenSpacePos =
						spacingHelper.GetY(regionStartTime, regionStartRow, rateEventY, rateChartTime, rateRow);
				}

				// Check region end.
				if (selectedRegionEndYScreenSpacePos == null && (
					    (checkRegionByTime && regionEndTime <= rateChartTime)
					    || (!checkRegionByTime && regionEndRow <= rateRow)))
				{
					selectedRegionEndYScreenSpacePos =
						spacingHelper.GetY(regionEndTime, regionEndRow, rateEventY, rateChartTime, rateRow);
				}

				// Check misc events.
				while (!scannedBeyondRegionBoundaryEnoughToCaptureMiscEvents && miscEventEnumerator.IsCurrentValid())
				{
					var chartTime = miscEventEnumerator.Current!.GetChartTime();
					var row = miscEventEnumerator.Current.GetChartPosition();
					if ((checkRegionByTime && chartTime <= rateChartTime) || (!checkRegionByTime && row <= rateRow))
					{
						var miscEventRowY = spacingHelper.GetY(chartTime, row, rateEventY, rateChartTime, rateRow);
						MiscEventWidgetLayoutManager.PositionEvent(miscEventEnumerator.Current, miscEventRowY);
						miscEventsToConsider.Add(miscEventEnumerator.Current);

						CheckForSettingScanBeyondBoundaryFlagForScanningUp(selectedRegionStartYScreenSpacePos,
							ref scannedBeyondRegionBoundaryEnoughToCaptureMiscEvents, miscEventRowY, regionStartIsInScanRange,
							firstRateEventY);
					}

					miscEventEnumerator.MovePrev();
				}

				// Stop looping.
				break;
			}
		}
	}

	/// <summary>
	/// Helper for SelectMiscEvents.
	/// </summary>
	private void CheckForSettingScanBeyondBoundaryFlagForScanningUp(
		double? selectedRegionStartYScreenSpacePos,
		ref bool scannedBeyondRegionBoundaryEnoughToCaptureMiscEvents,
		double miscEventRowY,
		bool regionStartIsInScanRange,
		double firstRateEventY)
	{
		// Check for scanning so far beyond the region boundary that we can safely stop.
		// If we know the y pos of the region start, check if we are far enough beyond it.
		if (selectedRegionStartYScreenSpacePos != null)
		{
			if (miscEventRowY < selectedRegionStartYScreenSpacePos -
			    MiscEventWidgetLayoutManager.GetMaxYForSingleRow())
			{
				scannedBeyondRegionBoundaryEnoughToCaptureMiscEvents = true;
			}
		}
		// If the region start is not within the scan range then we are already beyond it.
		// Check if we are far enough beyond the first rate event.
		else if (!regionStartIsInScanRange)
		{
			if (miscEventRowY < firstRateEventY - MiscEventWidgetLayoutManager.GetMaxYForSingleRow())
			{
				scannedBeyondRegionBoundaryEnoughToCaptureMiscEvents = true;
			}
		}
	}

	/// <summary>
	/// Helper for SelectMiscEvents.
	/// </summary>
	private void ScanDownForSelectedMiscEvents(
		EventSpacingHelper spacingHelper,
		IReadOnlyRedBlackTree<EditorRateAlteringEvent>.IReadOnlyRedBlackTreeEnumerator activeRateEnumerator,
		HashSet<EditorEvent> miscEventsToConsider,
		double regionStartTime,
		double regionEndTime,
		double regionStartRow,
		double regionEndRow,
		ref double? selectedRegionStartYScreenSpacePos,
		ref double? selectedRegionEndYScreenSpacePos)
	{
		var interpolatedScrollRate = GetCurrentInterpolatedScrollRate();
		var spacingZoom = ZoomManager.GetSpacingZoom();
		var miscEvents = Chart.GetMiscEvents();
		var checkRegionByTime = Preferences.Instance.PreferencesScroll.SpacingMode == SpacingMode.ConstantTime;
		var focalPointChartTime = Position.ChartTime;
		var focalPointChartPosition = Position.ChartPosition;
		var focalPointYDouble = (double)FocalPointScreenSpaceY;

		var rateEnumerator = activeRateEnumerator.Clone();
		spacingHelper.UpdatePpsAndPpr(rateEnumerator.Current, interpolatedScrollRate, spacingZoom);
		var rateEventY = spacingHelper.GetY(rateEnumerator.Current, focalPointYDouble, focalPointChartTime,
			focalPointChartPosition);
		var rateChartTime = rateEnumerator.Current!.GetChartTime();
		var rateRow = rateEnumerator.Current.GetRow();
		IReadOnlyRedBlackTree<EditorEvent>.IReadOnlyRedBlackTreeEnumerator miscEventEnumerator;
		if (rateEnumerator.Current.IsMiscEvent())
			miscEventEnumerator = miscEvents.Find(rateEnumerator.Current);
		else
			miscEventEnumerator = miscEvents.FindLeastFollowing(rateEnumerator.Current);
		miscEventEnumerator.MoveNext();
		var scannedBeyondRegionBoundaryEnoughToCaptureMiscEvents = false;
		var regionEndIsInScanRange = checkRegionByTime ? regionEndTime >= rateChartTime : regionEndRow >= rateRow;
		var firstRateEventY = rateEventY;
		while (true)
		{
			// Early out.
			if (scannedBeyondRegionBoundaryEnoughToCaptureMiscEvents)
				break;

			// Check the chunk from the current rate event to the next.
			if (rateEnumerator.MoveNext())
			{
				var nextRateEventY = spacingHelper.GetY(rateEnumerator.Current, rateEventY);
				var nextRateChartTime = rateEnumerator.Current.GetChartTime();
				var nextRateRow = rateEnumerator.Current.GetRow();

				// Check region start.
				if (selectedRegionStartYScreenSpacePos == null
				    && ((checkRegionByTime && regionStartTime >= rateChartTime && regionStartTime <= nextRateChartTime)
				        || (!checkRegionByTime && regionStartRow >= rateRow && regionStartRow <= nextRateRow)))
				{
					selectedRegionStartYScreenSpacePos = spacingHelper.GetY(regionStartTime, regionStartRow, rateEventY,
						rateChartTime, rateRow);
				}

				// Check region end.
				if (selectedRegionEndYScreenSpacePos == null
				    && regionEndTime >= rateChartTime
				    && regionEndTime <= nextRateChartTime)
				{
					selectedRegionEndYScreenSpacePos = spacingHelper.GetY(regionEndTime, regionEndRow, rateEventY,
						rateChartTime, rateRow);
				}

				// Check misc events.
				while (!scannedBeyondRegionBoundaryEnoughToCaptureMiscEvents && miscEventEnumerator.IsCurrentValid())
				{
					var chartTime = miscEventEnumerator.Current!.GetChartTime();
					var row = miscEventEnumerator.Current.GetChartPosition();

					if ((checkRegionByTime && chartTime >= rateChartTime && chartTime <= nextRateChartTime)
					    || (!checkRegionByTime && row >= rateRow && row <= nextRateRow))
					{
						var miscEventRowY = spacingHelper.GetY(chartTime, row, rateEventY, rateChartTime, rateRow);
						MiscEventWidgetLayoutManager.PositionEvent(miscEventEnumerator.Current, miscEventRowY);
						miscEventsToConsider.Add(miscEventEnumerator.Current);
						miscEventEnumerator.MoveNext();

						CheckForSettingScanBeyondBoundaryFlagForScanningDown(selectedRegionEndYScreenSpacePos,
							ref scannedBeyondRegionBoundaryEnoughToCaptureMiscEvents, miscEventRowY, regionEndIsInScanRange,
							firstRateEventY);
					}
					else
					{
						break;
					}
				}

				spacingHelper.UpdatePpsAndPpr(rateEnumerator.Current, interpolatedScrollRate, spacingZoom);
				rateChartTime = nextRateChartTime;
				rateRow = nextRateRow;
				rateEventY = nextRateEventY;
			}

			// No next rate event, scan down indefinitely.
			else
			{
				// Check region start.
				if (selectedRegionStartYScreenSpacePos == null && (
					    (checkRegionByTime && regionStartTime >= rateChartTime)
					    || (!checkRegionByTime && regionStartRow >= rateRow)))
				{
					selectedRegionStartYScreenSpacePos =
						spacingHelper.GetY(regionStartTime, regionStartRow, rateEventY, rateChartTime, rateRow);
				}

				// Check region end.
				if (selectedRegionEndYScreenSpacePos == null && (
					    (checkRegionByTime && regionEndTime >= rateChartTime)
					    || (!checkRegionByTime && regionEndRow >= rateRow)))
				{
					selectedRegionEndYScreenSpacePos =
						spacingHelper.GetY(regionEndTime, regionEndRow, rateEventY, rateChartTime, rateRow);
				}

				// Check misc events.
				while (!scannedBeyondRegionBoundaryEnoughToCaptureMiscEvents && miscEventEnumerator.IsCurrentValid())
				{
					var chartTime = miscEventEnumerator.Current!.GetChartTime();
					var row = miscEventEnumerator.Current.GetChartPosition();
					if ((checkRegionByTime && chartTime >= rateChartTime) || (!checkRegionByTime && row >= rateRow))
					{
						var miscEventRowY = spacingHelper.GetY(chartTime, row, rateEventY, rateChartTime, rateRow);
						MiscEventWidgetLayoutManager.PositionEvent(miscEventEnumerator.Current, miscEventRowY);
						miscEventsToConsider.Add(miscEventEnumerator.Current);

						CheckForSettingScanBeyondBoundaryFlagForScanningDown(selectedRegionEndYScreenSpacePos,
							ref scannedBeyondRegionBoundaryEnoughToCaptureMiscEvents, miscEventRowY, regionEndIsInScanRange,
							firstRateEventY);
					}

					miscEventEnumerator.MoveNext();
				}

				// Stop looping.
				break;
			}
		}
	}

	/// <summary>
	/// Helper for SelectMiscEvents.
	/// </summary>
	private void CheckForSettingScanBeyondBoundaryFlagForScanningDown(
		double? selectedRegionEndYScreenSpacePos,
		ref bool scannedBeyondRegionBoundaryEnoughToCaptureMiscEvents,
		double miscEventRowY,
		bool regionEndIsInScanRange,
		double firstRateEventY)
	{
		// Check for scanning so far beyond the region boundary that we can safely stop.
		// If we know the y pos of the region end, check if we are far enough beyond it.
		if (selectedRegionEndYScreenSpacePos != null)
		{
			if (miscEventRowY > selectedRegionEndYScreenSpacePos + MiscEventWidgetLayoutManager.GetMaxYForSingleRow())
			{
				scannedBeyondRegionBoundaryEnoughToCaptureMiscEvents = true;
			}
		}
		// If the region end is not within the scan range then we are already beyond it.
		// Check if we are far enough beyond the first rate event.
		else if (!regionEndIsInScanRange)
		{
			if (miscEventRowY > firstRateEventY + MiscEventWidgetLayoutManager.GetMaxYForSingleRow())
			{
				scannedBeyondRegionBoundaryEnoughToCaptureMiscEvents = true;
			}
		}
	}

	/// <summary>
	/// Gets the min and max lanes encompassed by the SelectedRegion based on the current selection preferences.
	/// </summary>
	/// <returns>Min and max lanes from the SelectedRegion.</returns>
	/// <remarks>Helper for FinishSelectedRegion.</remarks>
	private (int, int) GetSelectedLanes()
	{
		var (arrowWidthUnscaled, _) = GetArrowDimensions(false);
		var lanesWidth = Chart.NumInputs * arrowWidthUnscaled;
		var (minChartX, maxChartX) = SelectedRegion.GetSelectedXChartSpaceRange();

		// Determine the min and max lanes to consider for selection based on the preference for how notes should be considered.
		int minLane, maxLane;
		switch (Preferences.Instance.PreferencesSelection.Mode)
		{
			case PreferencesSelection.SelectionMode.OverlapAny:
				minLane = (int)Math.Floor((minChartX + lanesWidth * 0.5) / arrowWidthUnscaled);
				maxLane = (int)Math.Floor((maxChartX + lanesWidth * 0.5) / arrowWidthUnscaled);
				break;
			case PreferencesSelection.SelectionMode.OverlapCenter:
			default:
				minLane = (int)Math.Floor((minChartX + lanesWidth * 0.5 + arrowWidthUnscaled * 0.5) / arrowWidthUnscaled);
				maxLane = (int)Math.Floor((maxChartX + lanesWidth * 0.5 - arrowWidthUnscaled * 0.5) / arrowWidthUnscaled);
				break;
			case PreferencesSelection.SelectionMode.OverlapAll:
				minLane = (int)Math.Ceiling((minChartX + lanesWidth * 0.5) / arrowWidthUnscaled);
				maxLane = (int)Math.Floor((maxChartX + lanesWidth * 0.5) / arrowWidthUnscaled) - 1;
				break;
		}

		return (minLane, maxLane);
	}

	/// <summary>
	/// Given a time range defined by the given min and max time, returns an adjusted min and max time that
	/// are expanded by the given distance value. The given distance value is typically half the height of
	/// an event that should be captured by a selection, like half of an arrow height or half of a misc.
	/// event height.
	/// </summary>
	/// <returns>Adjusted min and max time.</returns>
	/// <remarks>Helper for FinishSelectedRegion.</remarks>
	private (double, double) AdjustSelectionTimeRange(double minTime, double maxTime, double halfHeight)
	{
		switch (Preferences.Instance.PreferencesSelection.Mode)
		{
			case PreferencesSelection.SelectionMode.OverlapAny:
				// This is an approximation as there may be rate altering events during the range.
				return (minTime - GetTimeRangeOfYPixelDurationAtTime(minTime, halfHeight),
					maxTime + GetTimeRangeOfYPixelDurationAtTime(maxTime, halfHeight));
			case PreferencesSelection.SelectionMode.OverlapAll:
				// This is an approximation as there may be rate altering events during the range.
				return (minTime + GetTimeRangeOfYPixelDurationAtTime(minTime, halfHeight),
					maxTime - GetTimeRangeOfYPixelDurationAtTime(maxTime, halfHeight));
			case PreferencesSelection.SelectionMode.OverlapCenter:
			default:
				return (minTime, maxTime);
		}
	}

	/// <summary>
	/// Given a position range defined by the given min and max position, returns an adjusted min and max
	/// position that are expanded by the given distance value. The given distance value is typically half
	/// the height of an event that should be captured by a selection, like half of an arrow height or half
	/// of a misc. event height.
	/// </summary>
	/// <returns>Adjusted min and max position.</returns>
	/// <remarks>Helper for FinishSelectedRegion.</remarks>
	private (double, double) AdjustSelectionPositionRange(double minPosition, double maxPosition, double halfHeight)
	{
		switch (Preferences.Instance.PreferencesSelection.Mode)
		{
			case PreferencesSelection.SelectionMode.OverlapAny:
				// This is an approximation as there may be rate altering events during the range.
				return (minPosition - GetPositionRangeOfYPixelDurationAtPosition(minPosition, halfHeight),
					maxPosition + GetPositionRangeOfYPixelDurationAtPosition(maxPosition, halfHeight));
			case PreferencesSelection.SelectionMode.OverlapAll:
				// This is an approximation as there may be rate altering events during the range.
				return (minPosition + GetPositionRangeOfYPixelDurationAtPosition(minPosition, halfHeight),
					maxPosition - GetPositionRangeOfYPixelDurationAtPosition(maxPosition, halfHeight));
			case PreferencesSelection.SelectionMode.OverlapCenter:
			default:
				return (minPosition, maxPosition);
		}
	}

	/// <summary>
	/// Returns whether the given EditorEvent is eligible to be selected based on its x values by checking
	/// if the range defined by it falls within the given start and end x values, taking into account the
	/// current selection preferences.
	/// </summary>
	/// <returns>Whether the given EditorEvent falls within the given range.</returns>
	/// <remarks>Helper for FinishSelectedRegion.</remarks>
	private bool DoesMiscEventFallWithinRange(EditorEvent editorEvent, double xStart, double yStart, double xEnd, double yEnd)
	{
		switch (Preferences.Instance.PreferencesSelection.Mode)
		{
			case PreferencesSelection.SelectionMode.OverlapAny:
				return editorEvent.X <= xEnd && editorEvent.X + editorEvent.W >= xStart && editorEvent.Y <= yEnd &&
				       editorEvent.Y + editorEvent.H >= yStart;
			case PreferencesSelection.SelectionMode.OverlapAll:
				return editorEvent.X >= xStart && editorEvent.X + editorEvent.W <= xEnd && editorEvent.Y >= yStart &&
				       editorEvent.Y + editorEvent.H <= yEnd;
			case PreferencesSelection.SelectionMode.OverlapCenter:
			default:
				return editorEvent.X + editorEvent.W * 0.5 >= xStart && editorEvent.X + editorEvent.W * 0.5 <= xEnd
				                                                     && editorEvent.Y + editorEvent.H * 0.5 >= yStart &&
				                                                     editorEvent.Y + editorEvent.H * 0.5 <= yEnd;
		}
	}

	/// <summary>
	/// Given a duration in pixel space in y, returns that duration as time based on the
	/// rate altering event present at the given time. Note that this duration is an
	/// approximation as the given pixel range may cover multiple rate altering events
	/// and only the rate altering event present at the given time is considered.
	/// </summary>
	/// <param name="time">Time to use for determining the current rate.</param>
	/// <param name="duration">Y duration in pixels,</param>
	/// <returns>Duration in time.</returns>
	/// <remarks>
	/// Helper for FinishSelectedRegion. Used to approximate the time of arrow tops and bottoms
	/// from their centers.
	/// </remarks>
	private double GetTimeRangeOfYPixelDurationAtTime(double time, double duration)
	{
		var rae = Chart.GetRateAlteringEvents().FindActiveRateAlteringEventForTime(time);
		var spacingHelper = EventSpacingHelper.GetSpacingHelper(Chart);
		spacingHelper.UpdatePpsAndPpr(rae, GetCurrentInterpolatedScrollRate(), ZoomManager.GetSpacingZoom());

		return duration / spacingHelper.GetPps();
	}

	/// <summary>
	/// Given a duration in pixel space in y, returns that duration as rows based on the
	/// rate altering event present at the given time. Note that this duration is an
	/// approximation as the given pixel range may cover multiple rate altering events
	/// and only the rate altering event present at the given position is considered.
	/// </summary>
	/// <param name="position">Chart position to use for determining the current rate.</param>
	/// <param name="duration">Y duration in pixels,</param>
	/// <returns>Duration in rows.</returns>
	/// <remarks>
	/// Helper for FinishSelectedRegion. Used to approximate the row of arrow tops and bottoms
	/// from their centers.
	/// </remarks>
	private double GetPositionRangeOfYPixelDurationAtPosition(double position, double duration)
	{
		var rae = Chart.GetRateAlteringEvents().FindActiveRateAlteringEventForPosition(position);
		var spacingHelper = EventSpacingHelper.GetSpacingHelper(Chart);
		spacingHelper.UpdatePpsAndPpr(rae, GetCurrentInterpolatedScrollRate(), ZoomManager.GetSpacingZoom());

		return duration / spacingHelper.GetPpr();
	}

	public void OnShiftSelectedNotesLeft()
	{
		if (!Selection.HasSelectedEvents())
			return;
		OnShiftNotesLeft(Selection.GetSelectedEvents());
	}

	public void OnShiftNotesLeft(IEnumerable<EditorEvent> events)
	{
		ActionQueue.Instance.Do(new ActionShiftSelectionLane(Editor, Chart, events, false, false));
	}

	public void OnShiftSelectedNotesLeftAndWrap()
	{
		if (!Selection.HasSelectedEvents())
			return;
		OnShiftNotesLeftAndWrap(Selection.GetSelectedEvents());
	}

	public void OnShiftNotesLeftAndWrap(IEnumerable<EditorEvent> events)
	{
		ActionQueue.Instance.Do(new ActionShiftSelectionLane(Editor, Chart, events, false, true));
	}

	public void OnShiftSelectedNotesRight()
	{
		if (!Selection.HasSelectedEvents())
			return;
		OnShiftNotesRight(Selection.GetSelectedEvents());
	}

	public void OnShiftNotesRight(IEnumerable<EditorEvent> events)
	{
		ActionQueue.Instance.Do(new ActionShiftSelectionLane(Editor, Chart, events, true, false));
	}

	public void OnShiftSelectedNotesRightAndWrap()
	{
		OnShiftNotesRightAndWrap(Selection.GetSelectedEvents());
	}

	public void OnShiftNotesRightAndWrap(IEnumerable<EditorEvent> events)
	{
		ActionQueue.Instance.Do(new ActionShiftSelectionLane(Editor, Chart, events, true, true));
	}

	public void OnShiftSelectedNotesEarlier(int rows)
	{
		if (!Selection.HasSelectedEvents())
			return;
		OnShiftNotesEarlier(Selection.GetSelectedEvents(), rows);
	}

	public void OnShiftNotesEarlier(IEnumerable<EditorEvent> events, int rows)
	{
		ActionQueue.Instance.Do(new ActionShiftSelectionRow(Editor, Chart, events, -rows));
	}

	public void OnShiftSelectedNotesLater(int rows)
	{
		if (!Selection.HasSelectedEvents())
			return;
		OnShiftNotesLater(Selection.GetSelectedEvents(), rows);
	}

	public void OnShiftNotesLater(IEnumerable<EditorEvent> events, int rows)
	{
		ActionQueue.Instance.Do(new ActionShiftSelectionRow(Editor, Chart, events, rows));
	}

	public void ClearSelection()
	{
		Selection.ClearSelectedEvents();
	}

	public EditorPatternEvent GetLastSelectedPatternEvent()
	{
		return LastSelectedPatternEvent;
	}

	public void SelectPattern(EditorPatternEvent pattern)
	{
		LastSelectedPatternEvent = pattern;
	}

	#endregion Selection

	#region Input Processing

	/// <summary>
	/// Processes input for selecting regions with the mouse.
	/// </summary>
	/// <remarks>Helper for ProcessInput.</remarks>
	public void ProcessInputForSelectedRegion(
		double currentTime,
		bool uiInterferingWithRegionClicking,
		IReadOnlyEditorMouseState mouseState,
		EditorButtonState buttonState)
	{
		var sizeZoom = ZoomManager.GetSizeZoom();

		// Receptors can interfere with clicking on notes. If there was a click, let the SelectedRegion process it.
		var forceStartRegionFromClick =
			buttonState.ClickedThisFrame() && !SelectedRegion.IsActive() && !uiInterferingWithRegionClicking;

		// Starting a selection.
		if (buttonState.DownThisFrame() || forceStartRegionFromClick)
		{
			var screenSpaceY = mouseState.Y();
			var (chartTime, chartPosition) = FindChartTimeAndRowForScreenSpaceY(screenSpaceY);
			var xInChartSpace = (mouseState.X() - FocalPointScreenSpaceX) / sizeZoom;
			SelectedRegion.Start(
				xInChartSpace,
				screenSpaceY,
				chartTime,
				chartPosition,
				sizeZoom,
				FocalPointScreenSpaceX,
				currentTime);
		}

		// Dragging a selection.
		if ((buttonState.Down() && SelectedRegion.IsActive()) || forceStartRegionFromClick)
		{
			var xInChartSpace = (mouseState.X() - FocalPointScreenSpaceX) / sizeZoom;
			SelectedRegion.UpdatePerFrameValues(xInChartSpace, mouseState.Y(), sizeZoom, FocalPointScreenSpaceX);
		}

		// Releasing a selection.
		if ((buttonState.Up() && SelectedRegion.IsActive()) || forceStartRegionFromClick)
		{
			FinishSelectedRegion();
		}
	}

	#endregion Input Processing

	#region Lane Input

	public void OnArrowModificationKeyDown()
	{
		if (LaneEditStates == null)
			return;

		foreach (var laneEditState in LaneEditStates)
		{
			if (laneEditState.IsActive() && laneEditState.GetEventBeingEdited() != null)
			{
				laneEditState.ArrowModificationKeyPressed(false);
			}
		}
	}

	public void OnArrowModificationKeyUp()
	{
		if (LaneEditStates == null)
			return;

		foreach (var laneEditState in LaneEditStates)
		{
			if (laneEditState.IsActive() && laneEditState.GetEventBeingEdited() != null)
			{
				laneEditState.ArrowModificationKeyPressed(true);
			}
		}
	}

	public void OnLaneInputDown(int lane, bool playing, int snapRows)
	{
		if (lane < 0 || lane >= Chart.NumInputs)
			return;

		Receptors?[lane].OnInputDown();

		if (Position.ChartPosition < 0)
			return;

		// TODO: If playing we should take sync into account and adjust the position.

		var p = Preferences.Instance;
		var row = Position.GetNearestRow();
		if (snapRows != 0)
		{
			var snappedRow = row / snapRows * snapRows;
			if (row - snappedRow >= snapRows * 0.5)
				snappedRow += snapRows;
			row = snappedRow;
		}

		// If there is a tap, mine, or hold start at this location, delete it now.
		// Deleting immediately feels more responsive than deleting on the input up event.
		var deletedNote = false;
		var existingEvent = Chart.GetEvents().FindNoteAt(row, lane, true);
		if (existingEvent != null && existingEvent.GetRow() == row)
		{
			deletedNote = true;
			LaneEditStates[lane].StartEditingWithDelete(row, new ActionDeleteEditorEvents(existingEvent));
		}

		SetLaneInputDownNote(lane, row);

		// If we are playing, immediately commit the note so it comes out as a tap and not a short hold.
		if (playing)
		{
			OnLaneInputUp(lane);
		}
		else
		{
			// If the NoteEntryMode is set to advance by the snap value and the snap is set, then advance.
			// We do not want to do this while playing.
			if (p.NoteEntryMode == NoteEntryMode.AdvanceBySnap && p.SnapIndex != 0 && !deletedNote)
			{
				OnLaneInputUp(lane);
				Editor.OnMoveDown();
			}
		}
	}

	private void SetLaneInputDownNote(int lane, int row)
	{
		// If the existing state is only a delete, revert back to that delete operation.
		if (LaneEditStates[lane].IsOnlyDelete())
		{
			LaneEditStates[lane].Clear(false);
		}

		// Otherwise, set the state to be editing a tap or a mine.
		else
		{
			if (KeyCommandManager.IsAnyInputDown(Preferences.Instance.PreferencesKeyBinds.ArrowModification))
			{
				var config = EventConfig.CreateMineConfig(Chart, row, lane);
				config.IsBeingEdited = true;
				LaneEditStates[lane].SetEditingTapOrMine(EditorEvent.CreateEvent(config));
			}
			else
			{
				var config = EventConfig.CreateTapConfig(Chart, row, lane);
				config.IsBeingEdited = true;
				LaneEditStates[lane].SetEditingTapOrMine(EditorEvent.CreateEvent(config));
			}
		}
	}

	public void OnLaneInputUp(int lane)
	{
		if (Chart == null)
			return;
		if (lane < 0 || lane >= Chart.NumInputs)
			return;

		Receptors?[lane].OnInputUp();

		if (!LaneEditStates[lane].IsActive())
			return;

		// If this action is only a delete, just commit the existing delete action.
		if (LaneEditStates[lane].IsOnlyDelete())
		{
			LaneEditStates[lane].Commit();
			return;
		}

		var row = LaneEditStates[lane].GetEventBeingEdited().GetRow();
		var existingEvent = Chart.GetEvents().FindNoteAt(row, lane, true);

		var newNoteIsMine = LaneEditStates[lane].GetEventBeingEdited() is EditorMineNoteEvent;
		var newNoteIsTap = LaneEditStates[lane].GetEventBeingEdited() is EditorTapNoteEvent;

		// Handle a new tap note overlapping an existing note.
		if (newNoteIsMine || newNoteIsTap)
		{
			if (existingEvent != null)
			{
				var existingIsTap = existingEvent is EditorTapNoteEvent;
				var existingIsMine = existingEvent is EditorMineNoteEvent;

				// Tap note over existing tap note.
				if (existingIsTap || existingIsMine)
				{
					// If the existing note is a tap and this note is a mine, then replace it with the mine.
					if (!existingIsMine && newNoteIsMine)
					{
						LaneEditStates[lane].SetEditingTapOrMine(LaneEditStates[lane].GetEventBeingEdited(),
							new List<EditorAction>
							{
								new ActionDeleteEditorEvents(existingEvent),
							});
					}

					// In all other cases, just delete the existing note and don't add anything else.
					else
					{
						LaneEditStates[lane].Clear(true);
						ActionQueue.Instance.Do(new ActionDeleteEditorEvents(existingEvent));
						return;
					}
				}

				// Tap note over hold note.
				else if (existingEvent is EditorHoldNoteEvent hn)
				{
					// If the tap note starts at the beginning of the hold, delete the hold.
					if (row == existingEvent.GetRow())
					{
						LaneEditStates[lane].SetEditingTapOrMine(LaneEditStates[lane].GetEventBeingEdited(),
							new List<EditorAction>
							{
								new ActionDeleteEditorEvents(existingEvent),
							});
					}

					// If the tap note is in the in the middle of the hold, shorten the hold.
					else
					{
						// Move the hold up by a 16th.
						var newHoldEndRow = row - MaxValidDenominator / 4;

						// If the hold would have a non-positive length, delete it and replace it with a tap.
						if (newHoldEndRow <= existingEvent.GetRow())
						{
							var deleteHold = new ActionDeleteEditorEvents(existingEvent);

							var config = EventConfig.CreateTapConfig(existingEvent);
							var insertNewNoteAtHoldStart = new ActionAddEditorEvent(EditorEvent.CreateEvent(config));

							LaneEditStates[lane].SetEditingTapOrMine(LaneEditStates[lane].GetEventBeingEdited(),
								new List<EditorAction>
								{
									deleteHold,
									insertNewNoteAtHoldStart,
								});
						}

						// Otherwise, the new length is valid. Update it.
						else
						{
							var changeLength = new ActionChangeHoldLength(hn, newHoldEndRow - hn.GetRow());
							LaneEditStates[lane].SetEditingTapOrMine(LaneEditStates[lane].GetEventBeingEdited(),
								new List<EditorAction>
								{
									changeLength,
								});
						}
					}
				}
			}
		}

		// Handle a new hold note overlapping any existing notes
		else if (LaneEditStates[lane].GetEventBeingEdited() is EditorHoldNoteEvent editHold)
		{
			var length = editHold.GetRowDuration();
			var roll = editHold.IsRoll();

			// If the hold is completely within another hold, do not add or delete notes, but make sure the outer
			// hold is the same type (hold/roll) as the new type.
			if (existingEvent is EditorHoldNoteEvent holdFull
			    && holdFull.GetRow() <= row
			    && holdFull.GetRow() + holdFull.GetRowDuration() >= row + length)
			{
				LaneEditStates[lane].Clear(true);
				if (holdFull.IsRoll() != roll)
					ActionQueue.Instance.Do(new ActionChangeHoldType(holdFull, roll));
				return;
			}

			var deleteActions = new List<EditorAction>();

			// If existing holds overlap with only the start or end of the new hold, delete them and extend the
			// new hold to cover their range. We just need to extend the new event now. The deletion of the
			// old event will will be handled below when we check for events fully contained within the new
			// hold region.
			if (existingEvent is EditorHoldNoteEvent hsnStart
			    && hsnStart.GetRow() < row
			    && hsnStart.GetEndRow() >= row
			    && hsnStart.GetEndRow() < row + length)
			{
				row = hsnStart.GetRow();
				length = editHold.GetEndRow() - hsnStart.GetRow();
			}

			existingEvent = Chart.GetEvents().FindNoteAt(row + length, lane, true);
			if (existingEvent is EditorHoldNoteEvent hsnEnd
			    && hsnEnd.GetRow() <= row + length
			    && hsnEnd.GetEndRow() >= row + length
			    && hsnEnd.GetRow() > row)
			{
				length = hsnEnd.GetEndRow() - row;
			}

			// For any event in the same lane within the region of the new hold, delete them.
			var e = Chart.GetEvents().FindBestByPosition(row);
			if (e != null)
			{
				while (e.MoveNext() && e.Current!.GetRow() <= row + length)
				{
					if (e.Current.GetRow() < row)
						continue;
					if (e.Current.GetLane() != lane)
						continue;
					if (e.Current.IsBeingEdited())
						continue;
					if (e.Current is EditorTapNoteEvent or EditorMineNoteEvent or EditorFakeNoteEvent or EditorLiftNoteEvent)
						deleteActions.Add(new ActionDeleteEditorEvents(e.Current));
					else if (e.Current is EditorHoldNoteEvent innerHold && innerHold.GetEndRow() <= row + length)
						deleteActions.Add(new ActionDeleteEditorEvents(innerHold));
				}
			}

			// Set the state to be editing a new hold after running the delete actions.
			LaneEditStates[lane].SetEditingHold(Chart, lane, row, LaneEditStates[lane].GetStartingRow(), length, roll,
				deleteActions);
		}

		LaneEditStates[lane].Commit();
	}

	public void UpdateLaneEditStatesFromPosition()
	{
		if (LaneEditStates == null)
			return;

		var row = Math.Max(0, Position.GetNearestRow());
		for (var lane = 0; lane < LaneEditStates.Length; lane++)
		{
			var laneEditState = LaneEditStates[lane];
			if (!laneEditState.IsActive())
				continue;

			// If moving back to the starting position.
			// In other words, the current state of the note being edited should be a tap.
			if (laneEditState.GetStartingRow() == row)
			{
				// If the event is a hold, convert it to a tap.
				// This will also convert holds to tap even if the starting action was deleting an existing note.
				if (laneEditState.GetEventBeingEdited() is EditorHoldNoteEvent)
				{
					SetLaneInputDownNote(lane, row);
				}
			}

			// If the current position is different than the starting position.
			// In other words, the current state of the note being edited should be a hold.
			else
			{
				var holdStartRow = laneEditState.GetStartingRow() < row ? laneEditState.GetStartingRow() : row;
				var holdEndRow = laneEditState.GetStartingRow() > row ? laneEditState.GetStartingRow() : row;

				// If the event is a tap, mine, deletion, or it is a hold with different bounds, convert it to a new hold.
				if (laneEditState.GetEventBeingEdited() is null
				    || laneEditState.GetEventBeingEdited() is EditorTapNoteEvent
				    || laneEditState.GetEventBeingEdited() is EditorMineNoteEvent
				    || (laneEditState.GetEventBeingEdited() is EditorHoldNoteEvent h
				        && (holdStartRow != h.GetRow() || holdEndRow != h.GetEndRow())))
				{
					var roll = KeyCommandManager.IsAnyInputDown(Preferences.Instance.PreferencesKeyBinds.ArrowModification);
					LaneEditStates[lane].SetEditingHold(Chart, lane, holdStartRow, laneEditState.GetStartingRow(),
						holdEndRow - holdStartRow, roll);
				}
			}
		}
	}

	public bool CancelLaneInput()
	{
		var anyCancelled = false;
		foreach (var laneEditState in LaneEditStates)
		{
			if (laneEditState.IsActive())
			{
				laneEditState.Clear(true);
				anyCancelled = true;
			}
		}

		return anyCancelled;
	}

	#endregion Lane Input

	#region Receptors

	public void UpdateReceptors(bool playing)
	{
		foreach (var receptor in Receptors)
		{
			receptor.Update(playing, Position.ChartPosition);
		}
	}

	public void DrawReceptorForegroundEffects(double sizeZoom, TextureAtlas textureAtlas,
		SpriteBatch spriteBatch)
	{
		foreach (var receptor in Receptors)
			receptor.DrawForegroundEffects(GetFocalPoint(), sizeZoom, textureAtlas, spriteBatch);
	}

	public void DrawReceptors(double sizeZoom, TextureAtlas textureAtlas, SpriteBatch spriteBatch)
	{
		foreach (var receptor in Receptors)
			receptor.Draw(GetFocalPoint(), sizeZoom, textureAtlas, spriteBatch);
	}

	#endregion Receptors

	#region AutoPlayer

	public void UpdateAutoPlayer()
	{
		AutoPlayer.Update(Position);
	}

	public void StopAutoPlayer()
	{
		AutoPlayer.Stop();
	}

	#endregion AutoPlayer

	#region UI

	public void DrawHeader()
	{
		Header.Draw();
	}

	public bool IsOverHeaderDraggableArea(int screenSpaceX, int screenSpaceY)
	{
		return Header.IsOverDraggableArea(screenSpaceX, screenSpaceY);
	}

	#endregion UI
}

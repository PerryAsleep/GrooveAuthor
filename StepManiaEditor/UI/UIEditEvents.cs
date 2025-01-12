using System;
using System.Collections.Generic;
using Fumen.ChartDefinition;
using Fumen.Converters;
using ImGuiNET;
using Microsoft.Xna.Framework.Input;
using static StepManiaEditor.ImGuiUtils;
using static StepManiaEditor.Utils;

namespace StepManiaEditor;

/// <summary>
/// Class for drawing UI menus for editing events.
/// </summary>
internal sealed class UIEditEvents
{
	private readonly Editor Editor;

	public UIEditEvents(Editor editor)
	{
		Editor = editor;
	}

	public void DrawSelectionMenu()
	{
		if (ImGui.BeginMenu("Selection"))
		{
			var selectedEvents = Editor.GetSelection().GetSelectedEvents();
			DrawShiftEventsMenuItems(selectedEvents);
			DrawConvertSelectedMenu(selectedEvents);
			ImGui.EndMenu();
		}
	}

	private void DrawShiftEventsMenuItems(IEnumerable<EditorEvent> events = null)
	{
		var p = Preferences.Instance.PreferencesKeyBinds;
		var chart = Editor.GetFocusedChart();
		var allEvents = false;
		if (events == null)
		{
			events = chart?.GetEvents();
			allEvents = true;
		}

		var disabled = !(chart?.CanBeEdited() ?? false) || events == null;
		if (disabled)
			PushDisabled();

		var rows = Editor.GetShiftNotesRows();
		var shiftAmount = $"1/{SMCommon.MaxValidDenominator / rows * SMCommon.NumBeatsPerMeasure}";

		if (ImGui.MenuItem("Mirror", allEvents ? null : UIControls.GetCommandString(p.Mirror)))
		{
			Editor.OnMirrorSelection();
		}

		if (ImGui.MenuItem("Flip", allEvents ? null : UIControls.GetCommandString(p.Flip)))
		{
			Editor.OnFlipSelection();
		}

		if (ImGui.MenuItem("Mirror and Flip", allEvents ? null : UIControls.GetCommandString(p.MirrorAndFlip)))
		{
			Editor.OnMirrorAndFlipSelection();
		}

		if (ImGui.MenuItem("Shift Right", allEvents ? null : UIControls.GetCommandString(p.ShiftRight)))
		{
			Editor.OnShiftNotesRight(events);
		}

		if (ImGui.MenuItem("Shift Right and Wrap", allEvents ? null : UIControls.GetCommandString(p.ShiftRightAndWrap)))
		{
			Editor.OnShiftNotesRightAndWrap(events);
		}

		if (ImGui.MenuItem("Shift Left", allEvents ? null : UIControls.GetCommandString(p.ShiftLeft)))
		{
			Editor.OnShiftNotesLeft(events);
		}

		if (ImGui.MenuItem("Shift Left and Wrap", allEvents ? null : UIControls.GetCommandString(p.ShiftLeftAndWrap)))
		{
			Editor.OnShiftNotesLeftAndWrap(events);
		}

		if (ImGui.MenuItem($"Shift Earlier ({shiftAmount})", allEvents ? null : UIControls.GetCommandString(p.ShiftEarlier)))
		{
			Editor.OnShiftNotesEarlier(events);
		}

		if (ImGui.MenuItem($"Shift Later ({shiftAmount})", allEvents ? null : UIControls.GetCommandString(p.ShiftLater)))
		{
			Editor.OnShiftNotesLater(events);
		}

		if (disabled)
			PopDisabled();
	}

	public void DrawShiftSelectedMenu(IEnumerable<EditorEvent> events)
	{
		if (ImGui.BeginMenu("Shift Selected"))
		{
			DrawShiftEventsMenuItems(events);
			ImGui.EndMenu();
		}
	}

	public void DrawShiftAllMenu()
	{
		if (ImGui.BeginMenu("Shift All"))
		{
			DrawShiftEventsMenuItems();
			ImGui.EndMenu();
		}
	}

	#region Convert Notes

	public void DrawConvertSelectedMenu(IEnumerable<EditorEvent> events)
	{
		if (ImGui.BeginMenu("Convert Selected"))
		{
			DrawConvertMenuItems(Editor.GetFocusedChart(), events);
			ImGui.EndMenu();
		}
	}

	public void DrawConvertAllMenu()
	{
		if (ImGui.BeginMenu("Convert All"))
		{
			DrawConvertMenuItems(Editor.GetFocusedChart());
			ImGui.EndMenu();
		}
	}

	private bool TryGetFocusedChartSelection(out IEnumerable<EditorEvent> events)
	{
		events = null;
		var focusedChartData = Editor.GetFocusedChartData();
		if (focusedChartData == null)
			return false;
		if (!focusedChartData.GetChart().CanBeEdited())
			return false;
		events = focusedChartData.GetSelection().GetSelectedEvents();
		if (events == null)
			return false;
		return true;
	}

	private void DrawConvertMenuItems(EditorChart chart, IEnumerable<EditorEvent> events = null)
	{
		var allEvents = false;
		if (events == null)
		{
			events = chart?.GetEvents();
			allEvents = true;
		}

		var p = Preferences.Instance.PreferencesKeyBinds;

		var disabled = !(chart?.CanBeEdited() ?? false) || events == null;
		if (disabled)
			PushDisabled();

		if (ImGui.MenuItem("Taps to Mines", allEvents ? null : UIControls.GetCommandString(p.ConvertSelectedTapsToMines)))
		{
			ConvertTapsToMines(chart, events);
		}

		if (ImGui.MenuItem("Taps to Fakes", allEvents ? null : UIControls.GetCommandString(p.ConvertSelectedTapsToFakes)))
		{
			ConvertTapsToFakes(chart, events);
		}

		if (ImGui.MenuItem("Taps to Lifts", allEvents ? null : UIControls.GetCommandString(p.ConvertSelectedTapsToLifts)))
		{
			ConvertTapsToLifts(chart, events);
		}

		ImGui.Separator();
		if (ImGui.MenuItem("Mines to Taps", allEvents ? null : UIControls.GetCommandString(p.ConvertSelectedMinesToTaps)))
		{
			ConvertMinesToTaps(chart, events);
		}

		if (ImGui.MenuItem("Mines to Fakes", allEvents ? null : UIControls.GetCommandString(p.ConvertSelectedMinesToFakes)))
		{
			ConvertMinesToFakes(chart, events);
		}

		if (ImGui.MenuItem("Mines to Lifts", allEvents ? null : UIControls.GetCommandString(p.ConvertSelectedMinesToLifts)))
		{
			ConvertMinesToLifts(chart, events);
		}

		ImGui.Separator();
		if (ImGui.MenuItem("Fakes to Taps", allEvents ? null : UIControls.GetCommandString(p.ConvertSelectedFakesToTaps)))
		{
			ConvertFakesToTaps(chart, events);
		}

		if (ImGui.MenuItem("Lifts to Taps", allEvents ? null : UIControls.GetCommandString(p.ConvertSelectedLiftsToTaps)))
		{
			ConvertLiftsToTaps(chart, events);
		}

		ImGui.Separator();
		if (ImGui.MenuItem("Holds to Rolls", allEvents ? null : UIControls.GetCommandString(p.ConvertSelectedHoldsToRolls)))
		{
			ConvertHoldsToRolls(chart, events);
		}

		if (ImGui.MenuItem("Holds to Taps", allEvents ? null : UIControls.GetCommandString(p.ConvertSelectedHoldsToTaps)))
		{
			ConvertHoldsToTaps(chart, events);
		}

		if (ImGui.MenuItem("Holds to Mines", allEvents ? null : UIControls.GetCommandString(p.ConvertSelectedHoldsToMines)))
		{
			ConvertHoldsToMines(chart, events);
		}

		ImGui.Separator();
		if (ImGui.MenuItem("Rolls to Holds", allEvents ? null : UIControls.GetCommandString(p.ConvertSelectedRollsToHolds)))
		{
			ConvertRollsToHolds(chart, events);
		}

		if (ImGui.MenuItem("Rolls to Taps", allEvents ? null : UIControls.GetCommandString(p.ConvertSelectedRollsToTaps)))
		{
			ConvertRollsToTaps(chart, events);
		}

		if (ImGui.MenuItem("Rolls to Mines", allEvents ? null : UIControls.GetCommandString(p.ConvertSelectedRollsToMines)))
		{
			ConvertRollsToMines(chart, events);
		}

		ImGui.Separator();
		if (ImGui.MenuItem("Warps to Negative Stops",
			    allEvents ? null : UIControls.GetCommandString(p.ConvertSelectedWarpsToNegativeStops)))
		{
			ConvertWarpsToNegativeStops(chart, events, allEvents);
		}

		if (ImGui.MenuItem("Negative Stops to Warps",
			    allEvents ? null : UIControls.GetCommandString(p.ConvertSelectedNegativeStopsToWarps)))
		{
			ConvertNegativeStopsToWarps(chart, events, allEvents);
		}

		if (disabled)
			PopDisabled();
	}

	#endregion Convert Notes

	#region Private Convert Selection Functions

	private void ConvertTapsToMines(EditorChart chart, IEnumerable<EditorEvent> events)
	{
		ActionQueue.Instance.Do(new ActionChangeNoteType(Editor, chart, events,
			(e) => e is EditorTapNoteEvent,
			(e) => EditorEvent.CreateEvent(EventConfig.CreateMineConfig(e))));
	}

	private void ConvertTapsToFakes(EditorChart chart, IEnumerable<EditorEvent> events)
	{
		ActionQueue.Instance.Do(new ActionChangeNoteType(Editor, chart, events,
			(e) => e is EditorTapNoteEvent,
			(e) => EditorEvent.CreateEvent(EventConfig.CreateFakeNoteConfig(e))));
	}

	private void ConvertTapsToLifts(EditorChart chart, IEnumerable<EditorEvent> events)
	{
		ActionQueue.Instance.Do(new ActionChangeNoteType(Editor, chart, events,
			(e) => e is EditorTapNoteEvent,
			(e) => EditorEvent.CreateEvent(EventConfig.CreateLiftNoteConfig(e))));
	}

	private void ConvertMinesToTaps(EditorChart chart, IEnumerable<EditorEvent> events)
	{
		ActionQueue.Instance.Do(new ActionChangeNoteType(Editor, chart, events,
			(e) => e is EditorMineNoteEvent,
			(e) => EditorEvent.CreateEvent(EventConfig.CreateTapConfig(e))));
	}

	private void ConvertMinesToFakes(EditorChart chart, IEnumerable<EditorEvent> events)
	{
		ActionQueue.Instance.Do(new ActionChangeNoteType(Editor, chart, events,
			(e) => e is EditorMineNoteEvent,
			(e) => EditorEvent.CreateEvent(EventConfig.CreateFakeNoteConfig(e))));
	}

	private void ConvertMinesToLifts(EditorChart chart, IEnumerable<EditorEvent> events)
	{
		ActionQueue.Instance.Do(new ActionChangeNoteType(Editor, chart, events,
			(e) => e is EditorMineNoteEvent,
			(e) => EditorEvent.CreateEvent(EventConfig.CreateLiftNoteConfig(e))));
	}

	private void ConvertFakesToTaps(EditorChart chart, IEnumerable<EditorEvent> events)
	{
		ActionQueue.Instance.Do(new ActionChangeNoteType(Editor, chart, events,
			(e) => e is EditorFakeNoteEvent,
			(e) => EditorEvent.CreateEvent(EventConfig.CreateTapConfig(e))));
	}

	private void ConvertLiftsToTaps(EditorChart chart, IEnumerable<EditorEvent> events)
	{
		ActionQueue.Instance.Do(new ActionChangeNoteType(Editor, chart, events,
			(e) => e is EditorLiftNoteEvent,
			(e) => EditorEvent.CreateEvent(EventConfig.CreateTapConfig(e))));
	}

	private void ConvertHoldsToRolls(EditorChart chart, IEnumerable<EditorEvent> events)
	{
		ActionQueue.Instance.Do(new ActionChangeNoteType(Editor, chart, events,
			(e) => e is EditorHoldNoteEvent hn && !hn.IsRoll(),
			(e) => EditorEvent.CreateEvent(
				EventConfig.CreateHoldConfig(chart, e.GetRow(), e.GetLane(), e.GetPlayer(), e.GetRowDuration(),
					true))));
	}

	private void ConvertHoldsToTaps(EditorChart chart, IEnumerable<EditorEvent> events)
	{
		ActionQueue.Instance.Do(new ActionChangeNoteType(Editor, chart, events,
			(e) => e is EditorHoldNoteEvent hn && !hn.IsRoll(),
			(e) => EditorEvent.CreateEvent(EventConfig.CreateTapConfig(e))));
	}

	private void ConvertHoldsToMines(EditorChart chart, IEnumerable<EditorEvent> events)
	{
		ActionQueue.Instance.Do(new ActionChangeNoteType(Editor, chart, events,
			(e) => e is EditorHoldNoteEvent hn && !hn.IsRoll(),
			(e) => EditorEvent.CreateEvent(
				EventConfig.CreateMineConfig(chart, e.GetRow(), e.GetLane(), e.GetPlayer()))));
	}

	private void ConvertRollsToHolds(EditorChart chart, IEnumerable<EditorEvent> events)
	{
		ActionQueue.Instance.Do(new ActionChangeNoteType(Editor, chart, events,
			(e) => e is EditorHoldNoteEvent hn && hn.IsRoll(),
			(e) => EditorEvent.CreateEvent(
				EventConfig.CreateHoldConfig(chart, e.GetRow(), e.GetLane(), e.GetPlayer(), e.GetRowDuration(),
					false))));
	}

	private void ConvertRollsToTaps(EditorChart chart, IEnumerable<EditorEvent> events)
	{
		ActionQueue.Instance.Do(new ActionChangeNoteType(Editor, chart, events,
			(e) => e is EditorHoldNoteEvent hn && hn.IsRoll(),
			(e) => EditorEvent.CreateEvent(EventConfig.CreateTapConfig(e))));
	}

	private void ConvertRollsToMines(EditorChart chart, IEnumerable<EditorEvent> events)
	{
		ActionQueue.Instance.Do(new ActionChangeNoteType(Editor, chart, events,
			(e) => e is EditorHoldNoteEvent hn && hn.IsRoll(),
			(e) => EditorEvent.CreateEvent(
				EventConfig.CreateMineConfig(chart, e.GetRow(), e.GetLane(), e.GetPlayer()))));
	}

	private void ConvertWarpsToNegativeStops(EditorChart chart, IEnumerable<EditorEvent> events, bool allEvents)
	{
		if (allEvents)
			ActionQueue.Instance.Do(new ActionChangeWarpsToNegativeStops(Editor, chart));
		else
			ActionQueue.Instance.Do(new ActionChangeWarpsToNegativeStops(Editor, chart, events));
	}

	private void ConvertNegativeStopsToWarps(EditorChart chart, IEnumerable<EditorEvent> events, bool allEvents)
	{
		if (allEvents)
			ActionQueue.Instance.Do(new ActionChangeNegativeStopsToWarps(Editor, chart));
		else
			ActionQueue.Instance.Do(new ActionChangeNegativeStopsToWarps(Editor, chart, events));
	}

	#endregion Private Convert Selection Functions

	#region Public Convert Selection Functions

	public void ConvertSelectedTapsToMines()
	{
		if (!TryGetFocusedChartSelection(out var events))
			return;
		ConvertTapsToMines(Editor.GetFocusedChart(), events);
	}

	public void ConvertSelectedTapsToFakes()
	{
		if (!TryGetFocusedChartSelection(out var events))
			return;
		ConvertTapsToFakes(Editor.GetFocusedChart(), events);
	}

	public void ConvertSelectedTapsToLifts()
	{
		if (!TryGetFocusedChartSelection(out var events))
			return;
		ConvertTapsToLifts(Editor.GetFocusedChart(), events);
	}

	public void ConvertSelectedMinesToTaps()
	{
		if (!TryGetFocusedChartSelection(out var events))
			return;
		ConvertMinesToTaps(Editor.GetFocusedChart(), events);
	}

	public void ConvertSelectedMinesToFakes()
	{
		if (!TryGetFocusedChartSelection(out var events))
			return;
		ConvertMinesToFakes(Editor.GetFocusedChart(), events);
	}

	public void ConvertSelectedMinesToLifts()
	{
		if (!TryGetFocusedChartSelection(out var events))
			return;
		ConvertMinesToLifts(Editor.GetFocusedChart(), events);
	}

	public void ConvertSelectedFakesToTaps()
	{
		if (!TryGetFocusedChartSelection(out var events))
			return;
		ConvertFakesToTaps(Editor.GetFocusedChart(), events);
	}

	public void ConvertSelectedLiftsToTaps()
	{
		if (!TryGetFocusedChartSelection(out var events))
			return;
		ConvertLiftsToTaps(Editor.GetFocusedChart(), events);
	}

	public void ConvertSelectedHoldsToRolls()
	{
		if (!TryGetFocusedChartSelection(out var events))
			return;
		ConvertHoldsToRolls(Editor.GetFocusedChart(), events);
	}

	public void ConvertSelectedHoldsToTaps()
	{
		if (!TryGetFocusedChartSelection(out var events))
			return;
		ConvertHoldsToTaps(Editor.GetFocusedChart(), events);
	}

	public void ConvertSelectedHoldsToMines()
	{
		if (!TryGetFocusedChartSelection(out var events))
			return;
		ConvertHoldsToMines(Editor.GetFocusedChart(), events);
	}

	public void ConvertSelectedRollsToHolds()
	{
		if (!TryGetFocusedChartSelection(out var events))
			return;
		ConvertRollsToHolds(Editor.GetFocusedChart(), events);
	}

	public void ConvertSelectedRollsToTaps()
	{
		if (!TryGetFocusedChartSelection(out var events))
			return;
		ConvertRollsToTaps(Editor.GetFocusedChart(), events);
	}

	public void ConvertSelectedRollsToMines()
	{
		if (!TryGetFocusedChartSelection(out var events))
			return;
		ConvertRollsToMines(Editor.GetFocusedChart(), events);
	}

	public void ConvertSelectedWarpsToNegativeStops()
	{
		if (!TryGetFocusedChartSelection(out var events))
			return;
		ConvertWarpsToNegativeStops(Editor.GetFocusedChart(), events, false);
	}

	public void ConvertSelectedNegativeStopsToWarps()
	{
		if (!TryGetFocusedChartSelection(out var events))
			return;
		ConvertNegativeStopsToWarps(Editor.GetFocusedChart(), events, false);
	}

	#endregion Public Convert Selection Functions

	#region Selection

	public void DrawSelectAllMenu()
	{
		var p = Preferences.Instance.PreferencesKeyBinds;

		if (ImGui.BeginMenu("Select All"))
		{
			var disabled = !(Editor.GetFocusedChart()?.CanBeEdited() ?? false);
			if (disabled)
				PushDisabled();

			if (ImGui.MenuItem("Notes", UIControls.GetCommandString(p.SelectAll)))
			{
				Editor.OnSelectAll();
			}

			if (ImGui.MenuItem("Taps", UIControls.GetCommandString(p.SelectAllTaps)))
			{
				Editor.OnSelectAll((e) => e is EditorTapNoteEvent);
			}

			if (ImGui.MenuItem("Mines", UIControls.GetCommandString(p.SelectAllMines)))
			{
				Editor.OnSelectAll((e) => e is EditorMineNoteEvent);
			}

			if (ImGui.MenuItem("Fakes", UIControls.GetCommandString(p.SelectAllFakes)))
			{
				Editor.OnSelectAll((e) => e is EditorFakeNoteEvent);
			}

			if (ImGui.MenuItem("Lifts", UIControls.GetCommandString(p.SelectAllLifts)))
			{
				Editor.OnSelectAll((e) => e is EditorLiftNoteEvent);
			}

			if (ImGui.MenuItem("Holds", UIControls.GetCommandString(p.SelectAllHolds)))
			{
				Editor.OnSelectAll((e) => e is EditorHoldNoteEvent hn && !hn.IsRoll());
			}

			if (ImGui.MenuItem("Rolls", UIControls.GetCommandString(p.SelectAllRolls)))
			{
				Editor.OnSelectAll((e) => e is EditorHoldNoteEvent hn && hn.IsRoll());
			}

			if (ImGui.MenuItem("Holds and Rolls", UIControls.GetCommandString(p.SelectAllHoldsAndRolls)))
			{
				Editor.OnSelectAll((e) => e is EditorHoldNoteEvent);
			}

			if (ImGui.MenuItem("Miscellaneous Events", UIControls.GetCommandString(p.SelectAllMiscEvents)))
			{
				Editor.OnSelectAllAlt();
			}

			if (ImGui.MenuItem("Notes and Miscellaneous Events", UIControls.GetCommandString(p.SelectAllNotes)))
			{
				Editor.OnSelectAllShift();
			}

			if (ImGui.MenuItem("Patterns", UIControls.GetCommandString(p.SelectAllPatterns)))
			{
				Editor.OnSelectAll((e) => e is EditorPatternEvent);
			}

			if (disabled)
				PopDisabled();

			ImGui.EndMenu();
		}
	}

	#endregion Selection

	#region Add Events

	/// <summary>
	/// Helper for getting information around the current position needed for adding an
	/// event to the closest current row.
	/// </summary>
	/// <param name="row">The row to use for adding an event.</param>
	/// <param name="eventsAtRow">The EditorEvents at the current row.</param>
	/// <param name="currentRateAlteringEvent">The current EditorRateAlteringEvent for the row.</param>
	/// <returns>Whether or not an event can be added.</returns>
	public bool GetAddEventRowData(out int row, out List<EditorEvent> eventsAtRow,
		out EditorRateAlteringEvent currentRateAlteringEvent)
	{
		row = 0;
		currentRateAlteringEvent = null;
		eventsAtRow = null;
		var focusedChart = Editor.GetFocusedChart();
		if (!Editor.CanEdit() || focusedChart == null)
			return false;
		var position = Editor.GetPosition();
		row = Math.Max(0, position.GetNearestRow());
		currentRateAlteringEvent = focusedChart.GetRateAlteringEvents().FindActiveRateAlteringEventForPosition(row);
		eventsAtRow = focusedChart.GetEvents().FindEventsAtRow(row);
		return true;
	}

	/// <summary>
	/// Returns whether or not an event of the given type can exist with the other events at its row.
	/// </summary>
	/// <param name="eventsAtRow">The EditorEvents at the row.</param>
	/// <param name="type">The type of event to check.</param>
	/// <returns>True if the event of the given type can exist with the given other events and false otherwise.</returns>
	private static bool CanTypeOfEventExistAtRow(List<EditorEvent> eventsAtRow, Type type)
	{
		if (eventsAtRow != null)
			foreach (var currentEvent in eventsAtRow)
				if (currentEvent.GetType() == type)
					return false;
		return true;
	}

	/// <summary>
	/// Adds the given EditorEvent. Assumes all checks for ensuring the event can be added have been performed.
	/// </summary>
	/// <param name="editorEvent">EditorEvent to add.</param>
	private static void AddValidatedEvent(EditorEvent editorEvent)
	{
		ActionQueue.Instance.Do(new ActionAddEditorEvent(editorEvent));
	}

	public void DrawAddEventMenu()
	{
		if (ImGui.BeginMenu("Add Event"))
		{
			var disabled = !GetAddEventRowData(out var row, out var eventsAtRow, out var currentRateAlteringEvent);
			if (disabled)
				PushDisabled();

			var p = Preferences.Instance.PreferencesKeyBinds;

			var hasTempoEvent = false;
			var hasInterpolatedScrollRateEvent = false;
			var hasScrollRateEvent = false;
			var hasStopEvent = false;
			var hasDelayEvent = false;
			var hasWarpEvent = false;
			var hasFakeEvent = false;
			var hasTickCountEvent = false;
			var hasMultipliersEvent = false;
			var hasTimeSignatureEvent = false;
			var hasLabelEvent = false;
			var hasPatternEvent = false;

			if (eventsAtRow != null)
			{
				foreach (var currentEvent in eventsAtRow)
				{
					if (currentEvent is EditorTempoEvent)
						hasTempoEvent = true;
					else if (currentEvent is EditorInterpolatedRateAlteringEvent)
						hasInterpolatedScrollRateEvent = true;
					else if (currentEvent is EditorScrollRateEvent)
						hasScrollRateEvent = true;
					else if (currentEvent is EditorStopEvent)
						hasStopEvent = true;
					else if (currentEvent is EditorDelayEvent)
						hasDelayEvent = true;
					else if (currentEvent is EditorWarpEvent)
						hasWarpEvent = true;
					else if (currentEvent is EditorFakeSegmentEvent)
						hasFakeEvent = true;
					else if (currentEvent is EditorTickCountEvent)
						hasTickCountEvent = true;
					else if (currentEvent is EditorMultipliersEvent)
						hasMultipliersEvent = true;
					else if (currentEvent is EditorTimeSignatureEvent)
						hasTimeSignatureEvent = true;
					else if (currentEvent is EditorLabelEvent)
						hasLabelEvent = true;
					else if (currentEvent is EditorPatternEvent)
						hasPatternEvent = true;
				}
			}

			var chart = Editor.GetFocusedChart();
			var patternsDisabled = chart == null || !chart.SupportsAutogenFeatures() || hasPatternEvent;

			DrawAddEventMenuItem("Tempo", p.AddEventTempo, !hasTempoEvent, UITempoColorRGBA,
				EditorTempoEvent.EventShortDescription, row,
				() => CreateTempoEvent(row, currentRateAlteringEvent));

			ImGui.Separator();
			DrawAddEventMenuItem("Interpolated Scroll Rate", p.AddEventInterpolatedScrollRate, !hasInterpolatedScrollRateEvent,
				UISpeedsColorRGBA,
				EditorInterpolatedRateAlteringEvent.EventShortDescription, row,
				() => CreateInterpolatedScrollRateEvent(row));
			DrawAddEventMenuItem("Scroll Rate", p.AddEventScrollRate, !hasScrollRateEvent, UIScrollsColorRGBA,
				EditorScrollRateEvent.EventShortDescription, row,
				() => CreateScrollRateEvent(row));

			ImGui.Separator();
			DrawAddEventMenuItem("Stop", p.AddEventStop, !hasStopEvent, UIStopColorRGBA,
				EditorStopEvent.EventShortDescription, row,
				() => CreateStopEvent(row, currentRateAlteringEvent));
			DrawAddEventMenuItem("Delay", p.AddEventDelay, !hasDelayEvent, UIDelayColorRGBA,
				EditorDelayEvent.EventShortDescription, row,
				() => CreateDelayEvent(row, currentRateAlteringEvent));
			DrawAddEventMenuItem("Warp", p.AddEventWarp, !hasWarpEvent, UIWarpColorRGBA,
				EditorWarpEvent.EventShortDescription, row,
				() => CreateWarpEvent(row));

			ImGui.Separator();
			DrawAddEventMenuItem("Fake Region", p.AddEventFakeRegion, !hasFakeEvent, UIFakesColorRGBA,
				EditorFakeSegmentEvent.EventShortDescription, row,
				() => CreateFakeRegionEvent(row));
			DrawAddEventMenuItem("Ticks", p.AddEventTicks, !hasTickCountEvent, UITicksColorRGBA,
				EditorTickCountEvent.EventShortDescription, row,
				() => CreateTicksEvent(row));
			DrawAddEventMenuItem("Combo Multipliers", p.AddEventComboMultipliers, !hasMultipliersEvent, UIMultipliersColorRGBA,
				EditorMultipliersEvent.EventShortDescription, row,
				() => CreateComboMultipliersEvent(row));
			DrawAddEventMenuItem("Time Signature", p.AddEventTimeSignature, !hasTimeSignatureEvent, UITimeSignatureColorRGBA,
				EditorTimeSignatureEvent.EventShortDescription, row,
				() => CreateTimeSignatureEvent(row));
			DrawAddEventMenuItem("Label", p.AddEventLabel, !hasLabelEvent, UILabelColorRGBA,
				EditorLabelEvent.EventShortDescription, row,
				() => CreateLabelEvent(row));
			DrawAddEventMenuItem("Pattern", p.AddEventPattern, !patternsDisabled, UIPatternColorRGBA,
				EditorPatternEvent.EventShortDescription, row,
				() => CreatePatternEvent(row));

			ImGui.Separator();
			if (MenuItemWithColor("(Move) Music Preview", UIControls.GetCommandString(p.MoveEventPreview), true,
				    UIPreviewColorRGBA))
			{
				MoveValidatedMusicPreview();
			}

			ToolTip(EditorPreviewRegionEvent.GetEventShortDescription());
			if (MenuItemWithColor("(Move) End Hint", UIControls.GetCommandString(p.MoveEventEndHint), true,
				    UILastSecondHintColorRGBA))
			{
				MoveValidatedEndHint();
			}

			ToolTip(EditorLastSecondHintEvent.EventShortDescription);

			if (disabled)
				PopDisabled();

			ImGui.EndMenu();
		}
	}

	private static void DrawAddEventMenuItem(string name, List<Keys[]> inputs, bool enabled, uint color, string toolTipText,
		int row,
		Func<EditorEvent> createEventFunc)
	{
		if (MenuItemWithColor(name, UIControls.GetCommandString(inputs), enabled, color))
		{
			AddValidatedEvent(createEventFunc());
		}

		if (!enabled)
		{
			toolTipText +=
				$"\n\nOnly one {name} event can be specified per row.\nThere is already a {name} specified on row {row}.";
		}

		ToolTip(toolTipText);
	}

	#endregion Add Events

	#region Private Create Event Functions

	private EditorEvent CreateTempoEvent(int row, EditorRateAlteringEvent currentRateAlteringEvent)
	{
		return EditorEvent.CreateEvent(EventConfig.CreateTempoConfig(Editor.GetFocusedChart(), row,
			currentRateAlteringEvent!.GetTempo()));
	}

	private EditorEvent CreateInterpolatedScrollRateEvent(int row)
	{
		return EditorEvent.CreateEvent(EventConfig.CreateScrollRateInterpolationConfig(Editor.GetFocusedChart(), row));
	}

	private EditorEvent CreateScrollRateEvent(int row)
	{
		return EditorEvent.CreateEvent(EventConfig.CreateScrollRateConfig(Editor.GetFocusedChart(), row));
	}

	private EditorEvent CreateStopEvent(int row, EditorRateAlteringEvent currentRateAlteringEvent)
	{
		var stopTime = currentRateAlteringEvent!.GetSecondsPerRow() * SMCommon.MaxValidDenominator;
		return EditorEvent.CreateEvent(EventConfig.CreateStopConfig(Editor.GetFocusedChart(), row, stopTime));
	}

	private EditorEvent CreateDelayEvent(int row, EditorRateAlteringEvent currentRateAlteringEvent)
	{
		var stopTime = currentRateAlteringEvent!.GetSecondsPerRow() * SMCommon.MaxValidDenominator;
		return EditorEvent.CreateEvent(EventConfig.CreateDelayConfig(Editor.GetFocusedChart(), row, stopTime));
	}

	private EditorEvent CreateWarpEvent(int row)
	{
		return EditorEvent.CreateEvent(EventConfig.CreateWarpConfig(Editor.GetFocusedChart(), row));
	}

	private EditorEvent CreateFakeRegionEvent(int row)
	{
		return EditorEvent.CreateEvent(EventConfig.CreateFakeConfig(Editor.GetFocusedChart(), row));
	}

	private EditorEvent CreateTicksEvent(int row)
	{
		return EditorEvent.CreateEvent(EventConfig.CreateTickCountConfig(Editor.GetFocusedChart(), row));
	}

	private EditorEvent CreateComboMultipliersEvent(int row)
	{
		return EditorEvent.CreateEvent(EventConfig.CreateMultipliersConfig(Editor.GetFocusedChart(), row));
	}

	private EditorEvent CreateTimeSignatureEvent(int row)
	{
		return EditorEvent.CreateEvent(
			EventConfig.CreateTimeSignatureConfig(Editor.GetFocusedChart(), row, EditorChart.DefaultTimeSignature));
	}

	private EditorEvent CreateLabelEvent(int row)
	{
		return EditorEvent.CreateEvent(EventConfig.CreateLabelConfig(Editor.GetFocusedChart(), row));
	}

	private EditorEvent CreatePatternEvent(int row)
	{
		return EditorEvent.CreateEvent(EventConfig.CreatePatternConfig(Editor.GetFocusedChart(), row));
	}

	private void MoveValidatedMusicPreview()
	{
		var startTime = Math.Max(0.0, Editor.GetPosition().SongTime);
		ActionQueue.Instance.Do(new ActionSetObjectFieldOrPropertyValue<double>(Editor.GetActiveSong(),
			nameof(EditorSong.SampleStart), startTime, true));
	}

	private void MoveValidatedEndHint()
	{
		var currentTime = Math.Max(0.0, Editor.GetPosition().SongTime);
		ActionQueue.Instance.Do(new ActionSetObjectFieldOrPropertyValue<double>(Editor.GetActiveSong(),
			nameof(EditorSong.LastSecondHint), currentTime, true));
	}

	#endregion Private Create Event Functions

	#region Public Add Event Functions

	public void AddTempoEvent()
	{
		if (!GetAddEventRowData(out var row, out var eventsAtRow, out var currentRateAlteringEvent))
			return;
		if (!CanTypeOfEventExistAtRow(eventsAtRow, typeof(EditorTempoEvent)))
			return;
		AddValidatedEvent(CreateTempoEvent(row, currentRateAlteringEvent));
	}

	public void AddInterpolatedScrollRateEvent()
	{
		if (!GetAddEventRowData(out var row, out var eventsAtRow, out _))
			return;
		if (!CanTypeOfEventExistAtRow(eventsAtRow, typeof(EditorInterpolatedRateAlteringEvent)))
			return;
		AddValidatedEvent(CreateInterpolatedScrollRateEvent(row));
	}

	public void AddScrollRateEvent()
	{
		if (!GetAddEventRowData(out var row, out var eventsAtRow, out _))
			return;
		if (!CanTypeOfEventExistAtRow(eventsAtRow, typeof(EditorScrollRateEvent)))
			return;
		AddValidatedEvent(CreateScrollRateEvent(row));
	}

	public void AddStopEvent()
	{
		if (!GetAddEventRowData(out var row, out var eventsAtRow, out var currentRateAlteringEvent))
			return;
		if (!CanTypeOfEventExistAtRow(eventsAtRow, typeof(EditorStopEvent)))
			return;
		AddValidatedEvent(CreateStopEvent(row, currentRateAlteringEvent));
	}

	public void AddDelayEvent()
	{
		if (!GetAddEventRowData(out var row, out var eventsAtRow, out var currentRateAlteringEvent))
			return;
		if (!CanTypeOfEventExistAtRow(eventsAtRow, typeof(EditorDelayEvent)))
			return;
		AddValidatedEvent(CreateDelayEvent(row, currentRateAlteringEvent));
	}

	public void AddWarpEvent()
	{
		if (!GetAddEventRowData(out var row, out var eventsAtRow, out _))
			return;
		if (!CanTypeOfEventExistAtRow(eventsAtRow, typeof(EditorWarpEvent)))
			return;
		AddValidatedEvent(CreateWarpEvent(row));
	}

	public void AddFakeRegionEvent()
	{
		if (!GetAddEventRowData(out var row, out var eventsAtRow, out _))
			return;
		if (!CanTypeOfEventExistAtRow(eventsAtRow, typeof(EditorFakeSegmentEvent)))
			return;
		AddValidatedEvent(CreateFakeRegionEvent(row));
	}

	public void AddTicksEvent()
	{
		if (!GetAddEventRowData(out var row, out var eventsAtRow, out _))
			return;
		if (!CanTypeOfEventExistAtRow(eventsAtRow, typeof(EditorTickCountEvent)))
			return;
		AddValidatedEvent(CreateTicksEvent(row));
	}

	public void AddComboMultipliersEvent()
	{
		if (!GetAddEventRowData(out var row, out var eventsAtRow, out _))
			return;
		if (!CanTypeOfEventExistAtRow(eventsAtRow, typeof(EditorMultipliersEvent)))
			return;
		AddValidatedEvent(CreateComboMultipliersEvent(row));
	}

	public void AddTimeSignatureEvent()
	{
		if (!GetAddEventRowData(out var row, out var eventsAtRow, out _))
			return;
		if (!CanTypeOfEventExistAtRow(eventsAtRow, typeof(EditorTimeSignatureEvent)))
			return;
		AddValidatedEvent(CreateTimeSignatureEvent(row));
	}

	public void AddLabelEvent()
	{
		if (!GetAddEventRowData(out var row, out var eventsAtRow, out _))
			return;
		if (!CanTypeOfEventExistAtRow(eventsAtRow, typeof(EditorLabelEvent)))
			return;
		AddValidatedEvent(CreateLabelEvent(row));
	}

	public void AddPatternEvent()
	{
		if (!GetAddEventRowData(out var row, out var eventsAtRow, out _))
			return;
		if (!CanTypeOfEventExistAtRow(eventsAtRow, typeof(EditorPatternEvent)))
			return;
		AddValidatedEvent(CreatePatternEvent(row));
	}

	public void MoveMusicPreview()
	{
		if (!Editor.CanEdit() || Editor.GetFocusedChart() == null)
			return;
		MoveValidatedMusicPreview();
	}

	public void MoveEndHint()
	{
		if (!Editor.CanEdit() || Editor.GetFocusedChart() == null)
			return;
		MoveValidatedEndHint();
	}

	#endregion Public Add Event Functions
}

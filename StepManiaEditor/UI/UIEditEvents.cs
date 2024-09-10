using System;
using System.Collections.Generic;
using Fumen.Converters;
using ImGuiNET;
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
		var chart = Editor.GetActiveChart();
		var allEvents = false;
		if (events == null)
		{
			events = chart?.GetEvents();
			allEvents = true;
		}

		var disabled = !(chart?.CanBeEdited() ?? false) || events == null;
		if (disabled)
			PushDisabled();

		if (ImGui.MenuItem("Mirror"))
		{
			ActionQueue.Instance.Do(new ActionMirrorSelection(Editor, chart, events));
		}

		if (ImGui.MenuItem("Flip"))
		{
			ActionQueue.Instance.Do(new ActionFlipSelection(Editor, chart, events));
		}

		if (ImGui.MenuItem("Mirror and Flip"))
		{
			ActionQueue.Instance.Do(new ActionMirrorAndFlipSelection(Editor, chart, events));
		}

		if (ImGui.MenuItem("Shift Right"))
		{
			Editor.OnShiftNotesRight(events);
		}

		if (allEvents)
		{
			if (ImGui.MenuItem("Shift Right and Wrap"))
			{
				Editor.OnShiftNotesRightAndWrap(events);
			}
		}
		else
		{
			if (ImGui.MenuItem("Shift Right and Wrap", "Ctrl+Shift+Right"))
			{
				Editor.OnShiftNotesRightAndWrap(events);
			}
		}

		if (ImGui.MenuItem("Shift Left"))
		{
			Editor.OnShiftNotesLeft(events);
		}

		if (allEvents)
		{
			if (ImGui.MenuItem("Shift Left and Wrap"))
			{
				Editor.OnShiftNotesLeftAndWrap(events);
			}
		}
		else
		{
			if (ImGui.MenuItem("Shift Left and Wrap", "Ctrl+Shift+Left"))
			{
				Editor.OnShiftNotesLeftAndWrap(events);
			}
		}

		var rows = Editor.GetShiftNotesRows();
		var shiftAmount = $"1/{SMCommon.MaxValidDenominator / rows * SMCommon.NumBeatsPerMeasure}";

		if (allEvents)
		{
			if (ImGui.MenuItem($"Shift Earlier ({shiftAmount})"))
			{
				Editor.OnShiftNotesEarlier(events);
			}

			if (ImGui.MenuItem($"Shift Later ({shiftAmount})"))
			{
				Editor.OnShiftNotesLater(events);
			}
		}
		else
		{
			if (ImGui.MenuItem($"Shift Earlier ({shiftAmount})", "Ctrl+Shift+Up"))
			{
				Editor.OnShiftNotesEarlier(events);
			}

			if (ImGui.MenuItem($"Shift Later ({shiftAmount})", "Ctrl+Shift+Down"))
			{
				Editor.OnShiftNotesLater(events);
			}
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

	public void DrawConvertSelectedMenu(IEnumerable<EditorEvent> events)
	{
		if (ImGui.BeginMenu("Convert Selected"))
		{
			DrawConvertMenuItems(Editor.GetActiveChart(), events);
			ImGui.EndMenu();
		}
	}

	public void DrawConvertAllMenu()
	{
		if (ImGui.BeginMenu("Convert All"))
		{
			DrawConvertMenuItems(Editor.GetActiveChart());
			ImGui.EndMenu();
		}
	}

	private void DrawConvertMenuItems(EditorChart chart, IEnumerable<EditorEvent> events = null)
	{
		var allEvents = false;
		if (events == null)
		{
			events = chart?.GetEvents();
			allEvents = true;
		}

		var disabled = !(chart?.CanBeEdited() ?? false) || events == null;
		if (disabled)
			PushDisabled();

		if (ImGui.MenuItem("Taps to Mines"))
		{
			ActionQueue.Instance.Do(new ActionChangeNoteType(Editor, chart, events,
				(e) => e is EditorTapNoteEvent,
				(e) => EditorEvent.CreateEvent(EventConfig.CreateMineConfig(e))));
		}

		if (ImGui.MenuItem("Taps to Fakes"))
		{
			ActionQueue.Instance.Do(new ActionChangeNoteType(Editor, chart, events,
				(e) => e is EditorTapNoteEvent,
				(e) => EditorEvent.CreateEvent(EventConfig.CreateFakeNoteConfig(e))));
		}

		if (ImGui.MenuItem("Taps to Lifts"))
		{
			ActionQueue.Instance.Do(new ActionChangeNoteType(Editor, chart, events,
				(e) => e is EditorTapNoteEvent,
				(e) => EditorEvent.CreateEvent(EventConfig.CreateLiftNoteConfig(e))));
		}

		ImGui.Separator();
		if (ImGui.MenuItem("Mines to Taps"))
		{
			ActionQueue.Instance.Do(new ActionChangeNoteType(Editor, chart, events,
				(e) => e is EditorMineNoteEvent,
				(e) => EditorEvent.CreateEvent(EventConfig.CreateTapConfig(e))));
		}

		if (ImGui.MenuItem("Mines to Fakes"))
		{
			ActionQueue.Instance.Do(new ActionChangeNoteType(Editor, chart, events,
				(e) => e is EditorMineNoteEvent,
				(e) => EditorEvent.CreateEvent(EventConfig.CreateFakeNoteConfig(e))));
		}

		if (ImGui.MenuItem("Mines to Lifts"))
		{
			ActionQueue.Instance.Do(new ActionChangeNoteType(Editor, chart, events,
				(e) => e is EditorMineNoteEvent,
				(e) => EditorEvent.CreateEvent(EventConfig.CreateLiftNoteConfig(e))));
		}

		ImGui.Separator();
		if (ImGui.MenuItem("Fakes to Taps"))
		{
			ActionQueue.Instance.Do(new ActionChangeNoteType(Editor, chart, events,
				(e) => e is EditorFakeNoteEvent,
				(e) => EditorEvent.CreateEvent(EventConfig.CreateTapConfig(e))));
		}

		if (ImGui.MenuItem("Lifts to Taps"))
		{
			ActionQueue.Instance.Do(new ActionChangeNoteType(Editor, chart, events,
				(e) => e is EditorLiftNoteEvent,
				(e) => EditorEvent.CreateEvent(EventConfig.CreateTapConfig(e))));
		}

		ImGui.Separator();
		if (ImGui.MenuItem("Holds to Rolls"))
		{
			ActionQueue.Instance.Do(new ActionChangeNoteType(Editor, chart, events,
				(e) => e is EditorHoldNoteEvent hn && !hn.IsRoll(),
				(e) => EditorEvent.CreateEvent(
					EventConfig.CreateHoldConfig(chart, e.GetRow(), e.GetLane(), e.GetRowDuration(),
						true))));
		}

		if (ImGui.MenuItem("Holds to Taps"))
		{
			ActionQueue.Instance.Do(new ActionChangeNoteType(Editor, chart, events,
				(e) => e is EditorHoldNoteEvent hn && !hn.IsRoll(),
				(e) => EditorEvent.CreateEvent(EventConfig.CreateTapConfig(e))));
		}

		if (ImGui.MenuItem("Holds to Mines"))
		{
			ActionQueue.Instance.Do(new ActionChangeNoteType(Editor, chart, events,
				(e) => e is EditorHoldNoteEvent hn && !hn.IsRoll(),
				(e) => EditorEvent.CreateEvent(
					EventConfig.CreateMineConfig(chart, e.GetRow(), e.GetLane()))));
		}

		ImGui.Separator();
		if (ImGui.MenuItem("Rolls to Holds"))
		{
			ActionQueue.Instance.Do(new ActionChangeNoteType(Editor, chart, events,
				(e) => e is EditorHoldNoteEvent hn && hn.IsRoll(),
				(e) => EditorEvent.CreateEvent(
					EventConfig.CreateHoldConfig(chart, e.GetRow(), e.GetLane(), e.GetRowDuration(),
						false))));
		}

		if (ImGui.MenuItem("Rolls to Taps"))
		{
			ActionQueue.Instance.Do(new ActionChangeNoteType(Editor, chart, events,
				(e) => e is EditorHoldNoteEvent hn && hn.IsRoll(),
				(e) => EditorEvent.CreateEvent(EventConfig.CreateTapConfig(e))));
		}

		if (ImGui.MenuItem("Rolls to Mines"))
		{
			ActionQueue.Instance.Do(new ActionChangeNoteType(Editor, chart, events,
				(e) => e is EditorHoldNoteEvent hn && hn.IsRoll(),
				(e) => EditorEvent.CreateEvent(
					EventConfig.CreateMineConfig(chart, e.GetRow(), e.GetLane()))));
		}

		ImGui.Separator();
		if (ImGui.MenuItem("Warps to Negative Stops"))
		{
			if (allEvents)
				ActionQueue.Instance.Do(new ActionChangeWarpsToNegativeStops(Editor, chart));
			else
				ActionQueue.Instance.Do(new ActionChangeWarpsToNegativeStops(Editor, chart, events));
		}

		if (ImGui.MenuItem("Negative Stops to Warps"))
		{
			if (allEvents)
				ActionQueue.Instance.Do(new ActionChangeNegativeStopsToWarps(Editor, chart));
			else
				ActionQueue.Instance.Do(new ActionChangeNegativeStopsToWarps(Editor, chart, events));
		}

		if (disabled)
			PopDisabled();
	}

	public void DrawSelectAllMenu()
	{
		if (ImGui.BeginMenu("Select All"))
		{
			var disabled = !(Editor.GetActiveChart()?.CanBeEdited() ?? false);
			if (disabled)
				PushDisabled();

			if (ImGui.MenuItem("Notes", "Ctrl+A"))
			{
				Editor.OnSelectAll();
			}

			if (ImGui.Selectable("Taps"))
			{
				Editor.OnSelectAll((e) => e is EditorTapNoteEvent);
			}

			if (ImGui.Selectable("Mines"))
			{
				Editor.OnSelectAll((e) => e is EditorMineNoteEvent);
			}

			if (ImGui.Selectable("Fakes"))
			{
				Editor.OnSelectAll((e) => e is EditorFakeNoteEvent);
			}

			if (ImGui.Selectable("Lifts"))
			{
				Editor.OnSelectAll((e) => e is EditorLiftNoteEvent);
			}

			if (ImGui.Selectable("Holds"))
			{
				Editor.OnSelectAll((e) => e is EditorHoldNoteEvent hn && !hn.IsRoll());
			}

			if (ImGui.Selectable("Rolls"))
			{
				Editor.OnSelectAll((e) => e is EditorHoldNoteEvent hn && hn.IsRoll());
			}

			if (ImGui.Selectable("Holds and Rolls"))
			{
				Editor.OnSelectAll((e) => e is EditorHoldNoteEvent);
			}

			if (ImGui.MenuItem("Miscellaneous Events", "Ctrl+Alt+A"))
			{
				Editor.OnSelectAllAlt();
			}

			if (ImGui.MenuItem("Notes and Miscellaneous Events", "Ctrl+Shift+A"))
			{
				Editor.OnSelectAllShift();
			}

			if (ImGui.Selectable("Patterns"))
			{
				Editor.OnSelectAll((e) => e is EditorPatternEvent);
			}

			if (disabled)
				PopDisabled();

			ImGui.EndMenu();
		}
	}

	public void DrawAddEventMenu()
	{
		var chart = Editor.GetActiveChart();
		var canEditChart = chart?.CanBeEdited() ?? false;
		var song = Editor.GetActiveSong();
		var canEditSong = song?.CanBeEdited() ?? false;
		var position = Editor.GetPosition();
		var row = Math.Max(0, position.GetNearestRow());
		var disabled = !canEditChart || !canEditSong;

		if (ImGui.BeginMenu("Add Event"))
		{
			if (disabled)
				PushDisabled();

			var events = chart?.GetEvents().FindEventsAtRow(row);
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

			if (events != null)
			{
				foreach (var currentEvent in events)
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

			var currentRateAlteringEvent =
				chart?.GetRateAlteringEvents().FindActiveRateAlteringEventForPosition(row) ?? null;

			DrawAddEventMenuItem("Tempo", !hasTempoEvent, UITempoColorRGBA, EditorTempoEvent.EventShortDescription, row,
				() => EditorEvent.CreateEvent(
					EventConfig.CreateTempoConfig(chart, row,
						currentRateAlteringEvent!.GetTempo())));

			ImGui.Separator();
			DrawAddEventMenuItem("Interpolated Scroll Rate", !hasInterpolatedScrollRateEvent, UISpeedsColorRGBA,
				EditorInterpolatedRateAlteringEvent.EventShortDescription, row,
				() => EditorEvent.CreateEvent(
					EventConfig.CreateScrollRateInterpolationConfig(chart, row)));
			DrawAddEventMenuItem("Scroll Rate", !hasScrollRateEvent, UIScrollsColorRGBA,
				EditorScrollRateEvent.EventShortDescription, row,
				() => EditorEvent.CreateEvent(EventConfig.CreateScrollRateConfig(chart, row)));

			ImGui.Separator();
			DrawAddEventMenuItem("Stop", !hasStopEvent, UIStopColorRGBA, EditorStopEvent.EventShortDescription, row,
				() =>
				{
					var stopTime = currentRateAlteringEvent!.GetSecondsPerRow() * SMCommon.MaxValidDenominator;
					return EditorEvent.CreateEvent(EventConfig.CreateStopConfig(chart, row, stopTime));
				});
			DrawAddEventMenuItem("Delay", !hasDelayEvent, UIDelayColorRGBA, EditorDelayEvent.EventShortDescription, row,
				() =>
				{
					var stopTime = currentRateAlteringEvent!.GetSecondsPerRow() * SMCommon.MaxValidDenominator;
					return EditorEvent.CreateEvent(EventConfig.CreateDelayConfig(chart, row, stopTime));
				});
			DrawAddEventMenuItem("Warp", !hasWarpEvent, UIWarpColorRGBA, EditorWarpEvent.EventShortDescription, row,
				() => EditorEvent.CreateEvent(EventConfig.CreateWarpConfig(chart, row)));

			ImGui.Separator();
			DrawAddEventMenuItem("Fake Region", !hasFakeEvent, UIFakesColorRGBA,
				EditorFakeSegmentEvent.EventShortDescription, row,
				() => EditorEvent.CreateEvent(EventConfig.CreateFakeConfig(chart, row)));
			DrawAddEventMenuItem("Ticks", !hasTickCountEvent, UITicksColorRGBA,
				EditorTickCountEvent.EventShortDescription, row,
				() => EditorEvent.CreateEvent(EventConfig.CreateTickCountConfig(chart, row)));
			DrawAddEventMenuItem("Combo Multipliers", !hasMultipliersEvent, UIMultipliersColorRGBA,
				EditorMultipliersEvent.EventShortDescription, row,
				() => EditorEvent.CreateEvent(EventConfig.CreateMultipliersConfig(chart, row)));
			DrawAddEventMenuItem("Time Signature", !hasTimeSignatureEvent, UITimeSignatureColorRGBA,
				EditorTimeSignatureEvent.EventShortDescription, row,
				() => EditorEvent.CreateEvent(EventConfig.CreateTimeSignatureConfig(chart,
					row, EditorChart.DefaultTimeSignature)));
			DrawAddEventMenuItem("Label", !hasLabelEvent, UILabelColorRGBA, EditorLabelEvent.EventShortDescription, row,
				() => EditorEvent.CreateEvent(EventConfig.CreateLabelConfig(chart, row)));
			DrawAddEventMenuItem("Pattern", !hasPatternEvent, UIPatternColorRGBA,
				EditorPatternEvent.EventShortDescription, row,
				() => EditorEvent.CreateEvent(EventConfig.CreatePatternConfig(chart, row)));

			ImGui.Separator();
			if (MenuItemWithColor("(Move) Music Preview", true, UIPreviewColorRGBA))
			{
				var startTime = Math.Max(0.0, position.SongTime);
				ActionQueue.Instance.Do(new ActionSetObjectFieldOrPropertyValue<double>(song,
					nameof(EditorSong.SampleStart), startTime, true));
			}

			ToolTip(EditorPreviewRegionEvent.EventShortDescription);
			if (MenuItemWithColor("(Move) End Hint", true, UILastSecondHintColorRGBA))
			{
				var currentTime = Math.Max(0.0, position.ChartTime);
				ActionQueue.Instance.Do(new ActionSetObjectFieldOrPropertyValue<double>(song,
					nameof(EditorSong.LastSecondHint), currentTime, true));
			}

			ToolTip(EditorLastSecondHintEvent.EventShortDescription);

			if (disabled)
				PopDisabled();

			ImGui.EndMenu();
		}
	}

	private static void DrawAddEventMenuItem(string name, bool enabled, uint color, string toolTipText, int row,
		Func<EditorEvent> createEventFunc)
	{
		if (MenuItemWithColor(name, enabled, color))
		{
			ActionQueue.Instance.Do(new ActionAddEditorEvent(createEventFunc()));
		}

		if (!enabled)
		{
			toolTipText +=
				$"\n\nOnly one {name} event can be specified per row.\nThere is already a {name} specified on row {row}.";
		}

		ToolTip(toolTipText);
	}
}

using System;
using System.Collections.Generic;
using System.Linq;
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
			DrawConvertSelectedMenu(selectedEvents);
			DrawSwapSelectedMenu(selectedEvents);
			DrawShiftEventsMenuItems(selectedEvents);
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

	public void DrawSwapSelectedMenu(IEnumerable<EditorEvent> events)
	{
		if (ImGui.BeginMenu("Swap Selected"))
		{
			DrawSwapMenuItems(Editor.GetFocusedChart(), events);
			ImGui.EndMenu();
		}
	}

	public void DrawSwapAllMenu()
	{
		if (ImGui.BeginMenu("Swap All"))
		{
			DrawSwapMenuItems(Editor.GetFocusedChart());
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

		if ((chart?.IsMultiPlayer() ?? false) && !allEvents)
		{
			for (var i = 0; i < chart.MaxPlayers; i++)
			{
				ImGui.PushStyleColor(ImGuiCol.Text, ArrowGraphicManager.GetUIColorForPlayer(i));

				var shortCut = i switch
				{
					0 => UIControls.GetCommandString(p.ConvertSelectedNotesToPlayer1),
					1 => UIControls.GetCommandString(p.ConvertSelectedNotesToPlayer2),
					2 => UIControls.GetCommandString(p.ConvertSelectedNotesToPlayer3),
					3 => UIControls.GetCommandString(p.ConvertSelectedNotesToPlayer4),
					_ => null,
				};

				if (ImGui.MenuItem($"Notes to Player {i + 1} Notes", shortCut))
				{
					ImGui.PopStyleColor();
					ConvertNotesToPlayer(chart, events, i);
				}
				else
				{
					ImGui.PopStyleColor();
				}
			}

			ImGui.Separator();
		}

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

	private void DrawSwapMenuItems(EditorChart chart, IEnumerable<EditorEvent> events = null)
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

		if ((chart?.IsMultiPlayer() ?? false) && !allEvents)
		{
			for (var p1 = 0; p1 < chart.MaxPlayers; p1++)
			{
				for (var p2 = p1; p2 < chart.MaxPlayers; p2++)
				{
					if (p1 == p2)
						continue;

					var shortCut = p1 switch
					{
						0 when p2 == 1 => UIControls.GetCommandString(p.SwapSelectedPlayer1And2Notes),
						0 when p2 == 2 => UIControls.GetCommandString(p.SwapSelectedPlayer1And3Notes),
						0 when p2 == 3 => UIControls.GetCommandString(p.SwapSelectedPlayer1And4Notes),
						1 when p2 == 2 => UIControls.GetCommandString(p.SwapSelectedPlayer2And3Notes),
						1 when p2 == 3 => UIControls.GetCommandString(p.SwapSelectedPlayer2And4Notes),
						2 when p2 == 3 => UIControls.GetCommandString(p.SwapSelectedPlayer3And4Notes),
						_ => null,
					};

					if (ImGui.MenuItem($"Player {p1 + 1} and {p2 + 1} Notes", shortCut))
					{
						SwapNotesBetweenPlayers(chart, events, p1, p2);
					}
				}
			}

			ImGui.Separator();
		}

		if (ImGui.MenuItem("Taps and Mines", allEvents ? null : UIControls.GetCommandString(p.SwapSelectedTapsAndMines)))
		{
			SwapTapsAndMines(chart, events);
		}

		if (ImGui.MenuItem("Taps and Fakes", allEvents ? null : UIControls.GetCommandString(p.SwapSelectedTapsAndFakes)))
		{
			SwapTapsAndFakes(chart, events);
		}

		if (ImGui.MenuItem("Taps and Lifts", allEvents ? null : UIControls.GetCommandString(p.SwapSelectedTapsAndLifts)))
		{
			SwapTapsAndLifts(chart, events);
		}

		ImGui.Separator();

		if (ImGui.MenuItem("Mines and Fakes", allEvents ? null : UIControls.GetCommandString(p.SwapSelectedMinesAndFakes)))
		{
			SwapMinesAndFakes(chart, events);
		}

		if (ImGui.MenuItem("Mines and Lifts", allEvents ? null : UIControls.GetCommandString(p.SwapSelectedMinesAndLifts)))
		{
			SwapMinesAndLifts(chart, events);
		}

		ImGui.Separator();
		if (ImGui.MenuItem("Holds and Rolls", allEvents ? null : UIControls.GetCommandString(p.SwapSelectedHoldsAndRolls)))
		{
			SwapHoldsAndRolls(chart, events);
		}

		if (ImGui.MenuItem("Holds and Taps", allEvents ? null : UIControls.GetCommandString(p.SwapSelectedHoldsAndTaps)))
		{
			SwapHoldsAndTaps(chart, events);
		}

		if (ImGui.MenuItem("Holds and Mines", allEvents ? null : UIControls.GetCommandString(p.SwapSelectedHoldsAndMines)))
		{
			SwapHoldsAndMines(chart, events);
		}

		ImGui.Separator();

		if (ImGui.MenuItem("Rolls and Taps", allEvents ? null : UIControls.GetCommandString(p.SwapSelectedRollsAndTaps)))
		{
			SwapRollsAndTaps(chart, events);
		}

		if (ImGui.MenuItem("Rolls and Mines", allEvents ? null : UIControls.GetCommandString(p.SwapSelectedRollsAndMines)))
		{
			SwapRollsAndMines(chart, events);
		}

		if (disabled)
			PopDisabled();
	}

	#endregion Convert Notes

	#region Private Convert Selection Functions

	private void ConvertNotesToPlayer(EditorChart chart, IEnumerable<EditorEvent> events, int player)
	{
		ActionQueue.Instance.Do(new ActionChangeNotePlayer(Editor, chart, events, player));
	}

	private void SwapNotesBetweenPlayers(EditorChart chart, IEnumerable<EditorEvent> events, int playerA, int playerB)
	{
		ActionQueue.Instance.Do(new ActionSwapNotePlayer(Editor, chart, events, playerA, playerB));
	}

	private void ConvertTapsToMines(EditorChart chart, IEnumerable<EditorEvent> events)
	{
		ActionQueue.Instance.Do(new ActionChangeNoteType(Editor, chart, events,
			e => e is EditorTapNoteEvent,
			e => EditorEvent.CreateEvent(EventConfig.CreateMineConfig(e)),
			"Taps", "Mines"));
	}

	private void ConvertTapsToFakes(EditorChart chart, IEnumerable<EditorEvent> events)
	{
		ActionQueue.Instance.Do(new ActionChangeNoteType(Editor, chart, events,
			e => e is EditorTapNoteEvent,
			e => EditorEvent.CreateEvent(EventConfig.CreateFakeNoteConfig(e)),
			"Taps", "Fakes"));
	}

	private void ConvertTapsToLifts(EditorChart chart, IEnumerable<EditorEvent> events)
	{
		ActionQueue.Instance.Do(new ActionChangeNoteType(Editor, chart, events,
			e => e is EditorTapNoteEvent,
			e => EditorEvent.CreateEvent(EventConfig.CreateLiftNoteConfig(e)),
			"Taps", "Lifts"));
	}

	private void ConvertMinesToTaps(EditorChart chart, IEnumerable<EditorEvent> events)
	{
		ActionQueue.Instance.Do(new ActionChangeNoteType(Editor, chart, events,
			e => e is EditorMineNoteEvent,
			e => EditorEvent.CreateEvent(EventConfig.CreateTapConfig(e)),
			"Mines", "Taps"));
	}

	private void ConvertMinesToFakes(EditorChart chart, IEnumerable<EditorEvent> events)
	{
		ActionQueue.Instance.Do(new ActionChangeNoteType(Editor, chart, events,
			e => e is EditorMineNoteEvent,
			e => EditorEvent.CreateEvent(EventConfig.CreateFakeNoteConfig(e)),
			"Mines", "Fakes"));
	}

	private void ConvertMinesToLifts(EditorChart chart, IEnumerable<EditorEvent> events)
	{
		ActionQueue.Instance.Do(new ActionChangeNoteType(Editor, chart, events,
			e => e is EditorMineNoteEvent,
			e => EditorEvent.CreateEvent(EventConfig.CreateLiftNoteConfig(e)),
			"Mines", "Lifts"));
	}

	private void ConvertFakesToTaps(EditorChart chart, IEnumerable<EditorEvent> events)
	{
		ActionQueue.Instance.Do(new ActionChangeNoteType(Editor, chart, events,
			e => e is EditorFakeNoteEvent,
			e => EditorEvent.CreateEvent(EventConfig.CreateTapConfig(e)),
			"Fakes", "Taps"));
	}

	private void ConvertLiftsToTaps(EditorChart chart, IEnumerable<EditorEvent> events)
	{
		ActionQueue.Instance.Do(new ActionChangeNoteType(Editor, chart, events,
			e => e is EditorLiftNoteEvent,
			e => EditorEvent.CreateEvent(EventConfig.CreateTapConfig(e)),
			"Lifts", "Taps"));
	}

	private void ConvertHoldsToRolls(EditorChart chart, IEnumerable<EditorEvent> events)
	{
		ActionQueue.Instance.Do(new ActionChangeNoteType(Editor, chart, events,
			e => e is EditorHoldNoteEvent hn && !hn.IsRoll(),
			e => EditorEvent.CreateEvent(
				EventConfig.CreateHoldConfig(chart, e.GetRow(), e.GetLane(), e.GetPlayer(), e.GetRowDuration(),
					true)),
			"Holds", "Rolls"));
	}

	private void ConvertHoldsToTaps(EditorChart chart, IEnumerable<EditorEvent> events)
	{
		ActionQueue.Instance.Do(new ActionChangeNoteType(Editor, chart, events,
			e => e is EditorHoldNoteEvent hn && !hn.IsRoll(),
			e => EditorEvent.CreateEvent(EventConfig.CreateTapConfig(e)),
			"Holds", "Taps"));
	}

	private void ConvertHoldsToMines(EditorChart chart, IEnumerable<EditorEvent> events)
	{
		ActionQueue.Instance.Do(new ActionChangeNoteType(Editor, chart, events,
			e => e is EditorHoldNoteEvent hn && !hn.IsRoll(),
			e => EditorEvent.CreateEvent(EventConfig.CreateMineConfig(e)),
			"Holds", "Mines"));
	}

	private void ConvertRollsToHolds(EditorChart chart, IEnumerable<EditorEvent> events)
	{
		ActionQueue.Instance.Do(new ActionChangeNoteType(Editor, chart, events,
			e => e is EditorHoldNoteEvent hn && hn.IsRoll(),
			e => EditorEvent.CreateEvent(
				EventConfig.CreateHoldConfig(chart, e.GetRow(), e.GetLane(), e.GetPlayer(), e.GetRowDuration(),
					false)),
			"Rolls", "Holds"));
	}

	private void ConvertRollsToTaps(EditorChart chart, IEnumerable<EditorEvent> events)
	{
		ActionQueue.Instance.Do(new ActionChangeNoteType(Editor, chart, events,
			e => e is EditorHoldNoteEvent hn && hn.IsRoll(),
			e => EditorEvent.CreateEvent(EventConfig.CreateTapConfig(e)),
			"Rolls", "Taps"));
	}

	private void ConvertRollsToMines(EditorChart chart, IEnumerable<EditorEvent> events)
	{
		ActionQueue.Instance.Do(new ActionChangeNoteType(Editor, chart, events,
			e => e is EditorHoldNoteEvent hn && hn.IsRoll(),
			e => EditorEvent.CreateEvent(
				EventConfig.CreateMineConfig(chart, e.GetRow(), e.GetLane(), e.GetPlayer())),
			"Rolls", "Mines"));
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

	private void SwapTapsAndMines(EditorChart chart, IEnumerable<EditorEvent> events)
	{
		ActionQueue.Instance.Do(new ActionChangeNoteType(Editor, chart, events,
			e => e is EditorTapNoteEvent or EditorMineNoteEvent,
			e =>
			{
				if (e is EditorTapNoteEvent)
					return EditorEvent.CreateEvent(EventConfig.CreateMineConfig(e));
				return EditorEvent.CreateEvent(EventConfig.CreateTapConfig(e));
			},
			"Taps", "Mines", true));
	}

	private void SwapTapsAndFakes(EditorChart chart, IEnumerable<EditorEvent> events)
	{
		ActionQueue.Instance.Do(new ActionChangeNoteType(Editor, chart, events,
			e => e is EditorTapNoteEvent or EditorFakeNoteEvent,
			e =>
			{
				if (e is EditorTapNoteEvent)
					return EditorEvent.CreateEvent(EventConfig.CreateFakeNoteConfig(e));
				return EditorEvent.CreateEvent(EventConfig.CreateTapConfig(e));
			},
			"Taps", "Fakes", true));
	}

	private void SwapTapsAndLifts(EditorChart chart, IEnumerable<EditorEvent> events)
	{
		ActionQueue.Instance.Do(new ActionChangeNoteType(Editor, chart, events,
			e => e is EditorTapNoteEvent or EditorLiftNoteEvent,
			e =>
			{
				if (e is EditorTapNoteEvent)
					return EditorEvent.CreateEvent(EventConfig.CreateLiftNoteConfig(e));
				return EditorEvent.CreateEvent(EventConfig.CreateTapConfig(e));
			},
			"Taps", "Lifts", true));
	}

	private void SwapMinesAndFakes(EditorChart chart, IEnumerable<EditorEvent> events)
	{
		ActionQueue.Instance.Do(new ActionChangeNoteType(Editor, chart, events,
			e => e is EditorMineNoteEvent or EditorFakeNoteEvent,
			e =>
			{
				if (e is EditorMineNoteEvent)
					return EditorEvent.CreateEvent(EventConfig.CreateFakeNoteConfig(e));
				return EditorEvent.CreateEvent(EventConfig.CreateMineConfig(e));
			},
			"Mines", "Fakes", true));
	}

	private void SwapMinesAndLifts(EditorChart chart, IEnumerable<EditorEvent> events)
	{
		ActionQueue.Instance.Do(new ActionChangeNoteType(Editor, chart, events,
			e => e is EditorMineNoteEvent or EditorLiftNoteEvent,
			e =>
			{
				if (e is EditorMineNoteEvent)
					return EditorEvent.CreateEvent(EventConfig.CreateLiftNoteConfig(e));
				return EditorEvent.CreateEvent(EventConfig.CreateMineConfig(e));
			},
			"Mines", "Lifts", true));
	}

	private void SwapHoldsAndRolls(EditorChart chart, IEnumerable<EditorEvent> events)
	{
		ActionQueue.Instance.Do(new ActionChangeNoteType(Editor, chart, events,
			e => e is EditorHoldNoteEvent,
			e =>
			{
				var roll = !((EditorHoldNoteEvent)e).IsRoll();
				return EditorEvent.CreateEvent(
					EventConfig.CreateHoldConfig(chart, e.GetRow(), e.GetLane(), e.GetPlayer(), e.GetRowDuration(),
						roll));
			},
			"Holds", "Rolls", true));
	}

	private void SwapHoldsAndTaps(EditorChart chart, IEnumerable<EditorEvent> events)
	{
		ActionQueue.Instance.Do(new ActionChangeNoteType(Editor, chart, events,
			e => (e is EditorHoldNoteEvent hn && !hn.IsRoll()) || e is EditorTapNoteEvent,
			e =>
			{
				if (e is EditorHoldNoteEvent)
					return EditorEvent.CreateEvent(EventConfig.CreateTapConfig(e));
				return EditorEvent.CreateEvent(
					EventConfig.CreateHoldConfig(chart, e.GetRow(), e.GetLane(), e.GetPlayer(), e.GetRowDuration(),
						false));
			},
			"Holds", "Taps", true)
		);
	}

	private void SwapHoldsAndMines(EditorChart chart, IEnumerable<EditorEvent> events)
	{
		ActionQueue.Instance.Do(new ActionChangeNoteType(Editor, chart, events,
			e => (e is EditorHoldNoteEvent hn && !hn.IsRoll()) || e is EditorMineNoteEvent,
			e =>
			{
				if (e is EditorHoldNoteEvent)
					return EditorEvent.CreateEvent(EventConfig.CreateMineConfig(e));
				return EditorEvent.CreateEvent(
					EventConfig.CreateHoldConfig(chart, e.GetRow(), e.GetLane(), e.GetPlayer(), e.GetRowDuration(),
						false));
			},
			"Holds", "Mines", true)
		);
	}

	private void SwapRollsAndTaps(EditorChart chart, IEnumerable<EditorEvent> events)
	{
		ActionQueue.Instance.Do(new ActionChangeNoteType(Editor, chart, events,
			e => (e is EditorHoldNoteEvent hn && hn.IsRoll()) || e is EditorTapNoteEvent,
			e =>
			{
				if (e is EditorHoldNoteEvent)
					return EditorEvent.CreateEvent(EventConfig.CreateTapConfig(e));
				return EditorEvent.CreateEvent(
					EventConfig.CreateHoldConfig(chart, e.GetRow(), e.GetLane(), e.GetPlayer(), e.GetRowDuration(),
						true));
			},
			"Rolls", "Taps", true)
		);
	}

	private void SwapRollsAndMines(EditorChart chart, IEnumerable<EditorEvent> events)
	{
		ActionQueue.Instance.Do(new ActionChangeNoteType(Editor, chart, events,
			e => (e is EditorHoldNoteEvent hn && hn.IsRoll()) || e is EditorMineNoteEvent,
			e =>
			{
				if (e is EditorHoldNoteEvent)
					return EditorEvent.CreateEvent(EventConfig.CreateMineConfig(e));
				return EditorEvent.CreateEvent(
					EventConfig.CreateHoldConfig(chart, e.GetRow(), e.GetLane(), e.GetPlayer(), e.GetRowDuration(),
						true));
			},
			"Rolls", "Mines", true)
		);
	}

	#endregion Private Convert Selection Functions

	#region Public Convert Selection Functions

	public void ConvertSelectedNotesToPlayer(int player)
	{
		if (!TryGetFocusedChartSelection(out var events) || events == null || !events.Any())
			return;
		ConvertNotesToPlayer(Editor.GetFocusedChart(), events, player);
	}

	public void SwapSelectedNotesBetweenPlayers(int playerA, int playerB)
	{
		if (!TryGetFocusedChartSelection(out var events) || events == null || !events.Any())
			return;
		SwapNotesBetweenPlayers(Editor.GetFocusedChart(), events, playerA, playerB);
	}

	public void ConvertSelectedTapsToMines()
	{
		if (!TryGetFocusedChartSelection(out var events) || events == null || !events.Any())
			return;
		ConvertTapsToMines(Editor.GetFocusedChart(), events);
	}

	public void ConvertSelectedTapsToFakes()
	{
		if (!TryGetFocusedChartSelection(out var events) || events == null || !events.Any())
			return;
		ConvertTapsToFakes(Editor.GetFocusedChart(), events);
	}

	public void ConvertSelectedTapsToLifts()
	{
		if (!TryGetFocusedChartSelection(out var events) || events == null || !events.Any())
			return;
		ConvertTapsToLifts(Editor.GetFocusedChart(), events);
	}

	public void ConvertSelectedMinesToTaps()
	{
		if (!TryGetFocusedChartSelection(out var events) || events == null || !events.Any())
			return;
		ConvertMinesToTaps(Editor.GetFocusedChart(), events);
	}

	public void ConvertSelectedMinesToFakes()
	{
		if (!TryGetFocusedChartSelection(out var events) || events == null || !events.Any())
			return;
		ConvertMinesToFakes(Editor.GetFocusedChart(), events);
	}

	public void ConvertSelectedMinesToLifts()
	{
		if (!TryGetFocusedChartSelection(out var events) || events == null || !events.Any())
			return;
		ConvertMinesToLifts(Editor.GetFocusedChart(), events);
	}

	public void ConvertSelectedFakesToTaps()
	{
		if (!TryGetFocusedChartSelection(out var events) || events == null || !events.Any())
			return;
		ConvertFakesToTaps(Editor.GetFocusedChart(), events);
	}

	public void ConvertSelectedLiftsToTaps()
	{
		if (!TryGetFocusedChartSelection(out var events) || events == null || !events.Any())
			return;
		ConvertLiftsToTaps(Editor.GetFocusedChart(), events);
	}

	public void ConvertSelectedHoldsToRolls()
	{
		if (!TryGetFocusedChartSelection(out var events) || events == null || !events.Any())
			return;
		ConvertHoldsToRolls(Editor.GetFocusedChart(), events);
	}

	public void ConvertSelectedHoldsToTaps()
	{
		if (!TryGetFocusedChartSelection(out var events) || events == null || !events.Any())
			return;
		ConvertHoldsToTaps(Editor.GetFocusedChart(), events);
	}

	public void ConvertSelectedHoldsToMines()
	{
		if (!TryGetFocusedChartSelection(out var events) || events == null || !events.Any())
			return;
		ConvertHoldsToMines(Editor.GetFocusedChart(), events);
	}

	public void ConvertSelectedRollsToHolds()
	{
		if (!TryGetFocusedChartSelection(out var events) || events == null || !events.Any())
			return;
		ConvertRollsToHolds(Editor.GetFocusedChart(), events);
	}

	public void ConvertSelectedRollsToTaps()
	{
		if (!TryGetFocusedChartSelection(out var events) || events == null || !events.Any())
			return;
		ConvertRollsToTaps(Editor.GetFocusedChart(), events);
	}

	public void ConvertSelectedRollsToMines()
	{
		if (!TryGetFocusedChartSelection(out var events) || events == null || !events.Any())
			return;
		ConvertRollsToMines(Editor.GetFocusedChart(), events);
	}

	public void ConvertSelectedWarpsToNegativeStops()
	{
		if (!TryGetFocusedChartSelection(out var events) || events == null || !events.Any())
			return;
		ConvertWarpsToNegativeStops(Editor.GetFocusedChart(), events, false);
	}

	public void ConvertSelectedNegativeStopsToWarps()
	{
		if (!TryGetFocusedChartSelection(out var events) || events == null || !events.Any())
			return;
		ConvertNegativeStopsToWarps(Editor.GetFocusedChart(), events, false);
	}

	public void SwapSelectedTapsAndMines()
	{
		if (!TryGetFocusedChartSelection(out var events) || events == null || !events.Any())
			return;
		SwapTapsAndMines(Editor.GetFocusedChart(), events);
	}

	public void SwapSelectedTapsAndFakes()
	{
		if (!TryGetFocusedChartSelection(out var events) || events == null || !events.Any())
			return;
		SwapTapsAndFakes(Editor.GetFocusedChart(), events);
	}

	public void SwapSelectedTapsAndLifts()
	{
		if (!TryGetFocusedChartSelection(out var events) || events == null || !events.Any())
			return;
		SwapTapsAndLifts(Editor.GetFocusedChart(), events);
	}

	public void SwapSelectedMinesAndFakes()
	{
		if (!TryGetFocusedChartSelection(out var events) || events == null || !events.Any())
			return;
		SwapMinesAndFakes(Editor.GetFocusedChart(), events);
	}

	public void SwapSelectedMinesAndLifts()
	{
		if (!TryGetFocusedChartSelection(out var events) || events == null || !events.Any())
			return;
		SwapMinesAndLifts(Editor.GetFocusedChart(), events);
	}

	public void SwapSelectedHoldsAndRolls()
	{
		if (!TryGetFocusedChartSelection(out var events) || events == null || !events.Any())
			return;
		SwapHoldsAndRolls(Editor.GetFocusedChart(), events);
	}

	public void SwapSelectedHoldsAndTaps()
	{
		if (!TryGetFocusedChartSelection(out var events) || events == null || !events.Any())
			return;
		SwapHoldsAndTaps(Editor.GetFocusedChart(), events);
	}

	public void SwapSelectedHoldsAndMines()
	{
		if (!TryGetFocusedChartSelection(out var events) || events == null || !events.Any())
			return;
		SwapHoldsAndMines(Editor.GetFocusedChart(), events);
	}

	public void SwapSelectedRollsAndTaps()
	{
		if (!TryGetFocusedChartSelection(out var events) || events == null || !events.Any())
			return;
		SwapRollsAndTaps(Editor.GetFocusedChart(), events);
	}

	public void SwapSelectedRollsAndMines()
	{
		if (!TryGetFocusedChartSelection(out var events) || events == null || !events.Any())
			return;
		SwapRollsAndMines(Editor.GetFocusedChart(), events);
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

			var multiPlayer = Editor.GetFocusedChart()?.IsMultiPlayer() ?? false;
			if (multiPlayer)
			{
				var numPlayers = Editor.GetFocusedChart().MaxPlayers;
				var currentPlayer = Editor.GetPlayer();

				ImGui.PushStyleColor(ImGuiCol.Text, ArrowGraphicManager.GetUIColorForPlayer(currentPlayer));
				if (ImGui.BeginMenu($"Current Player ({currentPlayer + 1})"))
				{
					ImGui.PopStyleColor();

					if (ImGui.MenuItem("Notes", UIControls.GetCommandString(p.SelectAllCurrentPlayerNotes)))
					{
						Editor.OnSelectAllForCurrentPlayer();
					}

					if (ImGui.MenuItem("Taps", UIControls.GetCommandString(p.SelectAllCurrentPlayerTaps)))
					{
						Editor.OnSelectAllForCurrentPlayer(e => e is EditorTapNoteEvent);
					}

					if (ImGui.MenuItem("Mines", UIControls.GetCommandString(p.SelectAllCurrentPlayerMines)))
					{
						Editor.OnSelectAllForCurrentPlayer(e => e is EditorMineNoteEvent);
					}

					if (ImGui.MenuItem("Fakes", UIControls.GetCommandString(p.SelectAllCurrentPlayerFakes)))
					{
						Editor.OnSelectAllForCurrentPlayer(e => e is EditorFakeNoteEvent);
					}

					if (ImGui.MenuItem("Lifts", UIControls.GetCommandString(p.SelectAllCurrentPlayerLifts)))
					{
						Editor.OnSelectAllForCurrentPlayer(e => e is EditorLiftNoteEvent);
					}

					if (ImGui.MenuItem("Holds", UIControls.GetCommandString(p.SelectAllCurrentPlayerHolds)))
					{
						Editor.OnSelectAllForCurrentPlayer(e => e is EditorHoldNoteEvent hn && !hn.IsRoll());
					}

					if (ImGui.MenuItem("Rolls", UIControls.GetCommandString(p.SelectAllCurrentPlayerRolls)))
					{
						Editor.OnSelectAllForCurrentPlayer(e => e is EditorHoldNoteEvent hn && hn.IsRoll());
					}

					if (ImGui.MenuItem("Holds and Rolls", UIControls.GetCommandString(p.SelectAllCurrentPlayerHoldsAndRolls)))
					{
						Editor.OnSelectAllForCurrentPlayer(e => e is EditorHoldNoteEvent);
					}

					ImGui.EndMenu();
				}
				else
				{
					ImGui.PopStyleColor();
				}

				ImGui.Separator();

				for (var i = 0; i < numPlayers; i++)
				{
					var player = i;
					ImGui.PushStyleColor(ImGuiCol.Text, ArrowGraphicManager.GetUIColorForPlayer(player));
					if (ImGui.BeginMenu($"Player {player + 1}"))
					{
						ImGui.PopStyleColor();
						if (ImGui.MenuItem("Notes"))
						{
							Editor.OnSelectAll(player);
						}

						if (ImGui.MenuItem("Taps"))
						{
							Editor.OnSelectAll(e => e is EditorTapNoteEvent && e.GetPlayer() == player);
						}

						if (ImGui.MenuItem("Mines"))
						{
							Editor.OnSelectAll(e => e is EditorMineNoteEvent && e.GetPlayer() == player);
						}

						if (ImGui.MenuItem("Fakes"))
						{
							Editor.OnSelectAll(e => e is EditorFakeNoteEvent && e.GetPlayer() == player);
						}

						if (ImGui.MenuItem("Lifts"))
						{
							Editor.OnSelectAll(e => e is EditorLiftNoteEvent && e.GetPlayer() == player);
						}

						if (ImGui.MenuItem("Holds"))
						{
							Editor.OnSelectAll(e => e is EditorHoldNoteEvent hn && !hn.IsRoll() && e.GetPlayer() == player);
						}

						if (ImGui.MenuItem("Rolls"))
						{
							Editor.OnSelectAll(e => e is EditorHoldNoteEvent hn && hn.IsRoll() && e.GetPlayer() == player);
						}

						if (ImGui.MenuItem("Holds and Rolls"))
						{
							Editor.OnSelectAll(e => e is EditorHoldNoteEvent && e.GetPlayer() == player);
						}

						ImGui.EndMenu();
					}
					else
					{
						ImGui.PopStyleColor();
					}
				}

				ImGui.Separator();
			}

			if (ImGui.MenuItem("Notes", UIControls.GetCommandString(p.SelectAllNotes)))
			{
				Editor.OnSelectAll();
			}

			if (ImGui.MenuItem("Taps", UIControls.GetCommandString(p.SelectAllTaps)))
			{
				Editor.OnSelectAll(e => e is EditorTapNoteEvent);
			}

			if (ImGui.MenuItem("Mines", UIControls.GetCommandString(p.SelectAllMines)))
			{
				Editor.OnSelectAll(e => e is EditorMineNoteEvent);
			}

			if (ImGui.MenuItem("Fakes", UIControls.GetCommandString(p.SelectAllFakes)))
			{
				Editor.OnSelectAll(e => e is EditorFakeNoteEvent);
			}

			if (ImGui.MenuItem("Lifts", UIControls.GetCommandString(p.SelectAllLifts)))
			{
				Editor.OnSelectAll(e => e is EditorLiftNoteEvent);
			}

			if (ImGui.MenuItem("Holds", UIControls.GetCommandString(p.SelectAllHolds)))
			{
				Editor.OnSelectAll(e => e is EditorHoldNoteEvent hn && !hn.IsRoll());
			}

			if (ImGui.MenuItem("Rolls", UIControls.GetCommandString(p.SelectAllRolls)))
			{
				Editor.OnSelectAll(e => e is EditorHoldNoteEvent hn && hn.IsRoll());
			}

			if (ImGui.MenuItem("Holds and Rolls", UIControls.GetCommandString(p.SelectAllHoldsAndRolls)))
			{
				Editor.OnSelectAll(e => e is EditorHoldNoteEvent);
			}

			if (ImGui.MenuItem("Miscellaneous Events", UIControls.GetCommandString(p.SelectAllMiscEvents)))
			{
				Editor.OnSelectAllAlt();
			}

			if (ImGui.MenuItem("Notes and Miscellaneous Events", UIControls.GetCommandString(p.SelectAll)))
			{
				Editor.OnSelectAllShift();
			}

			if (ImGui.MenuItem("Patterns", UIControls.GetCommandString(p.SelectAllPatterns)))
			{
				Editor.OnSelectAll(e => e is EditorPatternEvent);
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
			var hasAttackEvent = false;
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
					else if (currentEvent is EditorAttackEvent)
						hasAttackEvent = true;
					else if (currentEvent is EditorPatternEvent)
						hasPatternEvent = true;
				}
			}

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
			DrawAddEventMenuItem("Attack", p.AddEventAttack, !hasAttackEvent, UIAttackColorRGBA,
				EditorAttackEvent.EventShortDescription, row,
				() => CreateAttackEvent(row, currentRateAlteringEvent));
			DrawAddPatternMenuItem(row, hasPatternEvent);

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
			toolTipText += GetRowConflictText(name, row);
		}

		ToolTip(toolTipText);
	}

	private void DrawAddPatternMenuItem(int row, bool hasPatternEvent)
	{
		var p = Preferences.Instance.PreferencesKeyBinds;
		var chart = Editor.GetFocusedChart();
		var patternsNotSupported = chart == null || !chart.SupportsAutogenFeatures();
		var patternsDisabled = patternsNotSupported || hasPatternEvent;

		if (MenuItemWithColor("Pattern", UIControls.GetCommandString(p.AddEventPattern), !patternsDisabled, UIPatternColorRGBA))
		{
			AddValidatedEvent(CreatePatternEvent(row));
		}

		var toolTipText = EditorPatternEvent.EventShortDescription;

		if (patternsNotSupported)
		{
			if (chart == null)
			{
				toolTipText +=
					"\n\nPatterns are not supported in this chart.";
			}
			else
			{
				toolTipText +=
					$"\n\nPatterns are not supported in {GetPrettyEnumString(chart.ChartType)} charts.";
			}
		}
		else if (hasPatternEvent)
		{
			toolTipText += GetRowConflictText("Pattern", row);
		}

		ToolTip(toolTipText);
	}

	private static string GetRowConflictText(string name, int row)
	{
		return $"\n\nOnly one {name} event can be specified per row.\nThere is already a {name} specified on row {row}.";
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

	private EditorEvent CreateAttackEvent(int row, EditorRateAlteringEvent currentRateAlteringEvent)
	{
		var attackLength = currentRateAlteringEvent!.GetSecondsPerRow() * SMCommon.MaxValidDenominator;
		return EditorEvent.CreateEvent(EventConfig.CreateAttackConfig(Editor.GetFocusedChart(), row, attackLength));
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

		// Don't allow patterns in multiplayer charts
		var focusedChart = Editor.GetFocusedChart();
		if (focusedChart == null || !focusedChart.IsMultiPlayer())
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

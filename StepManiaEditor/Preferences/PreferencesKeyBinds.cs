using System.Collections.Generic;
using System.Text.Json.Serialization;
using Microsoft.Xna.Framework.Input;

internal sealed class PreferencesKeyBinds
{
	public const int NumLaneInputs = 10;

	// @formatter:off
	private static readonly List<Keys[]> DefaultOpen                                 = new() { new[] { Keys.LeftControl, Keys.O } };
	private static readonly List<Keys[]> DefaultOpenContainingFolder                 = new() { new[] { Keys.LeftControl, Keys.LeftShift, Keys.O } };
	private static readonly List<Keys[]> DefaultSaveAs                               = new() { new[] { Keys.LeftControl, Keys.LeftShift, Keys.S } };
	private static readonly List<Keys[]> DefaultSave                                 = new() { new[] { Keys.LeftControl, Keys.S } };
	private static readonly List<Keys[]> DefaultNew                                  = new() { new[] { Keys.LeftControl, Keys.N } };
	private static readonly List<Keys[]> DefaultReload                               = new() { new[] { Keys.LeftControl, Keys.R } };
	private static readonly List<Keys[]> DefaultUndo                                 = new() { new[] { Keys.LeftControl, Keys.Z } };
	private static readonly List<Keys[]> DefaultRedo                                 = new() { new[] { Keys.LeftControl, Keys.LeftShift, Keys.Z }, new[] { Keys.LeftControl, Keys.Y } };
	private static readonly List<Keys[]> DefaultSelectAllNotes                       = new() { new[] { Keys.LeftControl, Keys.A } };
	private static readonly List<Keys[]> DefaultSelectAllMiscEvents                  = new() { new[] { Keys.LeftControl, Keys.LeftAlt, Keys.A } };
	private static readonly List<Keys[]> DefaultSelectAll                            = new() { new[] { Keys.LeftControl, Keys.LeftShift, Keys.A } };
	private static readonly List<Keys[]> DefaultCopy                                 = new() { new[] { Keys.LeftControl, Keys.C } };
	private static readonly List<Keys[]> DefaultCut                                  = new() { new[] { Keys.LeftControl, Keys.X } };
	private static readonly List<Keys[]> DefaultPaste                                = new() { new[] { Keys.LeftControl, Keys.V } };
	private static readonly List<Keys[]> DefaultTogglePreview                        = new() { new[] { Keys.P } };
	private static readonly List<Keys[]> DefaultToggleAssistTick                     = new() { new[] { Keys.A } };
	private static readonly List<Keys[]> DefaultToggleBeatTick                       = new() { new[] { Keys.B } };
	private static readonly List<Keys[]> DefaultDecreaseMusicRate                    = new() { new[] { Keys.LeftShift, Keys.Left } };
	private static readonly List<Keys[]> DefaultIncreaseMusicRate                    = new() { new[] { Keys.LeftShift, Keys.Right } };
	private static readonly List<Keys[]> DefaultPlayPause                            = new() { new[] { Keys.Space } };
	private static readonly List<Keys[]> DefaultCancelGoBack                         = new() { new[] { Keys.Escape } };
	private static readonly List<Keys[]> DefaultToggleNoteEntryMode                  = new() { new[] { Keys.M } };
	private static readonly List<Keys[]> DefaultToggleSpacingMode                    = new() { new[] { Keys.S } };
	private static readonly List<Keys[]> DefaultOpenPreviousChart                    = new() { new[] { Keys.LeftControl, Keys.LeftAlt, Keys.Left } };
	private static readonly List<Keys[]> DefaultOpenNextChart                        = new() { new[] { Keys.LeftControl, Keys.LeftAlt, Keys.Right } };
	private static readonly List<Keys[]> DefaultCloseFocusedChart                    = new() { new[] { Keys.LeftControl, Keys.F4 }, new[] { Keys.LeftControl, Keys.W } };
	private static readonly List<Keys[]> DefaultKeepChartOpen                        = new() { new[] { Keys.LeftControl, Keys.LeftAlt, Keys.Home } };
	private static readonly List<Keys[]> DefaultMoveFocusedChartLeft                 = new() { new[] { Keys.LeftControl, Keys.LeftAlt, Keys.PageUp } };
	private static readonly List<Keys[]> DefaultMoveFocusedChartRight                = new() { new[] { Keys.LeftControl, Keys.LeftAlt, Keys.PageDown } };
	private static readonly List<Keys[]> DefaultFocusPreviousChart                   = new() { new[] { Keys.LeftControl, Keys.PageUp } };
	private static readonly List<Keys[]> DefaultFocusNextChart                       = new() { new[] { Keys.LeftControl, Keys.PageDown } };
	private static readonly List<Keys[]> DefaultDecreaseSnap                         = new() { new[] { Keys.Left } };
	private static readonly List<Keys[]> DefaultIncreaseSnap                         = new() { new[] { Keys.Right } };
	private static readonly List<Keys[]> DefaultMoveUp                               = new() { new[] { Keys.Up } };
	private static readonly List<Keys[]> DefaultMoveDown                             = new() { new[] { Keys.Down } };
	private static readonly List<Keys[]> DefaultMoveToPreviousMeasure                = new() { new[] { Keys.PageUp } };
	private static readonly List<Keys[]> DefaultMoveToNextMeasure                    = new() { new[] { Keys.PageDown } };
	private static readonly List<Keys[]> DefaultMoveToChartStart                     = new() { new[] { Keys.Home } };
	private static readonly List<Keys[]> DefaultMoveToChartEnd                       = new() { new[] { Keys.End } };
	private static readonly List<Keys[]> DefaultMoveToNextLabel                      = new() { new[] { Keys.LeftControl, Keys.L } };
	private static readonly List<Keys[]> DefaultMoveToPreviousLabel                  = new() { new[] { Keys.LeftControl, Keys.LeftShift, Keys.L } };
	private static readonly List<Keys[]> DefaultMoveToNextPattern                    = new() { new[] { Keys.LeftControl, Keys.P } };
	private static readonly List<Keys[]> DefaultMoveToPreviousPattern                = new() { new[] { Keys.LeftControl, Keys.LeftShift, Keys.P } };
	private static readonly List<Keys[]> DefaultRegenerateAllPatternsFixedSeeds      = new() { new[] { Keys.LeftAlt, Keys.P } };
	private static readonly List<Keys[]> DefaultRegenerateAllPatternsNewSeeds        = new() { new[] { Keys.LeftAlt, Keys.LeftShift, Keys.P } };
	private static readonly List<Keys[]> DefaultRegenerateSelectedPatternsFixedSeeds = new() { new[] { Keys.LeftControl, Keys.LeftAlt, Keys.P } };
	private static readonly List<Keys[]> DefaultRegenerateSelectedPatternsNewSeeds   = new() { new[] { Keys.LeftControl, Keys.LeftAlt, Keys.LeftShift, Keys.P } };
	private static readonly List<Keys[]> DefaultDelete                               = new() { new[] { Keys.Delete } };
	private static readonly List<Keys[]> DefaultShiftLeft                            = new() { new[] { Keys.LeftControl, Keys.LeftShift, Keys.LeftAlt, Keys.Left } };
	private static readonly List<Keys[]> DefaultShiftLeftAndWrap                     = new() { new[] { Keys.LeftControl, Keys.LeftShift, Keys.Left } };
	private static readonly List<Keys[]> DefaultShiftRight                           = new() { new[] { Keys.LeftControl, Keys.LeftShift, Keys.LeftAlt, Keys.Right } };
	private static readonly List<Keys[]> DefaultShiftRightAndWrap                    = new() { new[] { Keys.LeftControl, Keys.LeftShift, Keys.Right } };
	private static readonly List<Keys[]> DefaultShiftEarlier                         = new() { new[] { Keys.LeftControl, Keys.LeftShift, Keys.Up } };
	private static readonly List<Keys[]> DefaultShiftLater                           = new() { new[] { Keys.LeftControl, Keys.LeftShift, Keys.Down } };
	private static readonly List<Keys[]> DefaultArrow0                               = new() { new[] { Keys.D1 } };
	private static readonly List<Keys[]> DefaultArrow1                               = new() { new[] { Keys.D2 } };
	private static readonly List<Keys[]> DefaultArrow2                               = new() { new[] { Keys.D3 } };
	private static readonly List<Keys[]> DefaultArrow3                               = new() { new[] { Keys.D4 } };
	private static readonly List<Keys[]> DefaultArrow4                               = new() { new[] { Keys.D5 } };
	private static readonly List<Keys[]> DefaultArrow5                               = new() { new[] { Keys.D6 } };
	private static readonly List<Keys[]> DefaultArrow6                               = new() { new[] { Keys.D7 } };
	private static readonly List<Keys[]> DefaultArrow7                               = new() { new[] { Keys.D8 } };
	private static readonly List<Keys[]> DefaultArrow8                               = new() { new[] { Keys.D9 } };
	private static readonly List<Keys[]> DefaultArrow9                               = new() { new[] { Keys.D0 } };
	private static readonly List<Keys[]> DefaultArrowModification                    = new() { new[] { Keys.LeftShift } };
	private static readonly List<Keys[]> DefaultScrollZoom                           = new() { new[] { Keys.LeftControl } };
	private static readonly List<Keys[]> DefaultScrollSpacing                        = new() { new[] { Keys.LeftShift } };
	private static readonly List<Keys[]> DefaultMouseSelectionControlBehavior        = new() { new[] { Keys.LeftControl } };
	private static readonly List<Keys[]> DefaultMouseSelectionShiftBehavior          = new() { new[] { Keys.LeftShift } };
	private static readonly List<Keys[]> DefaultMouseSelectionAltBehavior            = new() { new[] { Keys.LeftAlt } };
	private static readonly List<Keys[]> DefaultLockReceptorMoveAxis                 = new() { new[] { Keys.LeftShift } };
	// @formatter:on

	[JsonInclude] public List<Keys[]> Open = DefaultOpen;
	[JsonInclude] public List<Keys[]> OpenContainingFolder = DefaultOpenContainingFolder;
	[JsonInclude] public List<Keys[]> SaveAs = DefaultSaveAs;
	[JsonInclude] public List<Keys[]> Save = DefaultSave;
	[JsonInclude] public List<Keys[]> New = DefaultNew;
	[JsonInclude] public List<Keys[]> Reload = DefaultReload;
	[JsonInclude] public List<Keys[]> Undo = DefaultUndo;
	[JsonInclude] public List<Keys[]> Redo = DefaultRedo;
	[JsonInclude] public List<Keys[]> SelectAllNotes = DefaultSelectAllNotes;
	[JsonInclude] public List<Keys[]> SelectAllMiscEvents = DefaultSelectAllMiscEvents;
	[JsonInclude] public List<Keys[]> SelectAll = DefaultSelectAll;
	[JsonInclude] public List<Keys[]> Copy = DefaultCopy;
	[JsonInclude] public List<Keys[]> Cut = DefaultCut;
	[JsonInclude] public List<Keys[]> Paste = DefaultPaste;
	[JsonInclude] public List<Keys[]> TogglePreview = DefaultTogglePreview;
	[JsonInclude] public List<Keys[]> ToggleAssistTick = DefaultToggleAssistTick;
	[JsonInclude] public List<Keys[]> ToggleBeatTick = DefaultToggleBeatTick;
	[JsonInclude] public List<Keys[]> DecreaseMusicRate = DefaultDecreaseMusicRate;
	[JsonInclude] public List<Keys[]> IncreaseMusicRate = DefaultIncreaseMusicRate;
	[JsonInclude] public List<Keys[]> PlayPause = DefaultPlayPause;
	[JsonInclude] public List<Keys[]> CancelGoBack = DefaultCancelGoBack;
	[JsonInclude] public List<Keys[]> ToggleNoteEntryMode = DefaultToggleNoteEntryMode;
	[JsonInclude] public List<Keys[]> ToggleSpacingMode = DefaultToggleSpacingMode;
	[JsonInclude] public List<Keys[]> OpenPreviousChart = DefaultOpenPreviousChart;
	[JsonInclude] public List<Keys[]> OpenNextChart = DefaultOpenNextChart;
	[JsonInclude] public List<Keys[]> CloseFocusedChart = DefaultCloseFocusedChart;
	[JsonInclude] public List<Keys[]> KeepChartOpen = DefaultKeepChartOpen;
	[JsonInclude] public List<Keys[]> MoveFocusedChartLeft = DefaultMoveFocusedChartLeft;
	[JsonInclude] public List<Keys[]> MoveFocusedChartRight = DefaultMoveFocusedChartRight;
	[JsonInclude] public List<Keys[]> FocusPreviousChart = DefaultFocusPreviousChart;
	[JsonInclude] public List<Keys[]> FocusNextChart = DefaultFocusNextChart;
	[JsonInclude] public List<Keys[]> DecreaseSnap = DefaultDecreaseSnap;
	[JsonInclude] public List<Keys[]> IncreaseSnap = DefaultIncreaseSnap;
	[JsonInclude] public List<Keys[]> MoveUp = DefaultMoveUp;
	[JsonInclude] public List<Keys[]> MoveDown = DefaultMoveDown;
	[JsonInclude] public List<Keys[]> MoveToPreviousMeasure = DefaultMoveToPreviousMeasure;
	[JsonInclude] public List<Keys[]> MoveToNextMeasure = DefaultMoveToNextMeasure;
	[JsonInclude] public List<Keys[]> MoveToChartStart = DefaultMoveToChartStart;
	[JsonInclude] public List<Keys[]> MoveToChartEnd = DefaultMoveToChartEnd;
	[JsonInclude] public List<Keys[]> MoveToNextLabel = DefaultMoveToNextLabel;
	[JsonInclude] public List<Keys[]> MoveToPreviousLabel = DefaultMoveToPreviousLabel;
	[JsonInclude] public List<Keys[]> MoveToNextPattern = DefaultMoveToNextPattern;
	[JsonInclude] public List<Keys[]> MoveToPreviousPattern = DefaultMoveToPreviousPattern;
	[JsonInclude] public List<Keys[]> RegenerateAllPatternsFixedSeeds = DefaultRegenerateAllPatternsFixedSeeds;
	[JsonInclude] public List<Keys[]> RegenerateAllPatternsNewSeeds = DefaultRegenerateAllPatternsNewSeeds;
	[JsonInclude] public List<Keys[]> RegenerateSelectedPatternsFixedSeeds = DefaultRegenerateSelectedPatternsFixedSeeds;
	[JsonInclude] public List<Keys[]> RegenerateSelectedPatternsNewSeeds = DefaultRegenerateSelectedPatternsNewSeeds;
	[JsonInclude] public List<Keys[]> Delete = DefaultDelete;
	[JsonInclude] public List<Keys[]> ShiftLeft = DefaultShiftLeft;
	[JsonInclude] public List<Keys[]> ShiftLeftAndWrap = DefaultShiftLeftAndWrap;
	[JsonInclude] public List<Keys[]> ShiftRight = DefaultShiftRight;
	[JsonInclude] public List<Keys[]> ShiftRightAndWrap = DefaultShiftRightAndWrap;
	[JsonInclude] public List<Keys[]> ShiftEarlier = DefaultShiftEarlier;
	[JsonInclude] public List<Keys[]> ShiftLater = DefaultShiftLater;
	[JsonInclude] public List<Keys[]> Arrow0 = DefaultArrow0;
	[JsonInclude] public List<Keys[]> Arrow1 = DefaultArrow1;
	[JsonInclude] public List<Keys[]> Arrow2 = DefaultArrow2;
	[JsonInclude] public List<Keys[]> Arrow3 = DefaultArrow3;
	[JsonInclude] public List<Keys[]> Arrow4 = DefaultArrow4;
	[JsonInclude] public List<Keys[]> Arrow5 = DefaultArrow5;
	[JsonInclude] public List<Keys[]> Arrow6 = DefaultArrow6;
	[JsonInclude] public List<Keys[]> Arrow7 = DefaultArrow7;
	[JsonInclude] public List<Keys[]> Arrow8 = DefaultArrow8;
	[JsonInclude] public List<Keys[]> Arrow9 = DefaultArrow9;
	[JsonInclude] public List<Keys[]> ArrowModification = DefaultArrowModification;
	[JsonInclude] public List<Keys[]> ScrollZoom = DefaultScrollZoom;
	[JsonInclude] public List<Keys[]> ScrollSpacing = DefaultScrollSpacing;
	[JsonInclude] public List<Keys[]> MouseSelectionControlBehavior = DefaultMouseSelectionControlBehavior;
	[JsonInclude] public List<Keys[]> MouseSelectionShiftBehavior = DefaultMouseSelectionShiftBehavior;
	[JsonInclude] public List<Keys[]> MouseSelectionAltBehavior = DefaultMouseSelectionAltBehavior;
	[JsonInclude] public List<Keys[]> LockReceptorMoveAxis = DefaultLockReceptorMoveAxis;


	public List<Keys[]> GetArrowInputs(int laneIndex)
	{
		switch (laneIndex)
		{
			case 0: return Arrow0;
			case 1: return Arrow1;
			case 2: return Arrow2;
			case 3: return Arrow3;
			case 4: return Arrow4;
			case 5: return Arrow5;
			case 6: return Arrow6;
			case 7: return Arrow7;
			case 8: return Arrow8;
			case 9: return Arrow9;
		}

		return null;
	}
}

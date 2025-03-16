using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
using Fumen;
using Microsoft.Xna.Framework.Input;

/// <summary>
/// Key binding preferences.
/// Key bindings are identified by the property name.
/// </summary>
internal sealed class PreferencesKeyBinds : Notifier<PreferencesKeyBinds>
{
	private const string DefaultFieldNamePrefix = "Default";
	public const string NotificationKeyBindingChanged = "KeyBindingChanged";

	#region Valid Keys

	/// <summary>
	/// All valid keys.
	/// Some keyboards have unexpected state for misc OEM keys.
	/// If we allow these then rebinding appears to have them stuck.
	/// It is better to limit the allowed keys to sensible values and avoid this.
	/// </summary>
	private static readonly bool[] ValidKeys = new bool[0xFF];

	static PreferencesKeyBinds()
	{
		ValidKeys[(int)Keys.Back] = true;
		ValidKeys[(int)Keys.Tab] = true;
		ValidKeys[(int)Keys.Enter] = true;
		ValidKeys[(int)Keys.Escape] = true;
		ValidKeys[(int)Keys.Space] = true;
		ValidKeys[(int)Keys.PageUp] = true;
		ValidKeys[(int)Keys.PageDown] = true;
		ValidKeys[(int)Keys.End] = true;
		ValidKeys[(int)Keys.Home] = true;
		ValidKeys[(int)Keys.Left] = true;
		ValidKeys[(int)Keys.Up] = true;
		ValidKeys[(int)Keys.Right] = true;
		ValidKeys[(int)Keys.Down] = true;
		ValidKeys[(int)Keys.Select] = true;
		ValidKeys[(int)Keys.Print] = true;
		ValidKeys[(int)Keys.Execute] = true;
		ValidKeys[(int)Keys.PrintScreen] = true;
		ValidKeys[(int)Keys.Insert] = true;
		ValidKeys[(int)Keys.Delete] = true;
		ValidKeys[(int)Keys.Help] = true;
		ValidKeys[(int)Keys.D0] = true;
		ValidKeys[(int)Keys.D1] = true;
		ValidKeys[(int)Keys.D2] = true;
		ValidKeys[(int)Keys.D3] = true;
		ValidKeys[(int)Keys.D4] = true;
		ValidKeys[(int)Keys.D5] = true;
		ValidKeys[(int)Keys.D6] = true;
		ValidKeys[(int)Keys.D7] = true;
		ValidKeys[(int)Keys.D8] = true;
		ValidKeys[(int)Keys.D9] = true;
		ValidKeys[(int)Keys.A] = true;
		ValidKeys[(int)Keys.B] = true;
		ValidKeys[(int)Keys.C] = true;
		ValidKeys[(int)Keys.D] = true;
		ValidKeys[(int)Keys.E] = true;
		ValidKeys[(int)Keys.F] = true;
		ValidKeys[(int)Keys.G] = true;
		ValidKeys[(int)Keys.H] = true;
		ValidKeys[(int)Keys.I] = true;
		ValidKeys[(int)Keys.J] = true;
		ValidKeys[(int)Keys.K] = true;
		ValidKeys[(int)Keys.L] = true;
		ValidKeys[(int)Keys.M] = true;
		ValidKeys[(int)Keys.N] = true;
		ValidKeys[(int)Keys.O] = true;
		ValidKeys[(int)Keys.P] = true;
		ValidKeys[(int)Keys.Q] = true;
		ValidKeys[(int)Keys.R] = true;
		ValidKeys[(int)Keys.S] = true;
		ValidKeys[(int)Keys.T] = true;
		ValidKeys[(int)Keys.U] = true;
		ValidKeys[(int)Keys.V] = true;
		ValidKeys[(int)Keys.W] = true;
		ValidKeys[(int)Keys.X] = true;
		ValidKeys[(int)Keys.Y] = true;
		ValidKeys[(int)Keys.Z] = true;
		ValidKeys[(int)Keys.LeftWindows] = true;
		ValidKeys[(int)Keys.RightWindows] = true;
		ValidKeys[(int)Keys.NumPad0] = true;
		ValidKeys[(int)Keys.NumPad1] = true;
		ValidKeys[(int)Keys.NumPad2] = true;
		ValidKeys[(int)Keys.NumPad3] = true;
		ValidKeys[(int)Keys.NumPad4] = true;
		ValidKeys[(int)Keys.NumPad5] = true;
		ValidKeys[(int)Keys.NumPad6] = true;
		ValidKeys[(int)Keys.NumPad7] = true;
		ValidKeys[(int)Keys.NumPad8] = true;
		ValidKeys[(int)Keys.NumPad9] = true;
		ValidKeys[(int)Keys.Multiply] = true;
		ValidKeys[(int)Keys.Add] = true;
		ValidKeys[(int)Keys.Separator] = true;
		ValidKeys[(int)Keys.Subtract] = true;
		ValidKeys[(int)Keys.Decimal] = true;
		ValidKeys[(int)Keys.Divide] = true;
		ValidKeys[(int)Keys.F1] = true;
		ValidKeys[(int)Keys.F2] = true;
		ValidKeys[(int)Keys.F3] = true;
		ValidKeys[(int)Keys.F4] = true;
		ValidKeys[(int)Keys.F5] = true;
		ValidKeys[(int)Keys.F6] = true;
		ValidKeys[(int)Keys.F7] = true;
		ValidKeys[(int)Keys.F8] = true;
		ValidKeys[(int)Keys.F9] = true;
		ValidKeys[(int)Keys.F10] = true;
		ValidKeys[(int)Keys.F11] = true;
		ValidKeys[(int)Keys.F12] = true;
		ValidKeys[(int)Keys.F13] = true;
		ValidKeys[(int)Keys.F14] = true;
		ValidKeys[(int)Keys.F15] = true;
		ValidKeys[(int)Keys.F16] = true;
		ValidKeys[(int)Keys.F17] = true;
		ValidKeys[(int)Keys.F18] = true;
		ValidKeys[(int)Keys.F19] = true;
		ValidKeys[(int)Keys.F20] = true;
		ValidKeys[(int)Keys.F21] = true;
		ValidKeys[(int)Keys.F22] = true;
		ValidKeys[(int)Keys.F23] = true;
		ValidKeys[(int)Keys.F24] = true;
		ValidKeys[(int)Keys.NumLock] = true;
		ValidKeys[(int)Keys.Scroll] = true;
		ValidKeys[(int)Keys.LeftShift] = true;
		ValidKeys[(int)Keys.RightShift] = true;
		ValidKeys[(int)Keys.LeftControl] = true;
		ValidKeys[(int)Keys.RightControl] = true;
		ValidKeys[(int)Keys.LeftAlt] = true;
		ValidKeys[(int)Keys.RightAlt] = true;
		ValidKeys[(int)Keys.VolumeMute] = true;
		ValidKeys[(int)Keys.VolumeDown] = true;
		ValidKeys[(int)Keys.VolumeUp] = true;
		ValidKeys[(int)Keys.MediaNextTrack] = true;
		ValidKeys[(int)Keys.MediaPreviousTrack] = true;
		ValidKeys[(int)Keys.MediaStop] = true;
		ValidKeys[(int)Keys.MediaPlayPause] = true;
		ValidKeys[(int)Keys.SelectMedia] = true;
		ValidKeys[(int)Keys.OemSemicolon] = true;
		ValidKeys[(int)Keys.OemPlus] = true;
		ValidKeys[(int)Keys.OemComma] = true;
		ValidKeys[(int)Keys.OemMinus] = true;
		ValidKeys[(int)Keys.OemPeriod] = true;
		ValidKeys[(int)Keys.OemQuestion] = true;
		ValidKeys[(int)Keys.OemTilde] = true;
		ValidKeys[(int)Keys.OemOpenBrackets] = true;
		ValidKeys[(int)Keys.OemPipe] = true;
		ValidKeys[(int)Keys.OemCloseBrackets] = true;
		ValidKeys[(int)Keys.OemQuotes] = true;
		ValidKeys[(int)Keys.OemBackslash] = true;
		ValidKeys[(int)Keys.Play] = true;
		ValidKeys[(int)Keys.Pause] = true;
	}

	public static bool IsValidKeyForBinding(Keys key)
	{
		return ValidKeys[(int)key];
	}

	#endregion Valid Keys

	// @formatter:off
	private static readonly Keys Ctrl = RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? Keys.LeftWindows : Keys.LeftControl;
	private static readonly List<Keys[]> DefaultOpen                                 = [[Ctrl, Keys.O]];
	private static readonly List<Keys[]> DefaultOpenContainingFolder                 = [[Ctrl, Keys.LeftShift, Keys.O]];
	private static readonly List<Keys[]> DefaultSaveAs                               = [[Ctrl, Keys.LeftShift, Keys.S]];
	private static readonly List<Keys[]> DefaultSave                                 = [[Ctrl, Keys.S]];
	private static readonly List<Keys[]> DefaultNew                                  = [[Ctrl, Keys.N]];
	private static readonly List<Keys[]> DefaultReload                               = [[Ctrl, Keys.R]];
	private static readonly List<Keys[]> DefaultClose                                = [[Ctrl, Keys.LeftShift, Keys.F4], [Ctrl, Keys.LeftShift, Keys.W]];
	private static readonly List<Keys[]> DefaultUndo                                 = [[Ctrl, Keys.Z]];
	private static readonly List<Keys[]> DefaultRedo                                 = [[Ctrl, Keys.LeftShift, Keys.Z], [Ctrl, Keys.Y]];
	private static readonly List<Keys[]> DefaultSelectAllNotes                       = [[Ctrl, Keys.A]];
	private static readonly List<Keys[]> DefaultSelectAllTaps                        = [Array.Empty<Keys>()];
	private static readonly List<Keys[]> DefaultSelectAllMines                       = [Array.Empty<Keys>()];
	private static readonly List<Keys[]> DefaultSelectAllFakes                       = [Array.Empty<Keys>()];
	private static readonly List<Keys[]> DefaultSelectAllLifts                       = [Array.Empty<Keys>()];
	private static readonly List<Keys[]> DefaultSelectAllHolds                       = [Array.Empty<Keys>()];
	private static readonly List<Keys[]> DefaultSelectAllRolls                       = [Array.Empty<Keys>()];
	private static readonly List<Keys[]> DefaultSelectAllHoldsAndRolls               = [Array.Empty<Keys>()];
	private static readonly List<Keys[]> DefaultSelectAllCurrentPlayerNotes          = [Array.Empty<Keys>()];
	private static readonly List<Keys[]> DefaultSelectAllCurrentPlayerTaps           = [Array.Empty<Keys>()];
	private static readonly List<Keys[]> DefaultSelectAllCurrentPlayerMines          = [Array.Empty<Keys>()];
	private static readonly List<Keys[]> DefaultSelectAllCurrentPlayerFakes          = [Array.Empty<Keys>()];
	private static readonly List<Keys[]> DefaultSelectAllCurrentPlayerLifts          = [Array.Empty<Keys>()];
	private static readonly List<Keys[]> DefaultSelectAllCurrentPlayerHolds          = [Array.Empty<Keys>()];
	private static readonly List<Keys[]> DefaultSelectAllCurrentPlayerRolls          = [Array.Empty<Keys>()];
	private static readonly List<Keys[]> DefaultSelectAllCurrentPlayerHoldsAndRolls  = [Array.Empty<Keys>()];
	private static readonly List<Keys[]> DefaultSelectAllMiscEvents                  = [[Ctrl, Keys.LeftAlt, Keys.A]];
	private static readonly List<Keys[]> DefaultSelectAll                            = [[Ctrl, Keys.LeftShift, Keys.A]];
	private static readonly List<Keys[]> DefaultSelectAllPatterns                    = [Array.Empty<Keys>()];
	private static readonly List<Keys[]> DefaultCopy                                 = [[Ctrl, Keys.C]];
	private static readonly List<Keys[]> DefaultCut                                  = [[Ctrl, Keys.X]];
	private static readonly List<Keys[]> DefaultPaste                                = [[Ctrl, Keys.V]];
	private static readonly List<Keys[]> DefaultTogglePreview                        = [[Keys.P]];
	private static readonly List<Keys[]> DefaultToggleAssistTick                     = [[Keys.A]];
	private static readonly List<Keys[]> DefaultToggleBeatTick                       = [[Keys.B]];
	private static readonly List<Keys[]> DefaultDecreaseMusicRate                    = [[Keys.LeftShift, Keys.Left]];
	private static readonly List<Keys[]> DefaultIncreaseMusicRate                    = [[Keys.LeftShift, Keys.Right]];
	private static readonly List<Keys[]> DefaultPlayPause                            = [[Keys.Space]];
	private static readonly List<Keys[]> DefaultCancelGoBack                         = [[Keys.Escape]];
	private static readonly List<Keys[]> DefaultToggleNoteEntryMode                  = [[Keys.M]];
	private static readonly List<Keys[]> DefaultToggleSpacingMode                    = [[Keys.S]];
	private static readonly List<Keys[]> DefaultTogglePlayer                         = [[Keys.OemQuestion]];
	private static readonly List<Keys[]> DefaultSetPlayer1                           = [Array.Empty<Keys>()];
	private static readonly List<Keys[]> DefaultSetPlayer2                           = [Array.Empty<Keys>()];
	private static readonly List<Keys[]> DefaultSetPlayer3                           = [Array.Empty<Keys>()];
	private static readonly List<Keys[]> DefaultSetPlayer4                           = [Array.Empty<Keys>()];
	private static readonly List<Keys[]> DefaultConvertSelectedNotesToPlayer1        = [Array.Empty<Keys>()];
	private static readonly List<Keys[]> DefaultConvertSelectedNotesToPlayer2        = [Array.Empty<Keys>()];
	private static readonly List<Keys[]> DefaultConvertSelectedNotesToPlayer3        = [Array.Empty<Keys>()];
	private static readonly List<Keys[]> DefaultConvertSelectedNotesToPlayer4        = [Array.Empty<Keys>()];
	private static readonly List<Keys[]> DefaultOpenPreviousChart                    = [[Ctrl, Keys.LeftAlt, Keys.Left]];
	private static readonly List<Keys[]> DefaultOpenNextChart                        = [[Ctrl, Keys.LeftAlt, Keys.Right]];
	private static readonly List<Keys[]> DefaultCloseFocusedChart                    = [[Ctrl, Keys.F4], [Ctrl, Keys.W]];
	private static readonly List<Keys[]> DefaultKeepChartOpen                        = [[Ctrl, Keys.LeftAlt, Keys.Home]];
	private static readonly List<Keys[]> DefaultMoveFocusedChartLeft                 = [[Ctrl, Keys.LeftAlt, Keys.PageUp]];
	private static readonly List<Keys[]> DefaultMoveFocusedChartRight                = [[Ctrl, Keys.LeftAlt, Keys.PageDown]];
	private static readonly List<Keys[]> DefaultFocusPreviousChart                   = [[Ctrl, Keys.PageUp]];
	private static readonly List<Keys[]> DefaultFocusNextChart                       = [[Ctrl, Keys.PageDown]];
	private static readonly List<Keys[]> DefaultDecreaseSnap                         = [[Keys.Left]];
	private static readonly List<Keys[]> DefaultIncreaseSnap                         = [[Keys.Right]];
	private static readonly List<Keys[]> DefaultMoveUp                               = [[Keys.Up]];
	private static readonly List<Keys[]> DefaultMoveDown                             = [[Keys.Down]];
	private static readonly List<Keys[]> DefaultMoveToPreviousRowWithSteps           = [Array.Empty<Keys>()];
	private static readonly List<Keys[]> DefaultMoveToNextRowWithSteps               = [Array.Empty<Keys>()];
	private static readonly List<Keys[]> DefaultMoveToPreviousRowWithEvent           = [Array.Empty<Keys>()];
	private static readonly List<Keys[]> DefaultMoveToNextRowWithEvent               = [Array.Empty<Keys>()];
	private static readonly List<Keys[]> DefaultMoveToStartOfStream                  = [[Ctrl, Keys.Up]];
	private static readonly List<Keys[]> DefaultMoveToEndOfStream                    = [[Ctrl, Keys.Down]];
	private static readonly List<Keys[]> DefaultMoveToPreviousMeasure                = [[Keys.PageUp]];
	private static readonly List<Keys[]> DefaultMoveToNextMeasure                    = [[Keys.PageDown]];
	private static readonly List<Keys[]> DefaultMoveToChartStart                     = [[Keys.Home]];
	private static readonly List<Keys[]> DefaultMoveToChartEnd                       = [[Keys.End]];
	private static readonly List<Keys[]> DefaultMoveToNextLabel                      = [[Ctrl, Keys.L]];
	private static readonly List<Keys[]> DefaultMoveToPreviousLabel                  = [[Ctrl, Keys.LeftShift, Keys.L]];
	private static readonly List<Keys[]> DefaultMoveToNextPattern                    = [[Ctrl, Keys.P]];
	private static readonly List<Keys[]> DefaultMoveToPreviousPattern                = [[Ctrl, Keys.LeftShift, Keys.P]];
	private static readonly List<Keys[]> DefaultRegenerateAllPatternsFixedSeeds      = [[Keys.LeftAlt, Keys.P]];
	private static readonly List<Keys[]> DefaultRegenerateAllPatternsNewSeeds        = [[Keys.LeftAlt, Keys.LeftShift, Keys.P]];
	private static readonly List<Keys[]> DefaultRegenerateSelectedPatternsFixedSeeds = [[Ctrl, Keys.LeftAlt, Keys.P]];
	private static readonly List<Keys[]> DefaultRegenerateSelectedPatternsNewSeeds   = [[Ctrl, Keys.LeftAlt, Keys.LeftShift, Keys.P]];
	private static readonly List<Keys[]> DefaultDelete                               = [[Keys.Delete]];
	private static readonly List<Keys[]> DefaultShiftLeft                            = [[Ctrl, Keys.LeftShift, Keys.LeftAlt, Keys.Left]];
	private static readonly List<Keys[]> DefaultShiftLeftAndWrap                     = [[Ctrl, Keys.LeftShift, Keys.Left]];
	private static readonly List<Keys[]> DefaultShiftRight                           = [[Ctrl, Keys.LeftShift, Keys.LeftAlt, Keys.Right]];
	private static readonly List<Keys[]> DefaultShiftRightAndWrap                    = [[Ctrl, Keys.LeftShift, Keys.Right]];
	private static readonly List<Keys[]> DefaultShiftEarlier                         = [[Ctrl, Keys.LeftShift, Keys.Up]];
	private static readonly List<Keys[]> DefaultShiftLater                           = [[Ctrl, Keys.LeftShift, Keys.Down]];
	private static readonly List<Keys[]> DefaultMirror                               = [[Ctrl, Keys.LeftShift, Keys.M]];
	private static readonly List<Keys[]> DefaultFlip                                 = [[Ctrl, Keys.LeftShift, Keys.F]];
	private static readonly List<Keys[]> DefaultMirrorAndFlip                        = [Array.Empty<Keys>()];
	private static readonly List<Keys[]> DefaultArrow0                               = [[Keys.D1]];
	private static readonly List<Keys[]> DefaultArrow1                               = [[Keys.D2]];
	private static readonly List<Keys[]> DefaultArrow2                               = [[Keys.D3]];
	private static readonly List<Keys[]> DefaultArrow3                               = [[Keys.D4]];
	private static readonly List<Keys[]> DefaultArrow4                               = [[Keys.D5]];
	private static readonly List<Keys[]> DefaultArrow5                               = [[Keys.D6]];
	private static readonly List<Keys[]> DefaultArrow6                               = [[Keys.D7]];
	private static readonly List<Keys[]> DefaultArrow7                               = [[Keys.D8]];
	private static readonly List<Keys[]> DefaultArrow8                               = [[Keys.D9]];
	private static readonly List<Keys[]> DefaultArrow9                               = [[Keys.D0]];
	private static readonly List<Keys[]> DefaultArrowModification                    = [[Keys.LeftShift]];
	private static readonly List<Keys[]> DefaultScrollZoom                           = [[Ctrl]];
	private static readonly List<Keys[]> DefaultScrollSpacing                        = [[Keys.LeftShift]];
	private static readonly List<Keys[]> DefaultMouseSelectionControlBehavior        = [[Ctrl]];
	private static readonly List<Keys[]> DefaultMouseSelectionShiftBehavior          = [[Keys.LeftShift]];
	private static readonly List<Keys[]> DefaultMouseSelectionAltBehavior            = [[Keys.LeftAlt]];
	private static readonly List<Keys[]> DefaultLockReceptorMoveAxis                 = [[Keys.LeftShift]];
	private static readonly List<Keys[]> DefaultAddEventTempo                        = [Array.Empty<Keys>()];
	private static readonly List<Keys[]> DefaultAddEventInterpolatedScrollRate       = [Array.Empty<Keys>()];
	private static readonly List<Keys[]> DefaultAddEventScrollRate                   = [Array.Empty<Keys>()];
	private static readonly List<Keys[]> DefaultAddEventStop                         = [Array.Empty<Keys>()];
	private static readonly List<Keys[]> DefaultAddEventDelay                        = [Array.Empty<Keys>()];
	private static readonly List<Keys[]> DefaultAddEventWarp                         = [Array.Empty<Keys>()];
	private static readonly List<Keys[]> DefaultAddEventFakeRegion                   = [Array.Empty<Keys>()];
	private static readonly List<Keys[]> DefaultAddEventTicks                        = [Array.Empty<Keys>()];
	private static readonly List<Keys[]> DefaultAddEventComboMultipliers             = [Array.Empty<Keys>()];
	private static readonly List<Keys[]> DefaultAddEventTimeSignature                = [Array.Empty<Keys>()];
	private static readonly List<Keys[]> DefaultAddEventLabel                        = [Array.Empty<Keys>()];
	private static readonly List<Keys[]> DefaultAddEventPattern                      = [Array.Empty<Keys>()];
	private static readonly List<Keys[]> DefaultMoveEventPreview                     = [Array.Empty<Keys>()];
	private static readonly List<Keys[]> DefaultMoveEventEndHint                     = [Array.Empty<Keys>()];
	private static readonly List<Keys[]> DefaultConvertSelectedTapsToMines           = [Array.Empty<Keys>()];
	private static readonly List<Keys[]> DefaultConvertSelectedTapsToFakes           = [Array.Empty<Keys>()];
	private static readonly List<Keys[]> DefaultConvertSelectedTapsToLifts           = [Array.Empty<Keys>()];
	private static readonly List<Keys[]> DefaultConvertSelectedMinesToTaps           = [Array.Empty<Keys>()];
	private static readonly List<Keys[]> DefaultConvertSelectedMinesToFakes          = [Array.Empty<Keys>()];
	private static readonly List<Keys[]> DefaultConvertSelectedMinesToLifts          = [Array.Empty<Keys>()];
	private static readonly List<Keys[]> DefaultConvertSelectedFakesToTaps           = [Array.Empty<Keys>()];
	private static readonly List<Keys[]> DefaultConvertSelectedLiftsToTaps           = [Array.Empty<Keys>()];
	private static readonly List<Keys[]> DefaultConvertSelectedHoldsToRolls          = [Array.Empty<Keys>()];
	private static readonly List<Keys[]> DefaultConvertSelectedHoldsToTaps           = [Array.Empty<Keys>()];
	private static readonly List<Keys[]> DefaultConvertSelectedHoldsToMines          = [Array.Empty<Keys>()];
	private static readonly List<Keys[]> DefaultConvertSelectedRollsToHolds          = [Array.Empty<Keys>()];
	private static readonly List<Keys[]> DefaultConvertSelectedRollsToTaps           = [Array.Empty<Keys>()];
	private static readonly List<Keys[]> DefaultConvertSelectedRollsToMines          = [Array.Empty<Keys>()];
	private static readonly List<Keys[]> DefaultConvertSelectedWarpsToNegativeStops  = [Array.Empty<Keys>()];
	private static readonly List<Keys[]> DefaultConvertSelectedNegativeStopsToWarps  = [Array.Empty<Keys>()];
	private static readonly List<Keys[]> DefaultSnapToNone                           = [Array.Empty<Keys>()];
	private static readonly List<Keys[]> DefaultSnapToQuarters                       = [Array.Empty<Keys>()];
	private static readonly List<Keys[]> DefaultSnapToEighths                        = [Array.Empty<Keys>()];
	private static readonly List<Keys[]> DefaultSnapToTwelfths                       = [Array.Empty<Keys>()];
	private static readonly List<Keys[]> DefaultSnapToSixteenths                     = [Array.Empty<Keys>()];
	private static readonly List<Keys[]> DefaultSnapToTwentyFourths                  = [Array.Empty<Keys>()];
	private static readonly List<Keys[]> DefaultSnapToThirtySeconds                  = [Array.Empty<Keys>()];
	private static readonly List<Keys[]> DefaultSnapToFortyEighths                   = [Array.Empty<Keys>()];
	private static readonly List<Keys[]> DefaultSnapToSixtyFourths                   = [Array.Empty<Keys>()];
	private static readonly List<Keys[]> DefaultSnapToOneHundredNinetySeconds        = [Array.Empty<Keys>()];
	// @formatter:on

	#region Properties

	// Regex to generate the properties below.
	// Copy new Defaults below, then search for this in selection:
	// .*private static readonly List\<Keys\[\]\> Default([a-zA-Z0-9]+) +.*;
	// Replace with this:
	// \t[JsonInclude]\r\n\tpublic List<Keys[]> $1\r\n\t{\r\n\t\tget => $1Internal;\r\n\t\tset\r\n\t\t{\r\n\t\t\t$1Internal = value;\r\n\t\t\tNotify(NotificationKeyBindingChanged, this, nameof($1));\r\n\t\t}\r\n\t}\r\n\tprivate List<Keys[]> $1Internal = Default$1;\r\n\r\n

	[JsonInclude]
	public List<Keys[]> Open
	{
		get => OpenInternal;
		set
		{
			OpenInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(Open));
		}
	}

	private List<Keys[]> OpenInternal = DefaultOpen;

	[JsonInclude]
	public List<Keys[]> OpenContainingFolder
	{
		get => OpenContainingFolderInternal;
		set
		{
			OpenContainingFolderInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(OpenContainingFolder));
		}
	}

	private List<Keys[]> OpenContainingFolderInternal = DefaultOpenContainingFolder;

	[JsonInclude]
	public List<Keys[]> SaveAs
	{
		get => SaveAsInternal;
		set
		{
			SaveAsInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(SaveAs));
		}
	}

	private List<Keys[]> SaveAsInternal = DefaultSaveAs;

	[JsonInclude]
	public List<Keys[]> Save
	{
		get => SaveInternal;
		set
		{
			SaveInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(Save));
		}
	}

	private List<Keys[]> SaveInternal = DefaultSave;

	[JsonInclude]
	public List<Keys[]> New
	{
		get => NewInternal;
		set
		{
			NewInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(New));
		}
	}

	private List<Keys[]> NewInternal = DefaultNew;

	[JsonInclude]
	public List<Keys[]> Reload
	{
		get => ReloadInternal;
		set
		{
			ReloadInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(Reload));
		}
	}

	private List<Keys[]> ReloadInternal = DefaultReload;

	[JsonInclude]
	public List<Keys[]> Close
	{
		get => CloseInternal;
		set
		{
			CloseInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(Close));
		}
	}

	private List<Keys[]> CloseInternal = DefaultClose;

	[JsonInclude]
	public List<Keys[]> Undo
	{
		get => UndoInternal;
		set
		{
			UndoInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(Undo));
		}
	}

	private List<Keys[]> UndoInternal = DefaultUndo;

	[JsonInclude]
	public List<Keys[]> Redo
	{
		get => RedoInternal;
		set
		{
			RedoInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(Redo));
		}
	}

	private List<Keys[]> RedoInternal = DefaultRedo;

	[JsonInclude]
	public List<Keys[]> SelectAllNotes
	{
		get => SelectAllNotesInternal;
		set
		{
			SelectAllNotesInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(SelectAllNotes));
		}
	}

	private List<Keys[]> SelectAllNotesInternal = DefaultSelectAllNotes;

	[JsonInclude]
	public List<Keys[]> SelectAllTaps
	{
		get => SelectAllTapsInternal;
		set
		{
			SelectAllTapsInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(SelectAllTaps));
		}
	}

	private List<Keys[]> SelectAllTapsInternal = DefaultSelectAllTaps;

	[JsonInclude]
	public List<Keys[]> SelectAllMines
	{
		get => SelectAllMinesInternal;
		set
		{
			SelectAllMinesInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(SelectAllMines));
		}
	}

	private List<Keys[]> SelectAllMinesInternal = DefaultSelectAllMines;

	[JsonInclude]
	public List<Keys[]> SelectAllFakes
	{
		get => SelectAllFakesInternal;
		set
		{
			SelectAllFakesInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(SelectAllFakes));
		}
	}

	private List<Keys[]> SelectAllFakesInternal = DefaultSelectAllFakes;

	[JsonInclude]
	public List<Keys[]> SelectAllLifts
	{
		get => SelectAllLiftsInternal;
		set
		{
			SelectAllLiftsInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(SelectAllLifts));
		}
	}

	private List<Keys[]> SelectAllLiftsInternal = DefaultSelectAllLifts;

	[JsonInclude]
	public List<Keys[]> SelectAllHolds
	{
		get => SelectAllHoldsInternal;
		set
		{
			SelectAllHoldsInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(SelectAllHolds));
		}
	}

	private List<Keys[]> SelectAllHoldsInternal = DefaultSelectAllHolds;

	[JsonInclude]
	public List<Keys[]> SelectAllRolls
	{
		get => SelectAllRollsInternal;
		set
		{
			SelectAllRollsInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(SelectAllRolls));
		}
	}

	private List<Keys[]> SelectAllRollsInternal = DefaultSelectAllRolls;

	[JsonInclude]
	public List<Keys[]> SelectAllHoldsAndRolls
	{
		get => SelectAllHoldsAndRollsInternal;
		set
		{
			SelectAllHoldsAndRollsInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(SelectAllHoldsAndRolls));
		}
	}

	private List<Keys[]> SelectAllHoldsAndRollsInternal = DefaultSelectAllHoldsAndRolls;

	[JsonInclude]
	public List<Keys[]> SelectAllCurrentPlayerNotes
	{
		get => SelectAllCurrentPlayerNotesInternal;
		set
		{
			SelectAllCurrentPlayerNotesInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(SelectAllCurrentPlayerNotes));
		}
	}

	private List<Keys[]> SelectAllCurrentPlayerNotesInternal = DefaultSelectAllCurrentPlayerNotes;

	[JsonInclude]
	public List<Keys[]> SelectAllCurrentPlayerTaps
	{
		get => SelectAllCurrentPlayerTapsInternal;
		set
		{
			SelectAllCurrentPlayerTapsInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(SelectAllCurrentPlayerTaps));
		}
	}

	private List<Keys[]> SelectAllCurrentPlayerTapsInternal = DefaultSelectAllCurrentPlayerTaps;

	[JsonInclude]
	public List<Keys[]> SelectAllCurrentPlayerMines
	{
		get => SelectAllCurrentPlayerMinesInternal;
		set
		{
			SelectAllCurrentPlayerMinesInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(SelectAllCurrentPlayerMines));
		}
	}

	private List<Keys[]> SelectAllCurrentPlayerMinesInternal = DefaultSelectAllCurrentPlayerMines;

	[JsonInclude]
	public List<Keys[]> SelectAllCurrentPlayerFakes
	{
		get => SelectAllCurrentPlayerFakesInternal;
		set
		{
			SelectAllCurrentPlayerFakesInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(SelectAllCurrentPlayerFakes));
		}
	}

	private List<Keys[]> SelectAllCurrentPlayerFakesInternal = DefaultSelectAllCurrentPlayerFakes;

	[JsonInclude]
	public List<Keys[]> SelectAllCurrentPlayerLifts
	{
		get => SelectAllCurrentPlayerLiftsInternal;
		set
		{
			SelectAllCurrentPlayerLiftsInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(SelectAllCurrentPlayerLifts));
		}
	}

	private List<Keys[]> SelectAllCurrentPlayerLiftsInternal = DefaultSelectAllCurrentPlayerLifts;

	[JsonInclude]
	public List<Keys[]> SelectAllCurrentPlayerHolds
	{
		get => SelectAllCurrentPlayerHoldsInternal;
		set
		{
			SelectAllCurrentPlayerHoldsInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(SelectAllCurrentPlayerHolds));
		}
	}

	private List<Keys[]> SelectAllCurrentPlayerHoldsInternal = DefaultSelectAllCurrentPlayerHolds;

	[JsonInclude]
	public List<Keys[]> SelectAllCurrentPlayerRolls
	{
		get => SelectAllCurrentPlayerRollsInternal;
		set
		{
			SelectAllCurrentPlayerRollsInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(SelectAllCurrentPlayerRolls));
		}
	}

	private List<Keys[]> SelectAllCurrentPlayerRollsInternal = DefaultSelectAllCurrentPlayerRolls;

	[JsonInclude]
	public List<Keys[]> SelectAllCurrentPlayerHoldsAndRolls
	{
		get => SelectAllCurrentPlayerHoldsAndRollsInternal;
		set
		{
			SelectAllCurrentPlayerHoldsAndRollsInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(SelectAllCurrentPlayerHoldsAndRolls));
		}
	}

	private List<Keys[]> SelectAllCurrentPlayerHoldsAndRollsInternal = DefaultSelectAllCurrentPlayerHoldsAndRolls;

	[JsonInclude]
	public List<Keys[]> SelectAllMiscEvents
	{
		get => SelectAllMiscEventsInternal;
		set
		{
			SelectAllMiscEventsInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(SelectAllMiscEvents));
		}
	}

	private List<Keys[]> SelectAllMiscEventsInternal = DefaultSelectAllMiscEvents;

	[JsonInclude]
	public List<Keys[]> SelectAll
	{
		get => SelectAllInternal;
		set
		{
			SelectAllInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(SelectAll));
		}
	}

	private List<Keys[]> SelectAllInternal = DefaultSelectAll;

	[JsonInclude]
	public List<Keys[]> SelectAllPatterns
	{
		get => SelectAllPatternsInternal;
		set
		{
			SelectAllPatternsInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(SelectAllPatterns));
		}
	}

	private List<Keys[]> SelectAllPatternsInternal = DefaultSelectAllPatterns;

	[JsonInclude]
	public List<Keys[]> Copy
	{
		get => CopyInternal;
		set
		{
			CopyInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(Copy));
		}
	}

	private List<Keys[]> CopyInternal = DefaultCopy;

	[JsonInclude]
	public List<Keys[]> Cut
	{
		get => CutInternal;
		set
		{
			CutInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(Cut));
		}
	}

	private List<Keys[]> CutInternal = DefaultCut;

	[JsonInclude]
	public List<Keys[]> Paste
	{
		get => PasteInternal;
		set
		{
			PasteInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(Paste));
		}
	}

	private List<Keys[]> PasteInternal = DefaultPaste;

	[JsonInclude]
	public List<Keys[]> TogglePreview
	{
		get => TogglePreviewInternal;
		set
		{
			TogglePreviewInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(TogglePreview));
		}
	}

	private List<Keys[]> TogglePreviewInternal = DefaultTogglePreview;

	[JsonInclude]
	public List<Keys[]> ToggleAssistTick
	{
		get => ToggleAssistTickInternal;
		set
		{
			ToggleAssistTickInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(ToggleAssistTick));
		}
	}

	private List<Keys[]> ToggleAssistTickInternal = DefaultToggleAssistTick;

	[JsonInclude]
	public List<Keys[]> ToggleBeatTick
	{
		get => ToggleBeatTickInternal;
		set
		{
			ToggleBeatTickInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(ToggleBeatTick));
		}
	}

	private List<Keys[]> ToggleBeatTickInternal = DefaultToggleBeatTick;

	[JsonInclude]
	public List<Keys[]> DecreaseMusicRate
	{
		get => DecreaseMusicRateInternal;
		set
		{
			DecreaseMusicRateInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(DecreaseMusicRate));
		}
	}

	private List<Keys[]> DecreaseMusicRateInternal = DefaultDecreaseMusicRate;

	[JsonInclude]
	public List<Keys[]> IncreaseMusicRate
	{
		get => IncreaseMusicRateInternal;
		set
		{
			IncreaseMusicRateInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(IncreaseMusicRate));
		}
	}

	private List<Keys[]> IncreaseMusicRateInternal = DefaultIncreaseMusicRate;

	[JsonInclude]
	public List<Keys[]> PlayPause
	{
		get => PlayPauseInternal;
		set
		{
			PlayPauseInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(PlayPause));
		}
	}

	private List<Keys[]> PlayPauseInternal = DefaultPlayPause;

	[JsonInclude]
	public List<Keys[]> CancelGoBack
	{
		get => CancelGoBackInternal;
		set
		{
			CancelGoBackInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(CancelGoBack));
		}
	}

	private List<Keys[]> CancelGoBackInternal = DefaultCancelGoBack;

	[JsonInclude]
	public List<Keys[]> ToggleNoteEntryMode
	{
		get => ToggleNoteEntryModeInternal;
		set
		{
			ToggleNoteEntryModeInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(ToggleNoteEntryMode));
		}
	}

	private List<Keys[]> ToggleNoteEntryModeInternal = DefaultToggleNoteEntryMode;

	[JsonInclude]
	public List<Keys[]> ToggleSpacingMode
	{
		get => ToggleSpacingModeInternal;
		set
		{
			ToggleSpacingModeInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(ToggleSpacingMode));
		}
	}

	private List<Keys[]> ToggleSpacingModeInternal = DefaultToggleSpacingMode;

	[JsonInclude]
	public List<Keys[]> TogglePlayer
	{
		get => TogglePlayerInternal;
		set
		{
			TogglePlayerInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(TogglePlayer));
		}
	}

	private List<Keys[]> TogglePlayerInternal = DefaultTogglePlayer;

	[JsonInclude]
	public List<Keys[]> SetPlayer1
	{
		get => SetPlayer1Internal;
		set
		{
			SetPlayer1Internal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(SetPlayer1));
		}
	}

	private List<Keys[]> SetPlayer1Internal = DefaultSetPlayer1;

	[JsonInclude]
	public List<Keys[]> SetPlayer2
	{
		get => SetPlayer2Internal;
		set
		{
			SetPlayer2Internal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(SetPlayer2));
		}
	}

	private List<Keys[]> SetPlayer2Internal = DefaultSetPlayer2;

	[JsonInclude]
	public List<Keys[]> SetPlayer3
	{
		get => SetPlayer3Internal;
		set
		{
			SetPlayer3Internal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(SetPlayer3));
		}
	}

	private List<Keys[]> SetPlayer3Internal = DefaultSetPlayer3;

	[JsonInclude]
	public List<Keys[]> SetPlayer4
	{
		get => SetPlayer4Internal;
		set
		{
			SetPlayer4Internal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(SetPlayer4));
		}
	}

	private List<Keys[]> SetPlayer4Internal = DefaultSetPlayer4;

	[JsonInclude]
	public List<Keys[]> ConvertSelectedNotesToPlayer1
	{
		get => ConvertSelectedNotesToPlayer1Internal;
		set
		{
			ConvertSelectedNotesToPlayer1Internal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(ConvertSelectedNotesToPlayer1));
		}
	}

	private List<Keys[]> ConvertSelectedNotesToPlayer1Internal = DefaultConvertSelectedNotesToPlayer1;

	[JsonInclude]
	public List<Keys[]> ConvertSelectedNotesToPlayer2
	{
		get => ConvertSelectedNotesToPlayer2Internal;
		set
		{
			ConvertSelectedNotesToPlayer2Internal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(ConvertSelectedNotesToPlayer2));
		}
	}

	private List<Keys[]> ConvertSelectedNotesToPlayer2Internal = DefaultConvertSelectedNotesToPlayer2;

	[JsonInclude]
	public List<Keys[]> ConvertSelectedNotesToPlayer3
	{
		get => ConvertSelectedNotesToPlayer3Internal;
		set
		{
			ConvertSelectedNotesToPlayer3Internal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(ConvertSelectedNotesToPlayer3));
		}
	}

	private List<Keys[]> ConvertSelectedNotesToPlayer3Internal = DefaultConvertSelectedNotesToPlayer3;

	[JsonInclude]
	public List<Keys[]> ConvertSelectedNotesToPlayer4
	{
		get => ConvertSelectedNotesToPlayer4Internal;
		set
		{
			ConvertSelectedNotesToPlayer4Internal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(ConvertSelectedNotesToPlayer4));
		}
	}

	private List<Keys[]> ConvertSelectedNotesToPlayer4Internal = DefaultConvertSelectedNotesToPlayer4;

	[JsonInclude]
	public List<Keys[]> OpenPreviousChart
	{
		get => OpenPreviousChartInternal;
		set
		{
			OpenPreviousChartInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(OpenPreviousChart));
		}
	}

	private List<Keys[]> OpenPreviousChartInternal = DefaultOpenPreviousChart;

	[JsonInclude]
	public List<Keys[]> OpenNextChart
	{
		get => OpenNextChartInternal;
		set
		{
			OpenNextChartInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(OpenNextChart));
		}
	}

	private List<Keys[]> OpenNextChartInternal = DefaultOpenNextChart;

	[JsonInclude]
	public List<Keys[]> CloseFocusedChart
	{
		get => CloseFocusedChartInternal;
		set
		{
			CloseFocusedChartInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(CloseFocusedChart));
		}
	}

	private List<Keys[]> CloseFocusedChartInternal = DefaultCloseFocusedChart;

	[JsonInclude]
	public List<Keys[]> KeepChartOpen
	{
		get => KeepChartOpenInternal;
		set
		{
			KeepChartOpenInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(KeepChartOpen));
		}
	}

	private List<Keys[]> KeepChartOpenInternal = DefaultKeepChartOpen;

	[JsonInclude]
	public List<Keys[]> MoveFocusedChartLeft
	{
		get => MoveFocusedChartLeftInternal;
		set
		{
			MoveFocusedChartLeftInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(MoveFocusedChartLeft));
		}
	}

	private List<Keys[]> MoveFocusedChartLeftInternal = DefaultMoveFocusedChartLeft;

	[JsonInclude]
	public List<Keys[]> MoveFocusedChartRight
	{
		get => MoveFocusedChartRightInternal;
		set
		{
			MoveFocusedChartRightInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(MoveFocusedChartRight));
		}
	}

	private List<Keys[]> MoveFocusedChartRightInternal = DefaultMoveFocusedChartRight;

	[JsonInclude]
	public List<Keys[]> FocusPreviousChart
	{
		get => FocusPreviousChartInternal;
		set
		{
			FocusPreviousChartInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(FocusPreviousChart));
		}
	}

	private List<Keys[]> FocusPreviousChartInternal = DefaultFocusPreviousChart;

	[JsonInclude]
	public List<Keys[]> FocusNextChart
	{
		get => FocusNextChartInternal;
		set
		{
			FocusNextChartInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(FocusNextChart));
		}
	}

	private List<Keys[]> FocusNextChartInternal = DefaultFocusNextChart;

	[JsonInclude]
	public List<Keys[]> DecreaseSnap
	{
		get => DecreaseSnapInternal;
		set
		{
			DecreaseSnapInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(DecreaseSnap));
		}
	}

	private List<Keys[]> DecreaseSnapInternal = DefaultDecreaseSnap;

	[JsonInclude]
	public List<Keys[]> IncreaseSnap
	{
		get => IncreaseSnapInternal;
		set
		{
			IncreaseSnapInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(IncreaseSnap));
		}
	}

	private List<Keys[]> IncreaseSnapInternal = DefaultIncreaseSnap;

	[JsonInclude]
	public List<Keys[]> MoveUp
	{
		get => MoveUpInternal;
		set
		{
			MoveUpInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(MoveUp));
		}
	}

	private List<Keys[]> MoveUpInternal = DefaultMoveUp;

	[JsonInclude]
	public List<Keys[]> MoveDown
	{
		get => MoveDownInternal;
		set
		{
			MoveDownInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(MoveDown));
		}
	}

	private List<Keys[]> MoveDownInternal = DefaultMoveDown;

	[JsonInclude]
	public List<Keys[]> MoveToPreviousRowWithSteps
	{
		get => MoveToPreviousRowWithStepsInternal;
		set
		{
			MoveToPreviousRowWithStepsInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(MoveToPreviousRowWithSteps));
		}
	}

	private List<Keys[]> MoveToPreviousRowWithStepsInternal = DefaultMoveToPreviousRowWithSteps;

	[JsonInclude]
	public List<Keys[]> MoveToNextRowWithSteps
	{
		get => MoveToNextRowWithStepsInternal;
		set
		{
			MoveToNextRowWithStepsInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(MoveToNextRowWithSteps));
		}
	}

	private List<Keys[]> MoveToNextRowWithStepsInternal = DefaultMoveToNextRowWithSteps;

	[JsonInclude]
	public List<Keys[]> MoveToPreviousRowWithEvent
	{
		get => MoveToPreviousRowWithEventInternal;
		set
		{
			MoveToPreviousRowWithEventInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(MoveToPreviousRowWithEvent));
		}
	}

	private List<Keys[]> MoveToPreviousRowWithEventInternal = DefaultMoveToPreviousRowWithEvent;

	[JsonInclude]
	public List<Keys[]> MoveToNextRowWithEvent
	{
		get => MoveToNextRowWithEventInternal;
		set
		{
			MoveToNextRowWithEventInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(MoveToNextRowWithEvent));
		}
	}

	private List<Keys[]> MoveToNextRowWithEventInternal = DefaultMoveToNextRowWithEvent;

	[JsonInclude]
	public List<Keys[]> MoveToStartOfStream
	{
		get => MoveToStartOfStreamInternal;
		set
		{
			MoveToStartOfStreamInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(MoveToStartOfStream));
		}
	}

	private List<Keys[]> MoveToStartOfStreamInternal = DefaultMoveToStartOfStream;

	[JsonInclude]
	public List<Keys[]> MoveToEndOfStream
	{
		get => MoveToEndOfStreamInternal;
		set
		{
			MoveToEndOfStreamInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(MoveToEndOfStream));
		}
	}

	private List<Keys[]> MoveToEndOfStreamInternal = DefaultMoveToEndOfStream;

	[JsonInclude]
	public List<Keys[]> MoveToPreviousMeasure
	{
		get => MoveToPreviousMeasureInternal;
		set
		{
			MoveToPreviousMeasureInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(MoveToPreviousMeasure));
		}
	}

	private List<Keys[]> MoveToPreviousMeasureInternal = DefaultMoveToPreviousMeasure;

	[JsonInclude]
	public List<Keys[]> MoveToNextMeasure
	{
		get => MoveToNextMeasureInternal;
		set
		{
			MoveToNextMeasureInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(MoveToNextMeasure));
		}
	}

	private List<Keys[]> MoveToNextMeasureInternal = DefaultMoveToNextMeasure;

	[JsonInclude]
	public List<Keys[]> MoveToChartStart
	{
		get => MoveToChartStartInternal;
		set
		{
			MoveToChartStartInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(MoveToChartStart));
		}
	}

	private List<Keys[]> MoveToChartStartInternal = DefaultMoveToChartStart;

	[JsonInclude]
	public List<Keys[]> MoveToChartEnd
	{
		get => MoveToChartEndInternal;
		set
		{
			MoveToChartEndInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(MoveToChartEnd));
		}
	}

	private List<Keys[]> MoveToChartEndInternal = DefaultMoveToChartEnd;

	[JsonInclude]
	public List<Keys[]> MoveToNextLabel
	{
		get => MoveToNextLabelInternal;
		set
		{
			MoveToNextLabelInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(MoveToNextLabel));
		}
	}

	private List<Keys[]> MoveToNextLabelInternal = DefaultMoveToNextLabel;

	[JsonInclude]
	public List<Keys[]> MoveToPreviousLabel
	{
		get => MoveToPreviousLabelInternal;
		set
		{
			MoveToPreviousLabelInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(MoveToPreviousLabel));
		}
	}

	private List<Keys[]> MoveToPreviousLabelInternal = DefaultMoveToPreviousLabel;

	[JsonInclude]
	public List<Keys[]> MoveToNextPattern
	{
		get => MoveToNextPatternInternal;
		set
		{
			MoveToNextPatternInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(MoveToNextPattern));
		}
	}

	private List<Keys[]> MoveToNextPatternInternal = DefaultMoveToNextPattern;

	[JsonInclude]
	public List<Keys[]> MoveToPreviousPattern
	{
		get => MoveToPreviousPatternInternal;
		set
		{
			MoveToPreviousPatternInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(MoveToPreviousPattern));
		}
	}

	private List<Keys[]> MoveToPreviousPatternInternal = DefaultMoveToPreviousPattern;

	[JsonInclude]
	public List<Keys[]> RegenerateAllPatternsFixedSeeds
	{
		get => RegenerateAllPatternsFixedSeedsInternal;
		set
		{
			RegenerateAllPatternsFixedSeedsInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(RegenerateAllPatternsFixedSeeds));
		}
	}

	private List<Keys[]> RegenerateAllPatternsFixedSeedsInternal = DefaultRegenerateAllPatternsFixedSeeds;

	[JsonInclude]
	public List<Keys[]> RegenerateAllPatternsNewSeeds
	{
		get => RegenerateAllPatternsNewSeedsInternal;
		set
		{
			RegenerateAllPatternsNewSeedsInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(RegenerateAllPatternsNewSeeds));
		}
	}

	private List<Keys[]> RegenerateAllPatternsNewSeedsInternal = DefaultRegenerateAllPatternsNewSeeds;

	[JsonInclude]
	public List<Keys[]> RegenerateSelectedPatternsFixedSeeds
	{
		get => RegenerateSelectedPatternsFixedSeedsInternal;
		set
		{
			RegenerateSelectedPatternsFixedSeedsInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(RegenerateSelectedPatternsFixedSeeds));
		}
	}

	private List<Keys[]> RegenerateSelectedPatternsFixedSeedsInternal = DefaultRegenerateSelectedPatternsFixedSeeds;

	[JsonInclude]
	public List<Keys[]> RegenerateSelectedPatternsNewSeeds
	{
		get => RegenerateSelectedPatternsNewSeedsInternal;
		set
		{
			RegenerateSelectedPatternsNewSeedsInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(RegenerateSelectedPatternsNewSeeds));
		}
	}

	private List<Keys[]> RegenerateSelectedPatternsNewSeedsInternal = DefaultRegenerateSelectedPatternsNewSeeds;

	[JsonInclude]
	public List<Keys[]> Delete
	{
		get => DeleteInternal;
		set
		{
			DeleteInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(Delete));
		}
	}

	private List<Keys[]> DeleteInternal = DefaultDelete;

	[JsonInclude]
	public List<Keys[]> ShiftLeft
	{
		get => ShiftLeftInternal;
		set
		{
			ShiftLeftInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(ShiftLeft));
		}
	}

	private List<Keys[]> ShiftLeftInternal = DefaultShiftLeft;

	[JsonInclude]
	public List<Keys[]> ShiftLeftAndWrap
	{
		get => ShiftLeftAndWrapInternal;
		set
		{
			ShiftLeftAndWrapInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(ShiftLeftAndWrap));
		}
	}

	private List<Keys[]> ShiftLeftAndWrapInternal = DefaultShiftLeftAndWrap;

	[JsonInclude]
	public List<Keys[]> ShiftRight
	{
		get => ShiftRightInternal;
		set
		{
			ShiftRightInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(ShiftRight));
		}
	}

	private List<Keys[]> ShiftRightInternal = DefaultShiftRight;

	[JsonInclude]
	public List<Keys[]> ShiftRightAndWrap
	{
		get => ShiftRightAndWrapInternal;
		set
		{
			ShiftRightAndWrapInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(ShiftRightAndWrap));
		}
	}

	private List<Keys[]> ShiftRightAndWrapInternal = DefaultShiftRightAndWrap;

	[JsonInclude]
	public List<Keys[]> ShiftEarlier
	{
		get => ShiftEarlierInternal;
		set
		{
			ShiftEarlierInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(ShiftEarlier));
		}
	}

	private List<Keys[]> ShiftEarlierInternal = DefaultShiftEarlier;

	[JsonInclude]
	public List<Keys[]> ShiftLater
	{
		get => ShiftLaterInternal;
		set
		{
			ShiftLaterInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(ShiftLater));
		}
	}

	private List<Keys[]> ShiftLaterInternal = DefaultShiftLater;

	[JsonInclude]
	public List<Keys[]> Mirror
	{
		get => MirrorInternal;
		set
		{
			MirrorInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(Mirror));
		}
	}

	private List<Keys[]> MirrorInternal = DefaultMirror;

	[JsonInclude]
	public List<Keys[]> Flip
	{
		get => FlipInternal;
		set
		{
			FlipInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(Flip));
		}
	}

	private List<Keys[]> FlipInternal = DefaultFlip;

	[JsonInclude]
	public List<Keys[]> MirrorAndFlip
	{
		get => MirrorAndFlipInternal;
		set
		{
			MirrorAndFlipInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(MirrorAndFlip));
		}
	}

	private List<Keys[]> MirrorAndFlipInternal = DefaultMirrorAndFlip;

	[JsonInclude]
	public List<Keys[]> Arrow0
	{
		get => Arrow0Internal;
		set
		{
			Arrow0Internal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(Arrow0));
		}
	}

	private List<Keys[]> Arrow0Internal = DefaultArrow0;

	[JsonInclude]
	public List<Keys[]> Arrow1
	{
		get => Arrow1Internal;
		set
		{
			Arrow1Internal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(Arrow1));
		}
	}

	private List<Keys[]> Arrow1Internal = DefaultArrow1;

	[JsonInclude]
	public List<Keys[]> Arrow2
	{
		get => Arrow2Internal;
		set
		{
			Arrow2Internal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(Arrow2));
		}
	}

	private List<Keys[]> Arrow2Internal = DefaultArrow2;

	[JsonInclude]
	public List<Keys[]> Arrow3
	{
		get => Arrow3Internal;
		set
		{
			Arrow3Internal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(Arrow3));
		}
	}

	private List<Keys[]> Arrow3Internal = DefaultArrow3;

	[JsonInclude]
	public List<Keys[]> Arrow4
	{
		get => Arrow4Internal;
		set
		{
			Arrow4Internal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(Arrow4));
		}
	}

	private List<Keys[]> Arrow4Internal = DefaultArrow4;

	[JsonInclude]
	public List<Keys[]> Arrow5
	{
		get => Arrow5Internal;
		set
		{
			Arrow5Internal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(Arrow5));
		}
	}

	private List<Keys[]> Arrow5Internal = DefaultArrow5;

	[JsonInclude]
	public List<Keys[]> Arrow6
	{
		get => Arrow6Internal;
		set
		{
			Arrow6Internal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(Arrow6));
		}
	}

	private List<Keys[]> Arrow6Internal = DefaultArrow6;

	[JsonInclude]
	public List<Keys[]> Arrow7
	{
		get => Arrow7Internal;
		set
		{
			Arrow7Internal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(Arrow7));
		}
	}

	private List<Keys[]> Arrow7Internal = DefaultArrow7;

	[JsonInclude]
	public List<Keys[]> Arrow8
	{
		get => Arrow8Internal;
		set
		{
			Arrow8Internal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(Arrow8));
		}
	}

	private List<Keys[]> Arrow8Internal = DefaultArrow8;

	[JsonInclude]
	public List<Keys[]> Arrow9
	{
		get => Arrow9Internal;
		set
		{
			Arrow9Internal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(Arrow9));
		}
	}

	private List<Keys[]> Arrow9Internal = DefaultArrow9;

	[JsonInclude]
	public List<Keys[]> ArrowModification
	{
		get => ArrowModificationInternal;
		set
		{
			ArrowModificationInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(ArrowModification));
		}
	}

	private List<Keys[]> ArrowModificationInternal = DefaultArrowModification;

	[JsonInclude]
	public List<Keys[]> ScrollZoom
	{
		get => ScrollZoomInternal;
		set
		{
			ScrollZoomInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(ScrollZoom));
		}
	}

	private List<Keys[]> ScrollZoomInternal = DefaultScrollZoom;

	[JsonInclude]
	public List<Keys[]> ScrollSpacing
	{
		get => ScrollSpacingInternal;
		set
		{
			ScrollSpacingInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(ScrollSpacing));
		}
	}

	private List<Keys[]> ScrollSpacingInternal = DefaultScrollSpacing;

	[JsonInclude]
	public List<Keys[]> MouseSelectionControlBehavior
	{
		get => MouseSelectionControlBehaviorInternal;
		set
		{
			MouseSelectionControlBehaviorInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(MouseSelectionControlBehavior));
		}
	}

	private List<Keys[]> MouseSelectionControlBehaviorInternal = DefaultMouseSelectionControlBehavior;

	[JsonInclude]
	public List<Keys[]> MouseSelectionShiftBehavior
	{
		get => MouseSelectionShiftBehaviorInternal;
		set
		{
			MouseSelectionShiftBehaviorInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(MouseSelectionShiftBehavior));
		}
	}

	private List<Keys[]> MouseSelectionShiftBehaviorInternal = DefaultMouseSelectionShiftBehavior;

	[JsonInclude]
	public List<Keys[]> MouseSelectionAltBehavior
	{
		get => MouseSelectionAltBehaviorInternal;
		set
		{
			MouseSelectionAltBehaviorInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(MouseSelectionAltBehavior));
		}
	}

	private List<Keys[]> MouseSelectionAltBehaviorInternal = DefaultMouseSelectionAltBehavior;

	[JsonInclude]
	public List<Keys[]> LockReceptorMoveAxis
	{
		get => LockReceptorMoveAxisInternal;
		set
		{
			LockReceptorMoveAxisInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(LockReceptorMoveAxis));
		}
	}

	private List<Keys[]> LockReceptorMoveAxisInternal = DefaultLockReceptorMoveAxis;

	[JsonInclude]
	public List<Keys[]> AddEventTempo
	{
		get => AddEventTempoInternal;
		set
		{
			AddEventTempoInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(AddEventTempo));
		}
	}

	private List<Keys[]> AddEventTempoInternal = DefaultAddEventTempo;

	[JsonInclude]
	public List<Keys[]> AddEventInterpolatedScrollRate
	{
		get => AddEventInterpolatedScrollRateInternal;
		set
		{
			AddEventInterpolatedScrollRateInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(AddEventInterpolatedScrollRate));
		}
	}

	private List<Keys[]> AddEventInterpolatedScrollRateInternal = DefaultAddEventInterpolatedScrollRate;

	[JsonInclude]
	public List<Keys[]> AddEventScrollRate
	{
		get => AddEventScrollRateInternal;
		set
		{
			AddEventScrollRateInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(AddEventScrollRate));
		}
	}

	private List<Keys[]> AddEventScrollRateInternal = DefaultAddEventScrollRate;

	[JsonInclude]
	public List<Keys[]> AddEventStop
	{
		get => AddEventStopInternal;
		set
		{
			AddEventStopInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(AddEventStop));
		}
	}

	private List<Keys[]> AddEventStopInternal = DefaultAddEventStop;

	[JsonInclude]
	public List<Keys[]> AddEventDelay
	{
		get => AddEventDelayInternal;
		set
		{
			AddEventDelayInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(AddEventDelay));
		}
	}

	private List<Keys[]> AddEventDelayInternal = DefaultAddEventDelay;

	[JsonInclude]
	public List<Keys[]> AddEventWarp
	{
		get => AddEventWarpInternal;
		set
		{
			AddEventWarpInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(AddEventWarp));
		}
	}

	private List<Keys[]> AddEventWarpInternal = DefaultAddEventWarp;

	[JsonInclude]
	public List<Keys[]> AddEventFakeRegion
	{
		get => AddEventFakeRegionInternal;
		set
		{
			AddEventFakeRegionInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(AddEventFakeRegion));
		}
	}

	private List<Keys[]> AddEventFakeRegionInternal = DefaultAddEventFakeRegion;

	[JsonInclude]
	public List<Keys[]> AddEventTicks
	{
		get => AddEventTicksInternal;
		set
		{
			AddEventTicksInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(AddEventTicks));
		}
	}

	private List<Keys[]> AddEventTicksInternal = DefaultAddEventTicks;

	[JsonInclude]
	public List<Keys[]> AddEventComboMultipliers
	{
		get => AddEventComboMultipliersInternal;
		set
		{
			AddEventComboMultipliersInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(AddEventComboMultipliers));
		}
	}

	private List<Keys[]> AddEventComboMultipliersInternal = DefaultAddEventComboMultipliers;

	[JsonInclude]
	public List<Keys[]> AddEventTimeSignature
	{
		get => AddEventTimeSignatureInternal;
		set
		{
			AddEventTimeSignatureInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(AddEventTimeSignature));
		}
	}

	private List<Keys[]> AddEventTimeSignatureInternal = DefaultAddEventTimeSignature;

	[JsonInclude]
	public List<Keys[]> AddEventLabel
	{
		get => AddEventLabelInternal;
		set
		{
			AddEventLabelInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(AddEventLabel));
		}
	}

	private List<Keys[]> AddEventLabelInternal = DefaultAddEventLabel;

	[JsonInclude]
	public List<Keys[]> AddEventPattern
	{
		get => AddEventPatternInternal;
		set
		{
			AddEventPatternInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(AddEventPattern));
		}
	}

	private List<Keys[]> AddEventPatternInternal = DefaultAddEventPattern;

	[JsonInclude]
	public List<Keys[]> MoveEventPreview
	{
		get => MoveEventPreviewInternal;
		set
		{
			MoveEventPreviewInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(MoveEventPreview));
		}
	}

	private List<Keys[]> MoveEventPreviewInternal = DefaultMoveEventPreview;

	[JsonInclude]
	public List<Keys[]> MoveEventEndHint
	{
		get => MoveEventEndHintInternal;
		set
		{
			MoveEventEndHintInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(MoveEventEndHint));
		}
	}

	private List<Keys[]> MoveEventEndHintInternal = DefaultMoveEventEndHint;

	[JsonInclude]
	public List<Keys[]> ConvertSelectedTapsToMines
	{
		get => ConvertSelectedTapsToMinesInternal;
		set
		{
			ConvertSelectedTapsToMinesInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(ConvertSelectedTapsToMines));
		}
	}

	private List<Keys[]> ConvertSelectedTapsToMinesInternal = DefaultConvertSelectedTapsToMines;

	[JsonInclude]
	public List<Keys[]> ConvertSelectedTapsToFakes
	{
		get => ConvertSelectedTapsToFakesInternal;
		set
		{
			ConvertSelectedTapsToFakesInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(ConvertSelectedTapsToFakes));
		}
	}

	private List<Keys[]> ConvertSelectedTapsToFakesInternal = DefaultConvertSelectedTapsToFakes;

	[JsonInclude]
	public List<Keys[]> ConvertSelectedTapsToLifts
	{
		get => ConvertSelectedTapsToLiftsInternal;
		set
		{
			ConvertSelectedTapsToLiftsInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(ConvertSelectedTapsToLifts));
		}
	}

	private List<Keys[]> ConvertSelectedTapsToLiftsInternal = DefaultConvertSelectedTapsToLifts;

	[JsonInclude]
	public List<Keys[]> ConvertSelectedMinesToTaps
	{
		get => ConvertSelectedMinesToTapsInternal;
		set
		{
			ConvertSelectedMinesToTapsInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(ConvertSelectedMinesToTaps));
		}
	}

	private List<Keys[]> ConvertSelectedMinesToTapsInternal = DefaultConvertSelectedMinesToTaps;

	[JsonInclude]
	public List<Keys[]> ConvertSelectedMinesToFakes
	{
		get => ConvertSelectedMinesToFakesInternal;
		set
		{
			ConvertSelectedMinesToFakesInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(ConvertSelectedMinesToFakes));
		}
	}

	private List<Keys[]> ConvertSelectedMinesToFakesInternal = DefaultConvertSelectedMinesToFakes;

	[JsonInclude]
	public List<Keys[]> ConvertSelectedMinesToLifts
	{
		get => ConvertSelectedMinesToLiftsInternal;
		set
		{
			ConvertSelectedMinesToLiftsInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(ConvertSelectedMinesToLifts));
		}
	}

	private List<Keys[]> ConvertSelectedMinesToLiftsInternal = DefaultConvertSelectedMinesToLifts;

	[JsonInclude]
	public List<Keys[]> ConvertSelectedFakesToTaps
	{
		get => ConvertSelectedFakesToTapsInternal;
		set
		{
			ConvertSelectedFakesToTapsInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(ConvertSelectedFakesToTaps));
		}
	}

	private List<Keys[]> ConvertSelectedFakesToTapsInternal = DefaultConvertSelectedFakesToTaps;

	[JsonInclude]
	public List<Keys[]> ConvertSelectedLiftsToTaps
	{
		get => ConvertSelectedLiftsToTapsInternal;
		set
		{
			ConvertSelectedLiftsToTapsInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(ConvertSelectedLiftsToTaps));
		}
	}

	private List<Keys[]> ConvertSelectedLiftsToTapsInternal = DefaultConvertSelectedLiftsToTaps;

	[JsonInclude]
	public List<Keys[]> ConvertSelectedHoldsToRolls
	{
		get => ConvertSelectedHoldsToRollsInternal;
		set
		{
			ConvertSelectedHoldsToRollsInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(ConvertSelectedHoldsToRolls));
		}
	}

	private List<Keys[]> ConvertSelectedHoldsToRollsInternal = DefaultConvertSelectedHoldsToRolls;

	[JsonInclude]
	public List<Keys[]> ConvertSelectedHoldsToTaps
	{
		get => ConvertSelectedHoldsToTapsInternal;
		set
		{
			ConvertSelectedHoldsToTapsInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(ConvertSelectedHoldsToTaps));
		}
	}

	private List<Keys[]> ConvertSelectedHoldsToTapsInternal = DefaultConvertSelectedHoldsToTaps;

	[JsonInclude]
	public List<Keys[]> ConvertSelectedHoldsToMines
	{
		get => ConvertSelectedHoldsToMinesInternal;
		set
		{
			ConvertSelectedHoldsToMinesInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(ConvertSelectedHoldsToMines));
		}
	}

	private List<Keys[]> ConvertSelectedHoldsToMinesInternal = DefaultConvertSelectedHoldsToMines;

	[JsonInclude]
	public List<Keys[]> ConvertSelectedRollsToHolds
	{
		get => ConvertSelectedRollsToHoldsInternal;
		set
		{
			ConvertSelectedRollsToHoldsInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(ConvertSelectedRollsToHolds));
		}
	}

	private List<Keys[]> ConvertSelectedRollsToHoldsInternal = DefaultConvertSelectedRollsToHolds;

	[JsonInclude]
	public List<Keys[]> ConvertSelectedRollsToTaps
	{
		get => ConvertSelectedRollsToTapsInternal;
		set
		{
			ConvertSelectedRollsToTapsInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(ConvertSelectedRollsToTaps));
		}
	}

	private List<Keys[]> ConvertSelectedRollsToTapsInternal = DefaultConvertSelectedRollsToTaps;

	[JsonInclude]
	public List<Keys[]> ConvertSelectedRollsToMines
	{
		get => ConvertSelectedRollsToMinesInternal;
		set
		{
			ConvertSelectedRollsToMinesInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(ConvertSelectedRollsToMines));
		}
	}

	private List<Keys[]> ConvertSelectedRollsToMinesInternal = DefaultConvertSelectedRollsToMines;

	[JsonInclude]
	public List<Keys[]> ConvertSelectedWarpsToNegativeStops
	{
		get => ConvertSelectedWarpsToNegativeStopsInternal;
		set
		{
			ConvertSelectedWarpsToNegativeStopsInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(ConvertSelectedWarpsToNegativeStops));
		}
	}

	private List<Keys[]> ConvertSelectedWarpsToNegativeStopsInternal = DefaultConvertSelectedWarpsToNegativeStops;

	[JsonInclude]
	public List<Keys[]> ConvertSelectedNegativeStopsToWarps
	{
		get => ConvertSelectedNegativeStopsToWarpsInternal;
		set
		{
			ConvertSelectedNegativeStopsToWarpsInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(ConvertSelectedNegativeStopsToWarps));
		}
	}

	private List<Keys[]> ConvertSelectedNegativeStopsToWarpsInternal = DefaultConvertSelectedNegativeStopsToWarps;

	[JsonInclude]
	public List<Keys[]> SnapToNone
	{
		get => SnapToNoneInternal;
		set
		{
			SnapToNoneInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(SnapToNone));
		}
	}

	private List<Keys[]> SnapToNoneInternal = DefaultSnapToNone;

	[JsonInclude]
	public List<Keys[]> SnapToQuarters
	{
		get => SnapToQuartersInternal;
		set
		{
			SnapToQuartersInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(SnapToQuarters));
		}
	}

	private List<Keys[]> SnapToQuartersInternal = DefaultSnapToQuarters;

	[JsonInclude]
	public List<Keys[]> SnapToEighths
	{
		get => SnapToEighthsInternal;
		set
		{
			SnapToEighthsInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(SnapToEighths));
		}
	}

	private List<Keys[]> SnapToEighthsInternal = DefaultSnapToEighths;

	[JsonInclude]
	public List<Keys[]> SnapToTwelfths
	{
		get => SnapToTwelfthsInternal;
		set
		{
			SnapToTwelfthsInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(SnapToTwelfths));
		}
	}

	private List<Keys[]> SnapToTwelfthsInternal = DefaultSnapToTwelfths;

	[JsonInclude]
	public List<Keys[]> SnapToSixteenths
	{
		get => SnapToSixteenthsInternal;
		set
		{
			SnapToSixteenthsInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(SnapToSixteenths));
		}
	}

	private List<Keys[]> SnapToSixteenthsInternal = DefaultSnapToSixteenths;

	[JsonInclude]
	public List<Keys[]> SnapToTwentyFourths
	{
		get => SnapToTwentyFourthsInternal;
		set
		{
			SnapToTwentyFourthsInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(SnapToTwentyFourths));
		}
	}

	private List<Keys[]> SnapToTwentyFourthsInternal = DefaultSnapToTwentyFourths;

	[JsonInclude]
	public List<Keys[]> SnapToThirtySeconds
	{
		get => SnapToThirtySecondsInternal;
		set
		{
			SnapToThirtySecondsInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(SnapToThirtySeconds));
		}
	}

	private List<Keys[]> SnapToThirtySecondsInternal = DefaultSnapToThirtySeconds;

	[JsonInclude]
	public List<Keys[]> SnapToFortyEighths
	{
		get => SnapToFortyEighthsInternal;
		set
		{
			SnapToFortyEighthsInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(SnapToFortyEighths));
		}
	}

	private List<Keys[]> SnapToFortyEighthsInternal = DefaultSnapToFortyEighths;

	[JsonInclude]
	public List<Keys[]> SnapToSixtyFourths
	{
		get => SnapToSixtyFourthsInternal;
		set
		{
			SnapToSixtyFourthsInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(SnapToSixtyFourths));
		}
	}

	private List<Keys[]> SnapToSixtyFourthsInternal = DefaultSnapToSixtyFourths;

	[JsonInclude]
	public List<Keys[]> SnapToOneHundredNinetySeconds
	{
		get => SnapToOneHundredNinetySecondsInternal;
		set
		{
			SnapToOneHundredNinetySecondsInternal = value;
			Notify(NotificationKeyBindingChanged, this, nameof(SnapToOneHundredNinetySeconds));
		}
	}

	private List<Keys[]> SnapToOneHundredNinetySecondsInternal = DefaultSnapToOneHundredNinetySeconds;

	#endregion Properties

	public void PostLoad()
	{
		var invalidKeys = new List<Keys>();
		foreach (var propInfo in GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
		{
			if (propInfo.PropertyType == typeof(List<Keys[]>))
			{
				var value = (List<Keys[]>)propInfo.GetValue(this);
				if (value == null)
					continue;

				// Ensure each property has at least an empty key list.
				if (value.Count == 0)
					value.Add(Array.Empty<Keys>());

				// Remove invalid keys.
				for (var i = 0; i < value.Count; i++)
				{
					var keyList = value[i];
					invalidKeys.Clear();
					foreach (var key in keyList)
					{
						if (!IsValidKeyForBinding(key))
						{
							invalidKeys.Add(key);
							break;
						}
					}

					if (invalidKeys.Count > 0)
					{
						var newKeys = new List<Keys>();
						foreach (var key in keyList)
						{
							if (IsValidKeyForBinding(key))
							{
								newKeys.Add(key);
							}
						}

						var newBinding = newKeys.Count > 0 ? $"\"{string.Join(", ", newKeys)}\"" : "Unbound";
						Logger.Warn(
							$"Key binding {propInfo.Name} contains unsupported keys in \"{string.Join(", ", keyList)}\"." +
							$" These keys will be removed: \"{string.Join(", ", invalidKeys)}\"." +
							$" New Binding: {newBinding}");

						value[i] = newKeys.ToArray();
					}
				}
			}
		}
	}

	/// <summary>
	/// Returns whether or not the given key binding should block input to others.
	/// </summary>
	/// <param name="id">Key binding id.</param>
	/// <returns>True if the given key binding should block input to others and false otherwise.</returns>
	public bool BlocksInput(string id)
	{
		switch (id)
		{
			case nameof(ArrowModification):
			case nameof(ScrollZoom):
			case nameof(ScrollSpacing):
			case nameof(MouseSelectionAltBehavior):
			case nameof(MouseSelectionControlBehavior):
			case nameof(MouseSelectionShiftBehavior):
				return false;
		}

		return true;
	}

	/// <summary>
	/// Gets the default bindings for the given key binding.
	/// </summary>
	/// <param name="id">Key binding id.</param>
	/// <returns>Default bindings</returns>
	public List<Keys[]> GetDefaults(string id)
	{
		var fieldInfo = GetType().GetField(DefaultFieldNamePrefix + id, BindingFlags.NonPublic | BindingFlags.Static);
		if (fieldInfo == null)
			return null;
		return (List<Keys[]>)fieldInfo.GetValue(this);
	}

	/// <summary>
	/// Clones the inputs for the given key binding and returns them.
	/// </summary>
	/// <param name="id">Key binding id.</param>
	/// <returns>Cloned key bindings.</returns>
	public List<Keys[]> CloneKeyBinding(string id)
	{
		var propertyInfo = GetType().GetProperty(id);
		if (propertyInfo == null)
			return [];
		var binding = (List<Keys[]>)propertyInfo.GetValue(this);
		if (binding == null)
			return [];
		return CloneKeyBinding(binding);
	}

	/// <summary>
	/// Clones given key binding inputs and returns them.
	/// </summary>
	/// <param name="binding">Key bindings.</param>
	/// <returns>Cloned key bindings.</returns>
	public static List<Keys[]> CloneKeyBinding(List<Keys[]> binding)
	{
		var clone = new List<Keys[]>();
		foreach (var input in binding)
			clone.Add((Keys[])input.Clone());
		return clone;
	}
}

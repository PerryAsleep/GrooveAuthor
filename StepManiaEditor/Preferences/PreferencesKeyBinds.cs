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
	private static readonly List<Keys[]> DefaultSavePackFile                         = [[]];
	private static readonly List<Keys[]> DefaultNew                                  = [[Ctrl, Keys.N]];
	private static readonly List<Keys[]> DefaultReload                               = [[Ctrl, Keys.R]];
	private static readonly List<Keys[]> DefaultClose                                = [[Ctrl, Keys.LeftShift, Keys.F4], [Ctrl, Keys.LeftShift, Keys.W]];
	private static readonly List<Keys[]> DefaultExit                                 = RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? [[Ctrl, Keys.Q]] : [[Keys.LeftAlt, Keys.F4]];
	private static readonly List<Keys[]> DefaultUndo                                 = [[Ctrl, Keys.Z]];
	private static readonly List<Keys[]> DefaultRedo                                 = [[Ctrl, Keys.LeftShift, Keys.Z], [Ctrl, Keys.Y]];
	private static readonly List<Keys[]> DefaultSelectRowRange                       = [[Keys.Tab]];
	private static readonly List<Keys[]> DefaultSelectAllNotes                       = [[Ctrl, Keys.A]];
	private static readonly List<Keys[]> DefaultSelectAllTaps                        = [[]];
	private static readonly List<Keys[]> DefaultSelectAllMines                       = [[]];
	private static readonly List<Keys[]> DefaultSelectAllFakes                       = [[]];
	private static readonly List<Keys[]> DefaultSelectAllLifts                       = [[]];
	private static readonly List<Keys[]> DefaultSelectAllHolds                       = [[]];
	private static readonly List<Keys[]> DefaultSelectAllRolls                       = [[]];
	private static readonly List<Keys[]> DefaultSelectAllHoldsAndRolls               = [[]];
	private static readonly List<Keys[]> DefaultSelectAllCurrentPlayerNotes          = [[]];
	private static readonly List<Keys[]> DefaultSelectAllCurrentPlayerTaps           = [[]];
	private static readonly List<Keys[]> DefaultSelectAllCurrentPlayerMines          = [[]];
	private static readonly List<Keys[]> DefaultSelectAllCurrentPlayerFakes          = [[]];
	private static readonly List<Keys[]> DefaultSelectAllCurrentPlayerLifts          = [[]];
	private static readonly List<Keys[]> DefaultSelectAllCurrentPlayerHolds          = [[]];
	private static readonly List<Keys[]> DefaultSelectAllCurrentPlayerRolls          = [[]];
	private static readonly List<Keys[]> DefaultSelectAllCurrentPlayerHoldsAndRolls  = [[]];
	private static readonly List<Keys[]> DefaultSelectAllMiscEvents                  = [[Ctrl, Keys.LeftAlt, Keys.A]];
	private static readonly List<Keys[]> DefaultSelectAll                            = [[Ctrl, Keys.LeftShift, Keys.A]];
	private static readonly List<Keys[]> DefaultSelectAllPatterns                    = [[]];
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
	private static readonly List<Keys[]> DefaultSetPlayer1                           = [[]];
	private static readonly List<Keys[]> DefaultSetPlayer2                           = [[]];
	private static readonly List<Keys[]> DefaultSetPlayer3                           = [[]];
	private static readonly List<Keys[]> DefaultSetPlayer4                           = [[]];
	private static readonly List<Keys[]> DefaultConvertSelectedNotesToPlayer1        = [[]];
	private static readonly List<Keys[]> DefaultConvertSelectedNotesToPlayer2        = [[]];
	private static readonly List<Keys[]> DefaultConvertSelectedNotesToPlayer3        = [[]];
	private static readonly List<Keys[]> DefaultConvertSelectedNotesToPlayer4        = [[]];
	private static readonly List<Keys[]> DefaultSwapSelectedPlayer1And2Notes         = [[]];
	private static readonly List<Keys[]> DefaultSwapSelectedPlayer1And3Notes         = [[]];
	private static readonly List<Keys[]> DefaultSwapSelectedPlayer1And4Notes         = [[]];
	private static readonly List<Keys[]> DefaultSwapSelectedPlayer2And3Notes         = [[]];
	private static readonly List<Keys[]> DefaultSwapSelectedPlayer2And4Notes         = [[]];
	private static readonly List<Keys[]> DefaultSwapSelectedPlayer3And4Notes         = [[]];
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
	private static readonly List<Keys[]> DefaultMoveToPreviousRowWithSteps           = [[]];
	private static readonly List<Keys[]> DefaultMoveToNextRowWithSteps               = [[]];
	private static readonly List<Keys[]> DefaultMoveToPreviousRowWithEvent           = [[]];
	private static readonly List<Keys[]> DefaultMoveToNextRowWithEvent               = [[]];
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
	private static readonly List<Keys[]> DefaultMirrorAndFlip                        = [[]];
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
	private static readonly List<Keys[]> DefaultAddEventTempo                        = [[]];
	private static readonly List<Keys[]> DefaultAddEventInterpolatedScrollRate       = [[]];
	private static readonly List<Keys[]> DefaultAddEventScrollRate                   = [[]];
	private static readonly List<Keys[]> DefaultAddEventStop                         = [[]];
	private static readonly List<Keys[]> DefaultAddEventDelay                        = [[]];
	private static readonly List<Keys[]> DefaultAddEventWarp                         = [[]];
	private static readonly List<Keys[]> DefaultAddEventFakeRegion                   = [[]];
	private static readonly List<Keys[]> DefaultAddEventTicks                        = [[]];
	private static readonly List<Keys[]> DefaultAddEventComboMultipliers             = [[]];
	private static readonly List<Keys[]> DefaultAddEventTimeSignature                = [[]];
	private static readonly List<Keys[]> DefaultAddEventLabel                        = [[]];
	private static readonly List<Keys[]> DefaultAddEventAttack                       = [[]];
	private static readonly List<Keys[]> DefaultAddEventPattern                      = [[]];
	private static readonly List<Keys[]> DefaultMoveEventPreview                     = [[]];
	private static readonly List<Keys[]> DefaultMoveEventEndHint                     = [[]];
	private static readonly List<Keys[]> DefaultConvertSelectedTapsToMines           = [[]];
	private static readonly List<Keys[]> DefaultConvertSelectedTapsToFakes           = [[]];
	private static readonly List<Keys[]> DefaultConvertSelectedTapsToLifts           = [[]];
	private static readonly List<Keys[]> DefaultConvertSelectedMinesToTaps           = [[]];
	private static readonly List<Keys[]> DefaultConvertSelectedMinesToFakes          = [[]];
	private static readonly List<Keys[]> DefaultConvertSelectedMinesToLifts          = [[]];
	private static readonly List<Keys[]> DefaultConvertSelectedFakesToTaps           = [[]];
	private static readonly List<Keys[]> DefaultConvertSelectedLiftsToTaps           = [[]];
	private static readonly List<Keys[]> DefaultConvertSelectedHoldsToRolls          = [[]];
	private static readonly List<Keys[]> DefaultConvertSelectedHoldsToTaps           = [[]];
	private static readonly List<Keys[]> DefaultConvertSelectedHoldsToMines          = [[]];
	private static readonly List<Keys[]> DefaultConvertSelectedRollsToHolds          = [[]];
	private static readonly List<Keys[]> DefaultConvertSelectedRollsToTaps           = [[]];
	private static readonly List<Keys[]> DefaultConvertSelectedRollsToMines          = [[]];
	private static readonly List<Keys[]> DefaultConvertSelectedWarpsToNegativeStops  = [[]];
	private static readonly List<Keys[]> DefaultConvertSelectedNegativeStopsToWarps  = [[]];
	private static readonly List<Keys[]> DefaultSwapSelectedTapsAndMines             = [[]];
	private static readonly List<Keys[]> DefaultSwapSelectedTapsAndFakes             = [[]];
	private static readonly List<Keys[]> DefaultSwapSelectedTapsAndLifts             = [[]];
	private static readonly List<Keys[]> DefaultSwapSelectedMinesAndFakes            = [[]];
	private static readonly List<Keys[]> DefaultSwapSelectedMinesAndLifts            = [[]];
	private static readonly List<Keys[]> DefaultSwapSelectedHoldsAndRolls            = [[]];
	private static readonly List<Keys[]> DefaultSwapSelectedHoldsAndTaps             = [[]];
	private static readonly List<Keys[]> DefaultSwapSelectedHoldsAndMines            = [[]];
	private static readonly List<Keys[]> DefaultSwapSelectedRollsAndTaps             = [[]];
	private static readonly List<Keys[]> DefaultSwapSelectedRollsAndMines            = [[]];
	private static readonly List<Keys[]> DefaultSnapToNone                           = [[]];
	private static readonly List<Keys[]> DefaultSnapToQuarters                       = [[]];
	private static readonly List<Keys[]> DefaultSnapToEighths                        = [[]];
	private static readonly List<Keys[]> DefaultSnapToTwelfths                       = [[]];
	private static readonly List<Keys[]> DefaultSnapToSixteenths                     = [[]];
	private static readonly List<Keys[]> DefaultSnapToTwentyFourths                  = [[]];
	private static readonly List<Keys[]> DefaultSnapToThirtySeconds                  = [[]];
	private static readonly List<Keys[]> DefaultSnapToFortyEighths                   = [[]];
	private static readonly List<Keys[]> DefaultSnapToSixtyFourths                   = [[]];
	private static readonly List<Keys[]> DefaultSnapToOneHundredNinetySeconds        = [[]];
	private static readonly List<Keys[]> DefaultToggleWaveForm                       = [[]];
	private static readonly List<Keys[]> DefaultToggleDark                           = [[]];
	private static readonly List<Keys[]> DefaultAutoApplyAllSongAssets               = [[]];
	private static readonly List<Keys[]> DefaultAutoApplyUnsetSongAssets             = [[]];
	// @formatter:on

	#region Properties

	// Regex to generate the properties below.
	// Copy new Defaults below, then search for this in selection:
	// .*private static readonly List\<Keys\[\]\> Default([a-zA-Z0-9]+) +.*;
	// Replace with this:
	// ReSharper disable CommentTypo
	// \t[JsonInclude]\r\n\tpublic List<Keys[]> $1\r\n\t{\r\n\t\tget;\r\n\t\tset\r\n\t\t{\r\n\t\t\tfield = value;\r\n\t\t\tNotify(NotificationKeyBindingChanged, this, nameof($1));\r\n\t\t}\r\n\t} = Default$1;\r\n\r\n
	// ReSharper restore CommentTypo

	[JsonInclude]
	public List<Keys[]> Open
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(Open));
		}
	} = DefaultOpen;

	[JsonInclude]
	public List<Keys[]> OpenContainingFolder
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(OpenContainingFolder));
		}
	} = DefaultOpenContainingFolder;

	[JsonInclude]
	public List<Keys[]> SaveAs
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(SaveAs));
		}
	} = DefaultSaveAs;

	[JsonInclude]
	public List<Keys[]> Save
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(Save));
		}
	} = DefaultSave;

	[JsonInclude]
	public List<Keys[]> SavePackFile
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(SavePackFile));
		}
	} = DefaultSavePackFile;

	[JsonInclude]
	public List<Keys[]> New
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(New));
		}
	} = DefaultNew;

	[JsonInclude]
	public List<Keys[]> Reload
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(Reload));
		}
	} = DefaultReload;

	[JsonInclude]
	public List<Keys[]> Close
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(Close));
		}
	} = DefaultClose;

	[JsonInclude]
	public List<Keys[]> Exit
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(Exit));
		}
	} = DefaultExit;

	[JsonInclude]
	public List<Keys[]> Undo
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(Undo));
		}
	} = DefaultUndo;

	[JsonInclude]
	public List<Keys[]> Redo
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(Redo));
		}
	} = DefaultRedo;

	[JsonInclude]
	public List<Keys[]> SelectRowRange
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(SelectRowRange));
		}
	} = DefaultSelectRowRange;

	[JsonInclude]
	public List<Keys[]> SelectAllNotes
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(SelectAllNotes));
		}
	} = DefaultSelectAllNotes;

	[JsonInclude]
	public List<Keys[]> SelectAllTaps
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(SelectAllTaps));
		}
	} = DefaultSelectAllTaps;

	[JsonInclude]
	public List<Keys[]> SelectAllMines
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(SelectAllMines));
		}
	} = DefaultSelectAllMines;

	[JsonInclude]
	public List<Keys[]> SelectAllFakes
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(SelectAllFakes));
		}
	} = DefaultSelectAllFakes;

	[JsonInclude]
	public List<Keys[]> SelectAllLifts
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(SelectAllLifts));
		}
	} = DefaultSelectAllLifts;

	[JsonInclude]
	public List<Keys[]> SelectAllHolds
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(SelectAllHolds));
		}
	} = DefaultSelectAllHolds;

	[JsonInclude]
	public List<Keys[]> SelectAllRolls
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(SelectAllRolls));
		}
	} = DefaultSelectAllRolls;

	[JsonInclude]
	public List<Keys[]> SelectAllHoldsAndRolls
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(SelectAllHoldsAndRolls));
		}
	} = DefaultSelectAllHoldsAndRolls;

	[JsonInclude]
	public List<Keys[]> SelectAllCurrentPlayerNotes
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(SelectAllCurrentPlayerNotes));
		}
	} = DefaultSelectAllCurrentPlayerNotes;

	[JsonInclude]
	public List<Keys[]> SelectAllCurrentPlayerTaps
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(SelectAllCurrentPlayerTaps));
		}
	} = DefaultSelectAllCurrentPlayerTaps;

	[JsonInclude]
	public List<Keys[]> SelectAllCurrentPlayerMines
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(SelectAllCurrentPlayerMines));
		}
	} = DefaultSelectAllCurrentPlayerMines;

	[JsonInclude]
	public List<Keys[]> SelectAllCurrentPlayerFakes
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(SelectAllCurrentPlayerFakes));
		}
	} = DefaultSelectAllCurrentPlayerFakes;

	[JsonInclude]
	public List<Keys[]> SelectAllCurrentPlayerLifts
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(SelectAllCurrentPlayerLifts));
		}
	} = DefaultSelectAllCurrentPlayerLifts;

	[JsonInclude]
	public List<Keys[]> SelectAllCurrentPlayerHolds
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(SelectAllCurrentPlayerHolds));
		}
	} = DefaultSelectAllCurrentPlayerHolds;

	[JsonInclude]
	public List<Keys[]> SelectAllCurrentPlayerRolls
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(SelectAllCurrentPlayerRolls));
		}
	} = DefaultSelectAllCurrentPlayerRolls;

	[JsonInclude]
	public List<Keys[]> SelectAllCurrentPlayerHoldsAndRolls
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(SelectAllCurrentPlayerHoldsAndRolls));
		}
	} = DefaultSelectAllCurrentPlayerHoldsAndRolls;

	[JsonInclude]
	public List<Keys[]> SelectAllMiscEvents
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(SelectAllMiscEvents));
		}
	} = DefaultSelectAllMiscEvents;

	[JsonInclude]
	public List<Keys[]> SelectAll
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(SelectAll));
		}
	} = DefaultSelectAll;

	[JsonInclude]
	public List<Keys[]> SelectAllPatterns
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(SelectAllPatterns));
		}
	} = DefaultSelectAllPatterns;

	[JsonInclude]
	public List<Keys[]> Copy
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(Copy));
		}
	} = DefaultCopy;

	[JsonInclude]
	public List<Keys[]> Cut
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(Cut));
		}
	} = DefaultCut;

	[JsonInclude]
	public List<Keys[]> Paste
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(Paste));
		}
	} = DefaultPaste;

	[JsonInclude]
	public List<Keys[]> TogglePreview
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(TogglePreview));
		}
	} = DefaultTogglePreview;

	[JsonInclude]
	public List<Keys[]> ToggleAssistTick
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(ToggleAssistTick));
		}
	} = DefaultToggleAssistTick;

	[JsonInclude]
	public List<Keys[]> ToggleBeatTick
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(ToggleBeatTick));
		}
	} = DefaultToggleBeatTick;

	[JsonInclude]
	public List<Keys[]> DecreaseMusicRate
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(DecreaseMusicRate));
		}
	} = DefaultDecreaseMusicRate;

	[JsonInclude]
	public List<Keys[]> IncreaseMusicRate
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(IncreaseMusicRate));
		}
	} = DefaultIncreaseMusicRate;

	[JsonInclude]
	public List<Keys[]> PlayPause
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(PlayPause));
		}
	} = DefaultPlayPause;

	[JsonInclude]
	public List<Keys[]> CancelGoBack
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(CancelGoBack));
		}
	} = DefaultCancelGoBack;

	[JsonInclude]
	public List<Keys[]> ToggleNoteEntryMode
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(ToggleNoteEntryMode));
		}
	} = DefaultToggleNoteEntryMode;

	[JsonInclude]
	public List<Keys[]> ToggleSpacingMode
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(ToggleSpacingMode));
		}
	} = DefaultToggleSpacingMode;

	[JsonInclude]
	public List<Keys[]> TogglePlayer
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(TogglePlayer));
		}
	} = DefaultTogglePlayer;

	[JsonInclude]
	public List<Keys[]> SetPlayer1
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(SetPlayer1));
		}
	} = DefaultSetPlayer1;

	[JsonInclude]
	public List<Keys[]> SetPlayer2
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(SetPlayer2));
		}
	} = DefaultSetPlayer2;

	[JsonInclude]
	public List<Keys[]> SetPlayer3
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(SetPlayer3));
		}
	} = DefaultSetPlayer3;

	[JsonInclude]
	public List<Keys[]> SetPlayer4
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(SetPlayer4));
		}
	} = DefaultSetPlayer4;

	[JsonInclude]
	public List<Keys[]> ConvertSelectedNotesToPlayer1
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(ConvertSelectedNotesToPlayer1));
		}
	} = DefaultConvertSelectedNotesToPlayer1;

	[JsonInclude]
	public List<Keys[]> ConvertSelectedNotesToPlayer2
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(ConvertSelectedNotesToPlayer2));
		}
	} = DefaultConvertSelectedNotesToPlayer2;

	[JsonInclude]
	public List<Keys[]> ConvertSelectedNotesToPlayer3
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(ConvertSelectedNotesToPlayer3));
		}
	} = DefaultConvertSelectedNotesToPlayer3;

	[JsonInclude]
	public List<Keys[]> ConvertSelectedNotesToPlayer4
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(ConvertSelectedNotesToPlayer4));
		}
	} = DefaultConvertSelectedNotesToPlayer4;

	[JsonInclude]
	public List<Keys[]> SwapSelectedPlayer1And2Notes
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(SwapSelectedPlayer1And2Notes));
		}
	} = DefaultSwapSelectedPlayer1And2Notes;


	[JsonInclude]
	public List<Keys[]> SwapSelectedPlayer1And3Notes
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(SwapSelectedPlayer1And3Notes));
		}
	} = DefaultSwapSelectedPlayer1And3Notes;


	[JsonInclude]
	public List<Keys[]> SwapSelectedPlayer1And4Notes
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(SwapSelectedPlayer1And4Notes));
		}
	} = DefaultSwapSelectedPlayer1And4Notes;


	[JsonInclude]
	public List<Keys[]> SwapSelectedPlayer2And3Notes
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(SwapSelectedPlayer2And3Notes));
		}
	} = DefaultSwapSelectedPlayer2And3Notes;


	[JsonInclude]
	public List<Keys[]> SwapSelectedPlayer2And4Notes
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(SwapSelectedPlayer2And4Notes));
		}
	} = DefaultSwapSelectedPlayer2And4Notes;


	[JsonInclude]
	public List<Keys[]> SwapSelectedPlayer3And4Notes
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(SwapSelectedPlayer3And4Notes));
		}
	} = DefaultSwapSelectedPlayer3And4Notes;

	[JsonInclude]
	public List<Keys[]> OpenPreviousChart
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(OpenPreviousChart));
		}
	} = DefaultOpenPreviousChart;

	[JsonInclude]
	public List<Keys[]> OpenNextChart
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(OpenNextChart));
		}
	} = DefaultOpenNextChart;

	[JsonInclude]
	public List<Keys[]> CloseFocusedChart
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(CloseFocusedChart));
		}
	} = DefaultCloseFocusedChart;

	[JsonInclude]
	public List<Keys[]> KeepChartOpen
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(KeepChartOpen));
		}
	} = DefaultKeepChartOpen;

	[JsonInclude]
	public List<Keys[]> MoveFocusedChartLeft
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(MoveFocusedChartLeft));
		}
	} = DefaultMoveFocusedChartLeft;

	[JsonInclude]
	public List<Keys[]> MoveFocusedChartRight
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(MoveFocusedChartRight));
		}
	} = DefaultMoveFocusedChartRight;

	[JsonInclude]
	public List<Keys[]> FocusPreviousChart
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(FocusPreviousChart));
		}
	} = DefaultFocusPreviousChart;

	[JsonInclude]
	public List<Keys[]> FocusNextChart
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(FocusNextChart));
		}
	} = DefaultFocusNextChart;

	[JsonInclude]
	public List<Keys[]> DecreaseSnap
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(DecreaseSnap));
		}
	} = DefaultDecreaseSnap;

	[JsonInclude]
	public List<Keys[]> IncreaseSnap
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(IncreaseSnap));
		}
	} = DefaultIncreaseSnap;

	[JsonInclude]
	public List<Keys[]> MoveUp
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(MoveUp));
		}
	} = DefaultMoveUp;

	[JsonInclude]
	public List<Keys[]> MoveDown
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(MoveDown));
		}
	} = DefaultMoveDown;

	[JsonInclude]
	public List<Keys[]> MoveToPreviousRowWithSteps
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(MoveToPreviousRowWithSteps));
		}
	} = DefaultMoveToPreviousRowWithSteps;

	[JsonInclude]
	public List<Keys[]> MoveToNextRowWithSteps
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(MoveToNextRowWithSteps));
		}
	} = DefaultMoveToNextRowWithSteps;

	[JsonInclude]
	public List<Keys[]> MoveToPreviousRowWithEvent
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(MoveToPreviousRowWithEvent));
		}
	} = DefaultMoveToPreviousRowWithEvent;

	[JsonInclude]
	public List<Keys[]> MoveToNextRowWithEvent
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(MoveToNextRowWithEvent));
		}
	} = DefaultMoveToNextRowWithEvent;

	[JsonInclude]
	public List<Keys[]> MoveToStartOfStream
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(MoveToStartOfStream));
		}
	} = DefaultMoveToStartOfStream;

	[JsonInclude]
	public List<Keys[]> MoveToEndOfStream
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(MoveToEndOfStream));
		}
	} = DefaultMoveToEndOfStream;

	[JsonInclude]
	public List<Keys[]> MoveToPreviousMeasure
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(MoveToPreviousMeasure));
		}
	} = DefaultMoveToPreviousMeasure;

	[JsonInclude]
	public List<Keys[]> MoveToNextMeasure
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(MoveToNextMeasure));
		}
	} = DefaultMoveToNextMeasure;

	[JsonInclude]
	public List<Keys[]> MoveToChartStart
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(MoveToChartStart));
		}
	} = DefaultMoveToChartStart;

	[JsonInclude]
	public List<Keys[]> MoveToChartEnd
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(MoveToChartEnd));
		}
	} = DefaultMoveToChartEnd;

	[JsonInclude]
	public List<Keys[]> MoveToNextLabel
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(MoveToNextLabel));
		}
	} = DefaultMoveToNextLabel;

	[JsonInclude]
	public List<Keys[]> MoveToPreviousLabel
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(MoveToPreviousLabel));
		}
	} = DefaultMoveToPreviousLabel;

	[JsonInclude]
	public List<Keys[]> MoveToNextPattern
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(MoveToNextPattern));
		}
	} = DefaultMoveToNextPattern;

	[JsonInclude]
	public List<Keys[]> MoveToPreviousPattern
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(MoveToPreviousPattern));
		}
	} = DefaultMoveToPreviousPattern;

	[JsonInclude]
	public List<Keys[]> RegenerateAllPatternsFixedSeeds
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(RegenerateAllPatternsFixedSeeds));
		}
	} = DefaultRegenerateAllPatternsFixedSeeds;

	[JsonInclude]
	public List<Keys[]> RegenerateAllPatternsNewSeeds
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(RegenerateAllPatternsNewSeeds));
		}
	} = DefaultRegenerateAllPatternsNewSeeds;

	[JsonInclude]
	public List<Keys[]> RegenerateSelectedPatternsFixedSeeds
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(RegenerateSelectedPatternsFixedSeeds));
		}
	} = DefaultRegenerateSelectedPatternsFixedSeeds;

	[JsonInclude]
	public List<Keys[]> RegenerateSelectedPatternsNewSeeds
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(RegenerateSelectedPatternsNewSeeds));
		}
	} = DefaultRegenerateSelectedPatternsNewSeeds;

	[JsonInclude]
	public List<Keys[]> Delete
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(Delete));
		}
	} = DefaultDelete;

	[JsonInclude]
	public List<Keys[]> ShiftLeft
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(ShiftLeft));
		}
	} = DefaultShiftLeft;

	[JsonInclude]
	public List<Keys[]> ShiftLeftAndWrap
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(ShiftLeftAndWrap));
		}
	} = DefaultShiftLeftAndWrap;

	[JsonInclude]
	public List<Keys[]> ShiftRight
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(ShiftRight));
		}
	} = DefaultShiftRight;

	[JsonInclude]
	public List<Keys[]> ShiftRightAndWrap
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(ShiftRightAndWrap));
		}
	} = DefaultShiftRightAndWrap;

	[JsonInclude]
	public List<Keys[]> ShiftEarlier
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(ShiftEarlier));
		}
	} = DefaultShiftEarlier;

	[JsonInclude]
	public List<Keys[]> ShiftLater
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(ShiftLater));
		}
	} = DefaultShiftLater;

	[JsonInclude]
	public List<Keys[]> Mirror
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(Mirror));
		}
	} = DefaultMirror;

	[JsonInclude]
	public List<Keys[]> Flip
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(Flip));
		}
	} = DefaultFlip;

	[JsonInclude]
	public List<Keys[]> MirrorAndFlip
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(MirrorAndFlip));
		}
	} = DefaultMirrorAndFlip;

	[JsonInclude]
	public List<Keys[]> Arrow0
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(Arrow0));
		}
	} = DefaultArrow0;

	[JsonInclude]
	public List<Keys[]> Arrow1
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(Arrow1));
		}
	} = DefaultArrow1;

	[JsonInclude]
	public List<Keys[]> Arrow2
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(Arrow2));
		}
	} = DefaultArrow2;

	[JsonInclude]
	public List<Keys[]> Arrow3
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(Arrow3));
		}
	} = DefaultArrow3;

	[JsonInclude]
	public List<Keys[]> Arrow4
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(Arrow4));
		}
	} = DefaultArrow4;

	[JsonInclude]
	public List<Keys[]> Arrow5
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(Arrow5));
		}
	} = DefaultArrow5;

	[JsonInclude]
	public List<Keys[]> Arrow6
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(Arrow6));
		}
	} = DefaultArrow6;

	[JsonInclude]
	public List<Keys[]> Arrow7
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(Arrow7));
		}
	} = DefaultArrow7;

	[JsonInclude]
	public List<Keys[]> Arrow8
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(Arrow8));
		}
	} = DefaultArrow8;

	[JsonInclude]
	public List<Keys[]> Arrow9
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(Arrow9));
		}
	} = DefaultArrow9;

	[JsonInclude]
	public List<Keys[]> ArrowModification
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(ArrowModification));
		}
	} = DefaultArrowModification;

	[JsonInclude]
	public List<Keys[]> ScrollZoom
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(ScrollZoom));
		}
	} = DefaultScrollZoom;

	[JsonInclude]
	public List<Keys[]> ScrollSpacing
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(ScrollSpacing));
		}
	} = DefaultScrollSpacing;

	[JsonInclude]
	public List<Keys[]> MouseSelectionControlBehavior
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(MouseSelectionControlBehavior));
		}
	} = DefaultMouseSelectionControlBehavior;

	[JsonInclude]
	public List<Keys[]> MouseSelectionShiftBehavior
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(MouseSelectionShiftBehavior));
		}
	} = DefaultMouseSelectionShiftBehavior;

	[JsonInclude]
	public List<Keys[]> MouseSelectionAltBehavior
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(MouseSelectionAltBehavior));
		}
	} = DefaultMouseSelectionAltBehavior;

	[JsonInclude]
	public List<Keys[]> LockReceptorMoveAxis
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(LockReceptorMoveAxis));
		}
	} = DefaultLockReceptorMoveAxis;

	[JsonInclude]
	public List<Keys[]> AddEventTempo
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(AddEventTempo));
		}
	} = DefaultAddEventTempo;

	[JsonInclude]
	public List<Keys[]> AddEventInterpolatedScrollRate
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(AddEventInterpolatedScrollRate));
		}
	} = DefaultAddEventInterpolatedScrollRate;

	[JsonInclude]
	public List<Keys[]> AddEventScrollRate
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(AddEventScrollRate));
		}
	} = DefaultAddEventScrollRate;

	[JsonInclude]
	public List<Keys[]> AddEventStop
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(AddEventStop));
		}
	} = DefaultAddEventStop;

	[JsonInclude]
	public List<Keys[]> AddEventDelay
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(AddEventDelay));
		}
	} = DefaultAddEventDelay;

	[JsonInclude]
	public List<Keys[]> AddEventWarp
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(AddEventWarp));
		}
	} = DefaultAddEventWarp;

	[JsonInclude]
	public List<Keys[]> AddEventFakeRegion
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(AddEventFakeRegion));
		}
	} = DefaultAddEventFakeRegion;

	[JsonInclude]
	public List<Keys[]> AddEventTicks
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(AddEventTicks));
		}
	} = DefaultAddEventTicks;

	[JsonInclude]
	public List<Keys[]> AddEventComboMultipliers
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(AddEventComboMultipliers));
		}
	} = DefaultAddEventComboMultipliers;

	[JsonInclude]
	public List<Keys[]> AddEventTimeSignature
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(AddEventTimeSignature));
		}
	} = DefaultAddEventTimeSignature;

	[JsonInclude]
	public List<Keys[]> AddEventLabel
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(AddEventLabel));
		}
	} = DefaultAddEventLabel;

	[JsonInclude]
	public List<Keys[]> AddEventAttack
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(AddEventAttack));
		}
	} = DefaultAddEventAttack;

	[JsonInclude]
	public List<Keys[]> AddEventPattern
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(AddEventPattern));
		}
	} = DefaultAddEventPattern;

	[JsonInclude]
	public List<Keys[]> MoveEventPreview
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(MoveEventPreview));
		}
	} = DefaultMoveEventPreview;

	[JsonInclude]
	public List<Keys[]> MoveEventEndHint
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(MoveEventEndHint));
		}
	} = DefaultMoveEventEndHint;

	[JsonInclude]
	public List<Keys[]> ConvertSelectedTapsToMines
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(ConvertSelectedTapsToMines));
		}
	} = DefaultConvertSelectedTapsToMines;

	[JsonInclude]
	public List<Keys[]> ConvertSelectedTapsToFakes
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(ConvertSelectedTapsToFakes));
		}
	} = DefaultConvertSelectedTapsToFakes;

	[JsonInclude]
	public List<Keys[]> ConvertSelectedTapsToLifts
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(ConvertSelectedTapsToLifts));
		}
	} = DefaultConvertSelectedTapsToLifts;

	[JsonInclude]
	public List<Keys[]> ConvertSelectedMinesToTaps
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(ConvertSelectedMinesToTaps));
		}
	} = DefaultConvertSelectedMinesToTaps;

	[JsonInclude]
	public List<Keys[]> ConvertSelectedMinesToFakes
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(ConvertSelectedMinesToFakes));
		}
	} = DefaultConvertSelectedMinesToFakes;

	[JsonInclude]
	public List<Keys[]> ConvertSelectedMinesToLifts
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(ConvertSelectedMinesToLifts));
		}
	} = DefaultConvertSelectedMinesToLifts;

	[JsonInclude]
	public List<Keys[]> ConvertSelectedFakesToTaps
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(ConvertSelectedFakesToTaps));
		}
	} = DefaultConvertSelectedFakesToTaps;

	[JsonInclude]
	public List<Keys[]> ConvertSelectedLiftsToTaps
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(ConvertSelectedLiftsToTaps));
		}
	} = DefaultConvertSelectedLiftsToTaps;

	[JsonInclude]
	public List<Keys[]> ConvertSelectedHoldsToRolls
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(ConvertSelectedHoldsToRolls));
		}
	} = DefaultConvertSelectedHoldsToRolls;

	[JsonInclude]
	public List<Keys[]> ConvertSelectedHoldsToTaps
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(ConvertSelectedHoldsToTaps));
		}
	} = DefaultConvertSelectedHoldsToTaps;

	[JsonInclude]
	public List<Keys[]> ConvertSelectedHoldsToMines
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(ConvertSelectedHoldsToMines));
		}
	} = DefaultConvertSelectedHoldsToMines;

	[JsonInclude]
	public List<Keys[]> ConvertSelectedRollsToHolds
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(ConvertSelectedRollsToHolds));
		}
	} = DefaultConvertSelectedRollsToHolds;

	[JsonInclude]
	public List<Keys[]> ConvertSelectedRollsToTaps
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(ConvertSelectedRollsToTaps));
		}
	} = DefaultConvertSelectedRollsToTaps;

	[JsonInclude]
	public List<Keys[]> ConvertSelectedRollsToMines
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(ConvertSelectedRollsToMines));
		}
	} = DefaultConvertSelectedRollsToMines;

	[JsonInclude]
	public List<Keys[]> ConvertSelectedWarpsToNegativeStops
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(ConvertSelectedWarpsToNegativeStops));
		}
	} = DefaultConvertSelectedWarpsToNegativeStops;

	[JsonInclude]
	public List<Keys[]> ConvertSelectedNegativeStopsToWarps
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(ConvertSelectedNegativeStopsToWarps));
		}
	} = DefaultConvertSelectedNegativeStopsToWarps;

	[JsonInclude]
	public List<Keys[]> SwapSelectedTapsAndMines
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(SwapSelectedTapsAndMines));
		}
	} = DefaultSwapSelectedTapsAndMines;

	[JsonInclude]
	public List<Keys[]> SwapSelectedTapsAndFakes
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(SwapSelectedTapsAndFakes));
		}
	} = DefaultSwapSelectedTapsAndFakes;

	[JsonInclude]
	public List<Keys[]> SwapSelectedTapsAndLifts
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(SwapSelectedTapsAndLifts));
		}
	} = DefaultSwapSelectedTapsAndLifts;

	[JsonInclude]
	public List<Keys[]> SwapSelectedMinesAndFakes
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(SwapSelectedMinesAndFakes));
		}
	} = DefaultSwapSelectedMinesAndFakes;

	[JsonInclude]
	public List<Keys[]> SwapSelectedMinesAndLifts
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(SwapSelectedMinesAndLifts));
		}
	} = DefaultSwapSelectedMinesAndLifts;

	[JsonInclude]
	public List<Keys[]> SwapSelectedHoldsAndRolls
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(SwapSelectedHoldsAndRolls));
		}
	} = DefaultSwapSelectedHoldsAndRolls;

	[JsonInclude]
	public List<Keys[]> SwapSelectedHoldsAndTaps
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(SwapSelectedHoldsAndTaps));
		}
	} = DefaultSwapSelectedHoldsAndTaps;

	[JsonInclude]
	public List<Keys[]> SwapSelectedHoldsAndMines
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(SwapSelectedHoldsAndMines));
		}
	} = DefaultSwapSelectedHoldsAndMines;

	[JsonInclude]
	public List<Keys[]> SwapSelectedRollsAndTaps
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(SwapSelectedRollsAndTaps));
		}
	} = DefaultSwapSelectedRollsAndTaps;

	[JsonInclude]
	public List<Keys[]> SwapSelectedRollsAndMines
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(SwapSelectedRollsAndMines));
		}
	} = DefaultSwapSelectedRollsAndMines;

	[JsonInclude]
	public List<Keys[]> SnapToNone
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(SnapToNone));
		}
	} = DefaultSnapToNone;

	[JsonInclude]
	public List<Keys[]> SnapToQuarters
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(SnapToQuarters));
		}
	} = DefaultSnapToQuarters;

	[JsonInclude]
	public List<Keys[]> SnapToEighths
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(SnapToEighths));
		}
	} = DefaultSnapToEighths;

	[JsonInclude]
	public List<Keys[]> SnapToTwelfths
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(SnapToTwelfths));
		}
	} = DefaultSnapToTwelfths;

	[JsonInclude]
	public List<Keys[]> SnapToSixteenths
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(SnapToSixteenths));
		}
	} = DefaultSnapToSixteenths;

	[JsonInclude]
	public List<Keys[]> SnapToTwentyFourths
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(SnapToTwentyFourths));
		}
	} = DefaultSnapToTwentyFourths;

	[JsonInclude]
	public List<Keys[]> SnapToThirtySeconds
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(SnapToThirtySeconds));
		}
	} = DefaultSnapToThirtySeconds;

	[JsonInclude]
	public List<Keys[]> SnapToFortyEighths
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(SnapToFortyEighths));
		}
	} = DefaultSnapToFortyEighths;

	[JsonInclude]
	public List<Keys[]> SnapToSixtyFourths
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(SnapToSixtyFourths));
		}
	} = DefaultSnapToSixtyFourths;

	[JsonInclude]
	public List<Keys[]> SnapToOneHundredNinetySeconds
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(SnapToOneHundredNinetySeconds));
		}
	} = DefaultSnapToOneHundredNinetySeconds;

	[JsonInclude]
	public List<Keys[]> ToggleWaveForm
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(ToggleWaveForm));
		}
	} = DefaultToggleWaveForm;

	[JsonInclude]
	public List<Keys[]> ToggleDark
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(ToggleDark));
		}
	} = DefaultToggleDark;

	[JsonInclude]
	public List<Keys[]> AutoApplyAllSongAssets
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(AutoApplyAllSongAssets));
		}
	} = DefaultAutoApplyAllSongAssets;

	[JsonInclude]
	public List<Keys[]> AutoApplyUnsetSongAssets
	{
		get;
		set
		{
			field = value;
			Notify(NotificationKeyBindingChanged, this, nameof(AutoApplyUnsetSongAssets));
		}
	} = DefaultAutoApplyUnsetSongAssets;

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
					value.Add([]);

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

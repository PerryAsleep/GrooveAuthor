using System.Collections.Generic;
using Fumen;
using Fumen.ChartDefinition;
using Fumen.Converters;
using Microsoft.Xna.Framework.Graphics;
using MonoGameExtensions;
using static StepManiaEditor.EditorAttackEvent;

namespace StepManiaEditor;

internal sealed class EditorAttackEvent : EditorEvent, IObserver<EditorModifier>
{
	public static readonly string EventShortDescription =
		"Modifiers to apply during gameplay.";

	public static readonly string WidgetHelp =
		"Attack.\n" +
		EventShortDescription;

	/// <summary>
	/// Modifer strings from Stepmania. This is not exhaustive as
	/// text like tempos and noteskin names can be used as attack modifiers.
	/// </summary>
	public static readonly string[] ModifierTypes =
	{
		"Alternate",
		"AttackMines",
		"AttenuateX",
		"AttenuateY",
		"AttenuateZ",
		"Backwards",
		"Bar",
		"Battery",
		"Beat",
		"BeatMult",
		"BeatOffset",
		"BeatPeriod",
		"BeatY",
		"BeatYMult",
		"BeatYOffset",
		"BeatYPeriod",
		"BeatZ",
		"BeatZMult",
		"BeatZOffset",
		"BeatZPeriod",
		"Big",
		"Blind",
		"Blink",
		"BMRize",
		"Boomerang",
		"Boost",
		"Bounce",
		"BounceOffset",
		"BouncePeriod",
		"BounceZ",
		"BounceZOffset",
		"BounceZPeriod",
		"Brake",
		"Bumpy",
		"Bumpy1",
		"Bumpy2",
		"Bumpy3",
		"Bumpy4",
		"Bumpy5",
		"Bumpy6",
		"Bumpy7",
		"Bumpy8",
		"Bumpy9",
		"Bumpy10",
		"Bumpy11",
		"Bumpy12",
		"Bumpy13",
		"Bumpy14",
		"Bumpy15",
		"Bumpy16",
		"BumpyOffset",
		"BumpyPeriod",
		"BumpyX",
		"BumpyXOffset",
		"BumpyXPeriod",
		"Centered",
		"ClearAll",
		"Confusion",
		"ConfusionOffset",
		"ConfusionOffset1",
		"ConfusionOffset2",
		"ConfusionOffset3",
		"ConfusionOffset4",
		"ConfusionOffset5",
		"ConfusionOffset6",
		"ConfusionOffset7",
		"ConfusionOffset8",
		"ConfusionOffset9",
		"ConfusionOffset10",
		"ConfusionOffset11",
		"ConfusionOffset12",
		"ConfusionOffset13",
		"ConfusionOffset14",
		"ConfusionOffset15",
		"ConfusionOffset16",
		"ConfusionX",
		"ConfusionXOffset",
		"ConfusionXOffset1",
		"ConfusionXOffset2",
		"ConfusionXOffset3",
		"ConfusionXOffset4",
		"ConfusionXOffset5",
		"ConfusionXOffset6",
		"ConfusionXOffset7",
		"ConfusionXOffset8",
		"ConfusionXOffset9",
		"ConfusionXOffset10",
		"ConfusionXOffset11",
		"ConfusionXOffset12",
		"ConfusionXOffset13",
		"ConfusionXOffset14",
		"ConfusionXOffset15",
		"ConfusionXOffset16",
		"ConfusionY",
		"ConfusionYOffset",
		"ConfusionYOffset1",
		"ConfusionYOffset2",
		"ConfusionYOffset3",
		"ConfusionYOffset4",
		"ConfusionYOffset5",
		"ConfusionYOffset6",
		"ConfusionYOffset7",
		"ConfusionYOffset8",
		"ConfusionYOffset9",
		"ConfusionYOffset10",
		"ConfusionYOffset11",
		"ConfusionYOffset12",
		"ConfusionYOffset13",
		"ConfusionYOffset14",
		"ConfusionYOffset15",
		"ConfusionYOffset16",
		"Converge",
		"Cosecant",
		"Cover",
		"Cross",
		"Dark",
		"Dark1",
		"Dark2",
		"Dark3",
		"Dark4",
		"Dark5",
		"Dark6",
		"Dark7",
		"Dark8",
		"Dark9",
		"Dark10",
		"Dark11",
		"Dark12",
		"Dark13",
		"Dark14",
		"Dark15",
		"Dark16",
		"Death",
		"Digital",
		"DigitalOffset",
		"DigitalPeriod",
		"DigitalSteps",
		"DigitalZ",
		"DigitalZOffset",
		"DigitalZPeriod",
		"DigitalZSteps",
		"Distant",
		"Dizzy",
		"DizzyHolds",
		"DrawSize",
		"DrawSizeBack",
		"Drunk",
		"DrunkOffset",
		"DrunkPeriod",
		"DrunkSpeed",
		"DrunkZ",
		"DrunkZOffset",
		"DrunkZPeriod",
		"DrunkZSpeed",
		"DwiWave",
		"Echo",
		"Expand",
		"ExpandPeriod",
		"FailArcade",
		"FailAtEnd",
		"FailDefault",
		"FailEndOfSong",
		"FailImmediate",
		"FailImmediateContinue",
		"FailOff",
		"Flip",
		"Floored",
		"Hallway",
		"Hidden",
		"HiddenOffset",
		"HideAllLights",
		"HideBassLights",
		"HideMarqueeLights",
		"HoldRolls",
		"HyperShuffle",
		"Incoming",
		"Invert",
		"Land",
		"Left",
		"Life",
		"LifeTime",
		"Little",
		"Lives",
		"LRMirror",
		"Mines",
		"Mini",
		"Mirror",
		"ModTimerBeat",
		"ModTimerDefault",
		"ModTimerGame",
		"ModTimerMult",
		"ModTimerOffset",
		"ModTimerSong",
		"MoveX1",
		"MoveX2",
		"MoveX3",
		"MoveX4",
		"MoveX5",
		"MoveX6",
		"MoveX7",
		"MoveX8",
		"MoveX9",
		"MoveX10",
		"MoveX11",
		"MoveX12",
		"MoveX13",
		"MoveX14",
		"MoveX15",
		"MoveX16",
		"MoveY1",
		"MoveY2",
		"MoveY3",
		"MoveY4",
		"MoveY5",
		"MoveY6",
		"MoveY7",
		"MoveY8",
		"MoveY9",
		"MoveY10",
		"MoveY11",
		"MoveY12",
		"MoveY13",
		"MoveY14",
		"MoveY15",
		"MoveY16",
		"MoveZ1",
		"MoveZ2",
		"MoveZ3",
		"MoveZ4",
		"MoveZ5",
		"MoveZ6",
		"MoveZ7",
		"MoveZ8",
		"MoveZ9",
		"MoveZ10",
		"MoveZ11",
		"MoveZ12",
		"MoveZ13",
		"MoveZ14",
		"MoveZ15",
		"MoveZ16",
		"MuteOnError",
		"NoAttacks",
		"NoFakes",
		"NoHands",
		"NoHideLights",
		"NoHolds",
		"NoJumps",
		"NoLifts",
		"NoMines",
		"NoQuads",
		"NoRecover",
		"Normal-Drain",
		"NoRolls",
		"NoStretch",
		"NoteSkin",
		"Overhead",
		"ParabolaX",
		"ParabolaY",
		"ParabolaZ",
		"PassMark",
		"Planted",
		"PlayerAutoplay",
		"Power-Drop",
		"PulseInner",
		"PulseOffset",
		"PulseOuter",
		"PulsePeriod",
		"Quick",
		"Random",
		"RandomAttacks",
		"RandomSpeed",
		"RandomVanish",
		"ResetSpeed",
		"Reverse",
		"Reverse1",
		"Reverse2",
		"Reverse3",
		"Reverse4",
		"Reverse5",
		"Reverse6",
		"Reverse7",
		"Reverse8",
		"Reverse9",
		"Reverse10",
		"Reverse11",
		"Reverse12",
		"Reverse13",
		"Reverse14",
		"Reverse15",
		"Reverse16",
		"Right",
		"Roll",
		"SawTooth",
		"SawToothPeriod",
		"SawToothZ",
		"SawToothZPeriod",
		"ShrinkLinear",
		"ShrinkMult",
		"Shuffle",
		"Skew",
		"Skippy",
		"SortShuffle",
		"Space",
		"Split",
		"Square",
		"SquareOffset",
		"SquarePeriod",
		"SquareZ",
		"SquareZOffset",
		"SquareZPeriod",
		"Stealth",
		"Stealth1",
		"Stealth2",
		"Stealth3",
		"Stealth4",
		"Stealth5",
		"Stealth6",
		"Stealth7",
		"Stealth8",
		"Stealth9",
		"Stealth10",
		"Stealth11",
		"Stealth12",
		"Stealth13",
		"Stealth14",
		"Stealth15",
		"Stealth16",
		"StealthPastReceptors",
		"StealthType",
		"Stomp",
		"Sudden",
		"SuddenDeath",
		"SuddenOffset",
		"SuperShuffle",
		"TanBumpy",
		"TanBumpyOffset",
		"TanBumpyPeriod",
		"TanBumpyX",
		"TanBumpyXOffset",
		"TanBumpyXPeriod",
		"TanDigital",
		"TanDigitalOffset",
		"TanDigitalPeriod",
		"TanDigitalSteps",
		"TanDigitalZ",
		"TanDigitalZOffset",
		"TanDigitalZPeriod",
		"TanDigitalZSteps",
		"TanDrunk",
		"TanDrunkOffset",
		"TanDrunkPeriod",
		"TanDrunkSpeed",
		"TanDrunkZ",
		"TanDrunkZOffset",
		"TanDrunkZPeriod",
		"TanDrunkZSpeed",
		"TanExpand",
		"TanExpandPeriod",
		"TanTipsy",
		"TanTipsyOffset",
		"TanTipsySpeed",
		"TanTornado",
		"TanTornadoOffset",
		"TanTornadoPeriod",
		"TanTornadoZ",
		"TanTornadoZOffset",
		"TanTornadoZPeriod",
		"Tilt",
		"Tiny",
		"Tiny1",
		"Tiny2",
		"Tiny3",
		"Tiny4",
		"Tiny5",
		"Tiny6",
		"Tiny7",
		"Tiny8",
		"Tiny9",
		"Tiny10",
		"Tiny11",
		"Tiny12",
		"Tiny13",
		"Tiny14",
		"Tiny15",
		"Tiny16",
		"Tipsy",
		"TipsyOffset",
		"TipsySpeed",
		"Tornado",
		"TornadoOffset",
		"TornadoPeriod",
		"TornadoZ",
		"TornadoZOffset",
		"TornadoZPeriod",
		"Turn",
		"Twirl",
		"Twister",
		"UDMirror",
		"VisualDelay",
		"W1",
		"W2",
		"W3",
		"W4",
		"W5",
		"Wave",
		"WavePeriod",
		"Wide",
		"XMode",
		"ZBuffer",
		"ZigZag",
		"ZigZagOffset",
		"ZigZagPeriod",
		"ZigZagZ",
		"ZigZagZOffset",
		"ZigZagZPeriod",
	};

	/// <summary>
	/// Class for wrapping an Attack Modifier and notifying the EditorAttack when it changes.
	/// </summary>
	internal sealed class EditorModifier : Notifier<EditorModifier>
	{
		public const string NotificationModiferChanged = "ModiferChanged";
		public readonly Modifier Modifier;

		public EditorModifier(Modifier modifier)
		{
			Modifier = modifier;
		}

		public string Name
		{
			get => Modifier.Name;
			set
			{
				if (Modifier.Name != value)
				{
					Modifier.Name = value;
					Notify(NotificationModiferChanged, this);
				}
			}
		}

		public double Level
		{
			get => Modifier.Level;
			set
			{
				if (!Modifier.Level.DoubleEquals(value))
				{
					Modifier.Level = value;
					Notify(NotificationModiferChanged, this);
				}
			}
		}

		public double Speed
		{
			get => Modifier.Speed;
			set
			{
				if (!Modifier.Speed.DoubleEquals(value))
				{
					Modifier.Speed = value;
					Notify(NotificationModiferChanged, this);
				}
			}
		}

		public double LengthSeconds
		{
			get => Modifier.LengthSeconds;
			set
			{
				if (!Modifier.LengthSeconds.DoubleEquals(value))
				{
					Modifier.LengthSeconds = value;
					Notify(NotificationModiferChanged, this);
				}
			}
		}
	}

	/// <summary>
	/// Underlying Attack event.
	/// </summary>
	private readonly Attack AttackEvent;

	/// <summary>
	/// Wrappers for the Attack's Modifiers.
	/// </summary>
	private readonly List<EditorModifier> EditorModifiers;

	/// <summary>
	/// Flag for whether the misc event widget width is dirty.
	/// </summary>
	private bool WidthDirty;

	/// <remarks>
	/// This lazily updates the width if it is dirty.
	/// This is a bit of hack because in order to determine the width we need to call into
	/// ImGui but that is not a thread-safe operation. If we were to set the width when
	/// loading the chart for example, this could crash. By lazily setting it we avoid this
	/// problem as long as we assume the caller of GetW() happens on the main thread.
	/// </remarks>
	private double WidthInternal;

	public override double W
	{
		get
		{
			if (WidthDirty)
			{
				WidthInternal = ImGuiLayoutUtils.GetMiscEditorEventStringWidth(GetMiscEventText());
				WidthDirty = false;
			}

			return WidthInternal;
		}
		set => WidthInternal = value;
	}

	public override double H
	{
		get => ImGuiLayoutUtils.GetMiscEditorEventHeight();
		set { }
	}

	public static bool IsValidModString(string v)
	{
		if (string.IsNullOrWhiteSpace(v))
			return false;
		if (v.Contains(' ')
		    || v.Contains('\r')
		    || v.Contains('\n')
		    || v.Contains('\t')
		    || v.Contains(' ')
		    || v.Contains(',')
		    || v.Contains(MSDFile.ValueStartMarker)
		    || v.Contains(MSDFile.ValueEndMarker)
		    || v.Contains(MSDFile.ParamMarker)
		    || v.Contains(MSDFile.EscapeMarker)
		    || v.Contains(MSDFile.CommentChar))
			return false;
		return true;
	}

	public EditorAttackEvent(EventConfig config, Attack chartEvent) : base(config)
	{
		AttackEvent = chartEvent;
		EditorModifiers = new List<EditorModifier>();
		foreach (var modifier in AttackEvent.Modifiers)
		{
			var mod = new EditorModifier(modifier);
			mod.AddObserver(this);
			EditorModifiers.Add(mod);
		}

		WidthDirty = true;
	}

	public IReadOnlyList<EditorModifier> GetModifiers()
	{
		return EditorModifiers;
	}

	public void AddModifier(Modifier modifier)
	{
		var mod = new EditorModifier(modifier);
		mod.AddObserver(this);
		EditorModifiers.Add(mod);
		AttackEvent.Modifiers.Add(modifier);
		WidthDirty = true;
	}

	public int IndexOf(Modifier modifier)
	{
		for (var i = 0; i < EditorModifiers.Count; i++)
		{
			if (EditorModifiers[i].Modifier == modifier)
			{
				return i;
			}
		}

		return -1;
	}

	public void InsertModifier(int index, EditorModifier modifier)
	{
		modifier.AddObserver(this);
		EditorModifiers.Insert(index, modifier);
		AttackEvent.Modifiers.Insert(index, modifier.Modifier);
		WidthDirty = true;
	}

	public void RemoveModifier(Modifier modifier)
	{
		for (var i = 0; i < EditorModifiers.Count; i++)
		{
			if (EditorModifiers[i].Modifier == modifier)
			{
				EditorModifiers[i].RemoveObserver(this);
				EditorModifiers.RemoveAt(i);
				AttackEvent.Modifiers.RemoveAt(i);
				WidthDirty = true;
				return;
			}
		}
	}

	public void RemoveModifier(EditorModifier modifier)
	{
		for (var i = 0; i < EditorModifiers.Count; i++)
		{
			if (EditorModifiers[i] == modifier)
			{
				EditorModifiers[i].RemoveObserver(this);
				EditorModifiers.RemoveAt(i);
				AttackEvent.Modifiers.RemoveAt(i);
				WidthDirty = true;
				return;
			}
		}
	}

	public string GetMiscEventText()
	{
		if (AttackEvent.Modifiers.Count == 0)
			return "No Mods";
		if (AttackEvent.Modifiers.Count == 1)
			return SMCommon.GetModString(AttackEvent.Modifiers[0], false, true);
		return "Multiple Mods";
	}

	public override string GetShortTypeName()
	{
		return "Attack";
	}

	public override bool IsMiscEvent()
	{
		return true;
	}

	public override bool IsSelectableWithoutModifiers()
	{
		return false;
	}

	public override bool IsSelectableWithModifiers()
	{
		return true;
	}

	public override void Draw(TextureAtlas textureAtlas, SpriteBatch spriteBatch, ArrowGraphicManager arrowGraphicManager)
	{
		if (Alpha <= 0.0f)
			return;
		ImGuiLayoutUtils.MiscEditorEventAttackWidget(
			GetImGuiId(),
			this,
			(int)X, (int)Y, (int)W,
			Utils.UIAttackColorRGBA,
			IsSelected(),
			Alpha,
			WidgetHelp,
			() => { EditorChart.OnAttackEventRequestEdit(this); });
	}

	public void OnNotify(string eventId, EditorModifier notifier, object payload)
	{
		WidthDirty = true;
	}
}

using Fumen;
using Fumen.ChartDefinition;
using ImGuiNET;
using static StepManiaEditor.ImGuiUtils;

namespace StepManiaEditor.UI;

/// <summary>
/// Class for drawing information about an EditorAttackEvent in a chart.
/// </summary>
internal sealed class UIAttackEvent : UIWindow
{
	private static readonly string[] AttackTypes =
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

	private static readonly int TitleColumnWidth = UiScaled(80);
	private static readonly int DefaultWidth = UiScaled(460);

	private Editor Editor;

	public static UIAttackEvent Instance { get; } = new();

	private UIAttackEvent() : base("Attack Event Properties")
	{
	}

	public void Init(Editor editor)
	{
		Editor = editor;
	}

	public override void Open(bool focus)
	{
		Preferences.Instance.ShowAttackEventWindow = true;
		if (focus)
			Focus();
	}

	public override void Close()
	{
		Preferences.Instance.ShowAttackEventWindow = false;
	}

	public void Draw(EditorAttackEvent attackEvent)
	{
		if (attackEvent == null)
		{
			Preferences.Instance.ShowAttackEventWindow = false;
		}

		if (!Preferences.Instance.ShowAttackEventWindow)
			return;

		var attack = attackEvent!.GetAttack();

		if (BeginWindow(WindowTitle, ref Preferences.Instance.ShowAttackEventWindow, DefaultWidth))
		{
			var disabled = !Editor.CanEdit();
			if (disabled)
				PushDisabled();

			if (ImGuiLayoutUtils.BeginTable("AttackEventTable", TitleColumnWidth))
			{
				ImGuiLayoutUtils.DrawRowChartPosition("Position", Editor, attackEvent,
					"The position of the attack.");

				if (ImGuiLayoutUtils.DrawRowButton("Add Mod", "Add Modifier", "Add a new modifier to this attack."))
				{
					ActionQueue.Instance.Do(new ActionAddModToAttack(attackEvent));
				}

				ImGuiLayoutUtils.EndTable();
			}

			for (var i = 0; i < attack.Modifiers.Count; i++)
			{
				ImGui.Separator();
				if (ImGuiLayoutUtils.BeginTable($"ModTable{i}", TitleColumnWidth))
				{
					var mod = attack.Modifiers[i];

					var oldName = mod.Name;
					ImGuiLayoutUtils.DrawRowModifier(mod, nameof(Modifier.Name), true, AttackTypes);
					if (mod.Name != oldName)
						attackEvent.OnModifiersChanged();

					var oldLevel = mod.Level;
					ImGuiLayoutUtils.DrawRowDragDouble(true, "Level", mod, nameof(Modifier.Level), true,
						"Modifier level. Sometimes referred to as strength. 100% is the default level. 0% will disable a modifier. Negative values will invert some modifiers.",
						1.0f, "%.6f%%");
					if (!mod.Level.DoubleEquals(oldLevel))
						attackEvent.OnModifiersChanged();

					var oldSpeed = mod.Speed;
					ImGuiLayoutUtils.DrawRowDragDouble(true, "Speed", mod, nameof(Modifier.Speed), true,
						"Speed at which the modifier is applied.",
						0.01f, "%.6fs");
					if (!mod.Speed.DoubleEquals(oldSpeed))
						attackEvent.OnModifiersChanged();

					var oldLength = mod.LengthSeconds;
					ImGuiLayoutUtils.DrawRowDragDouble(true, "Length", mod, nameof(Modifier.LengthSeconds), true,
						"Length of the modifier.",
						0.01f, "%.6fs");
					if (!mod.LengthSeconds.DoubleEquals(oldLength))
						attackEvent.OnModifiersChanged();

					if (ImGuiLayoutUtils.DrawRowButton("Delete", "Delete Modifier", "Delete this Modifier."))
					{
						ActionQueue.Instance.Do(new ActionDeleteModFromAttack(attackEvent, mod));
					}

					ImGuiLayoutUtils.EndTable();
				}
			}

			if (disabled)
				PopDisabled();
		}

		ImGui.End();
	}
}

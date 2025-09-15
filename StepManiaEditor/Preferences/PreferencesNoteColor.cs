using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Fumen;
using Fumen.Converters;
using Microsoft.Xna.Framework;
using static StepManiaEditor.PreferencesNoteColor;
using Vector3 = System.Numerics.Vector3;

namespace StepManiaEditor;

/// <summary>
/// Note color preferences.
/// Multiplayer colors are individual properties in order to ease edits through UI.
/// </summary>
internal sealed class PreferencesNoteColor : Notifier<PreferencesNoteColor>
{
	public enum ColorSet
	{
		Grooveauthor,
		Itg,
		Stepmania,
	}

	public class NoteColorSet
	{
		public NoteColorSet(
			Vector3 quarter,
			Vector3 eighth,
			Vector3 twelfth,
			Vector3 sixteenth,
			Vector3 twentyForth,
			Vector3 thirtySecond,
			Vector3 fortyEighth,
			Vector3 sixtyForth,
			Vector3 oneHundredNinetySecond,
			Vector3 hold,
			Vector3 roll,
			Vector3 mine)
		{
			Quarter = quarter;
			Eighth = eighth;
			Twelfth = twelfth;
			Sixteenth = sixteenth;
			TwentyForth = twentyForth;
			ThirtySecond = thirtySecond;
			FortyEighth = fortyEighth;
			SixtyForth = sixtyForth;
			OneHundredNinetySecond = oneHundredNinetySecond;
			Hold = hold;
			Roll = roll;
			Mine = mine;
		}

		public readonly Vector3 Quarter;
		public readonly Vector3 Eighth;
		public readonly Vector3 Twelfth;
		public readonly Vector3 Sixteenth;
		public readonly Vector3 TwentyForth;
		public readonly Vector3 ThirtySecond;
		public readonly Vector3 FortyEighth;
		public readonly Vector3 SixtyForth;
		public readonly Vector3 OneHundredNinetySecond;
		public readonly Vector3 Hold;
		public readonly Vector3 Roll;
		public readonly Vector3 Mine;
	}

	public const string NotificationPiuColoringMethodChanged = "PiuColoringMethodChanged";

	/// <summary>
	/// Brightness multiplier for selected variants of colors.
	/// </summary>
	public const float DefaultSelectionColorMultiplier = 1.7f;

	/// <summary>
	/// Brightness multiplier for held variants of hold colors.
	/// </summary>
	public const float DefaultHeldColorMultiplier = 1.5f;

	/// <summary>
	/// Brightness multiplier for the normal color in UI contexts.
	/// </summary>
	public const float DefaultArrowUIColorMultiplier = 1.5f;

	/// <summary>
	/// Brightness multiplier for the selected color in UI contexts.
	/// It is intention this is large and will result in whites for many colors.
	/// This is typically used in contexts where the colored area is small so differentiating
	/// between a selected and unselected note is more important than differentiating between
	/// individual note colors.
	/// </summary>
	public const float DefaultArrowUISelectedColorMultiplier = 8.0f;

	/// <summary>
	/// Saturation multiplier for coloring PIU holds.
	/// </summary>
	public const float DefaultPiuHoldSaturationMultiplier = 0.8f;

	/// <summary>
	/// Whether or not to use row-based coloring for PIU.
	/// </summary>
	public const bool DefaultUseRowBasedColoringForPiu = false;

	public static readonly NoteColorSet[] DefaultNoteColors =
	[
		new(
			new Vector3(0.7137255f, 0.0941176f, 0.0941176f),
			new Vector3(0.0941176f, 0.2078431f, 0.7137255f),
			new Vector3(0.2117647f, 0.6784314f, 0.2156863f),
			new Vector3(0.8745098f, 0.8470588f, 0.0588235f),
			new Vector3(0.5176471f, 0.0941176f, 0.7137255f),
			new Vector3(0.0941176f, 0.7137255f, 0.5960784f),
			new Vector3(0.7137255f, 0.0941176f, 0.5019607f),
			new Vector3(0.3098039f, 0.4352941f, 0.3450980f),
			new Vector3(0.3098039f, 0.4352941f, 0.3450980f),
			new Vector3(0.2666667f, 0.2666667f, 0.2666667f),
			new Vector3(0.7803921f, 0.4666667f, 0.1607843f),
			new Vector3(0.8196078f, 0.0078431f, 0.0078431f)),
		new(
			new Vector3(0.910f, 0.000f, 0.000f),
			new Vector3(0.157f, 0.275f, 1.000f),
			new Vector3(0.706f, 0.290f, 1.000f),
			new Vector3(0.000f, 0.815f, 0.000f),
			new Vector3(0.706f, 0.290f, 1.000f),
			new Vector3(1.000f, 1.000f, 0.000f),
			new Vector3(0.706f, 0.290f, 1.000f),
			new Vector3(0.000f, 0.910f, 0.898f),
			new Vector3(0.000f, 0.910f, 0.898f),
			new Vector3(0.498f, 0.494f, 0.498f),
			new Vector3(0.820f, 0.808f, 0.000f),
			new Vector3(1.000f, 0.000f, 0.000f)),
		new(
			new Vector3(0.922f, 0.125f, 0.004f),
			new Vector3(0.000f, 0.447f, 0.910f),
			new Vector3(0.361f, 0.914f, 0.000f),
			new Vector3(0.914f, 0.757f, 0.000f),
			new Vector3(0.412f, 0.400f, 0.925f),
			new Vector3(0.000f, 0.914f, 0.502f),
			new Vector3(0.914f, 0.000f, 0.404f),
			new Vector3(0.424f, 0.569f, 0.345f),
			new Vector3(0.424f, 0.569f, 0.345f),
			new Vector3(0.204f, 0.325f, 0.494f),
			new Vector3(0.275f, 0.494f, 0.204f),
			new Vector3(0.925f, 0.000f, 0.000f)),
	];

	public const float DefaultRoutineNoteColorAlpha = 0.90f;
	public const bool DefaultColorMultiplayerHoldsAndRolls = true;
	public static readonly Vector3 DefaultPlayer0Color = new(0.7109375f, 0.09375f, 0.7109375f);
	public static readonly Vector3 DefaultPlayer1Color = new(0.09375f, 0.7109375f, 0.7109375f);
	public static readonly Vector3 DefaultPlayer2Color = new(0.7109375f, 0.7109375f, 0.09375f);
	public static readonly Vector3 DefaultPlayer3Color = new(0.09375f, 0.7109375f, 0.09375f);
	public static readonly Vector3 DefaultPlayer4Color = new(0.7109375f, 0.390625f, 0.09375f);
	public static readonly Vector3 DefaultPlayer5Color = new(0.7109375f, 0.7109375f, 0.7109375f);
	public static readonly Vector3 DefaultPlayer6Color = new(0.09375f, 0.09375f, 0.09375f);
	public static readonly Vector3 DefaultPlayer7Color = new(0.7109375f, 0.390625f, 0.390625f);
	public static readonly Vector3 DefaultPlayer8Color = new(0.390625f, 0.390625f, 0.7109375f);
	public static readonly Vector3 DefaultPlayer9Color = new(0.390625f, 0.7109375f, 0.390625f);
	public static readonly Vector3 DefaultPiuTopColor = new(0.7019608f, 0.1058824f, 0.2156863f);
	public static readonly Vector3 DefaultPiuMiddleColor = new(1.0000000f, 0.9176471f, 0.0000000f);
	public static readonly Vector3 DefaultPiuBottomColor = new(0.1058824f, 0.2509804f, 0.7019608f);

	[JsonInclude] public bool ShowNoteColorPreferencesWindow;

	[JsonInclude]
	public float SelectionColorMultiplier
	{
		get => SelectionColorMultiplierInternal;
		set
		{
			SelectionColorMultiplierInternal = value;
			RefreshCachedColors();
		}
	}

	private float SelectionColorMultiplierInternal = DefaultSelectionColorMultiplier;

	[JsonInclude]
	public float HeldColorMultiplier
	{
		get => HeldColorMultiplierInternal;
		set
		{
			HeldColorMultiplierInternal = value;
			RefreshCachedColors();
		}
	}

	private float HeldColorMultiplierInternal = DefaultHeldColorMultiplier;

	[JsonInclude]
	public float ArrowUIColorMultiplier
	{
		get => ArrowUIColorMultiplierInternal;
		set
		{
			ArrowUIColorMultiplierInternal = value;
			RefreshCachedColors();
		}
	}

	private float ArrowUIColorMultiplierInternal = DefaultArrowUIColorMultiplier;

	[JsonInclude]
	public float ArrowUISelectedColorMultiplier
	{
		get => ArrowUISelectedColorMultiplierInternal;
		set
		{
			ArrowUISelectedColorMultiplierInternal = value;
			RefreshCachedColors();
		}
	}

	private float ArrowUISelectedColorMultiplierInternal = DefaultArrowUISelectedColorMultiplier;

	[JsonInclude]
	public float PiuHoldSaturationMultiplier
	{
		get => PiuHoldSaturationMultiplierInternal;
		set
		{
			PiuHoldSaturationMultiplierInternal = value;
			RefreshCachedColors();
		}
	}

	private float PiuHoldSaturationMultiplierInternal = DefaultPiuHoldSaturationMultiplier;

	[JsonInclude]
	public bool UseRowBasedColoringForPiu
	{
		get => UseRowBasedColoringForPiuInternal;
		set
		{
			UseRowBasedColoringForPiuInternal = value;
			Notify(NotificationPiuColoringMethodChanged, this);
		}
	}

	private bool UseRowBasedColoringForPiuInternal = DefaultUseRowBasedColoringForPiu;

	[JsonInclude] [JsonPropertyName("ColorHoldsAndRolls")]
	public bool ColorMultiplayerHoldsAndRolls = DefaultColorMultiplayerHoldsAndRolls;

	[JsonInclude]
	public float RoutineNoteColorAlpha
	{
		get => RoutineNoteColorAlphaInternal;
		set
		{
			RoutineNoteColorAlphaInternal = value;
			RefreshCachedColors();
		}
	}

	private float RoutineNoteColorAlphaInternal = DefaultRoutineNoteColorAlpha;

	[JsonInclude]
	public Vector3 Player0Color
	{
		get => Player0ColorInternal;
		set
		{
			Player0ColorInternal = value;
			RefreshCachedColors();
		}
	}

	private Vector3 Player0ColorInternal = DefaultPlayer0Color;

	[JsonInclude]
	public Vector3 Player1Color
	{
		get => Player1ColorInternal;
		set
		{
			Player1ColorInternal = value;
			RefreshCachedColors();
		}
	}

	private Vector3 Player1ColorInternal = DefaultPlayer1Color;

	[JsonInclude]
	public Vector3 Player2Color
	{
		get => Player2ColorInternal;
		set
		{
			Player2ColorInternal = value;
			RefreshCachedColors();
		}
	}

	private Vector3 Player2ColorInternal = DefaultPlayer2Color;

	[JsonInclude]
	public Vector3 Player3Color
	{
		get => Player3ColorInternal;
		set
		{
			Player3ColorInternal = value;
			RefreshCachedColors();
		}
	}

	private Vector3 Player3ColorInternal = DefaultPlayer3Color;

	[JsonInclude]
	public Vector3 Player4Color
	{
		get => Player4ColorInternal;
		set
		{
			Player4ColorInternal = value;
			RefreshCachedColors();
		}
	}

	private Vector3 Player4ColorInternal = DefaultPlayer4Color;

	[JsonInclude]
	public Vector3 Player5Color
	{
		get => Player5ColorInternal;
		set
		{
			Player5ColorInternal = value;
			RefreshCachedColors();
		}
	}

	private Vector3 Player5ColorInternal = DefaultPlayer5Color;

	[JsonInclude]
	public Vector3 Player6Color
	{
		get => Player6ColorInternal;
		set
		{
			Player6ColorInternal = value;
			RefreshCachedColors();
		}
	}

	private Vector3 Player6ColorInternal = DefaultPlayer6Color;

	[JsonInclude]
	public Vector3 Player7Color
	{
		get => Player7ColorInternal;
		set
		{
			Player7ColorInternal = value;
			RefreshCachedColors();
		}
	}

	private Vector3 Player7ColorInternal = DefaultPlayer7Color;

	[JsonInclude]
	public Vector3 Player8Color
	{
		get => Player8ColorInternal;
		set
		{
			Player8ColorInternal = value;
			RefreshCachedColors();
		}
	}

	private Vector3 Player8ColorInternal = DefaultPlayer8Color;

	[JsonInclude]
	public Vector3 Player9Color
	{
		get => Player9ColorInternal;
		set
		{
			Player9ColorInternal = value;
			RefreshCachedColors();
		}
	}

	private Vector3 Player9ColorInternal = DefaultPlayer9Color;

	[JsonInclude]
	public Vector3 QuarterColor
	{
		get => QuarterColorInternal;
		set
		{
			QuarterColorInternal = value;
			RefreshCachedColors();
		}
	}

	private Vector3 QuarterColorInternal = DefaultNoteColors[(int)ColorSet.Grooveauthor].Quarter;

	[JsonInclude]
	public Vector3 EighthColor
	{
		get => EighthColorInternal;
		set
		{
			EighthColorInternal = value;
			RefreshCachedColors();
		}
	}

	private Vector3 EighthColorInternal = DefaultNoteColors[(int)ColorSet.Grooveauthor].Eighth;

	[JsonInclude]
	public Vector3 TwelfthColor
	{
		get => TwelfthColorInternal;
		set
		{
			TwelfthColorInternal = value;
			RefreshCachedColors();
		}
	}

	private Vector3 TwelfthColorInternal = DefaultNoteColors[(int)ColorSet.Grooveauthor].Twelfth;

	[JsonInclude]
	public Vector3 SixteenthColor
	{
		get => SixteenthColorInternal;
		set
		{
			SixteenthColorInternal = value;
			RefreshCachedColors();
		}
	}

	private Vector3 SixteenthColorInternal = DefaultNoteColors[(int)ColorSet.Grooveauthor].Sixteenth;

	[JsonInclude]
	public Vector3 TwentyForthColor
	{
		get => TwentyForthColorInternal;
		set
		{
			TwentyForthColorInternal = value;
			RefreshCachedColors();
		}
	}

	private Vector3 TwentyForthColorInternal = DefaultNoteColors[(int)ColorSet.Grooveauthor].TwentyForth;

	[JsonInclude]
	public Vector3 ThirtySecondColor
	{
		get => ThirtySecondColorInternal;
		set
		{
			ThirtySecondColorInternal = value;
			RefreshCachedColors();
		}
	}

	private Vector3 ThirtySecondColorInternal = DefaultNoteColors[(int)ColorSet.Grooveauthor].ThirtySecond;

	[JsonInclude]
	public Vector3 FortyEighthColor
	{
		get => FortyEighthColorInternal;
		set
		{
			FortyEighthColorInternal = value;
			RefreshCachedColors();
		}
	}

	private Vector3 FortyEighthColorInternal = DefaultNoteColors[(int)ColorSet.Grooveauthor].FortyEighth;

	[JsonInclude]
	public Vector3 SixtyForthColor
	{
		get => SixtyForthColorInternal;
		set
		{
			SixtyForthColorInternal = value;
			RefreshCachedColors();
		}
	}

	private Vector3 SixtyForthColorInternal = DefaultNoteColors[(int)ColorSet.Grooveauthor].SixtyForth;

	[JsonInclude]
	public Vector3 OneHundredNinetySecondColor
	{
		get => OneHundredNinetySecondColorInternal;
		set
		{
			OneHundredNinetySecondColorInternal = value;
			RefreshCachedColors();
		}
	}

	private Vector3 OneHundredNinetySecondColorInternal = DefaultNoteColors[(int)ColorSet.Grooveauthor].OneHundredNinetySecond;

	[JsonInclude]
	public Vector3 PiuTopColor
	{
		get => PiuTopColorInternal;
		set
		{
			PiuTopColorInternal = value;
			RefreshCachedColors();
		}
	}

	private Vector3 PiuTopColorInternal = DefaultPiuTopColor;

	[JsonInclude]
	public Vector3 PiuMiddleColor
	{
		get => PiuMiddleColorInternal;
		set
		{
			PiuMiddleColorInternal = value;
			RefreshCachedColors();
		}
	}

	private Vector3 PiuMiddleColorInternal = DefaultPiuMiddleColor;

	[JsonInclude]
	public Vector3 PiuBottomColor
	{
		get => PiuBottomColorInternal;
		set
		{
			PiuBottomColorInternal = value;
			RefreshCachedColors();
		}
	}

	private Vector3 PiuBottomColorInternal = DefaultPiuBottomColor;

	[JsonInclude]
	public Vector3 HoldColor
	{
		get => HoldColorInternal;
		set
		{
			HoldColorInternal = value;
			RefreshCachedColors();
		}
	}

	private Vector3 HoldColorInternal = DefaultNoteColors[(int)ColorSet.Grooveauthor].Hold;

	[JsonInclude]
	public Vector3 RollColor
	{
		get => RollColorInternal;
		set
		{
			RollColorInternal = value;
			RefreshCachedColors();
		}
	}

	private Vector3 RollColorInternal = DefaultNoteColors[(int)ColorSet.Grooveauthor].Roll;

	[JsonInclude]
	public Vector3 MineColor
	{
		get => MineColorInternal;
		set
		{
			MineColorInternal = value;
			RefreshCachedColors();
		}
	}

	private Vector3 MineColorInternal = DefaultNoteColors[(int)ColorSet.Grooveauthor].Mine;

	private class PerPlayerNoteColors
	{
		public class ArrowColor
		{
			public readonly uint UIColor;
			public readonly uint UISelectedColor;
			public readonly Color XnaColor;
			public readonly Color XnaHeldColor;
			public readonly Color XnaSelectedColor;
			public readonly Color XnaHeldAndSelectedColor;

			public ArrowColor(Vector3 color, PreferencesNoteColor preferences)
			{
				XnaColor = new Color(color.X, color.Y, color.Z);
				XnaHeldColor = new Color(Math.Clamp(color.X * preferences.HeldColorMultiplier, 0.0f, 1.0f),
					Math.Clamp(color.Y * preferences.HeldColorMultiplier, 0.0f, 1.0f),
					Math.Clamp(color.Z * preferences.HeldColorMultiplier, 0.0f, 1.0f));
				XnaSelectedColor = new Color(Math.Clamp(color.X * preferences.SelectionColorMultiplier, 0.0f, 1.0f),
					Math.Clamp(color.Y * preferences.SelectionColorMultiplier, 0.0f, 1.0f),
					Math.Clamp(color.Z * preferences.SelectionColorMultiplier, 0.0f, 1.0f));
				XnaHeldAndSelectedColor = new Color(
					Math.Clamp(color.X * preferences.HeldColorMultiplier * preferences.SelectionColorMultiplier, 0.0f, 1.0f),
					Math.Clamp(color.Y * preferences.HeldColorMultiplier * preferences.SelectionColorMultiplier, 0.0f, 1.0f),
					Math.Clamp(color.Z * preferences.HeldColorMultiplier * preferences.SelectionColorMultiplier, 0.0f, 1.0f));
				UIColor = Utils.ColorRGBAMultiply(Utils.ToRGBA(color.X, color.Y, color.Z, 1.0f),
					preferences.ArrowUIColorMultiplier);
				UISelectedColor = Utils.ColorRGBAMultiply(Utils.ToRGBA(color.X, color.Y, color.Z, 1.0f),
					preferences.ArrowUISelectedColorMultiplier);
			}
		}

		public const int NumPiuLanes = 10;

		public readonly Dictionary<int, ArrowColor> ColorsBySubdivision;
		public readonly ArrowColor[] ColorsByRow;
		public readonly ArrowColor HoldBodyColor;
		public readonly ArrowColor RollBodyColor;
		public readonly ArrowColor MineColor;
		public readonly ArrowColor PlayerColor;
		public readonly ArrowColor[] PiuColorsByLane;
		public readonly ArrowColor[] PiuHoldColorsByLane;

		/// <summary>
		/// Constructor for single player colors.
		/// </summary>
		public PerPlayerNoteColors(PreferencesNoteColor preferences)
		{
			PlayerColor = new ArrowColor(Vector3.One, preferences);

			ColorsBySubdivision = new Dictionary<int, ArrowColor>
			{
				{ 1, new ArrowColor(preferences.QuarterColor, preferences) },
				{ 2, new ArrowColor(preferences.EighthColor, preferences) },
				{ 3, new ArrowColor(preferences.TwelfthColor, preferences) },
				{ 4, new ArrowColor(preferences.SixteenthColor, preferences) },
				{ 6, new ArrowColor(preferences.TwentyForthColor, preferences) },
				{ 8, new ArrowColor(preferences.ThirtySecondColor, preferences) },
				{ 12, new ArrowColor(preferences.FortyEighthColor, preferences) },
				{ 16, new ArrowColor(preferences.SixtyForthColor, preferences) },
				{ 48, new ArrowColor(preferences.OneHundredNinetySecondColor, preferences) },
			};
			ColorsByRow = new ArrowColor[SMCommon.MaxValidDenominator];
			for (var i = 0; i < SMCommon.MaxValidDenominator; i++)
			{
				var key = new Fraction(i, SMCommon.MaxValidDenominator).Reduce().Denominator;
				if (!ColorsBySubdivision.ContainsKey(key))
					key = 48;
				ColorsByRow[i] = ColorsBySubdivision[key];
			}

			HoldBodyColor = new ArrowColor(preferences.HoldColor, preferences);
			RollBodyColor = new ArrowColor(preferences.RollColor, preferences);
			MineColor = new ArrowColor(preferences.MineColor, preferences);

			var piuTopColor = new ArrowColor(preferences.PiuTopColor, preferences);
			var piuMiddleColor = new ArrowColor(preferences.PiuMiddleColor, preferences);
			var piuBottomColor = new ArrowColor(preferences.PiuBottomColor, preferences);

			PiuColorsByLane = new ArrowColor[NumPiuLanes];
			PiuColorsByLane[0] = piuBottomColor;
			PiuColorsByLane[1] = piuTopColor;
			PiuColorsByLane[2] = piuMiddleColor;
			PiuColorsByLane[3] = piuTopColor;
			PiuColorsByLane[4] = piuBottomColor;
			PiuColorsByLane[5] = piuBottomColor;
			PiuColorsByLane[6] = piuTopColor;
			PiuColorsByLane[7] = piuMiddleColor;
			PiuColorsByLane[8] = piuTopColor;
			PiuColorsByLane[9] = piuBottomColor;

			Vector3 Desaturate(Vector3 color)
			{
				var (h, s, v) = ColorUtils.RgbToHsv(color.X, color.Y, color.Z);
				s = Math.Clamp(s * preferences.PiuHoldSaturationMultiplier, 0.0f, 1.0f);
				var (r, g, b) = ColorUtils.HsvToRgb(h, s, v);
				return new Vector3(r, g, b);
			}

			piuTopColor = new ArrowColor(Desaturate(preferences.PiuTopColor), preferences);
			piuMiddleColor = new ArrowColor(Desaturate(preferences.PiuMiddleColor), preferences);
			piuBottomColor = new ArrowColor(Desaturate(preferences.PiuBottomColor), preferences);

			PiuHoldColorsByLane = new ArrowColor[NumPiuLanes];
			PiuHoldColorsByLane[0] = piuBottomColor;
			PiuHoldColorsByLane[1] = piuTopColor;
			PiuHoldColorsByLane[2] = piuMiddleColor;
			PiuHoldColorsByLane[3] = piuTopColor;
			PiuHoldColorsByLane[4] = piuBottomColor;
			PiuHoldColorsByLane[5] = piuBottomColor;
			PiuHoldColorsByLane[6] = piuTopColor;
			PiuHoldColorsByLane[7] = piuMiddleColor;
			PiuHoldColorsByLane[8] = piuTopColor;
			PiuHoldColorsByLane[9] = piuBottomColor;
		}

		/// <summary>
		/// Constructor for multiplayer colors.
		/// </summary>
		public PerPlayerNoteColors(PreferencesNoteColor preferences, Vector3 multiplayerColor)
		{
			PlayerColor = new ArrowColor(multiplayerColor, preferences);

			var a = preferences.RoutineNoteColorAlpha;

			Vector3 BlendColor(Vector3 c)
			{
				return new Vector3(
					Math.Clamp(c.X * (1.0f - a) + multiplayerColor.X * a, 0.0f, 1.0f),
					Math.Clamp(c.Y * (1.0f - a) + multiplayerColor.Y * a, 0.0f, 1.0f),
					Math.Clamp(c.Z * (1.0f - a) + multiplayerColor.Z * a, 0.0f, 1.0f));
			}

			ColorsBySubdivision = new Dictionary<int, ArrowColor>
			{
				{ 1, new ArrowColor(BlendColor(preferences.QuarterColor), preferences) },
				{ 2, new ArrowColor(BlendColor(preferences.EighthColor), preferences) },
				{ 3, new ArrowColor(BlendColor(preferences.TwelfthColor), preferences) },
				{ 4, new ArrowColor(BlendColor(preferences.SixteenthColor), preferences) },
				{ 6, new ArrowColor(BlendColor(preferences.TwentyForthColor), preferences) },
				{ 8, new ArrowColor(BlendColor(preferences.ThirtySecondColor), preferences) },
				{ 12, new ArrowColor(BlendColor(preferences.FortyEighthColor), preferences) },
				{ 16, new ArrowColor(BlendColor(preferences.SixtyForthColor), preferences) },
				{ 48, new ArrowColor(BlendColor(preferences.OneHundredNinetySecondColor), preferences) },
			};
			ColorsByRow = new ArrowColor[SMCommon.MaxValidDenominator];
			for (var i = 0; i < SMCommon.MaxValidDenominator; i++)
			{
				var key = new Fraction(i, SMCommon.MaxValidDenominator).Reduce().Denominator;
				if (!ColorsBySubdivision.ContainsKey(key))
					key = 48;
				ColorsByRow[i] = ColorsBySubdivision[key];
			}

			HoldBodyColor = new ArrowColor(BlendColor(preferences.HoldColor), preferences);
			RollBodyColor = new ArrowColor(BlendColor(preferences.RollColor), preferences);
			MineColor = new ArrowColor(BlendColor(preferences.MineColor), preferences);

			var piuTopColor = new ArrowColor(BlendColor(preferences.PiuTopColor), preferences);
			var piuMiddleColor = new ArrowColor(BlendColor(preferences.PiuMiddleColor), preferences);
			var piuBottomColor = new ArrowColor(BlendColor(preferences.PiuBottomColor), preferences);

			PiuColorsByLane = new ArrowColor[NumPiuLanes];
			PiuColorsByLane[0] = piuBottomColor;
			PiuColorsByLane[1] = piuTopColor;
			PiuColorsByLane[2] = piuMiddleColor;
			PiuColorsByLane[3] = piuTopColor;
			PiuColorsByLane[4] = piuBottomColor;
			PiuColorsByLane[5] = piuBottomColor;
			PiuColorsByLane[6] = piuTopColor;
			PiuColorsByLane[7] = piuMiddleColor;
			PiuColorsByLane[8] = piuTopColor;
			PiuColorsByLane[9] = piuBottomColor;

			Vector3 Desaturate(Vector3 color)
			{
				var (h, s, v) = ColorUtils.RgbToHsv(color.X, color.Y, color.Z);
				s = Math.Clamp(s * preferences.PiuHoldSaturationMultiplier, 0.0f, 1.0f);
				var (r, g, b) = ColorUtils.HsvToRgb(h, s, v);
				return new Vector3(r, g, b);
			}

			piuTopColor = new ArrowColor(Desaturate(BlendColor(preferences.PiuTopColor)), preferences);
			piuMiddleColor = new ArrowColor(Desaturate(BlendColor(preferences.PiuMiddleColor)), preferences);
			piuBottomColor = new ArrowColor(Desaturate(BlendColor(preferences.PiuBottomColor)), preferences);

			PiuHoldColorsByLane = new ArrowColor[NumPiuLanes];
			PiuHoldColorsByLane[0] = piuBottomColor;
			PiuHoldColorsByLane[1] = piuTopColor;
			PiuHoldColorsByLane[2] = piuMiddleColor;
			PiuHoldColorsByLane[3] = piuTopColor;
			PiuHoldColorsByLane[4] = piuBottomColor;
			PiuHoldColorsByLane[5] = piuBottomColor;
			PiuHoldColorsByLane[6] = piuTopColor;
			PiuHoldColorsByLane[7] = piuMiddleColor;
			PiuHoldColorsByLane[8] = piuTopColor;
			PiuHoldColorsByLane[9] = piuBottomColor;
		}
	}

	private PerPlayerNoteColors SinglePlayerNoteColors;
	private readonly List<PerPlayerNoteColors> MultiPlayerNoteColors = [];

	public void PostLoad()
	{
		RefreshCachedColors();
	}

	private void RefreshCachedColors()
	{
		SinglePlayerNoteColors = new PerPlayerNoteColors(this);

		MultiPlayerNoteColors.Clear();
		MultiPlayerNoteColors.Add(new PerPlayerNoteColors(this, Player0Color));
		MultiPlayerNoteColors.Add(new PerPlayerNoteColors(this, Player1Color));
		MultiPlayerNoteColors.Add(new PerPlayerNoteColors(this, Player2Color));
		MultiPlayerNoteColors.Add(new PerPlayerNoteColors(this, Player3Color));
		MultiPlayerNoteColors.Add(new PerPlayerNoteColors(this, Player4Color));
		MultiPlayerNoteColors.Add(new PerPlayerNoteColors(this, Player5Color));
		MultiPlayerNoteColors.Add(new PerPlayerNoteColors(this, Player6Color));
		MultiPlayerNoteColors.Add(new PerPlayerNoteColors(this, Player7Color));
		MultiPlayerNoteColors.Add(new PerPlayerNoteColors(this, Player8Color));
		MultiPlayerNoteColors.Add(new PerPlayerNoteColors(this, Player9Color));
	}

	public bool TryGetNoteColorForSubdivision(int subdivision, out Color xnaColor)
	{
		if (!SinglePlayerNoteColors.ColorsBySubdivision.TryGetValue(subdivision, out var color))
		{
			xnaColor = Color.White;
			return false;
		}

		xnaColor = color.XnaColor;
		return true;
	}

	public bool TryGetUINoteColorForSubdivision(int subdivision, out uint uiColor)
	{
		if (!SinglePlayerNoteColors.ColorsBySubdivision.TryGetValue(subdivision, out var color))
		{
			uiColor = 0;
			return false;
		}

		uiColor = color.UIColor;
		return true;
	}

	#region Single Player Colors

	public Color GetNoteColor(int row)
	{
		return SinglePlayerNoteColors.ColorsByRow[row % SMCommon.MaxValidDenominator].XnaColor;
	}

	public Color GetNoteColor(int row, bool selected)
	{
		if (selected)
			return SinglePlayerNoteColors.ColorsByRow[row % SMCommon.MaxValidDenominator].XnaSelectedColor;
		return SinglePlayerNoteColors.ColorsByRow[row % SMCommon.MaxValidDenominator].XnaColor;
	}

	public Color GetNoteColor(int row, bool selected, bool held)
	{
		if (selected)
		{
			if (held)
				return SinglePlayerNoteColors.ColorsByRow[row % SMCommon.MaxValidDenominator].XnaHeldAndSelectedColor;
			return SinglePlayerNoteColors.ColorsByRow[row % SMCommon.MaxValidDenominator].XnaSelectedColor;
		}

		if (held)
			return SinglePlayerNoteColors.ColorsByRow[row % SMCommon.MaxValidDenominator].XnaHeldColor;
		return SinglePlayerNoteColors.ColorsByRow[row % SMCommon.MaxValidDenominator].XnaColor;
	}

	public Color GetHeldNoteColor(int row)
	{
		return SinglePlayerNoteColors.ColorsByRow[row % SMCommon.MaxValidDenominator].XnaHeldColor;
	}

	public Color GetSelectedNoteColor(int row)
	{
		return SinglePlayerNoteColors.ColorsByRow[row % SMCommon.MaxValidDenominator].XnaSelectedColor;
	}

	public Color GetHeldAndSelectedNoteColor(int row)
	{
		return SinglePlayerNoteColors.ColorsByRow[row % SMCommon.MaxValidDenominator].XnaHeldAndSelectedColor;
	}

	public uint GetUINoteColor(int row, bool selected)
	{
		if (selected)
			return SinglePlayerNoteColors.ColorsByRow[row % SMCommon.MaxValidDenominator].UISelectedColor;
		return SinglePlayerNoteColors.ColorsByRow[row % SMCommon.MaxValidDenominator].UIColor;
	}

	public Color GetHoldBodyColor(bool selected, bool held)
	{
		if (selected)
		{
			if (held)
				return SinglePlayerNoteColors.HoldBodyColor.XnaHeldAndSelectedColor;
			return SinglePlayerNoteColors.HoldBodyColor.XnaSelectedColor;
		}

		if (held)
			return SinglePlayerNoteColors.HoldBodyColor.XnaHeldColor;
		return SinglePlayerNoteColors.HoldBodyColor.XnaColor;
	}

	public Color GetRollBodyColor(bool selected, bool held)
	{
		if (selected)
		{
			if (held)
				return SinglePlayerNoteColors.RollBodyColor.XnaHeldAndSelectedColor;
			return SinglePlayerNoteColors.RollBodyColor.XnaSelectedColor;
		}

		if (held)
			return SinglePlayerNoteColors.RollBodyColor.XnaHeldColor;
		return SinglePlayerNoteColors.RollBodyColor.XnaColor;
	}

	public Color GetMineColor(bool selected, bool held)
	{
		if (selected)
		{
			if (held)
				return SinglePlayerNoteColors.MineColor.XnaHeldAndSelectedColor;
			return SinglePlayerNoteColors.MineColor.XnaSelectedColor;
		}

		if (held)
			return SinglePlayerNoteColors.MineColor.XnaHeldColor;
		return SinglePlayerNoteColors.MineColor.XnaColor;
	}

	public uint GetUIHoldColor(bool selected)
	{
		if (selected)
			return SinglePlayerNoteColors.HoldBodyColor.UISelectedColor;
		return SinglePlayerNoteColors.HoldBodyColor.UIColor;
	}

	public uint GetUIRollColor(bool selected)
	{
		if (selected)
			return SinglePlayerNoteColors.RollBodyColor.UISelectedColor;
		return SinglePlayerNoteColors.RollBodyColor.UIColor;
	}

	public uint GetUIMineColor(bool selected)
	{
		if (selected)
			return SinglePlayerNoteColors.MineColor.UISelectedColor;
		return SinglePlayerNoteColors.MineColor.UIColor;
	}

	#endregion Single Player Colors

	#region Multiplayer Row Agnostic Colors

	public Color GetNoteColorForPlayer(int player)
	{
		return MultiPlayerNoteColors[player % MultiPlayerNoteColors.Count].PlayerColor.XnaColor;
	}

	public Color GetNoteColorForPlayer(int player, bool selected)
	{
		if (selected)
			return MultiPlayerNoteColors[player % MultiPlayerNoteColors.Count].PlayerColor.XnaSelectedColor;
		return MultiPlayerNoteColors[player % MultiPlayerNoteColors.Count].PlayerColor.XnaColor;
	}

	public Color GetNoteColorForPlayer(int player, bool selected, bool held)
	{
		if (selected)
		{
			if (held)
				return MultiPlayerNoteColors[player % MultiPlayerNoteColors.Count].PlayerColor.XnaHeldAndSelectedColor;
			return MultiPlayerNoteColors[player % MultiPlayerNoteColors.Count].PlayerColor.XnaSelectedColor;
		}

		if (held)
			return MultiPlayerNoteColors[player % MultiPlayerNoteColors.Count].PlayerColor.XnaHeldColor;
		return MultiPlayerNoteColors[player % MultiPlayerNoteColors.Count].PlayerColor.XnaColor;
	}

	public Color GetHeldNoteColorForPlayer(int player)
	{
		return MultiPlayerNoteColors[player % MultiPlayerNoteColors.Count].PlayerColor.XnaHeldColor;
	}

	public Color GetSelectedNoteColorForPlayer(int player)
	{
		return MultiPlayerNoteColors[player % MultiPlayerNoteColors.Count].PlayerColor.XnaSelectedColor;
	}

	public Color GetHeldAndSelectedNoteColorForPlayer(int player)
	{
		return MultiPlayerNoteColors[player % MultiPlayerNoteColors.Count].PlayerColor.XnaHeldAndSelectedColor;
	}

	public uint GetUINoteColorForPlayer(int player, bool selected)
	{
		if (selected)
			return MultiPlayerNoteColors[player % MultiPlayerNoteColors.Count].PlayerColor.UISelectedColor;
		return MultiPlayerNoteColors[player % MultiPlayerNoteColors.Count].PlayerColor.UIColor;
	}

	public Color GetHoldBodyColor(int player, bool selected, bool held)
	{
		if (selected)
		{
			if (held)
				return MultiPlayerNoteColors[player % MultiPlayerNoteColors.Count].HoldBodyColor.XnaHeldAndSelectedColor;
			return MultiPlayerNoteColors[player % MultiPlayerNoteColors.Count].HoldBodyColor.XnaSelectedColor;
		}

		if (held)
			return MultiPlayerNoteColors[player % MultiPlayerNoteColors.Count].HoldBodyColor.XnaHeldColor;
		return MultiPlayerNoteColors[player % MultiPlayerNoteColors.Count].HoldBodyColor.XnaColor;
	}

	public Color GetRollBodyColor(int player, bool selected, bool held)
	{
		if (selected)
		{
			if (held)
				return MultiPlayerNoteColors[player % MultiPlayerNoteColors.Count].RollBodyColor.XnaHeldAndSelectedColor;
			return MultiPlayerNoteColors[player % MultiPlayerNoteColors.Count].RollBodyColor.XnaSelectedColor;
		}

		if (held)
			return MultiPlayerNoteColors[player % MultiPlayerNoteColors.Count].RollBodyColor.XnaHeldColor;
		return MultiPlayerNoteColors[player % MultiPlayerNoteColors.Count].RollBodyColor.XnaColor;
	}

	public Color GetMineColor(int player, bool selected, bool held)
	{
		if (selected)
		{
			if (held)
				return MultiPlayerNoteColors[player % MultiPlayerNoteColors.Count].MineColor.XnaHeldAndSelectedColor;
			return MultiPlayerNoteColors[player % MultiPlayerNoteColors.Count].MineColor.XnaSelectedColor;
		}

		if (held)
			return MultiPlayerNoteColors[player % MultiPlayerNoteColors.Count].MineColor.XnaHeldColor;
		return MultiPlayerNoteColors[player % MultiPlayerNoteColors.Count].MineColor.XnaColor;
	}

	public uint GetUIHoldColor(int player, bool selected)
	{
		if (selected)
			return MultiPlayerNoteColors[player % MultiPlayerNoteColors.Count].HoldBodyColor.UISelectedColor;
		return MultiPlayerNoteColors[player % MultiPlayerNoteColors.Count].HoldBodyColor.UIColor;
	}

	public uint GetUIRollColor(int player, bool selected)
	{
		if (selected)
			return MultiPlayerNoteColors[player % MultiPlayerNoteColors.Count].RollBodyColor.UISelectedColor;
		return MultiPlayerNoteColors[player % MultiPlayerNoteColors.Count].RollBodyColor.UIColor;
	}

	public uint GetUIMineColor(int player, bool selected)
	{
		if (selected)
			return MultiPlayerNoteColors[player % MultiPlayerNoteColors.Count].MineColor.UISelectedColor;
		return MultiPlayerNoteColors[player % MultiPlayerNoteColors.Count].MineColor.UIColor;
	}

	#endregion Multiplayer Row Agnostic Colors

	#region Multiplayer Colors

	public Color GetNoteColor(int player, int row)
	{
		return MultiPlayerNoteColors[player % MultiPlayerNoteColors.Count].ColorsByRow[row % SMCommon.MaxValidDenominator]
			.XnaColor;
	}

	public Color GetNoteColor(int player, int row, bool selected)
	{
		if (selected)
			return MultiPlayerNoteColors[player % MultiPlayerNoteColors.Count].ColorsByRow[row % SMCommon.MaxValidDenominator]
				.XnaSelectedColor;
		return MultiPlayerNoteColors[player % MultiPlayerNoteColors.Count].ColorsByRow[row % SMCommon.MaxValidDenominator]
			.XnaColor;
	}

	public Color GetNoteColor(int player, int row, bool selected, bool held)
	{
		if (selected)
		{
			if (held)
				return MultiPlayerNoteColors[player % MultiPlayerNoteColors.Count].ColorsByRow[row % SMCommon.MaxValidDenominator]
					.XnaHeldAndSelectedColor;
			return MultiPlayerNoteColors[player % MultiPlayerNoteColors.Count].ColorsByRow[row % SMCommon.MaxValidDenominator]
				.XnaSelectedColor;
		}

		if (held)
			return MultiPlayerNoteColors[player % MultiPlayerNoteColors.Count].ColorsByRow[row % SMCommon.MaxValidDenominator]
				.XnaHeldColor;
		return MultiPlayerNoteColors[player % MultiPlayerNoteColors.Count].ColorsByRow[row % SMCommon.MaxValidDenominator]
			.XnaColor;
	}

	public Color GetHeldNoteColor(int player, int row)
	{
		return MultiPlayerNoteColors[player % MultiPlayerNoteColors.Count].ColorsByRow[row % SMCommon.MaxValidDenominator]
			.XnaHeldColor;
	}

	public Color GetSelectedNoteColor(int player, int row)
	{
		return MultiPlayerNoteColors[player % MultiPlayerNoteColors.Count].ColorsByRow[row % SMCommon.MaxValidDenominator]
			.XnaSelectedColor;
	}

	public Color GetHeldAndSelectedNoteColor(int player, int row)
	{
		return MultiPlayerNoteColors[player % MultiPlayerNoteColors.Count].ColorsByRow[row % SMCommon.MaxValidDenominator]
			.XnaHeldAndSelectedColor;
	}

	public uint GetUINoteColor(int player, int row, bool selected)
	{
		if (selected)
			return MultiPlayerNoteColors[player % MultiPlayerNoteColors.Count].ColorsByRow[row % SMCommon.MaxValidDenominator]
				.UISelectedColor;
		return MultiPlayerNoteColors[player % MultiPlayerNoteColors.Count].ColorsByRow[row % SMCommon.MaxValidDenominator]
			.UIColor;
	}

	#endregion Multiplayer Colors

	#region Single Player PIU Colors

	public Color GetPiuNoteColor(int lane)
	{
		return SinglePlayerNoteColors.PiuColorsByLane[lane % PerPlayerNoteColors.NumPiuLanes].XnaColor;
	}

	public Color GetPiuNoteColor(int lane, bool selected)
	{
		if (selected)
			return SinglePlayerNoteColors.PiuColorsByLane[lane % PerPlayerNoteColors.NumPiuLanes].XnaSelectedColor;
		return SinglePlayerNoteColors.PiuColorsByLane[lane % PerPlayerNoteColors.NumPiuLanes].XnaColor;
	}

	public Color GetPiuNoteColor(int lane, bool selected, bool held)
	{
		if (selected)
		{
			if (held)
				return SinglePlayerNoteColors.PiuColorsByLane[lane % PerPlayerNoteColors.NumPiuLanes].XnaHeldAndSelectedColor;
			return SinglePlayerNoteColors.PiuColorsByLane[lane % PerPlayerNoteColors.NumPiuLanes].XnaSelectedColor;
		}

		if (held)
			return SinglePlayerNoteColors.PiuColorsByLane[lane % PerPlayerNoteColors.NumPiuLanes].XnaHeldColor;
		return SinglePlayerNoteColors.PiuColorsByLane[lane % PerPlayerNoteColors.NumPiuLanes].XnaColor;
	}

	public Color GetPiuHeldNoteColor(int lane)
	{
		return SinglePlayerNoteColors.PiuColorsByLane[lane % PerPlayerNoteColors.NumPiuLanes].XnaHeldColor;
	}

	public Color GetPiuSelectedNoteColor(int lane)
	{
		return SinglePlayerNoteColors.PiuColorsByLane[lane % PerPlayerNoteColors.NumPiuLanes].XnaSelectedColor;
	}

	public Color GetPiuHeldAndSelectedNoteColor(int lane)
	{
		return SinglePlayerNoteColors.PiuColorsByLane[lane % PerPlayerNoteColors.NumPiuLanes].XnaHeldAndSelectedColor;
	}

	public uint GetPiuUINoteColor(int lane, bool selected)
	{
		if (selected)
			return SinglePlayerNoteColors.PiuColorsByLane[lane % PerPlayerNoteColors.NumPiuLanes].UISelectedColor;
		return SinglePlayerNoteColors.PiuColorsByLane[lane % PerPlayerNoteColors.NumPiuLanes].UIColor;
	}

	public uint GetPiuUIHoldColor(int lane, bool selected)
	{
		if (selected)
			return SinglePlayerNoteColors.PiuHoldColorsByLane[lane % PerPlayerNoteColors.NumPiuLanes].UISelectedColor;
		return SinglePlayerNoteColors.PiuHoldColorsByLane[lane % PerPlayerNoteColors.NumPiuLanes].UIColor;
	}

	public Color GetPiuHoldBodyColor(int lane, bool selected, bool held)
	{
		if (selected)
		{
			if (held)
				return SinglePlayerNoteColors.PiuHoldColorsByLane[lane % PerPlayerNoteColors.NumPiuLanes].XnaHeldAndSelectedColor;
			return SinglePlayerNoteColors.PiuHoldColorsByLane[lane % PerPlayerNoteColors.NumPiuLanes].XnaSelectedColor;
		}

		if (held)
			return SinglePlayerNoteColors.PiuHoldColorsByLane[lane % PerPlayerNoteColors.NumPiuLanes].XnaHeldColor;
		return SinglePlayerNoteColors.PiuHoldColorsByLane[lane % PerPlayerNoteColors.NumPiuLanes].XnaColor;
	}

	#endregion Single Player PIU Colors

	#region Multiplayer PIU Colors

	public Color GetPiuNoteColor(int player, int lane)
	{
		return MultiPlayerNoteColors[player % MultiPlayerNoteColors.Count].PiuColorsByLane[lane % PerPlayerNoteColors.NumPiuLanes]
			.XnaColor;
	}

	public Color GetPiuNoteColor(int player, int lane, bool selected)
	{
		if (selected)
			return MultiPlayerNoteColors[player % MultiPlayerNoteColors.Count]
				.PiuColorsByLane[lane % PerPlayerNoteColors.NumPiuLanes].XnaSelectedColor;
		return MultiPlayerNoteColors[player % MultiPlayerNoteColors.Count].PiuColorsByLane[lane % PerPlayerNoteColors.NumPiuLanes]
			.XnaColor;
	}

	public Color GetPiuNoteColor(int player, int lane, bool selected, bool held)
	{
		if (selected)
		{
			if (held)
				return MultiPlayerNoteColors[player % MultiPlayerNoteColors.Count]
					.PiuColorsByLane[lane % PerPlayerNoteColors.NumPiuLanes].XnaHeldAndSelectedColor;
			return MultiPlayerNoteColors[player % MultiPlayerNoteColors.Count]
				.PiuColorsByLane[lane % PerPlayerNoteColors.NumPiuLanes].XnaSelectedColor;
		}

		if (held)
			return MultiPlayerNoteColors[player % MultiPlayerNoteColors.Count]
				.PiuColorsByLane[lane % PerPlayerNoteColors.NumPiuLanes].XnaHeldColor;
		return MultiPlayerNoteColors[player % MultiPlayerNoteColors.Count].PiuColorsByLane[lane % PerPlayerNoteColors.NumPiuLanes]
			.XnaColor;
	}

	public Color GetPiuHeldNoteColor(int player, int lane)
	{
		return MultiPlayerNoteColors[player % MultiPlayerNoteColors.Count].PiuColorsByLane[lane % PerPlayerNoteColors.NumPiuLanes]
			.XnaHeldColor;
	}

	public Color GetPiuSelectedNoteColor(int player, int lane)
	{
		return MultiPlayerNoteColors[player % MultiPlayerNoteColors.Count].PiuColorsByLane[lane % PerPlayerNoteColors.NumPiuLanes]
			.XnaSelectedColor;
	}

	public Color GetPiuHeldAndSelectedNoteColor(int player, int lane)
	{
		return MultiPlayerNoteColors[player % MultiPlayerNoteColors.Count].PiuColorsByLane[lane % PerPlayerNoteColors.NumPiuLanes]
			.XnaHeldAndSelectedColor;
	}

	public uint GetPiuUINoteColor(int player, int lane, bool selected)
	{
		if (selected)
			return MultiPlayerNoteColors[player % MultiPlayerNoteColors.Count]
				.PiuColorsByLane[lane % PerPlayerNoteColors.NumPiuLanes].UISelectedColor;
		return MultiPlayerNoteColors[player % MultiPlayerNoteColors.Count].PiuColorsByLane[lane % PerPlayerNoteColors.NumPiuLanes]
			.UIColor;
	}

	public uint GetPiuUIHoldColor(int player, int lane, bool selected)
	{
		if (selected)
			return MultiPlayerNoteColors[player % MultiPlayerNoteColors.Count]
				.PiuHoldColorsByLane[lane % PerPlayerNoteColors.NumPiuLanes].UISelectedColor;
		return MultiPlayerNoteColors[player % MultiPlayerNoteColors.Count]
			.PiuHoldColorsByLane[lane % PerPlayerNoteColors.NumPiuLanes].UIColor;
	}

	public Color GetPiuHoldBodyColor(int player, int lane, bool selected, bool held)
	{
		if (selected)
		{
			if (held)
				return MultiPlayerNoteColors[player % MultiPlayerNoteColors.Count]
					.PiuHoldColorsByLane[lane % PerPlayerNoteColors.NumPiuLanes].XnaHeldAndSelectedColor;
			return MultiPlayerNoteColors[player % MultiPlayerNoteColors.Count]
				.PiuHoldColorsByLane[lane % PerPlayerNoteColors.NumPiuLanes].XnaSelectedColor;
		}

		if (held)
			return MultiPlayerNoteColors[player % MultiPlayerNoteColors.Count]
				.PiuHoldColorsByLane[lane % PerPlayerNoteColors.NumPiuLanes].XnaHeldColor;
		return MultiPlayerNoteColors[player % MultiPlayerNoteColors.Count]
			.PiuHoldColorsByLane[lane % PerPlayerNoteColors.NumPiuLanes].XnaColor;
	}

	#endregion Multiplayer PIU Colors

	#region Color Sets

	public NoteColorSet GetCurrentNoteColors()
	{
		return new NoteColorSet(
			QuarterColor,
			EighthColor,
			TwelfthColor,
			SixteenthColor,
			TwentyForthColor,
			ThirtySecondColor,
			FortyEighthColor,
			SixtyForthColor,
			OneHundredNinetySecondColor,
			HoldColor,
			RollColor,
			MineColor);
	}

	public void ApplyColorSet(ColorSet colorSet)
	{
		QuarterColor = DefaultNoteColors[(int)colorSet].Quarter;
		EighthColor = DefaultNoteColors[(int)colorSet].Eighth;
		TwelfthColor = DefaultNoteColors[(int)colorSet].Twelfth;
		SixteenthColor = DefaultNoteColors[(int)colorSet].Sixteenth;
		TwentyForthColor = DefaultNoteColors[(int)colorSet].TwentyForth;
		ThirtySecondColor = DefaultNoteColors[(int)colorSet].ThirtySecond;
		FortyEighthColor = DefaultNoteColors[(int)colorSet].FortyEighth;
		SixtyForthColor = DefaultNoteColors[(int)colorSet].SixtyForth;
		OneHundredNinetySecondColor = DefaultNoteColors[(int)colorSet].OneHundredNinetySecond;
		HoldColor = DefaultNoteColors[(int)colorSet].Hold;
		RollColor = DefaultNoteColors[(int)colorSet].Roll;
		MineColor = DefaultNoteColors[(int)colorSet].Mine;
	}

	#endregion Color Sets

	public bool IsUsingDefaults()
	{
		return SelectionColorMultiplier.FloatEquals(DefaultSelectionColorMultiplier)
		       && HeldColorMultiplier.FloatEquals(DefaultHeldColorMultiplier)
		       && ArrowUIColorMultiplier.FloatEquals(DefaultArrowUIColorMultiplier)
		       && ArrowUISelectedColorMultiplier.FloatEquals(DefaultArrowUISelectedColorMultiplier)
		       && PiuHoldSaturationMultiplier.FloatEquals(DefaultPiuHoldSaturationMultiplier)
		       && UseRowBasedColoringForPiu == DefaultUseRowBasedColoringForPiu
		       && RoutineNoteColorAlpha.FloatEquals(DefaultRoutineNoteColorAlpha)
		       && ColorMultiplayerHoldsAndRolls == DefaultColorMultiplayerHoldsAndRolls
		       && Player0Color.Equals(DefaultPlayer0Color)
		       && Player1Color.Equals(DefaultPlayer1Color)
		       && Player2Color.Equals(DefaultPlayer2Color)
		       && Player3Color.Equals(DefaultPlayer3Color)
		       && Player4Color.Equals(DefaultPlayer4Color)
		       && Player5Color.Equals(DefaultPlayer5Color)
		       && Player6Color.Equals(DefaultPlayer6Color)
		       && Player7Color.Equals(DefaultPlayer7Color)
		       && Player8Color.Equals(DefaultPlayer8Color)
		       && Player9Color.Equals(DefaultPlayer9Color)
		       && QuarterColor.Equals(DefaultNoteColors[(int)ColorSet.Grooveauthor].Quarter)
		       && EighthColor.Equals(DefaultNoteColors[(int)ColorSet.Grooveauthor].Eighth)
		       && TwelfthColor.Equals(DefaultNoteColors[(int)ColorSet.Grooveauthor].Twelfth)
		       && SixteenthColor.Equals(DefaultNoteColors[(int)ColorSet.Grooveauthor].Sixteenth)
		       && TwentyForthColor.Equals(DefaultNoteColors[(int)ColorSet.Grooveauthor].TwentyForth)
		       && ThirtySecondColor.Equals(DefaultNoteColors[(int)ColorSet.Grooveauthor].ThirtySecond)
		       && FortyEighthColor.Equals(DefaultNoteColors[(int)ColorSet.Grooveauthor].FortyEighth)
		       && SixtyForthColor.Equals(DefaultNoteColors[(int)ColorSet.Grooveauthor].SixtyForth)
		       && OneHundredNinetySecondColor.Equals(DefaultNoteColors[(int)ColorSet.Grooveauthor].OneHundredNinetySecond)
		       && PiuTopColor.Equals(DefaultPiuTopColor)
		       && PiuMiddleColor.Equals(DefaultPiuMiddleColor)
		       && PiuBottomColor.Equals(DefaultPiuBottomColor)
		       && HoldColor.Equals(DefaultNoteColors[(int)ColorSet.Grooveauthor].Hold)
		       && RollColor.Equals(DefaultNoteColors[(int)ColorSet.Grooveauthor].Roll)
		       && MineColor.Equals(DefaultNoteColors[(int)ColorSet.Grooveauthor].Mine);
	}

	public void RestoreDefaults()
	{
		// Don't enqueue an action if it would not have any effect.
		if (IsUsingDefaults())
			return;
		ActionQueue.Instance.Do(new ActionRestoreNoteColorPreferenceDefaults());
	}
}

/// <summary>
/// Action to restore not color preferences to their default values.
/// </summary>
internal sealed class ActionRestoreNoteColorPreferenceDefaults : EditorAction
{
	private readonly float PreviousSelectionColorMultiplier;
	private readonly float PreviousHeldColorMultiplier;
	private readonly float PreviousArrowUIColorMultiplier;
	private readonly float PreviousArrowUISelectedColorMultiplier;
	private readonly float PreviousPiuHoldSaturationMultiplier;
	private readonly bool PreviousUseRowBasedColoringForPiu;
	private readonly float PreviousRoutineNoteColorAlpha;
	private readonly bool PreviousColorMultiplayerHoldsAndRolls;
	private readonly Vector3 PreviousPlayer0Color;
	private readonly Vector3 PreviousPlayer1Color;
	private readonly Vector3 PreviousPlayer2Color;
	private readonly Vector3 PreviousPlayer3Color;
	private readonly Vector3 PreviousPlayer4Color;
	private readonly Vector3 PreviousPlayer5Color;
	private readonly Vector3 PreviousPlayer6Color;
	private readonly Vector3 PreviousPlayer7Color;
	private readonly Vector3 PreviousPlayer8Color;
	private readonly Vector3 PreviousPlayer9Color;
	private readonly Vector3 PreviousQuarterColor;
	private readonly Vector3 PreviousEighthColor;
	private readonly Vector3 PreviousTwelfthColor;
	private readonly Vector3 PreviousSixteenthColor;
	private readonly Vector3 PreviousTwentyForthColor;
	private readonly Vector3 PreviousThirtySecondColor;
	private readonly Vector3 PreviousFortyEighthColor;
	private readonly Vector3 PreviousSixtyForthColor;
	private readonly Vector3 PreviousOneHundredNinetySecondColor;
	private readonly Vector3 PreviousPiuTopColor;
	private readonly Vector3 PreviousPiuMiddleColor;
	private readonly Vector3 PreviousPiuBottomColor;
	private readonly Vector3 PreviousHoldColor;
	private readonly Vector3 PreviousRollColor;
	private readonly Vector3 PreviousMineColor;

	public ActionRestoreNoteColorPreferenceDefaults() : base(false, false)
	{
		var p = Preferences.Instance.PreferencesNoteColor;
		PreviousSelectionColorMultiplier = p.SelectionColorMultiplier;
		PreviousHeldColorMultiplier = p.HeldColorMultiplier;
		PreviousArrowUIColorMultiplier = p.ArrowUIColorMultiplier;
		PreviousArrowUISelectedColorMultiplier = p.ArrowUISelectedColorMultiplier;
		PreviousPiuHoldSaturationMultiplier = p.PiuHoldSaturationMultiplier;
		PreviousUseRowBasedColoringForPiu = p.UseRowBasedColoringForPiu;
		PreviousRoutineNoteColorAlpha = p.RoutineNoteColorAlpha;
		PreviousColorMultiplayerHoldsAndRolls = p.ColorMultiplayerHoldsAndRolls;
		PreviousPlayer0Color = p.Player0Color;
		PreviousPlayer1Color = p.Player1Color;
		PreviousPlayer2Color = p.Player2Color;
		PreviousPlayer3Color = p.Player3Color;
		PreviousPlayer4Color = p.Player4Color;
		PreviousPlayer5Color = p.Player5Color;
		PreviousPlayer6Color = p.Player6Color;
		PreviousPlayer7Color = p.Player7Color;
		PreviousPlayer8Color = p.Player8Color;
		PreviousPlayer9Color = p.Player9Color;
		PreviousQuarterColor = p.QuarterColor;
		PreviousEighthColor = p.EighthColor;
		PreviousTwelfthColor = p.TwelfthColor;
		PreviousSixteenthColor = p.SixteenthColor;
		PreviousTwentyForthColor = p.TwentyForthColor;
		PreviousThirtySecondColor = p.ThirtySecondColor;
		PreviousFortyEighthColor = p.FortyEighthColor;
		PreviousSixtyForthColor = p.SixtyForthColor;
		PreviousOneHundredNinetySecondColor = p.OneHundredNinetySecondColor;
		PreviousPiuTopColor = p.PiuTopColor;
		PreviousPiuMiddleColor = p.PiuMiddleColor;
		PreviousPiuBottomColor = p.PiuBottomColor;
		PreviousHoldColor = p.HoldColor;
		PreviousRollColor = p.RollColor;
		PreviousMineColor = p.MineColor;
	}

	public override bool AffectsFile()
	{
		return false;
	}

	public override string ToString()
	{
		return "Restore Note Color Preferences to default values.";
	}

	protected override void DoImplementation()
	{
		var p = Preferences.Instance.PreferencesNoteColor;
		p.SelectionColorMultiplier = DefaultSelectionColorMultiplier;
		p.HeldColorMultiplier = DefaultHeldColorMultiplier;
		p.ArrowUIColorMultiplier = DefaultArrowUIColorMultiplier;
		p.ArrowUISelectedColorMultiplier = DefaultArrowUISelectedColorMultiplier;
		p.PiuHoldSaturationMultiplier = DefaultPiuHoldSaturationMultiplier;
		p.UseRowBasedColoringForPiu = DefaultUseRowBasedColoringForPiu;
		p.RoutineNoteColorAlpha = DefaultRoutineNoteColorAlpha;
		p.ColorMultiplayerHoldsAndRolls = DefaultColorMultiplayerHoldsAndRolls;
		p.Player0Color = DefaultPlayer0Color;
		p.Player1Color = DefaultPlayer1Color;
		p.Player2Color = DefaultPlayer2Color;
		p.Player3Color = DefaultPlayer3Color;
		p.Player4Color = DefaultPlayer4Color;
		p.Player5Color = DefaultPlayer5Color;
		p.Player6Color = DefaultPlayer6Color;
		p.Player7Color = DefaultPlayer7Color;
		p.Player8Color = DefaultPlayer8Color;
		p.Player9Color = DefaultPlayer9Color;
		p.QuarterColor = DefaultNoteColors[(int)ColorSet.Grooveauthor].Quarter;
		p.EighthColor = DefaultNoteColors[(int)ColorSet.Grooveauthor].Eighth;
		p.TwelfthColor = DefaultNoteColors[(int)ColorSet.Grooveauthor].Twelfth;
		p.SixteenthColor = DefaultNoteColors[(int)ColorSet.Grooveauthor].Sixteenth;
		p.TwentyForthColor = DefaultNoteColors[(int)ColorSet.Grooveauthor].TwentyForth;
		p.ThirtySecondColor = DefaultNoteColors[(int)ColorSet.Grooveauthor].ThirtySecond;
		p.FortyEighthColor = DefaultNoteColors[(int)ColorSet.Grooveauthor].FortyEighth;
		p.SixtyForthColor = DefaultNoteColors[(int)ColorSet.Grooveauthor].SixtyForth;
		p.OneHundredNinetySecondColor = DefaultNoteColors[(int)ColorSet.Grooveauthor].OneHundredNinetySecond;
		p.PiuTopColor = DefaultPiuTopColor;
		p.PiuMiddleColor = DefaultPiuMiddleColor;
		p.PiuBottomColor = DefaultPiuBottomColor;
		p.HoldColor = DefaultNoteColors[(int)ColorSet.Grooveauthor].Hold;
		p.RollColor = DefaultNoteColors[(int)ColorSet.Grooveauthor].Roll;
		p.MineColor = DefaultNoteColors[(int)ColorSet.Grooveauthor].Mine;
	}

	protected override void UndoImplementation()
	{
		var p = Preferences.Instance.PreferencesNoteColor;
		p.SelectionColorMultiplier = PreviousSelectionColorMultiplier;
		p.HeldColorMultiplier = PreviousHeldColorMultiplier;
		p.ArrowUIColorMultiplier = PreviousArrowUIColorMultiplier;
		p.ArrowUISelectedColorMultiplier = PreviousArrowUISelectedColorMultiplier;
		p.PiuHoldSaturationMultiplier = PreviousPiuHoldSaturationMultiplier;
		p.UseRowBasedColoringForPiu = PreviousUseRowBasedColoringForPiu;
		p.RoutineNoteColorAlpha = PreviousRoutineNoteColorAlpha;
		p.ColorMultiplayerHoldsAndRolls = PreviousColorMultiplayerHoldsAndRolls;
		p.Player0Color = PreviousPlayer0Color;
		p.Player1Color = PreviousPlayer1Color;
		p.Player2Color = PreviousPlayer2Color;
		p.Player3Color = PreviousPlayer3Color;
		p.Player4Color = PreviousPlayer4Color;
		p.Player5Color = PreviousPlayer5Color;
		p.Player6Color = PreviousPlayer6Color;
		p.Player7Color = PreviousPlayer7Color;
		p.Player8Color = PreviousPlayer8Color;
		p.Player9Color = PreviousPlayer9Color;
		p.QuarterColor = PreviousQuarterColor;
		p.EighthColor = PreviousEighthColor;
		p.TwelfthColor = PreviousTwelfthColor;
		p.SixteenthColor = PreviousSixteenthColor;
		p.TwentyForthColor = PreviousTwentyForthColor;
		p.ThirtySecondColor = PreviousThirtySecondColor;
		p.FortyEighthColor = PreviousFortyEighthColor;
		p.SixtyForthColor = PreviousSixtyForthColor;
		p.OneHundredNinetySecondColor = PreviousOneHundredNinetySecondColor;
		p.PiuTopColor = PreviousPiuTopColor;
		p.PiuMiddleColor = PreviousPiuMiddleColor;
		p.PiuBottomColor = PreviousPiuBottomColor;
		p.HoldColor = PreviousHoldColor;
		p.RollColor = PreviousRollColor;
		p.MineColor = PreviousMineColor;
	}
}

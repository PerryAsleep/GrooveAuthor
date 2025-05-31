using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using static Fumen.Converters.SMCommon;

namespace StepManiaEditor;

/// <summary>
/// Class for managing access to textures and colors for arrows based on Chart type.
/// </summary>
internal abstract class ArrowGraphicManager
{
	protected const string TextureIdMineFill = "mine-fill";
	private const string TextureIdMineRim = "mine-rim";
	private const string TextureIdFakeMarker = "fake-marker";
	private const string TextureIdLiftMarker = "lift-marker";
	private const string TextureIdPlayerMarkerFill = "player-marker-fill";
	private const string TextureIdPlayerMarkerRim = "player-marker-rim";

	protected static readonly Dictionary<int, string> SnapTextureByBeatSubdivision = new()
	{
		[1] = "snap-1-4",
		[2] = "snap-1-8",
		[3] = "snap-1-12",
		[4] = "snap-1-16",
		[6] = "snap-1-24",
		[8] = "snap-1-32",
		[12] = "snap-1-48",
		[16] = "snap-1-64",
	};

	private static readonly Dictionary<ChartType, List<string>> ArrowIcons = new()
	{
		[ChartType.dance_single] =
		[
			"icon-dance-left",
			"icon-dance-down",
			"icon-dance-up",
			"icon-dance-right",
		],
		[ChartType.dance_double] =
		[
			"icon-dance-left",
			"icon-dance-down",
			"icon-dance-up",
			"icon-dance-right",
			"icon-dance-left",
			"icon-dance-down",
			"icon-dance-up",
			"icon-dance-right",
		],
		[ChartType.dance_solo] =
		[
			"icon-dance-left",
			"icon-dance-up-left",
			"icon-dance-down",
			"icon-dance-up",
			"icon-dance-up-right",
			"icon-dance-right",
		],
		[ChartType.dance_threepanel] =
		[
			"icon-dance-up-left",
			"icon-dance-down",
			"icon-dance-up-right",
		],
		[ChartType.dance_routine] =
		[
			"icon-dance-left",
			"icon-dance-down",
			"icon-dance-up",
			"icon-dance-right",
			"icon-dance-left",
			"icon-dance-down",
			"icon-dance-up",
			"icon-dance-right",
		],
		[ChartType.pump_single] =
		[
			"icon-pump-down-left",
			"icon-pump-up-left",
			"icon-pump-center",
			"icon-pump-up-right",
			"icon-pump-down-right",
		],
		[ChartType.pump_halfdouble] =
		[
			"icon-pump-center",
			"icon-pump-up-right",
			"icon-pump-down-right",
			"icon-pump-down-left",
			"icon-pump-up-left",
			"icon-pump-center",
		],
		[ChartType.pump_double] =
		[
			"icon-pump-down-left",
			"icon-pump-up-left",
			"icon-pump-center",
			"icon-pump-up-right",
			"icon-pump-down-right",
			"icon-pump-down-left",
			"icon-pump-up-left",
			"icon-pump-center",
			"icon-pump-up-right",
			"icon-pump-down-right",
		],
		[ChartType.pump_routine] =
		[
			"icon-pump-down-left",
			"icon-pump-up-left",
			"icon-pump-center",
			"icon-pump-up-right",
			"icon-pump-down-right",
			"icon-pump-down-left",
			"icon-pump-up-left",
			"icon-pump-center",
			"icon-pump-up-right",
			"icon-pump-down-right",
		],
		[ChartType.smx_beginner] =
		[
			"icon-dance-left",
			"icon-dance-center",
			"icon-dance-right",
		],
		[ChartType.smx_single] =
		[
			"icon-dance-left",
			"icon-dance-down",
			"icon-dance-center",
			"icon-dance-up",
			"icon-dance-right",
		],
		[ChartType.smx_dual] =
		[
			"icon-dance-left",
			"icon-dance-center",
			"icon-dance-right",
			"icon-dance-left",
			"icon-dance-center",
			"icon-dance-right",
		],
		[ChartType.smx_full] =
		[
			"icon-dance-left",
			"icon-dance-down",
			"icon-dance-center",
			"icon-dance-up",
			"icon-dance-right",
			"icon-dance-left",
			"icon-dance-down",
			"icon-dance-center",
			"icon-dance-up",
			"icon-dance-right",
		],
		[ChartType.smx_team] =
		[
			"icon-dance-left",
			"icon-dance-down",
			"icon-dance-center",
			"icon-dance-up",
			"icon-dance-right",
			"icon-dance-left",
			"icon-dance-down",
			"icon-dance-center",
			"icon-dance-up",
			"icon-dance-right",
		],
	};

	private static readonly Dictionary<ChartType, List<string>> DimArrowIcons;

	protected readonly PreferencesNoteColor Preferences;

	static ArrowGraphicManager()
	{
		// Set up dim arrow icons.
		DimArrowIcons = new Dictionary<ChartType, List<string>>();
		foreach (var kvp in ArrowIcons)
		{
			var dimList = new List<string>(kvp.Value.Count);
			foreach (var icon in kvp.Value)
			{
				dimList.Add($"{icon}-dim");
			}

			DimArrowIcons[kvp.Key] = dimList;
		}
	}

	protected ArrowGraphicManager(PreferencesNoteColor preferences)
	{
		Preferences = preferences;
	}

	public static string GetSelectedTextureId(string textureId)
	{
		if (string.IsNullOrEmpty(textureId))
			return null;
		return $"{textureId}-selected";
	}

	public static List<string> GetIcons(ChartType chartType)
	{
		if (ArrowIcons.TryGetValue(chartType, out var icons))
			return icons;
		return [];
	}

	public static List<string> GetDimIcons(ChartType chartType)
	{
		if (DimArrowIcons.TryGetValue(chartType, out var icons))
			return icons;
		return [];
	}

	protected static string GetTextureId(string textureId, bool selected)
	{
		return selected ? GetSelectedTextureId(textureId) : textureId;
	}

	/// <summary>
	/// Factory method for creating a new ArrowGraphicManager appropriate for the given ChartType.
	/// </summary>
	public static ArrowGraphicManager CreateArrowGraphicManager(ChartType chartType)
	{
		ArrowGraphicManager newArrowGraphicManager;
		var preferences = StepManiaEditor.Preferences.Instance.PreferencesNoteColor;
		switch (chartType)
		{
			case ChartType.dance_single:
			case ChartType.dance_double:
			case ChartType.dance_couple:
				newArrowGraphicManager = new ArrowGraphicManagerDanceSingleOrDouble(preferences);
				break;
			case ChartType.dance_routine:
				newArrowGraphicManager = new ArrowGraphicManagerDanceRoutine(preferences);
				break;
			case ChartType.dance_solo:
				newArrowGraphicManager = new ArrowGraphicManagerDanceSolo(preferences);
				break;
			case ChartType.dance_threepanel:
				newArrowGraphicManager = new ArrowGraphicManagerDanceThreePanel(preferences);
				break;
			case ChartType.smx_beginner:
				newArrowGraphicManager = new ArrowGraphicManagerDanceSMXBeginner(preferences);
				break;
			case ChartType.smx_single:
			case ChartType.smx_full:
				newArrowGraphicManager = new ArrowGraphicManagerDanceSMXSingleOrFull(preferences);
				break;
			case ChartType.smx_team:
				newArrowGraphicManager = new ArrowGraphicManagerDanceSMXTeam(preferences);
				break;
			case ChartType.smx_dual:
				newArrowGraphicManager = new ArrowGraphicManagerDanceSMXDual(preferences);
				break;

			case ChartType.pump_single:
			case ChartType.pump_double:
			case ChartType.pump_couple:
				newArrowGraphicManager = new ArrowGraphicManagerPIUSingleOrDouble(preferences);
				break;
			case ChartType.pump_routine:
				newArrowGraphicManager = new ArrowGraphicManagerPIURoutine(preferences);
				break;
			case ChartType.pump_halfdouble:
				newArrowGraphicManager = new ArrowGraphicManagerPIUSingleHalfDouble(preferences);
				break;
			default:
				return null;
		}

		return newArrowGraphicManager;
	}

	public abstract bool AreHoldCapsCentered();

	public abstract (string, float) GetReceptorTexture(int lane);
	public abstract (string, float) GetReceptorGlowTexture(int lane);
	public abstract (string, float) GetReceptorHeldTexture(int lane);

	public abstract (string, float, Color) GetArrowTextureFill(int row, int lane, bool selected, int player);
	public abstract (string, float) GetArrowTextureRim(int lane, bool selected);

	public abstract (string, bool, Color) GetHoldStartTextureFill(int row, int lane, bool held, bool selected, int player);
	public abstract (string, bool, Color) GetHoldBodyTextureFill(int row, int lane, bool held, bool selected, int player);
	public abstract (string, float, Color) GetHoldEndTextureFill(int row, int lane, bool held, bool selected, int player);
	public abstract (string, bool, Color) GetRollStartTextureFill(int row, int lane, bool held, bool selected, int player);
	public abstract (string, bool, Color) GetRollBodyTextureFill(int row, int lane, bool held, bool selected, int player);
	public abstract (string, float, Color) GetRollEndTextureFill(int row, int lane, bool held, bool selected, int player);

	public abstract (string, bool) GetHoldStartTextureRim(int lane, bool selected);
	public abstract (string, bool) GetHoldBodyTextureRim(int lane, bool selected);
	public abstract (string, float) GetHoldEndTextureRim(int lane, bool selected);

	public static bool TryGetArrowUIColorForSubdivision(int subdivision, out uint color)
	{
		return ArrowGraphicManagerDance.TryGetDanceArrowColorForSubdivision(subdivision, out color);
	}

	public abstract bool ShouldColorHoldsAndRollsInMultiplayerCharts();

	public abstract uint GetArrowUIColor(int row, int lane, bool selected, int player);
	public abstract uint GetHoldUIColor(int row, int lane, bool selected, int player);
	public abstract uint GetRollUIColor(int row, int lane, bool selected, int player);
	public abstract uint GetMineUIColor(bool selected, int player);

	public string GetMineRimTexture(bool selected)
	{
		return GetTextureId(TextureIdMineRim, selected);
	}

	public abstract (string, Color) GetMineFillTexture(bool selected, int player);

	public static string GetFakeMarkerTexture(bool selected)
	{
		return GetTextureId(TextureIdFakeMarker, selected);
	}

	public static string GetLiftMarkerTexture(bool selected)
	{
		return GetTextureId(TextureIdLiftMarker, selected);
	}

	public static string GetSnapIndicatorRimTexture()
	{
		return TextureIdPlayerMarkerRim;
	}

	public (string, Color) GetSnapIndicatorFillTexture(int subdivision)
	{
		if (!Preferences.TryGetNoteColorForSubdivision(subdivision, out var color))
			return (null, Color.White);
		return (TextureIdPlayerMarkerFill, color);
	}

	public (string, Color) GetPlayerMarkerFillTexture(int player)
	{
		return (TextureIdPlayerMarkerFill, Preferences.GetNoteColorForPlayer(player));
	}

	public static string GetPlayerMarkerRimTexture()
	{
		return TextureIdPlayerMarkerRim;
	}

	public uint GetUIColorForPlayer(int player)
	{
		return Preferences.GetUINoteColorForPlayer(player, false);
	}
}

internal abstract class ArrowGraphicManagerDance : ArrowGraphicManager
{
	protected struct UniqueDanceTextureSet
	{
		public string DownArrow;
		public string UpLeftArrow;
		public string CenterArrow;
	}

	protected struct HoldTextureSet
	{
		public UniqueDanceTextureSet Start;
		public UniqueDanceTextureSet Body;
		public UniqueDanceTextureSet End;
	}

	protected static readonly UniqueDanceTextureSet ArrowRims;
	protected static readonly UniqueDanceTextureSet ArrowFills;

	protected static readonly HoldTextureSet HoldFillTextures;
	protected static readonly HoldTextureSet RollFillTextures;
	protected static readonly HoldTextureSet HoldRimTextures;

	protected static readonly UniqueDanceTextureSet ReceptorTextures;
	protected static readonly UniqueDanceTextureSet ReceptorGlowTextures;
	protected static readonly UniqueDanceTextureSet ReceptorHeldTextures;

	static ArrowGraphicManagerDance()
	{
		ArrowRims = new UniqueDanceTextureSet
		{
			DownArrow = "itg-down-rim",
			UpLeftArrow = "itg-solo-rim",
			CenterArrow = "itg-center-rim",
		};
		ArrowFills = new UniqueDanceTextureSet
		{
			DownArrow = "itg-down-fill",
			UpLeftArrow = "itg-solo-fill",
			CenterArrow = "itg-center-fill",
		};

		HoldFillTextures = new HoldTextureSet
		{
			Start = new UniqueDanceTextureSet
			{
				DownArrow = null,
				CenterArrow = null,
				UpLeftArrow = "itg-solo-hold-start-fill",
			},
			Body = new UniqueDanceTextureSet
			{
				DownArrow = "itg-down-hold-fill",
				CenterArrow = "itg-center-hold-fill",
				UpLeftArrow = "itg-solo-hold-fill",
			},
			End = new UniqueDanceTextureSet
			{
				DownArrow = "itg-down-hold-end-fill",
				CenterArrow = "itg-center-hold-end-fill",
				UpLeftArrow = "itg-solo-hold-end-fill",
			},
		};

		RollFillTextures = new HoldTextureSet
		{
			Start = new UniqueDanceTextureSet
			{
				DownArrow = null,
				CenterArrow = null,
				UpLeftArrow = "itg-solo-roll-start-fill",
			},
			Body = new UniqueDanceTextureSet
			{
				DownArrow = "itg-down-roll-fill",
				CenterArrow = "itg-center-roll-fill",
				UpLeftArrow = "itg-solo-roll-fill",
			},
			End = new UniqueDanceTextureSet
			{
				DownArrow = "itg-down-roll-end-fill",
				CenterArrow = "itg-center-roll-end-fill",
				UpLeftArrow = "itg-solo-roll-end-fill",
			},
		};

		HoldRimTextures = new HoldTextureSet
		{
			Start = new UniqueDanceTextureSet
			{
				DownArrow = null,
				CenterArrow = null,
				UpLeftArrow = "itg-solo-hold-start-rim",
			},
			Body = new UniqueDanceTextureSet
			{
				DownArrow = "itg-down-hold-rim",
				CenterArrow = "itg-center-hold-rim",
				UpLeftArrow = "itg-solo-hold-rim",
			},
			End = new UniqueDanceTextureSet
			{
				DownArrow = "itg-down-hold-end-rim",
				CenterArrow = "itg-center-hold-end-rim",
				UpLeftArrow = "itg-solo-hold-end-rim",
			},
		};

		ReceptorTextures = new UniqueDanceTextureSet
		{
			DownArrow = "itg-down-receptor",
			CenterArrow = "itg-center-receptor",
			UpLeftArrow = "itg-solo-receptor",
		};
		ReceptorGlowTextures = new UniqueDanceTextureSet
		{
			DownArrow = "itg-down-receptor-glow",
			CenterArrow = "itg-center-receptor-glow",
			UpLeftArrow = "itg-solo-receptor-glow",
		};
		ReceptorHeldTextures = new UniqueDanceTextureSet
		{
			DownArrow = "itg-down-receptor-held",
			CenterArrow = "itg-center-receptor-held",
			UpLeftArrow = "itg-solo-receptor-held",
		};
	}

	protected ArrowGraphicManagerDance(PreferencesNoteColor preferences) : base(preferences)
	{
	}

	public static bool TryGetDanceArrowColorForSubdivision(int subdivision, out uint color)
	{
		return StepManiaEditor.Preferences.Instance.PreferencesNoteColor.TryGetUINoteColorForSubdivision(subdivision, out color);
	}

	public override bool AreHoldCapsCentered()
	{
		return false;
	}

	public override bool ShouldColorHoldsAndRollsInMultiplayerCharts()
	{
		return Preferences.ColorMultiplayerHoldsAndRolls;
	}
}

internal class ArrowGraphicManagerDanceSingleOrDouble : ArrowGraphicManagerDance
{
	protected static readonly float[] ArrowRotations =
	[
		(float)Math.PI * 0.5f, // L
		0.0f, // D
		(float)Math.PI, // U
		(float)Math.PI * 1.5f, // R
	];

	public ArrowGraphicManagerDanceSingleOrDouble(PreferencesNoteColor preferences) : base(preferences)
	{
	}

	public override (string, float) GetReceptorTexture(int lane)
	{
		return (ReceptorTextures.DownArrow, ArrowRotations[lane % 4]);
	}

	public override (string, float) GetReceptorGlowTexture(int lane)
	{
		return (ReceptorGlowTextures.DownArrow, ArrowRotations[lane % 4]);
	}

	public override (string, float) GetReceptorHeldTexture(int lane)
	{
		return (ReceptorHeldTextures.DownArrow, ArrowRotations[lane % 4]);
	}

	public override (string, float, Color) GetArrowTextureFill(int row, int lane, bool selected, int player)
	{
		return (ArrowFills.DownArrow, ArrowRotations[lane % 4], Preferences.GetNoteColor(row, selected));
	}

	public override (string, float) GetArrowTextureRim(int lane, bool selected)
	{
		return (GetTextureId(ArrowRims.DownArrow, selected), ArrowRotations[lane % 4]);
	}

	public override (string, bool, Color) GetHoldStartTextureFill(int row, int lane, bool held, bool selected, int player)
	{
		return (HoldFillTextures.Start.DownArrow, false, Preferences.GetHoldBodyColor(selected, held));
	}

	public override (string, bool, Color) GetHoldBodyTextureFill(int row, int lane, bool held, bool selected, int player)
	{
		return (HoldFillTextures.Body.DownArrow, false, Preferences.GetHoldBodyColor(selected, held));
	}

	public override (string, float, Color) GetHoldEndTextureFill(int row, int lane, bool held, bool selected, int player)
	{
		return (HoldFillTextures.End.DownArrow, 0.0f, Preferences.GetHoldBodyColor(selected, held));
	}

	public override (string, bool, Color) GetRollStartTextureFill(int row, int lane, bool held, bool selected, int player)
	{
		return (RollFillTextures.Start.DownArrow, false, Preferences.GetRollBodyColor(selected, held));
	}

	public override (string, bool, Color) GetRollBodyTextureFill(int row, int lane, bool held, bool selected, int player)
	{
		return (RollFillTextures.Body.DownArrow, false, Preferences.GetRollBodyColor(selected, held));
	}

	public override (string, float, Color) GetRollEndTextureFill(int row, int lane, bool held, bool selected, int player)
	{
		return (RollFillTextures.End.DownArrow, 0.0f, Preferences.GetRollBodyColor(selected, held));
	}

	public override (string, bool) GetHoldStartTextureRim(int lane, bool selected)
	{
		return (null, false);
	}

	public override (string, bool) GetHoldBodyTextureRim(int lane, bool selected)
	{
		return (GetTextureId(HoldRimTextures.Body.DownArrow, selected), false);
	}

	public override (string, float) GetHoldEndTextureRim(int lane, bool selected)
	{
		return (GetTextureId(HoldRimTextures.End.DownArrow, selected), 0.0f);
	}

	public override uint GetArrowUIColor(int row, int lane, bool selected, int player)
	{
		return Preferences.GetUINoteColor(row, selected);
	}

	public override uint GetHoldUIColor(int row, int lane, bool selected, int player)
	{
		return Preferences.GetUIHoldColor(selected);
	}

	public override uint GetRollUIColor(int row, int lane, bool selected, int player)
	{
		return Preferences.GetUIRollColor(selected);
	}

	public override uint GetMineUIColor(bool selected, int player)
	{
		return Preferences.GetUIMineColor(selected);
	}

	public override (string, Color) GetMineFillTexture(bool selected, int player)
	{
		return (TextureIdMineFill, Preferences.GetMineColor(selected, false));
	}
}

internal sealed class ArrowGraphicManagerDanceRoutine : ArrowGraphicManagerDanceSingleOrDouble
{
	public ArrowGraphicManagerDanceRoutine(PreferencesNoteColor preferences) : base(preferences)
	{
	}

	public override (string, float, Color) GetArrowTextureFill(int row, int lane, bool selected, int player)
	{
		return (ArrowFills.DownArrow, ArrowRotations[lane % 4], Preferences.GetNoteColor(player, row, selected));
	}

	public override (string, bool, Color) GetHoldStartTextureFill(int row, int lane, bool held, bool selected, int player)
	{
		if (!ShouldColorHoldsAndRollsInMultiplayerCharts())
			return base.GetHoldStartTextureFill(row, lane, held, selected, player);
		return (HoldFillTextures.Start.DownArrow, false, Preferences.GetHoldBodyColor(player, selected, held));
	}

	public override (string, bool, Color) GetHoldBodyTextureFill(int row, int lane, bool held, bool selected, int player)
	{
		if (!ShouldColorHoldsAndRollsInMultiplayerCharts())
			return base.GetHoldBodyTextureFill(row, lane, held, selected, player);
		return (HoldFillTextures.Body.DownArrow, false, Preferences.GetHoldBodyColor(player, selected, held));
	}

	public override (string, float, Color) GetHoldEndTextureFill(int row, int lane, bool held, bool selected, int player)
	{
		if (!ShouldColorHoldsAndRollsInMultiplayerCharts())
			return base.GetHoldEndTextureFill(row, lane, held, selected, player);
		return (HoldFillTextures.End.DownArrow, 0.0f, Preferences.GetHoldBodyColor(player, selected, held));
	}

	public override (string, bool, Color) GetRollStartTextureFill(int row, int lane, bool held, bool selected, int player)
	{
		if (!ShouldColorHoldsAndRollsInMultiplayerCharts())
			return base.GetRollStartTextureFill(row, lane, held, selected, player);
		return (RollFillTextures.Start.DownArrow, false, Preferences.GetRollBodyColor(player, selected, held));
	}

	public override (string, bool, Color) GetRollBodyTextureFill(int row, int lane, bool held, bool selected, int player)
	{
		if (!ShouldColorHoldsAndRollsInMultiplayerCharts())
			return base.GetRollBodyTextureFill(row, lane, held, selected, player);
		return (RollFillTextures.Body.DownArrow, false, Preferences.GetRollBodyColor(player, selected, held));
	}

	public override (string, float, Color) GetRollEndTextureFill(int row, int lane, bool held, bool selected, int player)
	{
		if (!ShouldColorHoldsAndRollsInMultiplayerCharts())
			return base.GetRollEndTextureFill(row, lane, held, selected, player);
		return (RollFillTextures.End.DownArrow, 0.0f, Preferences.GetRollBodyColor(player, selected, held));
	}

	public override uint GetArrowUIColor(int row, int lane, bool selected, int player)
	{
		return Preferences.GetUINoteColor(player, row, selected);
	}

	public override uint GetHoldUIColor(int row, int lane, bool selected, int player)
	{
		if (!ShouldColorHoldsAndRollsInMultiplayerCharts())
			return base.GetHoldUIColor(row, lane, selected, player);
		return Preferences.GetUIHoldColor(player, selected);
	}

	public override uint GetRollUIColor(int row, int lane, bool selected, int player)
	{
		if (!ShouldColorHoldsAndRollsInMultiplayerCharts())
			return base.GetRollUIColor(row, lane, selected, player);
		return Preferences.GetUIRollColor(player, selected);
	}

	public override uint GetMineUIColor(bool selected, int player)
	{
		return Preferences.GetUIMineColor(player, selected);
	}

	public override (string, Color) GetMineFillTexture(bool selected, int player)
	{
		return (TextureIdMineFill, Preferences.GetMineColor(player, selected, false));
	}
}

internal abstract class ArrowGraphicManagerDanceSoloBase : ArrowGraphicManagerDance
{
	protected ArrowGraphicManagerDanceSoloBase(PreferencesNoteColor preferences) : base(preferences)
	{
	}

	protected abstract bool ShouldUseUpLeftArrow(int lane);
	protected abstract float GetRotation(int lane);

	public override (string, float) GetReceptorTexture(int lane)
	{
		if (ShouldUseUpLeftArrow(lane))
			return (ReceptorTextures.UpLeftArrow, GetRotation(lane));
		return (ReceptorTextures.DownArrow, GetRotation(lane));
	}

	public override (string, float) GetReceptorGlowTexture(int lane)
	{
		if (ShouldUseUpLeftArrow(lane))
			return (ReceptorGlowTextures.UpLeftArrow, GetRotation(lane));
		return (ReceptorGlowTextures.DownArrow, GetRotation(lane));
	}

	public override (string, float) GetReceptorHeldTexture(int lane)
	{
		if (ShouldUseUpLeftArrow(lane))
			return (ReceptorHeldTextures.UpLeftArrow, GetRotation(lane));
		return (ReceptorHeldTextures.DownArrow, GetRotation(lane));
	}

	public override (string, float, Color) GetArrowTextureFill(int row, int lane, bool selected, int player)
	{
		if (ShouldUseUpLeftArrow(lane))
			return (ArrowFills.UpLeftArrow, GetRotation(lane), Preferences.GetNoteColor(row, selected));
		return (ArrowFills.DownArrow, GetRotation(lane), Preferences.GetNoteColor(row, selected));
	}

	public override (string, float) GetArrowTextureRim(int lane, bool selected)
	{
		if (ShouldUseUpLeftArrow(lane))
			return (GetTextureId(ArrowRims.UpLeftArrow, selected), GetRotation(lane));
		return (GetTextureId(ArrowRims.DownArrow, selected), GetRotation(lane));
	}

	public override (string, bool, Color) GetHoldStartTextureFill(int row, int lane, bool held, bool selected, int player)
	{
		// Always use the narrower diagonal hold graphics in solo.
		// But only use the start graphic for diagonal arrows since they have a gap needing to be filled.
		if (ShouldUseUpLeftArrow(lane))
			return (HoldFillTextures.Start.UpLeftArrow, false, Preferences.GetHoldBodyColor(selected, held));
		return (HoldFillTextures.Start.DownArrow, false, Preferences.GetHoldBodyColor(selected, held));
	}

	public override (string, bool, Color) GetHoldBodyTextureFill(int row, int lane, bool held, bool selected, int player)
	{
		// Always use the narrower diagonal hold graphics in solo.
		return (HoldFillTextures.Body.UpLeftArrow, false, Preferences.GetHoldBodyColor(selected, held));
	}

	public override (string, float, Color) GetHoldEndTextureFill(int row, int lane, bool held, bool selected, int player)
	{
		// Always use the narrower diagonal hold graphics in solo.
		return (HoldFillTextures.End.UpLeftArrow, 0.0f, Preferences.GetHoldBodyColor(selected, held));
	}

	public override (string, bool, Color) GetRollStartTextureFill(int row, int lane, bool held, bool selected, int player)
	{
		// Always use the narrower diagonal hold graphics in solo.
		// But only use the start graphic for diagonal arrows since they have a gap needing to be filled.
		if (ShouldUseUpLeftArrow(lane))
			return (RollFillTextures.Start.UpLeftArrow, false, Preferences.GetRollBodyColor(selected, held));
		return (RollFillTextures.Start.DownArrow, false, Preferences.GetRollBodyColor(selected, held));
	}

	public override (string, bool, Color) GetRollBodyTextureFill(int row, int lane, bool held, bool selected, int player)
	{
		// Always use the narrower diagonal hold graphics in solo.
		return (RollFillTextures.Body.UpLeftArrow, false, Preferences.GetRollBodyColor(selected, held));
	}

	public override (string, float, Color) GetRollEndTextureFill(int row, int lane, bool held, bool selected, int player)
	{
		// Always use the narrower diagonal hold graphics in solo.
		return (RollFillTextures.End.UpLeftArrow, 0.0f, Preferences.GetRollBodyColor(selected, held));
	}

	public override (string, bool) GetHoldStartTextureRim(int lane, bool selected)
	{
		// Always use the narrower diagonal hold graphics in solo.
		// But only use the start graphic for diagonal arrows since they have a gap needing to be filled.
		if (ShouldUseUpLeftArrow(lane))
			return (GetTextureId(HoldRimTextures.Start.UpLeftArrow, selected), false);
		return (GetTextureId(HoldRimTextures.Start.DownArrow, selected), false);
	}

	public override (string, bool) GetHoldBodyTextureRim(int lane, bool selected)
	{
		return (GetTextureId(HoldRimTextures.Body.UpLeftArrow, selected), false);
	}

	public override (string, float) GetHoldEndTextureRim(int lane, bool selected)
	{
		return (GetTextureId(HoldRimTextures.End.UpLeftArrow, selected), 0.0f);
	}

	public override uint GetArrowUIColor(int row, int lane, bool selected, int player)
	{
		return Preferences.GetUINoteColor(row, selected);
	}

	public override uint GetHoldUIColor(int row, int lane, bool selected, int player)
	{
		return Preferences.GetUIHoldColor(selected);
	}

	public override uint GetRollUIColor(int row, int lane, bool selected, int player)
	{
		return Preferences.GetUIRollColor(selected);
	}

	public override uint GetMineUIColor(bool selected, int player)
	{
		return Preferences.GetUIMineColor(selected);
	}

	public override (string, Color) GetMineFillTexture(bool selected, int player)
	{
		return (TextureIdMineFill, Preferences.GetMineColor(selected, false));
	}
}

internal sealed class ArrowGraphicManagerDanceSolo : ArrowGraphicManagerDanceSoloBase
{
	private static readonly float[] ArrowRotations =
	[
		(float)Math.PI * 0.5f, // L
		0.0f, // UL
		0.0f, // D
		(float)Math.PI, // U
		(float)Math.PI * 0.5f, // UR
		(float)Math.PI * 1.5f, // R
	];

	public ArrowGraphicManagerDanceSolo(PreferencesNoteColor preferences) : base(preferences)
	{
	}

	protected override bool ShouldUseUpLeftArrow(int lane)
	{
		return lane == 1 || lane == 4;
	}

	protected override float GetRotation(int lane)
	{
		return ArrowRotations[lane];
	}
}

internal sealed class ArrowGraphicManagerDanceThreePanel : ArrowGraphicManagerDanceSoloBase
{
	private static readonly float[] ArrowRotations =
	[
		0.0f, // UL
		0.0f, // D
		(float)Math.PI * 0.5f, // UR
	];

	public ArrowGraphicManagerDanceThreePanel(PreferencesNoteColor preferences) : base(preferences)
	{
	}

	protected override bool ShouldUseUpLeftArrow(int lane)
	{
		return lane == 0 || lane == 2;
	}

	protected override float GetRotation(int lane)
	{
		return ArrowRotations[lane];
	}
}

internal abstract class ArrowGraphicManagerDanceSMX : ArrowGraphicManagerDance
{
	protected abstract bool ShouldUseCenterArrow(int lane);
	protected abstract float GetRotation(int lane);

	protected ArrowGraphicManagerDanceSMX(PreferencesNoteColor preferences) : base(preferences)
	{
	}

	public override (string, float) GetReceptorTexture(int lane)
	{
		if (ShouldUseCenterArrow(lane))
			return (ReceptorTextures.CenterArrow, GetRotation(lane));
		return (ReceptorTextures.DownArrow, GetRotation(lane));
	}

	public override (string, float) GetReceptorGlowTexture(int lane)
	{
		if (ShouldUseCenterArrow(lane))
			return (ReceptorGlowTextures.CenterArrow, GetRotation(lane));
		return (ReceptorGlowTextures.DownArrow, GetRotation(lane));
	}

	public override (string, float) GetReceptorHeldTexture(int lane)
	{
		if (ShouldUseCenterArrow(lane))
			return (ReceptorHeldTextures.CenterArrow, GetRotation(lane));
		return (ReceptorHeldTextures.DownArrow, GetRotation(lane));
	}

	public override (string, float, Color) GetArrowTextureFill(int row, int lane, bool selected, int player)
	{
		if (ShouldUseCenterArrow(lane))
			return (ArrowFills.CenterArrow, GetRotation(lane), Preferences.GetNoteColor(row, selected));
		return (ArrowFills.DownArrow, GetRotation(lane), Preferences.GetNoteColor(row, selected));
	}

	public override (string, float) GetArrowTextureRim(int lane, bool selected)
	{
		if (ShouldUseCenterArrow(lane))
			return (GetTextureId(ArrowRims.CenterArrow, selected), GetRotation(lane));
		return (GetTextureId(ArrowRims.DownArrow, selected), GetRotation(lane));
	}

	public override (string, bool, Color) GetHoldStartTextureFill(int row, int lane, bool held, bool selected, int player)
	{
		return (HoldFillTextures.Start.CenterArrow, false, Preferences.GetHoldBodyColor(selected, held));
	}

	public override (string, bool, Color) GetHoldBodyTextureFill(int row, int lane, bool held, bool selected, int player)
	{
		return (HoldFillTextures.Body.CenterArrow, false, Preferences.GetHoldBodyColor(selected, held));
	}

	public override (string, float, Color) GetHoldEndTextureFill(int row, int lane, bool held, bool selected, int player)
	{
		return (HoldFillTextures.End.CenterArrow, 0.0f, Preferences.GetHoldBodyColor(selected, held));
	}

	public override (string, bool, Color) GetRollStartTextureFill(int row, int lane, bool held, bool selected, int player)
	{
		return (RollFillTextures.Start.CenterArrow, false, Preferences.GetRollBodyColor(selected, held));
	}

	public override (string, bool, Color) GetRollBodyTextureFill(int row, int lane, bool held, bool selected, int player)
	{
		return (RollFillTextures.Body.CenterArrow, false, Preferences.GetRollBodyColor(selected, held));
	}

	public override (string, float, Color) GetRollEndTextureFill(int row, int lane, bool held, bool selected, int player)
	{
		return (RollFillTextures.End.CenterArrow, 0.0f, Preferences.GetRollBodyColor(selected, held));
	}

	public override (string, bool) GetHoldStartTextureRim(int lane, bool selected)
	{
		return (GetTextureId(HoldRimTextures.Start.CenterArrow, selected), false);
	}

	public override (string, bool) GetHoldBodyTextureRim(int lane, bool selected)
	{
		return (GetTextureId(HoldRimTextures.Body.CenterArrow, selected), false);
	}

	public override (string, float) GetHoldEndTextureRim(int lane, bool selected)
	{
		return (GetTextureId(HoldRimTextures.End.CenterArrow, selected), 0.0f);
	}

	public override uint GetArrowUIColor(int row, int lane, bool selected, int player)
	{
		return Preferences.GetUINoteColor(row, selected);
	}

	public override uint GetHoldUIColor(int row, int lane, bool selected, int player)
	{
		return Preferences.GetUIHoldColor(selected);
	}

	public override uint GetRollUIColor(int row, int lane, bool selected, int player)
	{
		return Preferences.GetUIRollColor(selected);
	}

	public override uint GetMineUIColor(bool selected, int player)
	{
		return Preferences.GetUIMineColor(selected);
	}

	public override (string, Color) GetMineFillTexture(bool selected, int player)
	{
		return (TextureIdMineFill, Preferences.GetMineColor(selected, false));
	}
}

internal sealed class ArrowGraphicManagerDanceSMXBeginner : ArrowGraphicManagerDanceSMX
{
	private static readonly float[] ArrowRotations =
	[
		(float)Math.PI * 0.5f, // L
		0.0f, // Center
		(float)Math.PI * 1.5f, // R
	];

	public ArrowGraphicManagerDanceSMXBeginner(PreferencesNoteColor preferences) : base(preferences)
	{
	}

	protected override bool ShouldUseCenterArrow(int lane)
	{
		return lane == 1;
	}

	protected override float GetRotation(int lane)
	{
		return ArrowRotations[lane];
	}
}

internal class ArrowGraphicManagerDanceSMXSingleOrFull : ArrowGraphicManagerDanceSMX
{
	private static readonly float[] ArrowRotations =
	[
		(float)Math.PI * 0.5f, // L
		0.0f, // D
		0.0f, // Center
		(float)Math.PI, // U
		(float)Math.PI * 1.5f, // R
	];

	public ArrowGraphicManagerDanceSMXSingleOrFull(PreferencesNoteColor preferences) : base(preferences)
	{
	}

	protected override bool ShouldUseCenterArrow(int lane)
	{
		return lane % 5 == 2;
	}

	protected override float GetRotation(int lane)
	{
		return ArrowRotations[lane % 5];
	}
}

internal sealed class ArrowGraphicManagerDanceSMXDual : ArrowGraphicManagerDanceSMX
{
	private static readonly float[] ArrowRotations =
	[
		(float)Math.PI * 0.5f, // L
		0.0f, // Center
		(float)Math.PI * 1.5f, // R
		(float)Math.PI * 0.5f, // L
		0.0f, // Center
		(float)Math.PI * 1.5f, // R
	];

	public ArrowGraphicManagerDanceSMXDual(PreferencesNoteColor preferences) : base(preferences)
	{
	}

	protected override bool ShouldUseCenterArrow(int lane)
	{
		return lane == 1 || lane == 4;
	}

	protected override float GetRotation(int lane)
	{
		return ArrowRotations[lane];
	}
}

internal class ArrowGraphicManagerDanceSMXTeam : ArrowGraphicManagerDanceSMXSingleOrFull
{
	public ArrowGraphicManagerDanceSMXTeam(PreferencesNoteColor preferences) : base(preferences)
	{
	}

	public override (string, float, Color) GetArrowTextureFill(int row, int lane, bool selected, int player)
	{
		if (ShouldUseCenterArrow(lane))
			return (ArrowFills.CenterArrow, GetRotation(lane), Preferences.GetNoteColor(player, row, selected));
		return (ArrowFills.DownArrow, GetRotation(lane), Preferences.GetNoteColor(player, row, selected));
	}

	public override (string, bool, Color) GetHoldBodyTextureFill(int row, int lane, bool held, bool selected, int player)
	{
		if (!ShouldColorHoldsAndRollsInMultiplayerCharts())
			return base.GetHoldBodyTextureFill(row, lane, held, selected, player);
		return (HoldFillTextures.Body.CenterArrow, false, Preferences.GetHoldBodyColor(player, selected, held));
	}

	public override (string, float, Color) GetHoldEndTextureFill(int row, int lane, bool held, bool selected, int player)
	{
		if (!ShouldColorHoldsAndRollsInMultiplayerCharts())
			return base.GetHoldEndTextureFill(row, lane, held, selected, player);
		return (HoldFillTextures.End.CenterArrow, 0.0f, Preferences.GetHoldBodyColor(player, selected, held));
	}

	public override (string, bool, Color) GetRollBodyTextureFill(int row, int lane, bool held, bool selected, int player)
	{
		if (!ShouldColorHoldsAndRollsInMultiplayerCharts())
			return base.GetRollBodyTextureFill(row, lane, held, selected, player);
		return (RollFillTextures.Body.CenterArrow, false, Preferences.GetRollBodyColor(player, selected, held));
	}

	public override (string, float, Color) GetRollEndTextureFill(int row, int lane, bool held, bool selected, int player)
	{
		if (!ShouldColorHoldsAndRollsInMultiplayerCharts())
			return base.GetRollEndTextureFill(row, lane, held, selected, player);
		return (RollFillTextures.End.CenterArrow, 0.0f, Preferences.GetRollBodyColor(player, selected, held));
	}

	public override uint GetArrowUIColor(int row, int lane, bool selected, int player)
	{
		return Preferences.GetUINoteColor(player, row, selected);
	}

	public override uint GetHoldUIColor(int row, int lane, bool selected, int player)
	{
		if (!ShouldColorHoldsAndRollsInMultiplayerCharts())
			return base.GetHoldUIColor(row, lane, selected, player);
		return Preferences.GetUIHoldColor(player, selected);
	}

	public override uint GetRollUIColor(int row, int lane, bool selected, int player)
	{
		if (!ShouldColorHoldsAndRollsInMultiplayerCharts())
			return base.GetRollUIColor(row, lane, selected, player);
		return Preferences.GetUIRollColor(player, selected);
	}

	public override uint GetMineUIColor(bool selected, int player)
	{
		return Preferences.GetUIMineColor(player, selected);
	}

	public override (string, Color) GetMineFillTexture(bool selected, int player)
	{
		return (TextureIdMineFill, Preferences.GetMineColor(player, selected, false));
	}
}

internal abstract class ArrowGraphicManagerPIU : ArrowGraphicManager
{
	protected static readonly float[] ArrowRotations =
	[
		(float)Math.PI * 1.5f, // DL
		0.0f, // UL
		0.0f, // C
		(float)Math.PI * 0.5f, // UR
		(float)Math.PI, // DR
	];

	protected static readonly string[] ArrowRimTextures =
	[
		"piu-diagonal-rim", // DL
		"piu-diagonal-rim", // UL
		"piu-center-rim", // C
		"piu-diagonal-rim", // UR
		"piu-diagonal-rim", // DR
	];

	protected static readonly string[] ArrowFillTextures =
	[
		"piu-diagonal-fill", // DL
		"piu-diagonal-fill", // UL
		"piu-center-fill", // C
		"piu-diagonal-fill", // UR
		"piu-diagonal-fill", // DR
	];

	protected static readonly string[] HoldAndRollRimTextures =
	[
		"piu-diagonal-hold-rim", // DL
		"piu-diagonal-hold-rim", // UL
		"piu-center-hold-rim", // C
		"piu-diagonal-hold-rim", // UR
		"piu-diagonal-hold-rim", // DR
	];

	protected static readonly string[] HoldFillTextures =
	[
		"piu-diagonal-hold-fill", // DL
		"piu-diagonal-hold-fill", // UL
		"piu-center-hold-fill", // C
		"piu-diagonal-hold-fill", // UR
		"piu-diagonal-hold-fill", // DR
	];

	protected static readonly string[] RollFillTextures =
	[
		"piu-diagonal-roll-fill", // DL
		"piu-diagonal-roll-fill", // UL
		"piu-center-roll-fill", // C
		"piu-diagonal-roll-fill", // UR
		"piu-diagonal-roll-fill", // DR
	];

	protected static readonly string[] ReceptorTextures =
	[
		"piu-diagonal-receptor", // DL
		"piu-diagonal-receptor", // UL
		"piu-center-receptor", // C
		"piu-diagonal-receptor", // UR
		"piu-diagonal-receptor", // DR
	];

	protected static readonly string[] ReceptorGlowTextures =
	[
		"piu-diagonal-receptor-glow", // DL
		"piu-diagonal-receptor-glow", // UL
		"piu-center-receptor-glow", // C
		"piu-diagonal-receptor-glow", // UR
		"piu-diagonal-receptor-glow", // DR
	];

	protected static readonly string[] ReceptorHeldTextures =
	[
		"piu-diagonal-receptor-held", // DL
		"piu-diagonal-receptor-held", // UL
		"piu-center-receptor-held", // C
		"piu-diagonal-receptor-held", // UR
		"piu-diagonal-receptor-held", // DR
	];

	protected static readonly bool[] HoldMirrored =
	[
		false, // DL
		false, // UL
		false, // C
		true, // UR
		true, // DR
	];

	protected int StartArrowIndex;

	protected ArrowGraphicManagerPIU(PreferencesNoteColor preferences) : base(preferences)
	{
	}

	protected int GetTextureIndex(int lane)
	{
		return (lane + StartArrowIndex) % 5;
	}

	public override bool AreHoldCapsCentered()
	{
		return true;
	}

	public override bool ShouldColorHoldsAndRollsInMultiplayerCharts()
	{
		return true;
	}

	public override (string, float) GetReceptorTexture(int lane)
	{
		var i = GetTextureIndex(lane);
		return (ReceptorTextures[i], ArrowRotations[i]);
	}

	public override (string, float) GetReceptorGlowTexture(int lane)
	{
		var i = GetTextureIndex(lane);
		return (ReceptorGlowTextures[i], ArrowRotations[i]);
	}

	public override (string, float) GetReceptorHeldTexture(int lane)
	{
		var i = GetTextureIndex(lane);
		return (ReceptorHeldTextures[i], ArrowRotations[i]);
	}

	public override (string, float, Color) GetArrowTextureFill(int row, int lane, bool selected, int player)
	{
		var i = GetTextureIndex(lane);
		return (ArrowFillTextures[i], ArrowRotations[i], Preferences.GetPiuNoteColor(i, selected));
	}

	public override (string, float) GetArrowTextureRim(int lane, bool selected)
	{
		var i = GetTextureIndex(lane);
		return (GetTextureId(ArrowRimTextures[i], selected), ArrowRotations[i]);
	}

	public override (string, bool, Color) GetHoldStartTextureFill(int row, int lane, bool held, bool selected, int player)
	{
		return (null, false, Color.White);
	}

	public override (string, bool, Color) GetHoldBodyTextureFill(int row, int lane, bool held, bool selected, int player)
	{
		var i = GetTextureIndex(lane);
		return (HoldFillTextures[i], HoldMirrored[i], Preferences.GetPiuHoldBodyColor(i, selected, held));
	}

	public override (string, float, Color) GetHoldEndTextureFill(int row, int lane, bool held, bool selected, int player)
	{
		return GetArrowTextureFill(row, lane, selected, player);
	}

	public override (string, bool, Color) GetRollStartTextureFill(int row, int lane, bool held, bool selected, int player)
	{
		return (null, false, Color.White);
	}

	public override (string, bool, Color) GetRollBodyTextureFill(int row, int lane, bool held, bool selected, int player)
	{
		var i = GetTextureIndex(lane);
		return (RollFillTextures[i], HoldMirrored[i], Preferences.GetPiuHoldBodyColor(i, selected, held));
	}

	public override (string, float, Color) GetRollEndTextureFill(int row, int lane, bool held, bool selected, int player)
	{
		return GetArrowTextureFill(row, lane, selected, player);
	}

	public override (string, bool) GetHoldStartTextureRim(int lane, bool selected)
	{
		return (null, false);
	}

	public override (string, bool) GetHoldBodyTextureRim(int lane, bool selected)
	{
		var i = GetTextureIndex(lane);
		return (GetTextureId(HoldAndRollRimTextures[i], selected), HoldMirrored[i]);
	}

	public override (string, float) GetHoldEndTextureRim(int lane, bool selected)
	{
		return GetArrowTextureRim(lane, selected);
	}

	public override uint GetArrowUIColor(int row, int lane, bool selected, int player)
	{
		var i = GetTextureIndex(lane);
		return Preferences.GetPiuUINoteColor(i, selected);
	}

	public override uint GetHoldUIColor(int row, int lane, bool selected, int player)
	{
		var i = GetTextureIndex(lane);
		return Preferences.GetPiuUIHoldColor(i, selected);
	}

	public override uint GetRollUIColor(int row, int lane, bool selected, int player)
	{
		var i = GetTextureIndex(lane);
		return Preferences.GetPiuUIHoldColor(i, selected);
	}

	public override uint GetMineUIColor(bool selected, int player)
	{
		return Preferences.GetUIMineColor(selected);
	}

	public override (string, Color) GetMineFillTexture(bool selected, int player)
	{
		return (TextureIdMineFill, Preferences.GetMineColor(selected, false));
	}
}

internal class ArrowGraphicManagerPIUSingleOrDouble : ArrowGraphicManagerPIU
{
	public ArrowGraphicManagerPIUSingleOrDouble(PreferencesNoteColor preferences) : base(preferences)
	{
		StartArrowIndex = 0;
	}
}

internal sealed class ArrowGraphicManagerPIUSingleHalfDouble : ArrowGraphicManagerPIU
{
	public ArrowGraphicManagerPIUSingleHalfDouble(PreferencesNoteColor preferences) : base(preferences)
	{
		StartArrowIndex = 2;
	}
}

internal sealed class ArrowGraphicManagerPIURoutine : ArrowGraphicManagerPIUSingleOrDouble
{
	public ArrowGraphicManagerPIURoutine(PreferencesNoteColor preferences) : base(preferences)
	{
	}

	public override (string, float, Color) GetArrowTextureFill(int row, int lane, bool selected, int player)
	{
		var i = GetTextureIndex(lane);
		return (ArrowFillTextures[i], ArrowRotations[i], Preferences.GetPiuNoteColor(player, i, selected));
	}

	public override (string, bool, Color) GetHoldBodyTextureFill(int row, int lane, bool held, bool selected, int player)
	{
		var i = GetTextureIndex(lane);
		return (HoldFillTextures[i], HoldMirrored[i], Preferences.GetPiuHoldBodyColor(player, i, selected, held));
	}

	public override (string, bool, Color) GetRollBodyTextureFill(int row, int lane, bool held, bool selected, int player)
	{
		var i = GetTextureIndex(lane);
		return (RollFillTextures[i], HoldMirrored[i], Preferences.GetPiuHoldBodyColor(player, i, selected, held));
	}

	public override uint GetArrowUIColor(int row, int lane, bool selected, int player)
	{
		var i = GetTextureIndex(lane);
		return Preferences.GetPiuUINoteColor(player, i, selected);
	}

	public override uint GetHoldUIColor(int row, int lane, bool selected, int player)
	{
		var i = GetTextureIndex(lane);
		return Preferences.GetPiuUIHoldColor(player, i, selected);
	}

	public override uint GetRollUIColor(int row, int lane, bool selected, int player)
	{
		var i = GetTextureIndex(lane);
		return Preferences.GetPiuUIHoldColor(player, i, selected);
	}

	public override uint GetMineUIColor(bool selected, int player)
	{
		return Preferences.GetUIMineColor(player, selected);
	}

	public override (string, Color) GetMineFillTexture(bool selected, int player)
	{
		return (TextureIdMineFill, Preferences.GetMineColor(player, selected, false));
	}
}

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using Fumen;
using Microsoft.Xna.Framework;
using static Fumen.Converters.SMCommon;
using static StepManiaEditor.Utils;

namespace StepManiaEditor;

/// <summary>
/// Class for managing access to textures and colors for arrows based on Chart type.
/// </summary>
internal abstract class ArrowGraphicManager
{
	/// <summary>
	/// Brightness multiplier for the normal color.
	/// </summary>
	public const float ArrowUIColorMultiplier = 1.5f;

	/// <summary>
	/// Brightness multiplier for the selected color.
	/// It is intention this is large and will result in whites for many colors.
	/// This is typically used in contexts where the colored area is small so differentiating
	/// between a selected and unselected note is more important than differentiating between
	/// individual note colors.
	/// </summary>
	public const float ArrowUISelectedColorMultiplier = 8.0f;


	/// <summary>
	/// Set of colors to use for an arrow in various UI contexts.
	/// </summary>
	protected struct ArrowUIColorSet
	{
		/// <summary>
		/// RGBA Color.
		/// </summary>
		public uint Color;

		/// <summary>
		/// RGBA Selected color.
		/// </summary>
		public uint SelectedColor;

		/// <summary>
		/// Constructor taking a base color from which to generate the color set.
		/// </summary>
		/// <param name="color">Base color.</param>
		public ArrowUIColorSet(uint color)
		{
			Color = ColorRGBAMultiply(color, ArrowUIColorMultiplier);
			SelectedColor = ColorRGBAMultiply(color, ArrowUISelectedColorMultiplier);
		}

		[Pure]
		public uint GetColor(bool selected)
		{
			return selected ? SelectedColor : Color;
		}
	}

	private static readonly ArrowUIColorSet MineColor;

	private static readonly string TextureIdMine = "mine";
	private static readonly string TextureIdMineFill = "mine-fill";
	private static readonly string TextureIdMineRim = "mine-rim";
	private static readonly string TextureIdFakeMarker = "fake-marker";
	private static readonly string TextureIdLiftMarker = "lift-marker";
	private static readonly string TextureIdPlayerMarkerFill = "player-marker-fill";
	private static readonly string TextureIdPlayerMarkerRim = "player-marker-rim";

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
		[ChartType.dance_single] = new List<string>
		{
			"icon-dance-left",
			"icon-dance-down",
			"icon-dance-up",
			"icon-dance-right",
		},
		[ChartType.dance_double] = new List<string>
		{
			"icon-dance-left",
			"icon-dance-down",
			"icon-dance-up",
			"icon-dance-right",
			"icon-dance-left",
			"icon-dance-down",
			"icon-dance-up",
			"icon-dance-right",
		},
		[ChartType.dance_solo] = new List<string>
		{
			"icon-dance-left",
			"icon-dance-up-left",
			"icon-dance-down",
			"icon-dance-up",
			"icon-dance-up-right",
			"icon-dance-right",
		},
		[ChartType.dance_threepanel] = new List<string>
		{
			"icon-dance-up-left",
			"icon-dance-down",
			"icon-dance-up-right",
		},
		[ChartType.dance_routine] = new List<string>
		{
			"icon-dance-left",
			"icon-dance-down",
			"icon-dance-up",
			"icon-dance-right",
			"icon-dance-left",
			"icon-dance-down",
			"icon-dance-up",
			"icon-dance-right",
		},
		[ChartType.pump_single] = new List<string>
		{
			"icon-pump-down-left",
			"icon-pump-up-left",
			"icon-pump-center",
			"icon-pump-up-right",
			"icon-pump-down-right",
		},
		[ChartType.pump_halfdouble] = new List<string>
		{
			"icon-pump-center",
			"icon-pump-up-right",
			"icon-pump-down-right",
			"icon-pump-down-left",
			"icon-pump-up-left",
			"icon-pump-center",
		},
		[ChartType.pump_double] = new List<string>
		{
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
		},
		[ChartType.pump_routine] = new List<string>
		{
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
		},
		[ChartType.smx_beginner] = new List<string>
		{
			"icon-dance-left",
			"icon-dance-center",
			"icon-dance-right",
		},
		[ChartType.smx_single] = new List<string>
		{
			"icon-dance-left",
			"icon-dance-down",
			"icon-dance-center",
			"icon-dance-up",
			"icon-dance-right",
		},
		[ChartType.smx_dual] = new List<string>
		{
			"icon-dance-left",
			"icon-dance-center",
			"icon-dance-right",
			"icon-dance-left",
			"icon-dance-center",
			"icon-dance-right",
		},
		[ChartType.smx_full] = new List<string>
		{
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
		},
		[ChartType.smx_team] = new List<string>
		{
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
		},
	};

	private static readonly Dictionary<ChartType, List<string>> DimArrowIcons;

	static ArrowGraphicManager()
	{
		MineColor = new ArrowUIColorSet(0xFFB7B7B7); // light grey

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
		return new List<string>();
	}

	public static List<string> GetDimIcons(ChartType chartType)
	{
		if (DimArrowIcons.TryGetValue(chartType, out var icons))
			return icons;
		return new List<string>();
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
		switch (chartType)
		{
			case ChartType.dance_single:
			case ChartType.dance_double:
			case ChartType.dance_couple:
				return new ArrowGraphicManagerDanceSingleOrDouble();
			case ChartType.dance_routine:
				return new ArrowGraphicManagerDanceRoutine();
			case ChartType.dance_solo:
				return new ArrowGraphicManagerDanceSolo();
			case ChartType.dance_threepanel:
				return new ArrowGraphicManagerDanceThreePanel();

			case ChartType.smx_beginner:
				return new ArrowGraphicManagerDanceSMXBeginner();
			case ChartType.smx_single:
			case ChartType.smx_full:
				return new ArrowGraphicManagerDanceSMXSingleOrFull();
			case ChartType.smx_team:
				return new ArrowGraphicManagerDanceSMXTeam();
			case ChartType.smx_dual:
				return new ArrowGraphicManagerDanceSMXDual();

			case ChartType.pump_single:
			case ChartType.pump_double:
			case ChartType.pump_couple:
				return new ArrowGraphicManagerPIUSingleOrDouble();
			case ChartType.pump_routine:
				return new ArrowGraphicManagerPIURoutine();
			case ChartType.pump_halfdouble:
				return new ArrowGraphicManagerPIUSingleHalfDouble();

			default:
				return null;
		}
	}

	public abstract bool AreHoldCapsCentered();

	public abstract (string, float) GetReceptorTexture(int lane);
	public abstract (string, float) GetReceptorGlowTexture(int lane);
	public abstract (string, float) GetReceptorHeldTexture(int lane);

	public abstract (string, float) GetArrowTexture(int row, int lane, bool selected);
	public abstract (string, bool) GetHoldStartTexture(int row, int lane, bool held, bool selected);
	public abstract (string, bool) GetHoldBodyTexture(int row, int lane, bool held, bool selected);
	public abstract (string, float) GetHoldEndTexture(int row, int lane, bool held, bool selected);
	public abstract (string, bool) GetRollStartTexture(int row, int lane, bool held, bool selected);
	public abstract (string, bool) GetRollBodyTexture(int row, int lane, bool held, bool selected);
	public abstract (string, float) GetRollEndTexture(int row, int lane, bool held, bool selected);

	public abstract (string, float) GetPlayerArrowTextureRim(int lane, bool selected);
	public abstract (string, float, Color) GetPlayerArrowTextureFill(int row, int lane, bool selected, int player);

	public virtual (string, bool) GetPlayerHoldStartTextureRim(int lane, bool selected)
	{
		return (null, false);
	}

	public virtual (string, bool, Color) GetPlayerHoldStartTextureFill(int row, int lane, bool held, bool selected,
		int player)
	{
		return (null, false, Color.White);
	}

	public virtual (string, bool) GetPlayerHoldBodyTextureRim(int lane, bool selected)
	{
		return (null, false);
	}

	public virtual (string, bool, Color) GetPlayerHoldBodyTextureFill(int row, int lane, bool held, bool selected,
		int player)
	{
		return (null, false, Color.White);
	}

	public virtual (string, float) GetPlayerHoldEndTextureRim(int lane, bool selected)
	{
		return (null, 0.0f);
	}

	public virtual (string, float, Color) GetPlayerHoldEndTextureFill(int row, int lane, bool held, bool selected,
		int player)
	{
		return (null, 0.0f, Color.White);
	}

	public virtual (string, bool) GetPlayerRollStartTextureRim(int lane, bool selected)
	{
		return (null, false);
	}

	public virtual (string, bool, Color) GetPlayerRollStartTextureFill(int row, int lane, bool held, bool selected,
		int player)
	{
		return (null, false, Color.White);
	}

	public virtual (string, bool) GetPlayerRollBodyTextureRim(int lane, bool selected)
	{
		return (null, false);
	}

	public virtual (string, bool, Color) GetPlayerRollBodyTextureFill(int row, int lane, bool held, bool selected,
		int player)
	{
		return (null, false, Color.White);
	}

	public virtual (string, float) GetPlayerRollEndTextureRim(int lane, bool selected)
	{
		return (null, 0.0f);
	}

	public virtual (string, float, Color) GetPlayerRollEndTextureFill(int row, int lane, bool held, bool selected,
		int player)
	{
		return (null, 0.0f, Color.White);
	}

	public static uint GetArrowColorForSubdivision(int subdivision)
	{
		return ArrowGraphicManagerDance.GetDanceArrowColorForSubdivision(subdivision);
	}

	public abstract bool ShouldColorHoldsAndRollsInMultiplayerCharts();

	public abstract uint GetArrowColor(int row, int lane, bool selected, int player);
	public abstract uint GetHoldColor(int row, int lane, bool selected, int player);
	public abstract uint GetRollColor(int row, int lane, bool selected, int player);

	public static uint GetMineColor(bool selected)
	{
		return MineColor.GetColor(selected);
	}

	public string GetMineTexture(int row, int lane, bool selected)
	{
		return GetTextureId(TextureIdMine, selected);
	}

	public string GetMineRimTexture(int row, int lane, bool selected)
	{
		return GetTextureId(TextureIdMineRim, selected);
	}

	public (string, Color) GetMineFillTexture(int row, int lane, bool selected, int player)
	{
		return (TextureIdMineFill, GetColorForPlayer(player, selected, false));
	}

	public static string GetFakeMarkerTexture(int row, int lane, bool selected)
	{
		return GetTextureId(TextureIdFakeMarker, selected);
	}

	public static string GetLiftMarkerTexture(int row, int lane, bool selected)
	{
		return GetTextureId(TextureIdLiftMarker, selected);
	}

	public static string GetSnapIndicatorTexture(int subdivision)
	{
		if (subdivision == 0)
			return null;
		if (!SnapTextureByBeatSubdivision.TryGetValue(subdivision, out var texture))
			texture = SnapTextureByBeatSubdivision[16];
		return texture;
	}

	public static (string, Color) GetPlayerIndicatorFillTexture(int player)
	{
		return (TextureIdPlayerMarkerFill, GetColorForPlayer(player, false, false));
	}

	public static string GetPlayerMarkerRimTexture()
	{
		return TextureIdPlayerMarkerRim;
	}

	protected static Color GetColorForPlayer(int player, bool selected, bool held)
	{
		if (selected)
		{
			if (held)
				return Preferences.Instance.PreferencesMultiplayer.GetRoutineHeldAndSelectedNoteColor(player);
			return Preferences.Instance.PreferencesMultiplayer.GetRoutineSelectedNoteColor(player);
		}

		if (held)
			return Preferences.Instance.PreferencesMultiplayer.GetRoutineHeldNoteColor(player);
		return Preferences.Instance.PreferencesMultiplayer.GetRoutineNoteColor(player);
	}

	protected static uint GetUIColorForPlayer(int player, bool selected)
	{
		if (selected)
			return Preferences.Instance.PreferencesMultiplayer.GetRoutineSelectedUINoteColor(player);
		return Preferences.Instance.PreferencesMultiplayer.GetRoutineUINoteColor(player);
	}
}

internal abstract class ArrowGraphicManagerDance : ArrowGraphicManager
{
	protected struct UniqueDanceTextures
	{
		public string DownArrow;
		public string UpLeftArrow;
		public string CenterArrow;
	}

	protected struct HoldTextures
	{
		public UniqueDanceTextures Start;
		public UniqueDanceTextures Body;
		public UniqueDanceTextures End;
	}

	protected const string HoldAndRollBodyRimTexture = "itg-hold-body-rim";
	protected const string HoldAndRollBodyCenterRimTexture = "itg-hold-center-body-rim";
	protected const string HoldBodyFillTexture = "itg-hold-body-fill";
	protected const string HoldBodyCenterFillTexture = "itg-hold-center-body-fill";
	protected const string RollBodyFillTexture = "itg-roll-body-fill";
	protected const string RollBodyCenterFillTexture = "itg-roll-center-body-fill";
	protected const string HoldEndFillTexture = "itg-hold-end-fill";
	protected const string HoldEndCenterFillTexture = "itg-hold-center-end-fill";
	protected const string HoldAndRollEndRimTexture = "itg-hold-end-rim";
	protected const string HoldAndRollEndCenterRimTexture = "itg-hold-center-end-rim";
	protected const string RollEndFillTexture = "itg-roll-end-fill";
	protected const string RollEndCenterFillTexture = "itg-roll-center-end-fill";

	protected static readonly UniqueDanceTextures ArrowRims;
	protected static readonly UniqueDanceTextures ArrowFills;

	protected static readonly Dictionary<int, UniqueDanceTextures> ArrowTextureByBeatSubdivision;
	protected static readonly UniqueDanceTextures[] ArrowTextureByRow;
	protected static readonly HoldTextures HoldTexturesActive;
	protected static readonly HoldTextures HoldTexturesInactive;
	protected static readonly HoldTextures RollTexturesActive;
	protected static readonly HoldTextures RollTexturesInactive;

	protected static readonly UniqueDanceTextures ReceptorTextures;
	protected static readonly UniqueDanceTextures ReceptorGlowTextures;
	protected static readonly UniqueDanceTextures ReceptorHeldTextures;

	protected static readonly Dictionary<int, ArrowUIColorSet> ArrowColorBySubdivision;
	protected static readonly ArrowUIColorSet[] ArrowColorByRow;
	protected static readonly ArrowUIColorSet HoldColor;
	protected static readonly ArrowUIColorSet RollColor;

	static ArrowGraphicManagerDance()
	{
		ArrowRims = new UniqueDanceTextures
		{
			DownArrow = "itg-down-rim",
			UpLeftArrow = "itg-solo-rim",
			CenterArrow = "itg-center-rim",
		};
		ArrowFills = new UniqueDanceTextures
		{
			DownArrow = "itg-down-fill",
			UpLeftArrow = "itg-solo-fill",
			CenterArrow = "itg-center-fill",
		};

		ArrowTextureByBeatSubdivision = new Dictionary<int, UniqueDanceTextures>
		{
			{
				1,
				new UniqueDanceTextures
					{ DownArrow = "itg-down-1-4", UpLeftArrow = "itg-solo-1-4", CenterArrow = "itg-center-1-4" }
			},
			{
				2,
				new UniqueDanceTextures
					{ DownArrow = "itg-down-1-8", UpLeftArrow = "itg-solo-1-8", CenterArrow = "itg-center-1-8" }
			},
			{
				3,
				new UniqueDanceTextures
					{ DownArrow = "itg-down-1-12", UpLeftArrow = "itg-solo-1-12", CenterArrow = "itg-center-1-12" }
			},
			{
				4,
				new UniqueDanceTextures
					{ DownArrow = "itg-down-1-16", UpLeftArrow = "itg-solo-1-16", CenterArrow = "itg-center-1-16" }
			},
			{
				6,
				new UniqueDanceTextures
					{ DownArrow = "itg-down-1-24", UpLeftArrow = "itg-solo-1-24", CenterArrow = "itg-center-1-24" }
			},
			{
				8,
				new UniqueDanceTextures
					{ DownArrow = "itg-down-1-32", UpLeftArrow = "itg-solo-1-32", CenterArrow = "itg-center-1-32" }
			},
			{
				12,
				new UniqueDanceTextures
					{ DownArrow = "itg-down-1-48", UpLeftArrow = "itg-solo-1-48", CenterArrow = "itg-center-1-48" }
			},
			{
				16,
				new UniqueDanceTextures
					{ DownArrow = "itg-down-1-64", UpLeftArrow = "itg-solo-1-64", CenterArrow = "itg-center-1-64" }
			},
		};

		ArrowTextureByRow = new UniqueDanceTextures[MaxValidDenominator];
		for (var i = 0; i < MaxValidDenominator; i++)
		{
			var key = new Fraction(i, MaxValidDenominator).Reduce().Denominator;
			if (!ArrowTextureByBeatSubdivision.ContainsKey(key))
				key = 16;
			ArrowTextureByRow[i] = ArrowTextureByBeatSubdivision[key];
		}

		HoldTexturesActive = new HoldTextures
		{
			Start = new UniqueDanceTextures
			{
				DownArrow = null,
				CenterArrow = null,
				UpLeftArrow = "itg-hold-solo-start-active",
			},
			Body = new UniqueDanceTextures
			{
				DownArrow = "itg-hold-body-active",
				CenterArrow = "itg-hold-center-body-active",
				UpLeftArrow = "itg-hold-solo-body-active",
			},
			End = new UniqueDanceTextures
			{
				DownArrow = "itg-hold-end-active",
				CenterArrow = "itg-hold-center-end-active",
				UpLeftArrow = "itg-hold-solo-end-active",
			},
		};
		HoldTexturesInactive = new HoldTextures
		{
			Start = new UniqueDanceTextures
			{
				DownArrow = null,
				CenterArrow = null,
				UpLeftArrow = "itg-hold-solo-start-inactive",
			},
			Body = new UniqueDanceTextures
			{
				DownArrow = "itg-hold-body-inactive",
				CenterArrow = "itg-hold-center-body-inactive",
				UpLeftArrow = "itg-hold-solo-body-inactive",
			},
			End = new UniqueDanceTextures
			{
				DownArrow = "itg-hold-end-inactive",
				CenterArrow = "itg-hold-center-end-inactive",
				UpLeftArrow = "itg-hold-solo-end-inactive",
			},
		};

		RollTexturesActive = new HoldTextures
		{
			Start = new UniqueDanceTextures
			{
				DownArrow = null,
				CenterArrow = null,
				UpLeftArrow = "itg-roll-solo-start-active",
			},
			Body = new UniqueDanceTextures
			{
				DownArrow = "itg-roll-body-active",
				CenterArrow = "itg-roll-center-body-active",
				UpLeftArrow = "itg-roll-solo-body-active",
			},
			End = new UniqueDanceTextures
			{
				DownArrow = "itg-roll-end-active",
				CenterArrow = "itg-roll-center-end-active",
				UpLeftArrow = "itg-roll-solo-end-active",
			},
		};
		RollTexturesInactive = new HoldTextures
		{
			Start = new UniqueDanceTextures
			{
				DownArrow = null,
				CenterArrow = null,
				UpLeftArrow = "itg-roll-solo-start-inactive",
			},
			Body = new UniqueDanceTextures
			{
				DownArrow = "itg-roll-body-inactive",
				CenterArrow = "itg-roll-center-body-inactive",
				UpLeftArrow = "itg-roll-solo-body-inactive",
			},
			End = new UniqueDanceTextures
			{
				DownArrow = "itg-roll-end-inactive",
				CenterArrow = "itg-roll-center-end-inactive",
				UpLeftArrow = "itg-roll-solo-end-inactive",
			},
		};

		ReceptorTextures = new UniqueDanceTextures
		{
			DownArrow = "itg-down-receptor",
			CenterArrow = "itg-center-receptor",
			UpLeftArrow = "itg-solo-receptor",
		};
		ReceptorGlowTextures = new UniqueDanceTextures
		{
			DownArrow = "itg-down-receptor-glow",
			CenterArrow = "itg-center-receptor-glow",
			UpLeftArrow = "itg-solo-receptor-glow",
		};
		ReceptorHeldTextures = new UniqueDanceTextures
		{
			DownArrow = "itg-down-receptor-held",
			CenterArrow = "itg-center-receptor-held",
			UpLeftArrow = "itg-solo-receptor-held",
		};

		ArrowColorBySubdivision = new Dictionary<int, ArrowUIColorSet>
		{
			{ 1, new ArrowUIColorSet(0xFF1818B6) }, // Red
			{ 2, new ArrowUIColorSet(0xFFB63518) }, // Blue
			{ 3, new ArrowUIColorSet(0xFF37AD36) }, // Green
			{ 4, new ArrowUIColorSet(0xFF16CAD1) }, // Yellow
			{ 6, new ArrowUIColorSet(0xFFB61884) }, // Purple
			{ 8, new ArrowUIColorSet(0xFF98B618) }, // Cyan
			{ 12, new ArrowUIColorSet(0xFF8018B6) }, // Pink
			{ 16, new ArrowUIColorSet(0xFF586F4F) }, // Pale Grey Green
			{ 48, new ArrowUIColorSet(0xFF586F4F) }, // Pale Grey Green
		};
		ArrowColorByRow = new ArrowUIColorSet[MaxValidDenominator];
		for (var i = 0; i < MaxValidDenominator; i++)
		{
			var key = new Fraction(i, MaxValidDenominator).Reduce().Denominator;

			if (!ArrowColorBySubdivision.ContainsKey(key))
				key = 16;
			ArrowColorByRow[i] = ArrowColorBySubdivision[key];
		}

		HoldColor = new ArrowUIColorSet(0xFF696969); // Grey
		RollColor = new ArrowUIColorSet(0xFF2264A6); // Orange
	}

	public override bool AreHoldCapsCentered()
	{
		return false;
	}

	public override bool ShouldColorHoldsAndRollsInMultiplayerCharts()
	{
		return Preferences.Instance.PreferencesMultiplayer.ColorHoldsAndRolls;
	}

	public override uint GetArrowColor(int row, int lane, bool selected, int player)
	{
		return ArrowColorByRow[row % MaxValidDenominator].GetColor(selected);
	}

	public static uint GetDanceArrowColorForSubdivision(int subdivision)
	{
		return ArrowColorBySubdivision[subdivision].GetColor(false);
	}

	public override uint GetHoldColor(int row, int lane, bool selected, int player)
	{
		return HoldColor.GetColor(selected);
	}

	public override uint GetRollColor(int row, int lane, bool selected, int player)
	{
		return RollColor.GetColor(selected);
	}
}

internal class ArrowGraphicManagerDanceSingleOrDouble : ArrowGraphicManagerDance
{
	private static readonly float[] ArrowRotations =
	{
		(float)Math.PI * 0.5f, // L
		0.0f, // D
		(float)Math.PI, // U
		(float)Math.PI * 1.5f, // R
	};

	public override (string, float) GetArrowTexture(int row, int lane, bool selected)
	{
		return (GetTextureId(ArrowTextureByRow[row % MaxValidDenominator].DownArrow, selected),
			ArrowRotations[lane % 4]);
	}

	public override (string, float) GetReceptorGlowTexture(int lane)
	{
		return (ReceptorGlowTextures.DownArrow, ArrowRotations[lane % 4]);
	}

	public override (string, float) GetReceptorHeldTexture(int lane)
	{
		return (ReceptorHeldTextures.DownArrow, ArrowRotations[lane % 4]);
	}

	public override (string, float) GetReceptorTexture(int lane)
	{
		return (ReceptorTextures.DownArrow, ArrowRotations[lane % 4]);
	}

	public override (string, bool) GetHoldBodyTexture(int row, int lane, bool held, bool selected)
	{
		return (GetTextureId(held ? HoldTexturesActive.Body.DownArrow : HoldTexturesInactive.Body.DownArrow, selected), false);
	}

	public override (string, float) GetHoldEndTexture(int row, int lane, bool held, bool selected)
	{
		return (GetTextureId(held ? HoldTexturesActive.End.DownArrow : HoldTexturesInactive.End.DownArrow, selected), 0.0f);
	}

	public override (string, bool) GetHoldStartTexture(int row, int lane, bool held, bool selected)
	{
		return (GetTextureId(held ? HoldTexturesActive.Start.DownArrow : HoldTexturesInactive.Start.DownArrow, selected), false);
	}

	public override (string, bool) GetRollBodyTexture(int row, int lane, bool held, bool selected)
	{
		return (GetTextureId(held ? RollTexturesActive.Body.DownArrow : RollTexturesInactive.Body.DownArrow, selected), false);
	}

	public override (string, float) GetRollEndTexture(int row, int lane, bool held, bool selected)
	{
		return (GetTextureId(held ? RollTexturesActive.End.DownArrow : RollTexturesInactive.End.DownArrow, selected), 0.0f);
	}

	public override (string, bool) GetRollStartTexture(int row, int lane, bool held, bool selected)
	{
		return (GetTextureId(held ? RollTexturesActive.Start.DownArrow : RollTexturesInactive.Start.DownArrow, selected), false);
	}

	public override (string, float) GetPlayerArrowTextureRim(int lane, bool selected)
	{
		return (GetTextureId(ArrowRims.DownArrow, selected), ArrowRotations[lane % 4]);
	}

	public override (string, float, Color) GetPlayerArrowTextureFill(int row, int lane, bool selected, int player)
	{
		return (ArrowFills.DownArrow, ArrowRotations[lane % 4], GetColorForPlayer(player, selected, false));
	}
}

internal sealed class ArrowGraphicManagerDanceRoutine : ArrowGraphicManagerDanceSingleOrDouble
{
	public override uint GetArrowColor(int row, int lane, bool selected, int player)
	{
		return GetUIColorForPlayer(player, selected);
	}

	public override uint GetHoldColor(int row, int lane, bool selected, int player)
	{
		if (!ShouldColorHoldsAndRollsInMultiplayerCharts())
			return base.GetHoldColor(row, lane, selected, player);
		return GetUIColorForPlayer(player, selected);
	}

	public override uint GetRollColor(int row, int lane, bool selected, int player)
	{
		if (!ShouldColorHoldsAndRollsInMultiplayerCharts())
			return base.GetRollColor(row, lane, selected, player);
		return GetUIColorForPlayer(player, selected);
	}

	public override (string, bool) GetPlayerHoldBodyTextureRim(int lane, bool selected)
	{
		if (!ShouldColorHoldsAndRollsInMultiplayerCharts())
			return base.GetPlayerHoldBodyTextureRim(lane, selected);
		return (GetTextureId(HoldAndRollBodyRimTexture, selected), false);
	}

	public override (string, bool, Color) GetPlayerHoldBodyTextureFill(int row, int lane, bool held, bool selected,
		int player)
	{
		if (!ShouldColorHoldsAndRollsInMultiplayerCharts())
			return base.GetPlayerHoldBodyTextureFill(row, lane, held, selected, player);
		return (HoldBodyFillTexture, false, GetColorForPlayer(player, selected, held));
	}

	public override (string, float) GetPlayerHoldEndTextureRim(int lane, bool selected)
	{
		if (!ShouldColorHoldsAndRollsInMultiplayerCharts())
			return base.GetPlayerHoldEndTextureRim(lane, selected);
		return (GetTextureId(HoldAndRollEndRimTexture, selected), 0.0f);
	}

	public override (string, float, Color) GetPlayerHoldEndTextureFill(int row, int lane, bool held, bool selected,
		int player)
	{
		if (!ShouldColorHoldsAndRollsInMultiplayerCharts())
			return base.GetPlayerHoldEndTextureFill(row, lane, held, selected, player);
		return (HoldEndFillTexture, 0.0f, GetColorForPlayer(player, selected, held));
	}

	public override (string, bool) GetPlayerRollBodyTextureRim(int lane, bool selected)
	{
		if (!ShouldColorHoldsAndRollsInMultiplayerCharts())
			return base.GetPlayerRollBodyTextureRim(lane, selected);
		return (GetTextureId(HoldAndRollBodyRimTexture, selected), false);
	}

	public override (string, bool, Color) GetPlayerRollBodyTextureFill(int row, int lane, bool held, bool selected,
		int player)
	{
		if (!ShouldColorHoldsAndRollsInMultiplayerCharts())
			return base.GetPlayerRollBodyTextureFill(row, lane, held, selected, player);
		return (RollBodyFillTexture, false, GetColorForPlayer(player, selected, held));
	}

	public override (string, float) GetPlayerRollEndTextureRim(int lane, bool selected)
	{
		if (!ShouldColorHoldsAndRollsInMultiplayerCharts())
			return base.GetPlayerRollEndTextureRim(lane, selected);
		return (GetTextureId(HoldAndRollEndRimTexture, selected), 0.0f);
	}

	public override (string, float, Color) GetPlayerRollEndTextureFill(int row, int lane, bool held, bool selected,
		int player)
	{
		if (!ShouldColorHoldsAndRollsInMultiplayerCharts())
			return base.GetPlayerRollEndTextureFill(row, lane, held, selected, player);
		return (RollEndFillTexture, 0.0f, GetColorForPlayer(player, selected, held));
	}
}

internal abstract class ArrowGraphicManagerDanceSoloBase : ArrowGraphicManagerDance
{
	protected abstract bool ShouldUseUpLeftArrow(int lane);
	protected abstract float GetRotation(int lane);

	public override (string, float) GetArrowTexture(int row, int lane, bool selected)
	{
		if (ShouldUseUpLeftArrow(lane))
		{
			return (GetTextureId(ArrowTextureByRow[row % MaxValidDenominator].UpLeftArrow, selected),
				GetRotation(lane));
		}

		return (GetTextureId(ArrowTextureByRow[row % MaxValidDenominator].DownArrow, selected),
			GetRotation(lane));
	}

	public override (string, float) GetReceptorGlowTexture(int lane)
	{
		if (ShouldUseUpLeftArrow(lane))
		{
			return (ReceptorGlowTextures.UpLeftArrow, GetRotation(lane));
		}

		return (ReceptorGlowTextures.DownArrow, GetRotation(lane));
	}

	public override (string, float) GetReceptorHeldTexture(int lane)
	{
		if (ShouldUseUpLeftArrow(lane))
		{
			return (ReceptorHeldTextures.UpLeftArrow, GetRotation(lane));
		}

		return (ReceptorHeldTextures.DownArrow, GetRotation(lane));
	}

	public override (string, float) GetReceptorTexture(int lane)
	{
		if (ShouldUseUpLeftArrow(lane))
		{
			return (ReceptorTextures.UpLeftArrow, GetRotation(lane));
		}

		return (ReceptorTextures.DownArrow, GetRotation(lane));
	}

	public override (string, bool) GetHoldBodyTexture(int row, int lane, bool held, bool selected)
	{
		// Always use the narrower diagonal hold graphics in solo.
		return (GetTextureId(held ? HoldTexturesActive.Body.UpLeftArrow : HoldTexturesInactive.Body.UpLeftArrow, selected),
			false);
	}

	public override (string, float) GetHoldEndTexture(int row, int lane, bool held, bool selected)
	{
		// Always use the narrower diagonal hold graphics in solo.
		return (GetTextureId(held ? HoldTexturesActive.End.UpLeftArrow : HoldTexturesInactive.End.UpLeftArrow, selected), 0.0f);
	}

	public override (string, bool) GetHoldStartTexture(int row, int lane, bool held, bool selected)
	{
		// Always use the narrower diagonal hold graphics in solo.
		// But only use the start graphic for diagonal arrows since they have a gap needing to be filled.
		if (ShouldUseUpLeftArrow(lane))
		{
			return (GetTextureId(held ? HoldTexturesActive.Start.UpLeftArrow : HoldTexturesInactive.Start.UpLeftArrow, selected),
				false);
		}

		return (GetTextureId(held ? HoldTexturesActive.Start.DownArrow : HoldTexturesInactive.Start.DownArrow, selected), false);
	}

	public override (string, bool) GetRollBodyTexture(int row, int lane, bool held, bool selected)
	{
		// Always use the narrower diagonal hold graphics in solo.
		return (GetTextureId(held ? RollTexturesActive.Body.UpLeftArrow : RollTexturesInactive.Body.UpLeftArrow, selected),
			false);
	}

	public override (string, float) GetRollEndTexture(int row, int lane, bool held, bool selected)
	{
		// Always use the narrower diagonal hold graphics in solo.
		return (GetTextureId(held ? RollTexturesActive.End.UpLeftArrow : RollTexturesInactive.End.UpLeftArrow, selected), 0.0f);
	}

	public override (string, bool) GetRollStartTexture(int row, int lane, bool held, bool selected)
	{
		// Always use the narrower diagonal hold graphics in solo.
		// But only use the start graphic for diagonal arrows since they have a gap needing to be filled.
		if (ShouldUseUpLeftArrow(lane))
		{
			return (GetTextureId(held ? RollTexturesActive.Start.UpLeftArrow : RollTexturesInactive.Start.UpLeftArrow, selected),
				false);
		}

		return (GetTextureId(held ? RollTexturesActive.Start.DownArrow : RollTexturesInactive.Start.DownArrow, selected), false);
	}

	public override (string, float) GetPlayerArrowTextureRim(int lane, bool selected)
	{
		if (ShouldUseUpLeftArrow(lane))
		{
			return (GetTextureId(ArrowRims.UpLeftArrow, selected), GetRotation(lane));
		}

		return (GetTextureId(ArrowRims.DownArrow, selected), GetRotation(lane));
	}

	public override (string, float, Color) GetPlayerArrowTextureFill(int row, int lane, bool selected, int player)
	{
		if (ShouldUseUpLeftArrow(lane))
		{
			return (ArrowFills.UpLeftArrow, GetRotation(lane), GetColorForPlayer(player, selected, false));
		}

		return (ArrowFills.DownArrow, GetRotation(lane), GetColorForPlayer(player, selected, false));
	}
}

internal sealed class ArrowGraphicManagerDanceSolo : ArrowGraphicManagerDanceSoloBase
{
	private static readonly float[] ArrowRotations =
	{
		(float)Math.PI * 0.5f, // L
		0.0f, // UL
		0.0f, // D
		(float)Math.PI, // U
		(float)Math.PI * 0.5f, // UR
		(float)Math.PI * 1.5f, // R
	};

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
	{
		0.0f, // UL
		0.0f, // D
		(float)Math.PI * 0.5f, // UR
	};

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

	public override (string, float) GetArrowTexture(int row, int lane, bool selected)
	{
		if (ShouldUseCenterArrow(lane))
		{
			return (GetTextureId(ArrowTextureByRow[row % MaxValidDenominator].CenterArrow, selected),
				GetRotation(lane));
		}

		return (GetTextureId(ArrowTextureByRow[row % MaxValidDenominator].DownArrow, selected),
			GetRotation(lane));
	}

	public override (string, float) GetReceptorGlowTexture(int lane)
	{
		if (ShouldUseCenterArrow(lane))
		{
			return (ReceptorGlowTextures.CenterArrow, GetRotation(lane));
		}

		return (ReceptorGlowTextures.DownArrow, GetRotation(lane));
	}

	public override (string, float) GetReceptorHeldTexture(int lane)
	{
		if (ShouldUseCenterArrow(lane))
		{
			return (ReceptorHeldTextures.CenterArrow, GetRotation(lane));
		}

		return (ReceptorHeldTextures.DownArrow, GetRotation(lane));
	}

	public override (string, float) GetReceptorTexture(int lane)
	{
		if (ShouldUseCenterArrow(lane))
		{
			return (ReceptorTextures.CenterArrow, GetRotation(lane));
		}

		return (ReceptorTextures.DownArrow, GetRotation(lane));
	}

	public override (string, bool) GetHoldBodyTexture(int row, int lane, bool held, bool selected)
	{
		return (GetTextureId(held ? HoldTexturesActive.Body.CenterArrow : HoldTexturesInactive.Body.CenterArrow, selected),
			false);
	}

	public override (string, float) GetHoldEndTexture(int row, int lane, bool held, bool selected)
	{
		return (GetTextureId(held ? HoldTexturesActive.End.CenterArrow : HoldTexturesInactive.End.CenterArrow, selected), 0.0f);
	}

	public override (string, bool) GetHoldStartTexture(int row, int lane, bool held, bool selected)
	{
		return (GetTextureId(held ? HoldTexturesActive.Start.CenterArrow : HoldTexturesInactive.Start.CenterArrow, selected),
			false);
	}

	public override (string, bool) GetRollBodyTexture(int row, int lane, bool held, bool selected)
	{
		return (GetTextureId(held ? RollTexturesActive.Body.CenterArrow : RollTexturesInactive.Body.CenterArrow, selected),
			false);
	}

	public override (string, float) GetRollEndTexture(int row, int lane, bool held, bool selected)
	{
		return (GetTextureId(held ? RollTexturesActive.End.CenterArrow : RollTexturesInactive.End.CenterArrow, selected), 0.0f);
	}

	public override (string, bool) GetRollStartTexture(int row, int lane, bool held, bool selected)
	{
		return (GetTextureId(held ? RollTexturesActive.Start.CenterArrow : RollTexturesInactive.Start.CenterArrow, selected),
			false);
	}

	public override (string, float) GetPlayerArrowTextureRim(int lane, bool selected)
	{
		if (ShouldUseCenterArrow(lane))
		{
			return (GetTextureId(ArrowRims.CenterArrow, selected), GetRotation(lane));
		}

		return (GetTextureId(ArrowRims.DownArrow, selected), GetRotation(lane));
	}

	public override (string, float, Color) GetPlayerArrowTextureFill(int row, int lane, bool selected, int player)
	{
		if (ShouldUseCenterArrow(lane))
		{
			return (ArrowFills.CenterArrow, GetRotation(lane), GetColorForPlayer(player, selected, false));
		}

		return (ArrowFills.DownArrow, GetRotation(lane), GetColorForPlayer(player, selected, false));
	}
}

internal sealed class ArrowGraphicManagerDanceSMXBeginner : ArrowGraphicManagerDanceSMX
{
	private static readonly float[] ArrowRotations =
	{
		(float)Math.PI * 0.5f, // L
		0.0f, // Center
		(float)Math.PI * 1.5f, // R
	};

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
	{
		(float)Math.PI * 0.5f, // L
		0.0f, // D
		0.0f, // Center
		(float)Math.PI, // U
		(float)Math.PI * 1.5f, // R
	};

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
	{
		(float)Math.PI * 0.5f, // L
		0.0f, // Center
		(float)Math.PI * 1.5f, // R
		(float)Math.PI * 0.5f, // L
		0.0f, // Center
		(float)Math.PI * 1.5f, // R
	};

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
	public override uint GetArrowColor(int row, int lane, bool selected, int player)
	{
		return GetUIColorForPlayer(player, selected);
	}

	public override uint GetHoldColor(int row, int lane, bool selected, int player)
	{
		if (!ShouldColorHoldsAndRollsInMultiplayerCharts())
			return base.GetHoldColor(row, lane, selected, player);
		return GetUIColorForPlayer(player, selected);
	}

	public override uint GetRollColor(int row, int lane, bool selected, int player)
	{
		if (!ShouldColorHoldsAndRollsInMultiplayerCharts())
			return base.GetRollColor(row, lane, selected, player);
		return GetUIColorForPlayer(player, selected);
	}

	public override (string, bool) GetPlayerHoldBodyTextureRim(int lane, bool selected)
	{
		if (!ShouldColorHoldsAndRollsInMultiplayerCharts())
			return base.GetPlayerHoldBodyTextureRim(lane, selected);
		return (GetTextureId(HoldAndRollBodyCenterRimTexture, selected), false);
	}

	public override (string, bool, Color) GetPlayerHoldBodyTextureFill(int row, int lane, bool held, bool selected,
		int player)
	{
		if (!ShouldColorHoldsAndRollsInMultiplayerCharts())
			return base.GetPlayerHoldBodyTextureFill(row, lane, held, selected, player);
		return (HoldBodyCenterFillTexture, false, GetColorForPlayer(player, selected, held));
	}

	public override (string, float) GetPlayerHoldEndTextureRim(int lane, bool selected)
	{
		if (!ShouldColorHoldsAndRollsInMultiplayerCharts())
			return base.GetPlayerHoldEndTextureRim(lane, selected);
		return (GetTextureId(HoldAndRollEndCenterRimTexture, selected), 0.0f);
	}

	public override (string, float, Color) GetPlayerHoldEndTextureFill(int row, int lane, bool held, bool selected,
		int player)
	{
		if (!ShouldColorHoldsAndRollsInMultiplayerCharts())
			return base.GetPlayerHoldEndTextureFill(row, lane, held, selected, player);
		return (HoldEndCenterFillTexture, 0.0f, GetColorForPlayer(player, selected, held));
	}

	public override (string, bool) GetPlayerRollBodyTextureRim(int lane, bool selected)
	{
		if (!ShouldColorHoldsAndRollsInMultiplayerCharts())
			return base.GetPlayerRollBodyTextureRim(lane, selected);
		return (GetTextureId(HoldAndRollBodyCenterRimTexture, selected), false);
	}

	public override (string, bool, Color) GetPlayerRollBodyTextureFill(int row, int lane, bool held, bool selected,
		int player)
	{
		if (!ShouldColorHoldsAndRollsInMultiplayerCharts())
			return base.GetPlayerRollBodyTextureFill(row, lane, held, selected, player);
		return (RollBodyCenterFillTexture, false, GetColorForPlayer(player, selected, held));
	}

	public override (string, float) GetPlayerRollEndTextureRim(int lane, bool selected)
	{
		if (!ShouldColorHoldsAndRollsInMultiplayerCharts())
			return base.GetPlayerRollEndTextureRim(lane, selected);
		return (GetTextureId(HoldAndRollEndCenterRimTexture, selected), 0.0f);
	}

	public override (string, float, Color) GetPlayerRollEndTextureFill(int row, int lane, bool held, bool selected,
		int player)
	{
		if (!ShouldColorHoldsAndRollsInMultiplayerCharts())
			return base.GetPlayerRollEndTextureFill(row, lane, held, selected, player);
		return (RollEndCenterFillTexture, 0.0f, GetColorForPlayer(player, selected, held));
	}
}

internal abstract class ArrowGraphicManagerPIU : ArrowGraphicManager
{
	protected static readonly ArrowUIColorSet ArrowColorRed;
	protected static readonly ArrowUIColorSet ArrowColorBlue;
	protected static readonly ArrowUIColorSet ArrowColorYellow;

	protected static readonly ArrowUIColorSet HoldColorRed;
	protected static readonly ArrowUIColorSet HoldColorBlue;
	protected static readonly ArrowUIColorSet HoldColorYellow;

	protected static readonly ArrowUIColorSet RollColorRed;
	protected static readonly ArrowUIColorSet RollColorBlue;
	protected static readonly ArrowUIColorSet RollColorYellow;

	protected static readonly float[] ArrowRotationsColored =
	{
		0.0f, // DL
		0.0f, // UL
		0.0f, // C
		(float)Math.PI * 0.5f, // UR
		(float)Math.PI * 1.5f, // DR
	};

	protected static readonly float[] ArrowRotations =
	{
		(float)Math.PI * 1.5f, // DL
		0.0f, // UL
		0.0f, // C
		(float)Math.PI * 0.5f, // UR
		(float)Math.PI, // DR
	};

	protected static readonly string[] ArrowRimTextures =
	{
		"piu-diagonal-rim", // DL
		"piu-diagonal-rim", // UL
		"piu-center-rim", // C
		"piu-diagonal-rim", // UR
		"piu-diagonal-rim", // DR
	};

	protected static readonly string[] ArrowFillTextures =
	{
		"piu-diagonal-fill", // DL
		"piu-diagonal-fill", // UL
		"piu-center-fill", // C
		"piu-diagonal-fill", // UR
		"piu-diagonal-fill", // DR
	};

	protected static readonly string[] ArrowHoldAndRollRimTextures =
	{
		"piu-hold-diagonal-rim", // DL
		"piu-hold-diagonal-rim", // UL
		"piu-hold-center-rim", // C
		"piu-hold-diagonal-rim", // UR
		"piu-hold-diagonal-rim", // DR
	};

	protected static readonly string[] ArrowHoldFillTextures =
	{
		"piu-hold-diagonal-fill", // DL
		"piu-hold-diagonal-fill", // UL
		"piu-hold-center-fill", // C
		"piu-hold-diagonal-fill", // UR
		"piu-hold-diagonal-fill", // DR
	};

	protected static readonly string[] ArrowRollFillTextures =
	{
		"piu-roll-diagonal-fill", // DL
		"piu-roll-diagonal-fill", // UL
		"piu-roll-center-fill", // C
		"piu-roll-diagonal-fill", // UR
		"piu-roll-diagonal-fill", // DR
	};

	protected static readonly string[] ReceptorTextures =
	{
		"piu-diagonal-receptor", // DL
		"piu-diagonal-receptor", // UL
		"piu-center-receptor", // C
		"piu-diagonal-receptor", // UR
		"piu-diagonal-receptor", // DR
	};

	protected static readonly string[] ReceptorGlowTextures =
	{
		"piu-diagonal-receptor-glow", // DL
		"piu-diagonal-receptor-glow", // UL
		"piu-center-receptor-glow", // C
		"piu-diagonal-receptor-glow", // UR
		"piu-diagonal-receptor-glow", // DR
	};

	protected static readonly string[] ReceptorHeldTextures =
	{
		"piu-diagonal-receptor-held", // DL
		"piu-diagonal-receptor-held", // UL
		"piu-center-receptor-held", // C
		"piu-diagonal-receptor-held", // UR
		"piu-diagonal-receptor-held", // DR
	};

	protected static readonly string[] ArrowTextures =
	{
		"piu-diagonal-blue", // DL
		"piu-diagonal-red", // UL
		"piu-center", // C
		"piu-diagonal-red", // UR
		"piu-diagonal-blue", // DR
	};

	protected static readonly string[] HoldTextures =
	{
		"piu-hold-blue", // DL
		"piu-hold-red", // UL
		"piu-hold-center", // C
		"piu-hold-red", // UR
		"piu-hold-blue", // DR
	};

	protected static readonly string[] RollTextures =
	{
		"piu-roll-blue", // DL
		"piu-roll-red", // UL
		"piu-roll-center", // C
		"piu-roll-red", // UR
		"piu-roll-blue", // DR
	};

	protected static readonly bool[] HoldMirrored =
	{
		false, // DL
		false, // UL
		false, // C
		true, // UR
		true, // DR
	};

	protected static readonly ArrowUIColorSet[] ArrowColors;
	protected static readonly ArrowUIColorSet[] HoldColors;
	protected static readonly ArrowUIColorSet[] RollColors;

	protected int StartArrowIndex;

	static ArrowGraphicManagerPIU()
	{
		ArrowColorRed = new ArrowUIColorSet(0xFF371BB3);
		ArrowColorBlue = new ArrowUIColorSet(0xFFB3401B);
		ArrowColorYellow = new ArrowUIColorSet(0xFF00EAFF);

		HoldColorRed = new ArrowUIColorSet(0xFF5039B2);
		HoldColorBlue = new ArrowUIColorSet(0xFFB35639);
		HoldColorYellow = new ArrowUIColorSet(0xFF6BF3FF);

		RollColorRed = new ArrowUIColorSet(0xFF6B54F8);
		RollColorBlue = new ArrowUIColorSet(0xFFB38C1B);
		RollColorYellow = new ArrowUIColorSet(0xFF2FABB5);

		ArrowColors = new[]
		{
			ArrowColorBlue,
			ArrowColorRed,
			ArrowColorYellow,
			ArrowColorRed,
			ArrowColorBlue,
		};
		HoldColors = new[]
		{
			HoldColorBlue,
			HoldColorRed,
			HoldColorYellow,
			HoldColorRed,
			HoldColorBlue,
		};
		RollColors = new[]
		{
			RollColorBlue,
			RollColorRed,
			RollColorYellow,
			RollColorRed,
			RollColorBlue,
		};
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

	public override (string, float) GetArrowTexture(int row, int lane, bool selected)
	{
		var i = GetTextureIndex(lane);
		return (GetTextureId(ArrowTextures[i], selected), ArrowRotationsColored[i]);
	}

	public override (string, bool) GetHoldStartTexture(int row, int lane, bool held, bool selected)
	{
		return (null, false);
	}

	public override (string, bool) GetHoldBodyTexture(int row, int lane, bool held, bool selected)
	{
		var i = GetTextureIndex(lane);
		return (GetTextureId(HoldTextures[i], selected), HoldMirrored[i]);
	}

	public override (string, float) GetHoldEndTexture(int row, int lane, bool held, bool selected)
	{
		return GetArrowTexture(row, lane, selected);
	}

	public override (string, bool) GetRollStartTexture(int row, int lane, bool held, bool selected)
	{
		return (null, false);
	}

	public override (string, bool) GetRollBodyTexture(int row, int lane, bool held, bool selected)
	{
		var i = GetTextureIndex(lane);
		return (GetTextureId(RollTextures[i], selected), HoldMirrored[i]);
	}

	public override (string, float) GetRollEndTexture(int row, int lane, bool held, bool selected)
	{
		return GetArrowTexture(row, lane, selected);
	}

	public override uint GetArrowColor(int row, int lane, bool selected, int player)
	{
		var i = GetTextureIndex(lane);
		return ArrowColors[i].GetColor(selected);
	}

	public override uint GetHoldColor(int row, int lane, bool selected, int player)
	{
		var i = GetTextureIndex(lane);
		return HoldColors[i].GetColor(selected);
	}

	public override uint GetRollColor(int row, int lane, bool selected, int player)
	{
		var i = GetTextureIndex(lane);
		return RollColors[i].GetColor(selected);
	}

	public override (string, float) GetPlayerArrowTextureRim(int lane, bool selected)
	{
		var i = GetTextureIndex(lane);
		return (GetTextureId(ArrowRimTextures[i], selected), ArrowRotations[i]);
	}

	public override (string, float, Color) GetPlayerArrowTextureFill(int row, int lane, bool selected, int player)
	{
		var i = GetTextureIndex(lane);
		return (ArrowFillTextures[i], ArrowRotations[i], GetColorForPlayer(player, selected, false));
	}

	public override (string, bool) GetPlayerHoldBodyTextureRim(int lane, bool selected)
	{
		var i = GetTextureIndex(lane);
		return (GetTextureId(ArrowHoldAndRollRimTextures[i], selected), HoldMirrored[i]);
	}

	public override (string, bool, Color) GetPlayerHoldBodyTextureFill(int row, int lane, bool held, bool selected,
		int player)
	{
		var i = GetTextureIndex(lane);
		return (ArrowHoldFillTextures[i], HoldMirrored[i], GetColorForPlayer(player, selected, false));
	}

	public override (string, float) GetPlayerHoldEndTextureRim(int lane, bool selected)
	{
		return GetPlayerArrowTextureRim(lane, selected);
	}

	public override (string, float, Color) GetPlayerHoldEndTextureFill(int row, int lane, bool held, bool selected,
		int player)
	{
		var i = GetTextureIndex(lane);
		return (ArrowFillTextures[i], ArrowRotations[i], GetColorForPlayer(player, selected, false));
	}

	public override (string, bool) GetPlayerRollBodyTextureRim(int lane, bool selected)
	{
		var i = GetTextureIndex(lane);
		return (GetTextureId(ArrowHoldAndRollRimTextures[i], selected), HoldMirrored[i]);
	}

	public override (string, bool, Color) GetPlayerRollBodyTextureFill(int row, int lane, bool held, bool selected,
		int player)
	{
		var i = GetTextureIndex(lane);
		return (ArrowRollFillTextures[i], HoldMirrored[i], GetColorForPlayer(player, selected, false));
	}

	public override (string, float) GetPlayerRollEndTextureRim(int lane, bool selected)
	{
		return GetPlayerArrowTextureRim(lane, selected);
	}

	public override (string, float, Color) GetPlayerRollEndTextureFill(int row, int lane, bool held, bool selected,
		int player)
	{
		var i = GetTextureIndex(lane);
		return (ArrowFillTextures[i], ArrowRotations[i], GetColorForPlayer(player, selected, false));
	}
}

internal class ArrowGraphicManagerPIUSingleOrDouble : ArrowGraphicManagerPIU
{
	public ArrowGraphicManagerPIUSingleOrDouble()
	{
		StartArrowIndex = 0;
	}
}

internal sealed class ArrowGraphicManagerPIUSingleHalfDouble : ArrowGraphicManagerPIU
{
	public ArrowGraphicManagerPIUSingleHalfDouble()
	{
		StartArrowIndex = 2;
	}
}

internal sealed class ArrowGraphicManagerPIURoutine : ArrowGraphicManagerPIUSingleOrDouble
{
	public override uint GetArrowColor(int row, int lane, bool selected, int player)
	{
		return GetUIColorForPlayer(player, selected);
	}

	public override uint GetHoldColor(int row, int lane, bool selected, int player)
	{
		return GetUIColorForPlayer(player, selected);
	}

	public override uint GetRollColor(int row, int lane, bool selected, int player)
	{
		return GetUIColorForPlayer(player, selected);
	}
}

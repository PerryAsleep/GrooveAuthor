using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using Fumen;
using static Fumen.Converters.SMCommon;
using static StepManiaEditor.Utils;

namespace StepManiaEditor;

/// <summary>
/// Class for managing access to textures and colors for arrows based on Chart type.
/// </summary>
internal abstract class ArrowGraphicManager
{
	/// <summary>
	/// Set of colors to use for an arrow in various contexts.
	/// </summary>
	protected struct ArrowColorSet
	{
		/// <summary>
		/// Brightness multiplier for the normal color.
		/// </summary>
		private const float ColorMultiplier = 1.5f;

		/// <summary>
		/// Brightness multiplier for the selected color.
		/// It is intention this is large and will result in whites for many colors.
		/// This is typically used in contexts where the colored area is small so differentiating
		/// between a selected and unselected note is more important than differentiating between
		/// individual note colors.
		/// </summary>
		private const float SelectedColorMultiplier = 8.0f;

		/// <summary>
		/// RGBA Color.
		/// </summary>
		public uint Color;

		/// <summary>
		/// RGBA Selected color.
		/// </summary>
		public uint SelectedColor;

		/// <summary>
		/// BGR565 Color.
		/// </summary>
		public ushort ColorBgr565;

		/// <summary>
		/// Constructor taking a base color from which to generate the color set.
		/// </summary>
		/// <param name="color">Base color.</param>
		public ArrowColorSet(uint color)
		{
			Color = ColorRGBAMultiply(color, ColorMultiplier);
			SelectedColor = ColorRGBAMultiply(color, SelectedColorMultiplier);
			ColorBgr565 = ToBGR565(Color);
		}

		[Pure]
		public uint GetColor(bool selected)
		{
			return selected ? SelectedColor : Color;
		}

		[Pure]
		public ushort GetColorBgr565()
		{
			return ColorBgr565;
		}
	}

	private static readonly ArrowColorSet MineColor;

	private static readonly string TextureIdMine = "mine";

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
			"icon-pump-down-left","icon-pump-up-left",
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
	};

	static ArrowGraphicManager()
	{
		MineColor = new ArrowColorSet(0xFFB7B7B7); // light grey
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
			case ChartType.dance_routine:
				return new ArrowGraphicManagerDanceSingleOrDouble();
			case ChartType.dance_solo:
				return new ArrowGraphicManagerDanceSolo();
			case ChartType.dance_threepanel:
				return new ArrowGraphicManagerDanceThreePanel();

			case ChartType.smx_beginner:
				return new ArrowGraphicManagerDanceSMXBeginner();
			case ChartType.smx_single:
			case ChartType.smx_full:
			case ChartType.smx_team:
				return new ArrowGraphicManagerDanceSMXSingleOrFull();
			case ChartType.smx_dual:
				return new ArrowGraphicManagerDanceSMXDual();

			// TODO
			case ChartType.pump_single:
			case ChartType.pump_double:
			case ChartType.pump_couple:
			case ChartType.pump_routine:
				return new ArrowGraphicManagerPIUSingleOrDouble();
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

	public abstract (string, float) GetArrowTexture(int integerPosition, int lane, bool selected);

	public abstract (string, bool) GetHoldStartTexture(int integerPosition, int lane, bool held, bool selected);
	public abstract (string, bool) GetHoldBodyTexture(int integerPosition, int lane, bool held, bool selected);
	public abstract (string, float) GetHoldEndTexture(int integerPosition, int lane, bool held, bool selected);
	public abstract (string, bool) GetRollStartTexture(int integerPosition, int lane, bool held, bool selected);
	public abstract (string, bool) GetRollBodyTexture(int integerPosition, int lane, bool held, bool selected);
	public abstract (string, float) GetRollEndTexture(int integerPosition, int lane, bool held, bool selected);

	public static uint GetArrowColorForSubdivision(int subdivision)
	{
		return ArrowGraphicManagerDance.GetDanceArrowColorForSubdivision(subdivision);
	}

	public abstract uint GetArrowColor(int integerPosition, int lane, bool selected);
	public abstract ushort GetArrowColorBGR565(int integerPosition, int lane, bool selected);
	public abstract uint GetHoldColor(int integerPosition, int lane, bool selected);
	public abstract ushort GetHoldColorBGR565(int integerPosition, int lane, bool selected);
	public abstract uint GetRollColor(int integerPosition, int lane, bool selected);
	public abstract ushort GetRollColorBGR565(int integerPosition, int lane, bool selected);

	public static uint GetMineColor(bool selected)
	{
		return MineColor.GetColor(selected);
	}

	public static ushort GetMineColorBGR565()
	{
		return MineColor.GetColorBgr565();
	}

	public static string GetMineTexture(int integerPosition, int lane, bool selected)
	{
		return GetTextureId(TextureIdMine, selected);
	}

	public static string GetSnapIndicatorTexture(int subdivision)
	{
		if (subdivision == 0)
			return null;
		if (!SnapTextureByBeatSubdivision.TryGetValue(subdivision, out var texture))
			texture = SnapTextureByBeatSubdivision[16];
		return texture;
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

	protected static readonly Dictionary<int, UniqueDanceTextures> ArrowTextureByBeatSubdivision;
	protected static readonly UniqueDanceTextures[] ArrowTextureByRow;
	protected static readonly HoldTextures HoldTexturesActive;
	protected static readonly HoldTextures HoldTexturesInactive;
	protected static readonly HoldTextures RollTexturesActive;
	protected static readonly HoldTextures RollTexturesInactive;

	protected static readonly UniqueDanceTextures ReceptorTextures;
	protected static readonly UniqueDanceTextures ReceptorGlowTextures;
	protected static readonly UniqueDanceTextures ReceptorHeldTextures;

	protected static readonly Dictionary<int, ArrowColorSet> ArrowColorBySubdivision;
	protected static readonly ArrowColorSet[] ArrowColorByRow;
	protected static readonly ArrowColorSet HoldColor;
	protected static readonly ArrowColorSet RollColor;

	static ArrowGraphicManagerDance()
	{
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

		ArrowColorBySubdivision = new Dictionary<int, ArrowColorSet>
		{
			{ 1, new ArrowColorSet(0xFF1818B6) }, // Red
			{ 2, new ArrowColorSet(0xFFB63518) }, // Blue
			{ 3, new ArrowColorSet(0xFF37AD36) }, // Green
			{ 4, new ArrowColorSet(0xFF16CAD1) }, // Yellow
			{ 6, new ArrowColorSet(0xFFB61884) }, // Purple
			{ 8, new ArrowColorSet(0xFF98B618) }, // Cyan
			{ 12, new ArrowColorSet(0xFF8018B6) }, // Pink
			{ 16, new ArrowColorSet(0xFF586F4F) }, // Pale Grey Green
			{ 48, new ArrowColorSet(0xFF586F4F) }, // Pale Grey Green
		};
		ArrowColorByRow = new ArrowColorSet[MaxValidDenominator];
		for (var i = 0; i < MaxValidDenominator; i++)
		{
			var key = new Fraction(i, MaxValidDenominator).Reduce().Denominator;

			if (!ArrowColorBySubdivision.ContainsKey(key))
				key = 16;
			ArrowColorByRow[i] = ArrowColorBySubdivision[key];
		}

		HoldColor = new ArrowColorSet(0xFF696969); // Grey
		RollColor = new ArrowColorSet(0xFF2264A6); // Orange
	}

	public static HashSet<string> GetAllTextures()
	{
		var allTextures = new HashSet<string>();

		foreach (var kvp in ArrowTextureByBeatSubdivision)
		{
			allTextures.Add(kvp.Value.DownArrow);
			allTextures.Add(kvp.Value.CenterArrow);
			allTextures.Add(kvp.Value.UpLeftArrow);
		}

		void AddTextures(UniqueDanceTextures t)
		{
			if (t.DownArrow != null)
				allTextures.Add(t.DownArrow);
			if (t.CenterArrow != null)
				allTextures.Add(t.CenterArrow);
			if (t.UpLeftArrow != null)
				allTextures.Add(t.UpLeftArrow);
		}

		void AddHoldTextures(HoldTextures h)
		{
			AddTextures(h.Start);
			AddTextures(h.Body);
			AddTextures(h.End);
		}

		AddHoldTextures(HoldTexturesActive);
		AddHoldTextures(HoldTexturesInactive);
		AddHoldTextures(RollTexturesActive);
		AddHoldTextures(RollTexturesInactive);
		AddTextures(ReceptorTextures);
		AddTextures(ReceptorGlowTextures);
		AddTextures(ReceptorHeldTextures);

		return allTextures;
	}

	public override bool AreHoldCapsCentered()
	{
		return false;
	}

	public override uint GetArrowColor(int integerPosition, int lane, bool selected)
	{
		return ArrowColorByRow[integerPosition % MaxValidDenominator].GetColor(selected);
	}

	public static uint GetDanceArrowColorForSubdivision(int subdivision)
	{
		return ArrowColorBySubdivision[subdivision].GetColor(false);
	}

	public override ushort GetArrowColorBGR565(int integerPosition, int lane, bool selected)
	{
		return ArrowColorByRow[integerPosition % MaxValidDenominator].GetColorBgr565();
	}

	public override uint GetHoldColor(int integerPosition, int lane, bool selected)
	{
		return HoldColor.GetColor(selected);
	}

	public override ushort GetHoldColorBGR565(int integerPosition, int lane, bool selected)
	{
		return HoldColor.GetColorBgr565();
	}

	public override uint GetRollColor(int integerPosition, int lane, bool selected)
	{
		return RollColor.GetColor(selected);
	}

	public override ushort GetRollColorBGR565(int integerPosition, int lane, bool selected)
	{
		return RollColor.GetColorBgr565();
	}
}

internal sealed class ArrowGraphicManagerDanceSingleOrDouble : ArrowGraphicManagerDance
{
	private static readonly float[] ArrowRotations =
	{
		(float)Math.PI * 0.5f, // L
		0.0f, // D
		(float)Math.PI, // U
		(float)Math.PI * 1.5f, // R
	};

	public override (string, float) GetArrowTexture(int integerPosition, int lane, bool selected)
	{
		return (GetTextureId(ArrowTextureByRow[integerPosition % MaxValidDenominator].DownArrow, selected),
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

	public override (string, bool) GetHoldBodyTexture(int integerPosition, int lane, bool held, bool selected)
	{
		return (GetTextureId(held ? HoldTexturesActive.Body.DownArrow : HoldTexturesInactive.Body.DownArrow, selected), false);
	}

	public override (string, float) GetHoldEndTexture(int integerPosition, int lane, bool held, bool selected)
	{
		return (GetTextureId(held ? HoldTexturesActive.End.DownArrow : HoldTexturesInactive.End.DownArrow, selected), 0.0f);
	}

	public override (string, bool) GetHoldStartTexture(int integerPosition, int lane, bool held, bool selected)
	{
		return (GetTextureId(held ? HoldTexturesActive.Start.DownArrow : HoldTexturesInactive.Start.DownArrow, selected), false);
	}

	public override (string, bool) GetRollBodyTexture(int integerPosition, int lane, bool held, bool selected)
	{
		return (GetTextureId(held ? RollTexturesActive.Body.DownArrow : RollTexturesInactive.Body.DownArrow, selected), false);
	}

	public override (string, float) GetRollEndTexture(int integerPosition, int lane, bool held, bool selected)
	{
		return (GetTextureId(held ? RollTexturesActive.End.DownArrow : RollTexturesInactive.End.DownArrow, selected), 0.0f);
	}

	public override (string, bool) GetRollStartTexture(int integerPosition, int lane, bool held, bool selected)
	{
		return (GetTextureId(held ? RollTexturesActive.Start.DownArrow : RollTexturesInactive.Start.DownArrow, selected), false);
	}
}

internal abstract class ArrowGraphicManagerDanceSoloBase : ArrowGraphicManagerDance
{
	protected abstract bool ShouldUseUpLeftArrow(int lane);
	protected abstract float GetRotation(int lane);

	public override (string, float) GetArrowTexture(int integerPosition, int lane, bool selected)
	{
		if (ShouldUseUpLeftArrow(lane))
		{
			return (GetTextureId(ArrowTextureByRow[integerPosition % MaxValidDenominator].UpLeftArrow, selected),
				GetRotation(lane));
		}

		return (GetTextureId(ArrowTextureByRow[integerPosition % MaxValidDenominator].DownArrow, selected),
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

	public override (string, bool) GetHoldBodyTexture(int integerPosition, int lane, bool held, bool selected)
	{
		// Always use the narrower diagonal hold graphics in solo.
		return (GetTextureId(held ? HoldTexturesActive.Body.UpLeftArrow : HoldTexturesInactive.Body.UpLeftArrow, selected),
			false);
	}

	public override (string, float) GetHoldEndTexture(int integerPosition, int lane, bool held, bool selected)
	{
		// Always use the narrower diagonal hold graphics in solo.
		return (GetTextureId(held ? HoldTexturesActive.End.UpLeftArrow : HoldTexturesInactive.End.UpLeftArrow, selected), 0.0f);
	}

	public override (string, bool) GetHoldStartTexture(int integerPosition, int lane, bool held, bool selected)
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

	public override (string, bool) GetRollBodyTexture(int integerPosition, int lane, bool held, bool selected)
	{
		// Always use the narrower diagonal hold graphics in solo.
		return (GetTextureId(held ? RollTexturesActive.Body.UpLeftArrow : RollTexturesInactive.Body.UpLeftArrow, selected),
			false);
	}

	public override (string, float) GetRollEndTexture(int integerPosition, int lane, bool held, bool selected)
	{
		// Always use the narrower diagonal hold graphics in solo.
		return (GetTextureId(held ? RollTexturesActive.End.UpLeftArrow : RollTexturesInactive.End.UpLeftArrow, selected), 0.0f);
	}

	public override (string, bool) GetRollStartTexture(int integerPosition, int lane, bool held, bool selected)
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

	public override (string, float) GetArrowTexture(int integerPosition, int lane, bool selected)
	{
		if (ShouldUseCenterArrow(lane))
		{
			return (GetTextureId(ArrowTextureByRow[integerPosition % MaxValidDenominator].CenterArrow, selected),
				GetRotation(lane));
		}

		return (GetTextureId(ArrowTextureByRow[integerPosition % MaxValidDenominator].DownArrow, selected),
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

	public override (string, bool) GetHoldBodyTexture(int integerPosition, int lane, bool held, bool selected)
	{
		return (GetTextureId(held ? HoldTexturesActive.Body.CenterArrow : HoldTexturesInactive.Body.CenterArrow, selected),
			false);
	}

	public override (string, float) GetHoldEndTexture(int integerPosition, int lane, bool held, bool selected)
	{
		return (GetTextureId(held ? HoldTexturesActive.End.CenterArrow : HoldTexturesInactive.End.CenterArrow, selected), 0.0f);
	}

	public override (string, bool) GetHoldStartTexture(int integerPosition, int lane, bool held, bool selected)
	{
		return (GetTextureId(held ? HoldTexturesActive.Start.CenterArrow : HoldTexturesInactive.Start.CenterArrow, selected),
			false);
	}

	public override (string, bool) GetRollBodyTexture(int integerPosition, int lane, bool held, bool selected)
	{
		return (GetTextureId(held ? RollTexturesActive.Body.CenterArrow : RollTexturesInactive.Body.CenterArrow, selected),
			false);
	}

	public override (string, float) GetRollEndTexture(int integerPosition, int lane, bool held, bool selected)
	{
		return (GetTextureId(held ? RollTexturesActive.End.CenterArrow : RollTexturesInactive.End.CenterArrow, selected), 0.0f);
	}

	public override (string, bool) GetRollStartTexture(int integerPosition, int lane, bool held, bool selected)
	{
		return (GetTextureId(held ? RollTexturesActive.Start.CenterArrow : RollTexturesInactive.Start.CenterArrow, selected),
			false);
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

internal sealed class ArrowGraphicManagerDanceSMXSingleOrFull : ArrowGraphicManagerDanceSMX
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

internal abstract class ArrowGraphicManagerPIU : ArrowGraphicManager
{
	protected static readonly ArrowColorSet ArrowColorRed;
	protected static readonly ArrowColorSet ArrowColorBlue;
	protected static readonly ArrowColorSet ArrowColorYellow;

	protected static readonly ArrowColorSet HoldColorRed;
	protected static readonly ArrowColorSet HoldColorBlue;
	protected static readonly ArrowColorSet HoldColorYellow;

	protected static readonly ArrowColorSet RollColorRed;
	protected static readonly ArrowColorSet RollColorBlue;
	protected static readonly ArrowColorSet RollColorYellow;

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

	protected static readonly ArrowColorSet[] ArrowColors;
	protected static readonly ArrowColorSet[] HoldColors;
	protected static readonly ArrowColorSet[] RollColors;

	protected int StartArrowIndex;

	static ArrowGraphicManagerPIU()
	{
		ArrowColorRed = new ArrowColorSet(0xFF371BB3);
		ArrowColorBlue = new ArrowColorSet(0xFFB3401B);
		ArrowColorYellow = new ArrowColorSet(0xFF00EAFF);

		HoldColorRed = new ArrowColorSet(0xFF5039B2);
		HoldColorBlue = new ArrowColorSet(0xFFB35639);
		HoldColorYellow = new ArrowColorSet(0xFF6BF3FF);

		RollColorRed = new ArrowColorSet(0xFF6B54F8);
		RollColorBlue = new ArrowColorSet(0xFFB38C1B);
		RollColorYellow = new ArrowColorSet(0xFF2FABB5);

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

	public static HashSet<string> GetAllTextures()
	{
		var allTextures = new HashSet<string>();

		void AddTextures(string[] textures)
		{
			foreach (var t in textures)
				allTextures.Add(t);
		}

		AddTextures(ReceptorTextures);
		AddTextures(ReceptorGlowTextures);
		AddTextures(ReceptorHeldTextures);
		AddTextures(ArrowTextures);
		AddTextures(HoldTextures);
		AddTextures(RollTextures);

		return allTextures;
	}

	protected int GetTextureIndex(int lane)
	{
		return (lane + StartArrowIndex) % 5;
	}

	public override bool AreHoldCapsCentered()
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

	public override (string, float) GetArrowTexture(int integerPosition, int lane, bool selected)
	{
		var i = GetTextureIndex(lane);
		return (GetTextureId(ArrowTextures[i], selected), ArrowRotationsColored[i]);
	}

	public override (string, bool) GetHoldStartTexture(int integerPosition, int lane, bool held, bool selected)
	{
		return (null, false);
	}

	public override (string, bool) GetHoldBodyTexture(int integerPosition, int lane, bool held, bool selected)
	{
		var i = GetTextureIndex(lane);
		return (GetTextureId(HoldTextures[i], selected), HoldMirrored[i]);
	}

	public override (string, float) GetHoldEndTexture(int integerPosition, int lane, bool held, bool selected)
	{
		return GetArrowTexture(integerPosition, lane, selected);
	}

	public override (string, bool) GetRollStartTexture(int integerPosition, int lane, bool held, bool selected)
	{
		return (null, false);
	}

	public override (string, bool) GetRollBodyTexture(int integerPosition, int lane, bool held, bool selected)
	{
		var i = GetTextureIndex(lane);
		return (GetTextureId(RollTextures[i], selected), HoldMirrored[i]);
	}

	public override (string, float) GetRollEndTexture(int integerPosition, int lane, bool held, bool selected)
	{
		return GetArrowTexture(integerPosition, lane, selected);
	}

	public override uint GetArrowColor(int integerPosition, int lane, bool selected)
	{
		var i = GetTextureIndex(lane);
		return ArrowColors[i].GetColor(selected);
	}

	public override ushort GetArrowColorBGR565(int integerPosition, int lane, bool selected)
	{
		var i = GetTextureIndex(lane);
		return ArrowColors[i].GetColorBgr565();
	}

	public override uint GetHoldColor(int integerPosition, int lane, bool selected)
	{
		var i = GetTextureIndex(lane);
		return HoldColors[i].GetColor(selected);
	}

	public override ushort GetHoldColorBGR565(int integerPosition, int lane, bool selected)
	{
		var i = GetTextureIndex(lane);
		return HoldColors[i].GetColorBgr565();
	}

	public override uint GetRollColor(int integerPosition, int lane, bool selected)
	{
		var i = GetTextureIndex(lane);
		return RollColors[i].GetColor(selected);
	}

	public override ushort GetRollColorBGR565(int integerPosition, int lane, bool selected)
	{
		var i = GetTextureIndex(lane);
		return RollColors[i].GetColorBgr565();
	}
}

internal sealed class ArrowGraphicManagerPIUSingleOrDouble : ArrowGraphicManagerPIU
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

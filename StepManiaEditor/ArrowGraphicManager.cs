using System;
using System.Collections.Generic;
using Fumen;
using Fumen.Converters;
using static StepManiaEditor.Utils;

namespace StepManiaEditor
{
	/// <summary>
	/// Class for managing access to textures and colors for arrows based on Chart type.
	/// </summary>
	public abstract class ArrowGraphicManager
	{
		protected const float ColorMultiplier = 1.5f;

		private static readonly uint MineColorABGR;
		private static readonly ushort MineColorBGR565;

		private static readonly string TextureIdMine = "mine";

		static ArrowGraphicManager()
		{
			MineColorABGR = ColorABGRMultiply(0xFFB7B7B7, ColorMultiplier); // light grey
			MineColorBGR565 = ToBGR565(MineColorABGR);
		}

		/// <summary>
		/// Gets all texture ids which could possible be used by any ArrowGraphicManager.
		/// </summary>
		/// <returns></returns>
		public static HashSet<string> GetAllTextureIds()
		{
			var allTextures = new HashSet<string>();

			// Common textures.
			allTextures.Add(TextureIdMine);

			// ITG / SMX textures.
			var danceTextures = ArrowGraphicManagerDance.GetAllTextures();
			foreach (var danceTexture in danceTextures)
				allTextures.Add(danceTexture);

			// Pump textures.
			var pumpTextures = ArrowGraphicManagerPIU.GetAllTextures();
			foreach (var pumpTexture in pumpTextures)
				allTextures.Add(pumpTexture);

			return allTextures;
		}

		/// <summary>
		/// Factory method for creating a new ArrowGraphicManager appropriate for the given ChartType.
		/// </summary>
		public static ArrowGraphicManager CreateArrowGraphicManager(SMCommon.ChartType chartType)
		{
			switch (chartType)
			{
				case SMCommon.ChartType.dance_single:
				case SMCommon.ChartType.dance_double:
				case SMCommon.ChartType.dance_couple:
				case SMCommon.ChartType.dance_routine:
					return new ArrowGraphicManagerDanceSingleOrDouble();
				case SMCommon.ChartType.dance_solo:
					return new ArrowGraphicManagerDanceSolo();
				case SMCommon.ChartType.dance_threepanel:
					return new ArrowGraphicManagerDanceThreePanel();

				case SMCommon.ChartType.smx_beginner:
					return new ArrowGraphicManagerDanceSMXBeginner();
				case SMCommon.ChartType.smx_single:
				case SMCommon.ChartType.smx_full:
				case SMCommon.ChartType.smx_team:
					return new ArrowGraphicManagerDanceSMXSingleOrFull();
				case SMCommon.ChartType.smx_dual:
					return new ArrowGraphicManagerDanceSMXDual();

				// TODO
				case SMCommon.ChartType.pump_single:
				case SMCommon.ChartType.pump_halfdouble:
				case SMCommon.ChartType.pump_double:
				case SMCommon.ChartType.pump_couple:
				case SMCommon.ChartType.pump_routine:
					break;

				default:
					return null;
			}

			return null;
		}

		public abstract (string, float) GetReceptorTexture(int lane);
		public abstract (string, float) GetReceptorGlowTexture(int lane);
		public abstract (string, float) GetReceptorHeldTexture(int lane);

		public abstract (string, float) GetArrowTexture(int integerPosition, int lane);

		public abstract (string, bool) GetHoldStartTexture(int integerPosition, int lane, bool held);
		public abstract (string, bool) GetHoldBodyTexture(int integerPosition, int lane, bool held);
		public abstract (string, bool) GetHoldEndTexture(int integerPosition, int lane, bool held);
		public abstract (string, bool) GetRollStartTexture(int integerPosition, int lane, bool held);
		public abstract (string, bool) GetRollBodyTexture(int integerPosition, int lane, bool held);
		public abstract (string, bool) GetRollEndTexture(int integerPosition, int lane, bool held);


		public abstract uint GetArrowColorABGR(int integerPosition, int lane);
		public abstract uint GetArrowColorABGRForSubdivision(int subdivision);
		public abstract ushort GetArrowColorBGR565(int integerPosition, int lane);
		public abstract uint GetHoldColorABGR(int integerPosition, int lane);
		public abstract ushort GetHoldColorBGR565(int integerPosition, int lane);
		public abstract uint GetRollColorABGR(int integerPosition, int lane);
		public abstract ushort GetRollColorBGR565(int integerPosition, int lane);

		public uint GetMineColorABGR()
		{
			return MineColorABGR;
		}

		public ushort GetMineColorBGR565()
		{
			return MineColorBGR565;
		}

		public string GetMineTexture(int integerPosition, int lane)
		{
			return TextureIdMine;
		}
	}

	public abstract class ArrowGraphicManagerDance : ArrowGraphicManager
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

		protected static readonly Dictionary<int, uint> ArrowColorABGRBySubdivision;
		protected static readonly uint[] ArrowColorABGRByRow;
		protected static readonly ushort[] ArrowColorBGR565ByRow;
		protected static readonly uint HoldColorABGR;
		protected static readonly ushort HoldColorBGR565;
		protected static readonly uint RollColorABGR;
		protected static readonly ushort RollColorBGR565;

		static ArrowGraphicManagerDance()
		{
			ArrowTextureByBeatSubdivision = new Dictionary<int, UniqueDanceTextures>
			{
				{1, new UniqueDanceTextures { DownArrow = "itg-down-1-4", UpLeftArrow = "itg-solo-1-4", CenterArrow = "itg-center-1-4" }},
				{2, new UniqueDanceTextures { DownArrow = "itg-down-1-8", UpLeftArrow = "itg-solo-1-8", CenterArrow = "itg-center-1-8" }},
				{3, new UniqueDanceTextures { DownArrow = "itg-down-1-12", UpLeftArrow = "itg-solo-1-12", CenterArrow = "itg-center-1-12" }},
				{4, new UniqueDanceTextures { DownArrow = "itg-down-1-16", UpLeftArrow = "itg-solo-1-16", CenterArrow = "itg-center-1-16" }},
				{6, new UniqueDanceTextures { DownArrow = "itg-down-1-24", UpLeftArrow = "itg-solo-1-24", CenterArrow = "itg-center-1-24" }},
				{8, new UniqueDanceTextures { DownArrow = "itg-down-1-32", UpLeftArrow = "itg-solo-1-32", CenterArrow = "itg-center-1-32" }},
				{12, new UniqueDanceTextures { DownArrow = "itg-down-1-48", UpLeftArrow = "itg-solo-1-48", CenterArrow = "itg-center-1-48" }},
				{16, new UniqueDanceTextures { DownArrow = "itg-down-1-64", UpLeftArrow = "itg-solo-1-64", CenterArrow = "itg-center-1-64" }},
			};

			ArrowTextureByRow = new UniqueDanceTextures[SMCommon.MaxValidDenominator];
			for (var i = 0; i < SMCommon.MaxValidDenominator; i++)
			{
				var key = new Fraction(i, SMCommon.MaxValidDenominator).Reduce().Denominator;
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
					UpLeftArrow = "itg-hold-solo-start-active"
				},
				Body = new UniqueDanceTextures
				{
					DownArrow = "itg-hold-body-active",
					CenterArrow = "itg-hold-center-body-active",
					UpLeftArrow = "itg-hold-solo-body-active"
				},
				End = new UniqueDanceTextures
				{
					DownArrow = "itg-hold-end-active",
					CenterArrow = "itg-hold-center-end-active",
					UpLeftArrow = "itg-hold-solo-end-active"
				}
			};
			HoldTexturesInactive = new HoldTextures
			{
				Start = new UniqueDanceTextures
				{
					DownArrow = null,
					CenterArrow = null,
					UpLeftArrow = "itg-hold-solo-start-inactive"
				},
				Body = new UniqueDanceTextures
				{
					DownArrow = "itg-hold-body-inactive",
					CenterArrow = "itg-hold-center-body-inactive",
					UpLeftArrow = "itg-hold-solo-body-inactive"
				},
				End = new UniqueDanceTextures
				{
					DownArrow = "itg-hold-end-inactive",
					CenterArrow = "itg-hold-center-end-inactive",
					UpLeftArrow = "itg-hold-solo-end-inactive"
				}
			};

			RollTexturesActive = new HoldTextures
			{
				Start = new UniqueDanceTextures
				{
					DownArrow = null,
					CenterArrow = null,
					UpLeftArrow = "itg-roll-solo-start-active"
				},
				Body = new UniqueDanceTextures
				{
					DownArrow = "itg-roll-body-active",
					CenterArrow = "itg-roll-center-body-active",
					UpLeftArrow = "itg-roll-solo-body-active"
				},
				End = new UniqueDanceTextures
				{
					DownArrow = "itg-roll-end-active",
					CenterArrow = "itg-roll-center-end-active",
					UpLeftArrow = "itg-roll-solo-end-active"
				}
			};
			RollTexturesInactive = new HoldTextures
			{
				Start = new UniqueDanceTextures
				{
					DownArrow = null,
					CenterArrow = null,
					UpLeftArrow = "itg-roll-solo-start-inactive"
				},
				Body = new UniqueDanceTextures
				{
					DownArrow = "itg-roll-body-inactive",
					CenterArrow = "itg-roll-center-body-inactive",
					UpLeftArrow = "itg-roll-solo-body-inactive"
				},
				End = new UniqueDanceTextures
				{
					DownArrow = "itg-roll-end-inactive",
					CenterArrow = "itg-roll-center-end-inactive",
					UpLeftArrow = "itg-roll-solo-end-inactive"
				}
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

			ArrowColorABGRBySubdivision = new Dictionary<int, uint>
			{
				{ 1, ColorABGRMultiply(0xFF1818B6, ColorMultiplier) }, // Red
				{ 2, ColorABGRMultiply(0xFFB63518, ColorMultiplier) }, // Blue
				{ 3, ColorABGRMultiply(0xFF37AD36, ColorMultiplier) }, // Green
				{ 4, ColorABGRMultiply(0xFF16CAD1, ColorMultiplier) }, // Yellow
				{ 6, ColorABGRMultiply(0xFFB61884, ColorMultiplier) }, // Purple
				{ 8, ColorABGRMultiply(0xFF98B618, ColorMultiplier) }, // Cyan
				{ 12, ColorABGRMultiply(0xFF8018B6, ColorMultiplier) }, // Pink
				{ 16, ColorABGRMultiply(0xFF586F4F, ColorMultiplier) }, // Pale Grey Green
				{ 48, ColorABGRMultiply(0xFF586F4F, ColorMultiplier) }, // Pale Grey Green
			};
			ArrowColorABGRByRow = new uint[SMCommon.MaxValidDenominator];
			ArrowColorBGR565ByRow = new ushort[SMCommon.MaxValidDenominator];
			for (var i = 0; i < SMCommon.MaxValidDenominator; i++)
			{
				var key = new Fraction(i, SMCommon.MaxValidDenominator).Reduce().Denominator;

				if (!ArrowColorABGRBySubdivision.ContainsKey(key))
					key = 16;
				ArrowColorABGRByRow[i] = ArrowColorABGRBySubdivision[key];
				ArrowColorBGR565ByRow[i] = ToBGR565(ArrowColorABGRByRow[i]);
			}
			
			HoldColorABGR = ColorABGRMultiply(0xFF696969, ColorMultiplier); // Grey
			HoldColorBGR565 = ToBGR565(HoldColorABGR);
			RollColorABGR = ColorABGRMultiply(0xFF2264A6, ColorMultiplier); // Orange
			RollColorBGR565 = ToBGR565(RollColorABGR);
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

		public override uint GetArrowColorABGR(int integerPosition, int lane)
		{
			return ArrowColorABGRByRow[integerPosition % SMCommon.MaxValidDenominator];
		}

		public override uint GetArrowColorABGRForSubdivision(int subdivision)
		{
			return ArrowColorABGRBySubdivision[subdivision];
		}

		public override ushort GetArrowColorBGR565(int integerPosition, int lane)
		{
			return ArrowColorBGR565ByRow[integerPosition % SMCommon.MaxValidDenominator];
		}
		
		public override uint GetHoldColorABGR(int integerPosition, int lane)
		{
			return HoldColorABGR;
		}

		public override ushort GetHoldColorBGR565(int integerPosition, int lane)
		{
			return HoldColorBGR565;
		}

		public override uint GetRollColorABGR(int integerPosition, int lane)
		{
			return RollColorABGR;
		}

		public override ushort GetRollColorBGR565(int integerPosition, int lane)
		{
			return RollColorBGR565;
		}
	}

	public class ArrowGraphicManagerDanceSingleOrDouble : ArrowGraphicManagerDance
	{
		private static readonly float[] ArrowRotations =
		{
			(float)Math.PI * 0.5f,	// L
			0.0f,					// D
			(float)Math.PI,			// U
			(float)Math.PI * 1.5f,	// R
		};

		public override (string, float) GetArrowTexture(int integerPosition, int lane)
		{
			return (ArrowTextureByRow[integerPosition % SMCommon.MaxValidDenominator].DownArrow, ArrowRotations[lane % 4]);
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

		public override (string, bool) GetHoldBodyTexture(int integerPosition, int lane, bool held)
		{
			return (held ? HoldTexturesActive.Body.DownArrow : HoldTexturesInactive.Body.DownArrow, false);
		}

		public override (string, bool) GetHoldEndTexture(int integerPosition, int lane, bool held)
		{
			return (held ? HoldTexturesActive.End.DownArrow : HoldTexturesInactive.End.DownArrow, false);
		}

		public override (string, bool) GetHoldStartTexture(int integerPosition, int lane, bool held)
		{
			return (held ? HoldTexturesActive.Start.DownArrow : HoldTexturesInactive.Start.DownArrow, false);
		}

		public override (string, bool) GetRollBodyTexture(int integerPosition, int lane, bool held)
		{
			return (held ? RollTexturesActive.Body.DownArrow : RollTexturesInactive.Body.DownArrow, false);
		}

		public override (string, bool) GetRollEndTexture(int integerPosition, int lane, bool held)
		{
			return (held ? RollTexturesActive.End.DownArrow : RollTexturesInactive.End.DownArrow, false);
		}

		public override (string, bool) GetRollStartTexture(int integerPosition, int lane, bool held)
		{
			return (held ? RollTexturesActive.Start.DownArrow : RollTexturesInactive.Start.DownArrow, false);
		}
	}

	public abstract class ArrowGraphicManagerDanceSoloBase : ArrowGraphicManagerDance
	{
		protected abstract bool ShouldUseUpLeftArrow(int lane);
		protected abstract float GetRotation(int lane);

		public override (string, float) GetArrowTexture(int integerPosition, int lane)
		{
			if (ShouldUseUpLeftArrow(lane))
			{
				return (ArrowTextureByRow[integerPosition % SMCommon.MaxValidDenominator].UpLeftArrow, GetRotation(lane));
			}
			return (ArrowTextureByRow[integerPosition % SMCommon.MaxValidDenominator].DownArrow, GetRotation(lane));
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

		public override (string, bool) GetHoldBodyTexture(int integerPosition, int lane, bool held)
		{
			// Always use the narrower diagonal hold graphics in solo.
			return (held ? HoldTexturesActive.Body.UpLeftArrow : HoldTexturesInactive.Body.UpLeftArrow, false);
		}

		public override (string, bool) GetHoldEndTexture(int integerPosition, int lane, bool held)
		{
			// Always use the narrower diagonal hold graphics in solo.
			return (held ? HoldTexturesActive.End.UpLeftArrow : HoldTexturesInactive.End.UpLeftArrow, false);
		}

		public override (string, bool) GetHoldStartTexture(int integerPosition, int lane, bool held)
		{
			// Always use the narrower diagonal hold graphics in solo.
			// But only use the start graphic for diagonal arrows since they have a gap needing to be filled.
			if (ShouldUseUpLeftArrow(lane))
			{
				return (held ? HoldTexturesActive.Start.UpLeftArrow : HoldTexturesInactive.Start.UpLeftArrow, false);
			}
			return (held ? HoldTexturesActive.Start.DownArrow : HoldTexturesInactive.Start.DownArrow, false);
		}

		public override (string, bool) GetRollBodyTexture(int integerPosition, int lane, bool held)
		{
			// Always use the narrower diagonal hold graphics in solo.
			return (held ? RollTexturesActive.Body.UpLeftArrow : RollTexturesInactive.Body.UpLeftArrow, false);
		}

		public override (string, bool) GetRollEndTexture(int integerPosition, int lane, bool held)
		{
			// Always use the narrower diagonal hold graphics in solo.
			return (held ? RollTexturesActive.End.UpLeftArrow : RollTexturesInactive.End.UpLeftArrow, false);
		}

		public override (string, bool) GetRollStartTexture(int integerPosition, int lane, bool held)
		{
			// Always use the narrower diagonal hold graphics in solo.
			// But only use the start graphic for diagonal arrows since they have a gap needing to be filled.
			if (ShouldUseUpLeftArrow(lane))
			{
				return (held ? RollTexturesActive.Start.UpLeftArrow : RollTexturesInactive.Start.UpLeftArrow, false);
			}
			return (held ? RollTexturesActive.Start.DownArrow : RollTexturesInactive.Start.DownArrow, false);
		}
	}

	public class ArrowGraphicManagerDanceSolo : ArrowGraphicManagerDanceSoloBase
	{
		private static readonly float[] ArrowRotations =
		{
			(float)Math.PI * 0.5f,	// L
			0.0f,					// UL
			0.0f,					// D
			(float)Math.PI,			// U
			(float)Math.PI * 0.5f,	// UR
			(float)Math.PI * 1.5f,	// R
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

	public class ArrowGraphicManagerDanceThreePanel : ArrowGraphicManagerDanceSoloBase
	{
		private static readonly float[] ArrowRotations =
		{
			0.0f,					// UL
			0.0f,					// D
			(float)Math.PI * 0.5f,	// UR
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

	public abstract class ArrowGraphicManagerDanceSMX : ArrowGraphicManagerDance
	{
		protected abstract bool ShouldUseCenterArrow(int lane);
		protected abstract float GetRotation(int lane);

		public override (string, float) GetArrowTexture(int integerPosition, int lane)
		{
			if (ShouldUseCenterArrow(lane))
			{
				return (ArrowTextureByRow[integerPosition % SMCommon.MaxValidDenominator].CenterArrow, GetRotation(lane));
			}
			return (ArrowTextureByRow[integerPosition % SMCommon.MaxValidDenominator].DownArrow, GetRotation(lane));
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

		public override (string, bool) GetHoldBodyTexture(int integerPosition, int lane, bool held)
		{
			return (held ? HoldTexturesActive.Body.CenterArrow : HoldTexturesInactive.Body.CenterArrow, false);
		}

		public override (string, bool) GetHoldEndTexture(int integerPosition, int lane, bool held)
		{
			return (held ? HoldTexturesActive.End.CenterArrow : HoldTexturesInactive.End.CenterArrow, false);
		}

		public override (string, bool) GetHoldStartTexture(int integerPosition, int lane, bool held)
		{
			return (held ? HoldTexturesActive.Start.CenterArrow : HoldTexturesInactive.Start.CenterArrow, false);
		}

		public override (string, bool) GetRollBodyTexture(int integerPosition, int lane, bool held)
		{
			return (held ? RollTexturesActive.Body.CenterArrow : RollTexturesInactive.Body.CenterArrow, false);
		}

		public override (string, bool) GetRollEndTexture(int integerPosition, int lane, bool held)
		{
			return (held ? RollTexturesActive.End.CenterArrow : RollTexturesInactive.End.CenterArrow, false);
		}

		public override (string, bool) GetRollStartTexture(int integerPosition, int lane, bool held)
		{
			return (held ? RollTexturesActive.Start.CenterArrow : RollTexturesInactive.Start.CenterArrow, false);
		}
	}

	public class ArrowGraphicManagerDanceSMXBeginner : ArrowGraphicManagerDanceSMX
	{
		private static readonly float[] ArrowRotations =
		{
			(float) Math.PI * 0.5f, // L
			0.0f,					// Center
			(float) Math.PI* 1.5f,	// R
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

	public class ArrowGraphicManagerDanceSMXSingleOrFull : ArrowGraphicManagerDanceSMX
	{
		private static readonly float[] ArrowRotations =
		{
			(float)Math.PI * 0.5f,	// L
			0.0f,					// D
			0.0f,					// Center
			(float)Math.PI,			// U
			(float)Math.PI * 1.5f,	// R
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

	public class ArrowGraphicManagerDanceSMXDual : ArrowGraphicManagerDanceSMX
	{
		private static readonly float[] ArrowRotations =
		{
			(float)Math.PI * 0.5f,	// L
			0.0f,					// Center
			(float)Math.PI * 1.5f,	// R
			(float)Math.PI * 0.5f,	// L
			0.0f,					// Center
			(float)Math.PI * 1.5f,	// R
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

	public abstract class ArrowGraphicManagerPIU : ArrowGraphicManager
	{
		protected struct ShapeTextures
		{
			public string Diagonal;
			public string Center;
		}

		protected struct ColorTextures
		{
			public string Red;
			public string Yellow;
			public string Blue;
		}

		protected static readonly uint ArrowColorRedABGR;
		protected static readonly ushort ArrowColorRedBGR565;
		protected static readonly uint ArrowColorBlueABGR;
		protected static readonly ushort ArrowColorBlueBGR565;
		protected static readonly uint ArrowColorYellowABGR;
		protected static readonly ushort ArrowColorYellowBGR565;

		protected static readonly uint HoldColorRedABGR;
		protected static readonly ushort HoldColorRedBGR565;
		protected static readonly uint HoldColorBlueABGR;
		protected static readonly ushort HoldColorBlueBGR565;
		protected static readonly uint HoldColorYellowABGR;
		protected static readonly ushort HoldColorYellowBGR565;

		protected static readonly uint RollColorRedABGR;
		protected static readonly ushort RollColorRedBGR565;
		protected static readonly uint RollColorBlueABGR;
		protected static readonly ushort RollColorBlueBGR565;
		protected static readonly uint RollColorYellowABGR;
		protected static readonly ushort RollColorYellowBGR565;

		protected static readonly ShapeTextures ReceptorTextures;
		protected static readonly ShapeTextures ReceptorGlowTextures;
		protected static readonly ShapeTextures ReceptorHeldTextures;

		protected static readonly ColorTextures ArrowTextures;
		protected static readonly ColorTextures HoldTextures;
		protected static readonly ColorTextures RollTextures;

		static ArrowGraphicManagerPIU()
		{
			ReceptorTextures = new ShapeTextures
			{
				Center = "piu-center-receptor",
				Diagonal = "piu-diagonal-receptor"
			};
			ReceptorGlowTextures = new ShapeTextures
			{
				Center = "piu-center-receptor-glow",
				Diagonal = "piu-diagonal-receptor-glow"
			};
			ReceptorHeldTextures = new ShapeTextures
			{
				Center = "piu-center-receptor-held",
				Diagonal = "piu-diagonal-receptor-held"
			};

			ArrowTextures = new ColorTextures
			{
				Red = "piu-diagonal-red",
				Yellow = "piu-center",
				Blue = "piu-diagonal-blue",
			};
			HoldTextures = new ColorTextures
			{
				Red = "piu-hold-red",
				Yellow = "piu-hold-center",
				Blue = "piu-hold-blue",
			};
			RollTextures = new ColorTextures
			{
				Red = "piu-roll-red",
				Yellow = "piu-roll-center",
				Blue = "piu-roll-blue",
			};

			ArrowColorRedABGR = ColorABGRMultiply(0xFF371BB3, ColorMultiplier);
			ArrowColorRedBGR565 = ToBGR565(ArrowColorRedABGR);
			ArrowColorBlueABGR = ColorABGRMultiply(0xFFB3401B, ColorMultiplier);
			ArrowColorBlueBGR565 = ToBGR565(ArrowColorBlueABGR);
			ArrowColorYellowABGR = ColorABGRMultiply(0xFF00EAFF, ColorMultiplier);
			ArrowColorYellowBGR565 = ToBGR565(ArrowColorYellowABGR);

			HoldColorRedABGR = ColorABGRMultiply(0xFF5039B2, ColorMultiplier);
			HoldColorRedBGR565 = ToBGR565(HoldColorRedABGR);
			HoldColorBlueABGR = ColorABGRMultiply(0xFFB35639, ColorMultiplier);
			HoldColorBlueBGR565 = ToBGR565(HoldColorBlueABGR);
			HoldColorYellowABGR = ColorABGRMultiply(0xFF6BF3FF, ColorMultiplier);
			HoldColorYellowBGR565 = ToBGR565(HoldColorYellowABGR);

			RollColorRedABGR = ColorABGRMultiply(0xFF6B54F8, ColorMultiplier);
			RollColorRedBGR565 = ToBGR565(RollColorRedABGR);
			RollColorBlueABGR = ColorABGRMultiply(0xFFB38C1B, ColorMultiplier);
			RollColorBlueBGR565 = ToBGR565(RollColorBlueABGR);
			RollColorYellowABGR = ColorABGRMultiply(0xFF2FABB5, ColorMultiplier);
			RollColorYellowBGR565 = ToBGR565(RollColorYellowABGR);
		}

		public static HashSet<string> GetAllTextures()
		{
			var allTextures = new HashSet<string>();

			void AddShapeTextures(ShapeTextures t)
			{
				if (t.Center != null)
					allTextures.Add(t.Center);
				if (t.Diagonal != null)
					allTextures.Add(t.Diagonal);
			}
			void AddColorTextures(ColorTextures t)
			{
				if (t.Red != null)
					allTextures.Add(t.Red);
				if (t.Yellow != null)
					allTextures.Add(t.Yellow);
				if (t.Blue != null)
					allTextures.Add(t.Blue);
			}

			AddShapeTextures(ReceptorTextures);
			AddShapeTextures(ReceptorGlowTextures);
			AddShapeTextures(ReceptorHeldTextures);
			AddColorTextures(ArrowTextures);
			AddColorTextures(HoldTextures);
			AddColorTextures(RollTextures);

			return allTextures;
		}
	}

	public class ArrowGraphicManagerPIUSingle : ArrowGraphicManagerPIU
	{
		public override (string, float) GetReceptorTexture(int lane)
		{
			throw new NotImplementedException();
		}

		public override (string, float) GetReceptorGlowTexture(int lane)
		{
			throw new NotImplementedException();
		}

		public override (string, float) GetReceptorHeldTexture(int lane)
		{
			throw new NotImplementedException();
		}

		public override (string, float) GetArrowTexture(int integerPosition, int lane)
		{
			throw new NotImplementedException();
		}

		public override (string, bool) GetHoldStartTexture(int integerPosition, int lane, bool held)
		{
			throw new NotImplementedException();
		}

		public override (string, bool) GetHoldBodyTexture(int integerPosition, int lane, bool held)
		{
			throw new NotImplementedException();
		}

		public override (string, bool) GetHoldEndTexture(int integerPosition, int lane, bool held)
		{
			throw new NotImplementedException();
		}

		public override (string, bool) GetRollStartTexture(int integerPosition, int lane, bool held)
		{
			throw new NotImplementedException();
		}

		public override (string, bool) GetRollBodyTexture(int integerPosition, int lane, bool held)
		{
			throw new NotImplementedException();
		}

		public override (string, bool) GetRollEndTexture(int integerPosition, int lane, bool held)
		{
			throw new NotImplementedException();
		}

		public override uint GetArrowColorABGR(int integerPosition, int lane)
		{
			throw new NotImplementedException();
		}

		public override uint GetArrowColorABGRForSubdivision(int subdivision)
		{
			throw new NotImplementedException();
		}

		public override ushort GetArrowColorBGR565(int integerPosition, int lane)
		{
			throw new NotImplementedException();
		}

		public override uint GetHoldColorABGR(int integerPosition, int lane)
		{
			throw new NotImplementedException();
		}

		public override ushort GetHoldColorBGR565(int integerPosition, int lane)
		{
			throw new NotImplementedException();
		}

		public override uint GetRollColorABGR(int integerPosition, int lane)
		{
			throw new NotImplementedException();
		}

		public override ushort GetRollColorBGR565(int integerPosition, int lane)
		{
			throw new NotImplementedException();
		}
	}
}

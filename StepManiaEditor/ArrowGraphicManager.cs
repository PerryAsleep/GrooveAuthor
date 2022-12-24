using System;
using System.Collections.Generic;
using Fumen;
using Fumen.Converters;
using Microsoft.Xna.Framework.Graphics;
using static StepManiaEditor.Utils;

namespace StepManiaEditor
{
	/// <summary>
	/// Class for managing access to textures and colors for arrows based on Chart type.
	/// </summary>
	internal abstract class ArrowGraphicManager
	{
		protected const float ColorMultiplier = 1.5f;
		protected const float SelectedColorMultiplier = 3.0f;

		private static readonly uint MineColorRGBA;
		private static readonly ushort MineColorBGR565;

		private static readonly string TextureIdMine = "mine";

		protected static readonly Dictionary<int, string> SnapTextureByBeatSubdivision;

		static ArrowGraphicManager()
		{
			MineColorRGBA = ColorRGBAMultiply(0xFFB7B7B7, ColorMultiplier); // light grey
			MineColorBGR565 = ToBGR565(MineColorRGBA);

			SnapTextureByBeatSubdivision = new Dictionary<int, string>
			{
				{1, "snap-1-4"},
				{2, "snap-1-8"},
				{3, "snap-1-12"},
				{4, "snap-1-16"},
				{6, "snap-1-24"},
				{8, "snap-1-32"},
				{12, "snap-1-48"},
				{16, "snap-1-64"},
			};
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
			foreach (var kvp in SnapTextureByBeatSubdivision)
				allTextures.Add(kvp.Value);

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

		public static string GetSelectedTextureId(string textureId)
		{
			if (string.IsNullOrEmpty(textureId))
				return null;
			return $"{textureId}-selected";
		}

		protected static string GetTextureId(string textureId, bool selected)
		{
			return selected ? GetSelectedTextureId(textureId) : textureId;
		}

		public static Texture2D GenerateSelectedTexture(GraphicsDevice graphicsDevice, Texture2D input)
		{
			var w = input.Width;
			var h = input.Height;

			Texture2D newTexture = new Texture2D(graphicsDevice, w, h);

			var n = w * h;
			var colorData = new uint[n];
			var newColorData = new uint[n];
			input.GetData(colorData);
			for (var i = 0; i < n; i++)
			{
				newColorData[i] = ColorRGBAMultiply(colorData[i], SelectedColorMultiplier);
			}

			newTexture.SetData(newColorData);
			return newTexture;
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
				case SMCommon.ChartType.pump_double:
				case SMCommon.ChartType.pump_couple:
				case SMCommon.ChartType.pump_routine:
					return new ArrowGraphicManagerPIUSingleOrDouble();
				case SMCommon.ChartType.pump_halfdouble:
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

		public static uint GetArrowColorRGBAForSubdivision(int subdivision)
		{
			return ArrowGraphicManagerDance.GetDanceArrowColorRGBAForSubdivision(subdivision);
		}

		public abstract uint GetArrowColorRGBA(int integerPosition, int lane);
		public abstract ushort GetArrowColorBGR565(int integerPosition, int lane);
		public abstract uint GetHoldColorRGBA(int integerPosition, int lane);
		public abstract ushort GetHoldColorBGR565(int integerPosition, int lane);
		public abstract uint GetRollColorRGBA(int integerPosition, int lane);
		public abstract ushort GetRollColorBGR565(int integerPosition, int lane);

		public static uint GetMineColorRGBA()
		{
			return MineColorRGBA;
		}

		public static ushort GetMineColorBGR565()
		{
			return MineColorBGR565;
		}

		public static string GetMineTexture(int integerPosition, int lane, bool selected)
		{
			return GetTextureId(TextureIdMine, selected);
		}

		public static string GetSnapIndicatorTexture(int subdivision)
		{
			if (subdivision == 0)
				return null;
			if (!SnapTextureByBeatSubdivision.TryGetValue(subdivision, out string texture))
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

		protected static readonly Dictionary<int, uint> ArrowColorRGBABySubdivision;
		protected static readonly uint[] ArrowColorRGBAByRow;
		protected static readonly ushort[] ArrowColorBGR565ByRow;
		protected static readonly uint HoldColorRGBA;
		protected static readonly ushort HoldColorBGR565;
		protected static readonly uint RollColorRGBA;
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

			ArrowColorRGBABySubdivision = new Dictionary<int, uint>
			{
				{ 1, ColorRGBAMultiply(0xFF1818B6, ColorMultiplier) }, // Red
				{ 2, ColorRGBAMultiply(0xFFB63518, ColorMultiplier) }, // Blue
				{ 3, ColorRGBAMultiply(0xFF37AD36, ColorMultiplier) }, // Green
				{ 4, ColorRGBAMultiply(0xFF16CAD1, ColorMultiplier) }, // Yellow
				{ 6, ColorRGBAMultiply(0xFFB61884, ColorMultiplier) }, // Purple
				{ 8, ColorRGBAMultiply(0xFF98B618, ColorMultiplier) }, // Cyan
				{ 12, ColorRGBAMultiply(0xFF8018B6, ColorMultiplier) }, // Pink
				{ 16, ColorRGBAMultiply(0xFF586F4F, ColorMultiplier) }, // Pale Grey Green
				{ 48, ColorRGBAMultiply(0xFF586F4F, ColorMultiplier) }, // Pale Grey Green
			};
			ArrowColorRGBAByRow = new uint[SMCommon.MaxValidDenominator];
			ArrowColorBGR565ByRow = new ushort[SMCommon.MaxValidDenominator];
			for (var i = 0; i < SMCommon.MaxValidDenominator; i++)
			{
				var key = new Fraction(i, SMCommon.MaxValidDenominator).Reduce().Denominator;

				if (!ArrowColorRGBABySubdivision.ContainsKey(key))
					key = 16;
				ArrowColorRGBAByRow[i] = ArrowColorRGBABySubdivision[key];
				ArrowColorBGR565ByRow[i] = ToBGR565(ArrowColorRGBAByRow[i]);
			}
			
			HoldColorRGBA = ColorRGBAMultiply(0xFF696969, ColorMultiplier); // Grey
			HoldColorBGR565 = ToBGR565(HoldColorRGBA);
			RollColorRGBA = ColorRGBAMultiply(0xFF2264A6, ColorMultiplier); // Orange
			RollColorBGR565 = ToBGR565(RollColorRGBA);
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

		public override uint GetArrowColorRGBA(int integerPosition, int lane)
		{
			return ArrowColorRGBAByRow[integerPosition % SMCommon.MaxValidDenominator];
		}

		public static uint GetDanceArrowColorRGBAForSubdivision(int subdivision)
		{
			return ArrowColorRGBABySubdivision[subdivision];
		}

		public override ushort GetArrowColorBGR565(int integerPosition, int lane)
		{
			return ArrowColorBGR565ByRow[integerPosition % SMCommon.MaxValidDenominator];
		}
		
		public override uint GetHoldColorRGBA(int integerPosition, int lane)
		{
			return HoldColorRGBA;
		}

		public override ushort GetHoldColorBGR565(int integerPosition, int lane)
		{
			return HoldColorBGR565;
		}

		public override uint GetRollColorRGBA(int integerPosition, int lane)
		{
			return RollColorRGBA;
		}

		public override ushort GetRollColorBGR565(int integerPosition, int lane)
		{
			return RollColorBGR565;
		}
	}

	internal sealed class ArrowGraphicManagerDanceSingleOrDouble : ArrowGraphicManagerDance
	{
		private static readonly float[] ArrowRotations =
		{
			(float)Math.PI * 0.5f,	// L
			0.0f,					// D
			(float)Math.PI,			// U
			(float)Math.PI * 1.5f,	// R
		};

		public override (string, float) GetArrowTexture(int integerPosition, int lane, bool selected)
		{
			return (GetTextureId(ArrowTextureByRow[integerPosition % SMCommon.MaxValidDenominator].DownArrow, selected), ArrowRotations[lane % 4]);
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
				return (GetTextureId(ArrowTextureByRow[integerPosition % SMCommon.MaxValidDenominator].UpLeftArrow, selected), GetRotation(lane));
			}
			return (GetTextureId(ArrowTextureByRow[integerPosition % SMCommon.MaxValidDenominator].DownArrow, selected), GetRotation(lane));
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
			return (GetTextureId(held ? HoldTexturesActive.Body.UpLeftArrow : HoldTexturesInactive.Body.UpLeftArrow, selected), false);
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
				return (GetTextureId(held ? HoldTexturesActive.Start.UpLeftArrow : HoldTexturesInactive.Start.UpLeftArrow, selected), false);
			}
			return (GetTextureId(held ? HoldTexturesActive.Start.DownArrow : HoldTexturesInactive.Start.DownArrow, selected), false);
		}

		public override (string, bool) GetRollBodyTexture(int integerPosition, int lane, bool held, bool selected)
		{
			// Always use the narrower diagonal hold graphics in solo.
			return (GetTextureId(held ? RollTexturesActive.Body.UpLeftArrow : RollTexturesInactive.Body.UpLeftArrow, selected), false);
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
				return (GetTextureId(held ? RollTexturesActive.Start.UpLeftArrow : RollTexturesInactive.Start.UpLeftArrow, selected), false);
			}
			return (GetTextureId(held ? RollTexturesActive.Start.DownArrow : RollTexturesInactive.Start.DownArrow, selected), false);
		}
	}

	internal sealed class ArrowGraphicManagerDanceSolo : ArrowGraphicManagerDanceSoloBase
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

	internal sealed class ArrowGraphicManagerDanceThreePanel : ArrowGraphicManagerDanceSoloBase
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

	internal abstract class ArrowGraphicManagerDanceSMX : ArrowGraphicManagerDance
	{
		protected abstract bool ShouldUseCenterArrow(int lane);
		protected abstract float GetRotation(int lane);

		public override (string, float) GetArrowTexture(int integerPosition, int lane, bool selected)
		{
			if (ShouldUseCenterArrow(lane))
			{
				return (GetTextureId(ArrowTextureByRow[integerPosition % SMCommon.MaxValidDenominator].CenterArrow, selected), GetRotation(lane));
			}
			return (GetTextureId(ArrowTextureByRow[integerPosition % SMCommon.MaxValidDenominator].DownArrow, selected), GetRotation(lane));
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
			return (GetTextureId(held ? HoldTexturesActive.Body.CenterArrow : HoldTexturesInactive.Body.CenterArrow, selected), false);
		}

		public override (string, float) GetHoldEndTexture(int integerPosition, int lane, bool held, bool selected)
		{
			return (GetTextureId(held ? HoldTexturesActive.End.CenterArrow : HoldTexturesInactive.End.CenterArrow, selected), 0.0f);
		}

		public override (string, bool) GetHoldStartTexture(int integerPosition, int lane, bool held, bool selected)
		{
			return (GetTextureId(held ? HoldTexturesActive.Start.CenterArrow : HoldTexturesInactive.Start.CenterArrow, selected), false);
		}

		public override (string, bool) GetRollBodyTexture(int integerPosition, int lane, bool held, bool selected)
		{
			return (GetTextureId(held ? RollTexturesActive.Body.CenterArrow : RollTexturesInactive.Body.CenterArrow, selected), false);
		}

		public override (string, float) GetRollEndTexture(int integerPosition, int lane, bool held, bool selected)
		{
			return (GetTextureId(held ? RollTexturesActive.End.CenterArrow : RollTexturesInactive.End.CenterArrow, selected), 0.0f);
		}

		public override (string, bool) GetRollStartTexture(int integerPosition, int lane, bool held, bool selected)
		{
			return (GetTextureId(held ? RollTexturesActive.Start.CenterArrow : RollTexturesInactive.Start.CenterArrow, selected), false);
		}
	}

	internal sealed class ArrowGraphicManagerDanceSMXBeginner : ArrowGraphicManagerDanceSMX
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

	internal sealed class ArrowGraphicManagerDanceSMXSingleOrFull : ArrowGraphicManagerDanceSMX
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

	internal sealed class ArrowGraphicManagerDanceSMXDual : ArrowGraphicManagerDanceSMX
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

	internal abstract class ArrowGraphicManagerPIU : ArrowGraphicManager
	{
		protected static readonly uint ArrowColorRedRGBA;
		protected static readonly ushort ArrowColorRedBGR565;
		protected static readonly uint ArrowColorBlueRGBA;
		protected static readonly ushort ArrowColorBlueBGR565;
		protected static readonly uint ArrowColorYellowRGBA;
		protected static readonly ushort ArrowColorYellowBGR565;

		protected static readonly uint HoldColorRedRGBA;
		protected static readonly ushort HoldColorRedBGR565;
		protected static readonly uint HoldColorBlueRGBA;
		protected static readonly ushort HoldColorBlueBGR565;
		protected static readonly uint HoldColorYellowRGBA;
		protected static readonly ushort HoldColorYellowBGR565;

		protected static readonly uint RollColorRedRGBA;
		protected static readonly ushort RollColorRedBGR565;
		protected static readonly uint RollColorBlueRGBA;
		protected static readonly ushort RollColorBlueBGR565;
		protected static readonly uint RollColorYellowRGBA;
		protected static readonly ushort RollColorYellowBGR565;

		protected static readonly float[] ArrowRotationsColored =
		{
			0.0f,					// DL
			0.0f,					// UL
			0.0f,					// C
			(float)Math.PI * 0.5f,	// UR
			(float)Math.PI * 1.5f,	// DR
		};
		protected static readonly float[] ArrowRotations =
		{
			(float)Math.PI * 1.5f,	// DL
			0.0f,					// UL
			0.0f,					// C
			(float)Math.PI * 0.5f,	// UR
			(float)Math.PI,			// DR
		};

		protected static readonly string[] ReceptorTextures =
		{
			"piu-diagonal-receptor",	// DL
			"piu-diagonal-receptor",	// UL
			"piu-center-receptor",		// C
			"piu-diagonal-receptor",	// UR
			"piu-diagonal-receptor",	// DR
		};
		protected static readonly string[] ReceptorGlowTextures =
		{
			"piu-diagonal-receptor-glow",	// DL
			"piu-diagonal-receptor-glow",	// UL
			"piu-center-receptor-glow",		// C
			"piu-diagonal-receptor-glow",	// UR
			"piu-diagonal-receptor-glow",	// DR
		};
		protected static readonly string[] ReceptorHeldTextures =
		{
			"piu-diagonal-receptor-held",	// DL
			"piu-diagonal-receptor-held",	// UL
			"piu-center-receptor-held",		// C
			"piu-diagonal-receptor-held",	// UR
			"piu-diagonal-receptor-held",	// DR
		};
		protected static readonly string[] ArrowTextures =
		{
			"piu-diagonal-blue",	// DL
			"piu-diagonal-red",		// UL
			"piu-center",			// C
			"piu-diagonal-red",		// UR
			"piu-diagonal-blue",	// DR
		};
		protected static readonly string[] HoldTextures =
		{
			"piu-hold-blue",	// DL
			"piu-hold-red",		// UL
			"piu-hold-center",	// C
			"piu-hold-red",		// UR
			"piu-hold-blue",	// DR
		};
		protected static readonly string[] RollTextures =
		{
			"piu-roll-blue",	// DL
			"piu-roll-red",		// UL
			"piu-roll-center",	// C
			"piu-roll-red",		// UR
			"piu-roll-blue",	// DR
		};
		protected static readonly bool[] HoldMirrored =
		{
			false,	// DL
			false,	// UL
			false,	// C
			true,	// UR
			true,	// DR
		};

		protected static readonly uint[] ArrowColorsRGBA;
		protected static readonly ushort[] ArrowColorsBGR565;
		protected static readonly uint[] HoldColorsRGBA;
		protected static readonly ushort[] HoldColorsBGR565;
		protected static readonly uint[] RollColorsRGBA;
		protected static readonly ushort[] RollColorsBGR565;

		protected int StartArrowIndex = 0;

		static ArrowGraphicManagerPIU()
		{
			ArrowColorRedRGBA = ColorRGBAMultiply(0xFF371BB3, ColorMultiplier);
			ArrowColorRedBGR565 = ToBGR565(ArrowColorRedRGBA);
			ArrowColorBlueRGBA = ColorRGBAMultiply(0xFFB3401B, ColorMultiplier);
			ArrowColorBlueBGR565 = ToBGR565(ArrowColorBlueRGBA);
			ArrowColorYellowRGBA = ColorRGBAMultiply(0xFF00EAFF, ColorMultiplier);
			ArrowColorYellowBGR565 = ToBGR565(ArrowColorYellowRGBA);

			HoldColorRedRGBA = ColorRGBAMultiply(0xFF5039B2, ColorMultiplier);
			HoldColorRedBGR565 = ToBGR565(HoldColorRedRGBA);
			HoldColorBlueRGBA = ColorRGBAMultiply(0xFFB35639, ColorMultiplier);
			HoldColorBlueBGR565 = ToBGR565(HoldColorBlueRGBA);
			HoldColorYellowRGBA = ColorRGBAMultiply(0xFF6BF3FF, ColorMultiplier);
			HoldColorYellowBGR565 = ToBGR565(HoldColorYellowRGBA);

			RollColorRedRGBA = ColorRGBAMultiply(0xFF6B54F8, ColorMultiplier);
			RollColorRedBGR565 = ToBGR565(RollColorRedRGBA);
			RollColorBlueRGBA = ColorRGBAMultiply(0xFFB38C1B, ColorMultiplier);
			RollColorBlueBGR565 = ToBGR565(RollColorBlueRGBA);
			RollColorYellowRGBA = ColorRGBAMultiply(0xFF2FABB5, ColorMultiplier);
			RollColorYellowBGR565 = ToBGR565(RollColorYellowRGBA);

			ArrowColorsRGBA = new uint[]
			{
				ArrowColorBlueRGBA,
				ArrowColorRedRGBA,
				ArrowColorYellowRGBA,
				ArrowColorRedRGBA,
				ArrowColorBlueRGBA,
			};
			ArrowColorsBGR565 = new ushort[]
			{
				ArrowColorBlueBGR565,
				ArrowColorRedBGR565,
				ArrowColorYellowBGR565,
				ArrowColorRedBGR565,
				ArrowColorBlueBGR565,
			};
			HoldColorsRGBA = new uint[]
			{
				HoldColorBlueRGBA,
				HoldColorRedRGBA,
				HoldColorYellowRGBA,
				HoldColorRedRGBA,
				HoldColorBlueRGBA,
			};
			HoldColorsBGR565 = new ushort[]
			{
				HoldColorBlueBGR565,
				HoldColorRedBGR565,
				HoldColorYellowBGR565,
				HoldColorRedBGR565,
				HoldColorBlueBGR565,
			};
			RollColorsRGBA = new uint[]
			{
				RollColorBlueRGBA,
				RollColorRedRGBA,
				RollColorYellowRGBA,
				RollColorRedRGBA,
				RollColorBlueRGBA,
			};
			RollColorsBGR565 = new ushort[]
			{
				RollColorBlueBGR565,
				RollColorRedBGR565,
				RollColorYellowBGR565,
				RollColorRedBGR565,
				RollColorBlueBGR565,
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
			return (HoldTextures[i], HoldMirrored[i]);
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

		public override uint GetArrowColorRGBA(int integerPosition, int lane)
		{
			var i = GetTextureIndex(lane);
			return ArrowColorsRGBA[i];
		}

		public override ushort GetArrowColorBGR565(int integerPosition, int lane)
		{
			var i = GetTextureIndex(lane);
			return ArrowColorsBGR565[i];
		}

		public override uint GetHoldColorRGBA(int integerPosition, int lane)
		{
			var i = GetTextureIndex(lane);
			return HoldColorsRGBA[i];
		}

		public override ushort GetHoldColorBGR565(int integerPosition, int lane)
		{
			var i = GetTextureIndex(lane);
			return HoldColorsBGR565[i];
		}

		public override uint GetRollColorRGBA(int integerPosition, int lane)
		{
			var i = GetTextureIndex(lane);
			return RollColorsRGBA[i];
		}

		public override ushort GetRollColorBGR565(int integerPosition, int lane)
		{
			var i = GetTextureIndex(lane);
			return RollColorsBGR565[i];
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
}

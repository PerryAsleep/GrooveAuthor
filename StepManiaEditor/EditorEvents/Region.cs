using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using static StepManiaEditor.Editor;
using static StepManiaEditor.Utils;

namespace StepManiaEditor
{
	/// <summary>
	/// Interface for defining and drawing a rectangle region on screen.
	/// </summary>
	internal interface IRegion
	{
		public double GetRegionX();
		public double GetRegionY();
		public double GetRegionW();
		public double GetRegionH();
		public Color GetRegionColor();

		public void DrawRegionImpl(TextureAtlas textureAtlas, SpriteBatch spriteBatch, int screenHeight, Color color)
		{
			var x = GetRegionX();
			var w = GetRegionW();
			if (w < 0)
			{
				x += w;
				w = -w;
			}

			var y = GetRegionY();
			var h = GetRegionH();
			if (h < 0)
			{
				y += h;
				h = -h;
			}

			if (y > screenHeight || y + h < 0)
				return;
			if (y < 0)
			{
				h += y;
				y = 0;
			}
			if (y + h > screenHeight)
			{
				h = screenHeight - y;
			}

			// TODO: round?
			textureAtlas.Draw(TextureIdRegionRect, spriteBatch, new Rectangle((int)x, (int)y, (int)w, (int)h), color);
		}
	}

	/// <summary>
	/// Interface for defining and drawing a rectangle region on screen where the y position
	/// is based on time or row.
	/// </summary>
	internal interface IChartRegion : IRegion
	{
		public void SetRegionX(double x);
		public void SetRegionY(double y);
		public void SetRegionW(double w);
		public void SetRegionH(double h);

		public double GetRegionPosition();
		public double GetRegionDuration();
		public bool AreRegionUnitsTime();
		public bool IsVisible(SpacingMode mode);
	}

	internal static class RegionExtensions
	{
		public static void DrawRegion(this IRegion region, TextureAtlas textureAtlas, SpriteBatch spriteBatch, int screenHeight)
		{
			region.DrawRegionImpl(textureAtlas, spriteBatch, screenHeight, region.GetRegionColor());
		}
	}
}

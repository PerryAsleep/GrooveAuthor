using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using static StepManiaEditor.Editor;
using static StepManiaEditor.Utils;

namespace StepManiaEditor
{
	/// <summary>
	/// Interface for defining and drawing a rectangle region on screen where the y position
	/// is based on time or row.
	/// </summary>
	public interface IRegion
	{
		public double RegionX { get; set; }
		public double RegionY { get; set; }
		public double RegionW { get; set; }
		public double RegionH { get; set; }

		public double GetRegionPosition();
		public double GetRegionDuration();
		public bool AreRegionUnitsTime();
		public bool IsVisible(SpacingMode mode);
		public Color GetRegionColor();

		public void DrawRegionImpl(TextureAtlas textureAtlas, SpriteBatch spriteBatch, int screenHeight, Color color)
		{
			var y = RegionY;
			var h = RegionH;
			if (y < 0)
			{
				y = 0;
				h += RegionY;
			}
			if (h > screenHeight)
			{
				h = screenHeight;
			}
			// TODO: round?
			textureAtlas.Draw(TextureIdRegionRect, spriteBatch, new Rectangle((int)RegionX, (int)y, (int)RegionW, (int)h), color);
		}
	}

	public static class RegionExtensions
	{
		public static void DrawRegion(this IRegion region, TextureAtlas textureAtlas, SpriteBatch spriteBatch, int screenHeight)
		{
			region.DrawRegionImpl(textureAtlas, spriteBatch, screenHeight, region.GetRegionColor());
		}
	}
}

using System.Drawing;
using Microsoft.Xna.Framework.Graphics;
using MonoGameExtensions;
using static StepManiaEditor.Utils;
using Color = Microsoft.Xna.Framework.Color;

namespace StepManiaEditor;

/// <summary>
/// Interface for defining and drawing a rectangle region on screen.
/// </summary>
internal interface IRegion
{
	public double GetRegionX();
	public double GetRegionY();
	public double GetRegionW();
	public double GetRegionH();
	public double GetRegionZ();
	public float GetRegionAlpha();
	public Color GetRegionColor();

	public void DrawRegionImpl(TextureAtlas textureAtlas, SpriteBatch spriteBatch, int screenHeight, Color color, float alpha)
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

		var xf = (float)x;
		var yf = (float)y;
		var wf = (float)w;
		var hf = (float)h;

		// If the bounds are so small that the border would cover all the fill, just draw the border.
		var rimColor = GetColor(color, alpha);
		if (hf <= 2.0f || wf <= 2.0f)
		{
			textureAtlas.Draw(TextureIdRegionRect, spriteBatch, new RectangleF(xf, yf, wf, hf), rimColor);
		}
		else
		{
			// Draw fill.
			var fillColor = GetColor(color, alpha * RegionAlpha);
			textureAtlas.Draw(TextureIdRegionRect, spriteBatch, new RectangleF(xf, yf, wf, hf), fillColor);

			// Draw border.
			textureAtlas.Draw(TextureIdRegionRect, spriteBatch,
				new RectangleF(xf, yf, 1.0f, hf), rimColor);
			textureAtlas.Draw(TextureIdRegionRect, spriteBatch,
				new RectangleF(xf, yf, wf, 1.0f), rimColor);
			textureAtlas.Draw(TextureIdRegionRect, spriteBatch,
				new RectangleF(xf + wf - 1.0f, yf, 1.0f, hf), rimColor);
			textureAtlas.Draw(TextureIdRegionRect, spriteBatch,
				new RectangleF(xf, yf + hf - 1.0f, wf, 1.0f), rimColor);
		}
	}

	private static Color GetColor(Color color, float alpha)
	{
		if (alpha >= 1.0f)
			return color;
		return new Color((float)color.R / byte.MaxValue, (float)color.G / byte.MaxValue, (float)color.B / byte.MaxValue, alpha);
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

	public double GetChartPosition();
	public double GetChartTime();
	public double GetChartPositionDurationForRegion();
	public double GetChartTimeDurationForRegion();
	public bool IsRegionSelection();
}

internal static class RegionExtensions
{
	public static void DrawRegion(this IRegion region, TextureAtlas textureAtlas, SpriteBatch spriteBatch, int screenHeight)
	{
		region.DrawRegionImpl(textureAtlas, spriteBatch, screenHeight, region.GetRegionColor(), region.GetRegionAlpha());
	}
}

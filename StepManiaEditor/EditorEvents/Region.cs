﻿using System.Drawing;
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

		textureAtlas.Draw(TextureIdRegionRect, spriteBatch,
			new RectangleF((float)x, (float)y, (float)w, (float)h), color);
	}

	public static Color GetColor(Color color, float alpha)
	{
		if (alpha >= 1.0f)
			return color;
		alpha = System.Math.Clamp(alpha * ((float)color.A / byte.MaxValue), 0.0f, 1.0f);
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
}

internal static class RegionExtensions
{
	public static void DrawRegion(this IRegion region, TextureAtlas textureAtlas, SpriteBatch spriteBatch, int screenHeight)
	{
		region.DrawRegionImpl(textureAtlas, spriteBatch, screenHeight, region.GetRegionColor());
	}
}

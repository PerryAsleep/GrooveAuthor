using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Graphics;

namespace StepManiaEditor
{
	internal sealed class TextureUtils
	{
		/// <summary>
		/// Given a texture, returns list of textures representing the mip levels.
		/// The first texture is the given texture. Every subsequent texture is half the size of the previous.
		/// Textures are generated using bilinear interpolation.
		/// </summary>
		public static List<Texture2D> GenerateMipLevels(GraphicsDevice graphicsDevice, Texture2D texture)
		{
			var mipLevels = new List<Texture2D>();
			mipLevels.Add(texture);
			RecursiveGenerateMipLevels(graphicsDevice, texture, mipLevels);
			return mipLevels;
		}

		private static void RecursiveGenerateMipLevels(GraphicsDevice graphicsDevice, Texture2D texture, List<Texture2D> mipLevels)
		{
			if (texture.Width <= 1 || texture.Height <= 1)
				return;

			var W = texture.Width;
			var H = texture.Height;
			var w = W >> 1;
			var h = H >> 1;

			Texture2D newTexture = new Texture2D(graphicsDevice, w, h);
			uint[] colorData = new uint[texture.Width * texture.Height];
			texture.GetData(colorData);

			var newColorData = new uint[w * h];
			for (var x = 0; x < w; x++)
			{
				for (var y = 0; y < h; y++)
				{
					// Perform bilinear interpolation.
					var X = x * 2;
					var Y = y * 2;
					uint a = colorData[Y * W + X];
					uint b = colorData[Y * W + X + 1];
					uint c = colorData[(Y + 1) * W + X];
					uint d = colorData[(Y + 1) * W + X + 1];
					newColorData[y * w + x] = Utils.ColorRGBAInterpolate(
						Utils.ColorRGBAInterpolate(a, b, 0.5f),
						Utils.ColorRGBAInterpolate(c, d, 0.5f), 0.5f);
				}
			}
			newTexture.SetData(newColorData);

			mipLevels.Add(newTexture);
			RecursiveGenerateMipLevels(graphicsDevice, newTexture, mipLevels);
		}

		/// <summary>
		/// Gets the average color of the given texture.
		/// The average color is calculated from the texture's HSV values.
		/// Hue is averaged.
		/// Value and saturation are averaged with root mean square.
		/// </summary>
		public static uint GetTextureColor(Texture2D texture)
		{
			var colorData = GetRGBAColorData(texture);
			double hueXSum = 0.0f;
			double hueYSum = 0.0f;
			double saturationSumOfSquares = 0.0f;
			double valueSumOfSquares = 0.0f;
			float r, g, b, a, h, s, v;
			double hx, hy;
			foreach (var color in colorData)
			{
				// Convert the color to HSV values.
				(r, g, b, a) = Utils.ToFloats(color);
				(h, s, v) = RgbToHsv(r, g, b);

				saturationSumOfSquares += (s * s);
				valueSumOfSquares += (v * v);

				// Hue values are angles around a circle. We need to determine the average x and y
				// and then compute the average angle from those values.
				hx = Math.Cos(h);
				hy = Math.Sin(h);
				hueXSum += hx;
				hueYSum += hy;
			}

			// Determine the average hue by determining the angle of the average hue x and y values.
			hx = (hueXSum / colorData.Length);
			hy = (hueYSum / colorData.Length);
			double avgHue = Math.Atan2(hy, hx);
			if (avgHue < 0.0)
				avgHue = 2.0 * Math.PI + avgHue;

			// Convert back to RGB.
			(r, g, b) = HsvToRgb(
				(float)avgHue,
				(float)Math.Sqrt(saturationSumOfSquares / colorData.Length),
				(float)Math.Sqrt(valueSumOfSquares / colorData.Length));

			return Utils.ToRGBA(r, g, b, 1.0f);
		}

		/// <summary>
		/// Given a color represented by red, green, and blue floating point values ranging from 0.0f to 1.0f,
		/// return the hue, saturation, and value of the color.
		/// Hue is represented as a degree in radians between 0.0 and 2*pi.
		/// For pure grey colors the returned hue will be 0.0.
		/// </summary>
		public static (float, float, float) RgbToHsv(float r, float g, float b)
		{
			float h = 0.0f, s, v;
			var min = Math.Min(Math.Min(r, g), b);
			var max = Math.Max(Math.Max(r, g), b);

			v = max;
			s = max.FloatEquals(0.0f) ? 0.0f : (max - min) / max;
			if (!s.FloatEquals(0.0f))
			{
				var d = max - min;
				if (r.FloatEquals(max))
				{
					h = (g - b) / d;
				}
				else if (g.FloatEquals(max))
				{
					h = 2 + (b - r) / d;
				}
				else
				{
					h = 4 + (r - g) / d;
				}
				h *= (float)(Math.PI / 3.0f);
				if (h < 0.0f)
				{
					h += (float)(2.0f * Math.PI);
				}
			}

			return (h, s, v);
		}

		/// <summary>
		/// Given a color represented by hue, saturation, and value return the red, blue, and green
		/// values of the color. The saturation and value parameters are expected to be in the range
		/// of 0.0 to 1.0. The hue value is expected to be between 0.0 and 2*pi. The returned color
		/// values will be between 0.0 and 1.0.
		/// </summary>
		public static (float, float, float) HsvToRgb(float h, float s, float v)
		{
			float r, g, b;

			if (s.FloatEquals(0.0f))
			{
				r = v;
				g = v;
				b = v;
			}
			else
			{
				if (h.FloatEquals((float)(Math.PI * 2.0f)))
					h = 0.0f;
				else
					h = (float)((h * 3.0f) / Math.PI);
				var sextant = (float)Math.Floor(h);
				var f = h - sextant;
				var p = v * (1.0f - s);
				var q = v * (1.0f - (s * f));
				var t = v * (1.0f - (s * (1.0f - f)));
				switch (sextant)
				{
					default:
					case 0: r = v; g = t; b = p; break;
					case 1: r = q; g = v; b = p; break;
					case 2: r = p; g = v; b = t; break;
					case 3: r = p; g = q; b = v; break;
					case 4: r = t; g = p; b = v; break;
					case 5: r = v; g = p; b = q; break;
				}

			}

			return (r, g, b);
		}

		public static uint[] GetRGBAColorData(Texture2D texture)
		{
			var data = new uint[texture.Width * texture.Height];
			switch (texture.Format)
			{
				case SurfaceFormat.Color:
					{
						texture.GetData(data);
						break;
					}
				default:
					break;
			}
			return data;
		}
	}
}

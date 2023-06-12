using Fumen;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MonoGameExtensions;

public class TextureUtils
{
	public enum TextureLayoutMode
	{
		/// <summary>
		/// Draw the texture at its original size, centered in the destination area. If the texture is larger
		/// than the destination area then it will be cropped as needed to fit. If it is smaller then it will
		/// be rendered smaller.
		/// </summary>
		OriginalSize,

		/// <summary>
		/// The texture will fill the destination area exactly. It will shrink or grow as needed and the aspect
		/// ratio will change to match the destination.
		/// </summary>
		Stretch,

		/// <summary>
		/// Maintain the texture's original aspect ratio and fill the destination area. If the texture aspect
		/// ratio does not match the destination area's aspect ratio, then the texture will be cropped.
		/// </summary>
		Fill,

		/// <summary>
		/// Letterbox or pillarbox as needed such that texture's original aspect ratio is maintained and it fills
		/// the destination area as much as possible.
		/// </summary>
		Box,
	}

	public static (float xOffset, float yOffset, System.Numerics.Vector2 size, System.Numerics.Vector2 uv0,
		System.Numerics.Vector2 uv1) GetTextureUVs(
			Texture2D texture, uint width, uint height, TextureLayoutMode mode)
	{
		var xOffset = 0.0f;
		var yOffset = 0.0f;

		// The size of the image to draw.
		var size = new System.Numerics.Vector2(width, height);

		// The UV coordinates for drawing the texture on the image.
		var uv0 = new System.Numerics.Vector2(0.0f, 0.0f);
		var uv1 = new System.Numerics.Vector2(1.0f, 1.0f);

		switch (mode)
		{
			// Maintain the original size of the texture.
			// Crop and offset as needed.
			case TextureLayoutMode.OriginalSize:
			{
				// If the texture is wider than the destination area then adjust the UV X values
				// so that we crop the texture.
				if (texture.Width > width)
				{
					xOffset = 0.0f;
					size.X = width;
					uv0.X = (texture.Width - width) * 0.5f / texture.Width;
					uv1.X = 1.0f - uv0.X;
				}
				// If the destination area is wider than the texture, then set the X offset value
				// so that we center the texture in X within the destination area.
				else if (texture.Width < width)
				{
					xOffset = (width - texture.Width) * 0.5f;
					size.X = texture.Width;
					uv0.X = 0.0f;
					uv1.X = 1.0f;
				}

				// If the texture is taller than the destination area then adjust the UV Y values
				// so that we crop the texture.
				if (texture.Height > height)
				{
					yOffset = 0.0f;
					size.Y = height;
					uv0.Y = (texture.Height - height) * 0.5f / texture.Height;
					uv1.Y = 1.0f - uv0.Y;
				}
				// If the destination area is taller than the texture, then set the Y offset value
				// so that we center the texture in Y within the destination area.
				else if (texture.Height < height)
				{
					yOffset = (height - texture.Height) * 0.5f;
					size.Y = texture.Height;
					uv0.Y = 0.0f;
					uv1.Y = 1.0f;
				}

				break;
			}

			// Stretch the texture to exactly fill the destination area.
			// The parameters are already set for rendering in this mode.
			case TextureLayoutMode.Stretch:
			{
				break;
			}

			// Scale the texture uniformly such that it fills the entire destination area.
			// Crop the dimension which goes beyond the destination area as needed.
			case TextureLayoutMode.Fill:
			{
				var textureAspectRatio = (float)texture.Width / texture.Height;
				var destinationAspectRatio = (float)width / height;

				// If the texture is wider than the destination area, crop the left and right.
				if (textureAspectRatio > destinationAspectRatio)
				{
					// Crop left and right.
					var scaledTextureW = texture.Width * ((float)height / texture.Height);
					uv0.X = (scaledTextureW - height) * 0.5f / scaledTextureW;
					uv1.X = 1.0f - uv0.X;

					// Fill Y.
					uv0.Y = 0.0f;
					uv1.Y = 1.0f;
				}

				// If the texture is taller than the destination area, crop the top and bottom.
				else if (textureAspectRatio < destinationAspectRatio)
				{
					// Fill X.
					uv0.X = 0.0f;
					uv1.X = 1.0f;

					// Crop top and bottom.
					var scaledTextureH = texture.Height * ((float)width / texture.Width);
					uv0.Y = (scaledTextureH - width) * 0.5f / scaledTextureH;
					uv1.Y = 1.0f - uv0.Y;
				}

				break;
			}

			// Scale the texture uniformly such that it fills the destination area without going over
			// in either dimension.
			case TextureLayoutMode.Box:
			{
				var textureAspectRatio = (float)texture.Width / texture.Height;
				var destinationAspectRatio = (float)width / height;

				// If the texture is wider than the destination area, letterbox.
				if (textureAspectRatio > destinationAspectRatio)
				{
					var scale = (float)width / texture.Width;
					size.X = texture.Width * scale;
					size.Y = texture.Height * scale;
					yOffset = (height - texture.Height * scale) * 0.5f;
				}

				// If the texture is taller than the destination area, pillarbox.
				else if (textureAspectRatio < destinationAspectRatio)
				{
					var scale = (float)height / texture.Height;
					size.X = texture.Width * scale;
					size.Y = texture.Height * scale;
					xOffset = (width - texture.Width * scale) * 0.5f;
				}

				break;
			}
		}

		return (xOffset, yOffset, size, uv0, uv1);
	}

	public static void DrawTexture(
		SpriteBatch spriteBatch,
		Texture2D texture,
		int x,
		int y,
		uint width,
		uint height,
		TextureLayoutMode mode)
	{
		var (xOffset, yOffset, size, uv0, uv1) = GetTextureUVs(texture, width, height, mode);

		var destRect = new Rectangle((int)(x + xOffset), (int)(y + yOffset), (int)size.X, (int)size.Y);
		var sourceRect = new Rectangle((int)(uv0.X * texture.Width), (int)(uv0.Y * texture.Height), (int)(uv1.X * texture.Width),
			(int)(uv1.Y * texture.Height));

		spriteBatch.Draw(texture, destRect, sourceRect, Color.White);
	}

	/// <summary>
	/// Given a texture, returns list of textures representing the mip levels.
	/// The first texture is the given texture. Every subsequent texture is half the size of the previous.
	/// Textures are generated using bilinear interpolation.
	/// </summary>
	public static List<Texture2D> GenerateMipLevels(GraphicsDevice graphicsDevice, Texture2D texture)
	{
		List<Texture2D> mipLevels = new() { texture };
		RecursiveGenerateMipLevels(graphicsDevice, texture, mipLevels);
		return mipLevels;
	}

	private static void RecursiveGenerateMipLevels(GraphicsDevice graphicsDevice, Texture2D texture, List<Texture2D> mipLevels)
	{
		if (texture.Width <= 1 || texture.Height <= 1)
			return;

		var previousLevelWidth = texture.Width;
		var previousLevelHeight = texture.Height;
		var currentLevelWidth = previousLevelWidth >> 1;
		var currentLevelHeight = previousLevelHeight >> 1;

		var newTexture = new Texture2D(graphicsDevice, currentLevelWidth, currentLevelHeight);
		var colorData = new uint[texture.Width * texture.Height];
		texture.GetData(colorData);

		var newColorData = new uint[currentLevelWidth * currentLevelHeight];
		for (var xIndex = 0; xIndex < currentLevelWidth; xIndex++)
		{
			for (var yIndex = 0; yIndex < currentLevelHeight; yIndex++)
			{
				// Perform bilinear interpolation.
				var x = xIndex * 2;
				var y = yIndex * 2;
				var a = colorData[y * previousLevelWidth + x];
				var b = colorData[y * previousLevelWidth + x + 1];
				var c = colorData[(y + 1) * previousLevelWidth + x];
				var d = colorData[(y + 1) * previousLevelWidth + x + 1];
				newColorData[yIndex * currentLevelWidth + xIndex] = Fumen.ColorUtils.ColorRGBAInterpolate(
					Fumen.ColorUtils.ColorRGBAInterpolate(a, b, 0.5f),
					Fumen.ColorUtils.ColorRGBAInterpolate(c, d, 0.5f), 0.5f);
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
	/// Alpha is ignored.
	/// </summary>
	public static uint GetTextureColor(Texture2D texture)
	{
		var colorData = GetRGBAColorData(texture);
		double hueXSum = 0.0f;
		double hueYSum = 0.0f;
		double saturationSumOfSquares = 0.0f;
		double valueSumOfSquares = 0.0f;
		float r, g, b;
		double hx, hy;
		foreach (var color in colorData)
		{
			// Convert the color to HSV values.
			(r, g, b, _) = Fumen.ColorUtils.ToFloats(color);
			var (h, s, v) = Fumen.ColorUtils.RgbToHsv(r, g, b);

			saturationSumOfSquares += s * s;
			valueSumOfSquares += v * v;

			// Hue values are angles around a circle. We need to determine the average x and y
			// and then compute the average angle from those values.
			hx = Math.Cos(h);
			hy = Math.Sin(h);
			hueXSum += hx;
			hueYSum += hy;
		}

		// Determine the average hue by determining the angle of the average hue x and y values.
		hx = hueXSum / colorData.Length;
		hy = hueYSum / colorData.Length;
		var avgHue = Math.Atan2(hy, hx);
		if (avgHue < 0.0)
			avgHue = 2.0 * Math.PI + avgHue;

		// Convert back to RGB.
		(r, g, b) = Fumen.ColorUtils.HsvToRgb(
			(float)avgHue,
			(float)Math.Sqrt(saturationSumOfSquares / colorData.Length),
			(float)Math.Sqrt(valueSumOfSquares / colorData.Length));

		return Fumen.ColorUtils.ToRGBA(r, g, b, 1.0f);
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
		}

		return data;
	}
}

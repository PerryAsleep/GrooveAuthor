using System.Collections.Generic;
using Microsoft.Xna.Framework.Graphics;

namespace StepManiaEditor
{
	class TextureUtils
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
	}
}

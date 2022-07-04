using Fumen;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using static StepManiaEditor.Utils;

namespace StepManiaEditor
{
	public class EditorMarkerEvent
	{
		public double X;
		public double Y;
		public double W;
		public double H;
		public double Scale;
		public bool MeasureMarker;
		public int Measure;

		public void Draw(TextureAtlas textureAtlas, SpriteBatch spriteBatch, SpriteFont font)
		{
			// Measure marker.
			if (MeasureMarker)
			{
				var alpha = Interpolation.Lerp(1.0f, 0.0f, MeasureMarkerScaleToStartingFading, MeasureMarkerMinScale, Scale);

				// Measure line.
				textureAtlas.Draw(
					TextureIdMeasureMarker,
					spriteBatch,
					new Rectangle((int)(X + 0.5), (int)(Y + 0.5), (int)(W + .5), (int)(H + 0.5)),
					(float)alpha);

				// Measure number value.
				if (Scale < MeasureNumberMinScale)
					return;

				alpha = Interpolation.Lerp(1.0f, 0.0f, MeasureNumberScaleToStartFading, MeasureNumberMinScale, Scale);
				var measureString = Measure.ToString();
				var anchorPos = new Vector2((float)(X - 20 * Scale), (float)Y);
				var drawPos = GetDrawPos(font, measureString, anchorPos, 1.0f, HorizontalAlignment.Right,
					VerticalAlignment.Center);
				spriteBatch.DrawString(font, measureString, drawPos, new Color(1.0f, 1.0f, 1.0f, (float)alpha), 0.0f, Vector2.Zero, 1.0f, SpriteEffects.None, 1.0f);
			}

			// Beat marker.
			else
			{
				var alpha = Interpolation.Lerp(1.0f, 0.0f, BeatMarkerScaleToStartingFading, BeatMarkerMinScale, Scale);

				// Beat line.
				textureAtlas.Draw(
					TextureIdBeatMarker,
					spriteBatch,
					new Rectangle((int)(X + 0.5), (int)(Y + 0.5), (int)(W + .5), (int)(H + 0.5)),
					(float)alpha);
			}
		}
	}
}

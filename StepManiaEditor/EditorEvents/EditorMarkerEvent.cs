using System.Drawing;
using Fumen;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGameExtensions;
using static StepManiaEditor.Utils;
using static StepManiaEditor.ImGuiUtils;
using Color = Microsoft.Xna.Framework.Color;

namespace StepManiaEditor;

internal sealed class EditorMarkerEvent
{
	private readonly double X;
	private readonly double Y;
	private readonly double W;
	private readonly double H;
	private readonly double Scale;
	private readonly bool MeasureMarker;
	private readonly int Measure;

	public EditorMarkerEvent(double x, double y, double w, double h, double scale, bool measureMarker, int measure)
	{
		X = x;
		Y = y;
		W = w;
		H = h;
		Scale = scale;
		MeasureMarker = measureMarker;
		Measure = measure;
	}

	private static double GetNumberAlpha(double scale)
	{
		return Interpolation.Lerp(1.0f, 0.0f, MeasureNumberScaleToStartFading, MeasureNumberMinScale, scale);
	}

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
				new RectangleF((float)X, (float)Y, (float)W, (float)H),
				(float)alpha);

			// Measure number value.
			if (Scale < MeasureNumberMinScale)
				return;

			alpha = GetNumberAlpha(Scale);
			var measureString = Measure.ToString();
			var anchorPos = new Vector2((float)(X - GetMeasureMarkerNumberPadding()), (float)Y);
			var drawPos = GetDrawPos(font, measureString, anchorPos, 1.0f, HorizontalAlignment.Right,
				VerticalAlignment.Center);
			spriteBatch.DrawString(font, measureString, drawPos, new Color(1.0f, 1.0f, 1.0f, (float)alpha), 0.0f, Vector2.Zero,
				1.0f, SpriteEffects.None, 1.0f);
		}

		// Beat marker.
		else
		{
			var alpha = Interpolation.Lerp(1.0f, 0.0f, BeatMarkerScaleToStartingFading, BeatMarkerMinScale, Scale);

			// Beat line.
			textureAtlas.Draw(
				TextureIdBeatMarker,
				spriteBatch,
				new RectangleF((float)X, (float)Y, (float)W, (float)H),
				(float)alpha);
		}
	}
}

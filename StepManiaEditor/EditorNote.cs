using System;
using System.Collections.Generic;
using System.Text;
using Fumen;
using Fumen.ChartDefinition;
using Fumen.Converters;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StepManiaLibrary;

namespace StepManiaEditor
{
	public class EditorEvent : IComparable<EditorEvent>
	{
		public Event ChartEvent;

		// Foot, expression, etc.

		public double X;
		public double Y;
		public double W;
		public double H;
		public double Scale = 1.0;

		public EditorEvent(Event chartEvent)
		{
			ChartEvent = chartEvent;
		}

		// Still not sure about if holds should be 1 Event or 2.
		public int GetLane()
		{
			if (ChartEvent is LaneNote ln)
				return ln.Lane;
			return -1;
		}

		public int GetRow()
		{
			return ChartEvent.IntegerPosition;
		}

		public int GetLength()
		{
			return 0;
		}

		public int CompareTo(EditorEvent other)
		{
			return SMCommon.SMEventComparer.Compare(ChartEvent, other.ChartEvent);
		}

		public void Update()
		{

		}

		public bool InSelection(double x, double y, double w, double h)
		{
			// TODO
			return false;
		}

		public void Draw(TextureAtlas textureAtlas, SpriteBatch spriteBatch)
		{
			var rot = new[] { (float)Math.PI * 0.5f, 0.0f, (float)Math.PI, (float)Math.PI * 1.5f };

			if (ChartEvent is LaneTapNote ltn)
			{
				var measureSubdivision = new Fraction(ltn.IntegerPosition % SMCommon.RowsPerMeasure, SMCommon.RowsPerMeasure).Reduce().Denominator;
				var textureId = Utils.GetArrowTextureId(measureSubdivision);

				textureAtlas.Draw(
					textureId,
					spriteBatch,
					new Vector2((float)X, (float)Y),
					(float)Scale,
					rot[ltn.Lane % rot.Length]);
			}
		}
	}
}

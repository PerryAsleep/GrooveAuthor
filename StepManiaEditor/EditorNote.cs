using System;
using Fumen;
using Fumen.ChartDefinition;
using Fumen.Converters;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using static StepManiaEditor.Utils;

namespace StepManiaEditor
{
	public class MarkerEvent
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


	public class EditorEvent : IComparable<EditorEvent>
	{
		// Foot, expression, etc.

		public double X;
		public double Y;
		public double W;
		public double H;
		public double Scale = 1.0;

		public Event ChartEvent;

		public static EditorEvent CreateEvent(Event chartEvent)
		{
			if (chartEvent is LaneTapNote ltn)
				return new EditorTapNote(ltn);
			if (chartEvent is LaneHoldStartNote lhsn)
				return new EditorHoldStartNote(lhsn);
			if (chartEvent is LaneHoldEndNote lhen)
				return new EditorHoldEndNote(lhen);
			if (chartEvent is LaneNote ln && ln.SourceType == SMCommon.NoteChars[(int)SMCommon.NoteType.Mine].ToString())
				return new EditorMineNote(ln);

			// TODO: More event types
			// For now, using what should be an abstract class
			return new EditorEvent(chartEvent);

			return null;
		}

		public EditorEvent(Event chartEvent)
		{
			ChartEvent = chartEvent;
		}
		
		public virtual int GetLane()
		{
			if (ChartEvent is LaneNote ln)
				return ln.Lane;
			return -1;
		}

		public virtual int GetRow()
		{
			return ChartEvent.IntegerPosition;
		}

		public virtual int GetLength()
		{
			return 0;
		}

		public virtual int CompareTo(EditorEvent other)
		{
			return SMCommon.SMEventComparer.Compare(ChartEvent, other.ChartEvent);
		}

		public virtual bool InSelection(double x, double y, double w, double h)
		{
			return X < x + w && X + W > x && Y < y + h && Y + H > y;
		}

		public virtual void Draw(TextureAtlas textureAtlas, SpriteBatch spriteBatch)
		{
		}

		public static int CompareToRow(double row, EditorEvent editorEvent)
		{
			return row.CompareTo(editorEvent.ChartEvent.IntegerPosition);
		}

		public static int CompareToTime(double time, EditorEvent editorEvent)
		{
			return time.CompareTo(editorEvent.ChartEvent.TimeMicros / 1000000.0);
		}
	}

	public class EditorTapNote : EditorEvent
	{
		private readonly LaneTapNote LaneTapNote;

		public EditorTapNote(LaneTapNote chartEvent) : base(chartEvent)
		{
			LaneTapNote = chartEvent;
		}

		public override int GetLane()
		{
			return LaneTapNote.Lane;
		}

		public override void Draw(TextureAtlas textureAtlas, SpriteBatch spriteBatch)
		{
			var rot = new[] { (float)Math.PI * 0.5f, 0.0f, (float)Math.PI, (float)Math.PI * 1.5f };

			var measureSubdivision = new Fraction(LaneTapNote.IntegerPosition % SMCommon.RowsPerMeasure, SMCommon.RowsPerMeasure).Reduce().Denominator;
			var textureId = GetArrowTextureId(measureSubdivision);

			textureAtlas.Draw(
				textureId,
				spriteBatch,
				new Vector2((float)X, (float)Y),
				(float)Scale,
				rot[LaneTapNote.Lane % rot.Length],
				1.0f);
		}
	}

	public class EditorMineNote : EditorEvent
	{
		private readonly LaneNote LaneNote;

		public EditorMineNote(LaneNote chartEvent) : base(chartEvent)
		{
			LaneNote = chartEvent;
		}

		public override int GetLane()
		{
			return LaneNote.Lane;
		}

		public override void Draw(TextureAtlas textureAtlas, SpriteBatch spriteBatch)
		{
			textureAtlas.Draw(
				TextureIdMine,
				spriteBatch,
				new Vector2((float)X, (float)Y),
				(float)Scale,
				0.0f,
				1.0f);
		}
	}


	public class EditorHoldStartNote : EditorEvent
	{
		private readonly LaneHoldStartNote LaneHoldStartNote;
		private EditorHoldEndNote EditorHoldEndNote;

		public EditorHoldStartNote(LaneHoldStartNote chartEvent) : base(chartEvent)
		{
			LaneHoldStartNote = chartEvent;
		}

		public void SetHoldEndNote(EditorHoldEndNote editorHoldEndNote)
		{
			EditorHoldEndNote = editorHoldEndNote;
		}

		public EditorHoldEndNote GetHoldEndNote()
		{
			return EditorHoldEndNote;
		}

		public override int GetLane()
		{
			return LaneHoldStartNote.Lane;
		}

		public override int GetLength()
		{
			return EditorHoldEndNote.GetRow() - LaneHoldStartNote.IntegerPosition;
		}

		public bool IsRoll()
		{
			return LaneHoldStartNote.SourceType == SMCommon.NoteChars[(int)SMCommon.NoteType.RollStart].ToString();
		}

		public override void Draw(TextureAtlas textureAtlas, SpriteBatch spriteBatch)
		{
			var rot = new[] { (float)Math.PI * 0.5f, 0.0f, (float)Math.PI, (float)Math.PI * 1.5f };

			var measureSubdivision = new Fraction(LaneHoldStartNote.IntegerPosition % SMCommon.RowsPerMeasure, SMCommon.RowsPerMeasure).Reduce().Denominator;
			var textureId = GetArrowTextureId(measureSubdivision);

			textureAtlas.Draw(
				textureId,
				spriteBatch,
				new Vector2((float)X, (float)Y),
				(float)Scale,
				rot[LaneHoldStartNote.Lane % rot.Length],
				1.0f);
		}
	}

	public class EditorHoldEndNote : EditorEvent
	{
		private EditorHoldStartNote EditorHoldStartNote;
		private readonly LaneHoldEndNote LaneHoldEndNote;

		/// <summary>
		/// Whether or not this hold should be considered active for rendering.
		/// </summary>
		public bool Active;

		public EditorHoldEndNote(LaneHoldEndNote chartEvent) : base(chartEvent)
		{
			LaneHoldEndNote = chartEvent;
		}

		public void SetHoldStartNote(EditorHoldStartNote editorHoldStartNote)
		{
			EditorHoldStartNote = editorHoldStartNote;
		}

		public EditorHoldStartNote GetHoldStartNote()
		{
			return EditorHoldStartNote;
		}

		public override int GetLane()
		{
			return LaneHoldEndNote.Lane;
		}

		public bool IsRoll()
		{
			return EditorHoldStartNote.IsRoll();
		}

		public override void Draw(TextureAtlas textureAtlas, SpriteBatch spriteBatch)
		{
			string bodyTextureId;
			string capTextureId;
			var roll = IsRoll();
			if (Active)
			{
				bodyTextureId = roll ? TextureIdRollActive : TextureIdHoldActive;
				capTextureId = roll ? TextureIdRollActiveCap : TextureIdHoldActiveCap;
			}
			else
			{
				bodyTextureId = roll ? TextureIdRollInactive : TextureIdHoldInactive;
				capTextureId = roll ? TextureIdRollInactiveCap : TextureIdHoldInactiveCap;
			}

			// TODO: Tiling?

			var capH = (int)(DefaultHoldCapHeight * Scale + 0.5);
			var bodyTileH = (int)(DefaultHoldSegmentHeight * Scale + 0.5);
			var y = (int)(Y + H + 0.5) - capH;
			var minY = (int)(Y + 0.5);
			var x = (int)(X + 0.5);
			var w = (int)(W + 0.5);
			textureAtlas.Draw(capTextureId, spriteBatch, new Rectangle(x, y, w, capH), 1.0f);

			// TODO: depth
			while (y >= minY)
			{
				var h = Math.Min(bodyTileH, y - minY);
				if (h == 0)
					break;
				y -= h;
				textureAtlas.Draw(bodyTextureId, spriteBatch, new Rectangle(x, y, w, h), 1.0f);
			}
		}
	}

}

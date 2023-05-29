using System;
using System.Collections.Generic;
using Fumen.ChartDefinition;
using Fumen.Converters;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using static System.Diagnostics.Debug;
using static StepManiaEditor.Utils;

namespace StepManiaEditor
{
	internal sealed class EditorHoldNoteEvent : EditorEvent
	{
		/// <summary>
		/// The first underlying Event of the hold: the LaneHoldStartNote.
		/// </summary>
		private readonly LaneHoldStartNote LaneHoldStartNote;
		/// <summary>
		/// The second underlying Event of the hold: the LaneHoldEndNote.
		/// </summary>
		private readonly LaneHoldEndNote LaneHoldEndNote;
		/// <summary>
		/// Whether or not this hold is a roll.
		/// </summary>
		private bool Roll;

		/// <summary>
		/// Whether or not this hold should be considered active (lit by input/autoplay) for rendering.
		/// </summary>
		private bool NextDrawActive;
		/// <summary>
		/// When the hold is active the start needs to be brought down to match the receptor.
		/// This cutoff value is used to cut off the top of the hold and bring the start down.
		/// </summary>
		private double NextDrawActiveYCutoffPoint;

		public EditorHoldNoteEvent(EventConfig config, LaneHoldStartNote startEvent, LaneHoldEndNote endEvent) : base(config)
		{
			LaneHoldStartNote = startEvent;
			LaneHoldEndNote = endEvent;
			Roll = LaneHoldStartNote.SourceType == SMCommon.NoteChars[(int)SMCommon.NoteType.RollStart].ToString();
		}

		/// <summary>
		/// Static method to create a hold.
		/// </summary>
		public static EditorHoldNoteEvent CreateHold(EditorChart chart, int lane, int row, int length, bool roll)
		{
			var holdStartTime = 0.0;
			chart.TryGetTimeFromChartPosition(row, ref holdStartTime);
			var holdStartNote = new LaneHoldStartNote()
			{
				Lane = lane,
				IntegerPosition = row,
				TimeSeconds = holdStartTime
			};
			var holdEndTime = 0.0;
			chart.TryGetTimeFromChartPosition(row + length, ref holdEndTime);
			var holdEndNote = new LaneHoldEndNote()
			{
				Lane = lane,
				IntegerPosition = row + length,
				TimeSeconds = holdEndTime
			};

			var config = EventConfig.CreateHoldConfig(chart, holdStartNote, holdEndNote);
			var hold = new EditorHoldNoteEvent(config, holdStartNote, holdEndNote);
			hold.SetIsRoll(roll);
			return hold;
		}

		public override int GetLane()
		{
			return LaneHoldStartNote.Lane;
		}

		public override void SetLane(int lane)
		{
			Assert(lane >= 0 && lane < EditorChart.NumInputs);
			LaneHoldStartNote.Lane = lane;
			LaneHoldEndNote.Lane = lane; 
		}

		public override void SetNewPosition(int row)
		{
			var len = GetLength();
			ChartPosition = row;
			SetNewPositionForEvent(LaneHoldStartNote, row);
			SetNewPositionForEvent(LaneHoldEndNote, row + len);
		}

		public void RefreshHoldEndTime()
		{
			SetNewPositionForEvent(LaneHoldEndNote, GetRow() + GetLength());
		}

		public override int GetLength()
		{
			return LaneHoldEndNote.IntegerPosition - LaneHoldStartNote.IntegerPosition;
		}

		public void SetLength(int length)
		{
			SetNewPositionForEvent(LaneHoldEndNote, GetRow() + length);
		}

		public override double GetEndChartPosition()
		{
			return LaneHoldEndNote.IntegerPosition;
		}

		public override int GetEndRow()
		{
			return LaneHoldEndNote.IntegerPosition;
		}

		public override double GetEndChartTime()
		{
			return LaneHoldEndNote.TimeSeconds;
		}

		public override List<Event> GetEvents()
		{
			return new List<Event>() { LaneHoldStartNote, LaneHoldEndNote };
		}

		public bool IsRoll()
		{
			return Roll;
		}

		public void SetIsRoll(bool roll)
		{
			Roll = roll;
			LaneHoldStartNote.SourceType = Roll ? SMCommon.NoteChars[(int)SMCommon.NoteType.RollStart].ToString() : string.Empty;
		}

		public override bool IsMiscEvent() { return false; }
		public override bool IsSelectableWithoutModifiers() { return true; }
		public override bool IsSelectableWithModifiers() { return false; }

		public void SetNextDrawActive(bool active, double y)
		{
			NextDrawActive = active;
			NextDrawActiveYCutoffPoint = y;
		}

		public override void Draw(TextureAtlas textureAtlas, SpriteBatch spriteBatch, ArrowGraphicManager arrowGraphicManager)
		{
			var alpha = IsBeingEdited() ? ActiveEditEventAlpha : Alpha;
			if (alpha <= 0.0f)
			{
				NextDrawActive = false;
				NextDrawActiveYCutoffPoint = 0.0;
				return;
			}

			var active = NextDrawActive && Preferences.Instance.PreferencesReceptors.AutoPlayLightHolds;
			var activeAndCutoff = NextDrawActive && Preferences.Instance.PreferencesReceptors.AutoPlayHideArrows;
			var selected = IsSelected();

			var (startArrowTexture, holdRot) = arrowGraphicManager.GetArrowTexture(LaneHoldStartNote.IntegerPosition, GetLane(), selected);
			var (_, startArrowHeight) = textureAtlas.GetDimensions(startArrowTexture);
			var halfArrowHeight = startArrowHeight * 0.5 * Scale;

			var roll = IsRoll();

			// The hold body texture is a tiled texture that starts at the end of the hold and ends at the arrow.
			var (holdBodyTextureId, holdBodyMirrored) = roll ?
				arrowGraphicManager.GetRollBodyTexture(LaneHoldEndNote.IntegerPosition, LaneHoldEndNote.Lane, active, selected) :
				arrowGraphicManager.GetHoldBodyTexture(LaneHoldEndNote.IntegerPosition, LaneHoldEndNote.Lane, active, selected);
			// The hold cap texture is a texture that is drawn once at the end of the hold.
			var (holdCapTextureId, holdCapRotation) = roll ?
				arrowGraphicManager.GetRollEndTexture(LaneHoldEndNote.IntegerPosition, LaneHoldEndNote.Lane, active, selected) :
				arrowGraphicManager.GetHoldEndTexture(LaneHoldEndNote.IntegerPosition, LaneHoldEndNote.Lane, active, selected);
			// The hold start texture is only used to extend the start of the hold upward into the arrow for certain
			// arrow graphics which wouldn't otherwise mask the hold start, like solo diagonals.
			var (holdBodyStartTexture, holdBodyStartMirror) = roll ?
				arrowGraphicManager.GetRollStartTexture(LaneHoldStartNote.IntegerPosition, GetLane(), NextDrawActive, selected) :
				arrowGraphicManager.GetHoldStartTexture(LaneHoldStartNote.IntegerPosition, GetLane(), NextDrawActive, selected);

			var (_, capH) = textureAtlas.GetDimensions(holdCapTextureId);
			var (bodyTexW, bodyTexH) = textureAtlas.GetDimensions(holdBodyTextureId);

			// Determine the Y value and height to use.
			// If the note is active, we should bring down the top to the cutoff point.
			var bodyY = Y + halfArrowHeight;
			var noteH = H - halfArrowHeight;
			if (activeAndCutoff)
			{
				noteH -= (NextDrawActiveYCutoffPoint - bodyY);
				bodyY = NextDrawActiveYCutoffPoint;
			}

			capH = (int)(capH * Scale + 0.5);
			var bodyTileH = (int)(bodyTexH * Scale + 0.5);
			var y = (int)(bodyY + noteH + 0.5) - capH;
			var minY = (int)(bodyY + 0.5);
			var x = (int)(X + 0.5);
			var w = (int)(W + 0.5);

			// Record the cap position for drawing later.
			var capY = y;
			var minimumCapY = bodyY;
			if (arrowGraphicManager.AreHoldCapsCentered())
			{
				y += (int)(capH * 0.5f);
				minimumCapY = Y;
			}

			// Adjust the starting y value so we don't needlessly loop when zoomed in and a large
			// area of the hold is off the screen.
			if (y > ScreenHeight + capH)
			{
				y -= ((y - (int)ScreenHeight) / capH) * capH;
			}

			// Draw the body by looping up from the bottom, ensuring that each tiled body texture aligns
			// perfectly with the previous one.
			var spriteEffects = holdBodyMirrored ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
			while (y >= minY)
			{
				var h = Math.Min(bodyTileH, y - minY);
				if (h == 0)
					break;
				y -= h;
				if (y < -bodyTileH)
					break;
				if (h < bodyTileH)
				{
					var sourceH = (int)(bodyTexH * ((double)h / bodyTileH));
					textureAtlas.Draw(holdBodyTextureId, spriteBatch, new Rectangle(0, bodyTexH - sourceH, bodyTexW, sourceH), new Rectangle(x, y, w, h), 0.0f, alpha, spriteEffects);
				}
				else
				{
					textureAtlas.Draw(holdBodyTextureId, spriteBatch, new Rectangle(x, y, w, h), 0.0f, alpha, spriteEffects);
				}
			}

			// Some arrows, like solo diagonals need a hold start graphic to fill the gap at the top of the hold
			// between the arrow midpoint and the widest part of the arrow.
			if (holdBodyStartTexture != null)
			{
				// It is assumed there is no height padding baked into this texture.
				var (_, holdBodyStartHeight) = textureAtlas.GetDimensions(holdBodyStartTexture);
				var holdBodyStartH = (int)(holdBodyStartHeight * Scale);

				textureAtlas.Draw(
					holdBodyStartTexture,
					spriteBatch,
					new Rectangle(x, minY - holdBodyStartH, w, holdBodyStartH),
					0.0f,
					alpha,
					holdBodyStartMirror ? SpriteEffects.FlipHorizontally : SpriteEffects.None);
			}

			// Draw the cap, if it is visible.
			// Also ensure that the cap is below the start. In negative scroll rate regions it may be
			// above the start, in which case we do not want to render it.
			// The cap should be drawn after the body as some caps render on top of the body.
			if (capY > -capH && capY < ScreenHeight && capY >= minimumCapY)
				textureAtlas.Draw(holdCapTextureId, spriteBatch, new Rectangle(x, capY, w, capH), holdCapRotation, alpha, SpriteEffects.None);

			// Draw the arrow at the start of the hold.
			var holdStartY = bodyY - halfArrowHeight;
			textureAtlas.Draw(
					startArrowTexture,
					spriteBatch,
					new Vector2((float)X, (float)holdStartY),
					Scale,
					holdRot,
					alpha);

			// Reset active flags.
			NextDrawActive = false;
			NextDrawActiveYCutoffPoint = 0.0;
		}
	}
}

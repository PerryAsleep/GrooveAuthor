using System;
using Fumen.ChartDefinition;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using static StepManiaEditor.Utils;

namespace StepManiaEditor
{
	public class EditorHoldEndNoteEvent : EditorEvent
	{
		private EditorHoldStartNoteEvent EditorHoldStartNoteEvent;
		private readonly LaneHoldEndNote LaneHoldEndNote;
		private static uint ScreenHeight;

		/// <summary>
		/// Whether or not this hold should be considered active for rendering.
		/// </summary>
		private bool NextDrawActive;
		private double NextDrawActiveYCutoffPoint;

		public EditorHoldEndNoteEvent(EditorChart editorChart, LaneHoldEndNote chartEvent) : base(editorChart, chartEvent)
		{
			LaneHoldEndNote = chartEvent;
		}

		public EditorHoldEndNoteEvent(EditorChart editorChart, LaneHoldEndNote chartEvent, bool isBeingEdited) : base(editorChart, chartEvent, isBeingEdited)
		{
			LaneHoldEndNote = chartEvent;
		}

		public static void SetScreenHeight(uint screenHeight)
		{
			ScreenHeight = screenHeight;
		}

		public void SetHoldStartNote(EditorHoldStartNoteEvent editorHoldStartNoteEvent)
		{
			EditorHoldStartNoteEvent = editorHoldStartNoteEvent;
		}

		public EditorHoldStartNoteEvent GetHoldStartNote()
		{
			return EditorHoldStartNoteEvent;
		}

		public override int GetLane()
		{
			return LaneHoldEndNote.Lane;
		}

		public bool IsRoll()
		{
			return EditorHoldStartNoteEvent.IsRoll();
		}

		public void SetIsRoll(bool roll)
		{
			EditorHoldStartNoteEvent.SetIsRoll(roll);
		}

		public void SetNextDrawActive(bool active, double y)
		{
			NextDrawActive = active;
			NextDrawActiveYCutoffPoint = y;
		}

		public override void Draw(TextureAtlas textureAtlas, SpriteBatch spriteBatch, ArrowGraphicManager arrowGraphicManager)
		{
			var roll = IsRoll();
			var alpha = IsBeingEdited() ? ActiveEditEventAlpha : 1.0f;

			var active = NextDrawActive && Preferences.Instance.PreferencesAnimations.AutoPlayLightHolds;
			var activeAndCutoff = NextDrawActive && Preferences.Instance.PreferencesAnimations.AutoPlayHideArrows;

			var (bodyTextureId, bodyMirrored) = roll ?
				arrowGraphicManager.GetRollBodyTexture(LaneHoldEndNote.IntegerPosition, LaneHoldEndNote.Lane, active) :
				arrowGraphicManager.GetHoldBodyTexture(LaneHoldEndNote.IntegerPosition, LaneHoldEndNote.Lane, active);
			var (capTextureId, capMirrored) = roll ?
				arrowGraphicManager.GetRollEndTexture(LaneHoldEndNote.IntegerPosition, LaneHoldEndNote.Lane, active) :
				arrowGraphicManager.GetHoldEndTexture(LaneHoldEndNote.IntegerPosition, LaneHoldEndNote.Lane, active);

			var (_, capH) = textureAtlas.GetDimensions(capTextureId);
			var (bodyTexW, bodyTexH) = textureAtlas.GetDimensions(bodyTextureId);

			// Determine the Y value and height to use.
			// If the note is active, we should bring down the top to the cutoff point.
			var noteY = GetY();
			var noteH = GetH();
			if (activeAndCutoff)
			{
				noteH -= (NextDrawActiveYCutoffPoint - noteY);
				noteY = NextDrawActiveYCutoffPoint;
			}

			capH = (int)(capH * GetScale() + 0.5);
			var bodyTileH = (int)(bodyTexH * GetScale() + 0.5);
			var y = (int)(noteY + noteH + 0.5) - capH;
			var minY = (int)(noteY + 0.5);
			var x = (int)(GetX() + 0.5);
			var w = (int)(GetW() + 0.5);

			var spriteEffects = capMirrored ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

			// Draw the cap, if it is visible.
			if (y > -capH && y < ScreenHeight)
				textureAtlas.Draw(capTextureId, spriteBatch, new Rectangle(x, y, w, capH), 0.0f, alpha, spriteEffects);

			// Adjust the starting y value so we don't needlessly loop when zoomed in and a large
			// area of the hold is off the screen.
			if (y > ScreenHeight + capH)
			{
				y -= ((y - (int)ScreenHeight) / capH) * capH;
			}

			// Draw the body.
			spriteEffects = bodyMirrored ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
			while (y >= minY)
			{
				var h = Math.Min(bodyTileH, y - minY);
				if (h == 0)
					break;
				y -= h;
				if (y < -capH)
					break;
				if (h < bodyTileH)
				{
					var sourceH = (int)(bodyTexH * ((double)h / bodyTileH));
					textureAtlas.Draw(bodyTextureId, spriteBatch, new Rectangle(0, bodyTexH - sourceH, bodyTexW, sourceH), new Rectangle(x, y, w, h), 0.0f, alpha, spriteEffects);
				}
				else
				{
					textureAtlas.Draw(bodyTextureId, spriteBatch, new Rectangle(x, y, w, h), 0.0f, alpha, spriteEffects);
				}
			}

			var (holdStartTexture, holdStartMirror) = arrowGraphicManager.GetHoldStartTexture(GetHoldStartNote().GetRow(), GetLane(), NextDrawActive);
			var holdStartY = 0.0;
			if (holdStartTexture != null || activeAndCutoff)
			{
				var (startTexture, _) = arrowGraphicManager.GetArrowTexture(GetHoldStartNote().GetRow(), GetLane());
				var (_, startHeight) = textureAtlas.GetDimensions(startTexture);
				holdStartY = noteY - (startHeight * 0.5 * GetScale());
			}

			// Some arrows, like solo diagonals need a hold start graphic to fill the gap at the top of the hold
			// between the arrow midpoint and the widest part of the arrow.
			if (holdStartTexture != null)
			{
				textureAtlas.Draw(
					holdStartTexture,
					spriteBatch,
					new Vector2((float)GetX(), (float)holdStartY),
					GetScale(),
					0.0f,
					alpha,
					holdStartMirror ? SpriteEffects.FlipHorizontally : SpriteEffects.None);
			}

			// If active, draw the hold start note on top of the receptors.
			// The actual hold start note will not draw since it is above the receptors.
			if (activeAndCutoff)
			{
				GetHoldStartNote().DrawAtY(textureAtlas, spriteBatch, arrowGraphicManager, holdStartY);
			}

			// Reset active flags.
			NextDrawActive = false;
			NextDrawActiveYCutoffPoint = 0.0;
		}
	}
}

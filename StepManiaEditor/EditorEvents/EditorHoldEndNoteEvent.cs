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
		public bool Active;

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

		public override void Draw(TextureAtlas textureAtlas, SpriteBatch spriteBatch, ArrowGraphicManager arrowGraphicManager)
		{
			var roll = IsRoll();
			var alpha = IsBeingEdited() ? ActiveEditEventAlpha : 1.0f;

			var (bodyTextureId, bodyMirrored) = roll ?
				arrowGraphicManager.GetRollBodyTexture(LaneHoldEndNote.IntegerPosition, LaneHoldEndNote.Lane, Active) :
				arrowGraphicManager.GetHoldBodyTexture(LaneHoldEndNote.IntegerPosition, LaneHoldEndNote.Lane, Active);
			var (capTextureId, capMirrored) = roll ?
				arrowGraphicManager.GetRollEndTexture(LaneHoldEndNote.IntegerPosition, LaneHoldEndNote.Lane, Active) :
				arrowGraphicManager.GetHoldEndTexture(LaneHoldEndNote.IntegerPosition, LaneHoldEndNote.Lane, Active);

			// TODO: Tiling?

			var (_, capH) = textureAtlas.GetDimensions(capTextureId);
			var (bodyTexW, bodyTexH) = textureAtlas.GetDimensions(bodyTextureId);

			capH = (int)(capH * GetScale() + 0.5);
			var bodyTileH = (int)(bodyTexH * GetScale() + 0.5);
			var y = (int)(GetY() + GetH() + 0.5) - capH;
			var minY = (int)(GetY() + 0.5);
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

			// TODO: depth
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
		}
	}
}

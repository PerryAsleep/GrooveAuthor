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

			var capH = (int)(DefaultHoldCapHeight * GetScale() + 0.5);
			var bodyTileH = (int)(DefaultHoldSegmentHeight * GetScale() + 0.5);
			var y = (int)(GetY() + GetH() + 0.5) - capH;
			var minY = (int)(GetY() + 0.5);
			var x = (int)(GetX() + 0.5);
			var w = (int)(GetW() + 0.5);
			
			// Draw the cap, if it is visible.
			if (y > -capH && y < ScreenHeight)
				textureAtlas.Draw(capTextureId, spriteBatch, new Rectangle(x, y, w, capH), 1.0f);

			// Adjust the starting y value so we don't needlessly loop when zoomed in and a large
			// area of the hold is off the screen.
			if (y > ScreenHeight + capH)
			{
				y -= ((y - (int)ScreenHeight) / capH) * capH;
			}

			// TODO: depth
			while (y >= minY)
			{
				var h = Math.Min(bodyTileH, y - minY);
				if (h == 0)
					break;
				y -= h;
				if (y < -capH)
					break;
				textureAtlas.Draw(bodyTextureId, spriteBatch, new Rectangle(x, y, w, h), 1.0f);
			}
		}
	}
}

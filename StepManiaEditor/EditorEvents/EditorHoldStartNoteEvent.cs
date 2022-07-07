using System;
using Fumen.ChartDefinition;
using Fumen.Converters;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using static StepManiaEditor.Utils;

namespace StepManiaEditor
{
	public class EditorHoldStartNoteEvent : EditorEvent
	{
		private readonly LaneHoldStartNote LaneHoldStartNote;
		private EditorHoldEndNoteEvent EditorHoldEndNoteEvent;
		private readonly bool Roll;

		public EditorHoldStartNoteEvent(EditorChart editorChart, LaneHoldStartNote chartEvent) : base(editorChart, chartEvent)
		{
			LaneHoldStartNote = chartEvent;
			Roll = LaneHoldStartNote.SourceType == SMCommon.NoteChars[(int)SMCommon.NoteType.RollStart].ToString();
		}

		public void SetHoldEndNote(EditorHoldEndNoteEvent editorHoldEndNoteEvent)
		{
			EditorHoldEndNoteEvent = editorHoldEndNoteEvent;
		}

		public EditorHoldEndNoteEvent GetHoldEndNote()
		{
			return EditorHoldEndNoteEvent;
		}

		public override int GetLane()
		{
			return LaneHoldStartNote.Lane;
		}

		public override int GetLength()
		{
			return EditorHoldEndNoteEvent.GetRow() - LaneHoldStartNote.IntegerPosition;
		}

		public bool IsRoll()
		{
			return Roll;
		}

		public override void Draw(TextureAtlas textureAtlas, SpriteBatch spriteBatch)
		{
			var rot = new[] { (float)Math.PI * 0.5f, 0.0f, (float)Math.PI, (float)Math.PI * 1.5f };

			var textureId = GetArrowTextureId(LaneHoldStartNote.IntegerPosition);

			textureAtlas.Draw(
				textureId,
				spriteBatch,
				new Vector2((float)GetX(), (float)GetY()),
				(float)GetScale(),
				rot[LaneHoldStartNote.Lane % rot.Length],
				1.0f);
		}
	}
}

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
		private bool Roll;

		public EditorHoldStartNoteEvent(EditorChart editorChart, LaneHoldStartNote chartEvent) : base(editorChart, chartEvent)
		{
			LaneHoldStartNote = chartEvent;
			Roll = LaneHoldStartNote.SourceType == SMCommon.NoteChars[(int)SMCommon.NoteType.RollStart].ToString();
		}

		public EditorHoldStartNoteEvent(EditorChart editorChart, LaneHoldStartNote chartEvent, bool isBeingEdited) : base(editorChart, chartEvent, isBeingEdited)
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

		public void SetIsRoll(bool roll)
		{
			Roll = roll;
			LaneHoldStartNote.SourceType = Roll ? SMCommon.NoteChars[(int)SMCommon.NoteType.RollStart].ToString() : string.Empty;
		}

		public override void Draw(TextureAtlas textureAtlas, SpriteBatch spriteBatch, ArrowGraphicManager arrowGraphicManager)
		{
			DrawAtY(textureAtlas, spriteBatch, arrowGraphicManager, GetY());
		}

		public void DrawAtY(TextureAtlas textureAtlas, SpriteBatch spriteBatch, ArrowGraphicManager arrowGraphicManager, double y)
		{
			var alpha = IsBeingEdited() ? ActiveEditEventAlpha : 1.0f;

			var (holdTexture, holdRot) = arrowGraphicManager.GetArrowTexture(LaneHoldStartNote.IntegerPosition, GetLane());
			textureAtlas.Draw(
				holdTexture,
				spriteBatch,
				new Vector2((float)GetX(), (float)y),
				(float)GetScale(),
				holdRot,
				alpha);
		}
	}
}

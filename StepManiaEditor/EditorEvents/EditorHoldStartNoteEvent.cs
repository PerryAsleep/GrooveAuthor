using System;
using System.Collections.Generic;
using Fumen.ChartDefinition;
using Fumen.Converters;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using static System.ComponentModel.Design.ObjectSelectorEditor;
using static StepManiaEditor.Utils;

namespace StepManiaEditor
{
	internal sealed class EditorHoldStartNoteEvent : EditorEvent
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

		public override bool DoesPointIntersect(double x, double y)
		{
			// Include the hold body when considering intersections.
			var endPoint = EditorHoldEndNoteEvent.Y + EditorHoldEndNoteEvent.H;
			return x >= X && x <= X + W && y >= Y && y <= endPoint;
		}

		public override List<EditorEvent> GetEventsSelectedTogether()
		{
			// Always select both the start and end together.
			return new List<EditorEvent>() { this, EditorHoldEndNoteEvent };
		}

		public override bool IsMiscEvent() { return false; }
		public override bool IsSelectableWithoutModifiers() { return true; }
		public override bool IsSelectableWithModifiers() { return false; }

		public override void Draw(TextureAtlas textureAtlas, SpriteBatch spriteBatch, ArrowGraphicManager arrowGraphicManager)
		{
			DrawAtY(textureAtlas, spriteBatch, arrowGraphicManager, Y);
		}

		public void DrawAtY(TextureAtlas textureAtlas, SpriteBatch spriteBatch, ArrowGraphicManager arrowGraphicManager, double y)
		{
			var alpha = IsBeingEdited() ? ActiveEditEventAlpha : 1.0f;

			var (holdTexture, holdRot) = arrowGraphicManager.GetArrowTexture(LaneHoldStartNote.IntegerPosition, GetLane(), IsSelected());
			textureAtlas.Draw(
				holdTexture,
				spriteBatch,
				new Vector2((float)X, (float)y),
				Scale,
				holdRot,
				alpha);
		}
	}
}

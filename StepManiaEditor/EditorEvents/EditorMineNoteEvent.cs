using Fumen.ChartDefinition;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using static StepManiaEditor.Utils;

namespace StepManiaEditor
{
	internal sealed class EditorMineNoteEvent : EditorEvent
	{
		private readonly LaneNote LaneNote;

		public EditorMineNoteEvent(EditorChart editorChart, LaneNote chartEvent) : base(editorChart, chartEvent)
		{
			LaneNote = chartEvent;
		}

		public EditorMineNoteEvent(EditorChart editorChart, LaneNote chartEvent, bool isBeingEdited) : base(editorChart, chartEvent, isBeingEdited)
		{
			LaneNote = chartEvent;
		}

		public override int GetLane()
		{
			return LaneNote.Lane;
		}

		public override bool IsSelectableWithoutModifiers() { return true; }
		public override bool IsSelectableWithModifiers() { return false; }

		public override void Draw(TextureAtlas textureAtlas, SpriteBatch spriteBatch, ArrowGraphicManager arrowGraphicManager)
		{
			var alpha = IsBeingEdited() ? ActiveEditEventAlpha : 1.0f;
			textureAtlas.Draw(
				ArrowGraphicManager.GetMineTexture(GetRow(), LaneNote.Lane, IsSelected()),
				spriteBatch,
				new Vector2((float)X, (float)Y),
				Scale,
				0.0f,
				alpha);
		}
	}
}

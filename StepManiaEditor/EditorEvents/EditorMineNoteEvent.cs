using Fumen.ChartDefinition;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using static StepManiaEditor.Utils;

namespace StepManiaEditor
{
	internal sealed class EditorMineNoteEvent : EditorEvent
	{
		private readonly LaneNote LaneNote;

		public EditorMineNoteEvent(EventConfig config, LaneNote chartEvent) : base(config)
		{
			LaneNote = chartEvent;
		}

		public override int GetLane()
		{
			return LaneNote.Lane;
		}

		public override bool IsMiscEvent() { return false; }
		public override bool IsSelectableWithoutModifiers() { return true; }
		public override bool IsSelectableWithModifiers() { return false; }

		public override void Draw(TextureAtlas textureAtlas, SpriteBatch spriteBatch, ArrowGraphicManager arrowGraphicManager)
		{
			var alpha = IsBeingEdited() ? ActiveEditEventAlpha : Alpha;
			if (alpha <= 0.0f)
				return;
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

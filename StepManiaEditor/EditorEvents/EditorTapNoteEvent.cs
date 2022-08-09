using System;
using Fumen.ChartDefinition;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using static StepManiaEditor.Utils;

namespace StepManiaEditor
{
	public class EditorTapNoteEvent : EditorEvent
	{
		private readonly LaneTapNote LaneTapNote;

		public EditorTapNoteEvent(EditorChart editorChart, LaneTapNote chartEvent) : base(editorChart, chartEvent)
		{
			LaneTapNote = chartEvent;
		}

		public EditorTapNoteEvent(EditorChart editorChart, LaneTapNote chartEvent, bool isBeingEdited) : base(editorChart, chartEvent, isBeingEdited)
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

			var textureId = GetArrowTextureId(LaneTapNote.IntegerPosition);
			var alpha = IsBeingEdited() ? ActiveEditEventAlpha : 1.0f;

			textureAtlas.Draw(
				textureId,
				spriteBatch,
				new Vector2((float)GetX(), (float)GetY()),
				(float)GetScale(),
				rot[LaneTapNote.Lane % rot.Length],
				alpha);
		}
	}
}

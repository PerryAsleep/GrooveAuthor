using Fumen.ChartDefinition;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGameExtensions;
using static StepManiaEditor.Utils;

namespace StepManiaEditor;

internal sealed class EditorTapNoteEvent : EditorEvent
{
	private readonly LaneTapNote LaneTapNote;

	public EditorTapNoteEvent(EventConfig config, LaneTapNote chartEvent) : base(config)
	{
		LaneTapNote = chartEvent;
	}

	public override int GetLane()
	{
		return LaneTapNote.Lane;
	}

	public override bool IsMiscEvent()
	{
		return false;
	}

	public override bool IsSelectableWithoutModifiers()
	{
		return true;
	}

	public override bool IsSelectableWithModifiers()
	{
		return false;
	}

	public override void Draw(TextureAtlas textureAtlas, SpriteBatch spriteBatch, ArrowGraphicManager arrowGraphicManager)
	{
		var alpha = IsBeingEdited() ? ActiveEditEventAlpha : Alpha;
		if (alpha <= 0.0f)
			return;
		var (textureId, rot) = arrowGraphicManager.GetArrowTexture(LaneTapNote.IntegerPosition, LaneTapNote.Lane, IsSelected());
		textureAtlas.Draw(
			textureId,
			spriteBatch,
			new Vector2((float)X, (float)Y),
			Scale,
			rot,
			alpha);
	}
}

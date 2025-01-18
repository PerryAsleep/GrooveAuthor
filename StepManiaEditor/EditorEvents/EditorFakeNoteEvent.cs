using Fumen.ChartDefinition;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGameExtensions;

namespace StepManiaEditor;

internal sealed class EditorFakeNoteEvent : EditorEvent
{
	private readonly LaneTapNote LaneTapNote;

	public EditorFakeNoteEvent(EventConfig config, LaneTapNote chartEvent) : base(config)
	{
		LaneTapNote = chartEvent;
	}

	public override string GetShortTypeName()
	{
		return "Fake";
	}

	public override int GetLane()
	{
		return LaneTapNote.Lane;
	}

	public override bool IsStep()
	{
		return true;
	}

	public override bool IsLaneNote()
	{
		return true;
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

	public override bool IsFake()
	{
		return true;
	}

	public override void Draw(TextureAtlas textureAtlas, SpriteBatch spriteBatch, ArrowGraphicManager arrowGraphicManager)
	{
		var alpha = GetRenderAlpha();
		if (alpha <= 0.0f)
			return;

		DrawTap(textureAtlas, spriteBatch, arrowGraphicManager);
		DrawFakeMarker(textureAtlas, spriteBatch, arrowGraphicManager);
	}
}

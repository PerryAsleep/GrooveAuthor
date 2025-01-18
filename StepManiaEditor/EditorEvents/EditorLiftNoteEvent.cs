using Fumen.ChartDefinition;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGameExtensions;

namespace StepManiaEditor;

internal sealed class EditorLiftNoteEvent : EditorEvent
{
	private readonly LaneTapNote LaneTapNote;

	public EditorLiftNoteEvent(EventConfig config, LaneTapNote chartEvent) : base(config)
	{
		LaneTapNote = chartEvent;
	}

	public override string GetShortTypeName()
	{
		return "Lift";
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

	public override bool IsConsumedByReceptors()
	{
		return !IsFake();
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
		var alpha = GetRenderAlpha();
		if (alpha <= 0.0f)
			return;

		// Draw the arrow.
		DrawTap(textureAtlas, spriteBatch, arrowGraphicManager);

		// Draw the lift marker. Do not draw it with the selection overlay as it looks weird.
		// Don't draw it if we are going to draw the fake marker as they occupy the same space and
		// relaying fake information is more important than relaying lift information.
		if (!IsFake())
		{
			var liftTextureId = ArrowGraphicManager.GetLiftMarkerTexture(LaneTapNote.IntegerPosition, GetLane(), false);
			var (textureId, _) = arrowGraphicManager.GetArrowTexture(GetRow(), GetLane(), IsSelected());
			var (arrowW, arrowH) = textureAtlas.GetDimensions(textureId);
			var (markerW, markerH) = textureAtlas.GetDimensions(liftTextureId);
			var markerX = X + (arrowW - markerW) * 0.5 * Scale;
			var markerY = Y + (arrowH - markerH) * 0.5 * Scale;
			textureAtlas.Draw(
				liftTextureId,
				spriteBatch,
				new Vector2((float)markerX, (float)markerY),
				Scale,
				0.0f,
				alpha);
		}
		else
		{
			DrawFakeMarker(textureAtlas, spriteBatch, arrowGraphicManager);
		}
	}
}

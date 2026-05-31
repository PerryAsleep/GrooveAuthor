using System;
using System.Drawing;
using Fumen.ChartDefinition;
using Fumen.Converters;
using Microsoft.Xna.Framework.Graphics;
using MonoGameExtensions;
using static System.Diagnostics.Debug;
using Color = Microsoft.Xna.Framework.Color;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

namespace StepManiaEditor;

internal sealed class EditorHoldNoteEvent : EditorEvent
{
	/// <summary>
	/// The first underlying Event of the hold: the LaneHoldStartNote.
	/// </summary>
	private readonly LaneHoldStartNote LaneHoldStartNote;

	/// <summary>
	/// The second underlying Event of the hold: the LaneHoldEndNote.
	/// </summary>
	private readonly LaneHoldEndNote LaneHoldEndNote;

	/// <summary>
	/// Whether or not this hold is a roll.
	/// </summary>
	private bool Roll;

	/// <summary>
	/// Whether or not this hold should be considered active (lit by input/autoplay) for rendering.
	/// </summary>
	private bool NextDrawActive;

	/// <summary>
	/// When the hold is active the start needs to be brought down to match the receptor.
	/// This cutoff value is used to cut off the top of the hold and bring the start down.
	/// </summary>
	private double NextDrawActiveYCutoffPoint;

	/// <summary>
	/// The screen space height of the hold's head arrow, recorded when the hold's dimensions are set.
	/// The head arrow always extends from Y in the positive y direction regardless of scroll direction,
	/// so this is used to keep the head within the hold's selectable bounds when reversed (where the
	/// body extends in the negative y direction and H is negative).
	/// </summary>
	private double HoldStartHeight;

	public EditorHoldNoteEvent(EventConfig config, LaneHoldStartNote startEvent, LaneHoldEndNote endEvent) : base(config)
	{
		LaneHoldStartNote = startEvent;
		LaneHoldEndNote = endEvent;
		Roll = LaneHoldStartNote.SourceType == SMCommon.NoteStrings[(int)SMCommon.NoteType.RollStart];
	}

	public override string GetShortTypeName()
	{
		return IsRoll() ? "Roll" : "Hold";
	}

	public override int GetLane()
	{
		return LaneHoldStartNote.Lane;
	}

	public override void SetLane(int lane)
	{
		Assert(lane >= 0 && lane < EditorChart.NumInputs);
		LaneHoldStartNote.Lane = lane;
		LaneHoldEndNote.Lane = lane;
	}

	protected override void RefreshTimeBasedOnRowImplementation(EditorRateAlteringEvent activeRateAlteringEvent)
	{
		base.RefreshTimeBasedOnRowImplementation(activeRateAlteringEvent);
		RefreshHoldEndTime();
	}

	private void SetNewHoldEndPosition(int row)
	{
		LaneHoldEndNote.IntegerPosition = row;
		var chartTime = 0.0;
		EditorChart.TryGetTimeOfEvent(LaneHoldEndNote, ref chartTime);
		LaneHoldEndNote.TimeSeconds = chartTime;
	}

	public override void SetRow(int row)
	{
		var len = GetRowDuration();
		base.SetRow(row);
		SetNewHoldEndPosition(row + len);
	}

	/// <summary>
	/// Sets the player index associated with this event.
	/// </summary>
	/// <param name="player">Player index to set.</param>
	/// <remarks>
	/// Set this carefully. This changes how events are sorted.
	/// This cannot be changed while this event is in a sorted list without resorting.
	/// </remarks>
	public override void SetPlayer(int player)
	{
		LaneHoldStartNote.Player = player;
		LaneHoldEndNote.Player = player;
	}

	public void RefreshHoldEndTime()
	{
		SetNewHoldEndPosition(GetRow() + GetRowDuration());
	}

	public void SetRowDuration(int length)
	{
		SetNewHoldEndPosition(GetRow() + length);
	}

	public override double GetEndChartPosition()
	{
		return LaneHoldEndNote.IntegerPosition;
	}

	public override int GetEndRow()
	{
		return LaneHoldEndNote.IntegerPosition;
	}

	public override double GetEndChartTime()
	{
		return LaneHoldEndNote.TimeSeconds;
	}

	public override Event GetAdditionalEvent()
	{
		return GetHoldEndEvent();
	}

	public LaneHoldEndNote GetHoldEndEvent()
	{
		return LaneHoldEndNote;
	}

	public bool IsRoll()
	{
		return Roll;
	}

	public void SetIsRoll(bool roll)
	{
		if (roll == Roll)
			return;
		Roll = roll;
		LaneHoldStartNote.SourceType = Roll ? SMCommon.NoteStrings[(int)SMCommon.NoteType.RollStart] : string.Empty;
		LaneHoldStartNote.DestType = LaneHoldStartNote.SourceType;
		EditorChart.OnHoldTypeChanged(this);
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

	public void SetNextDrawActive(bool active, double y)
	{
		NextDrawActive = active;
		NextDrawActiveYCutoffPoint = y;
	}

	public void SetHoldStartHeight(double height)
	{
		HoldStartHeight = height;
	}

	public override bool DoesPointIntersect(double x, double y)
	{
		var left = Math.Min(X, X + W);
		var right = Math.Max(X, X + W);
		// The body occupies [Y, Y + H] (H is negative when reversed) and the head arrow occupies
		// [Y, Y + HoldStartHeight]. Use the union of the two so the head remains selectable in both
		// scroll directions.
		var top = Math.Min(Y + H, Y);
		var bottom = Math.Max(Y + H, Y + HoldStartHeight);
		return x >= left && x <= right && y >= top && y <= bottom;
	}

	public override bool DoesSelectionIntersect(double x, double y, double w, double h)
	{
		var left = Math.Min(X, X + W);
		var right = Math.Max(X, X + W);
		// The body occupies [Y, Y + H] (H is negative when reversed) and the head arrow occupies
		// [Y, Y + HoldStartHeight]. Use the union of the two so the head remains selectable in both
		// scroll directions.
		var top = Math.Min(Y + H, Y);
		var bottom = Math.Max(Y + H, Y + HoldStartHeight);
		return left < x + w && right > x && top < y + h && bottom > y;
	}

	private readonly struct HoldRenderState
	{
		private readonly TextureAtlas TextureAtlas;
		private readonly SpriteBatch SpriteBatch;

		private readonly float Alpha;
		private readonly double Scale;

		public readonly string StartArrowRimTextureId;

		private readonly string BodyFillTextureId;
		private readonly bool BodyFillMirrored;
		private readonly Color BodyColor;
		private readonly string BodyRimTextureId;
		private readonly bool BodyRimMirrored;

		public readonly double StartHeight;
		private readonly string StartFillTextureId;
		private readonly bool StartFillMirrored;
		private readonly Color StartColor;
		private readonly string StartRimTextureId;
		private readonly bool StartRimMirrored;

		private readonly string EndFillTextureId;
		private readonly float EndFillRotation;
		private readonly Color EndColor;
		public readonly string EndRimTextureId;
		private readonly float EndRimRotation;

		private readonly int BodyTextureWidth;
		private readonly int BodyTextureHeight;

		private readonly bool Reverse;
		private readonly bool FlipEndCapsInReverse;

		public HoldRenderState(
			TextureAtlas textureAtlas,
			SpriteBatch spriteBatch,
			ArrowGraphicManager arrowGraphicManager,
			EditorHoldNoteEvent holdNoteEvent,
			bool active,
			bool startActive,
			float alpha,
			double scale)
		{
			TextureAtlas = textureAtlas;
			SpriteBatch = spriteBatch;
			Alpha = alpha;
			Scale = scale;
			Reverse = Preferences.Instance.PreferencesScroll.Reverse;
			FlipEndCapsInReverse = arrowGraphicManager.ShouldFlipHoldEndCapsInReverse();

			var selected = holdNoteEvent.IsSelected();
			var row = holdNoteEvent.GetRow();
			var lane = holdNoteEvent.GetLane();
			var player = holdNoteEvent.GetPlayer();
			var roll = holdNoteEvent.IsRoll();

			(StartArrowRimTextureId, _) = arrowGraphicManager.GetArrowTextureRim(lane, selected);

			if (roll)
			{
				(StartFillTextureId, StartFillMirrored, StartColor) =
					arrowGraphicManager.GetRollStartTextureFill(row, lane, startActive, selected, player);
				(BodyFillTextureId, BodyFillMirrored, BodyColor) =
					arrowGraphicManager.GetRollBodyTextureFill(row, lane, active, selected, player);
				(EndFillTextureId, EndFillRotation, EndColor) =
					arrowGraphicManager.GetRollEndTextureFill(row, lane, active, selected, player);
			}
			else
			{
				(StartFillTextureId, StartFillMirrored, StartColor) =
					arrowGraphicManager.GetHoldStartTextureFill(row, lane, startActive, selected, player);
				(BodyFillTextureId, BodyFillMirrored, BodyColor) =
					arrowGraphicManager.GetHoldBodyTextureFill(row, lane, active, selected, player);
				(EndFillTextureId, EndFillRotation, EndColor) =
					arrowGraphicManager.GetHoldEndTextureFill(row, lane, active, selected, player);
			}

			(StartRimTextureId, StartRimMirrored) =
				arrowGraphicManager.GetHoldStartTextureRim(lane, selected);
			(BodyRimTextureId, BodyRimMirrored) =
				arrowGraphicManager.GetHoldBodyTextureRim(lane, selected);
			(EndRimTextureId, EndRimRotation) =
				arrowGraphicManager.GetHoldEndTextureRim(lane, selected);

			(BodyTextureWidth, BodyTextureHeight) = textureAtlas.GetDimensions(BodyRimTextureId);

			// Measure the start graphic height.
			if (!string.IsNullOrEmpty(StartFillTextureId) && !string.IsNullOrEmpty(StartRimTextureId))
			{
				var (_, startRimHeight) = textureAtlas.GetDimensions(StartRimTextureId);
				StartHeight = startRimHeight * Scale;
			}
			else
			{
				StartHeight = 0.0;
			}
		}

		/// <summary>
		/// Returns the top y in screen space at which to draw a rectangle whose unreflected top is the
		/// given y and whose height is the given h. When reversed the rectangle is reflected about the
		/// given pivot (the head center) so the hold renders extending upward from the head.
		/// </summary>
		private double ReflectTopIfNeeded(double y, double h, double pivotY)
		{
			return Reverse ? 2.0 * pivotY - (y + h) : y;
		}

		/// <summary>
		/// Adds a vertical flip to the given sprite effects when reversed so body and cap sprites are
		/// mirrored to point the correct way.
		/// </summary>
		private SpriteEffects ApplyFlip(SpriteEffects effects)
		{
			return Reverse ? effects | SpriteEffects.FlipVertically : effects;
		}

		/// <summary>
		/// Adds a vertical flip to the end cap sprite effects when reversed, unless the current style
		/// should not flip its end caps.
		/// </summary>
		private SpriteEffects ApplyEndCapFlip(SpriteEffects effects)
		{
			return Reverse && FlipEndCapsInReverse ? effects | SpriteEffects.FlipVertically : effects;
		}

		public void DrawStart(double x, double y, double w, double pivotY)
		{
			if (string.IsNullOrEmpty(StartFillTextureId) || string.IsNullOrEmpty(StartRimTextureId))
				return;

			// It is assumed there is no height padding baked into this texture.
			var (_, holdBodyStartHeight) = TextureAtlas.GetDimensions(StartRimTextureId);
			var holdBodyStartH = holdBodyStartHeight * Scale;
			var top = ReflectTopIfNeeded(y - holdBodyStartH, holdBodyStartH, pivotY);

			// Draw fill.
			TextureAtlas.Draw(
				StartFillTextureId,
				SpriteBatch,
				new RectangleF((float)x, (float)top, (float)w, (float)holdBodyStartH),
				0.0f,
				new Color(StartColor.R, StartColor.G, StartColor.B, (byte)(StartColor.A * Alpha)),
				ApplyFlip(StartFillMirrored ? SpriteEffects.FlipHorizontally : SpriteEffects.None));

			// Draw rim.
			TextureAtlas.Draw(
				StartRimTextureId,
				SpriteBatch,
				new RectangleF((float)x, (float)top, (float)w, (float)holdBodyStartH),
				0.0f,
				Alpha,
				ApplyFlip(StartRimMirrored ? SpriteEffects.FlipHorizontally : SpriteEffects.None));
		}

		public void DrawBody(double x, double y, double w, double minY, double pivotY)
		{
			var bodyTileH = BodyTextureHeight * Scale;
			var fillSpriteEffects = ApplyFlip(BodyFillMirrored ? SpriteEffects.FlipHorizontally : SpriteEffects.None);
			var rimSpriteEffects = ApplyFlip(BodyRimMirrored ? SpriteEffects.FlipHorizontally : SpriteEffects.None);
			var fillColor = new Color(BodyColor.R, BodyColor.G, BodyColor.B, (byte)(BodyColor.A * Alpha));

			var screenBottom = Reverse ? 2.0 * pivotY : ScreenHeight;
			var screenTop = Reverse ? 2.0 * pivotY - ScreenHeight : 0.0;

			// Adjust the starting y value so we don't needlessly loop when zoomed in and a large
			// area of the hold is off the screen.
			if (y > screenBottom + bodyTileH)
			{
				y -= (int)((y - (screenBottom + bodyTileH)) / bodyTileH) * bodyTileH;
			}

			// Draw the body by looping up from the bottom, ensuring that each tiled body texture aligns
			// perfectly with the previous one. We cannot use texture wrapping here because the image
			// is a sub-texture and wrapping only works on entire textures.
			while (y >= minY)
			{
				var h = Math.Min(bodyTileH, y - minY);
				if (h == 0)
					break;
				y -= h;
				if (y < screenTop - bodyTileH)
					break;
				if (h < bodyTileH)
				{
					var sourceH = (int)(BodyTextureHeight * (h / bodyTileH));
					var sourceRect = new Rectangle(0, BodyTextureHeight - sourceH, BodyTextureWidth, sourceH);
					var destRect = new RectangleF((float)x, (float)ReflectTopIfNeeded(y, h, pivotY), (float)w, (float)h);

					// Draw fill.
					TextureAtlas.Draw(
						BodyFillTextureId,
						SpriteBatch,
						sourceRect,
						destRect,
						0.0f,
						fillColor,
						fillSpriteEffects);

					// Draw rim.
					TextureAtlas.Draw(
						BodyRimTextureId,
						SpriteBatch,
						sourceRect,
						destRect,
						0.0f,
						Alpha,
						rimSpriteEffects);
				}
				else
				{
					var destRect = new RectangleF((float)x, (float)ReflectTopIfNeeded(y, h, pivotY), (float)w, (float)h);

					// Draw fill.
					TextureAtlas.Draw(
						BodyFillTextureId,
						SpriteBatch,
						destRect,
						0.0f,
						fillColor,
						fillSpriteEffects);

					// Draw rim.
					TextureAtlas.Draw(
						BodyRimTextureId,
						SpriteBatch,
						destRect,
						0.0f,
						Alpha,
						rimSpriteEffects);
				}
			}
		}

		public void DrawEnd(double x, double y, double w, double h, double pivotY)
		{
			var destination = new RectangleF((float)x, (float)ReflectTopIfNeeded(y, h, pivotY), (float)w, (float)h);

			// Draw fill.
			TextureAtlas.Draw(
				EndFillTextureId,
				SpriteBatch,
				destination,
				EndFillRotation,
				new Color(EndColor.R, EndColor.G, EndColor.B, (byte)(EndColor.A * Alpha)),
				ApplyEndCapFlip(SpriteEffects.None));

			// Draw rim.
			TextureAtlas.Draw(
				EndRimTextureId,
				SpriteBatch,
				destination,
				EndRimRotation,
				Alpha,
				ApplyEndCapFlip(SpriteEffects.None));
		}
	}

	public override void Draw(TextureAtlas textureAtlas, SpriteBatch spriteBatch, ArrowGraphicManager arrowGraphicManager)
	{
		var alpha = GetRenderAlpha();
		if (alpha <= 0.0f)
		{
			NextDrawActive = false;
			NextDrawActiveYCutoffPoint = 0.0;
			return;
		}

		var active = NextDrawActive && Preferences.Instance.PreferencesReceptors.AutoPlayLightHolds;
		var activeAndCutoff = NextDrawActive && Preferences.Instance.PreferencesReceptors.AutoPlayHideArrows;

		var state = new HoldRenderState(textureAtlas, spriteBatch, arrowGraphicManager, this, active, NextDrawActive, alpha,
			Scale);
		var (_, startArrowHeight) = textureAtlas.GetDimensions(state.StartArrowRimTextureId);
		var halfArrowHeight = startArrowHeight * 0.5 * Scale;
		var (_, capTextureH) = textureAtlas.GetDimensions(state.EndRimTextureId);

		var reverse = Preferences.Instance.PreferencesScroll.Reverse;
		// Point for flipping when in Reverse.
		var pivotY = Y + halfArrowHeight;

		// If the note is active, we should bring the head to the cutoff point.
		var bodyY = Y + halfArrowHeight;
		var noteH = reverse ? -H + halfArrowHeight : H - halfArrowHeight;

		if (activeAndCutoff)
		{
			var newBodyY = reverse ? 2.0 * pivotY - NextDrawActiveYCutoffPoint : NextDrawActiveYCutoffPoint;
			noteH -= newBodyY - bodyY;
			bodyY = newBodyY;
		}

		var capH = capTextureH * Scale;
		var y = bodyY + noteH - capH;
		var minY = bodyY;

		// Record the cap position for drawing later.
		// Round down on the minimumCapY to avoid rounding errors for 0 length
		// holds, which are more common than negative holds.
		var capY = y;
		var minimumCapY = bodyY;
		if (arrowGraphicManager.AreHoldCapsCentered())
		{
			y += capH * 0.5f;
			minimumCapY = Y;
		}

		// In Reverse, arrows are not flipped, so for an arrow that uses a hold start graphic (e.g. solo
		// diagonals) the widest part of the arrow is on the side nearest the hold body. Rather than
		// extending the body with the start graphic to fill the gap as we do when not reversed, cut
		// off the start of the body by the start graphic's height so the body begins under the widest
		// part of the arrow and the arrow hides the body's starting edge. The start graphic itself is
		// not drawn in this case. The body occupies [minY, y] in unreflected space; if it is shorter
		// than the cutoff we don't draw the body, but the arrow and end cap are still drawn. We always
		// draw the end cap so the hold is indicated, but in rare edge cases when we need to clamp, it
		// can cut into the center of the arrow. We would ideally push a scissor rect here to fix that
		// but MonoGame only supports clip rects at the entire sprite batch level. This edge case is so
		// rare (solo + reverse + extremely short hold) that it isn't worth the effort to improve this.
		var drawStart = true;
		if (reverse && state.StartHeight > 0.0)
		{
			drawStart = false;
			minY = Math.Min(y, minY + state.StartHeight);
		}

		// Draw the body.
		state.DrawBody(X, y, W, minY, pivotY);

		// Some arrows, like solo diagonals need a hold start graphic to fill the gap at the top of the hold
		// between the arrow midpoint and the widest part of the arrow. When reversed we cut off the body
		// start instead (see above) and do not draw the start graphic.
		if (drawStart)
			state.DrawStart(X, minY, W, pivotY);

		// Draw the cap, if it is visible.
		// Also ensure that the cap is below the start. In negative scroll rate regions it may be
		// above the start, in which case we do not want to render it.
		// The cap should be drawn after the body as some caps render on top of the body.
		// The visible range is expressed in unreflected space, which is shifted when reversed.
		var normalScreenTop = reverse ? 2.0 * pivotY - ScreenHeight : 0.0;
		var normalScreenBottom = reverse ? 2.0 * pivotY : ScreenHeight;
		if (capY > normalScreenTop - capH && capY < normalScreenBottom && capY >= minimumCapY)
		{
			state.DrawEnd(X, capY, W, capH, pivotY);
		}

		// Draw the arrow at the start of the hold. The head arrow is not reflected by the draw helpers,
		// so when reversed compute its real screen position by reflecting bodyY about the pivot.
		var holdStartY = (reverse ? 2.0 * pivotY - bodyY : bodyY) - halfArrowHeight;
		DrawTap(textureAtlas, spriteBatch, arrowGraphicManager, X, holdStartY);

		// Draw the fake marker if this note is a fake.
		if (IsFake())
			DrawFakeMarker(textureAtlas, spriteBatch, state.StartArrowRimTextureId, X, holdStartY);

		// Reset active flags.
		NextDrawActive = false;
		NextDrawActiveYCutoffPoint = 0.0;
	}

	public bool Matches(EditorHoldNoteEvent other)
	{
		return base.Matches(other)
		       && LaneHoldStartNote.Matches(other.LaneHoldStartNote)
		       && LaneHoldEndNote.Matches(other.LaneHoldEndNote)
		       && Roll == other.Roll;
	}

	public override bool Matches(EditorEvent other)
	{
		if (other.GetType() != GetType())
			return false;
		return Matches((EditorHoldNoteEvent)other);
	}
}

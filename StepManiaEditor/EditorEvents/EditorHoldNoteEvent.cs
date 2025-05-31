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
		}

		public void DrawStart(double x, double y, double w)
		{
			if (string.IsNullOrEmpty(StartFillTextureId) || string.IsNullOrEmpty(StartRimTextureId))
				return;

			// It is assumed there is no height padding baked into this texture.
			var (_, holdBodyStartHeight) = TextureAtlas.GetDimensions(StartRimTextureId);
			var holdBodyStartH = holdBodyStartHeight * Scale;

			// Draw fill.
			TextureAtlas.Draw(
				StartFillTextureId,
				SpriteBatch,
				new RectangleF((float)x, (float)(y - holdBodyStartH), (float)w, (float)holdBodyStartH),
				0.0f,
				new Color(StartColor.R, StartColor.G, StartColor.B, (byte)(StartColor.A * Alpha)),
				StartFillMirrored ? SpriteEffects.FlipHorizontally : SpriteEffects.None);

			// Draw rim.
			TextureAtlas.Draw(
				StartRimTextureId,
				SpriteBatch,
				new RectangleF((float)x, (float)(y - holdBodyStartH), (float)w, (float)holdBodyStartH),
				0.0f,
				Alpha,
				StartRimMirrored ? SpriteEffects.FlipHorizontally : SpriteEffects.None);
		}

		public void DrawBody(double x, double y, double w, double minY)
		{
			var bodyTileH = BodyTextureHeight * Scale;
			var fillSpriteEffects = BodyFillMirrored ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
			var rimSpriteEffects = BodyRimMirrored ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
			var fillColor = new Color(BodyColor.R, BodyColor.G, BodyColor.B, (byte)(BodyColor.A * Alpha));

			// Adjust the starting y value so we don't needlessly loop when zoomed in and a large
			// area of the hold is off the screen.
			if (y > ScreenHeight + bodyTileH)
			{
				y -= (int)((y - (ScreenHeight + bodyTileH)) / bodyTileH) * bodyTileH;
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
				if (y < -bodyTileH)
					break;
				if (h < bodyTileH)
				{
					var sourceH = (int)(BodyTextureHeight * (h / bodyTileH));
					var sourceRect = new Rectangle(0, BodyTextureHeight - sourceH, BodyTextureWidth, sourceH);
					var destRect = new RectangleF((float)x, (float)y, (float)w, (float)h);

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
					var destRect = new RectangleF((float)x, (float)y, (float)w, (float)h);

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

		public void DrawEnd(double x, double y, double w, double h)
		{
			var destination = new RectangleF((float)x, (float)y, (float)w, (float)h);

			// Draw fill.
			TextureAtlas.Draw(
				EndFillTextureId,
				SpriteBatch,
				destination,
				EndFillRotation,
				new Color(EndColor.R, EndColor.G, EndColor.B, (byte)(EndColor.A * Alpha)),
				SpriteEffects.None);

			// Draw rim.
			TextureAtlas.Draw(
				EndRimTextureId,
				SpriteBatch,
				destination,
				EndRimRotation,
				Alpha,
				SpriteEffects.None);
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

		// Determine the Y value and height to use.
		// If the note is active, we should bring down the top to the cutoff point.
		var bodyY = Y + halfArrowHeight;
		var noteH = H - halfArrowHeight;
		if (activeAndCutoff)
		{
			noteH -= NextDrawActiveYCutoffPoint - bodyY;
			bodyY = NextDrawActiveYCutoffPoint;
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

		// Draw the body.
		state.DrawBody(X, y, W, minY);

		// Some arrows, like solo diagonals need a hold start graphic to fill the gap at the top of the hold
		// between the arrow midpoint and the widest part of the arrow.
		state.DrawStart(X, minY, W);

		// Draw the cap, if it is visible.
		// Also ensure that the cap is below the start. In negative scroll rate regions it may be
		// above the start, in which case we do not want to render it.
		// The cap should be drawn after the body as some caps render on top of the body.
		if (capY > -capH && capY < ScreenHeight && capY >= minimumCapY)
		{
			state.DrawEnd(X, capY, W, capH);
		}

		// Draw the arrow at the start of the hold.
		var holdStartY = bodyY - halfArrowHeight;
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

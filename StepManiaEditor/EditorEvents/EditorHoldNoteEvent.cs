using System;
using Fumen.ChartDefinition;
using Fumen.Converters;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGameExtensions;
using static System.Diagnostics.Debug;

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

	private struct HoldRenderState
	{
		private readonly TextureAtlas TextureAtlas;
		private readonly SpriteBatch SpriteBatch;
		private readonly ArrowGraphicManager ArrowGraphicManager;

		public bool Multiplayer;
		public float Alpha;
		public double Scale;

		public string StartArrowTextureId;
		public float StartArrowRotation;

		public string BodyTextureId;
		public bool BodyMirrored;
		public string CapTextureId;
		public float CapRotation;
		public string StartTextureId;
		public bool StartMirrored;

		public string BodyFillTextureId;
		public bool BodyFillMirrored;
		public Color BodyColor;
		public string BodyRimTextureId;
		public bool BodyRimMirrored;

		public string StartFillTextureId;
		public bool StartFillMirrored;
		public Color StartColor;
		public string StartRimTextureId;
		public float StartRimRotation;

		public string EndFillTextureId;
		public float EndFillRotation;
		public Color EndColor;
		public string EndRimTextureId;
		public float EndRimRotation;

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
			ArrowGraphicManager = arrowGraphicManager;
			Multiplayer = holdNoteEvent.EditorChart.IsMultiPlayer();
			Alpha = alpha;
			Scale = scale;

			var selected = holdNoteEvent.IsSelected();
			var row = holdNoteEvent.GetRow();
			var lane = holdNoteEvent.GetLane();
			var player = holdNoteEvent.GetPlayer();
			var roll = holdNoteEvent.IsRoll();
			var startRowForColoring = holdNoteEvent.GetStepColorRow();

			(StartArrowTextureId, StartArrowRotation) =
				ArrowGraphicManager.GetArrowTexture(startRowForColoring, lane, selected);

			if (roll)
			{
				(BodyTextureId, BodyMirrored) =
					ArrowGraphicManager.GetRollBodyTexture(row, lane, active, selected);
				(CapTextureId, CapRotation) =
					ArrowGraphicManager.GetRollEndTexture(row, lane, active, selected);
				(StartTextureId, StartMirrored) =
					ArrowGraphicManager.GetRollStartTexture(startRowForColoring, lane, startActive, selected);
				if (Multiplayer)
				{
					(BodyFillTextureId, BodyFillMirrored, BodyColor) =
						ArrowGraphicManager.GetPlayerRollBodyTextureFill(row, lane, active, selected, player);
					(BodyRimTextureId, BodyRimMirrored) =
						ArrowGraphicManager.GetPlayerRollBodyTextureRim(lane, selected);
					(StartFillTextureId, StartFillMirrored, StartColor) =
						ArrowGraphicManager.GetPlayerRollStartTextureFill(row, lane, startActive, selected, player);
					(StartRimTextureId, StartRimRotation) =
						ArrowGraphicManager.GetPlayerRollStartTextureRim(lane, selected);
					(EndFillTextureId, EndFillRotation, EndColor) =
						ArrowGraphicManager.GetPlayerRollEndTextureFill(row, lane, active, selected, player);
					(EndRimTextureId, EndRimRotation) =
						ArrowGraphicManager.GetPlayerRollEndTextureRim(lane, selected);
				}
			}
			else
			{
				(BodyTextureId, BodyMirrored) =
					ArrowGraphicManager.GetHoldBodyTexture(row, lane, active, selected);
				(CapTextureId, CapRotation) =
					ArrowGraphicManager.GetHoldEndTexture(row, lane, active, selected);
				(StartTextureId, StartMirrored) = 
					ArrowGraphicManager.GetHoldStartTexture(startRowForColoring, lane, startActive, selected);
				if (Multiplayer)
				{
					(BodyFillTextureId, BodyFillMirrored, BodyColor) =
						ArrowGraphicManager.GetPlayerHoldBodyTextureFill(row, lane, active, selected, player);
					(BodyRimTextureId, BodyRimMirrored) =
						ArrowGraphicManager.GetPlayerHoldBodyTextureRim(lane, selected);
					(StartFillTextureId, StartFillMirrored, StartColor) =
						ArrowGraphicManager.GetPlayerHoldStartTextureFill(row, lane, startActive, selected, player);
					(StartRimTextureId, StartRimRotation) =
						ArrowGraphicManager.GetPlayerHoldStartTextureRim(lane, selected);
					(EndFillTextureId, EndFillRotation, EndColor) =
						ArrowGraphicManager.GetPlayerHoldEndTextureFill(row, lane, active, selected, player);
					(EndRimTextureId, EndRimRotation) =
						ArrowGraphicManager.GetPlayerHoldEndTextureRim(lane, selected);
				}
			}
		}

		public void DrawStart(int x, int y, int w)
		{
			if (StartTextureId == null)
				return;

			// It is assumed there is no height padding baked into this texture.
			var (_, holdBodyStartHeight) = TextureAtlas.GetDimensions(StartTextureId);
			var holdBodyStartH = (int)(holdBodyStartHeight * Scale);

			TextureAtlas.Draw(
				StartTextureId,
				SpriteBatch,
				new Rectangle(x, y - holdBodyStartH, w, holdBodyStartH),
				0.0f,
				Alpha,
				StartMirrored ? SpriteEffects.FlipHorizontally : SpriteEffects.None);
		}
		public void DrawBody()
		{
		}

		public void DrawCap(int x, int y, int w, int h)
		{
			TextureAtlas.Draw(
				CapTextureId,
				SpriteBatch,
				new Rectangle(x, y, w, h),
				CapRotation,
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

		HoldRenderState state = new HoldRenderState(textureAtlas, spriteBatch, arrowGraphicManager, this, active, NextDrawActive, alpha, Scale);
		var (_, startArrowHeight) = textureAtlas.GetDimensions(state.StartArrowTextureId);
		var halfArrowHeight = startArrowHeight * 0.5 * Scale;
		var (_, capH) = textureAtlas.GetDimensions(state.CapTextureId);
		var (bodyTexW, bodyTexH) = textureAtlas.GetDimensions(state.BodyTextureId);

		// Determine the Y value and height to use.
		// If the note is active, we should bring down the top to the cutoff point.
		var bodyY = Y + halfArrowHeight;
		var noteH = H - halfArrowHeight;
		if (activeAndCutoff)
		{
			noteH -= NextDrawActiveYCutoffPoint - bodyY;
			bodyY = NextDrawActiveYCutoffPoint;
		}

		capH = (int)(capH * Scale + 0.5);
		var bodyTileH = (int)(bodyTexH * Scale + 0.5);
		var y = (int)(bodyY + noteH + 0.5) - capH;
		var minY = (int)(bodyY + 0.5);
		var x = (int)(X + 0.5);
		var w = (int)(W + 0.5);

		// Record the cap position for drawing later.
		// Round down on the minimumCapY to avoid rounding errors for 0 length
		// holds, which are more common than negative holds.
		var capY = y;
		var minimumCapY = (int)bodyY;
		if (arrowGraphicManager.AreHoldCapsCentered())
		{
			y += (int)(capH * 0.5f);
			minimumCapY = (int)Y;
		}

		// Adjust the starting y value so we don't needlessly loop when zoomed in and a large
		// area of the hold is off the screen.
		if (y > ScreenHeight + capH)
		{
			y -= (y - (int)ScreenHeight) / capH * capH;
		}

		// Draw the body by looping up from the bottom, ensuring that each tiled body texture aligns
		// perfectly with the previous one.
		var spriteEffects = holdBodyMirrored ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
		
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
				var sourceH = (int)(bodyTexH * ((double)h / bodyTileH));
				textureAtlas.Draw(holdBodyTextureId, spriteBatch, new Rectangle(0, bodyTexH - sourceH, bodyTexW, sourceH),
					new Rectangle(x, y, w, h), 0.0f, alpha, spriteEffects);
			}
			else
			{
				state.DrawStart(textureAtlas, S);

				textureAtlas.Draw(holdBodyTextureId, spriteBatch, new Rectangle(x, y, w, h), 0.0f, alpha, spriteEffects);
			}
		}

		// Some arrows, like solo diagonals need a hold start graphic to fill the gap at the top of the hold
		// between the arrow midpoint and the widest part of the arrow.
		state.DrawStart(x, minY, w);

		// Draw the cap, if it is visible.
		// Also ensure that the cap is below the start. In negative scroll rate regions it may be
		// above the start, in which case we do not want to render it.
		// The cap should be drawn after the body as some caps render on top of the body.
		if (capY > -capH && capY < ScreenHeight && capY >= minimumCapY)
		{
			state.DrawCap(x, capY, w, capH);
		}

		// Draw the arrow at the start of the hold.
		var holdStartY = bodyY - halfArrowHeight;
		DrawTap(textureAtlas, spriteBatch, arrowGraphicManager, X, holdStartY);

		if (IsFake())
			DrawFakeMarker(textureAtlas, spriteBatch, state.StartArrowTextureId, X, holdStartY);

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

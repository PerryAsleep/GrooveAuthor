using Fumen.ChartDefinition;
using Fumen.Converters;
using Microsoft.Xna.Framework.Graphics;

namespace StepManiaEditor
{
	public class EditorWarpEvent : EditorRateAlteringEvent
	{
		public static readonly string WidgetHelp =
			"Warp.\n" +
			"Expected format: \"<length>rows\". e.g. \"48rows\"\n" +
			"Length must be non-negative.\n" +
			"A warp will instantly advance the chart forward by the specified number of rows.\n" +
			"This is the preferred method of achieving this effect rather than using negative\n" +
			"stops or tempos. Warp durations are specified in rows where one beat in StepMania is\n" +
			$"{SMCommon.MaxValidDenominator} rows.";
		private const string Format = "%irows";
		private const float Speed = 1.0f;

		public Warp WarpEvent;
		private bool WidthDirty;

		public int IntValue
		{
			get => WarpEvent.LengthIntegerPosition;
			set
			{
				if (WarpEvent.LengthIntegerPosition != value && value >= 0)
				{
					WarpEvent.LengthIntegerPosition = value;
					WidthDirty = true;
					EditorChart.OnRateAlteringEventModified(this);
				}
			}
		}

		/// <remarks>
		/// This lazily updates the width if it is dirty.
		/// This is a bit of hack because in order to determine the width we need to call into
		/// ImGui but that is not a thread-safe operation. If we were to set the width when
		/// loading the chart for example, this could crash. By lazily setting it we avoid this
		/// problem as long as we assume the caller of GetW() happens on the main thread.
		/// </remarks>
		public override double GetW()
		{
			if (WidthDirty)
			{
				SetW(ImGuiLayoutUtils.GetMiscEditorEventDragIntWidgetWidth(IntValue, Format));
				WidthDirty = false;
			}
			return base.GetW();
		}

		public EditorWarpEvent(EditorChart editorChart, Warp chartEvent) : base(editorChart, chartEvent)
		{
			WarpEvent = chartEvent;
			WidthDirty = true;
		}

		public override void Draw(TextureAtlas textureAtlas, SpriteBatch spriteBatch, ArrowGraphicManager arrowGraphicManager)
		{
			if (GetAlpha() <= 0.0f)
				return;
			ImGuiLayoutUtils.MiscEditorEventDragIntWidget(
				GetImGuiId(),
				this,
				nameof(IntValue),
				(int)GetX(), (int)GetY(), (int)GetW(),
				Utils.UIWarpColorABGR,
				false,
				CanBeDeleted,
				Speed,
				Format,
				GetAlpha(),
				WidgetHelp,
				0);
		}
	}
}

using Fumen.ChartDefinition;
using Microsoft.Xna.Framework.Graphics;

namespace StepManiaEditor
{
	public class EditorWarpEvent : EditorRateAlteringEvent
	{
		public Warp WarpEvent;

		private const string Format = "%irows";
		private bool WidthDirty;

		public int IntValue
		{
			get => WarpEvent.LengthIntegerPosition;
			set
			{
				if (WarpEvent.LengthIntegerPosition != value)
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

		public override void Draw(TextureAtlas textureAtlas, SpriteBatch spriteBatch)
		{
			ImGuiLayoutUtils.MiscEditorEventDragIntWidget(
				GetImGuiId(),
				this,
				nameof(IntValue),
				(int)GetX(), (int)GetY(), (int)GetW(),
				Utils.UIWarpColorABGR,
				false,
				CanBeDeleted,
				Format);
		}
	}
}

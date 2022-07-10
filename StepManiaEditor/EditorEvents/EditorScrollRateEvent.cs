using Fumen.ChartDefinition;
using Microsoft.Xna.Framework.Graphics;

namespace StepManiaEditor
{
	public class EditorScrollRateEvent : EditorRateAlteringEvent
	{
		public ScrollRate ScrollRateEvent;

		private const string Format = "%.9gx";
		private const float Speed = 0.01f;
		private bool WidthDirty;

		public double DoubleValue
		{
			get => ScrollRateEvent.Rate;
			set
			{
				if (!ScrollRateEvent.Rate.DoubleEquals(value))
				{
					ScrollRateEvent.Rate = value;
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
				SetW(ImGuiLayoutUtils.GetMiscEditorEventDragDoubleWidgetWidth(DoubleValue, Format));
				WidthDirty = false;
			}
			return base.GetW();
		}

		public EditorScrollRateEvent(EditorChart editorChart, ScrollRate chartEvent) : base(editorChart, chartEvent)
		{
			ScrollRateEvent = chartEvent;
			WidthDirty = true;
		}

		public override void Draw(TextureAtlas textureAtlas, SpriteBatch spriteBatch)
		{
			if (GetAlpha() <= 0.0f)
				return;
			ImGuiLayoutUtils.MiscEditorEventDragDoubleWidget(
				GetImGuiId(),
				this,
				nameof(DoubleValue),
				(int)GetX(), (int)GetY(), (int)GetW(),
				Utils.UIScrollsColorABGR,
				false,
				CanBeDeleted,
				Speed,
				Format,
				GetAlpha());
		}
	}
}

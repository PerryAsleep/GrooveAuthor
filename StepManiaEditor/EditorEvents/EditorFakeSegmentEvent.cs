using Fumen.ChartDefinition;
using Microsoft.Xna.Framework.Graphics;
using static Fumen.Utils;

namespace StepManiaEditor
{
	public class EditorFakeSegmentEvent : EditorEvent
	{
		public FakeSegment FakeSegmentEvent;

		private const string Format = "%.9gs";
		private bool WidthDirty;

		public double DoubleValue
		{
			get => ToSeconds(FakeSegmentEvent.LengthMicros);
			set
			{
				var newMicros = ToMicros(value);
				if (FakeSegmentEvent.LengthMicros != newMicros)
				{
					FakeSegmentEvent.LengthMicros = newMicros;
					WidthDirty = true;
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

		public EditorFakeSegmentEvent(EditorChart editorChart, FakeSegment chartEvent) : base(editorChart, chartEvent)
		{
			FakeSegmentEvent = chartEvent;
			WidthDirty = true;
		}

		public override void Draw(TextureAtlas textureAtlas, SpriteBatch spriteBatch)
		{
			ImGuiLayoutUtils.MiscEditorEventDragDoubleWidget(
				GetImGuiId(),
				this,
				nameof(DoubleValue),
				(int)GetX(), (int)GetY(), (int)GetW(),
				Utils.UIFakesColorABGR,
				false,
				true,
				Format);
		}
	}
}

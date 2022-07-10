using Fumen.ChartDefinition;
using Microsoft.Xna.Framework.Graphics;

namespace StepManiaEditor
{
	public class EditorTickCountEvent : EditorEvent
	{
		public TickCount TickCountEvent;

		private const string Format = "%iticks";
		private const float Speed = 0.1f;
		private bool WidthDirty;
		public bool CanBeDeleted;

		public int IntValue
		{
			get => TickCountEvent.Ticks;
			set
			{
				if (value != TickCountEvent.Ticks)
				{
					TickCountEvent.Ticks = value;
					WidthDirty = true;
				}
			}
		}

		public EditorTickCountEvent(EditorChart editorChart, TickCount chartEvent) : base(editorChart, chartEvent)
		{
			TickCountEvent = chartEvent;
			WidthDirty = true;
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

		public override void Draw(TextureAtlas textureAtlas, SpriteBatch spriteBatch)
		{
			if (GetAlpha() <= 0.0f)
				return;
			ImGuiLayoutUtils.MiscEditorEventDragIntWidget(
				GetImGuiId(),
				this,
				nameof(IntValue),
				(int)GetX(), (int)GetY(), (int)GetW(),
				Utils.UITicksColorABGR,
				false,
				CanBeDeleted,
				Speed,
				Format,
				GetAlpha(),
				0);
		}
	}
}

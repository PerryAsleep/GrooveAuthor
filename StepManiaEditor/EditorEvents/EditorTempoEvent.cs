using Fumen.ChartDefinition;
using Fumen.Converters;
using Microsoft.Xna.Framework.Graphics;

namespace StepManiaEditor
{
	public class EditorTempoEvent : EditorRateAlteringEvent
	{
		public static readonly string WidgetHelp =
			"Tempo.\n" +
			"Tempo in beats per minute.\n" +
			"Expected format: \"<value>bpm\". e.g. \"120.0bpm\".\n" +
			$"StepMania defines a beat as {SMCommon.MaxValidDenominator} rows.";
			// TODO: 0.0 and negative tempo handling
		private const string Format = "%.9gbpm";
		private const float Speed = 0.25f;

		public Tempo TempoEvent;
		private bool WidthDirty;

		public double DoubleValue
		{
			get => TempoEvent.TempoBPM;
			set
			{
				// TODO: 0.0 and negative bpm handling
				if (!value.DoubleEquals(0.0))
				{
					if (!TempoEvent.TempoBPM.DoubleEquals(value))
					{
						TempoEvent.TempoBPM = value;
						WidthDirty = true;
						EditorChart.OnRateAlteringEventModified(this);
					}
				}
			}
		}

		public EditorTempoEvent(EditorChart editorChart, Tempo chartEvent) : base(editorChart, chartEvent)
		{
			TempoEvent = chartEvent;
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
				SetW(ImGuiLayoutUtils.GetMiscEditorEventDragDoubleWidgetWidth(DoubleValue, Format));
				WidthDirty = false;
			}
			return base.GetW();
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
				Utils.UITempoColorABGR,
				false,
				CanBeDeleted,
				Speed,
				Format,
				GetAlpha(),
				WidgetHelp);
		}
	}
}

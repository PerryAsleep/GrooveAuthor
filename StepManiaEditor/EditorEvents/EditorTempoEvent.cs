using Fumen.ChartDefinition;
using Fumen.Converters;
using Microsoft.Xna.Framework.Graphics;

namespace StepManiaEditor
{
	internal sealed class EditorTempoEvent : EditorRateAlteringEvent
	{
		public static readonly string EventShortDescription =
			"Tempo in beats per minute.\n" +
			$"StepMania defines a beat as {SMCommon.MaxValidDenominator} rows.";
		public static readonly string WidgetHelp =
			"Tempo.\n" +
			"Expected format: \"<value>bpm\". e.g. \"120.0bpm\".\n" +
			EventShortDescription;
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
		private double _W;
		public override double W
		{
			get
			{
				if (WidthDirty)
				{
					_W = ImGuiLayoutUtils.GetMiscEditorEventDragDoubleWidgetWidth(DoubleValue, Format);
					WidthDirty = false;
				}
				return _W;
			}
			set
			{
				_W = value;
			}
		}

		public override void Draw(TextureAtlas textureAtlas, SpriteBatch spriteBatch, ArrowGraphicManager arrowGraphicManager)
		{
			if (Alpha <= 0.0f)
				return;
			ImGuiLayoutUtils.MiscEditorEventDragDoubleWidget(
				GetImGuiId(),
				this,
				nameof(DoubleValue),
				(int)X, (int)Y, (int)W,
				Utils.UITempoColorRGBA,
				false,
				CanBeDeleted,
				Speed,
				Format,
				Alpha,
				WidgetHelp);
		}
	}
}

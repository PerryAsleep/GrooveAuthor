using static Fumen.FumenExtensions;
using Microsoft.Xna.Framework.Graphics;



namespace StepManiaEditor
{
	/// <summary>
	/// Class for rendering the song last second hint.
	/// The last second hint is rendered as a miscellaneous editor event widget.
	/// The last second hint does not correspond to an Event in a Chart.
	/// </summary>
	internal sealed class EditorLastSecondHintEvent : EditorEvent
	{
		public static readonly string LastSecondHintDescription =
			"The specified end time of the song." +
			"\nOptional. When not set StepMania will stop a chart shortly after the last note." +
			"\nUseful if you want the chart to continue after the last note.";
		public static readonly string EventShortDescription =
			LastSecondHintDescription +
			"\nThe End Hint can be edited in the Song Properties window.";
		public static readonly string WidgetHelp =
			"End Hint.\n" +
			EventShortDescription;

		private const string Format = "%.9gs";
		private const float Speed = 0.01f;

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
		private bool WidthDirty;

		public double DoubleValue
		{
			get => EditorChart.EditorSong.LastSecondHint;
			set
			{
				if (!EditorChart.EditorSong.LastSecondHint.DoubleEquals(value) && value >= 0.0)
				{
					EditorChart.EditorSong.LastSecondHint = value;
				}
			}
		}

		public EditorLastSecondHintEvent(EditorChart editorChart, double chartPosition)
			: base(EventConfig.CreateConfigNoEvent(editorChart, chartPosition))
		{
			WidthDirty = true;

			// Do not allow repositioning of this event through the widget.
			// To allow this we'll need to fix how updating event timing data from rate altering events
			// handles events which do not correspond to Stepmania chart events (like this event).
			// These events are deleted and re-added as part of the timing update so that their positions can
			// be re-derived. If we allow this event to be repositioned, it may be repositioned in a group
			// with other rate altering events. When this happens, the event tree can end up in an invalid
			// state due to the deletion behavior described above.
			IsPositionImmutable = true;
		}

		public override bool IsMiscEvent() { return true; }
		public override bool IsSelectableWithoutModifiers() { return false; }
		public override bool IsSelectableWithModifiers() { return false; }

		/// <summary>
		/// Updates the chart time of the event to match its row.
		/// Overriden as this ChartEvent's time is stored on the song's last second hint value and not
		/// on an underlying Event.
		/// </summary>
		public override void ResetTimeBasedOnRow()
		{
			var chartTime = 0.0;
			EditorChart.TryGetTimeFromChartPosition(GetChartPosition(), ref chartTime);
			EditorChart.EditorSong.LastSecondHint = chartTime;
		}

		/// <summary>
		/// Gets the chart time in seconds of the event.
		/// Overriden as this event's time is derived from the song's last second hint time.
		/// </summary>
		public override double GetChartTime()
		{
			return EditorChart.EditorSong.LastSecondHint;
		}

		public override void Draw(TextureAtlas textureAtlas, SpriteBatch spriteBatch, ArrowGraphicManager arrowGraphicManager)
		{
			ImGuiLayoutUtils.MiscEditorEventLastSecondHintWidget(
				"LastSecondHintWidget",
				this,
				nameof(DoubleValue),
				(int)X, (int)Y, (int)W,
				Utils.UILastSecondHintColorRGBA,
				IsSelected(),
				CanBeDeleted(),
				Speed,
				Format,
				Alpha,
				WidgetHelp,
				0.0);
		}
	}
}

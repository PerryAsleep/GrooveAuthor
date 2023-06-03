using Microsoft.Xna.Framework;
using static StepManiaEditor.Editor;
using static StepManiaEditor.Utils;
using static Fumen.FumenExtensions;
using Microsoft.Xna.Framework.Graphics;

namespace StepManiaEditor;

/// <summary>
/// Class for rendering the song/chart preview.
/// The preview is rendered as an IRegion and also as a miscellaneous editor event widget.
/// The preview does not correspond to an Event in a Chart.
/// </summary>
internal sealed class EditorPreviewRegionEvent : EditorEvent, IChartRegion, Fumen.IObserver<EditorSong>
{
	public static readonly string PreviewDescription =
		"The music Preview plays when a player scrolls to this song in StepMania.";

	public static readonly string EventShortDescription =
		PreviewDescription +
		"\nThe Preview time and length can also be set in the Song Properties window.";

	public static readonly string WidgetHelp =
		"Music Preview.\n" +
		EventShortDescription +
		"Expected format: \"<length>s\". e.g. \"1.0s\"\n" +
		"Time must be non-negative.";

	private const string Format = "%.9gs";
	private const float Speed = 0.01f;

	#region IChartRegion Implementation

	private double RegionX, RegionY, RegionW, RegionH;

	public double GetRegionX()
	{
		return RegionX;
	}

	public double GetRegionY()
	{
		return RegionY;
	}

	public double GetRegionW()
	{
		return RegionW;
	}

	public double GetRegionH()
	{
		return RegionH;
	}

	public void SetRegionX(double x)
	{
		RegionX = x;
	}

	public void SetRegionY(double y)
	{
		RegionY = y;
	}

	public void SetRegionW(double w)
	{
		RegionW = w;
	}

	public void SetRegionH(double h)
	{
		RegionH = h;
	}

	public double GetRegionPosition()
	{
		return GetChartTime();
	}

	public double GetRegionDuration()
	{
		return EditorChart.GetEditorSong().SampleLength;
	}

	public bool AreRegionUnitsTime()
	{
		return true;
	}

	public bool IsVisible(SpacingMode mode)
	{
		return true;
	}

	public Color GetRegionColor()
	{
		return IRegion.GetColor(PreviewRegionColor, Alpha);
	}

	#endregion IChartRegion Implementation

	/// <remarks>
	/// This lazily updates the width if it is dirty.
	/// This is a bit of hack because in order to determine the width we need to call into
	/// ImGui but that is not a thread-safe operation. If we were to set the width when
	/// loading the chart for example, this could crash. By lazily setting it we avoid this
	/// problem as long as we assume the caller of GetW() happens on the main thread.
	/// </remarks>
	private double WidthInternal;

	public override double W
	{
		get
		{
			if (WidthDirty)
			{
				WidthInternal = ImGuiLayoutUtils.GetMiscEditorEventDragDoubleWidgetWidth(DoubleValue, Format);
				WidthDirty = false;
			}

			return WidthInternal;
		}
		set => WidthInternal = value;
	}

	private bool WidthDirty;

	public double DoubleValue
	{
		get => EditorChart.GetEditorSong().SampleLength;
		set
		{
			if (!EditorChart.GetEditorSong().SampleLength.DoubleEquals(value) && value >= 0.0)
			{
				EditorChart.GetEditorSong().SampleLength = value;
				WidthDirty = true;
			}
		}
	}

	public EditorPreviewRegionEvent(EditorChart editorChart, double chartPosition)
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

	public override void OnAddedToChart()
	{
		EditorChart.GetEditorSong().AddObserver(this);
	}

	public override void OnRemovedFromChart()
	{
		EditorChart.GetEditorSong().RemoveObserver(this);
	}

	/// <summary>
	/// Updates the chart time of the event to match its row.
	/// Overriden as this ChartEvent's time is stored on the song's sample start value and not
	/// on an underlying Event.
	/// </summary>
	public override void ResetTimeBasedOnRow()
	{
		var chartTime = 0.0;
		EditorChart.TryGetTimeFromChartPosition(GetChartPosition(), ref chartTime);
		EditorChart.GetEditorSong().SampleStart = EditorPosition.GetSongTimeFromChartTime(EditorChart, chartTime);
	}

	/// <summary>
	/// Gets the chart time in seconds of the event.
	/// Overriden as this event's time is derived from the song's sample start time.
	/// </summary>
	public override double GetChartTime()
	{
		return EditorPosition.GetChartTimeFromSongTime(EditorChart, EditorChart.GetEditorSong().SampleStart);
	}

	public override bool IsMiscEvent()
	{
		return true;
	}

	public override bool IsSelectableWithoutModifiers()
	{
		return false;
	}

	public override bool IsSelectableWithModifiers()
	{
		return false;
	}

	public override void Draw(TextureAtlas textureAtlas, SpriteBatch spriteBatch, ArrowGraphicManager arrowGraphicManager)
	{
		ImGuiLayoutUtils.MiscEditorEventPreviewDragDoubleWidget(
			"PreviewWidget",
			this,
			nameof(DoubleValue),
			(int)X, (int)Y, (int)W,
			UIPreviewColorRGBA,
			IsSelected(),
			Speed,
			Format,
			Alpha,
			WidgetHelp,
			0.0);
	}

	public void OnNotify(string eventId, EditorSong song, object payload)
	{
		if (eventId == EditorSong.NotificationSampleLengthChanged)
			WidthDirty = true;
	}
}

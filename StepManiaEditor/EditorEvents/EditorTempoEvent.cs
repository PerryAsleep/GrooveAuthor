using Fumen.ChartDefinition;
using Fumen.Converters;
using Microsoft.Xna.Framework.Graphics;
using MonoGameExtensions;
using static System.Diagnostics.Debug;
using static Fumen.FumenExtensions;

namespace StepManiaEditor;

internal sealed class EditorTempoEvent : EditorRateAlteringEvent
{
	public static readonly string EventShortDescription =
		"Tempo in beats per minute.\n" +
		$"StepMania defines a beat as {SMCommon.MaxValidDenominator} rows.";

	public static readonly string WidgetHelp =
		"Tempo.\n" +
		"Expected format: \"<value>bpm\". e.g. \"120.0bpm\".\n" +
		EventShortDescription;

	public const double MinTempo = 0.000001;

	private const string Format = "%.9gbpm";
	private const float Speed = 0.25f;

	private readonly Tempo TempoEvent;
	private bool WidthDirty;

	public double DoubleValue
	{
		get => TempoEvent.TempoBPM;
		set
		{
			Assert(EditorChart.CanBeEdited());
			if (!EditorChart.CanBeEdited())
				return;

			if (value >= MinTempo && !TempoEvent.TempoBPM.DoubleEquals(value))
			{
				TempoEvent.TempoBPM = value;
				WidthDirty = true;
				EditorChart.OnTempoModified(this);
			}
		}
	}

	public override double GetTempo()
	{
		return TempoEvent.TempoBPM;
	}

	public double GetRowsPerSecond(int rowsPerBeat)
	{
		return TempoEvent.GetRowsPerSecond(rowsPerBeat);
	}

	public double GetSecondsPerRow(int rowsPerBeat)
	{
		return TempoEvent.GetSecondsPerRow(rowsPerBeat);
	}

	public EditorTempoEvent(EventConfig config, Tempo chartEvent) : base(config)
	{
		TempoEvent = chartEvent;
		WidthDirty = true;

		Assert(TempoEvent.TempoBPM >= MinTempo);
		if (TempoEvent.TempoBPM < MinTempo)
			TempoEvent.TempoBPM = MinTempo;
	}

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

	public override string GetShortTypeName()
	{
		return "Tempo";
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
		return true;
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
			IsSelected(),
			CanBeDeleted(),
			Speed,
			Format,
			Alpha,
			WidgetHelp,
			MinTempo);
	}
}

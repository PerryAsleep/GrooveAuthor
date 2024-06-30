using Fumen;
using Fumen.ChartDefinition;
using Fumen.Converters;
using Microsoft.Xna.Framework.Graphics;
using MonoGameExtensions;
using static System.Diagnostics.Debug;

namespace StepManiaEditor;

internal sealed class EditorTimeSignatureEvent : EditorRateAlteringEvent
{
	public static readonly string EventShortDescription =
		"StepMania ignores time signatures during gameplay. They are a convenience for visualizing measures in the editor.\n" +
		$"To change how {Utils.GetAppName()} colors steps based on time signatures, edit the Step Coloring value in the Options.";

	public static readonly string WidgetHelp =
		"Time Signature.\n" +
		"Expected format: \"<beat unit>/<number of beats>\". e.g. \"4/4\"\n" +
		"Both values must be positive.\n" +
		$"Denominator must be a power of two and no greater than {SMCommon.MaxValidDenominator}.\n" +
		EventShortDescription;

	private readonly TimeSignature TimeSignatureEvent;
	private bool WidthDirty;

	public string StringValue
	{
		get => TimeSignatureEvent.Signature.ToString();
		set
		{
			var (valid, newSignature) = IsValidTimeSignatureString(value);
			if (valid)
			{
				Assert(EditorChart.CanBeEdited());
				if (!EditorChart.CanBeEdited())
					return;

				// Compare numerator and denominator explicitly to avoid equality
				// of fractions like 2/2 and 4/4.
				if (!(TimeSignatureEvent.Signature.Numerator == newSignature.Numerator &&
				      TimeSignatureEvent.Signature.Denominator == newSignature.Denominator))
				{
					TimeSignatureEvent.Signature = newSignature;
					WidthDirty = true;
					EditorChart.OnTimeSignatureModified(this);
				}
			}
		}
	}

	public int Measure { get; set; }

	public Fraction GetSignature()
	{
		return TimeSignatureEvent.Signature;
	}

	public int GetNumerator()
	{
		return TimeSignatureEvent.Signature.Numerator;
	}

	public int GetDenominator()
	{
		return TimeSignatureEvent.Signature.Denominator;
	}

	public override int GetRowRelativeToMeasureStart(int row)
	{
		return SMCommon.GetRowRelativeToMeasureStart(TimeSignatureEvent, row);
	}

	public int GetRowsPerMeasure()
	{
		var rowsPerWholeNote = SMCommon.NumBeatsPerMeasure * SMCommon.MaxValidDenominator;
		return rowsPerWholeNote * TimeSignatureEvent.Signature.Numerator / TimeSignatureEvent.Signature.Denominator;
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
				WidthInternal = ImGuiLayoutUtils.GetMiscEditorEventStringWidth(StringValue);
				WidthDirty = false;
			}

			return WidthInternal;
		}
		set => WidthInternal = value;
	}

	public static (bool, Fraction) IsValidTimeSignatureString(string v)
	{
		var f = Fraction.FromString(v);
		if (f == null)
			return (false, null);
		if (f.Denominator <= 0 || f.Denominator > SMCommon.MaxValidDenominator)
			return (false, f);
		if (f.Numerator <= 0)
			return (false, f);
		if ((f.Denominator & (f.Denominator - 1)) != 0)
			return (false, f);
		return (true, f);
	}

	public EditorTimeSignatureEvent(EventConfig config, TimeSignature chartEvent) : base(config)
	{
		TimeSignatureEvent = chartEvent;
		// Pull the measure from the MetricPosition.
		Measure = TimeSignatureEvent.MetricPosition.Measure;
		WidthDirty = true;
	}

	public override string GetShortTypeName()
	{
		return "Time Signature";
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
		ImGuiLayoutUtils.MiscEditorEventTimeSignatureWidget(
			GetImGuiId(),
			this,
			nameof(StringValue),
			(int)X, (int)Y, (int)W,
			Utils.UITimeSignatureColorRGBA,
			IsSelected(),
			CanBeDeleted(),
			Alpha,
			WidgetHelp);
	}
}

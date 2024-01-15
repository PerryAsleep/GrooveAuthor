namespace StepManiaEditor;

internal sealed class PerformanceTimings
{
	public const string EditorCPU = "Update and Draw";
	public const string Update = "Update";
	public const string ChartEvents = "Chart Events";
	public const string MiniMap = "Mini Map";
	public const string Waveform = "Waveform";
	public const string Draw = "Draw";
	public const string Present = "Present";

	public static readonly string[] PerfTimings =
	{
		EditorCPU,
		Update,
		ChartEvents,
		MiniMap,
		Waveform,
		Draw,
		Present,
	};

	// One longer than PerfTimings to include a description for the overall frame time.
	public static readonly string[] PerfDescriptions =
	{
		"Entire time spent in one tick. The sum of the Update, Draw, and Present times.",
		"Entire time spent updating and drawing prior to presenting the rendered image.",
		"Entire time spent performing time-dependent updates.",
		"Time spent updating Chart Events.",
		"Time spent updating the Mini Map.",
		"Time spent updating the Waveform.",
		"Time spent drawing prior to presentation.",
		"Time spent presenting the rendered image.",
	};
}

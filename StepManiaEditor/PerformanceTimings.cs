namespace StepManiaEditor;

internal sealed class PerformanceTimings
{
	public const string EditorCPU = "EditorCPU";
	public const string Update = "Update";
	public const string ChartEvents = "Chart Events";
	public const string MiniMap = "Mini Map";
	public const string Waveform = "Waveform";
	public const string Draw = "Draw";
	public const string Present = "Present";
	public const string PresentWait = "PresentWait";

	public static readonly string[] PerfTimings =
	{
		EditorCPU,
		Update,
		ChartEvents,
		MiniMap,
		Waveform,
		Draw,
		Present,
		PresentWait,
	};

	public static readonly string[] PerfUserFacingNames =
	{
		"Entire Frame",
		"Update and Draw Commands",
		"Update",
		"Update: Chart Events",
		"Update: Mini Map",
		"Update: Waveform",
		"Draw Commands",
		"Render: Present",
		"Render: Swap Chain Wait",
	};

	public static readonly uint[] PerfPlotColors =
	{
		0x8A297A29, // green
		0x8A7A4A29, // blue
		0x8A7A4A29, // blue
		0x8A7A4A29, // blue
		0x8A7A4A29, // blue
		0x8A7A4A29, // blue
		0x8A297A77, // yellow
		0x8A29297A, // red
		0x8A29297A, // red
	};

	public static readonly string[] PerfUserFacingDescriptions =
	{
		"Entire time spent in one tick. The sum of the Update, Draw, and Present times.",
		"Entire time spent updating and drawing prior to presenting the rendered image.",
		"Entire time spent performing time-dependent updates.",
		"Time spent updating Chart Events.",
		"Time spent updating the Mini Map.",
		"Time spent updating the Waveform.",
		"Time spent creating draw commands prior to presentation.",
		"Time spent presenting the rendered image.",
		"Time spent waiting for the swap chain to finish presenting.",
	};
}

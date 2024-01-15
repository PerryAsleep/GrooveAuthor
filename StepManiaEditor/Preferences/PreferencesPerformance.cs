using System.Text.Json.Serialization;
using Fumen;
using static StepManiaEditor.PreferencesPerformance;

namespace StepManiaEditor;

/// <summary>
/// Preferences for performance monitoring.
/// </summary>
internal sealed class PreferencesPerformance
{
	/// <summary>
	/// When displaying frame times, the max value should they be displayed against.
	/// </summary>
	public enum FrameMaxTimeMode
	{
		/// <summary>
		/// Each timing should use an independent max value, based on the current data.
		/// </summary>
		Independent,

		/// <summary>
		/// Each timing should use one shared value based on the highest time from the current data.
		/// </summary>
		Shared,

		/// <summary>
		/// Each timing should use one shared value, set explicitly by used.
		/// </summary>
		Explicit,
	}

	// Default values.
	public const int DefaultMaxFramesToDraw = 512;
	public const FrameMaxTimeMode DefaultFrameMaxTimeMode = FrameMaxTimeMode.Shared;
	public const double DefaultExplicitFrameMaxTime = 1.0 / 60;
	public const bool DefaultPerformanceMonitorPaused = false;

	// Preferences.
	[JsonInclude] public bool ShowPerformanceWindow;
	[JsonInclude] public int MaxFramesToDraw = DefaultMaxFramesToDraw;
	[JsonInclude] public FrameMaxTimeMode FrameMaxTime = DefaultFrameMaxTimeMode;
	[JsonInclude] public double ExplicitFrameMaxTime = DefaultExplicitFrameMaxTime;
	[JsonInclude] public bool PerformanceMonitorPaused = DefaultPerformanceMonitorPaused;

	public bool IsUsingDefaults()
	{
		return MaxFramesToDraw == DefaultMaxFramesToDraw
		       && FrameMaxTime == DefaultFrameMaxTimeMode
		       && ExplicitFrameMaxTime.DoubleEquals(DefaultExplicitFrameMaxTime)
		       && PerformanceMonitorPaused == DefaultPerformanceMonitorPaused;
	}

	public void RestoreDefaults()
	{
		// Don't enqueue an action if it would not have any effect.
		if (IsUsingDefaults())
			return;
		ActionQueue.Instance.Do(new ActionRestorePerformancePreferenceDefaults());
	}
}

/// <summary>
/// Action to restore performance monitoring preferences to their default values.
/// </summary>
internal sealed class ActionRestorePerformancePreferenceDefaults : EditorAction
{
	private readonly int PreviousMaxFramesToDraw;
	private readonly FrameMaxTimeMode PreviousFrameMaxTime;
	private readonly double PreviousExplicitFrameMaxTime;
	private readonly bool PreviousPerformanceMonitorPaused;

	public ActionRestorePerformancePreferenceDefaults() : base(false, false)
	{
		var p = Preferences.Instance.PreferencesPerformance;
		PreviousMaxFramesToDraw = p.MaxFramesToDraw;
		PreviousFrameMaxTime = p.FrameMaxTime;
		PreviousExplicitFrameMaxTime = p.ExplicitFrameMaxTime;
		PreviousPerformanceMonitorPaused = p.PerformanceMonitorPaused;
	}

	public override bool AffectsFile()
	{
		return false;
	}

	public override string ToString()
	{
		return "Restore performance monitoring default preferences.";
	}

	protected override void DoImplementation()
	{
		var p = Preferences.Instance.PreferencesPerformance;
		p.MaxFramesToDraw = DefaultMaxFramesToDraw;
		p.FrameMaxTime = DefaultFrameMaxTimeMode;
		p.ExplicitFrameMaxTime = DefaultExplicitFrameMaxTime;
		p.PerformanceMonitorPaused = DefaultPerformanceMonitorPaused;
	}

	protected override void UndoImplementation()
	{
		var p = Preferences.Instance.PreferencesPerformance;
		p.MaxFramesToDraw = PreviousMaxFramesToDraw;
		p.FrameMaxTime = PreviousFrameMaxTime;
		p.ExplicitFrameMaxTime = PreviousExplicitFrameMaxTime;
		p.PerformanceMonitorPaused = PreviousPerformanceMonitorPaused;
	}
}

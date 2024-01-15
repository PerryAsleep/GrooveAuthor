using System;
using System.Numerics;
using Fumen;
using ImGuiNET;
using static StepManiaEditor.ImGuiUtils;

namespace StepManiaEditor;

/// <summary>
/// Class for drawing performance monitoring information.
/// </summary>
internal sealed class UIPerformance
{
	public const string WindowTitle = "Performance";

	private static readonly int TitleColumnWidth = UiScaled(150);
	private static readonly float PlotHeight = UiScaled(80);

	private readonly PerformanceMonitor PerformanceMonitor;

	// Members to avoid per-frame allocations.
	private readonly float[] FrameTimeValues;
	private readonly float[] TimingValues;
	private readonly float[] TimingLastFrameValues;
	private readonly float[] TimingAverages;
	private readonly float[] MaxTimePerTiming;

	public UIPerformance(PerformanceMonitor performanceMonitor)
	{
		PerformanceMonitor = performanceMonitor;

		var maxFrames = PerformanceMonitor.GetMaxNumFrames();
		var timingsPerFrame = PerformanceMonitor.GetNumTimingsPerFrame();

		FrameTimeValues = new float[maxFrames];
		TimingValues = new float[maxFrames * timingsPerFrame];
		TimingLastFrameValues = new float[timingsPerFrame];
		TimingAverages = new float[timingsPerFrame];
		MaxTimePerTiming = new float[timingsPerFrame];
	}

	public void Draw()
	{
		var p = Preferences.Instance.PreferencesPerformance;
		if (!p.ShowPerformanceWindow)
			return;

		ImGui.SetNextWindowSize(Vector2.Zero, ImGuiCond.FirstUseEver);
		if (ImGui.Begin(WindowTitle, ref p.ShowPerformanceWindow))
		{
			// Compute how many frames have data that can be displayed, and the average time over the last second.
			var frameTimeLastSecond = 0.0;
			var numFramesInLastSecond = 0;
			var totalSeconds = 0.0;
			var numFrames = 0;
			var numTimingsPerFrame = PerformanceMonitor.GetNumTimingsPerFrame();
			foreach (var frameData in PerformanceMonitor)
			{
				var frameSeconds = frameData.GetSeconds();
				totalSeconds += frameSeconds;
				if (totalSeconds <= 1.0)
				{
					frameTimeLastSecond += frameSeconds;
					numFramesInLastSecond++;
				}

				numFrames++;
			}

			// At startup we may have no frames to display. In that case don't draw anything.
			if (numFrames > 0)
			{
				var frameIndex = 0;
				var greatestTime = 0.0f;

				// Compute the frame data to display.
				foreach (var frameData in PerformanceMonitor)
				{
					FrameTimeValues[frameIndex] = (float)frameData.GetSeconds();

					var timingIndex = 0;
					foreach (var timingData in frameData.GetTimingData())
					{
						var time = (float)timingData.GetSeconds();
						greatestTime = Math.Max(greatestTime, time);

						if (frameIndex == 0)
						{
							TimingLastFrameValues[timingIndex] = time;
							TimingAverages[timingIndex] = 0.0f;
							MaxTimePerTiming[timingIndex] = 0.0f;
						}

						MaxTimePerTiming[timingIndex] = Math.Max(MaxTimePerTiming[timingIndex], time);

						if (frameIndex < numFramesInLastSecond)
							TimingAverages[timingIndex] += time;

						TimingValues[frameIndex * numTimingsPerFrame + timingIndex] = (float)timingData.GetSeconds();
						timingIndex++;
					}

					frameIndex++;
				}

				for (var timingIndex = 0; timingIndex < numTimingsPerFrame; timingIndex++)
				{
					TimingAverages[timingIndex] /= numFramesInLastSecond;
				}

				if (numFramesInLastSecond > 0)
					frameTimeLastSecond /= numFramesInLastSecond;
				var frameMsOverLastSecond = frameTimeLastSecond * 1000;

				// Draw UI controls.
				if (ImGuiLayoutUtils.BeginTable("Performance Overview", TitleColumnWidth))
				{
					ImGuiLayoutUtils.DrawTitleAndText("Avg Frame Time", $"{frameMsOverLastSecond:F6} ms",
						"Average frame time over the last second.");
					ImGuiLayoutUtils.DrawTitleAndText("Avg FPS", $"{1.0 / frameTimeLastSecond:F6} ms",
						"Average frames per second over the last second.");
					ImGuiLayoutUtils.DrawTitleAndText("Frame Time", $"{FrameTimeValues[0] * 1000:F6} ms",
						"Time of the last frame.");
					ImGuiLayoutUtils.DrawTitleAndText("FPS", $"{1.0 / FrameTimeValues[0]:F6} ms",
						"Frames per second of the last frame.");
					ImGuiLayoutUtils.DrawRowCheckbox(true, "Paused", p, nameof(PreferencesPerformance.PerformanceMonitorPaused),
						false,
						"Whether or not to pause collection of frame data.");
					ImGuiLayoutUtils.DrawRowEnum<PreferencesPerformance.FrameMaxTimeMode>(true, "Plot Max Time Mode", p,
						nameof(PreferencesPerformance.FrameMaxTime), false,
						"How to determine the maximum time for scaling the plots."
						+ "\nIndependent: Each plot should scale independently."
						+ "\nShared:      Each plot will scale together, based on the total frame time."
						+ "\nExplicit:    Each plot will scale together, based on an explicit max time value.");

					var disabledTime = p.FrameMaxTime != PreferencesPerformance.FrameMaxTimeMode.Explicit;
					if (disabledTime)
						PushDisabled();
					ImGuiLayoutUtils.DrawRowDragDouble(true, "Plot Max Time", p, nameof(p.ExplicitFrameMaxTime), false,
						"Explicit max time to use for all plots.", 0.0001F, "%.6f seconds", 0.0, 1.0);
					if (disabledTime)
						PopDisabled();

					ImGuiLayoutUtils.EndTable();
				}

				// Determine the max time for the plots.
				var maxTime = float.MaxValue;
				switch (p.FrameMaxTime)
				{
					case PreferencesPerformance.FrameMaxTimeMode.Explicit:
						maxTime = (float)p.ExplicitFrameMaxTime;
						break;
					case PreferencesPerformance.FrameMaxTimeMode.Independent:
						maxTime = float.MaxValue;
						break;
					case PreferencesPerformance.FrameMaxTimeMode.Shared:
						maxTime = greatestTime;
						break;
				}

				// Draw a plot per timing type.
				ImGui.Separator();
				if (ImGuiLayoutUtils.BeginTable("Performance Overview", TitleColumnWidth))
				{
					for (var i = 0; i < numTimingsPerFrame; i++)
					{
						var maxTimeDisplay = maxTime;
						if (maxTimeDisplay.FloatEquals(float.MaxValue))
							maxTimeDisplay = MaxTimePerTiming[i];

						ImGui.PushStyleColor(ImGuiCol.FrameBg, PerformanceTimings.PerfPlotColors[i]);

						ImGuiLayoutUtils.DrawRowPlot(
							PerformanceTimings.PerfUserFacingNames[i],
							ref TimingValues[i],
							numFrames,
							$"{TimingAverages[i] * 1000:F6} ms avg ({TimingLastFrameValues[i] * 1000:F6} ms current)",
							maxTime,
							PlotHeight,
							numTimingsPerFrame * 4,
							$"{PerformanceTimings.PerfUserFacingDescriptions[i]}\nOut of {maxTimeDisplay * 1000:F6} ms."
						);

						ImGui.PopStyleColor();
					}

					ImGuiLayoutUtils.EndTable();
				}
			}
		}

		ImGui.End();
	}
}

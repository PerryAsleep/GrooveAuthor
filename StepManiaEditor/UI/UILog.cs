using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Text;
using Fumen;
using ImGuiNET;
using static StepManiaEditor.ImGuiUtils;

namespace StepManiaEditor;

/// <summary>
/// Class for drawing the log.
/// </summary>
internal class UILog
{
	public const string WindowTitle = "Log";

	private static readonly string[] LogWindowDateStrings =
	{
		"None",
		"HH:mm:ss",
		"HH:mm:ss.fff",
		"yyyy-MM-dd HH:mm:ss.fff",
	};

	private static readonly Vector4[] LogWindowLevelColors =
	{
		new(1.0f, 1.0f, 1.0f, 1.0f),
		new(1.0f, 1.0f, 0.0f, 1.0f),
		new(1.0f, 0.0f, 0.0f, 1.0f),
	};

	private static readonly int LevelWidth = UiScaled(60);
	private static readonly int LevelTextWidth = UiScaled(32);
	private static readonly int TimeWidth = UiScaled(170);
	private static readonly int TimeTextWidth = UiScaled(25);
	private static readonly int WrapCheckBoxWidth = UiScaled(20);
	private static readonly int WrapTextWidth = UiScaled(24);
	private static readonly Vector2 ButtonSize = new(UiScaled(50), 0.0f);
	private static readonly int DefaultWidth = UiScaled(561);
	private static readonly int DefaultHeight = UiScaled(300);
	private static readonly int DefaultWindowY = UiScaled(21);
	private static readonly int DefaultWindowX = UiScaled(1768);

	private readonly Editor Editor;

	public UILog(Editor editor)
	{
		Editor = editor;
	}

	public static Vector4 GetColor(LogLevel level)
	{
		return LogWindowLevelColors[(int)level];
	}

	public void Draw(LinkedList<Logger.LogMessage> logBuffer, object logBufferLock, string logFilePath)
	{
		if (!Preferences.Instance.ShowLogWindow)
			return;

		var viewportWidth = UiScaled(Editor.GetViewportWidth());
		var logPosition = new Vector2(Math.Min(DefaultWindowX, viewportWidth - DefaultWidth), DefaultWindowY);
		var logWidth = Math.Max(DefaultWidth, viewportWidth - logPosition.X);
		var logSize = new Vector2(logWidth, DefaultHeight);
		ImGui.SetNextWindowSize(logSize, ImGuiCond.FirstUseEver);
		ImGui.SetNextWindowPos(logPosition, ImGuiCond.FirstUseEver);
		if (ImGui.Begin(WindowTitle, ref Preferences.Instance.ShowLogWindow, ImGuiWindowFlags.NoScrollbar))
		{
			lock (logBufferLock)
			{
				Text("Level", LevelTextWidth);

				ImGui.SameLine();
				ImGui.SetNextItemWidth(LevelWidth);
				ComboFromEnum("##ComboLogLevel", ref Preferences.Instance.LogWindowLevel);

				ImGui.SameLine();
				Text("Time", TimeTextWidth);
				ImGui.SameLine();
				ImGui.SetNextItemWidth(TimeWidth);
				ImGui.Combo("##ComboLogTime", ref Preferences.Instance.LogWindowDateDisplay, LogWindowDateStrings,
					LogWindowDateStrings.Length);

				ImGui.SameLine();
				Text("Wrap", WrapTextWidth);
				ImGui.SameLine();
				ImGui.SetNextItemWidth(WrapCheckBoxWidth);
				ImGui.Checkbox("##CheckboxLogWrap", ref Preferences.Instance.LogWindowLineWrap);

				// Right justify the buttons.
				var spacing = ImGui.GetStyle().ItemSpacing.X;
				var padding = Math.Max(0.0f, ImGui.GetContentRegionAvail().X
				                             - ButtonSize.X * 3.0f
				                             - LevelTextWidth
				                             - LevelWidth
				                             - TimeTextWidth
				                             - TimeWidth
				                             - WrapTextWidth
				                             - WrapCheckBoxWidth
				                             - spacing * 8.0f);
				if (padding > 0.0f)
				{
					// Adding padding will add two more spacing elements.
					// We need to remove the x spacing to make the padding math work and then restore the x spacing
					// after the next element.
					ImGui.SameLine();
					ImGui.GetStyle().ItemSpacing.X = 0.0f;
					ImGui.Dummy(new Vector2(padding, 0.0f));
				}

				// Clear log button.
				ImGui.SameLine();
				if (ImGui.Button("Clear", ButtonSize))
				{
					Logger.ClearBuffer();
				}

				// Reset the padding after the previous button.
				if (padding > 0.0f)
					ImGui.GetStyle().ItemSpacing.X = spacing;

				// Copy log button.
				ImGui.SameLine();
				StringBuilder copyStringBuilder = null;
				if (ImGui.Button("Copy", ButtonSize))
				{
					// Set up StringBuilder for copying as we draw.
					copyStringBuilder = new StringBuilder();
				}

				// Open log file button.
				ImGui.SameLine();
				if (string.IsNullOrEmpty(logFilePath))
					PushDisabled();
				if (ImGui.Button("Open", ButtonSize))
				{
					// Open the log file letting the operating system choose
					// the appropriate application.
					try
					{
						var processStartInfo = new ProcessStartInfo
						{
							FileName = logFilePath!,
							UseShellExecute = true,
						};
						Process.Start(processStartInfo);
					}
					catch (Exception e)
					{
						Logger.Error($"Failed to open log file: {e}");
					}
				}

				if (string.IsNullOrEmpty(logFilePath))
					PopDisabled();

				// Log messages.
				ImGui.Separator();
				var format = LogWindowDateStrings[Preferences.Instance.LogWindowDateDisplay];
				var flags = Preferences.Instance.LogWindowLineWrap ? ImGuiWindowFlags.None : ImGuiWindowFlags.HorizontalScrollbar;
				var first = true;
				var logDate = Preferences.Instance.LogWindowDateDisplay != 0;
				ImGui.BeginChild("LogMessages", Vector2.Zero, false, flags);
				{
					var node = logBuffer.First;
					while (node != null)
					{
						var message = node.Value;

						if (message.Level < Preferences.Instance.LogWindowLevel)
						{
							node = node.Next;
							continue;
						}

						if (logDate)
						{
							var timeText = message.Time.ToString(format);
							if (copyStringBuilder != null)
							{
								if (!first)
									copyStringBuilder.Append('\n');
								copyStringBuilder.Append(timeText);
								copyStringBuilder.Append(' ');
							}

							ImGui.Text(timeText);
							ImGui.SameLine();
						}

						if (Preferences.Instance.LogWindowLineWrap)
							ImGui.PushTextWrapPos();
						ImGui.TextColored(LogWindowLevelColors[(int)message.Level], message.Message);
						if (!first && !logDate)
							copyStringBuilder?.Append('\n');
						copyStringBuilder?.Append(message.Message);
						if (Preferences.Instance.LogWindowLineWrap)
							ImGui.PopTextWrapPos();

						first = false;
						node = node.Next;
					}
				}
				ImGui.EndChild();

				// Copy to clipboard.
				if (copyStringBuilder != null)
				{
					var text = copyStringBuilder.ToString();
					if (!string.IsNullOrEmpty(text))
					{
						System.Windows.Forms.Clipboard.SetText(text);
						Logger.Info("Copied log to clipboard.");
					}
					else
					{
						Logger.Warn("No log text available to copy.");
					}
				}
			}
		}

		ImGui.End();
	}
}

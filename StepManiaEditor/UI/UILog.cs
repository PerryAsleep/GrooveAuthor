using System.Collections.Generic;
using Fumen;
using ImGuiNET;
using static StepManiaEditor.Utils;

namespace StepManiaEditor
{
	/// <summary>
	/// Class for drawing the log.
	/// </summary>
	class UILog
	{
		private static readonly string[] LogWindowDateStrings =
		{
			"None",
			"HH:mm:ss",
			"HH:mm:ss.fff",
			"yyyy-MM-dd HH:mm:ss.fff"
		};

		private static readonly System.Numerics.Vector4[] LogWindowLevelColors =
		{
			new System.Numerics.Vector4(1.0f, 1.0f, 1.0f, 1.0f),
			new System.Numerics.Vector4(1.0f, 1.0f, 0.0f, 1.0f),
			new System.Numerics.Vector4(1.0f, 0.0f, 0.0f, 1.0f),
		};

		private static readonly int WindowW = UiScaled(200);
		private static readonly int WindowH = UiScaled(100);
		private static readonly int LevelWidth = UiScaled(60);
		private static readonly int TimeWidth = UiScaled(186);

		public static void Draw(LinkedList<Logger.LogMessage> logBuffer, object logBufferLock)
		{
			if (!Preferences.Instance.ShowLogWindow)
				return;

			ImGui.SetNextWindowSize(new System.Numerics.Vector2(WindowW, WindowH), ImGuiCond.FirstUseEver);
			if (ImGui.Begin("Log", ref Preferences.Instance.ShowLogWindow, ImGuiWindowFlags.NoScrollbar))
			{
				lock (logBufferLock)
				{
					ImGui.PushItemWidth(LevelWidth);
					ComboFromEnum("Level", ref Preferences.Instance.LogWindowLevel);
					ImGui.PopItemWidth();

					ImGui.SameLine();
					ImGui.PushItemWidth(TimeWidth);
					ImGui.Combo("Time", ref Preferences.Instance.LogWindowDateDisplay, LogWindowDateStrings,
						LogWindowDateStrings.Length);
					ImGui.PopItemWidth();

					ImGui.SameLine();
					ImGui.Checkbox("Wrap", ref Preferences.Instance.LogWindowLineWrap);

					ImGui.Separator();

					var format = LogWindowDateStrings[Preferences.Instance.LogWindowDateDisplay];

					var flags = Preferences.Instance.LogWindowLineWrap ? ImGuiWindowFlags.None : ImGuiWindowFlags.HorizontalScrollbar;
					ImGui.BeginChild("LogMessages", new System.Numerics.Vector2(), false, flags);
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

							if (Preferences.Instance.LogWindowDateDisplay != 0)
							{
								ImGui.Text(message.Time.ToString(format));
								ImGui.SameLine();
							}

							if (Preferences.Instance.LogWindowLineWrap)
								ImGui.PushTextWrapPos();
							ImGui.TextColored(LogWindowLevelColors[(int)message.Level], message.Message);
							if (Preferences.Instance.LogWindowLineWrap)
								ImGui.PopTextWrapPos();

							node = node.Next;
						}
					}
					ImGui.EndChild();
				}
			}
			ImGui.End();
		}
	}
}

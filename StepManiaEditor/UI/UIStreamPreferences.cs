using ImGuiNET;
using static StepManiaEditor.ImGuiUtils;

namespace StepManiaEditor;

/// <summary>
/// Class for drawing stream preferences UI.
/// </summary>
internal sealed class UIStreamPreferences
{
	public const string WindowTitle = "Stream Preferences";

	private static readonly int TitleColumnWidth = UiScaled(120);
	private static readonly int DefaultWidth = UiScaled(460);

	public static void Draw()
	{
		var p = Preferences.Instance.PreferencesStream;
		if (!p.ShowStreamPreferencesWindow)
			return;

		if (BeginWindow(WindowTitle, ref p.ShowStreamPreferencesWindow, DefaultWidth))
		{
			if (ImGuiLayoutUtils.BeginTable("Stream", TitleColumnWidth))
			{
				ImGuiLayoutUtils.DrawRowSubdivisions(true, "Note Type", p, nameof(PreferencesStream.NoteType), false,
					"The note type to use when considering whether a measure is part of a stream.");

				ImGuiLayoutUtils.DrawRowCheckbox(true, "Show Break Lengths", p, nameof(PreferencesStream.ShowBreakLengths), false,
					"If true then breaks will show with full lengths. If false then breaks will show with abbreviated notation.");

				ImGuiLayoutUtils.DrawRowDragInt(true, "Min Stream Length", p,
					nameof(PreferencesStream.MinimumLengthToConsiderStream), false,
					"The minimum length in measures for stream to be counted.", 0.1F, "%i measures", 0, 8);

				ImGuiLayoutUtils.DrawRowDragInt(true, "Short Break Length", p,
					nameof(PreferencesStream.ShortBreakCutoff), false,
					"Breaks at or under this many measures will be considered short breaks for stream notation.", 0.1F, "%i measures", 0, 64);

				ImGuiLayoutUtils.DrawRowCharacterInput(true, "Short Break Mark", p, nameof(PreferencesStream.ShortBreakCharacter), false,
					"Character to use to represent short breaks in stream notation.");

				ImGuiLayoutUtils.DrawRowCharacterInput(true, "Long Break Mark", p, nameof(PreferencesStream.LongBreakCharacter), false,
					"Character to use to represent long breaks in stream notation.");

				ImGuiLayoutUtils.EndTable();
			}

			ImGui.Separator();
			if (ImGuiLayoutUtils.BeginTable("Stream Restore", TitleColumnWidth))
			{
				if (ImGuiLayoutUtils.DrawRowButton("Restore Defaults", "Restore Defaults",
						"Restore all stream preferences to their default values."))
				{
					p.RestoreDefaults();
				}

				ImGuiLayoutUtils.EndTable();
			}
		}

		ImGui.End();
	}
}

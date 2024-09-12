using ImGuiNET;
using static StepManiaEditor.ImGuiUtils;

namespace StepManiaEditor;

/// <summary>
/// Class for drawing stream preferences UI.
/// </summary>
internal sealed class UIStreamPreferences : UIWindow
{
	private static readonly int TitleColumnWidth = UiScaled(120);
	private static readonly int DefaultWidth = UiScaled(460);

	public static UIStreamPreferences Instance { get; } = new();

	private UIStreamPreferences() : base("Stream Preferences")
	{
	}

	public override void Open(bool focus)
	{
		Preferences.Instance.PreferencesStream.ShowStreamPreferencesWindow = true;
		if (focus)
			Focus();
	}

	public override void Close()
	{
		Preferences.Instance.PreferencesStream.ShowStreamPreferencesWindow = false;
	}

	public void Draw()
	{
		var p = Preferences.Instance.PreferencesStream;
		if (!p.ShowStreamPreferencesWindow)
			return;

		if (BeginWindow(WindowTitle, ref p.ShowStreamPreferencesWindow, DefaultWidth))
			DrawContents();
		ImGui.End();
	}

	public static void DrawContents()
	{
		var p = Preferences.Instance.PreferencesStream;

		if (ImGuiLayoutUtils.BeginTable("Stream", TitleColumnWidth))
		{
			ImGuiLayoutUtils.DrawRowSubdivisions(true, "Note Type", p, nameof(PreferencesStream.NoteType), false,
				"The note type to use when considering whether a measure is part of a stream.");

			ImGuiLayoutUtils.DrawRowEnum<StepAccumulationType>(true, "Accumulation Type", p,
				nameof(PreferencesStream.AccumulationType), false,
				"How to count steps for stream determination." +
				"\nStep: Each individual note is counted once. Two notes on the same row count as two events." +
				"\nRow:  Multiple notes on the same row are counted as one. Two notes on the same row count as one event.");

			ImGuiLayoutUtils.DrawRowCheckbox(true, "Show Break Lengths", p, nameof(PreferencesStream.ShowBreakLengths), false,
				"If true then breaks will show with full lengths. If false then breaks will show with abbreviated notation.");

			ImGuiLayoutUtils.DrawRowDragInt(true, "Min Stream Length", p,
				nameof(PreferencesStream.MinimumLengthToConsiderStream), false,
				"The minimum length in measures for stream to be counted.", 0.1F, "%i measures", 0, 8);

			ImGuiLayoutUtils.DrawRowDragInt(true, "Short Break Length", p,
				nameof(PreferencesStream.ShortBreakCutoff), false,
				"Breaks at or under this many measures will be considered short breaks for stream notation.", 0.1F,
				"%i measures", 0, 64);

			ImGuiLayoutUtils.DrawRowCharacterInput(true, "Short Break Mark", p, nameof(PreferencesStream.ShortBreakCharacter),
				false,
				"Character to use to represent short breaks in stream notation.");

			ImGuiLayoutUtils.DrawRowCharacterInput(true, "Long Break Mark", p, nameof(PreferencesStream.LongBreakCharacter),
				false,
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
}

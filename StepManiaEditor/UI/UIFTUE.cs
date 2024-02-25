using System;
using System.Numerics;
using ImGuiNET;
using static StepManiaEditor.ImGuiUtils;

namespace StepManiaEditor;

/// <summary>
/// Class for drawing first time user experience dialog sequences.
/// </summary>
internal sealed class UIFTUE
{
	private static readonly int TitleColumnWidth = UiScaled(160);
	private static readonly float DefaultWidth = UiScaled(622);
	private static readonly float DefaultHeightLarge = UiScaled(410);
	private static readonly Vector2 DefaultSizeLarge = new(DefaultWidth, DefaultHeightLarge);
	private static readonly float DefaultHeightMedium = UiScaled(220);
	private static readonly Vector2 DefaultSizeMedium = new(DefaultWidth, DefaultHeightMedium);
	private static readonly float DefaultHeightSmall = UiScaled(125);
	private static readonly Vector2 DefaultSizeSmall = new(DefaultWidth, DefaultHeightSmall);
	public static readonly float NavButtonWidth = UiScaled(80);
	public static readonly float NavButtonHeight = UiScaled(21);
	public static readonly float SeparatorHeight = UiScaled(1);
	public static readonly Vector2 NavButtonSize = new(NavButtonWidth, NavButtonHeight);
	public static readonly Vector2 PageIndexPadding = new(UiScaled(523), 1);
	private static bool WindowOpen;
	private int NumFtueSteps;

	/// <summary>
	/// Window sizes to use so the size and layout changes as little as possible while
	/// not having too much blank space on smaller dialogs.
	/// </summary>
	private enum FtueWindowSize
	{
		Small,
		Medium,
		Large,
	}

	private readonly Editor Editor;

	public UIFTUE(Editor editor)
	{
		Editor = editor;
	}

	public void Draw()
	{
		var p = Preferences.Instance;
		var version = Editor.GetAppVersion();
		var maxVersionWithFtue = new Version(0, 1, 0, 0);

		if (p.LastCompletedFtueVersion != null && version >= maxVersionWithFtue)
			return;

		// v0.1.0 FTUE
		var v010 = new Version(0, 1, 0, 0);
		if (p.LastCompletedFtueVersion == null || p.LastCompletedFtueVersion < v010)
		{
			NumFtueSteps = 6;
			do
			{
				switch (p.FtueIndex)
				{
					case 0:
						DrawWelcome(version);
						break;
					case 1:
						DrawDefaultChartType(version);
						break;
					case 2:
						DrawNewSongSync(version);
						break;
					case 3:
						DrawOpenSongSync(version);
						break;
					case 4:
						DrawStepGraphs(version);
						break;
					case 5:
						DrawFinalMessage(version);
						break;
					default:
						p.LastCompletedFtueVersion = v010;
						break;
				}
			} while (!WindowOpen && p.LastCompletedFtueVersion != v010);
		}

		// Add more FTUE dialogs as needed.
	}

	private void DrawWelcome(Version version)
	{
		if (OpenWindow($"Welcome##{version}", FtueWindowSize.Small))
		{
			ImGui.Text($"Thank you for installing {Editor.GetAppName()}."
			           + $"\n\nPlease take a moment to set a few options so {Editor.GetAppName()} can provide you with the best experience.");
			DrawNextButton();
			ImGui.EndPopup();
		}
	}

	private void DrawDefaultChartType(Version version)
	{
		if (OpenWindow($"Welcome##{version}DefaultChartType", FtueWindowSize.Small))
		{
			ImGui.Text("Which type of chart do you work with the most?");

			ImGui.Separator();
			if (ImGuiLayoutUtils.BeginTable("Default Chart Type", TitleColumnWidth))
			{
				UIOptions.DrawDefaultType(false);
				ImGuiLayoutUtils.EndTable();
			}

			DrawNavigationButtons();
			ImGui.EndPopup();
		}
	}

	private void DrawNewSongSync(Version version)
	{
		if (OpenWindow($"Welcome##{version}SongSync", FtueWindowSize.Small))
		{
			ImGui.Text("When creating new songs, how do you prefer them to be synced?");

			ImGui.Separator();
			if (ImGuiLayoutUtils.BeginTable("New Song Sync", TitleColumnWidth))
			{
				UIOptions.DrawNewSongSync(false);
				ImGuiLayoutUtils.EndTable();
			}

			DrawNavigationButtons();
			ImGui.EndPopup();
		}
	}

	private void DrawOpenSongSync(Version version)
	{
		if (OpenWindow($"Welcome##{version}DefaultSync", FtueWindowSize.Small))
		{
			ImGui.Text($"When opening existing songs with unknown sync, what default sync should {Editor.GetAppName()} use?");

			ImGui.Separator();
			if (ImGuiLayoutUtils.BeginTable("Default Song Sync", TitleColumnWidth))
			{
				UIOptions.DrawDefaultSongSync(false);
				ImGuiLayoutUtils.EndTable();
			}

			DrawNavigationButtons();
			ImGui.EndPopup();
		}
	}

	private void DrawStepGraphs(Version version)
	{
		if (OpenWindow($"Welcome##{version}StepGraphs", FtueWindowSize.Large))
		{
			ImGui.TextWrapped(
				$"{Editor.GetAppName()} has features which rely on understanding how pads are laid out and how the body moves."
				+ " These include advanced features like automatic chart generation and step generation, and simpler features like mirroring steps."
				+ $" {Editor.GetAppName()} uses Step Graph files to support these features."
				+ $" Step Graph files for all supported chart types are provided with {Editor.GetAppName()}, but they need to be loaded in order to be used."
				+ " These files can be large."
				+ $"\n\nWhich Step Graph files should {Editor.GetAppName()} load by default?"
				+ "\n\nIf you want to work freely with all chart types, select everything below. This will use more memory."
				+ " If you only ever work with a few chart types, you can save memory and improve startup performance by selecting only the types you work with."
				+ "\n\nThis can be changed at any time, but requires an application restart to take effect.");

			ImGui.Separator();
			if (ImGuiLayoutUtils.BeginTable("Startup Step Graphs", TitleColumnWidth))
			{
				UIOptions.DrawStartupStepGraphs(false);
				ImGuiLayoutUtils.EndTable();
			}

			DrawNavigationButtons();
			ImGui.EndPopup();
		}
	}

	private void DrawFinalMessage(Version version)
	{
		if (OpenWindow($"Welcome##{version}Final", FtueWindowSize.Medium))
		{
			ImGui.TextWrapped("That's it!"
			                  + "\n\nBelow are some resources to help get you started.");

			ImGui.Separator();
			if (ImGuiLayoutUtils.BeginTable("Final Links", TitleColumnWidth))
			{
				if (ImGuiLayoutUtils.DrawRowButton("Documentation", "Open Documentation",
					    "Documentation is written in Markdown. VSCode is a good application for viewing Markdown."
					    + " Alternatively, documentation can be viewed on GitHub."))
				{
					Documentation.OpenDocumentation();
				}

				if (ImGuiLayoutUtils.DrawRowButton("GitHub", $"Open {Editor.GetAppName()} on GitHub",
					    $"Open the GitHub page for {Editor.GetAppName()}: {Documentation.GitHubUrl}"))
				{
					Documentation.OpenGitHub();
				}

				ImGuiLayoutUtils.EndTable();
			}

			ImGui.Separator();
			ImGui.TextWrapped($"Thank you, and I hope you enjoy {Editor.GetAppName()}."
			                  + "\n-Perry");

			DrawNavigationButtons();
			ImGui.EndPopup();
		}
	}

	private bool OpenWindow(string title, FtueWindowSize size)
	{
		var screenW = Editor.GetViewportWidth();
		var screenH = Editor.GetViewportHeight();

		float windowHeight;
		Vector2 windowSize;
		switch (size)
		{
			case FtueWindowSize.Small:
				windowHeight = DefaultHeightSmall;
				windowSize = DefaultSizeSmall;
				break;
			case FtueWindowSize.Medium:
				windowHeight = DefaultHeightMedium;
				windowSize = DefaultSizeMedium;
				break;
			case FtueWindowSize.Large:
			default:
				windowHeight = DefaultHeightLarge;
				windowSize = DefaultSizeLarge;
				break;
		}

		var windowPos = new Vector2((screenW - DefaultWidth) * 0.5f, (screenH - windowHeight) * 0.5f);

		WindowOpen = true;
		ImGui.OpenPopup(title);
		ImGui.SetNextWindowSize(windowSize);
		ImGui.SetNextWindowPos(windowPos, ImGuiCond.Always);
		var open = ImGui.BeginPopupModal(title, ref WindowOpen,
			ImGuiWindowFlags.NoResize |
			ImGuiWindowFlags.Modal |
			ImGuiWindowFlags.NoMove |
			ImGuiWindowFlags.NoDecoration);

		if (open)
		{
			ImGui.Text("Welcome!");
			ImGui.SameLine();
			ImGui.Dummy(PageIndexPadding);
			ImGui.SameLine();
			ImGui.Text($"{Preferences.Instance.FtueIndex + 1}/{NumFtueSteps}");
			ImGui.Separator();
		}

		return open;
	}

	private static void DrawNextButton()
	{
		// Add a dummy padding element to get the bottoms to be bottom-justified.
		var yPadding = ImGui.GetContentRegionAvail().Y - ImGui.GetStyle().ItemSpacing.Y * 2 - NavButtonHeight - SeparatorHeight;
		ImGui.Dummy(new Vector2(1, yPadding));

		// Determine the padding size to keep the Next button right-justified.
		var paddingSize = ImGui.GetContentRegionAvail().X - (NavButtonWidth + ImGui.GetStyle().ItemSpacing.X);

		ImGui.Separator();
		ImGui.Dummy(new Vector2(paddingSize, 1));
		ImGui.SameLine();
		if (ImGui.Button("Next", NavButtonSize))
		{
			Preferences.Instance.FtueIndex++;
			WindowOpen = false;
			ImGui.CloseCurrentPopup();
		}
	}

	private void DrawNavigationButtons()
	{
		// Add a dummy padding element to get the bottoms to be bottom-justified.
		var yPadding = ImGui.GetContentRegionAvail().Y - ImGui.GetStyle().ItemSpacing.Y * 2 - NavButtonHeight - SeparatorHeight;
		ImGui.Dummy(new Vector2(1, yPadding));

		// Determine the spacing between the buttons.
		var paddingSize = ImGui.GetContentRegionAvail().X - (NavButtonWidth + ImGui.GetStyle().ItemSpacing.X) * 2;

		ImGui.Separator();
		if (ImGui.Button("Back", NavButtonSize))
		{
			Preferences.Instance.FtueIndex--;
			WindowOpen = false;
			ImGui.CloseCurrentPopup();
		}

		ImGui.SameLine();
		ImGui.Dummy(new Vector2(paddingSize, 1));

		ImGui.SameLine();
		var final = Preferences.Instance.FtueIndex == NumFtueSteps - 1;
		if (ImGui.Button(final ? "Done" : "Next", NavButtonSize))
		{
			Preferences.Instance.FtueIndex++;
			WindowOpen = false;
			ImGui.CloseCurrentPopup();
		}
	}
}

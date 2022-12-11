using System.Numerics;
using ImGuiNET;

namespace StepManiaEditor
{
	/// <summary>
	/// Class for drawing MiniMap preferences UI.
	/// </summary>
	public class UIMiniMapPreferences
	{
		private Editor Editor;

		public UIMiniMapPreferences(Editor editor)
		{
			Editor = editor;
		}

		public void Draw()
		{
			var p = Preferences.Instance.PreferencesMiniMap;
			if (!p.ShowMiniMapPreferencesWindow)
				return;

			ImGui.SetNextWindowSize(new Vector2(0, 0), ImGuiCond.FirstUseEver);
			ImGui.Begin("MiniMap Preferences", ref p.ShowMiniMapPreferencesWindow, ImGuiWindowFlags.NoScrollbar);

			DrawContents();

			ImGui.End();
		}

		public void DrawContents()
		{
			var p = Preferences.Instance.PreferencesMiniMap;

			if (ImGuiLayoutUtils.BeginTable("Show MiniMap", 120))
			{
				ImGuiLayoutUtils.DrawRowCheckbox(true, "Show Mini Map", p, nameof(PreferencesMiniMap.ShowMiniMap), false,
					"Whether to show the mini map." +
					"\nDisabling the mini map will increase performance.");
				ImGuiLayoutUtils.EndTable();
			}

			ImGui.Separator();
			if (ImGuiLayoutUtils.BeginTable("MiniMap Layout", 120))
			{
				ImGuiLayoutUtils.DrawRowEnum<MiniMap.Position>(true, "Position", p, nameof(PreferencesMiniMap.MiniMapPosition), false,
					"Where the mini map should be located.");

				ImGuiLayoutUtils.DrawRowSliderUInt(true, "X Offset", p, nameof(PreferencesMiniMap.MiniMapXPadding), 0, 1024, false, "%i pixels",
					ImGuiSliderFlags.None,
					"The x position offset in pixels of the mini map with respect to the selected location.");

				ImGuiLayoutUtils.DrawRowSliderUInt(true, "Width", p, nameof(PreferencesMiniMap.MiniMapWidth), 2, 128, false, "%i pixels",
					ImGuiSliderFlags.None,
					"The width of the mini map in pixels.");

				ImGuiLayoutUtils.DrawRowSliderUInt(true, "Note Width", p, nameof(PreferencesMiniMap.MiniMapNoteWidth), 1, 32,
					false, "%i pixels", ImGuiSliderFlags.None,
					"The width of the notes in the mini map in pixels.");

				ImGuiLayoutUtils.DrawRowSliderUInt(true, "Note Spacing", p, nameof(PreferencesMiniMap.MiniMapNoteSpacing), 0,
					32, false, "%i pixels", ImGuiSliderFlags.None,
					"The spacing between notes in the mini map in pixels.");

				ImGuiLayoutUtils.EndTable();
			}

			ImGui.Separator();
			if (ImGuiLayoutUtils.BeginTable("MiniMap Selection", 120))
			{
				ImGuiLayoutUtils.DrawRowEnum<MiniMap.SelectMode>(true, "Select Mode", p, nameof(PreferencesMiniMap.MiniMapSelectMode), false,
					"How the editor should move when selecting an area outside of the editor range in the mini map."
					+ "\nMove Editor To Cursor:         Move the editor to the cursor, not to the area under the cursor."
					+ "\n                               This is the natural option if you consider the mini map like a scroll bar."
					+ "\nMove Editor To Selected Area:  Move the editor to the area under the cursor, not to the cursor."
					+ "\n                               This is the natural option if you consider the mini map like a map.");

				ImGuiLayoutUtils.EndTable();
			}

			ImGui.Separator();
			if (ImGuiLayoutUtils.BeginTable("MiniMap Spacing", 120))
			{
				ImGuiLayoutUtils.DrawRowEnum<Editor.SpacingMode>(true, "Variable Mode", p, nameof(PreferencesMiniMap.MiniMapSpacingModeForVariable), PreferencesMiniMap.MiniMapVariableSpacingModes, false,
					"The Spacing Mode the MiniMap should use when the Scroll Spacing Mode is Variable.");

				ImGuiLayoutUtils.DrawRowSliderUInt(true, "Constant Time Range", p, nameof(PreferencesMiniMap.MiniMapVisibleTimeRange), 30, 300, false,
					"%i seconds", ImGuiSliderFlags.Logarithmic,
					"The amount of time visible on the mini map when using Constant Time spacing.");

				ImGuiLayoutUtils.DrawRowSliderUInt(true, "Constant Row Range", p, nameof(PreferencesMiniMap.MiniMapVisibleRowRange), 3072, 28800, false,
					"%i rows", ImGuiSliderFlags.Logarithmic,
					"The amount of space visible on the mini map when using Constant Row spacing.");

				ImGuiLayoutUtils.EndTable();
			}

			ImGui.Separator();
			if (ImGuiLayoutUtils.BeginTable("MiniMap Restore", 120))
			{
				if (ImGuiLayoutUtils.DrawRowButton("Restore Defaults", "Restore Defaults",
						"Restore all mini map preferences to their default values."))
				{
					p.RestoreDefaults();
				}
				ImGuiLayoutUtils.EndTable();
			}
		}
	}
}

using System.Numerics;
using ImGuiNET;

namespace StepManiaEditor
{
	/// <summary>
	/// Class for drawing WaveForm preferences UI.
	/// </summary>
	public class UIWaveFormPreferences
	{
		public enum SparseColorOption
		{
			DarkerDenseColor,
			SameAsDenseColor,
			UniqueColor
		}

		private readonly Editor Editor;
		private readonly MusicManager MusicManager;

		public UIWaveFormPreferences(Editor editor, MusicManager musicManager)
		{
			Editor = editor;
			MusicManager = musicManager;
		}

		public void Draw()
		{
			var p = Preferences.Instance.PreferencesWaveForm;
			if (!p.ShowWaveFormPreferencesWindow)
				return;

			ImGui.SetNextWindowSize(new Vector2(0, 0), ImGuiCond.FirstUseEver);
			ImGui.Begin("Waveform Preferences", ref p.ShowWaveFormPreferencesWindow, ImGuiWindowFlags.NoScrollbar);

			if (ImGuiLayoutUtils.BeginTable("Show Waveform", 120))
			{
				ImGuiLayoutUtils.DrawRowCheckbox(true, "Show Waveform", p, nameof(PreferencesWaveForm.ShowWaveForm), false,
					"Whether to show the waveform." +
					"\nDisabling the waveform will increase performance.");
				ImGuiLayoutUtils.EndTable();
			}

			ImGui.Separator();
			if (ImGuiLayoutUtils.BeginTable("Waveform", 120))
			{
				ImGuiLayoutUtils.DrawRowCheckbox(true, "Scale Width", p,
					nameof(PreferencesWaveForm.WaveFormScaleXWhenZooming), false,
					"When zooming, whether the waveform should scale its width to match" +
					"\nthe chart instead of staying a constant width.");

				ImGuiLayoutUtils.DrawRowSliderFloat(true, "Channel Width", p,
					nameof(PreferencesWaveForm.WaveFormMaxXPercentagePerChannel),
					0.0f, 1.0f, false, "Width scale of each channel in the waveform.");

				ImGuiLayoutUtils.DrawRowColorEdit4(true, "Background Color", p,
					nameof(PreferencesWaveForm.WaveFormBackgroundColor), ImGuiColorEditFlags.AlphaPreviewHalf | ImGuiColorEditFlags.AlphaBar, false,
					"Color for the background of the waveform.");

				ImGuiLayoutUtils.DrawRowColorEdit4(true, "Dense Color", p,
					nameof(PreferencesWaveForm.WaveFormDenseColor), ImGuiColorEditFlags.AlphaPreviewHalf | ImGuiColorEditFlags.AlphaBar, false,
					"Color for the dense area of the waveform." +
					"\nFor each y pixel in the waveform, the dense area represents the total magnitude of" +
					"\nof the wave's travel during the samples at that pixel.");

				ImGuiLayoutUtils.DrawRowSliderFloat(true, "Dense Region Scale", p,
					nameof(PreferencesWaveForm.DenseScale),
					0.0f, 10.0f, false, "Scale of the dense region of the waveform.");

				ImGuiLayoutUtils.DrawRowEnum<SparseColorOption>(true, "Sparse Color", p, nameof(PreferencesWaveForm.WaveFormSparseColorOption), false,
					"How to color the sparse area of the waveform." +
					"\nFor each y pixel in the waveform, the sparse area is the range of all" +
					"\nsamples at that pixel.");

				switch (p.WaveFormSparseColorOption)
				{
					case SparseColorOption.DarkerDenseColor:
					{
						ImGuiLayoutUtils.DrawRowSliderFloat(true, "Sparse Color Scale", p,
							nameof(PreferencesWaveForm.WaveFormSparseColorScale),
							0.0f, 1.0f, false,
							"Treat the sparse color as the dense color darkened by this percentage.");
						break;
					}
					case SparseColorOption.SameAsDenseColor:
					{
						break;
					}
					case SparseColorOption.UniqueColor:
					{
						ImGuiLayoutUtils.DrawRowColorEdit4(true, "Sparse Color", p,
							nameof(PreferencesWaveForm.WaveFormSparseColor), ImGuiColorEditFlags.AlphaPreviewHalf | ImGuiColorEditFlags.AlphaBar, false,
							"Color for the sparse area of the waveform." +
							"\nFor each y pixel in the waveform, the sparse area represents the range of all" +
							"\nsamples at that pixel.");
						break;
					}
				}

				if (ImGuiLayoutUtils.DrawRowSliderInt(true, "Loading Parallelism", p,
					    nameof(PreferencesWaveForm.WaveFormLoadingMaxParallelism), 1, 128, false,
						"Number of threads to use for loading the waveform." +
					    "\nSetting this to a low value will result in slower waveform loads." +
					    "\nSetting this to a high value will result in faster waveform up loads up to the point your" +
					    "\nCPU is saturated, at which point performance will degrade."))
				{
					MusicManager.GetMusicMipMap().SetLoadParallelism(p.WaveFormLoadingMaxParallelism);
				}

				ImGuiLayoutUtils.EndTable();
			}

			ImGui.Separator();
			if (ImGuiLayoutUtils.BeginTable("Waveform Antialiasing", 120))
			{
				ImGuiLayoutUtils.DrawRowCheckbox("Antialias", ref p.AntiAlias,
					"Whether or not to use FXAA.");
				
				ImGuiLayoutUtils.DrawRowSliderFloat(true, "Subpix", p, nameof(PreferencesWaveForm.AntiAliasSubpix), 0.0f, 1.0f, false,
					"Amount of sub-pixel aliasing removal." +
					"\nThis can effect sharpness." +
					"\n   1.00 - upper limit (softer)" +
					"\n   0.75 - default amount of filtering" +
					"\n   0.50 - lower limit (sharper, less sub-pixel aliasing removal)" +
					"\n   0.25 - almost off" +
					"\n   0.00 - completely off");
				ImGuiLayoutUtils.DrawRowSliderFloat(true, "Edge Threshold", p, nameof(PreferencesWaveForm.AntiAliasEdgeThreshold), 0.0f, 1.0f, false,
					"The minimum amount of local contrast required to apply algorithm." +
					"\n   0.333 - too little (faster)" +
					"\n   0.250 - low quality" +
					"\n   0.166 - default" +
					"\n   0.125 - high quality " +
					"\n   0.063 - overkill (slower)");
				ImGuiLayoutUtils.DrawRowSliderFloat(true, "Edge Threshold Min", p, nameof(PreferencesWaveForm.AntiAliasEdgeThresholdMin), 0.0f, 1.0f, false,
					"Trims the algorithm from processing darks." +
					"\n   0.0833 - upper limit (default, the start of visible unfiltered edges)" +
					"\n   0.0625 - high quality (faster)" +
					"\n   0.0312 - visible limit (slower)");

				ImGuiLayoutUtils.EndTable();
			}

			ImGui.Separator();
			if (ImGuiLayoutUtils.BeginTable("Waveform Restore", 120))
			{
				if (ImGuiLayoutUtils.DrawRowButton("Restore Defaults", "Restore Defaults",
					    "Restore all waveform preferences to their default values."))
				{
					p.RestoreDefaults();
				}
				ImGuiLayoutUtils.EndTable();
			}

			ImGui.End();
		}
	}
}

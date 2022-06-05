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
				ImGuiLayoutUtils.DrawRowCheckboxUndoable("Show Waveform", p, nameof(PreferencesWaveForm.ShowWaveForm),
					"Whether to show the waveform." +
					"\nDisabling the waveform will increase performance.");
				ImGuiLayoutUtils.EndTable();
			}

			ImGui.Separator();
			if (ImGuiLayoutUtils.BeginTable("Waveform", 120))
			{
				ImGuiLayoutUtils.DrawRowCheckboxUndoable("Scale Width", p,
					nameof(PreferencesWaveForm.WaveFormScaleXWhenZooming),
					"When zooming, whether the waveform should scale its width to match" +
					"\nthe chart instead of staying a constant width.");

				ImGuiLayoutUtils.DrawRowSliderFloat(true, "Channel Width", p,
					nameof(PreferencesWaveForm.WaveFormMaxXPercentagePerChannel),
					0.0f, 1.0f, "Width scale of each channel in the waveform.");

				ImGuiLayoutUtils.DrawRowColorEdit3(true, "Dense Color", p,
					nameof(PreferencesWaveForm.WaveFormDenseColor), ImGuiColorEditFlags.NoAlpha,
					"Color for the dense area of the waveform." +
					"\nFor each y pixel in the waveform, the dense area represents the root mean square" +
					"\nof all samples at that pixel.");

				ImGuiLayoutUtils.DrawRowEnum<SparseColorOption>(true, "Sparse Color", p, nameof(PreferencesWaveForm.WaveFormSparseColorOption),
					"How to color the sparse area of the waveform." +
					"\nFor each y pixel in the waveform, the sparse area is the range of all" +
					"\nsamples at that pixel.");

				switch (p.WaveFormSparseColorOption)
				{
					case SparseColorOption.DarkerDenseColor:
					{
						ImGuiLayoutUtils.DrawRowSliderFloat(true, "Sparse Color Scale", p,
							nameof(PreferencesWaveForm.WaveFormSparseColorScale),
							0.0f, 1.0f,
							"Treat the sparse color as the dense color darkened by this percentage.");
						break;
					}
					case SparseColorOption.SameAsDenseColor:
					{
						break;
					}
					case SparseColorOption.UniqueColor:
					{
						ImGuiLayoutUtils.DrawRowColorEdit3(true, "Sparse Color", p,
							nameof(PreferencesWaveForm.WaveFormSparseColor), ImGuiColorEditFlags.NoAlpha,
							"Color for the sparse area of the waveform." +
							"\nFor each y pixel in the waveform, the sparse area represents the range of all" +
							"\nsamples at that pixel.");
						break;
					}
				}

				if (ImGuiLayoutUtils.DrawRowSliderInt(true, "Loading Parallelism", p,
					    nameof(PreferencesWaveForm.WaveFormLoadingMaxParallelism), 1, 128,
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

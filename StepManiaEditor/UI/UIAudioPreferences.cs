using Fumen;
using ImGuiNET;
using static StepManiaEditor.ImGuiUtils;

namespace StepManiaEditor;

/// <summary>
/// Class for drawing audio control UI.
/// </summary>
internal sealed class UIAudioPreferences
{
	public const string WindowTitle = "Audio Preferences";

	private static readonly int TitleColumnWidth = UiScaled(160);
	private static readonly int DefaultWidth = UiScaled(460);

	private readonly SoundManager SoundManager;

	public UIAudioPreferences(SoundManager soundManager)
	{
		SoundManager = soundManager;
	}

	public void Draw()
	{
		var p = Preferences.Instance.PreferencesAudio;
		if (!p.ShowAudioPreferencesWindow)
			return;

		if (BeginWindow(WindowTitle, ref p.ShowAudioPreferencesWindow, DefaultWidth))
		{
			if (ImGuiLayoutUtils.BeginTable("Audio", TitleColumnWidth))
			{
				ImGuiLayoutUtils.DrawRowDragDouble(true, "Music Rate", p, nameof(PreferencesAudio.MusicRate), false,
					"Music Rate."
					+ "\nThe music rate can also be adjusted with Shift+Left and Shift+Right",
					0.001f, "%.3fx", MusicManager.MinMusicRate, MusicManager.MaxMusicRate);

				ImGuiLayoutUtils.DrawRowSliderFloat(true, "Main Volume", p, nameof(PreferencesAudio.MainVolume),
					0.0f, 1.0f,
					false,
					"Volume of all audio.");
				ImGuiLayoutUtils.DrawRowSliderFloat(true, "Song Volume", p, nameof(PreferencesAudio.MusicVolume),
					0.0f, 1.0f,
					false,
					"Volume of the music.");
				ImGuiLayoutUtils.DrawRowDragDouble(true, "Preview Fade In Time", p,
					nameof(PreferencesAudio.PreviewFadeInTime),
					false,
					"Time over which the preview should fade in when previewing the song.", 0.001f, "%.3f seconds", 0.0);
				ImGuiLayoutUtils.DrawRowDragDouble(true, "Preview Fade Out Time", p,
					nameof(PreferencesAudio.PreviewFadeOutTime),
					false,
					"Time over which the preview should fade out when previewing the song.", 0.001f, "%.3f seconds", 0.0);

				ImGuiLayoutUtils.EndTable();
			}

			ImGui.Separator();
			if (ImGuiLayoutUtils.BeginTable("Assist Tick Table", TitleColumnWidth))
			{
				ImGuiLayoutUtils.DrawRowCheckbox(true, "Assist Tick", p, nameof(PreferencesAudio.UseAssistTick),
					false,
					"Whether or not to use assist tick.");
				ImGuiLayoutUtils.DrawRowCheckbox(true, "Skip on Beat Tick", p, nameof(PreferencesAudio.SkipAssistTickOnBeatTick),
					false,
					"Whether or not to skip playing assist tick sounds when a beat tick sound plays.");
				ImGuiLayoutUtils.DrawRowSliderFloat(true, "Assist Tick Volume", p,
					nameof(PreferencesAudio.AssistTickVolume),
					0.0f, 1.0f, false,
					"Volume of assist ticks.");
				ImGuiLayoutUtils.DrawRowDragFloat(true, "Assist Tick Attack Time", p,
					nameof(PreferencesAudio.AssistTickAttackTime), false,
					"Attack time in seconds of the assist tick sound."
					+ "\nAttack time is the time from the start of the sound file to the point at which a listener would"
					+ "\nconsider it to start. This should not be modified unless you change the assist tick file to use"
					+ "\na different sound.",
					0.0001f, "%.6f seconds", 0.0f);

				ImGuiLayoutUtils.EndTable();
			}

			ImGui.Separator();
			if (ImGuiLayoutUtils.BeginTable("Beat Tick Table", TitleColumnWidth))
			{
				ImGuiLayoutUtils.DrawRowCheckbox(true, "Beat Tick", p, nameof(PreferencesAudio.UseBeatTick),
					false,
					"Whether or not to use beat tick.");
				ImGuiLayoutUtils.DrawRowCheckbox(true, "Skip on Assist Tick", p,
					nameof(PreferencesAudio.SkipBeatTickOnAssistTick),
					false,
					"Whether or not to skip playing beat tick sounds when an assist tick sound plays.");
				ImGuiLayoutUtils.DrawRowSliderFloat(true, "Beat Tick Volume", p,
					nameof(PreferencesAudio.BeatTickVolume),
					0.0f, 1.0f, false,
					"Volume of beat ticks.");
				ImGuiLayoutUtils.DrawRowDragFloat(true, "Beat Tick Attack Time", p,
					nameof(PreferencesAudio.BeatTickAttackTime), false,
					"Attack time in seconds of the beat tick sound."
					+ "\nAttack time is the time from the start of the sound file to the point at which a listener would"
					+ "\nconsider it to start. This should not be modified unless you change the beat tick file to use"
					+ "\na different sound.",
					0.0001f, "%.6f seconds", 0.0f);

				ImGuiLayoutUtils.EndTable();
			}

			ImGui.Separator();
			if (ImGuiLayoutUtils.BeginTable("Advanced Audio", TitleColumnWidth))
			{
				ImGuiLayoutUtils.DrawRowDragDouble(true, "Audio Offset", p, nameof(PreferencesAudio.AudioOffset),
					false,
					$"Offset used when playing songs through {Utils.GetAppName()}."
					+ "\nIf the audio and visuals appear out of sync when playing a song, adjusting this value can"
					+ "\ncompensate for this lag and bring the two in sync."
					+ "\nNote that setting this to a nonzero value will cause the audio to play at an earlier or later"
					+ "\nposition for a given time, which can result in audible differences when starting playback. For"
					+ "\nexample, if the chart's position is exactly at the start of a beat and then playback begins, a"
					+ "\npositive offset will result in the sound starting later than may be expected. As another example,"
					+ "\nif the chart's position is at the start of the preview, and then the chart is played, what is"
					+ "\nheard will not match the actual preview audio precisely when using a nonzero offset."
					+ "\nPlease do note however that playing the preview through the Song Properties window or with the P"
					+ "\nkey will always result in an accurate preview playback, even if there is an Audio Offset set."
					+ "\nIncreasing this value will cause the audio to play earlier."
					+ "\nDecreasing this value will cause the audio to play later.",
					0.0001f, "%.6f seconds");

				var sampleRate = SoundManager.GetSampleRate();
				var audioLatency = (int)((p.DspNumBuffers - 1.5) * p.DspBufferSize / sampleRate *
				                         1000);
				var audioLatencyString =
					$"\nThe current estimated audio latency is ~{audioLatency}ms (({p.DspNumBuffers} - 1.5)buffers * {p.DspBufferSize}samples/buffer / {sampleRate}samples/s).";
				var audioBufferDescription =
					"\nThis value should only be modified if you know what you are doing and have performance/latency needs."
					+ "\nLowering this value too much may cause stuttering and increase CPU demand."
					+ "\nIncreasing this value may cause audio to sound delayed."
					+ $"\nThe sample rate of the application is {sampleRate}hz."
					+ audioLatencyString
					+ "\nChanges to this value take effect after restarting the application.";

				ImGuiLayoutUtils.DrawRowDragInt(true, "Audio Num Buffers", p,
					nameof(PreferencesAudio.DspNumBuffers), false,
					"Number of audio buffers in the audio ring buffer."
					+ audioBufferDescription,
					0.1f,
					"%i buffers",
					2, 32);
				ImGuiLayoutUtils.DrawRowDragInt(true, "Audio Buffer Size", p,
					nameof(PreferencesAudio.DspBufferSize), false,
					"Number of audio buffers in the audio ring buffer."
					+ "\nUnits are samples."
					+ audioBufferDescription,
					1.0f,
					"%i samples",
					32, 8192);

				ImGuiLayoutUtils.EndTable();
			}
		}

		ImGui.Separator();
		if (ImGuiLayoutUtils.BeginTable("Audio Preferences Restore", TitleColumnWidth))
		{
			if (ImGuiLayoutUtils.DrawRowButton("Restore Defaults", "Restore Defaults",
				    "Restore all audio preferences to their default values."))
			{
				p.RestoreDefaults();
			}

			ImGuiLayoutUtils.EndTable();
		}

		ImGui.End();
	}
}

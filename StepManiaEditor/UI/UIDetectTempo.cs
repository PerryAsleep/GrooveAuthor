using System;
using System.Diagnostics;
using System.Numerics;
using System.Threading;
using ImGuiNET;
using StepManiaLibrary;
using static StepManiaEditor.ImGuiUtils;

namespace StepManiaEditor;

internal sealed class UIDetectTempo
{
	public const string WindowTitle = "Detect Tempo";

	private static readonly int TitleColumnWidth = UiScaled(142);
	private static readonly int DefaultWidth = UiScaled(560);
	private static readonly int DeleteWidth = UiScaled(40);
	private static readonly int ApplyWidth = UiScaled(40);
	private static readonly int PlotHeight = UiScaled(100);

	private readonly Editor Editor;
	private readonly MusicManager MusicManager;

	/// <summary>
	/// Whether or not this window is showing.
	/// This state is tracked internally and not persisted.
	/// </summary>
	private bool Showing;

	public UIDetectTempo(Editor editor, MusicManager musicManager)
	{
		Editor = editor;
		MusicManager = musicManager;
	}

	/// <summary>
	/// Show this UI with the given EditorChart as the source EditorChart for autogeneration.
	/// </summary>
	public void Show()
	{
		Showing = true;
	}

	/// <summary>
	/// Close this UI if it is showing.
	/// </summary>
	public void Close()
	{
		Showing = false;
	}

	public void Draw()
	{
		if (!Showing)
			return;

		if (BeginWindow(WindowTitle, ref Showing, DefaultWidth, ImGuiWindowFlags.NoCollapse))
		{
			var p = Preferences.Instance.PreferencesTempoDetection;

			var tempoResults = MusicManager.GetMusicTempo();
			var musicFileName = MusicManager.GetMusicFileName();
			var song = Editor.GetActiveSong();

			if (ImGui.CollapsingHeader("Advanced Configuration"))
			{
				ImGui.TextWrapped(
					"Tempo detection works by sampling the music at various times. For each sample window, the sound is decomposed "
					+ "into frequency bands. These bands are enveloped, and then for each one a comb filter is applied for all "
					+ "tempos in the search range. The tempos producing the greatest correlations are returned as the best tempos.");
				ImGui.Separator();

				if (ImGuiLayoutUtils.BeginTable("DetectTempoParameters", TitleColumnWidth))
				{
					ImGuiLayoutUtils.DrawRowDragDouble(true, "Min Tempo", p, nameof(PreferencesTempoDetection.MinTempo), false,
						"The minimum tempo for the search range.",
						0.01f, "%.1f bpm", 0.0, 400.0);
					ImGuiLayoutUtils.DrawRowDragDouble(true, "Max Tempo", p, nameof(PreferencesTempoDetection.MaxTempo), false,
						"The maximum tempo for the search range.",
						0.01f, "%.1f bpm", 0.0, 400.0);
					ImGuiLayoutUtils.DrawRowDragInt(true, "Num Results", p, nameof(PreferencesTempoDetection.NumTemposToFind),
						false,
						"The number of best tempos to return.",
						0.1f, "%i", 1, 100);
					ImGuiLayoutUtils.DrawRowDragDouble(true, "Min Tempo Separation", p,
						nameof(PreferencesTempoDetection.MinSeparationBetweenBestTempos), false,
						"When finding tempos with high comb filter correlations, don't return tempos which aren't separated by at least "
						+ "this many beats per minute. This is useful because if, for example, the actual tempo is 100bpm, 99.9bpm will "
						+ "also be highly correlated but it should be ignored since it is only high due to its proximity to the actual tempo.",
						0.01f, "%.1f bpm", 0.0, 400.0);
					ImGuiLayoutUtils.DrawRowDragDouble(true, "Window Time", p, nameof(PreferencesTempoDetection.WindowTime),
						false,
						"Sample window time. Generally, the larger this window is the more accurate results will be. Larger windows take "
						+ "more time to process.",
						0.01f, "%.2fs", 0.0, 60.0);
					ImGuiLayoutUtils.DrawRowDragDouble(true, "Comb Filter Resolution", p,
						nameof(PreferencesTempoDetection.CombFilterResolution), false,
						"Comb filter resolution in beats per minute. Finer resolutions will catch more precise non-integer tempos.",
						0.001f, "%.2f bpm", 0.001, 1.0);
					ImGuiLayoutUtils.DrawRowDragDouble(true, "Comb Filter Beats", p,
						nameof(PreferencesTempoDetection.CombFilterBeats), false,
						"When correlating the signal with itself with a comb filter, how many beats to compare.",
						0.001f, "%.2f", 0.1, 16.0);
					ImGuiLayoutUtils.DrawRowDragDouble(true, "Envelope Attack Percent", p,
						nameof(PreferencesTempoDetection.EnvelopeAttackPercent), false,
						".", 0.01f, "%.4fs", 0.0, 1.0);
					ImGuiLayoutUtils.DrawRowDragDouble(true, "Envelope Release Percent", p,
						nameof(PreferencesTempoDetection.EnvelopeReleasePercent), false,
						".", 0.01f, "%.4fs", 0.0, 1.0);

					ImGuiLayoutUtils.DrawTitle("Frequency Bands",
						"Frequency bands to decompose the music into for analysis. Frequency bands are specified by a low and high Hz range. "
						+ "Each band has a weight associated with it for treating some bands as more significant than others.");
					var width = ImGui.GetContentRegionAvail().X;
					if (ImGui.BeginTable("Frequency Band Table", 1, ImGuiTableFlags.None, new Vector2(width, 0), width))
					{
						ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthStretch, 100.0f);
						ImGui.TableNextRow();
						ImGui.TableSetColumnIndex(0);

						var indexToDelete = -1;
						if (ImGui.BeginTable("Frequency Band Table Inner", 4, ImGuiTableFlags.Borders))
						{
							ImGui.TableSetupColumn("Low", ImGuiTableColumnFlags.WidthStretch, 100);
							ImGui.TableSetupColumn("High", ImGuiTableColumnFlags.WidthStretch, 100);
							ImGui.TableSetupColumn("Weight", ImGuiTableColumnFlags.WidthStretch, 100);
							ImGui.TableSetupColumn("Delete", ImGuiTableColumnFlags.WidthFixed, DeleteWidth);
							ImGui.TableHeadersRow();

							var index = 0;
							foreach (var b in p.FrequencyBands)
							{
								ImGui.TableNextRow();

								ImGui.TableNextColumn();
								ImGuiLayoutUtils.DrawDragInt(true, $"FrequencyBandLow##{index}", b,
									nameof(TempoDetector.FrequencyBand.Low), ImGui.GetContentRegionAvail().X, false, null, 1.0f,
									"%iHz", 0, 20000);

								ImGui.TableNextColumn();
								ImGuiLayoutUtils.DrawDragInt(true, $"FrequencyBandHigh##{index}", b,
									nameof(TempoDetector.FrequencyBand.High), ImGui.GetContentRegionAvail().X, false, null, 1.0f,
									"%iHz", 0, 20000);

								ImGui.TableNextColumn();
								ImGuiLayoutUtils.DrawDragInt(true, $"FrequencyBandWeight##{index}", b,
									nameof(TempoDetector.FrequencyBand.Weight), ImGui.GetContentRegionAvail().X, false, null,
									1.0f, "%i", 0, 100);

								ImGui.TableNextColumn();
								if (ImGui.Button($"Delete##FrequencyBand{index}"))
								{
									indexToDelete = index;
								}

								index++;
							}

							ImGui.EndTable();
						}

						if (indexToDelete >= 0)
						{
							ActionQueue.Instance.Do(new ActionRemoveFromObjectFieldOrPropertyList<TempoDetector.FrequencyBand>(p,
								nameof(PreferencesTempoDetection.FrequencyBands), indexToDelete, false));
						}

						ImGui.TableNextRow();
						ImGui.TableSetColumnIndex(0);
						if (ImGui.Button("Add##FrequencyBand"))
						{
							ActionQueue.Instance.Do(new ActionAddToObjectFieldOrPropertyList<TempoDetector.FrequencyBand>(p,
								nameof(PreferencesTempoDetection.FrequencyBands), new TempoDetector.FrequencyBand(), false));
						}

						ImGui.EndTable();
					}

					ImGuiLayoutUtils.DrawTitle("Measurement Locations",
						"Locations within the song where sample windows should be taken. Locations may be specified by time relative the song "
						+ "start or end, or as a percentage into the total song.");
					width = ImGui.GetContentRegionAvail().X;
					if (ImGui.BeginTable("Measurement Location Table", 1, ImGuiTableFlags.None, new Vector2(width, 0), width))
					{
						ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthStretch, 100.0f);
						ImGui.TableNextRow();
						ImGui.TableSetColumnIndex(0);

						var indexToDelete = -1;
						if (ImGui.BeginTable("Measurement Location Table Inner", 4, ImGuiTableFlags.Borders))
						{
							ImGui.TableSetupColumn("Location", ImGuiTableColumnFlags.WidthStretch, 100);
							ImGui.TableSetupColumn("Time (Abs)", ImGuiTableColumnFlags.WidthStretch, 100);
							ImGui.TableSetupColumn("Time (Percent)", ImGuiTableColumnFlags.WidthStretch, 100);
							ImGui.TableSetupColumn("Delete", ImGuiTableColumnFlags.WidthFixed, DeleteWidth);
							ImGui.TableHeadersRow();

							var index = 0;
							foreach (var l in p.MeasurementLocations)
							{
								ImGui.TableNextRow();

								ImGui.TableNextColumn();
								ImGuiLayoutUtils.DrawEnum<TempoDetector.LocationType>(true, $"MeasurementLocationLoc##{index}", l,
									nameof(TempoDetector.Location.Type), ImGui.GetContentRegionAvail().X, null, false);

								ImGui.TableNextColumn();
								if (l.Type == TempoDetector.LocationType.RelativeToStart ||
								    l.Type == TempoDetector.LocationType.RelativeToEnd)
									ImGuiLayoutUtils.DrawDragDouble(true, $"MeasurementLocationTimeAbs##{index}", l,
										nameof(TempoDetector.Location.Time), ImGui.GetContentRegionAvail().X, null, 1.0f, "%fs",
										false, 0.0, 3600.0);

								ImGui.TableNextColumn();
								if (l.Type == TempoDetector.LocationType.Percentage)
									ImGuiLayoutUtils.DrawDragDouble(true, $"MeasurementLocationTimeRel##{index}", l,
										nameof(TempoDetector.Location.Percentage), ImGui.GetContentRegionAvail().X, null, 1.0f,
										"%f", false, 0.0, 1.0);

								ImGui.TableNextColumn();
								if (ImGui.Button($"Delete##MeasurementLocations{index}"))
								{
									indexToDelete = index;
								}

								index++;
							}

							ImGui.EndTable();
						}

						if (indexToDelete >= 0)
						{
							ActionQueue.Instance.Do(new ActionRemoveFromObjectFieldOrPropertyList<TempoDetector.Location>(p,
								nameof(PreferencesTempoDetection.MeasurementLocations), indexToDelete, false));
						}

						ImGui.TableNextRow();
						ImGui.TableSetColumnIndex(0);
						if (ImGui.Button("Add##MeasurementLocations"))
						{
							ActionQueue.Instance.Do(new ActionAddToObjectFieldOrPropertyList<TempoDetector.Location>(p,
								nameof(PreferencesTempoDetection.MeasurementLocations), new TempoDetector.Location(), false));
						}

						ImGui.EndTable();
					}

					DrawRowDebugWriteWavs(p);

					ImGuiLayoutUtils.EndTable();
				}

				ImGui.Separator();
				if (ImGuiLayoutUtils.BeginTable("Tempo Detection Preferences Restore", TitleColumnWidth))
				{
					if (ImGuiLayoutUtils.DrawRowButton("Restore Defaults", "Restore Defaults",
						    "Restore all tempo detection preferences to their default values."))
					{
						p.RestoreDefaults();
					}

					ImGuiLayoutUtils.EndTable();
				}

				ImGui.Separator();
				if (ImGui.CollapsingHeader("Result Plots"))
				{
					if (tempoResults != null)
					{
						var i = 0;
						foreach (var result in tempoResults.GetResultsByLocation())
						{
							ImGui.Separator();
							ImGui.Text($"Results for window {GetLocationString(result.GetLocation())}");
							if (ImGuiLayoutUtils.BeginTable($"DetectTempoResultsByLocation##{i}", TitleColumnWidth))
							{
								ImGuiLayoutUtils.DrawRowPlot(
									"Tempo Correlations (Normalized)",
									ref result.GetNormalizedCorrelations()[0],
									result.GetNormalizedCorrelations().Length,
									"",
									1.0f,
									PlotHeight,
									4,
									"Visual representation of tempo correlations."
								);

								// REMOVE THIS
								var min = float.MaxValue;
								var max = float.MinValue;
								var scaledCorrelations = new float[result.GetCorrelations().Length];
								foreach (var correlation in result.GetCorrelations())
								{
									min = Math.Min(correlation, min);
									max = Math.Max(correlation, max);
								}

								var j = 0;
								foreach (var correlation in result.GetCorrelations())
								{
									scaledCorrelations[j] = (correlation - min) / (max - min);
									j++;
								}

								ImGuiLayoutUtils.DrawRowPlot(
									"Tempo Correlations",
									ref scaledCorrelations[0],
									scaledCorrelations.Length,
									"",
									1.0f,
									PlotHeight,
									4,
									"Visual representation of tempo correlations."
								);

								ImGuiLayoutUtils.EndTable();
							}

							i++;
						}
					}
					else
					{
						ImGui.Text("Run Detect Tempo to produce plots.");
					}
				}
			}

			ImGui.Separator();
			if (ImGuiLayoutUtils.BeginTable("DetectTempoResults", TitleColumnWidth))
			{
				var help = "Best tempo results, sorted best to worst.";
				if (!string.IsNullOrEmpty(musicFileName))
				{
					var fileName = System.IO.Path.GetFileName(musicFileName);
					if (!string.IsNullOrEmpty(fileName))
					{
						help = $"Best tempo results for {fileName}, sorted best to worst.";
					}
				}

				ImGuiLayoutUtils.DrawTitle("Best Tempos", help);
				var width = ImGui.GetContentRegionAvail().X;
				if (ImGui.BeginTable("DetectTempoResultsTable", 1, ImGuiTableFlags.None, new Vector2(width, 0), width))
				{
					ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthStretch, 100.0f);
					ImGui.TableNextRow();
					ImGui.TableSetColumnIndex(0);

					if (ImGui.BeginTable("DetectTempoResultsTableInner", 4, ImGuiTableFlags.Borders))
					{
						ImGui.TableSetupColumn("Tempo", ImGuiTableColumnFlags.WidthStretch, 100);
						ImGui.TableSetupColumn("Factor", ImGuiTableColumnFlags.WidthStretch, 100);
						ImGui.TableSetupColumn("Correlation", ImGuiTableColumnFlags.WidthStretch, 100);
						ImGui.TableSetupColumn("Apply", ImGuiTableColumnFlags.WidthFixed, ApplyWidth);
						ImGui.TableHeadersRow();

						if (tempoResults != null)
						{
							var i = 0;
							var bestTempo = tempoResults.GetBestTempo();
							foreach (var tempo in tempoResults.GetBestTempos())
							{
								var highlightRow = i == tempoResults.GetBestTempoIndex();
								ImGui.TableNextRow();

								ImGui.TableNextColumn();
								if (highlightRow)
									ImGui.TableSetBgColor(ImGuiTableBgTarget.CellBg, Utils.UIHighlightedCellColor);
								ImGui.Text($"{tempo.GetTempo():N2} bpm");

								ImGui.TableNextColumn();
								if (highlightRow)
									ImGui.TableSetBgColor(ImGuiTableBgTarget.CellBg, Utils.UIHighlightedCellColor);
								ImGui.Text($"{tempo.GetTempo() / bestTempo:N2}");

								ImGui.TableNextColumn();
								if (highlightRow)
									ImGui.TableSetBgColor(ImGuiTableBgTarget.CellBg, Utils.UIHighlightedCellColor);
								ImGui.Text($"{tempo.GetCorrelation():N6}");

								ImGui.TableNextColumn();
								if (highlightRow)
									ImGui.TableSetBgColor(ImGuiTableBgTarget.CellBg, Utils.UIHighlightedCellColor);
								if (ImGui.Button($"Apply##{i}"))
								{
									tempoResults.SelectBestTempoIndex(i);
									ActionQueue.Instance.Do(new ActionSetTempo(song, musicFileName, tempo.GetTempo()));
								}

								i++;
							}
						}
						else
						{
							PushDisabled();
							for (var t = 0; t < p.NumTemposToFind; t++)
							{
								ImGui.TableNextRow();
								ImGui.TableNextColumn();
								ImGui.TableNextColumn();
								ImGui.TableNextColumn();
								ImGui.TableNextColumn();
								ImGui.Button("Apply");
							}

							PopDisabled();
						}

						ImGui.EndTable();
					}

					ImGui.EndTable();
				}

				ImGuiLayoutUtils.EndTable();
			}

			ImGui.Separator();
			var detecting = MusicManager.IsDetectingMusicTempo();
			var detectDisabled = Editor.GetActiveSong() == null || detecting || !MusicManager.CanDetectMusicTempo();
			if (detectDisabled)
				PushDisabled();
			if (ImGui.Button("Detect Tempo"))
			{
				DetectTempo();
			}

			if (detectDisabled)
				PopDisabled();
			if (detecting)
			{
				ImGui.SameLine();
				ImGui.Text("Detecting tempo...");
			}
		}
		else
		{
			Close();
		}

		ImGui.End();
	}

	[Conditional("DEBUG")]
	private static void DrawRowDebugWriteWavs(PreferencesTempoDetection p)
	{
		ImGuiLayoutUtils.DrawRowCheckbox(true, "Write Debug Wavs", p,
			nameof(PreferencesTempoDetection.WriteDebugWavs), false,
			"Whether or not to write debug wav files as part of the tempo detection process.");
	}

	private static string GetLocationString(TempoDetector.Location location)
	{
		switch (location.Type)
		{
			case TempoDetector.LocationType.RelativeToStart:
				return $"{location.Time}s from start";
			case TempoDetector.LocationType.RelativeToEnd:
				return $"{location.Time}s from end";
			case TempoDetector.LocationType.Percentage:
				return $"{(int)(location.Percentage * 100)}%% into song";
		}

		return null;
	}

	private async void DetectTempo()
	{
		await MusicManager.DetectMusicTempo(new CancellationToken());
	}
}

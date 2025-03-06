using System;
using System.Numerics;
using Fumen.Converters;
using ImGuiNET;
using static StepManiaEditor.ImGuiUtils;
using static Fumen.FumenExtensions;
using static StepManiaEditor.Editor;

namespace StepManiaEditor;

/// <summary>
/// Class for drawing quick hotbar-style controls and information about the current Position within the focused Chart.
/// </summary>
internal sealed class UIHotbar : UIWindow
{
	private const uint ColorTextGrey = 0xFF777777;
	private const uint ColorBgDarkGrey = 0xF0222222;

	public static readonly int DefaultWidth = UiScaled(768);
	public static readonly int DefaultHeight = UiScaled(200);

	private static readonly int ColumnWidthTitleSpacing = UiScaled(71);
	private static readonly int ColumnWidthTitleSnap = UiScaled(58);
	private static readonly int ColumnWidthValueSnap = UiScaled(110);
	private static readonly int ColumnWidthTitleUI = UiScaled(77);
	private static readonly int ColumnWidthValueUI = UiScaled(108);
	private static readonly int ColumnWidthTitleSteps = UiScaled(40);
	private static readonly int ColumnWidthValueSteps = UiScaled(47);

	private static readonly int TotalColumnWidth = UiScaled(524);
	private static readonly double TableNameColWidthPct = 37.0 / TotalColumnWidth;
	private static readonly double TablePositionColWidthPct = 73.0 / TotalColumnWidth;
	private static readonly double TableSongTimeColWidthPct = 104.0 / TotalColumnWidth;
	private static readonly double TableChartTimeColWidthPct = 104.0 / TotalColumnWidth;
	private static readonly double TableMeasureColWidthPct = 61.0 / TotalColumnWidth;
	private static readonly double TableBeatColWidthPct = 67.0 / TotalColumnWidth;
	private static readonly double TableRowColWidthPct = 78.0 / TotalColumnWidth;

	private Editor Editor;

	public static UIHotbar Instance { get; } = new();

	private UIHotbar() : base("Hotbar")
	{
	}

	public void Init(Editor editor)
	{
		Editor = editor;
	}

	public override void Open(bool focus)
	{
		Preferences.Instance.ShowHotbar = true;
		if (focus)
			Focus();
	}

	public override void Close()
	{
		Preferences.Instance.ShowHotbar = false;
	}

	/// <summary>
	/// Draws the chart position information.
	/// </summary>
	public void Draw()
	{
		if (!Preferences.Instance.ShowHotbar)
			return;

		if (ImGui.Begin(WindowTitle, ref Preferences.Instance.ShowHotbar))
		{
			var pScroll = Preferences.Instance.PreferencesScroll;
			var pKeyBinds = Preferences.Instance.PreferencesKeyBinds;
			var spacing = ImGui.GetStyle().ItemSpacing.X;
			var totalWidth = ImGui.GetContentRegionAvail().X;
			var tableUIWidth = ColumnWidthTitleUI + ColumnWidthValueUI + spacing;
			var tableStepsWidth = ColumnWidthTitleSteps + ColumnWidthValueSteps + spacing;
			var tableSnapWidth = ColumnWidthTitleSnap + ColumnWidthValueSnap + spacing;
			var variableWidthTableWidth = (int)(totalWidth - tableUIWidth - tableStepsWidth - tableSnapWidth - spacing * 3);
			var columnWidthValueSpacing = Math.Max(1, variableWidthTableWidth - spacing - ColumnWidthTitleSpacing);

			if (ImGuiLayoutUtils.BeginTable("Spacing", ColumnWidthTitleSpacing, columnWidthValueSpacing))
			{
				// Spacing mode.
				UIScrollPreferences.DrawSpacingModeRow("Spacing Mode", true);

				// Spacing.
				var spacingHelperText = $"\n\n{UIScrollPreferences.GetSpacingHelpText()}";
				switch (pScroll.SpacingMode)
				{
					case SpacingMode.ConstantRow:
					{
						ImGuiLayoutUtils.DrawRowSliderFloatWithReset(
							false,
							"Spacing",
							pScroll,
							nameof(PreferencesScroll.RowBasedPixelsPerRowFloat),
							(float)ZoomManager.MinConstantRowSpacing,
							(float)ZoomManager.MaxConstantRowSpacing,
							(float)PreferencesScroll.DefaultRowBasedPixelsPerRow,
							false,
							"Spacing in pixels per row at default zoom level." + spacingHelperText,
							"%.3f",
							ImGuiSliderFlags.Logarithmic);
						break;
					}
					case SpacingMode.ConstantTime:
					{
						ImGuiLayoutUtils.DrawRowSliderFloatWithReset(
							false,
							"Spacing",
							pScroll,
							nameof(PreferencesScroll.TimeBasedPixelsPerSecondFloat),
							(float)ZoomManager.MinConstantTimeSpeed,
							(float)ZoomManager.MaxConstantTimeSpeed,
							(float)PreferencesScroll.DefaultTimeBasedPixelsPerSecond,
							false,
							"Speed in pixels per second at default zoom level." + spacingHelperText,
							"%.3f",
							ImGuiSliderFlags.Logarithmic);
						break;
					}
					case SpacingMode.Variable:
					{
						ImGuiLayoutUtils.DrawRowSliderFloatWithReset(
							false,
							"Spacing",
							pScroll,
							nameof(PreferencesScroll.VariablePixelsPerSecondAtDefaultBPMFloat),
							(float)ZoomManager.MinVariableSpeed,
							(float)ZoomManager.MaxVariableSpeed,
							(float)PreferencesScroll.DefaultVariablePixelsPerSecondAtDefaultBPM,
							false,
							$"Speed in pixels per second at default zoom level at {PreferencesScroll.DefaultVariableSpeedBPM} BPM." +
							spacingHelperText,
							"%.3f",
							ImGuiSliderFlags.Logarithmic);
						break;
					}
				}

				// Zoom level.
				// ImGUI.NET does not currently support passing ImGuiSliderFlags to double controls.
				// Casting to a float to allow use of ImGuiSliderFlags.Logarithmic.
				var zoom = (float)Editor.GetSpacingZoom();
				var originalZoom = zoom;
				var keyBind = UIControls.GetCommandString(pKeyBinds.ScrollZoom) + UIControls.MultipleKeysJoinString + "Scroll";
				ImGuiLayoutUtils.DrawRowDragFloat("Zoom", ref zoom,
					"Chart zoom level." +
					$"\n\nZoom level can be adjusted with {keyBind}.",
					100.0f, "%.6f", (float)ZoomManager.MinZoom,
					(float)ZoomManager.MaxZoom, ImGuiSliderFlags.Logarithmic);
				if (!zoom.FloatEquals(originalZoom))
				{
					Editor.SetSpacingZoom(zoom);
				}

				// Size cap.
				UIScrollPreferences.DrawSizeCapRow();

				ImGuiLayoutUtils.EndTable();
			}

			ImGui.SameLine();
			if (ImGuiLayoutUtils.BeginTable("Snap", ColumnWidthTitleSnap, ColumnWidthValueSnap))
			{
				// Snap level.
				ImGuiLayoutUtils.DrawRowSnapLevels("Snap", Editor.GetSnapManager(),
					"Current note type being snapped to." +
					"\n\nSnap can be changed with the left and right arrow keys.");

				// Snap lock level.
				ImGuiLayoutUtils.DrawRowSnapLockLevels("Snap Limit", Editor.GetSnapManager(),
					"Current limit on note types which can be snapped to." +
					"\nThe limit will restrict the snap note types to note types which evenly divide it.");

				// Automove.
				var autoAdvance = Preferences.Instance.NoteEntryMode == NoteEntryMode.AdvanceBySnap;
				var keyBind = UIControls.GetCommandString(pKeyBinds.ToggleNoteEntryMode);
				if (ImGuiLayoutUtils.DrawRowCheckbox("Automove", ref autoAdvance,
					    "Automove will automatically advance the cursor position when adding a new note." +
					    $"\n\nAutomove can be toggled with {keyBind}."))
					Preferences.Instance.NoteEntryMode = autoAdvance ? NoteEntryMode.AdvanceBySnap : NoteEntryMode.Normal;

				if (Editor.GetFocusedChart()?.IsMultiPlayer() ?? false)
				{
					// Player selection.
					keyBind = UIControls.GetCommandString(pKeyBinds.TogglePlayer);
					ImGuiLayoutUtils.DrawRowPlayerSelection("Player", Editor.GetFocusedChart().MaxPlayers,
						Editor.GetFocusedChartData()?.GetArrowGraphicManager(),
						"Current player. New steps will assigned to this player." +
						$"\n\nThe player can be toggled with {keyBind}." +
						"\nThe number of players for a chart can be assigned in the Chart Properties window.");
				}
				else
				{
					// Step Coloring.
					UIOptions.DrawStepColoring("Step Color");
				}

				ImGuiLayoutUtils.EndTable();
			}

			ImGui.SameLine();
			if (ImGuiLayoutUtils.BeginTable("UI Visibility", ColumnWidthTitleUI, ColumnWidthValueUI))
			{
				ImGuiLayoutUtils.DrawRowCheckboxWithButton("Mini Map", ref Preferences.Instance.PreferencesMiniMap.ShowMiniMap,
					"Options", () => { UIMiniMapPreferences.Instance.Open(true); },
					"Whether or not to show the Mini Map.");

				var b = Preferences.Instance.PreferencesDensityGraph.ShowDensityGraph;
				if (ImGuiLayoutUtils.DrawRowCheckboxWithButton("Density Graph", ref b,
					    "Options", () => { UIDensityGraphPreferences.Instance.Open(true); },
					    "Whether or not to show the Density Graph."))
				{
					Preferences.Instance.PreferencesDensityGraph.ShowDensityGraph = b;
				}

				ImGuiLayoutUtils.DrawRowCheckboxWithButton("Waveform", ref Preferences.Instance.PreferencesWaveForm.ShowWaveForm,
					"Options", () => { UIWaveFormPreferences.Instance.Open(true); },
					"Whether or not to show the Waveform.");

				ImGuiLayoutUtils.DrawRowCheckboxWithButton("Dark", ref Preferences.Instance.PreferencesDark.ShowDarkBg,
					"Options", () => { UIDarkPreferences.Instance.Open(true); },
					"Whether or not to show the dark background.");

				ImGuiLayoutUtils.EndTable();
			}

			ImGui.SameLine();
			if (ImGuiLayoutUtils.BeginTable("Step Visibility", ColumnWidthTitleSteps, ColumnWidthValueSteps))
			{
				ImGuiLayoutUtils.DrawRowCheckbox("Notes", ref Preferences.Instance.PreferencesOptions.RenderNotes,
					"Whether or not to render notes.");
				ImGuiLayoutUtils.DrawRowCheckbox("Markers", ref Preferences.Instance.PreferencesOptions.RenderMarkers,
					"Whether or not to render beat and measure markers.");
				ImGuiLayoutUtils.DrawRowCheckbox("Regions", ref Preferences.Instance.PreferencesOptions.RenderRegions,
					"Whether or not to render regions behind the chart for events like stops, the preview, etc.");
				ImGuiLayoutUtils.DrawRowCheckbox("Misc", ref Preferences.Instance.PreferencesOptions.RenderMiscEvents,
					"Whether or not to render miscellaneous events like timing events, labels, etc.");
				ImGuiLayoutUtils.EndTable();
			}

			// Draw the table with position information.
			DrawPositionTable();
		}

		ImGui.End();
	}

	private void DrawPositionTable()
	{
		var tableWidth = ImGui.GetContentRegionAvail().X;
		if (ImGui.BeginTable("UIChartPositionTable", 7, ImGuiTableFlags.Borders | ImGuiTableFlags.SizingStretchProp))
		{
			var originalItemSpacingX = ImGui.GetStyle().ItemSpacing.X;
			ImGui.GetStyle().ItemSpacing.X = 0;

			ImGui.TableSetupColumn("UIChartPositionTableC1", ImGuiTableColumnFlags.WidthStretch,
				(float)(TableNameColWidthPct * tableWidth));
			ImGui.TableSetupColumn("UIChartPositionTableC2", ImGuiTableColumnFlags.WidthStretch,
				(float)(TablePositionColWidthPct * tableWidth));
			ImGui.TableSetupColumn("UIChartPositionTableC3", ImGuiTableColumnFlags.WidthStretch,
				(float)(TableSongTimeColWidthPct * tableWidth));
			ImGui.TableSetupColumn("UIChartPositionTableC4", ImGuiTableColumnFlags.WidthStretch,
				(float)(TableChartTimeColWidthPct * tableWidth));
			ImGui.TableSetupColumn("UIChartPositionTableC5", ImGuiTableColumnFlags.WidthStretch,
				(float)(TableMeasureColWidthPct * tableWidth));
			ImGui.TableSetupColumn("UIChartPositionTableC6", ImGuiTableColumnFlags.WidthStretch,
				(float)(TableBeatColWidthPct * tableWidth));
			ImGui.TableSetupColumn("UIChartPositionTableC7", ImGuiTableColumnFlags.WidthStretch,
				(float)(TableRowColWidthPct * tableWidth));

			// Header
			DrawPositionTableHeader();

			// Chart Position
			DrawPositionTableRow("Chart", Editor.GetFocalPointScreenSpaceX(), Editor.GetFocalPointScreenSpaceY(),
				Editor.GetPosition());

			// Cursor Position
			var mouseState = Editor.GetMouseState();
			DrawPositionTableRow("Cursor", mouseState.X(), mouseState.Y(), mouseState.GetEditorPosition());

			ImGui.EndTable();

			ImGui.GetStyle().ItemSpacing.X = originalItemSpacingX;
		}
	}

	private static void DrawPositionTableHeader()
	{
		ImGui.TableNextRow();

		var colIndex = 0;
		DrawPositionTableHeaderCell("", ref colIndex);
		DrawPositionTableHeaderCell("Position", ref colIndex);
		DrawPositionTableHeaderCell("Song Time", ref colIndex);
		DrawPositionTableHeaderCell("Chart Time", ref colIndex);
		DrawPositionTableHeaderCell("Measure", ref colIndex);
		DrawPositionTableHeaderCell("Beat", ref colIndex);
		DrawPositionTableHeaderCell("Row", ref colIndex);
	}

	private static void DrawPositionTableHeaderCell(string text, ref int index)
	{
		ImGui.TableSetColumnIndex(index++);
		if (index > 1)
			ImGui.TableSetBgColor(ImGuiTableBgTarget.CellBg, ColorBgDarkGrey);

		var textWidth = ImGui.CalcTextSize(text).X;
		var availableWidth = ImGui.GetContentRegionAvail().X;
		if (availableWidth - textWidth > 0)
		{
			ImGui.Dummy(new Vector2(availableWidth - textWidth, 1));
			ImGui.SameLine();
		}

		ImGui.TextUnformatted(text);
	}

	private static void DrawPositionTableRow(string label, int x, int y, IReadOnlyEditorPosition position)
	{
		ImGui.TableNextRow();
		var colIndex = 0;

		ImGui.TableSetColumnIndex(colIndex++);
		ImGui.TextUnformatted(label);

		DrawPositionTableCell($"({x}, {y})", ref colIndex);
		DrawPositionTableCell(FormatTime(position.SongTime), ref colIndex);
		DrawPositionTableCell(FormatTime(position.ChartTime), ref colIndex);
		DrawPositionTableCell(FormatDouble(GetMeasure(position)), ref colIndex);
		DrawPositionTableCell(FormatDouble(position.ChartPosition / SMCommon.MaxValidDenominator), ref colIndex);
		DrawPositionTableCell(FormatDouble(position.ChartPosition), ref colIndex);
	}

	private static void DrawPositionTableCell(string text, ref int index)
	{
		ImGui.TableSetColumnIndex(index++);

		var textWidth = ImGui.CalcTextSize(text).X;
		var availableWidth = ImGui.GetContentRegionAvail().X;
		if (availableWidth - textWidth > 0)
		{
			ImGui.Dummy(new Vector2(availableWidth - textWidth, 1));
			ImGui.SameLine();
		}

		var delimiters = new[] { '.', ':' };
		var delimiterIndex = text.LastIndexOfAny(delimiters);
		if (delimiterIndex < 0)
		{
			ImGui.TextUnformatted(text);
		}
		else
		{
			var firstText = text.Substring(0, delimiterIndex);
			var secondText = text.Substring(delimiterIndex);

			ImGui.TextUnformatted(firstText);
			ImGui.SameLine();

			ImGui.PushStyleColor(ImGuiCol.Text, ColorTextGrey);
			ImGui.TextUnformatted(secondText);
			ImGui.PopStyleColor();
		}
	}

	private static double GetMeasure(IReadOnlyEditorPosition position)
	{
		return position.ActiveChart?.GetMeasureForChartPosition(position.ChartPosition) ?? 0.0;
	}

	private static string FormatDouble(double value)
	{
		if (double.IsNegativeInfinity(value))
		{
			return "-Infinity";
		}

		if (double.IsPositiveInfinity(value))
		{
			return "Infinity";
		}

		if (double.IsNaN(value))
		{
			return "NaN";
		}

		return $"{value:N3}";
	}

	private static string FormatTime(double seconds)
	{
		const string formatMinutes = @"mm\:ss\:ffffff";
		const string formatHours = @"hh\:mm\:ss\:ffffff";
		const string formatDays = @"d\.hh\:mm\:ss\:ffffff";

		if (seconds < TimeSpan.MinValue.TotalSeconds || double.IsNegativeInfinity(seconds))
		{
			return "-Infinity";
		}

		if (seconds > TimeSpan.MaxValue.TotalSeconds || double.IsPositiveInfinity(seconds))
		{
			return "Infinity";
		}

		if (double.IsNaN(seconds))
		{
			return "NaN";
		}

		var formatString = formatMinutes;
		var abs = Math.Abs(seconds);
		if (abs >= 86400.0)
		{
			formatString = formatDays;
		}
		else if (abs >= 3600.0)
		{
			formatString = formatHours;
		}

		var str = TimeSpan.FromSeconds(seconds).ToString(formatString);

		if (seconds < 0.0)
		{
			return "-" + str;
		}

		return str;
	}
}

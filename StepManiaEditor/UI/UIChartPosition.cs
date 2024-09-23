using System;
using System.Numerics;
using Fumen.Converters;
using ImGuiNET;
using static StepManiaEditor.ImGuiUtils;
using static Fumen.FumenExtensions;
using static StepManiaEditor.Editor;

namespace StepManiaEditor;

/// <summary>
/// Class for drawing information about the current Position within the current Chart.
/// </summary>
internal sealed class UIChartPosition : UIWindow
{
	private const uint ColorTextWhite = 0xFFFFFFFF;
	private const uint ColorTextGrey = 0xFF777777;
	private const uint ColorBgDarkGrey = 0xF0222222;

	public static readonly int Width = UiScaled(800);
	public static readonly int Height = UiScaled(63);
	public static readonly int HalfWidth = Width / 2;
	public static readonly int HalfHeight = Height / 2;

	private static readonly int ColumnWidthTitleSpacing = UiScaled(71);
	private static readonly int ColumnWidthTitleSnap = UiScaled(58);
	private static readonly int ColumnWidthTitleUI = UiScaled(77);
	private static readonly int ColumnWidthValueUI = UiScaled(108);
	private static readonly int ColumnWidthTitleSteps = UiScaled(40);
	private static readonly int ColumnWidthValueSteps = UiScaled(47);

	private static readonly int TableNameColWidth = UiScaled(37);
	private static readonly int TablePositionColWidth = UiScaled(73);
	private static readonly int TableSongTimeColWidth = UiScaled(104);
	private static readonly int TableChartTimeColWidth = UiScaled(104);
	private static readonly int TableMeasureColWidth = UiScaled(61);
	private static readonly int TableBeatColWidth = UiScaled(67);
	private static readonly int TableRowColWidth = UiScaled(78);
	private static readonly int TableWidth = UiScaled(588);
	private static readonly int TableHeight = UiScaled(48);
	private static readonly float ButtonSizeCapWidth = UiScaled(24);

	private Editor Editor;

	public static UIChartPosition Instance { get; } = new();

	private UIChartPosition() : base("Hotbar")
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

		//ImGui.SetNextWindowSize(new Vector2(Width, Height), ImGuiCond.FirstUseEver);
		if (ImGui.Begin(WindowTitle, ref Preferences.Instance.ShowHotbar))
		{
			var pScroll = Preferences.Instance.PreferencesScroll;
			var spacing = ImGui.GetStyle().ItemSpacing.X;
			var totalWidth = ImGui.GetContentRegionAvail().X;
			var tableUIWidth = ColumnWidthTitleUI + ColumnWidthValueUI + spacing;
			var tableStepsWidth = ColumnWidthTitleSteps + ColumnWidthValueSteps + spacing;
			var variableWidthTableWidth = (int)((totalWidth - tableUIWidth - tableStepsWidth - spacing * 3) * 0.5);
			var columnWidthValueSpacing = variableWidthTableWidth - spacing - ColumnWidthTitleSpacing;
			var columnWidthValueSnap = variableWidthTableWidth - spacing - ColumnWidthTitleSnap;

			if (ImGuiLayoutUtils.BeginTable("Spacing", ColumnWidthTitleSpacing, columnWidthValueSpacing))
			{
				// Spacing mode.
				UIScrollPreferences.DrawSpacingModeRow("Spacing Mode", true);

				// Spacing.
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
							"Spacing in pixels per row at default zoom level." +
							"\n\nSpacing can be adjusted with Shift+Scroll.",
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
							"Speed in pixels per second at default zoom level." +
							"\n\nSpacing can be adjusted with Shift+Scroll.",
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
							"\n\nSpacing can be adjusted with Shift+Scroll.",
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
				ImGuiLayoutUtils.DrawRowDragFloat("Zoom", ref zoom,
					"Chart zoom level." +
					"\n\nZoom level can be adjusted with Ctrl+Scroll.",
					100.0f, "%.6f", (float)ZoomManager.MinZoom,
					(float)ZoomManager.MaxZoom, ImGuiSliderFlags.Logarithmic);
				if (!zoom.FloatEquals(originalZoom))
				{
					Editor.SetSpacingZoom(zoom);
				}

				// Size cap.
				var sizeCap = Editor.GetSizeCap();
				var originalSizeCap= sizeCap;
				if (ImGuiLayoutUtils.DrawRowDragDoubleWithThreeButtons("Size Cap", ref sizeCap,
					() => Editor.SetSizeCap(1.0), "1", ButtonSizeCapWidth,
					() => Editor.SetSizeCap(0.5), "1/2", ButtonSizeCapWidth,
					() => Editor.SetSizeCap(0.25), "1/4", ButtonSizeCapWidth,
					"Maximum allowed size of the notes.",
					0.001f, "%.6f", ZoomManager.MinSizeCap, ZoomManager.MaxSizeCap) && !sizeCap.DoubleEquals(originalSizeCap))
				{
					Editor.SetSizeCap(sizeCap);
				}

				ImGuiLayoutUtils.EndTable();
			}

			ImGui.SameLine();
			if (ImGuiLayoutUtils.BeginTable("Snap", ColumnWidthTitleSnap, columnWidthValueSnap))
			{
				// Snap level.
				ImGuiLayoutUtils.DrawRowSnapLevels("Snap", Editor.GetSnapManager(),
					"Current note type being snapped to." +
					"\nThe limit will restrict the snap note types to note types which evenly divide it." +
					"\n\nSnap can be changed with the left and right arrow keys.");

				// Automove.
				var autoAdvance = Preferences.Instance.NoteEntryMode == NoteEntryMode.AdvanceBySnap;
				if (ImGuiLayoutUtils.DrawRowCheckbox("Automove", ref autoAdvance,
					    "Automove will automatically advance the cursor position when adding a new note." +
					    "\n\nAutomove can be toggled with the M key."))
					Preferences.Instance.NoteEntryMode = autoAdvance ? NoteEntryMode.AdvanceBySnap : NoteEntryMode.Normal;

				// Step Coloring.
				UIOptions.DrawStepColoring("Step Color");

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
				ImGuiLayoutUtils.DrawRowCheckbox("Notes", ref Preferences.Instance.RenderNotes,
					"Whether or not to render notes.");
				ImGuiLayoutUtils.DrawRowCheckbox("Markers", ref Preferences.Instance.RenderMarkers,
					"Whether or not to render beat and measure markers.");
				ImGuiLayoutUtils.DrawRowCheckbox("Regions", ref Preferences.Instance.RenderRegions,
					"Whether or not to render regions behind the chart for events like stops, the preview, etc.");
				ImGuiLayoutUtils.DrawRowCheckbox("Misc", ref Preferences.Instance.RenderMiscEvents,
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
		if (ImGui.BeginTable("UIChartPositionTable", 7,
			    ImGuiTableFlags.Borders, new Vector2(TableWidth, TableHeight)))
		{
			var originalCellPaddingY = ImGui.GetStyle().CellPadding.Y;
			var originalFramePaddingY = ImGui.GetStyle().FramePadding.Y;
			var originalInnerItemSpacingX = ImGui.GetStyle().ItemInnerSpacing.X;
			var originalItemSpacingX = ImGui.GetStyle().ItemSpacing.X;
			ImGui.GetStyle().ItemInnerSpacing.X = 0;
			ImGui.GetStyle().ItemSpacing.X = 0;
			ImGui.GetStyle().CellPadding.Y = 0;
			ImGui.GetStyle().FramePadding.Y = 0;

			ImGui.TableSetupColumn("UIChartPositionTableC1", ImGuiTableColumnFlags.WidthFixed, TableNameColWidth);
			ImGui.TableSetupColumn("UIChartPositionTableC2", ImGuiTableColumnFlags.WidthFixed, TablePositionColWidth);
			ImGui.TableSetupColumn("UIChartPositionTableC3", ImGuiTableColumnFlags.WidthFixed, TableSongTimeColWidth);
			ImGui.TableSetupColumn("UIChartPositionTableC4", ImGuiTableColumnFlags.WidthFixed, TableChartTimeColWidth);
			ImGui.TableSetupColumn("UIChartPositionTableC5", ImGuiTableColumnFlags.WidthFixed, TableMeasureColWidth);
			ImGui.TableSetupColumn("UIChartPositionTableC6", ImGuiTableColumnFlags.WidthFixed, TableBeatColWidth);
			ImGui.TableSetupColumn("UIChartPositionTableC7", ImGuiTableColumnFlags.WidthFixed, TableRowColWidth);

			// Header
			DrawPositionTableHeader();

			// Chart Position
			DrawPositionTableRow("Chart", Editor.GetFocalPointX(), Editor.GetFocalPointY(), Editor.GetPosition());

			// Cursor Position
			var mouseState = Editor.GetMouseState();
			DrawPositionTableRow("Cursor", mouseState.X(), mouseState.Y(), mouseState.GetEditorPosition());

			ImGui.EndTable();

			ImGui.GetStyle().ItemInnerSpacing.X = originalInnerItemSpacingX;
			ImGui.GetStyle().ItemSpacing.X = originalItemSpacingX;
			ImGui.GetStyle().FramePadding.Y = originalFramePaddingY;
			ImGui.GetStyle().CellPadding.Y = originalCellPaddingY;
		}
	}

	private static void DrawPositionTableHeader()
	{
		ImGui.TableNextRow();

		var colIndex = 0;
		DrawPositionTableHeaderCell(TableNameColWidth, "", ref colIndex);
		DrawPositionTableHeaderCell(TablePositionColWidth, "Position", ref colIndex);
		DrawPositionTableHeaderCell(TableSongTimeColWidth, "Song Time", ref colIndex);
		DrawPositionTableHeaderCell(TableChartTimeColWidth, "Chart Time", ref colIndex);
		DrawPositionTableHeaderCell(TableMeasureColWidth, "Measure", ref colIndex);
		DrawPositionTableHeaderCell(TableBeatColWidth, "Beat", ref colIndex);
		DrawPositionTableHeaderCell(TableRowColWidth, "Row", ref colIndex);
	}

	private static void DrawPositionTableHeaderCell(int width, string text, ref int index)
	{
		ImGui.TableSetColumnIndex(index++);
		if (index > 1)
			ImGui.TableSetBgColor(ImGuiTableBgTarget.CellBg, ColorBgDarkGrey);

		var textWidth = ImGui.CalcTextSize(text).X;
		if (width - textWidth > 0)
		{
			ImGui.Dummy(new Vector2(width - textWidth, 1));
			ImGui.SameLine();
		}

		ImGui.Text(text);
	}

	private static void DrawPositionTableRow(string label, int x, int y, IReadOnlyEditorPosition position)
	{
		ImGui.TableNextRow();
		var colIndex = 0;

		ImGui.TableSetColumnIndex(colIndex++);
		ImGui.Text(label);

		DrawPositionTableCell(TablePositionColWidth, $"({x}, {y})", ref colIndex);
		DrawPositionTableCell(TableSongTimeColWidth, FormatTime(position.SongTime), ref colIndex);
		DrawPositionTableCell(TableChartTimeColWidth, FormatTime(position.ChartTime), ref colIndex);
		DrawPositionTableCell(TableMeasureColWidth, FormatDouble(GetMeasure(position)), ref colIndex);
		DrawPositionTableCell(TableBeatColWidth, FormatDouble(position.ChartPosition / SMCommon.MaxValidDenominator),
			ref colIndex);
		DrawPositionTableCell(TableRowColWidth, FormatDouble(position.ChartPosition), ref colIndex);
	}

	private static void DrawPositionTableCell(int width, string text, ref int index)
	{
		ImGui.TableSetColumnIndex(index++);

		var textWidth = ImGui.CalcTextSize(text).X;
		if (width - textWidth > 0)
		{
			ImGui.Dummy(new Vector2(width - textWidth, 1));
			ImGui.SameLine();
		}

		var delimiters = new[] { '.', ':' };
		var delimiterIndex = text.LastIndexOfAny(delimiters);
		if (delimiterIndex < 0)
		{
			ImGui.Text(text);
		}
		else
		{
			var firstText = text.Substring(0, delimiterIndex);
			var secondText = text.Substring(delimiterIndex);

			ImGui.Text(firstText);
			ImGui.SameLine();

			ImGui.PushStyleColor(ImGuiCol.Text, ColorTextGrey);
			ImGui.Text(secondText);
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

using System;
using System.Collections.Generic;
using System.Numerics;
using Fumen;
using ImGuiNET;
using static Fumen.Converters.SMCommon;
using static StepManiaEditor.PreferencesPerformedChartConfig;
using static StepManiaEditor.ImGuiUtils;

namespace StepManiaEditor;

/// <summary>
/// Class for drawing an ImGui widget that shows arrow weights with vertical sliders and int drag controls per lane.
/// </summary>
internal sealed class ImGuiArrowWeightsWidget
{
	private static readonly float IconWidth = UiScaled(16);
	private static readonly float IconHeight = UiScaled(16);
	private static readonly float LaneWidth = UiScaled(20);
	private static readonly float SliderHeight = UiScaled(40);
	private static readonly float LaneSpacingX = UiScaled(2);
	private static readonly float LaneSpacingY = UiScaled(2);
	private static readonly Vector2 ArrowWeightWidgetSliderSize = new(LaneWidth, SliderHeight);
	private static readonly Vector2 IconSize = new(IconWidth, IconHeight);
	private static readonly Vector2 IconPadding = new((LaneWidth - IconWidth) * 0.5f, (LaneWidth - IconWidth) * 0.5f);

	private readonly Editor Editor;

	/// <summary>
	/// Cached weight value for undo / redo.
	/// </summary>
	private int CachedValue;

	public ImGuiArrowWeightsWidget(Editor editor)
	{
		Editor = editor;
	}

	public static float GetFullWidth(NamedConfig config)
	{
		var numColumns = config.GetMaxNumWeightsForAnyChartType();
		return numColumns * LaneWidth + (numColumns - 1) * LaneSpacingX;
	}

	/// <summary>
	/// Helper method for drawing.
	/// </summary>
	/// <param name="weights">Weight values to use for the vertical sliders.</param>
	/// <param name="values">Numeric values to use for the int drag controls.</param>
	/// <param name="chartType">ChartType to use for drawing.</param>
	/// <param name="configForUpdates">
	/// Optional NamedConfig to use for updates for enabled controls. This is expected to be null when drawing
	/// an EditorChart's step counts.
	/// </param>
	private void Draw(IReadOnlyList<int> weights, IReadOnlyList<int> values, ChartType chartType, NamedConfig configForUpdates)
	{
		var chartTypeString = ChartTypeString(chartType);

		// Set tighter spacing.
		var originalItemSpacingX = ImGui.GetStyle().ItemSpacing.X;
		var originalItemSpacingY = ImGui.GetStyle().ItemSpacing.Y;
		ImGui.GetStyle().ItemSpacing.X = LaneSpacingX;
		ImGui.GetStyle().ItemSpacing.Y = LaneSpacingY;

		// Determine the weight range for coloring sliders.
		var maxWeight = int.MinValue;
		var minWeight = int.MaxValue;
		foreach (var weight in weights)
		{
			minWeight = Math.Min(Math.Max(1, weight), minWeight);
			maxWeight = Math.Max(Math.Max(1, weight), maxWeight);
		}

		// Vertical slider control.
		var numArrows = weights.Count;
		for (var index = 0; index < numArrows; index++)
		{
			var weightBefore = weights[index];
			var weight = weights[index];

			// Color the slider by scaling the hue between red and green.
			var h = Interpolation.LogarithmicInterpolate(0.2793f, 1.9548f, minWeight, maxWeight, weight);
			var (r, g, b) = ColorUtils.HsvToRgb(h, 0.5f, 0.5f);
			ImGui.PushStyleColor(ImGuiCol.FrameBg, new Vector4(r, g, b, 1.0f));
			(r, g, b) = ColorUtils.HsvToRgb(h, 0.6f, 0.5f);
			ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, new Vector4(r, g, b, 1.0f));
			(r, g, b) = ColorUtils.HsvToRgb(h, 0.7f, 0.5f);
			ImGui.PushStyleColor(ImGuiCol.FrameBgActive, new Vector4(r, g, b, 1.0f));
			(r, g, b) = ColorUtils.HsvToRgb(h, 0.9f, 0.9f);
			ImGui.PushStyleColor(ImGuiCol.SliderGrab, new Vector4(r, g, b, 1.0f));
			(r, g, b) = ColorUtils.HsvToRgb(h, 0.95f, 0.95f);
			ImGui.PushStyleColor(ImGuiCol.SliderGrabActive, new Vector4(r, g, b, 1.0f));

			// Draw the slider
			ImGui.VSliderInt($"##{chartTypeString}v{index}", ArrowWeightWidgetSliderSize, ref weight, 0, 100, "");
			weight = Math.Clamp(weight, 0, 100);
			configForUpdates?.SetArrowWeight(chartType, index, weight);

			if (ImGui.IsItemActivated())
			{
				CachedValue = weightBefore;
			}

			if (ImGui.IsItemDeactivatedAfterEdit() && CachedValue != weight)
			{
				ActionQueue.Instance.Do(
					new ActionSetPerformedChartConfigArrowWeight(configForUpdates, chartType, index, weight, CachedValue));
			}

			if (index != numArrows - 1)
				ImGui.SameLine();

			ImGui.PopStyleColor(5);
		}

		// Drag int control.
		for (var index = 0; index < numArrows; index++)
		{
			var valueBefore = values[index];
			var value = values[index];
			ImGui.SetNextItemWidth(LaneWidth);
			ImGui.DragInt($"##{chartTypeString}{index}", ref value, 1, 0, 100);
			value = Math.Clamp(value, 0, 100);
			configForUpdates?.SetArrowWeight(chartType, index, value);

			if (ImGui.IsItemActivated())
			{
				CachedValue = valueBefore;
			}

			if (ImGui.IsItemDeactivatedAfterEdit() && CachedValue != value)
			{
				ActionQueue.Instance.Do(
					new ActionSetPerformedChartConfigArrowWeight(configForUpdates, chartType, index, value, CachedValue));
			}

			if (index != numArrows - 1)
				ImGui.SameLine();
		}

		// Icon.
		var textureAtlas = Editor.GetTextureAtlas();
		var imGuiTextureAtlasTexture = Editor.GetTextureAtlasImGuiTexture();
		var (atlasW, atlasH) = textureAtlas.GetDimensions();
		var icons = ArrowGraphicManager.GetIcons(chartType);
		for (var index = 0; index < numArrows; index++)
		{
			ImGui.Dummy(IconPadding);

			var preLoopItemSpacingX = ImGui.GetStyle().ItemSpacing.X;
			ImGui.GetStyle().ItemSpacing.X = 0;

			ImGui.SameLine();

			var (x, y, w, h) = textureAtlas.GetSubTextureBounds(icons[index]);
			ImGui.Image(imGuiTextureAtlasTexture, IconSize, new Vector2(x / (float)atlasW, y / (float)atlasH),
				new Vector2((x + w) / (float)atlasW, (y + h) / (float)atlasH));
			ImGui.SameLine();

			ImGui.Dummy(IconPadding);

			ImGui.GetStyle().ItemSpacing.X = preLoopItemSpacingX;

			if (index != numArrows - 1)
				ImGui.SameLine();
		}

		// Restore spacing.
		ImGui.GetStyle().ItemSpacing.X = originalItemSpacingX;
		ImGui.GetStyle().ItemSpacing.Y = originalItemSpacingY;
	}

	/// <summary>
	/// Draw the widget representing weights for the step counts of the given EditorChart.
	/// These values are not editable and the slider values are normalized to the highest
	/// value to make the visualization more pronounced.
	/// </summary>
	/// <param name="editorChart">EditorChart to use for drawing.</param>
	public void DrawChartStepCounts(EditorChart editorChart)
	{
		var stepCountsByLane = editorChart.GetStepCountByLane();
		var totalStepCount = editorChart.GetStepCount();
		var maxStepCountByLane = 0;
		for (var a = 0; a < stepCountsByLane.Length; a++)
		{
			maxStepCountByLane = Math.Max(maxStepCountByLane, stepCountsByLane[a]);
		}

		// Set up normalized weights and values (out of 100 percent).
		var weights = new int[stepCountsByLane.Length];
		var values = new int[stepCountsByLane.Length];
		for (var i = 0; i < stepCountsByLane.Length; i++)
		{
			if (maxStepCountByLane == 0)
			{
				weights[i] = 0;
				values[i] = 0;
			}
			else
			{
				weights[i] = (int)Math.Round(100.0f * stepCountsByLane[i] / maxStepCountByLane);
				values[i] = (int)Math.Round(100.0f * stepCountsByLane[i] / totalStepCount);
			}

			weights[i] = MathUtils.Clamp(weights[i], 0, 100);
			values[i] = MathUtils.Clamp(values[i], 0, 100);
		}

		PushDisabled();
		Draw(weights, values, editorChart.ChartType, null);
		PopDisabled();
	}

	/// <summary>
	/// Draw the widget representing weights from a PerformedChart NamedConfig.
	/// These values are typically editable and use vertical slider values that
	/// are not normalized to the highest value so that they can be slid to larger
	/// values.
	/// </summary>
	/// <param name="config">NamedConfig to use for drawing.</param>
	/// <param name="chartType">ChartType to use for drawing.</param>
	public void DrawConfig(NamedConfig config, ChartType chartType)
	{
		var chartTypeString = ChartTypeString(chartType);
		if (!config.Config.ArrowWeights.TryGetValue(chartTypeString, out var weights))
			return;
		// The weights and values are the same for this config.
		Draw(weights, weights, chartType, config);
	}
}

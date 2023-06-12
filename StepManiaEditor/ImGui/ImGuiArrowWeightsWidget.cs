using System;
using System.Numerics;
using Fumen;
using ImGuiNET;
using static Fumen.Converters.SMCommon;
using static StepManiaEditor.PreferencesPerformedChartConfig;
using static StepManiaEditor.ImGuiUtils;

namespace StepManiaEditor;

internal class ImGuiArrowWeightsWidget
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
	private readonly ChartType ChartType;

	/// <summary>
	/// Cached weight value for undo / redo.
	/// </summary>
	private int CachedValue;

	public ImGuiArrowWeightsWidget(ChartType chartType, Editor editor)
	{
		ChartType = chartType;
		Editor = editor;
	}

	public static float GetFullWidth(NamedConfig config)
	{
		var numColumns = config.GetMaxNumWeightsForAnyChartType();
		return numColumns * LaneWidth + (numColumns - 1) * LaneSpacingX;
	}

	public void Draw(NamedConfig config)
	{
		var chartTypeString = ChartTypeString(ChartType);
		if (!config.Config.ArrowWeights.TryGetValue(chartTypeString, out var weights))
			return;

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
			config.SetArrowWeight(ChartType, index, weight);

			if (ImGui.IsItemActivated())
			{
				CachedValue = weightBefore;
			}

			if (ImGui.IsItemDeactivatedAfterEdit() && CachedValue != weight)
			{
				ActionQueue.Instance.Do(
					new ActionSetPerformedChartConfigArrowWeight(config, ChartType, index, weight, CachedValue));
			}

			if (index != numArrows - 1)
				ImGui.SameLine();

			ImGui.PopStyleColor(5);
		}

		// Drag int control.
		for (var index = 0; index < numArrows; index++)
		{
			var weightBefore = weights[index];
			var weight = weights[index];
			ImGui.SetNextItemWidth(LaneWidth);
			ImGui.DragInt($"##{chartTypeString}{index}", ref weight, 1, 0, 100);
			weight = Math.Clamp(weight, 0, 100);
			config.SetArrowWeight(ChartType, index, weight);

			if (ImGui.IsItemActivated())
			{
				CachedValue = weightBefore;
			}

			if (ImGui.IsItemDeactivatedAfterEdit() && CachedValue != weight)
			{
				ActionQueue.Instance.Do(
					new ActionSetPerformedChartConfigArrowWeight(config, ChartType, index, weight, CachedValue));
			}

			if (index != numArrows - 1)
				ImGui.SameLine();
		}

		// Icon.
		var textureAtlas = Editor.GetTextureAtlas();
		var imGuiTextureAtlasTexture = Editor.GetTextureAtlasImGuiTexture();
		var (atlasW, atlasH) = textureAtlas.GetDimensions();
		var icons = ArrowGraphicManager.GetIcons(ChartType);
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
}

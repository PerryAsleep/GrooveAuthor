using Fumen.Converters;
using ImGuiNET;

namespace StepManiaEditor;

/// <summary>
/// Data related to one snap level.
/// A snap level locks cursor movement to a specific number of rows based on a note type.
/// </summary>
internal sealed class SnapData
{
	public readonly int Rows;
	public readonly int SubDivision;
	public readonly string Text;

	public SnapData(int rows)
	{
		Rows = rows;
		if (Rows > 0)
		{
			SubDivision = SMCommon.MaxValidDenominator / Rows;
			Text = $"1/{SMCommon.MaxValidDenominator / Rows * SMCommon.NumBeatsPerMeasure}";
		}
		else
		{
			SubDivision = 0;
			Text = "None";
		}
	}

	public uint GetUIColor()
	{
		if (Rows > 0 && ArrowGraphicManager.TryGetArrowUIColorForSubdivision(SubDivision, out var color))
			return color;
		return ImGui.GetColorU32(ImGuiCol.Text);
	}
}

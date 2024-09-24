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
	public readonly string Texture;
	private int SubDivision;
	private string Text;

	public SnapData(int rows)
	{
		Rows = rows;
		Init();
	}

	public SnapData(int rows, string texture)
	{
		Rows = rows;
		Texture = texture;
		Init();
	}

	private void Init()
	{
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

	public uint GetColor()
	{
		if (Rows > 0)
			return ArrowGraphicManager.GetArrowColorForSubdivision(SubDivision);
		return ImGui.GetColorU32(ImGuiCol.Text);
	}

	public string GetText()
	{
		return Text;
	}
}

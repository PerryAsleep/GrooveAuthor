using System.Collections.Generic;
using static Fumen.Converters.SMCommon;

namespace StepManiaEditor;

/// <summary>
/// Class for managing levels of SnapData.
/// </summary>
internal sealed class SnapManager
{
	private readonly SnapData[] SnapLevels;

	public SnapManager()
	{
		// Set up snap levels for all valid denominators.
		SnapLevels = new SnapData[ValidDenominators.Length + 1];
		SnapLevels[0] = new SnapData(0);
		for (var denominatorIndex = 0; denominatorIndex < ValidDenominators.Length; denominatorIndex++)
		{
			SnapLevels[denominatorIndex + 1] = new SnapData(
				MaxValidDenominator / ValidDenominators[denominatorIndex],
				ArrowGraphicManager.GetSnapIndicatorTexture(ValidDenominators[denominatorIndex]));
		}

		var p = Preferences.Instance;
		if (p.SnapIndex < 0 || p.SnapIndex >= SnapLevels.Length)
			p.SnapIndex = 0;
		if (p.SnapLockIndex < 0 || p.SnapLockIndex >= SnapLevels.Length)
			p.SnapLockIndex = 0;
	}

	public IReadOnlyList<SnapData> GetSnapLevels()
	{
		return SnapLevels.AsReadOnly();
	}

	public int GetCurrentRows()
	{
		return SnapLevels[Preferences.Instance.SnapIndex].Rows;
	}

	public string GetCurrentTexture()
	{
		return SnapLevels[Preferences.Instance.SnapIndex].Texture;
	}

	public static bool IsSnapIndexValidForSnapLock(int snapIndex)
	{
		if (snapIndex == 0)
			return true;

		var p = Preferences.Instance;
		if (p.SnapLockIndex == 0)
			return true;

		return ValidDenominators[p.SnapLockIndex - 1] % ValidDenominators[snapIndex - 1] == 0;
	}

	public void DecreaseSnap()
	{
		var p = Preferences.Instance;
		do
		{
			p.SnapIndex--;
			if (p.SnapIndex < 0)
				p.SnapIndex = SnapLevels.Length - 1;
		} while (!IsSnapIndexValidForSnapLock(p.SnapIndex));
	}

	public void IncreaseSnap()
	{
		var p = Preferences.Instance;
		do
		{
			p.SnapIndex++;
			if (p.SnapIndex >= SnapLevels.Length)
				p.SnapIndex = 0;
		} while (!IsSnapIndexValidForSnapLock(p.SnapIndex));
	}

	public void SetSnapToLevel(int snapLevel)
	{
		if (!IsSnapIndexValidForSnapLock(snapLevel))
			return;
		Preferences.Instance.SnapIndex = snapLevel;
	}
}

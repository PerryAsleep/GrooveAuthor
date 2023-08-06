using System;
using StepManiaEditor.AutogenConfig;

namespace StepManiaEditor;

/// <summary>
/// Action to delete an EditorPerformedChartConfig.
/// </summary>
internal sealed class ActionDeletePerformedChartConfig : EditorAction
{
	private readonly Guid ConfigGuid;
	private readonly EditorPerformedChartConfig Config;
	private bool LastSelectedAutogenPerformedChartConfigUsedDeletedConfig;

	public ActionDeletePerformedChartConfig(Guid configGuid) : base(false, false)
	{
		ConfigGuid = configGuid;
		Config = PerformedChartConfigManager.Instance.GetConfig(ConfigGuid);
	}

	public override string ToString()
	{
		return $"Delete {Config.Name} Performed Chart Config.";
	}

	public override bool AffectsFile()
	{
		return false;
	}

	protected override void DoImplementation()
	{
		LastSelectedAutogenPerformedChartConfigUsedDeletedConfig =
			Preferences.Instance.LastSelectedAutogenPerformedChartConfig == ConfigGuid;
		PerformedChartConfigManager.Instance.DeleteConfig(ConfigGuid);
		if (LastSelectedAutogenPerformedChartConfigUsedDeletedConfig)
			Preferences.Instance.LastSelectedAutogenPerformedChartConfig =
				PerformedChartConfigManager.DefaultPerformedChartConfigGuid;
	}

	protected override void UndoImplementation()
	{
		PerformedChartConfigManager.Instance.AddConfig(Config);
		if (LastSelectedAutogenPerformedChartConfigUsedDeletedConfig)
			Preferences.Instance.LastSelectedAutogenPerformedChartConfig = ConfigGuid;
	}
}

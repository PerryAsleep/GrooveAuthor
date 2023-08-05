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
		Config = ConfigManager.Instance.GetPerformedChartConfig(ConfigGuid);
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
		ConfigManager.Instance.DeletePerformedChartConfig(ConfigGuid);
		if (LastSelectedAutogenPerformedChartConfigUsedDeletedConfig)
			Preferences.Instance.LastSelectedAutogenPerformedChartConfig =
				ConfigManager.DefaultPerformedChartConfigGuid;
	}

	protected override void UndoImplementation()
	{
		ConfigManager.Instance.AddPerformedChartConfig(Config);
		if (LastSelectedAutogenPerformedChartConfigUsedDeletedConfig)
			Preferences.Instance.LastSelectedAutogenPerformedChartConfig = ConfigGuid;
	}
}

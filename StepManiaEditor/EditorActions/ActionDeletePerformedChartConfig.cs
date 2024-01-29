using System;
using System.Collections.Generic;
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
	private readonly List<EditorPatternEvent> EventsWithDeletedConfig;

	public ActionDeletePerformedChartConfig(Editor editor, Guid configGuid) : base(false, false)
	{
		ConfigGuid = configGuid;
		Config = PerformedChartConfigManager.Instance.GetConfig(ConfigGuid);
		EventsWithDeletedConfig = new List<EditorPatternEvent>();

		var song = editor.GetActiveSong();
		if (song != null)
		{
			var charts = song.GetCharts();
			foreach (var chart in charts)
			{
				foreach (var pattern in chart.GetPatterns())
				{
					if (pattern.PerformedChartConfigGuid == configGuid)
					{
						EventsWithDeletedConfig.Add(pattern);
					}
				}
			}
		}
	}

	public override string ToString()
	{
		return $"Delete {Config} Performed Chart Config.";
	}

	public override bool AffectsFile()
	{
		return EventsWithDeletedConfig.Count > 0;
	}

	protected override void DoImplementation()
	{
		foreach (var pattern in EventsWithDeletedConfig)
		{
			pattern.PerformedChartConfigGuid = PerformedChartConfigManager.DefaultPerformedChartConfigGuid;
		}

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

		foreach (var pattern in EventsWithDeletedConfig)
		{
			pattern.PerformedChartConfigGuid = ConfigGuid;
		}
	}
}

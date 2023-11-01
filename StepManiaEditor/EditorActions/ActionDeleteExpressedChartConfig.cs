using System;
using System.Collections.Generic;
using StepManiaEditor.AutogenConfig;

namespace StepManiaEditor;

/// <summary>
/// Action to delete an EditorExpressedChartConfig.
/// </summary>
internal sealed class ActionDeleteExpressedChartConfig : EditorAction
{
	private readonly Guid ConfigGuid;
	private readonly EditorExpressedChartConfig Config;
	private readonly List<EditorChart> ChartsWithDeletedConfig;

	public ActionDeleteExpressedChartConfig(Editor editor, Guid configGuid) : base(false, false)
	{
		ConfigGuid = configGuid;
		Config = ExpressedChartConfigManager.Instance.GetConfig(ConfigGuid);
		ChartsWithDeletedConfig = new List<EditorChart>();

		var song = editor.GetActiveSong();
		if (song != null)
		{
			var charts = song.GetCharts();
			foreach (var chart in charts)
			{
				if (chart.ExpressedChartConfig == ConfigGuid)
				{
					ChartsWithDeletedConfig.Add(chart);
				}
			}
		}
	}

	public override string ToString()
	{
		return $"Delete {Config.Name} Expressed Chart Config.";
	}

	public override bool AffectsFile()
	{
		return ChartsWithDeletedConfig.Count > 0;
	}

	protected override void DoImplementation()
	{
		foreach (var chart in ChartsWithDeletedConfig)
		{
			chart.ExpressedChartConfig = ExpressedChartConfigManager.DefaultExpressedChartDynamicConfigGuid;
		}

		ExpressedChartConfigManager.Instance.DeleteConfig(ConfigGuid);
	}

	protected override void UndoImplementation()
	{
		ExpressedChartConfigManager.Instance.AddConfig(Config);

		foreach (var chart in ChartsWithDeletedConfig)
		{
			chart.ExpressedChartConfig = ConfigGuid;
		}
	}
}

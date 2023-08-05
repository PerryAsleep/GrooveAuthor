using System;
using System.Collections.Generic;
using StepManiaEditor.AutogenConfig;

namespace StepManiaEditor;

/// <summary>
/// Action to delete an EditorExpressedChartConfig.
/// </summary>
internal sealed class ActionDeleteExpressedChartConfig : EditorAction
{
	private readonly Editor Editor;
	private readonly Guid ConfigGuid;
	private readonly EditorExpressedChartConfig Config;
	private readonly List<EditorChart> ChartsWithDeletedConfig;

	public ActionDeleteExpressedChartConfig(Editor editor, Guid configGuid) : base(false, false)
	{
		Editor = editor;
		ConfigGuid = configGuid;
		Config = ConfigManager.Instance.GetExpressedChartConfig(ConfigGuid);
		ChartsWithDeletedConfig = new List<EditorChart>();

		var song = Editor.GetActiveSong();
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
		var song = Editor.GetActiveSong();
		if (song != null)
		{
			var charts = song.GetCharts();
			foreach (var chart in charts)
			{
				if (chart.ExpressedChartConfig == ConfigGuid)
				{
					chart.ExpressedChartConfig = ConfigManager.DefaultExpressedChartDynamicConfigGuid;
				}
			}
		}

		ConfigManager.Instance.DeleteExpressedChartConfig(ConfigGuid);
	}

	protected override void UndoImplementation()
	{
		ConfigManager.Instance.AddExpressedChartConfig(Config);

		foreach (var chart in ChartsWithDeletedConfig)
		{
			chart.ExpressedChartConfig = ConfigGuid;
		}
	}
}

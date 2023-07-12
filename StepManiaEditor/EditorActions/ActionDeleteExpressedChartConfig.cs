using System;
using System.Collections.Generic;

namespace StepManiaEditor;

/// <summary>
/// Action to delete an ExpressedChart configuration.
/// </summary>
internal sealed class ActionDeleteExpressedChartConfig : EditorAction
{
	private readonly Editor Editor;
	private readonly Guid ConfigGuid;
	private readonly PreferencesExpressedChartConfig.NamedConfig NamedConfig;
	private readonly List<EditorChart> ChartsWithDeletedConfig;

	public ActionDeleteExpressedChartConfig(Editor editor, Guid configGuid) : base(false, false)
	{
		Editor = editor;
		ConfigGuid = configGuid;
		NamedConfig = Preferences.Instance.PreferencesExpressedChartConfig.GetNamedConfig(ConfigGuid);
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
		return $"Delete {NamedConfig.Name} Expressed Chart Config.";
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
					chart.ExpressedChartConfig = PreferencesExpressedChartConfig.DefaultDynamicConfigGuid;
				}
			}
		}

		Preferences.Instance.PreferencesExpressedChartConfig.DeleteConfig(ConfigGuid);
	}

	protected override void UndoImplementation()
	{
		Preferences.Instance.PreferencesExpressedChartConfig.AddConfig(NamedConfig);

		foreach (var chart in ChartsWithDeletedConfig)
		{
			chart.ExpressedChartConfig = ConfigGuid;
		}
	}
}

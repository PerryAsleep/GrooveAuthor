using System.Collections.Generic;

namespace StepManiaEditor;

/// <summary>
/// Action to delete an ExpressedChart configuration.
/// </summary>
internal sealed class ActionDeleteExpressedChartConfig : EditorAction
{
	private readonly Editor Editor;
	private readonly string ConfigName;
	private readonly PreferencesExpressedChartConfig.NamedConfig NamedConfig;
	private readonly List<EditorChart> ChartsWithDeletedConfig;

	public ActionDeleteExpressedChartConfig(Editor editor, string configName) : base(false, false)
	{
		Editor = editor;
		ConfigName = configName;
		NamedConfig = Preferences.Instance.PreferencesExpressedChartConfig.GetNamedConfig(ConfigName);
		ChartsWithDeletedConfig = new List<EditorChart>();

		var song = Editor.GetActiveSong();
		if (song != null)
		{
			var charts = song.GetCharts();
			foreach (var chart in charts)
			{
				if (chart.ExpressedChartConfig == ConfigName)
				{
					ChartsWithDeletedConfig.Add(chart);
				}
			}
		}
	}

	public override string ToString()
	{
		return $"Delete {ConfigName} Expressed Chart Config.";
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
				if (chart.ExpressedChartConfig == ConfigName)
				{
					chart.ExpressedChartConfig = PreferencesExpressedChartConfig.DefaultDynamicConfigName;
				}
			}
		}

		Preferences.Instance.PreferencesExpressedChartConfig.DeleteConfig(ConfigName);
	}

	protected override void UndoImplementation()
	{
		Preferences.Instance.PreferencesExpressedChartConfig.AddConfig(NamedConfig);

		foreach (var chart in ChartsWithDeletedConfig)
		{
			chart.ExpressedChartConfig = ConfigName;
		}
	}
}

using System.Collections.Generic;

namespace StepManiaEditor
{
	internal sealed class ActionDeleteExpressedChartConfig : EditorAction
	{
		private Editor Editor;
		private string ConfigName;
		private List<EditorChart> ChartsWithDeletedConfig;

		public ActionDeleteExpressedChartConfig(Editor editor, string configName) : base(false, false)
		{
			Editor = editor;
			ConfigName = configName;
			ChartsWithDeletedConfig = new List<EditorChart>();

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
				foreach(var chart in charts)
				{
					if (chart.ExpressedChartConfig == ConfigName)
					{
						chart.ExpressedChartConfig = PreferencesExpressedChartConfig.DefaultConfigName;
					}
				}
			}

			Preferences.Instance.PreferencesExpressedChartConfig.DeleteConfig(ConfigName);
		}

		protected override void UndoImplementation()
		{
			Preferences.Instance.PreferencesExpressedChartConfig.AddConfig(ConfigName);

			foreach(var chart in ChartsWithDeletedConfig)
			{
				chart.ExpressedChartConfig = ConfigName;
			}
		}
	}
}

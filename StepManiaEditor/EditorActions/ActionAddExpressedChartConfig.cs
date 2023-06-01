
namespace StepManiaEditor
{
	internal sealed class ActionAddExpressedChartConfig : EditorAction
	{
		private readonly string ConfigName;
		private readonly EditorChart EditorChart;
		private readonly string EditorChartOldConfigName;

		public ActionAddExpressedChartConfig() : base(false, false)
		{
			ConfigName = Preferences.Instance.PreferencesExpressedChartConfig.GetNewConfigName();
		}

		public ActionAddExpressedChartConfig(string configName, EditorChart editorChart) : base(false, false)
		{
			ConfigName = configName;
			EditorChart = editorChart;
			if (EditorChart != null)
			{
				EditorChartOldConfigName = EditorChart.ExpressedChartConfig;
			}
		}

		public override string ToString()
		{
			return $"Add Expressed Chart Config.";
		}

		public override bool AffectsFile()
		{
			return EditorChart != null;
		}

		protected override void DoImplementation()
		{
			Preferences.Instance.PreferencesExpressedChartConfig.AddConfig(ConfigName);
			if (EditorChart != null)
			{
				EditorChart.ExpressedChartConfig = ConfigName;
			}
		}

		protected override void UndoImplementation()
		{
			Preferences.Instance.PreferencesExpressedChartConfig.DeleteConfig(ConfigName);
			if (EditorChart != null)
			{
				EditorChart.ExpressedChartConfig = EditorChartOldConfigName;
			}
		}
	}
}

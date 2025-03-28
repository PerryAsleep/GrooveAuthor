using System;
using System.Collections.Generic;
using StepManiaEditor.AutogenConfig;

namespace StepManiaEditor;

/// <summary>
/// Action to delete an EditorPatternConfig.
/// </summary>
internal sealed class ActionDeletePatternConfig : EditorAction
{
	private readonly Guid ConfigGuid;
	private readonly EditorPatternConfig Config;
	private readonly List<EditorPatternEvent> EventsWithDeletedConfig;

	public ActionDeletePatternConfig(Editor editor, Guid configGuid) : base(false, false)
	{
		ConfigGuid = configGuid;
		Config = PatternConfigManager.Instance.GetConfig(ConfigGuid);
		EventsWithDeletedConfig = [];

		var song = editor.GetActiveSong();
		if (song != null)
		{
			var charts = song.GetCharts();
			foreach (var chart in charts)
			{
				foreach (var pattern in chart.GetPatterns())
				{
					if (pattern.PatternConfigGuid == configGuid)
					{
						EventsWithDeletedConfig.Add(pattern);
					}
				}
			}
		}
	}

	public override string ToString()
	{
		return $"Delete {Config} Pattern Config.";
	}

	public override bool AffectsFile()
	{
		return EventsWithDeletedConfig.Count > 0;
	}

	protected override void DoImplementation()
	{
		foreach (var pattern in EventsWithDeletedConfig)
		{
			pattern.PatternConfigGuid = PatternConfigManager.DefaultPatternConfigSixteenthsGuid;
		}

		PatternConfigManager.Instance.DeleteConfig(ConfigGuid);
	}

	protected override void UndoImplementation()
	{
		PatternConfigManager.Instance.AddConfig(Config);

		foreach (var pattern in EventsWithDeletedConfig)
		{
			pattern.PatternConfigGuid = ConfigGuid;
		}
	}
}

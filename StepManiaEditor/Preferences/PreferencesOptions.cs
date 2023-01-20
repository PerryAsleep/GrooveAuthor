using System;
using System.Linq;
using System.Text.Json.Serialization;
using Fumen.Converters;
using static Fumen.Converters.SMCommon;
using static Fumen.FumenExtensions;

namespace StepManiaEditor
{
	internal sealed class PreferencesOptions
	{
		// Default values.
		public const int DefaultRecentFilesHistorySize = 10;
		public const SMCommon.ChartType DefaultDefaultStepsType = SMCommon.ChartType.dance_single;
		public const SMCommon.ChartDifficultyType DefaultDefaultDifficultyType = SMCommon.ChartDifficultyType.Challenge;
		public const double DefaultPreviewFadeInTime = 0.0;
		public const double DefaultPreviewFadeOutTime = 1.5;
		public static readonly SMCommon.ChartType[] DefaultStartupChartTypes =
		{
			SMCommon.ChartType.dance_single,
			SMCommon.ChartType.dance_double
		};
		public static bool[] DefaultStartupChartTypesBools;
		public const bool DefaultOpenLastOpenedFileOnLaunch = false;

		// Preferences.
		[JsonInclude] public bool ShowOptionsWindow = false;
		[JsonInclude] public int RecentFilesHistorySize = DefaultRecentFilesHistorySize;
		[JsonInclude] public SMCommon.ChartType DefaultStepsType = DefaultDefaultStepsType;
		[JsonInclude] public SMCommon.ChartDifficultyType DefaultDifficultyType = DefaultDefaultDifficultyType;
		[JsonInclude] public double PreviewFadeInTime = DefaultPreviewFadeInTime;
		[JsonInclude] public double PreviewFadeOutTime = DefaultPreviewFadeOutTime;
		[JsonInclude] public SMCommon.ChartType[] StartupChartTypes = (SMCommon.ChartType[])DefaultStartupChartTypes.Clone();
		[JsonInclude] public bool OpenLastOpenedFileOnLaunch = DefaultOpenLastOpenedFileOnLaunch;

		// Strings are serialized, but converted to an array of booleans for UI.
		[JsonIgnore] public bool[] StartupChartTypesBools;

		public bool IsUsingDefaults()
		{
			return RecentFilesHistorySize == DefaultRecentFilesHistorySize
			       && DefaultStepsType == DefaultDefaultStepsType
			       && DefaultDifficultyType == DefaultDefaultDifficultyType
			       && PreviewFadeInTime.DoubleEquals(DefaultPreviewFadeInTime)
			       && PreviewFadeOutTime.DoubleEquals(DefaultPreviewFadeOutTime)
			       && StartupChartTypesBools.SequenceEqual(DefaultStartupChartTypesBools)
			       && OpenLastOpenedFileOnLaunch == DefaultOpenLastOpenedFileOnLaunch;
		}

		public void RestoreDefaults()
		{
			// Don't enqueue an action if it would not have any effect.
			if (IsUsingDefaults())
				return;
			ActionQueue.Instance.Do(new ActionRestoreOptionPreferenceDefaults());
		}

		public void PostLoad()
		{
			// Set up StartupChartTypesBools from StartupChartTypes.
			StartupChartTypesBools = new bool[Editor.SupportedChartTypes.Length];
			DefaultStartupChartTypesBools = new bool[Editor.SupportedChartTypes.Length];
			foreach (var chartType in StartupChartTypes)
			{
				StartupChartTypesBools[FindSupportedChartTypeIndex(chartType)] = true;
			}

			foreach (var chartType in DefaultStartupChartTypes)
			{
				DefaultStartupChartTypesBools[FindSupportedChartTypeIndex(chartType)] = true;
			}
		}

		public void PreSave()
		{
			// Set up StartupChartTypes from StartupChartTypesBools.
			var count = 0;
			for (var i = 0; i < StartupChartTypesBools.Length; i++)
			{
				if (StartupChartTypesBools[i])
					count++;
			}
			StartupChartTypes = new SMCommon.ChartType[count];
			count = 0;
			for (var i = 0; i < StartupChartTypesBools.Length; i++)
			{
				if (StartupChartTypesBools[i])
				{
					StartupChartTypes[count++] = Editor.SupportedChartTypes[i];
				}
			}
		}

		private int FindSupportedChartTypeIndex(ChartType chartType)
		{
			for (int i = 0; i < Editor.SupportedChartTypes.Length; i++)
			{
				if (Editor.SupportedChartTypes[i] == chartType)
				{
					return i;
				}
			}
			return 0;
		}
	}

	/// <summary>
	/// Action to restore Options preferences to their default values.
	/// </summary>
	internal sealed class ActionRestoreOptionPreferenceDefaults : EditorAction
	{
		private readonly int PreviousRecentFilesHistorySize;
		private readonly SMCommon.ChartType PreviousDefaultStepsType;
		private readonly SMCommon.ChartDifficultyType PreviousDefaultDifficultyType;
		private readonly double PreviousPreviewFadeInTime;
		private readonly double PreviousPreviewFadeOutTime;
		private readonly bool[] PreviousStartupChartTypesBools;
		private readonly bool PreviousOpenLastOpenedFileOnLaunch;

		public ActionRestoreOptionPreferenceDefaults()
		{
			var p = Preferences.Instance.PreferencesOptions;
			
			PreviousRecentFilesHistorySize = p.RecentFilesHistorySize;
			PreviousDefaultStepsType = p.DefaultStepsType;
			PreviousDefaultDifficultyType = p.DefaultDifficultyType;
			PreviousPreviewFadeInTime = p.PreviewFadeInTime;
			PreviousPreviewFadeOutTime = p.PreviewFadeOutTime;
			PreviousStartupChartTypesBools = (bool[])p.StartupChartTypesBools.Clone();
			PreviousOpenLastOpenedFileOnLaunch = p.OpenLastOpenedFileOnLaunch;
		}

		public override bool AffectsFile()
		{
			return false;
		}

		public override string ToString()
		{
			return "Restore option default preferences.";
		}

		public override void Do()
		{
			var p = Preferences.Instance.PreferencesOptions;
			p.RecentFilesHistorySize = PreferencesOptions.DefaultRecentFilesHistorySize;
			p.DefaultStepsType = PreferencesOptions.DefaultDefaultStepsType;
			p.DefaultDifficultyType = PreferencesOptions.DefaultDefaultDifficultyType;
			p.PreviewFadeInTime = PreferencesOptions.DefaultPreviewFadeInTime;
			p.PreviewFadeOutTime = PreferencesOptions.DefaultPreviewFadeOutTime;
			p.StartupChartTypesBools = (bool[])PreferencesOptions.DefaultStartupChartTypesBools.Clone();
			p.OpenLastOpenedFileOnLaunch = PreferencesOptions.DefaultOpenLastOpenedFileOnLaunch;
		}

		public override void Undo()
		{
			var p = Preferences.Instance.PreferencesOptions;
			p.RecentFilesHistorySize = PreviousRecentFilesHistorySize;
			p.DefaultStepsType = PreviousDefaultStepsType;
			p.DefaultDifficultyType = PreviousDefaultDifficultyType;
			p.PreviewFadeInTime = PreviousPreviewFadeInTime;
			p.PreviewFadeOutTime = PreviousPreviewFadeOutTime;
			p.StartupChartTypesBools = (bool[])PreviousStartupChartTypesBools.Clone();
			p.OpenLastOpenedFileOnLaunch = PreviousOpenLastOpenedFileOnLaunch;
		}
	}
}

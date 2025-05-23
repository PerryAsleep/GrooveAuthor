﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Fumen;
using Fumen.ChartDefinition;
using Fumen.Converters;
using Microsoft.Xna.Framework.Graphics;
using static Fumen.Converters.SMCommon;
using static Fumen.Converters.SMWriterBase;
using static StepManiaEditor.ImGuiUtils;
using static StepManiaEditor.Utils;
using static System.Diagnostics.Debug;

namespace StepManiaEditor;

// ReSharper disable InconsistentNaming
internal enum Selectable
{
	YES,
	NO,
	ROULETTE,
	ES,
	OMES,
}
// ReSharper restore InconsistentNaming

/// <summary>
/// Editor representation of a Stepmania song.
/// An EditorSong can have multiple EditorCharts.
/// 
/// EditorSong is not thread-safe. Some actions, like saving, are asynchronous. While asynchronous actions
/// are running edits are forbidden. Call CanBeEdited to determine if the EditorSong can be edited or not.
/// Additionally, Observers can listen for the NotificationCanEditChanged notification to respond to changes
/// in mutability.
/// 
/// It is expected that Update is called once per frame.
/// </summary>
internal sealed class EditorSong : Notifier<EditorSong>, Fumen.IObserver<WorkQueue>, IDisposable
{
	/// <summary>
	/// Data saved in the song file as a custom data chunk of Editor-specific data at the Song level.
	/// </summary>
	private class CustomSaveDataV1
	{
		public double SyncOffset;
		public ChartType? DefaultChartType;
		public int DefaultChartIndex;
	}

	/// <summary>
	/// Parameters to use when saving a song.
	/// </summary>
	internal class SaveParameters
	{
		public SaveParameters(FileFormatType fileType, string fullPath, Action<bool> callback = null,
			EditorPack packToSave = null)
		{
			FileType = fileType;
			FullPath = fullPath;
			Callback = callback;
			PackToSave = packToSave;
		}

		/// <summary>
		/// FileFormatType to save as.
		/// </summary>
		public readonly FileFormatType FileType;

		/// <summary>
		/// Full path to the file to save to.
		/// </summary>
		public readonly string FullPath;

		/// <summary>
		/// Optional EditorPack to save while saving the song.
		/// </summary>
		public readonly EditorPack PackToSave;

		/// <summary>
		/// Action to call when saving is complete.
		/// The parameter represents whether saving succeeded.
		/// </summary>
		public readonly Action<bool> Callback;

		/// <summary>
		/// Whether or not to require identical timing events between charts when saving sm files.
		/// </summary>
		public bool RequireIdenticalTimingInSmFiles;

		/// <summary>
		/// Whether or not to omit chart timing data and force song timing data.
		/// </summary>
		public bool OmitChartTimingData;

		/// <summary>
		/// Whether or not to omit custom save data.
		/// </summary>
		public bool OmitCustomSaveData;

		/// <summary>
		/// Whether or not to anonymize save data.
		/// </summary>
		public bool AnonymizeSaveData;

		/// <summary>
		/// Whether or not to use the StepF2 format for Pump routine charts.
		/// </summary>
		public bool UseStepF2ForPumpRoutine;
	}

	/// <summary>
	/// Version of custom data saved to the Song.
	/// </summary>
	private const int CustomSaveDataVersion = 1;

	public const string NotificationCanEditChanged = "CanEditChanged";
	public const string NotificationMusicChanged = "MusicChanged";
	public const string NotificationMusicPreviewChanged = "MusicPreviewChanged";
	public const string NotificationSyncOffsetChanged = "SyncOffsetChanged";
	public const string NotificationMusicOffsetChanged = "MusicOffsetChanged";
	public const string NotificationSampleLengthChanged = "SampleLengthChanged";
	public const string NotificationFileDirectoryChanged = "FileDirectoryChanged";

	private const string TagCustomSongData = "SongData";
	private const string TagCustomSongDataVersion = "SongDataVersion";

	/// <summary>
	/// Options for serializing and deserializing custom Song data.
	/// </summary>
	private static readonly JsonSerializerOptions CustomSaveDataSerializationOptions = new()
	{
		Converters =
		{
			new JsonStringEnumConverter(JsonNamingPolicy.CamelCase),
		},
		ReadCommentHandling = JsonCommentHandling.Skip,
		IncludeFields = true,
	};

	/// <summary>
	/// WorkQueue for long-running tasks like saving.
	/// </summary>
	private readonly WorkQueue WorkQueue;

	/// <summary>
	/// All EditorCharts for this EditorSong.
	/// </summary>
	private readonly Dictionary<ChartType, List<EditorChart>> Charts = new();

	/// <summary>
	/// The EditorChart to use for song timing data.
	/// StepMania requires some timing data to be persisted at the Song level.
	/// </summary>
	private EditorChart TimingChartInternal;

	/// <summary>
	/// Charts from the original Song that are unsupported in the editor.
	/// These are saved off into this member so they can be saved back out.
	/// </summary>
	private readonly List<Chart> UnsupportedCharts = [];

	/// <summary>
	/// Extras from the original Song.
	/// These are saved off into this member so they can be saved back out.
	/// </summary>
	private readonly Extras OriginalSongExtras;

	private string FileDirectory;
	private string FileName;
	private string FileFullPath;
	private FileFormat FileFormat;

	/// <summary>
	/// Whether or not this EditorSong is currently saving.
	/// </summary>
	private bool Saving;

	private DateTime LastSaveCompleteTime;

	#region Properties

	public string Title
	{
		get => TitleInternal;
		set
		{
			Assert(CanBeEdited());
			if (!CanBeEdited())
				return;
			TitleInternal = value;
		}
	}

	private string TitleInternal = "";

	public string TitleTransliteration
	{
		get => TitleTransliterationInternal;
		set
		{
			Assert(CanBeEdited());
			if (!CanBeEdited())
				return;
			TitleTransliterationInternal = value;
		}
	}

	private string TitleTransliterationInternal = "";

	public string Subtitle
	{
		get => SubtitleInternal;
		set
		{
			Assert(CanBeEdited());
			if (!CanBeEdited())
				return;
			SubtitleInternal = value;
		}
	}

	private string SubtitleInternal = "";

	public string SubtitleTransliteration
	{
		get => SubtitleTransliterationInternal;
		set
		{
			Assert(CanBeEdited());
			if (!CanBeEdited())
				return;
			SubtitleTransliterationInternal = value;
		}
	}

	private string SubtitleTransliterationInternal = "";

	public string Artist
	{
		get => ArtistInternal;
		set
		{
			Assert(CanBeEdited());
			if (!CanBeEdited())
				return;
			ArtistInternal = value;
		}
	}

	private string ArtistInternal = "";

	public string ArtistTransliteration
	{
		get => ArtistTransliterationInternal;
		set
		{
			Assert(CanBeEdited());
			if (!CanBeEdited())
				return;
			ArtistTransliterationInternal = value;
		}
	}

	private string ArtistTransliterationInternal = "";

	public string Genre
	{
		get => GenreInternal;
		set
		{
			Assert(CanBeEdited());
			if (!CanBeEdited())
				return;
			GenreInternal = value;
		}
	}

	private string GenreInternal = "";

	public string Origin
	{
		get => OriginInternal;
		set
		{
			Assert(CanBeEdited());
			if (!CanBeEdited())
				return;
			OriginInternal = value;
		}
	}

	private string OriginInternal = "";

	public string Credit
	{
		get => CreditInternal;
		set
		{
			Assert(CanBeEdited());
			if (!CanBeEdited())
				return;
			CreditInternal = value;
		}
	}

	private string CreditInternal = "";

	public string LyricsPath
	{
		get => LyricsPathInternal;
		set
		{
			Assert(CanBeEdited());
			if (!CanBeEdited())
				return;
			LyricsPathInternal = value;
		}
	}

	private string LyricsPathInternal = "";

	public string PreviewVideoPath
	{
		get => PreviewVideoPathInternal;
		set
		{
			Assert(CanBeEdited());
			if (!CanBeEdited())
				return;
			PreviewVideoPathInternal = value;
		}
	}

	private string PreviewVideoPathInternal = "";

	public string MusicPath
	{
		get => MusicPathInternal;
		set
		{
			Assert(CanBeEdited());
			if (!CanBeEdited())
				return;
			var newMusicPath = value ?? "";
			if (MusicPath == newMusicPath)
				return;

			MusicPathInternal = newMusicPath;
			Notify(NotificationMusicChanged, this);
		}
	}

	// ReSharper disable once MemberInitializerValueIgnored
	private string MusicPathInternal = "";

	public string MusicPreviewPath
	{
		get => MusicPreviewPathInternal;
		set
		{
			Assert(CanBeEdited());
			if (!CanBeEdited())
				return;
			var newMusicPreviewPath = value ?? "";
			if (MusicPreviewPathInternal == newMusicPreviewPath)
				return;

			MusicPreviewPathInternal = newMusicPreviewPath;
			Notify(NotificationMusicPreviewChanged, this);
		}
	}

	// ReSharper disable once MemberInitializerValueIgnored
	private string MusicPreviewPathInternal = "";

	public double MusicOffset
	{
		get => MusicOffsetInternal;
		set
		{
			Assert(CanBeEdited());
			if (!CanBeEdited())
				return;
			if (MusicOffsetInternal.DoubleEquals(value))
				return;

			DeletePreviewEvents();
			MusicOffsetInternal = value;
			AddPreviewEvents();
			Notify(NotificationMusicOffsetChanged, this);
		}
	}

	private double MusicOffsetInternal;

	public double SyncOffset
	{
		get => SyncOffsetInternal;
		set
		{
			Assert(CanBeEdited());
			if (!CanBeEdited())
				return;
			if (SyncOffsetInternal.DoubleEquals(value))
				return;

			DeletePreviewEvents();
			SyncOffsetInternal = value;
			AddPreviewEvents();
			Notify(NotificationSyncOffsetChanged, this);
		}
	}

	private double SyncOffsetInternal;

	public double LastSecondHint
	{
		get => LastSecondHintInternal;
		set
		{
			Assert(CanBeEdited());
			if (!CanBeEdited())
				return;
			if (LastSecondHintInternal.DoubleEquals(value))
				return;

			DeleteLastSecondHintEvents();
			LastSecondHintInternal = value;
			AddLastSecondHintEvents();
		}
	}

	private double LastSecondHintInternal;

	public double SampleStart
	{
		get => SampleStartInternal;
		set
		{
			Assert(CanBeEdited());
			if (!CanBeEdited())
				return;
			if (SampleStartInternal.DoubleEquals(value))
				return;

			DeletePreviewEvents();
			SampleStartInternal = value;
			AddPreviewEvents();
		}
	}

	private double SampleStartInternal;

	public double SampleLength
	{
		get => SampleLengthInternal;
		set
		{
			Assert(CanBeEdited());
			if (!CanBeEdited())
				return;
			if (SampleLengthInternal.DoubleEquals(value))
				return;

			SampleLengthInternal = value;
			Notify(NotificationSampleLengthChanged, this);
		}
	}

	private double SampleLengthInternal;

	public Selectable Selectable
	{
		get => SelectableInternal;
		set
		{
			Assert(CanBeEdited());
			if (!CanBeEdited())
				return;
			SelectableInternal = value;
		}
	}

	private Selectable SelectableInternal = Selectable.YES;

	public EditorChart TimingChart
	{
		get => TimingChartInternal;
		set
		{
			Assert(CanBeEdited());
			if (!CanBeEdited())
				return;
			var found = false;
			if (value != null)
			{
				foreach (var kvp in Charts)
				{
					foreach (var chart in kvp.Value)
					{
						if (chart == value)
						{
							found = true;
							break;
						}
					}

					if (found)
						break;
				}

				Assert(found);
				// ReSharper disable once ConditionIsAlwaysTrueOrFalse
				if (!found)
					// ReSharper disable once HeuristicUnreachableCode
					return;
			}

			TimingChartInternal = value;
		}
	}

	#endregion Properties

	#region EditorImageData

	public string BannerPath
	{
		get => Banner.Path;
		set
		{
			Assert(CanBeEdited());
			if (!CanBeEdited())
				return;
			Banner.UpdatePath(FileDirectory, value);
		}
	}

	private readonly EditorImageData Banner;

	public IReadOnlyEditorImageData GetBanner()
	{
		return Banner;
	}

	public string BackgroundPath
	{
		get => Background.Path;
		set
		{
			Assert(CanBeEdited());
			if (!CanBeEdited())
				return;
			Background.UpdatePath(FileDirectory, value);
		}
	}

	private readonly EditorImageData Background;

	public IReadOnlyEditorImageData GetBackground()
	{
		return Background;
	}

	public string JacketPath
	{
		get => Jacket.Path;
		set
		{
			Assert(CanBeEdited());
			if (!CanBeEdited())
				return;
			Jacket.UpdatePath(FileDirectory, value);
		}
	}

	private readonly EditorImageData Jacket;

	public IReadOnlyEditorImageData GetJacket()
	{
		return Jacket;
	}

	public string CDImagePath
	{
		get => CDImage.Path;
		set
		{
			Assert(CanBeEdited());
			if (!CanBeEdited())
				return;
			CDImage.UpdatePath(FileDirectory, value);
		}
	}

	private readonly EditorImageData CDImage;

	public IReadOnlyEditorImageData GetCDImage()
	{
		return CDImage;
	}

	public string DiscImagePath
	{
		get => DiscImage.Path;
		set
		{
			Assert(CanBeEdited());
			if (!CanBeEdited())
				return;
			DiscImage.UpdatePath(FileDirectory, value);
		}
	}

	private readonly EditorImageData DiscImage;

	public IReadOnlyEditorImageData GetDiscImage()
	{
		return DiscImage;
	}

	public string CDTitlePath
	{
		get => CDTitle.Path;
		set
		{
			Assert(CanBeEdited());
			if (!CanBeEdited())
				return;
			CDTitle.UpdatePath(FileDirectory, value);
		}
	}

	private readonly EditorImageData CDTitle;

	public IReadOnlyEditorImageData GetCDTitle()
	{
		return CDTitle;
	}

	#endregion EditorImageData

	#region Constructors

	public EditorSong(
		GraphicsDevice graphicsDevice,
		ImGuiRenderer imGuiRenderer)
	{
		WorkQueue = new WorkQueue();
		WorkQueue.AddObserver(this);

		Banner = new EditorImageData(FileDirectory, graphicsDevice, imGuiRenderer, (uint)GetBannerWidth(),
			(uint)GetBannerHeight(), null, false);
		Background = new EditorImageData(FileDirectory, graphicsDevice, imGuiRenderer, (uint)GetBackgroundWidth(),
			(uint)GetBackgroundHeight(), null, true);
		Jacket = new EditorImageData(null);
		CDImage = new EditorImageData(null);
		DiscImage = new EditorImageData(null);
		CDTitle = new EditorImageData(FileDirectory, graphicsDevice, imGuiRenderer, (uint)GetCDTitleWidth(),
			(uint)GetCDTitleHeight(), null, false);

		MusicPathInternal = "";
		MusicPreviewPathInternal = "";
		SyncOffsetInternal = Preferences.Instance.PreferencesOptions.NewSongSyncOffset;
	}

	public EditorSong(
		string fullFilePath,
		Song song,
		GraphicsDevice graphicsDevice,
		ImGuiRenderer imGuiRenderer,
		Func<Chart, bool> isChartSupported)
	{
		WorkQueue = new WorkQueue();
		WorkQueue.AddObserver(this);

		SetFullFilePath(fullFilePath);

		OriginalSongExtras = song.Extras;

		TitleInternal = song.Title ?? "";
		TitleTransliterationInternal = song.TitleTransliteration ?? "";
		SubtitleInternal = song.SubTitle ?? "";
		SubtitleTransliterationInternal = song.SubTitleTransliteration ?? "";
		ArtistInternal = song.Artist ?? "";
		ArtistTransliterationInternal = song.ArtistTransliteration ?? "";
		GenreInternal = song.Genre ?? "";
		song.Extras.TryGetExtra(TagOrigin, out OriginInternal, true);
		OriginInternal ??= "";
		song.Extras.TryGetExtra(TagCredit, out CreditInternal, true);
		CreditInternal ??= "";

		Banner = new EditorImageData(FileDirectory, graphicsDevice, imGuiRenderer, (uint)GetBannerWidth(),
			(uint)GetBannerHeight(), song.SongSelectImage, false);
		song.Extras.TryGetExtra(TagBackground, out string tempStr, true);
		Background = new EditorImageData(FileDirectory, graphicsDevice, imGuiRenderer, (uint)GetBackgroundWidth(),
			(uint)GetBackgroundHeight(), tempStr, true);
		song.Extras.TryGetExtra(TagJacket, out tempStr, true);
		Jacket = new EditorImageData(tempStr);
		song.Extras.TryGetExtra(TagCDImage, out tempStr, true);
		CDImage = new EditorImageData(tempStr);
		song.Extras.TryGetExtra(TagDiscImage, out tempStr, true);
		DiscImage = new EditorImageData(tempStr);
		song.Extras.TryGetExtra(TagCDTitle, out tempStr, true);
		CDTitle = new EditorImageData(FileDirectory, graphicsDevice, imGuiRenderer, (uint)GetCDTitleWidth(),
			(uint)GetCDTitleHeight(), tempStr, false);

		song.Extras.TryGetExtra(TagLyricsPath, out LyricsPathInternal, true);
		LyricsPathInternal ??= "";
		song.Extras.TryGetExtra(TagPreviewVid, out PreviewVideoPathInternal, true);
		PreviewVideoPathInternal ??= "";

		song.Extras.TryGetExtra(TagMusic, out string musicPath, true);
		MusicPathInternal = musicPath;

		MusicPreviewPathInternal = song.PreviewMusicFile ?? "";
		song.Extras.TryGetExtra(TagOffset, out double musicOffset, true);
		MusicOffsetInternal = musicOffset;

		song.Extras.TryGetExtra(TagLastSecondHint, out double lastSecondHint, true);
		LastSecondHintInternal = lastSecondHint;

		SampleStartInternal = song.PreviewSampleStart;
		SampleLengthInternal = song.PreviewSampleLength;

		var hasDisplayTempo = song.Extras.TryGetExtra(TagDisplayBPM, out object _, true);
		DisplayTempo displayTempo = null;
		if (hasDisplayTempo)
		{
			displayTempo = new DisplayTempo();
			displayTempo.FromString(GetDisplayBPMStringFromSourceExtrasList(song.Extras, null));
		}

		song.Extras.TryGetExtra(TagSelectable, out string selectableString, true);
		if (!string.IsNullOrEmpty(selectableString))
		{
			if (!Enum.TryParse(selectableString, true, out SelectableInternal))
			{
				SelectableInternal = Selectable.YES;
				Logger.Warn($"Failed to parse Song {TagSelectable} value: '{selectableString}'.");
			}
		}

		SyncOffsetInternal = Preferences.Instance.PreferencesOptions.OpenSongSyncOffset;

		foreach (var chart in song.Charts)
		{
			if (!isChartSupported(chart))
			{
				UnsupportedCharts.Add(chart);
				continue;
			}

			EditorChart editorChart;
			try
			{
				editorChart = new EditorChart(this, chart);
			}
			catch (Exception e)
			{
				Logger.Error($"Failed to set up {chart.Type} chart. {e}");
				continue;
			}

			if (!Charts.ContainsKey(editorChart.ChartType))
				Charts.Add(editorChart.ChartType, []);
			Charts[editorChart.ChartType].Add(editorChart);

			if (hasDisplayTempo && !editorChart.HasDisplayTempoFromChart())
			{
				editorChart.CopyDisplayTempo(displayTempo);
			}
		}

		DeserializeCustomSongData(song);

		if (TimingChart == null)
			ChooseTimingChart();

		UpdateChartSortInternal();
	}

	#endregion Constructors

	#region Clean-up

	public void RemoveObservers()
	{
		WorkQueue.RemoveObserver(this);
		foreach (var editorChartsForChartType in Charts)
		{
			foreach (var editorChart in editorChartsForChartType.Value)
			{
				editorChart.RemoveObservers();
			}
		}
	}

	#endregion Clean-up

	#region EditorChart

	public IReadOnlyList<EditorChart> GetCharts(ChartType chartType)
	{
		return Charts.GetValueOrDefault(chartType);
	}

	public IReadOnlyList<EditorChart> GetCharts()
	{
		var allCharts = new List<EditorChart>();
		foreach (var kvp in Charts)
		{
			allCharts.AddRange(kvp.Value);
		}

		return allCharts;
	}

	public int GetNumCharts()
	{
		var numCharts = 0;
		foreach (var chartsByType in Charts)
			numCharts += chartsByType.Value.Count;
		return numCharts;
	}

	public EditorChart AddChart(ChartType chartType)
	{
		Assert(CanBeEdited());
		if (!CanBeEdited())
			return null;

		EditorChart chart;
		try
		{
			chart = new EditorChart(this, chartType);
		}
		catch (Exception e)
		{
			Logger.Error($"Failed add {ChartTypeString(chartType)} chart. {e}");
			return null;
		}

		if (!Charts.ContainsKey(chartType))
			Charts.Add(chartType, []);
		Charts[chartType].Add(chart);

		TimingChart ??= chart;

		UpdateChartSortInternal();
		return chart;
	}

	public EditorChart AddChart(EditorChart chart)
	{
		Assert(CanBeEdited());
		if (!CanBeEdited())
			return null;

		if (!Charts.ContainsKey(chart.ChartType))
			Charts.Add(chart.ChartType, []);
		Charts[chart.ChartType].Add(chart);
		UpdateChartSortInternal();
		return chart;
	}

	public void DeleteChart(EditorChart chart)
	{
		Assert(CanBeEdited());
		if (!CanBeEdited())
			return;

		if (!Charts.TryGetValue(chart.ChartType, out var charts))
			return;
		charts.Remove(chart);
		if (Charts[chart.ChartType].Count == 0)
			Charts.Remove(chart.ChartType);

		// When deleting the timing Chart, choose a new one.
		if (chart == TimingChart)
			ChooseTimingChart();

		UpdateChartSortInternal();
	}

	public void UpdateChartSort()
	{
		Assert(CanBeEdited());
		if (!CanBeEdited())
			return;

		UpdateChartSortInternal();
	}

	private void UpdateChartSortInternal()
	{
		foreach (var kvp in Charts)
		{
			kvp.Value.Sort(new ChartComparer());
		}

		var index = 0;
		foreach (var chart in GetSortedCharts())
		{
			chart.SetIndexInSong(index);
			index++;
		}
	}

	public IReadOnlyList<EditorChart> GetSortedCharts()
	{
		var sortedCharts = new List<EditorChart>();
		foreach (var chartType in Editor.SupportedChartTypes)
		{
			if (Charts.TryGetValue(chartType, out var charts))
			{
				sortedCharts.AddRange(charts);
			}
		}

		return sortedCharts;
	}

	#endregion EditorChart

	#region Default Chart Selection

	/// <summary>
	/// Resets the TimingChart to a good default based on the user's preferences.
	/// </summary>
	private void ChooseTimingChart()
	{
		var p = Preferences.Instance.PreferencesOptions;
		TimingChart = SelectBestChart(p.DefaultStepsType, p.DefaultDifficultyType);
	}

	/// <summary>
	/// Returns the best EditorChart to use based on the user's preferences and the available EditorCharts for this EditorSong.
	/// </summary>
	/// <param name="preferredChartType">The preferred ChartType (StepMania StepsType) to use.</param>
	/// <param name="preferredChartDifficultyType">The preferred DifficultyType to use.</param>
	/// <returns>Best EditorChart to use or null if no valid EditorCharts exist.</returns>
	public EditorChart SelectBestChart(
		ChartType preferredChartType,
		ChartDifficultyType preferredChartDifficultyType)
	{
		var preferredChartsByType = GetCharts(preferredChartType);
		var hasChartsOfPreferredType = preferredChartsByType != null;

		// Choose the preferred chart, if it exists.
		if (hasChartsOfPreferredType)
		{
			foreach (var chart in preferredChartsByType)
			{
				if (chart.ChartDifficultyType == preferredChartDifficultyType)
					return chart;
			}
		}

		var orderedDifficultyTypes = new[]
		{
			ChartDifficultyType.Challenge,
			ChartDifficultyType.Hard,
			ChartDifficultyType.Medium,
			ChartDifficultyType.Easy,
			ChartDifficultyType.Beginner,
			ChartDifficultyType.Edit,
		};

		// If the preferred chart doesn't exist, try to choose the highest difficulty type
		// of the preferred chart type.
		if (hasChartsOfPreferredType)
		{
			foreach (var currentDifficultyType in orderedDifficultyTypes)
			{
				foreach (var chart in preferredChartsByType)
				{
					if (chart.ChartDifficultyType == currentDifficultyType)
						return chart;
				}
			}
		}

		// No charts of the specified type exist. Try the next best type.
		var nextBestChartType = ChartType.dance_single;
		var hasNextBestChartType = true;
		if (preferredChartType == ChartType.dance_single)
			nextBestChartType = ChartType.dance_double;
		else if (preferredChartType == ChartType.dance_double)
			nextBestChartType = ChartType.dance_single;
		else if (preferredChartType == ChartType.pump_single)
			nextBestChartType = ChartType.pump_double;
		else if (preferredChartType == ChartType.pump_double)
			nextBestChartType = ChartType.pump_single;
		else
			hasNextBestChartType = false;
		if (hasNextBestChartType)
		{
			var nextBestChartsByType = GetCharts(nextBestChartType);
			if (nextBestChartsByType != null)
			{
				foreach (var currentDifficultyType in orderedDifficultyTypes)
				{
					foreach (var chart in nextBestChartsByType)
					{
						if (chart.ChartDifficultyType == currentDifficultyType)
							return chart;
					}
				}
			}
		}

		// At this point, just return the first chart we have.
		foreach (var supportedChartType in Editor.SupportedChartTypes)
		{
			var charts = GetCharts(supportedChartType);
			if (charts?.Count > 0)
			{
				return charts[0];
			}
		}

		return null;
	}

	#endregion Default Chart Selection

	#region Misc

	public void AddObservers(
		Fumen.IObserver<EditorSong> observer,
		Fumen.IObserver<EditorChart> chartObserver)
	{
		if (observer != null)
			AddObserver(observer);

		foreach (var editorChartsForChartType in Charts)
		{
			foreach (var editorChart in editorChartsForChartType.Value)
			{
				editorChart.AddObserver(chartObserver);
			}
		}
	}

	public void RemoveObservers(
		Fumen.IObserver<EditorSong> observer,
		Fumen.IObserver<EditorChart> chartObserver)
	{
		if (observer != null)
			RemoveObserver(observer);

		foreach (var editorChartsForChartType in Charts)
		{
			foreach (var editorChart in editorChartsForChartType.Value)
			{
				editorChart.RemoveObserver(chartObserver);
			}
		}
	}

	public bool CanBeEdited()
	{
		// The Song cannot be edited if work is queued up.
		// The exception to that is if that work itself is synchronous as it means the edit
		// is coming from that enqueued work.
		if (WorkQueue.IsRunningSynchronousWork())
			return true;
		return WorkQueue.IsEmpty();
	}

	public bool CanAllChartsBeEdited()
	{
		foreach (var kvp in Charts)
		{
			foreach (var chart in kvp.Value)
			{
				if (!chart.CanBeEdited())
					return false;
			}
		}

		return true;
	}

	public double GetBestChartStartingTempo()
	{
		// If other charts are present, use the most common tempo from the other charts.
		var histogram = new Dictionary<double, int>();
		foreach (var kvp in Charts)
		{
			foreach (var chart in kvp.Value)
			{
				var tempo = chart.GetStartingTempo();
				if (histogram.TryGetValue(tempo, out var count))
				{
					histogram[tempo] = count + 1;
				}
				else
				{
					histogram.Add(tempo, 1);
				}
			}
		}

		if (histogram.Count > 0)
		{
			return histogram.Aggregate((x, y) => x.Value > y.Value ? x : y).Key;
		}

		// Otherwise, if a tempo is specified on the song from the sm/ssc file, use that.
		if (OriginalSongExtras?.TryGetExtra(TagBPMs, out Dictionary<double, double> songTempos, true) ?? false)
		{
			if (songTempos.TryGetValue(0.0, out var firstTempo))
			{
				if (firstTempo >= EditorTempoEvent.MinTempo)
				{
					return firstTempo;
				}
			}
		}

		// Otherwise, if the song has an explicit display BPM, use that.
		if (OriginalSongExtras?.TryGetExtra(TagDisplayBPM, out object _, true) ?? false)
		{
			var displayTempo = new DisplayTempo();
			displayTempo.FromString(GetDisplayBPMStringFromSourceExtrasList(OriginalSongExtras, null));
			if (displayTempo.Mode == DisplayTempoMode.Specified)
			{
				if (displayTempo.SpecifiedTempoMin >= EditorTempoEvent.MinTempo)
				{
					return displayTempo.SpecifiedTempoMin;
				}
			}
		}

		// Failing all the above, use the default tempo.
		return EditorChart.DefaultTempo;
	}

	public Fraction GetBestChartStartingTimeSignature()
	{
		var histogram = new Dictionary<Fraction, int>();
		foreach (var kvp in Charts)
		{
			foreach (var chart in kvp.Value)
			{
				var timeSignature = chart.GetStartingTimeSignature();
				if (histogram.TryGetValue(timeSignature, out var count))
				{
					histogram[timeSignature] = count + 1;
				}
				else
				{
					histogram.Add(timeSignature, 1);
				}
			}
		}

		if (histogram.Count > 0)
		{
			return histogram.Aggregate((x, y) => x.Value > y.Value ? x : y).Key;
		}

		return EditorChart.DefaultTimeSignature;
	}

	public bool IsUsingSongForPreview()
	{
		return string.IsNullOrEmpty(MusicPreviewPath);
	}

	public bool IsMusicInvalid()
	{
		// If there is music set for the song, it is not invalid.
		if (!string.IsNullOrEmpty(MusicPath))
			return false;

		// There is no music set. This is only okay if there is at least one
		// chart and all charts have music set.
		var hasAtLeastOneChart = false;
		var foundChartWithNoMusic = false;
		foreach (var kvp in Charts)
		{
			foreach (var chart in kvp.Value)
			{
				hasAtLeastOneChart = true;
				if (string.IsNullOrEmpty(chart.MusicPath))
				{
					foundChartWithNoMusic = true;
					break;
				}
			}

			if (foundChartWithNoMusic)
				break;
		}

		// If there is at least one chart and all charts have music, it is not invalid.
		if (hasAtLeastOneChart && !foundChartWithNoMusic)
			return false;

		// There is no song music, and there are either no charts or there are charts
		// without explicit music set. This is not valid.
		return true;
	}

	/// <summary>
	/// Updates this EditorSong. Expected to be called once per frame.
	/// </summary>
	/// <param name="currentTime">Total application time in seconds.</param>
	public void Update(double currentTime)
	{
		// Update the WorkQueue.
		WorkQueue.Update();

		// Update potential texture animations.
		Banner?.Update(currentTime);
		Background?.Update(currentTime);
		Jacket?.Update(currentTime);
		CDImage?.Update(currentTime);
		DiscImage?.Update(currentTime);
		CDTitle?.Update(currentTime);

		// Update all charts.
		foreach (var editorChartsForChartType in Charts)
		{
			foreach (var editorChart in editorChartsForChartType.Value)
			{
				editorChart.Update();
			}
		}
	}

	#endregion Misc

	#region Time-Based Event Shifting

	/// <summary>
	/// Deletes all EditorPreviewRegionEvents from all charts.
	/// When modifying properties that affect the song time, the preview region sort may change
	/// relative to other events. We therefore need to delete these events then re-add them.
	/// </summary>
	private void DeletePreviewEvents()
	{
		Assert(CanBeEdited());
		if (!CanBeEdited())
			return;
		foreach (var kvp in Charts)
		{
			foreach (var chart in kvp.Value)
			{
				chart.DeletePreviewEvent();
			}
		}
	}

	/// <summary>
	/// Adds EditorPreviewRegionEvents to all charts.
	/// When modifying properties that affect the song time, the preview region sort may change
	/// relative to other events. We therefore need to delete these events then re-add them.
	/// </summary>
	private void AddPreviewEvents()
	{
		Assert(CanBeEdited());
		if (!CanBeEdited())
			return;
		foreach (var kvp in Charts)
		{
			foreach (var chart in kvp.Value)
			{
				chart.AddPreviewEvent();
			}
		}
	}

	/// <summary>
	/// Deletes all EditorLastSecondHintEvents from all charts.
	/// When modifying the last second hint value, the EditorLastSecondHintEvent event sort may change
	/// relative to other events. We therefore need to delete these events then re-add them.
	/// </summary>
	private void DeleteLastSecondHintEvents()
	{
		Assert(CanBeEdited());
		if (!CanBeEdited())
			return;
		foreach (var kvp in Charts)
		{
			foreach (var chart in kvp.Value)
			{
				chart.DeleteLastSecondHintEvent();
			}
		}
	}

	/// <summary>
	/// Adds EditorLastSecondHintEvents to all charts.
	/// When modifying the last second hint value, the EditorLastSecondHintEvent event sort may change
	/// relative to other events. We therefore need to delete these events then re-add them.
	/// </summary>
	private void AddLastSecondHintEvents()
	{
		Assert(CanBeEdited());
		if (!CanBeEdited())
			return;
		foreach (var kvp in Charts)
		{
			foreach (var chart in kvp.Value)
			{
				chart.AddLastSecondHintEvent();
			}
		}
	}

	#endregion Time-Based Event Shifting

	#region IObserver

	/// <summary>
	/// Function called when receiving a notification from the WorkQueue.
	/// </summary>
	public void OnNotify(string eventId, WorkQueue notifier, object payload)
	{
		switch (eventId)
		{
			case WorkQueue.NotificationWorking:
				Notify(NotificationCanEditChanged, this);
				break;
			case WorkQueue.NotificationWorkComplete:
				Notify(NotificationCanEditChanged, this);
				break;
		}
	}

	#endregion IObserver

	#region Saving

	public string GetFileDirectory()
	{
		return FileDirectory;
	}

	public string GetFileName()
	{
		return FileName;
	}

	public string GetFileFullPath()
	{
		return FileFullPath;
	}

	public FileFormat GetFileFormat()
	{
		return FileFormat;
	}

	/// <summary>
	/// Sets the full path of the EditorSong.
	/// Updates all relative paths to other assets to be relative to the new full path.
	/// </summary>
	/// <param name="fullFilePath">New full path of the EditorSong.</param>
	private void SetFullFilePath(string fullFilePath)
	{
		var oldPath = FileFullPath;

		// Update the path information.
		FileFullPath = fullFilePath;
		FileName = System.IO.Path.GetFileName(fullFilePath);
		FileDirectory = System.IO.Path.GetDirectoryName(fullFilePath);
		FileFormat = FileFormat.GetFileFormatByExtension(System.IO.Path.GetExtension(fullFilePath));

		// Update paths which were relative to the old path to be relative to the new path.
		UpdateRelativePaths(oldPath, FileFullPath);

		if (oldPath != FileFullPath)
			Notify(NotificationFileDirectoryChanged, this);
	}

	/// <summary>
	/// Updates all relative paths to be relative from the old full path to the new provided full path.
	/// </summary>
	/// <param name="oldFullPath">Old full path of this EditorSong.</param>
	/// <param name="newFullPath">New full path of this EditorSong.</param>
	private void UpdateRelativePaths(string oldFullPath, string newFullPath)
	{
		MusicPath = UpdateRelativePath(oldFullPath, newFullPath, MusicPath);
		MusicPreviewPath = UpdateRelativePath(oldFullPath, newFullPath, MusicPreviewPath);
		LyricsPath = UpdateRelativePath(oldFullPath, newFullPath, LyricsPath);
		PreviewVideoPath = UpdateRelativePath(oldFullPath, newFullPath, PreviewVideoPath);
		Banner?.UpdatePath(FileDirectory, UpdateRelativePath(oldFullPath, newFullPath, Banner.Path));
		Background?.UpdatePath(FileDirectory, UpdateRelativePath(oldFullPath, newFullPath, Background.Path));
		Jacket?.UpdatePath(FileDirectory, UpdateRelativePath(oldFullPath, newFullPath, Jacket.Path));
		CDImage?.UpdatePath(FileDirectory, UpdateRelativePath(oldFullPath, newFullPath, CDImage.Path));
		DiscImage?.UpdatePath(FileDirectory, UpdateRelativePath(oldFullPath, newFullPath, DiscImage.Path));
		CDTitle?.UpdatePath(FileDirectory, UpdateRelativePath(oldFullPath, newFullPath, CDTitle.Path));
	}

	/// <summary>
	/// Updates a path relative to the old full path to be relative to the new full path.
	/// Used to update EditorSong variables like MusicPath when the full path to the Song changes.
	/// </summary>
	/// <param name="oldFullPath">The old full path.</param>
	/// <param name="newFullPath">The new full path.</param>
	/// <param name="relativePath">The path relative to the old full path to update.</param>
	/// <returns>Path relative to the new full path.</returns>
	private static string UpdateRelativePath(string oldFullPath, string newFullPath, string relativePath)
	{
		if (string.IsNullOrEmpty(relativePath))
		{
			return relativePath;
		}

		try
		{
			// Occasionally paths will be absolute. This can occur if the song is new and hasn't been saved yet.
			// In that case there is no song path to be relative to. If the path is absolute, convert it to be
			// relative to the new full path.
			if (System.IO.Path.IsPathRooted(relativePath))
			{
				relativePath = Path.GetRelativePath(System.IO.Path.GetDirectoryName(newFullPath), relativePath);
			}
			// Normally, the relative path exists and is relative to the old full path. In this case, convert
			// the relative path to an absolute path first, then convert that absolute path to be relative to
			// the new full path.
			else if (!string.IsNullOrEmpty(oldFullPath))
			{
				var relativeFullPath = Path.GetFullPathFromRelativePathToFullPath(oldFullPath, relativePath);
				relativePath = Path.GetRelativePath(System.IO.Path.GetDirectoryName(newFullPath), relativeFullPath);
			}
		}
		catch (Exception)
		{
			// Ignored
		}

		return relativePath;
	}

	/// <summary>
	/// Returns whether or not the EditorSong is currently saving.
	/// </summary>
	/// <returns>True if the EditorSong is currently saving and false otherwise.</returns>
	public bool IsSaving()
	{
		return Saving;
	}

	/// <summary>
	/// Returns the last time the EditorSong was saved.
	/// </summary>
	/// <returns>The last time the EditorSong was saved</returns>
	public DateTime GetLastSaveCompleteTime()
	{
		return LastSaveCompleteTime;
	}

	/// <summary>
	/// Performs a series of checks prior to saving to ensure the song and charts have no
	/// incompatibilities with the given SaveParameters. Will log warnings and errors based
	/// on any incompatibilities.
	/// </summary>
	/// <param name="saveParameters">SaveParameters used for saving.</param>
	/// <returns>
	/// True if the song can be saved and false otherwise.
	/// </returns>
	private bool PerformPreSaveChecks(SaveParameters saveParameters)
	{
		var canBeSaved = true;

		// Perform format-specific checks.
		switch (saveParameters.FileType)
		{
			case FileFormatType.SM:
			{
				// LastSecondHint values aren't compatible with the sm format but can be safely ignored.
				if (LastSecondHint > 0.0)
				{
					Logger.Warn(
						$"Song has last second hint at {LastSecondHint}. Last second hint values are not compatible with {FileFormatType.SM} files and will be omitted.");
				}

				break;
			}
			case FileFormatType.SSC:
			{
				// When forcing song timing log errors on inconsistencies.
				// We could warn and continue, but it is better to avoid saving lossy data to disk.
				if (saveParameters.OmitChartTimingData)
				{
					if (!DoChartTimingAndScrollEventsMatch())
					{
						Logger.Error(
							"\"Remove Chart Timing\" was specified but the charts in this song have different timing and/or scroll events." +
							" Please address incompatibilities and try again." +
							$" Consider applying the timing and scroll events from {TimingChart.GetDescriptiveName()} to all charts using the button in the Song Properties window.");
						canBeSaved = false;
					}

					if (!DoNonTimingAndScrollEventsWhichStepmaniaTreatsAsSplitTimingEventsMatch())
					{
						Logger.Error(
							"\"Remove Chart Timing\" was specified but the charts in this song have different events which Stepmania" +
							" interprets as indicating charts should use their own timing." +
							" Please address incompatibilities and try again." +
							$" Consider applying all misc. Stepmania events from {TimingChart.GetDescriptiveName()} to all charts using the button in the Song Properties window.");
						canBeSaved = false;
					}
				}

				break;
			}
		}

		// Check each chart independently.
		foreach (var kvp in Charts)
		{
			var charts = kvp.Value;
			foreach (var chart in charts)
			{
				if (!chart.PerformPreSaveChecks(saveParameters))
					canBeSaved = false;
			}
		}

		// Sm file split-timing checks.
		if (saveParameters.FileType == FileFormatType.SM)
		{
			// Check for non timing and scroll events which cannot be split in sm files.
			// Treat this as a warning only since this does not affect timing.
			_ = DoNonTimingAndScrollEventsWhichCannotBeSplitByChartInSmFilesMatch();

			if (!DoChartTimingAndScrollEventsMatch())
			{
				if (saveParameters.RequireIdenticalTimingInSmFiles)
				{
					Logger.Error(
						$"The charts in this song have different timing and/or scroll events. {saveParameters.FileType} files do not support this. Consider saving this file as an {FileFormatType.SSC} file." +
						$" You can also force saving of {saveParameters.FileType} files with incompatible timing by unchecking \"Require Identical Timing in SM Files\".");
					canBeSaved = false;
				}
				else
				{
					Logger.Warn(
						$"The charts in this song have different timing and/or scroll events. {saveParameters.FileType} files do not support this. The saved charts will all use the timing and scroll events from {TimingChart.GetDescriptiveName()}."
						+ " To treat this as an error you can enable \"Require Identical Timing in SM Files\" in Advanced Save Options.");
				}
			}
		}

		if (!canBeSaved)
		{
			var errorString =
				$"Encountered one or more errors saving {saveParameters.FileType} file. Please address incompatibilities and try again.";
			if (saveParameters.FileType == FileFormatType.SM)
				errorString +=
					$" Consider saving as an {FileFormatType.SSC} file to address {FileFormatType.SM}-specific issues.";
			Logger.Error(errorString);
		}

		return canBeSaved;
	}

	/// <summary>
	/// Returns whether or not the timing and scroll events matching between all charts in the song.
	/// Logs warnings on inconsistencies.
	/// </summary>
	/// <returns>True if all timing and scroll events match and false otherwise.</returns>
	private bool DoChartTimingAndScrollEventsMatch()
	{
		var timingAndScrollMatches = true;
		var chartToCompareAgainst = TimingChart;
		foreach (var kvp in Charts)
		{
			var charts = kvp.Value;
			foreach (var chart in charts)
			{
				if (chartToCompareAgainst == null)
				{
					chartToCompareAgainst = chart;
				}
				else if (chartToCompareAgainst != chart)
				{
					// Check for and log incompatible timing events, but allow the chart to be saved since we will use the
					// TimingChart for the timing data.
					if (!chart.DoTimingAndScrollEventsMatch(chartToCompareAgainst))
						timingAndScrollMatches = false;
				}
			}
		}

		return timingAndScrollMatches;
	}

	/// <summary>
	/// Returns whether or not events which cannot be split by chart in sm files
	/// match across all charts. This does not check events which cannot even exist
	/// in the first place in sm files, like combo multipliers.
	/// Logs warnings on inconsistencies.
	/// </summary>
	/// <returns>True if all events match and false otherwise.</returns>
	private bool DoNonTimingAndScrollEventsWhichCannotBeSplitByChartInSmFilesMatch()
	{
		var matches = true;
		var chartToCompareAgainst = TimingChart;
		foreach (var kvp in Charts)
		{
			var charts = kvp.Value;
			foreach (var chart in charts)
			{
				if (chartToCompareAgainst == null)
				{
					chartToCompareAgainst = chart;
				}
				else if (chartToCompareAgainst != chart)
				{
					// Check attacks and tick counts.
					// Do not check other events which cannot exist in sm files like combo multipliers.
					if (!chart.DoAttacksMatch(chartToCompareAgainst))
						matches = false;
					if (!chart.DoTickCountsMatch(chartToCompareAgainst))
						matches = false;
				}
			}
		}

		return matches;
	}

	/// <summary>
	/// Returns whether non-timing and non-scroll events which Stepmania examines to determine
	/// if a chart uses split timing from the song match across all charts in this song.
	/// Logs warnings on inconsistencies.
	/// </summary>
	/// <returns>True if all events match and false otherwise.</returns>
	private bool DoNonTimingAndScrollEventsWhichStepmaniaTreatsAsSplitTimingEventsMatch()
	{
		var matches = true;
		var chartToCompareAgainst = TimingChart;
		foreach (var kvp in Charts)
		{
			var charts = kvp.Value;
			foreach (var chart in charts)
			{
				if (chartToCompareAgainst == null)
				{
					chartToCompareAgainst = chart;
				}
				else if (chartToCompareAgainst != chart)
				{
					// Check labels, fakes, tick counts, and combo multipliers.
					// Do not check attacks as Stepmania does not check these.
					// Do not check timing or scroll events as they are handled elsewhere.
					if (!chart.DoLabelsMatch(chartToCompareAgainst))
						matches = false;
					if (!chart.DoFakeSegmentsMatch(chartToCompareAgainst))
						matches = false;
					if (!chart.DoTickCountsMatch(chartToCompareAgainst))
						matches = false;
					if (!chart.DoMultipliersMatch(chartToCompareAgainst))
						matches = false;
				}
			}
		}

		return matches;
	}

	/// <summary>
	/// Saves this EditorSong to disk.
	/// Much of the work for saving occurs asynchronously.
	/// </summary>
	/// <param name="saveParameters">Parameters to save with.</param>
	public void Save(SaveParameters saveParameters)
	{
		// Variable to track completion when all asynchronous operations have finished.
		var complete = false;
		var success = false;

		// Mark that we are saving.
		WorkQueue.Enqueue(() => { Saving = true; });

		// First, update the path.
		WorkQueue.Enqueue(() => { SetFullFilePath(saveParameters.FullPath); });

		// Next, enqueue a task to save this EditorSong to disk.
		WorkQueue.Enqueue(new Task(() =>
			{
				Logger.Info($"Saving {saveParameters.FullPath}...");

				// Check for compatibility problems and early out.
				if (!PerformPreSaveChecks(saveParameters))
				{
					complete = true;
					return;
				}

				var customProperties =
					saveParameters.OmitCustomSaveData ? null : new SMWriterCustomProperties();
				var song = new Song();
				var fallbackChartIndex = -1;

				// Action to call when all EditorCharts have completed saving to Chart objects.
				// This Action will be invoked on the main thread.
				void OnAllChartsComplete()
				{
					var saveLock = new object();
					var packSaveComplete = saveParameters.PackToSave != null;
					var songSaveComplete = false;

					// Save the pack.
					saveParameters.PackToSave?.SaveItgManiaPack(true, () =>
					{
						// Mark this WorkQueue item as complete when both the pack and song are done saving.
						lock (saveLock)
						{
							packSaveComplete = true;
							if (songSaveComplete)
								complete = true;
						}
					});

					// Run the code to save the song in a new Task as it is CPU and IO intensive.
					_ = Task.Run(() =>
					{
						var fallbackChart = fallbackChartIndex >= 0 && fallbackChartIndex < song.Charts.Count
							? song.Charts[fallbackChartIndex]
							: null;

						// Set the source type to the type we want to save. Save logic examines the source
						// type to infer if a conversion is occurring or if we are saving out the same file.
						// We are saving out the same file we opened originally, even if their extensions
						// are different.
						song.SourceType = saveParameters.FileType;

						song.Extras = new Extras(OriginalSongExtras);

						song.Title = Title;
						song.TitleTransliteration = TitleTransliteration;
						song.SubTitle = Subtitle;
						song.SubTitleTransliteration = SubtitleTransliteration;
						song.Artist = Artist;
						song.ArtistTransliteration = ArtistTransliteration;

						song.Extras.AddDestExtra(TagMusic, MusicPath, true);
						song.PreviewMusicFile = MusicPreviewPath;
						song.Extras.AddDestExtra(TagOffset, MusicOffset, true);
						song.Extras.AddDestExtra(TagLastSecondHint, LastSecondHint, true);
						song.PreviewSampleStart = SampleStart;
						song.PreviewSampleLength = SampleLength;

						if (saveParameters.AnonymizeSaveData)
						{
							song.Genre = null;
							song.Extras.RemoveSourceExtra(TagCredit);
							song.Extras.RemoveSourceExtra(TagOrigin);
							song.SongSelectImage = null;
							song.Extras.RemoveSourceExtra(TagBackground);
							song.Extras.RemoveSourceExtra(TagJacket);
							song.Extras.RemoveSourceExtra(TagCDImage);
							song.Extras.RemoveSourceExtra(TagDiscImage);
							song.Extras.RemoveSourceExtra(TagCDTitle);
							song.Extras.RemoveSourceExtra(TagLyricsPath);
							song.Extras.RemoveSourceExtra(TagPreviewVid);
						}
						else
						{
							song.Genre = Genre;
							song.Extras.AddDestExtra(TagCredit, Credit, true);
							song.Extras.AddDestExtra(TagOrigin, Origin, true);
							song.SongSelectImage = Banner.Path;
							song.Extras.AddDestExtra(TagBackground, Background.Path, true);
							song.Extras.AddDestExtra(TagJacket, Jacket.Path, true);
							song.Extras.AddDestExtra(TagCDImage, CDImage.Path, true);
							song.Extras.AddDestExtra(TagDiscImage, DiscImage.Path, true);
							song.Extras.AddDestExtra(TagCDTitle, CDTitle.Path, true);
							song.Extras.AddDestExtra(TagLyricsPath, LyricsPath, true);
							song.Extras.AddDestExtra(TagPreviewVid, PreviewVideoPath, true);
						}

						// For sm files the display BPM must be specified on the song instead of the charts.
						// Copy one of the charts' values.
						if (saveParameters.FileType == FileFormatType.SM && fallbackChart != null)
						{
							song.Extras.AddDestExtra(TagDisplayBPM, fallbackChart.Tempo);
						}
						// Otherwise, remove any DisplayBPM tag which might be present so we only use chart values.
						else
						{
							song.Extras.RemoveSourceExtra(TagDisplayBPM);
						}

						song.Extras.AddDestExtra(TagSelectable, Selectable.ToString(), true);

						if (!saveParameters.OmitCustomSaveData)
							SerializeCustomSongData(customProperties!.CustomSongProperties);

						foreach (var unsupportedChart in UnsupportedCharts)
						{
							song.Charts.Add(unsupportedChart);
						}

						// Save the Song to disk.
						// This is done synchronously as this calling code is asynchronous.
						var config = new SMWriterBaseConfig
						{
							FilePath = saveParameters.FullPath,
							Song = song,
							MeasureSpacingBehavior = MeasureSpacingBehavior.UseLeastCommonMultiple,
							PropertyEmissionBehavior = PropertyEmissionBehavior.Stepmania,
							CustomProperties = customProperties,
							FallbackChart = fallbackChart,
							ForceOnlySongLevelTiming = saveParameters.OmitChartTimingData,
							UseStepF2ForPumpMultiplayerCharts = saveParameters.UseStepF2ForPumpRoutine,
						};
						switch (saveParameters.FileType)
						{
							case FileFormatType.SM:
								success = new SMWriter(config).Save();
								break;
							case FileFormatType.SSC:
								success = new SSCWriter(config).Save();
								break;
							default:
								Logger.Error("Unsupported file format. Cannot save.");
								break;
						}

						Logger.Info($"Saved {saveParameters.FullPath}.");

						// Mark this WorkQueue item as complete when both the pack and song are done saving.
						lock (saveLock)
						{
							songSaveComplete = true;
							if (packSaveComplete)
								complete = true;
						}
					});
				}

				// Create Lists to hold the Charts and CustomChartProperties for the EditorCharts.
				var numCharts = GetNumCharts();
				song.Charts = new List<Chart>(numCharts);
				if (customProperties != null)
					customProperties.CustomChartProperties = new List<Dictionary<string, string>>(numCharts);
				for (var i = 0; i < numCharts; i++)
				{
					song.Charts.Add(null);
					customProperties?.CustomChartProperties.Add(null);
				}

				// Save all EditorCharts to Charts and call OnAllChartsComplete when done.
				// This is intentionally sequential.
				// Each chart could be saved in parallel, and for large songs with many charts this would be quicker,
				// however the default TaskScheduler will pause for over a frame when Starting a Task when there are
				// a large number of Tasks already running. This causes a noticeable hitch when saving. To avoid this
				// we intentionally reduce the number of in flight Tasks by running Chart saves sequentially. Note
				// that this means saving will take at least one frame per Chart due to the Charts' WorkQueues
				// processing once per frame.
				if (numCharts > 0)
				{
					// Create array of all EditorCharts.
					var editorCharts = new EditorChart[numCharts];
					var chartIndex = 0;
					foreach (var editorChartsForChartType in Charts)
					{
						foreach (var editorChart in editorChartsForChartType.Value)
						{
							if (editorChart == TimingChartInternal)
								fallbackChartIndex = chartIndex;
							editorCharts[chartIndex] = editorChart;
							chartIndex++;
						}
					}

					// Save the first chart, and set up an Action to save the next when it is done.
					editorCharts[0].SaveToChart(saveParameters,
						(savedChart, savedChartProperties) =>
						{
							OnChartSaved(song, saveParameters, customProperties, editorCharts, savedChart, savedChartProperties,
								0, numCharts,
								OnAllChartsComplete);
						});
				}
				else
				{
					OnAllChartsComplete();
				}
			}),
			// Callback to caller when saving is complete.
			() => saveParameters.Callback?.Invoke(success),
			// Custom completion check.
			() => complete);

		// Mark that we are done saving.
		WorkQueue.Enqueue(() =>
		{
			LastSaveCompleteTime = DateTime.Now;
			Saving = false;
		});
	}

	/// <summary>
	/// Helper method that gets called when a Chart is done saving.
	/// </summary>
	private void OnChartSaved(
		Song song,
		SaveParameters saveParameters,
		SMWriterCustomProperties customProperties,
		EditorChart[] editorCharts,
		Chart chart,
		Dictionary<string, string> chartProperties,
		int chartIndex,
		int numCharts,
		Action onAllChartsComplete)
	{
		// Record the newly created Chart and its properties.
		song.Charts[chartIndex] = chart;
		if (customProperties != null)
			customProperties.CustomChartProperties[chartIndex] = chartProperties;

		// If there are more Charts to save then save the next Chart.
		var nextChartIndex = chartIndex + 1;
		if (nextChartIndex < numCharts)
		{
			editorCharts[nextChartIndex].SaveToChart(saveParameters,
				(savedChart, savedChartProperties) =>
				{
					OnChartSaved(
						song,
						saveParameters,
						customProperties,
						editorCharts,
						savedChart,
						savedChartProperties,
						nextChartIndex,
						numCharts,
						onAllChartsComplete);
				});
		}
		// If all Charts are complete, invoke the Action to finish saving the Song.
		else
		{
			onAllChartsComplete();
		}
	}

	#endregion Saving

	#region Custom Data Serialization

	/// <summary>
	/// Serialize custom data into the given dictionary
	/// </summary>
	/// <param name="customSongProperties">Dictionary of custom song properties to serialize into.</param>
	private void SerializeCustomSongData(Dictionary<string, string> customSongProperties)
	{
		// Convert the TimingChart into a ChartType and index for serialization.
		ChartType? defaultChartType = null;
		var defaultChartIndex = 0;
		if (TimingChart != null)
		{
			defaultChartType = TimingChart.ChartType;
			if (Charts.TryGetValue(defaultChartType.Value, out var chartsForType))
			{
				for (var i = 0; i < chartsForType.Count; i++)
				{
					if (chartsForType[i] == TimingChart)
					{
						defaultChartIndex = i;
						break;
					}
				}
			}
		}

		// Serialize the custom data.
		var customSaveData = new CustomSaveDataV1
		{
			SyncOffset = SyncOffset,
			DefaultChartType = defaultChartType,
			DefaultChartIndex = defaultChartIndex,
		};
		var jsonString = JsonSerializer.Serialize(customSaveData, CustomSaveDataSerializationOptions);

		// Save the serialized json and version.
		customSongProperties.Add(GetCustomPropertyName(TagCustomSongDataVersion), CustomSaveDataVersion.ToString());
		customSongProperties.Add(GetCustomPropertyName(TagCustomSongData), jsonString);
	}

	/// <summary>
	/// Deserialize custom data stored on the given Song into this EditorSong.
	/// </summary>
	/// <param name="song">Song to deserialize custom data from.</param>
	private void DeserializeCustomSongData(Song song)
	{
		var versionTag = GetCustomPropertyName(TagCustomSongDataVersion);
		var dataTag = GetCustomPropertyName(TagCustomSongData);

		// Get the version and the serialized custom data.
		if (!song.Extras.TryGetExtra(versionTag, out string versionString, true))
			return;
		if (!int.TryParse(versionString, out var version))
			return;
		if (!song.Extras.TryGetExtra(dataTag, out string customSaveDataString, true))
			return;

		// Deserialized the data based on the version.
		switch (version)
		{
			case 1:
			{
				DeserializeV1CustomData(customSaveDataString);
				break;
			}
			default:
			{
				Logger.Warn($"Unsupported {versionTag}: {version}.");
				break;
			}
		}
	}

	/// <summary>
	/// Deserialize custom data from a serialized string of CustomSaveDataV1 data.
	/// </summary>
	/// <param name="customDataString">Serialized string of CustomSaveDataV1 data.</param>
	/// <returns>True if deserialization was successful and false otherwise.</returns>
	private bool DeserializeV1CustomData(string customDataString)
	{
		try
		{
			var customSaveData =
				JsonSerializer.Deserialize<CustomSaveDataV1>(customDataString, CustomSaveDataSerializationOptions);

			SyncOffset = customSaveData.SyncOffset;

			if (customSaveData.DefaultChartType != null)
			{
				if (Charts.TryGetValue(customSaveData.DefaultChartType.Value, out var chartList))
				{
					if (customSaveData.DefaultChartIndex >= 0 && customSaveData.DefaultChartIndex < chartList.Count)
					{
						TimingChart = chartList[customSaveData.DefaultChartIndex];
					}
				}
			}

			return true;
		}
		catch (Exception e)
		{
			Logger.Warn($"Failed to deserialize {GetCustomPropertyName(TagCustomSongData)} value: \"{customDataString}\". {e}");
		}

		return false;
	}

	#endregion Custom Data Serialization

	#region IDisposable

	public void Dispose()
	{
		Banner?.Dispose();
		Background?.Dispose();
		Jacket?.Dispose();
		CDImage?.Dispose();
		DiscImage?.Dispose();
		CDTitle?.Dispose();
	}

	#endregion IDisposable
}

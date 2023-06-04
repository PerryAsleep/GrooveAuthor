using System;
using System.Collections.Generic;
using System.Linq;
using Fumen;
using Fumen.ChartDefinition;
using Fumen.Converters;
using Microsoft.Xna.Framework.Graphics;
using static StepManiaEditor.Utils;
using static StepManiaEditor.ImGuiUtils;
using static Fumen.Converters.SMCommon;
using static Fumen.Converters.SMWriterBase;
using static System.Diagnostics.Debug;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.Threading.Tasks;

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
internal sealed class EditorSong : Notifier<EditorSong>, Fumen.IObserver<WorkQueue>
{
	/// <summary>
	/// Data saved in the song file as a custom data chunk of Editor-specific data at the Song level.
	/// </summary>
	private class CustomSaveDataV1
	{
		public double SyncOffset;
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
	/// WorkQueue for long running tasks like saving.
	/// </summary>
	private readonly WorkQueue WorkQueue;

	/// <summary>
	/// All EditorCharts for this EditorSong.
	/// </summary>
	private readonly Dictionary<ChartType, List<EditorChart>> Charts = new();

	/// <summary>
	/// Charts from the original Song that are unsupported in the editor.
	/// These are saved off into this member so they can be saved back out.
	/// </summary>
	private readonly List<Chart> UnsupportedCharts = new();

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
			MusicPathInternal = value ?? "";
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
			MusicPreviewPathInternal = value ?? "";
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

			if (!SampleLengthInternal.DoubleEquals(value))
			{
				SampleLengthInternal = value;
				Notify(NotificationSampleLengthChanged, this);
			}
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
			Banner.Path = value;
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
			Background.Path = value;
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
			Jacket.Path = value;
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
			CDImage.Path = value;
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
			DiscImage.Path = value;
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
			CDTitle.Path = value;
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
		ImGuiRenderer imGuiRenderer,
		Fumen.IObserver<EditorSong> observer)
	{
		if (observer != null)
			AddObserver(observer);

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
		Func<Chart, bool> isChartSupported,
		Fumen.IObserver<EditorSong> observer,
		Fumen.IObserver<EditorChart> chartObserver)
	{
		if (observer != null)
			AddObserver(observer);

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

		DeserializeCustomSongData(song);

		foreach (var chart in song.Charts)
		{
			if (!isChartSupported(chart))
			{
				UnsupportedCharts.Add(chart);
				continue;
			}

			var editorChart = new EditorChart(this, chart, chartObserver);
			if (!Charts.ContainsKey(editorChart.ChartType))
				Charts.Add(editorChart.ChartType, new List<EditorChart>());
			Charts[editorChart.ChartType].Add(editorChart);

			if (hasDisplayTempo && !editorChart.HasDisplayTempoFromChart())
			{
				editorChart.CopyDisplayTempo(displayTempo);
			}
		}

		UpdateChartSortInternal();
	}

	#endregion Constructors

	#region EditorChart

	public IReadOnlyList<EditorChart> GetCharts(ChartType chartType)
	{
		if (!Charts.TryGetValue(chartType, out var charts))
			return null;
		return charts;
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

	public EditorChart AddChart(ChartType chartType, Fumen.IObserver<EditorChart> observer)
	{
		Assert(CanBeEdited());
		if (!CanBeEdited())
			return null;

		var chart = new EditorChart(this, chartType, observer);
		if (!Charts.ContainsKey(chartType))
			Charts.Add(chartType, new List<EditorChart>());
		Charts[chartType].Add(chart);
		UpdateChartSortInternal();
		return chart;
	}

	public EditorChart AddChart(EditorChart chart)
	{
		Assert(CanBeEdited());
		if (!CanBeEdited())
			return null;

		if (!Charts.ContainsKey(chart.ChartType))
			Charts.Add(chart.ChartType, new List<EditorChart>());
		Charts[chart.ChartType].Add(chart);
		UpdateChartSortInternal();
		return chart;
	}

	public void DeleteChart(EditorChart chart)
	{
		Assert(CanBeEdited());
		if (!CanBeEdited())
			return;

		if (!Charts.ContainsKey(chart.ChartType))
			return;
		Charts[chart.ChartType].Remove(chart);
		if (Charts[chart.ChartType].Count == 0)
			Charts.Remove(chart.ChartType);
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
	}

	#endregion EditorChart

	#region Misc

	public bool CanBeEdited()
	{
		// The Song cannot be edited if work is queued up.
		// The exception to that is if that work itself is synchronous as it means the edit
		// is coming from that enqueued work.
		if (WorkQueue.IsRunningSynchronousWork())
			return true;
		return WorkQueue.IsEmpty();
	}

	public double GetBestChartStartingTempo()
	{
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

	/// <summary>
	/// Updates this EditorSong. Expected to be called once per frame.
	/// </summary>
	public void Update()
	{
		// Update the WorkQueue.
		WorkQueue.Update();

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
		if (Banner != null)
			Banner.Path = UpdateRelativePath(oldFullPath, newFullPath, Banner.Path);
		if (Background != null)
			Background.Path = UpdateRelativePath(oldFullPath, newFullPath, Background.Path);
		if (Jacket != null)
			Jacket.Path = UpdateRelativePath(oldFullPath, newFullPath, Jacket.Path);
		if (CDImage != null)
			CDImage.Path = UpdateRelativePath(oldFullPath, newFullPath, CDImage.Path);
		if (DiscImage != null)
			DiscImage.Path = UpdateRelativePath(oldFullPath, newFullPath, DiscImage.Path);
		if (CDTitle != null)
			CDTitle.Path = UpdateRelativePath(oldFullPath, newFullPath, CDTitle.Path);
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
	/// Saves this EditorSong to disk.
	/// Much of the work for saving occurs asynchronously.
	/// When the saving has completed the given callback will be called.
	/// </summary>
	/// <param name="fileType">FileFormatType to save as.</param>
	/// <param name="fullPath">Full path to the file to save to.</param>
	/// <param name="callback">Action to call when saving is complete.</param>
	public void Save(FileFormatType fileType, string fullPath, Action callback)
	{
		// Variable to track completion when all asynchronous operations have finished.
		var complete = false;

		// Mark that we are saving.
		WorkQueue.Enqueue(() => { Saving = true; });

		// First, update the path.
		WorkQueue.Enqueue(() => { SetFullFilePath(fullPath); });

		// Next, enqueue a task to save this EditorSong to disk.
		WorkQueue.Enqueue(new Task(() =>
			{
				Logger.Info($"Saving {fullPath}...");

				// TODO: Check for incompatible features with SM format.
				if (fileType == FileFormatType.SM)
				{
				}

				var customProperties = new SMWriterCustomProperties();
				var song = new Song();

				// Action to call when all EditorCharts have completed saving to Chart objects.
				// This Action will be invoked on the main thread.
				void OnAllChartsComplete()
				{
					// Run the code below in a new Task as it is CPU and IO intensive.
					Task.Run(() =>
					{
						song.Extras = new Extras(OriginalSongExtras);
						// TODO: Remove timing data?

						song.Title = Title;
						song.TitleTransliteration = TitleTransliteration;
						song.SubTitle = Subtitle;
						song.SubTitleTransliteration = SubtitleTransliteration;
						song.Artist = Artist;
						song.ArtistTransliteration = ArtistTransliteration;
						song.Genre = Genre;
						song.Extras.AddDestExtra(TagOrigin, Origin, true);
						song.Extras.AddDestExtra(TagCredit, Credit, true);
						song.SongSelectImage = Banner.Path;
						song.Extras.AddDestExtra(TagBackground, Background.Path, true);
						song.Extras.AddDestExtra(TagJacket, Jacket.Path, true);
						song.Extras.AddDestExtra(TagCDImage, CDImage.Path, true);
						song.Extras.AddDestExtra(TagDiscImage, DiscImage.Path, true);
						song.Extras.AddDestExtra(TagCDTitle, CDTitle.Path, true);
						song.Extras.AddDestExtra(TagLyricsPath, LyricsPath, true);
						song.Extras.AddDestExtra(TagPreviewVid, PreviewVideoPath, true);

						song.Extras.AddDestExtra(TagMusic, MusicPath, true);
						song.PreviewMusicFile = MusicPreviewPath;
						song.Extras.AddDestExtra(TagOffset, MusicOffset, true);
						song.Extras.AddDestExtra(TagLastSecondHint, LastSecondHint, true);
						song.PreviewSampleStart = SampleStart;
						song.PreviewSampleLength = SampleLength;

						// Do not add the display BPM.
						// We want to use the charts' display BPMs.
						song.Extras.RemoveSourceExtra(TagDisplayBPM);

						song.Extras.AddDestExtra(TagSelectable, Selectable.ToString(), true);

						SerializeCustomSongData(customProperties.CustomSongProperties);

						foreach (var unsupportedChart in UnsupportedCharts)
						{
							song.Charts.Add(unsupportedChart);
						}

						// Save the Song to disk.
						// This is done synchronously as this calling code is asynchronous.
						var config = new SMWriterBaseConfig
						{
							FilePath = fullPath,
							Song = song,
							MeasureSpacingBehavior = MeasureSpacingBehavior.UseLeastCommonMultiple,
							PropertyEmissionBehavior = PropertyEmissionBehavior.Stepmania,
							CustomProperties = customProperties,
							// HACK:
							FallbackChart = song.Charts.Count > 0 ? song.Charts[0] : null,
						};
						switch (fileType)
						{
							case FileFormatType.SM:
								new SMWriter(config).Save();
								break;
							case FileFormatType.SSC:
								new SSCWriter(config).Save();
								break;
							default:
								Logger.Error("Unsupported file format. Cannot save.");
								break;
						}

						Logger.Info($"Saved {fullPath}.");

						// Mark the entire save as completed now so the WorkQueue can continue.
						complete = true;
					});
				}

				// Create Lists to hold the Charts and CustomChartProperties for the EditorCharts.
				var numCharts = GetNumCharts();
				song.Charts = new List<Chart>(numCharts);
				customProperties.CustomChartProperties = new List<Dictionary<string, string>>(numCharts);
				for (var i = 0; i < numCharts; i++)
				{
					song.Charts.Add(null);
					customProperties.CustomChartProperties.Add(null);
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
							editorCharts[chartIndex] = editorChart;
							chartIndex++;
						}
					}

					// Save the first chart, and set up an Action to save the next when it is done.
					editorCharts[0].SaveToChart((savedChart, savedChartProperties) =>
					{
						OnChartSaved(song, customProperties, editorCharts, savedChart, savedChartProperties, 0, numCharts,
							OnAllChartsComplete);
					});
				}
				else
				{
					OnAllChartsComplete();
				}
			}),
			// Callback to caller when saving is complete.
			callback,
			// Custom completion check.
			() => complete);

		// Mark that we done saving.
		WorkQueue.Enqueue(() => { Saving = false; });
	}

	/// <summary>
	/// Helper method that gets called when a Chart is done saving.
	/// </summary>
	private void OnChartSaved(
		Song song,
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
		customProperties.CustomChartProperties[chartIndex] = chartProperties;

		// If there are more Charts to save then save the next Chart.
		var nextChartIndex = chartIndex + 1;
		if (nextChartIndex < numCharts)
		{
			editorCharts[nextChartIndex].SaveToChart((savedChart, savedChartProperties) =>
			{
				OnChartSaved(
					song,
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
		// Serialize the custom data.
		var customSaveData = new CustomSaveDataV1
		{
			SyncOffset = SyncOffset,
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
			return true;
		}
		catch (Exception e)
		{
			Logger.Warn($"Failed to deserialize {GetCustomPropertyName(TagCustomSongData)} value: \"{customDataString}\". {e}");
		}

		return false;
	}

	#endregion Custom Data Serialization
}

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
using System.Text.Json.Serialization;
using System.Text.Json;

namespace StepManiaEditor
{
	internal enum Selectable
	{
		YES,
		NO,
		ROULETTE,
		ES,
		OMES
	}

	internal enum DisplayTempoMode
	{
		Random,
		Specified,
		Actual
	}

	/// <summary>
	/// Small class to hold a Texture for a song or chart property that
	/// represents a file path to an image asset.
	/// </summary>
	internal sealed class EditorImageData
	{
		private string FileDirectory;
		private EditorTexture Texture;
		private string PathInternal = "";

		/// <summary>
		/// Path property.
		/// On set, begins an asynchronous load of the image asset specified to the Texture.
		/// </summary>
		public string Path
		{
			get => PathInternal;
			set
			{
				var newValue = value ?? "";
				if (PathInternal == newValue)
					return;

				PathInternal = newValue;
				if (!string.IsNullOrEmpty(PathInternal))
					Texture?.LoadAsync(Fumen.Path.Combine(FileDirectory, PathInternal));
				else
					Texture?.UnloadAsync();
			}
		}

		/// <summary>
		/// Constructor.
		/// When constructed through this method, no Texture will be used.
		/// </summary>
		public EditorImageData(string path)
		{
			Path = path;
		}

		/// <summary>
		/// Constructor.
		/// When constructed through this method, a Texture will be used and loaded asynchronously
		/// whenever the Path changes.
		/// </summary>
		public EditorImageData(
			string fileDirectory,
			GraphicsDevice graphicsDevice,
			ImGuiRenderer imGuiRenderer,
			uint width,
			uint height,
			string path,
			bool cacheTextureColor)
		{
			FileDirectory = fileDirectory;
			Texture = new EditorTexture(graphicsDevice, imGuiRenderer, width, height, cacheTextureColor);
			Path = path;
		}

		public EditorTexture GetTexture()
		{
			return Texture;
		}
	}

	internal class EditorChartTimingData
	{
		//song_tag_handlers["STOPS"]= &SetSongStops;
		//song_tag_handlers["DELAYS"]= &SetSongDelays;
		//song_tag_handlers["BPMS"]= &SetSongBPMs;
		//song_tag_handlers["WARPS"]= &SetSongWarps;
		//song_tag_handlers["LABELS"]= &SetSongLabels;
		//song_tag_handlers["TIMESIGNATURES"]= &SetSongTimeSignatures;
		//song_tag_handlers["TICKCOUNTS"]= &SetSongTickCounts;
		//song_tag_handlers["COMBOS"]= &SetSongCombos;
		//song_tag_handlers["SPEEDS"]= &SetSongSpeeds;
		//song_tag_handlers["SCROLLS"]= &SetSongScrolls;
		//song_tag_handlers["FAKES"]= &SetSongFakes;
	}

	internal sealed class DisplayTempo
	{
		public DisplayTempoMode Mode;
		public double SpecifiedTempoMin;
		public double SpecifiedTempoMax;

		// Not serialized. Used for UI controls to avoid having to enter both a min and a max
		// when just wanted one tempo.
		public bool ShouldAllowEditsOfMax = true;

		public DisplayTempo()
		{
			Mode = DisplayTempoMode.Actual;
			SpecifiedTempoMin = 0.0;
			SpecifiedTempoMax = 0.0;
		}

		public DisplayTempo(DisplayTempoMode mode, double min, double max)
		{
			Mode = mode;
			SpecifiedTempoMin = min;
			SpecifiedTempoMax = max;
			ShouldAllowEditsOfMax = !SpecifiedTempoMin.DoubleEquals(SpecifiedTempoMax);
		}

		public DisplayTempo(DisplayTempo other)
		{
			Mode = other.Mode;
			SpecifiedTempoMin = other.SpecifiedTempoMin;
			SpecifiedTempoMax = other.SpecifiedTempoMax;
			ShouldAllowEditsOfMax = other.ShouldAllowEditsOfMax;
		}

		public void FromString(string displayTempoString)
		{
			Mode = DisplayTempoMode.Actual;
			SpecifiedTempoMin = 0.0;
			SpecifiedTempoMax = 0.0;

			if (string.IsNullOrEmpty(displayTempoString))
				return;
			
			var parsed = false;
			if (displayTempoString == "*")
			{
				parsed = true;
				Mode = DisplayTempoMode.Random;
			}
			else
			{
				var parts = displayTempoString.Split(MSDFile.ParamMarker);
				if (parts.Length == 1)
				{
					if (double.TryParse(parts[0], out SpecifiedTempoMin))
					{
						parsed = true;
						SpecifiedTempoMax = SpecifiedTempoMin;
						Mode = DisplayTempoMode.Specified;
						ShouldAllowEditsOfMax = false;
					}
				}
				else if (parts.Length == 2)
				{
					if (double.TryParse(parts[0], out SpecifiedTempoMin) && double.TryParse(parts[1], out SpecifiedTempoMax))
					{
						parsed = true;
						Mode = DisplayTempoMode.Specified;
						ShouldAllowEditsOfMax = !SpecifiedTempoMin.DoubleEquals(SpecifiedTempoMax);
					}
				}
			}

			if (!parsed)
			{
				Logger.Warn($"Failed to parse {TagDisplayBPM} value: '{displayTempoString}'.");
			}
		}

		public override string ToString()
		{
			switch (Mode)
			{
				case DisplayTempoMode.Random:
					return "*";
				case DisplayTempoMode.Specified:
					if (!SpecifiedTempoMin.DoubleEquals(SpecifiedTempoMax))
					{
						var min = SpecifiedTempoMin.ToString(SMDoubleFormat);
						var max = SpecifiedTempoMax.ToString(SMDoubleFormat);
						return $"{min}:{max}";
					}
					return SpecifiedTempoMin.ToString(SMDoubleFormat);
				case DisplayTempoMode.Actual:
					return "";
			}
			return "";
		}
	}

	/// <summary>
	/// Editor represenation of a Stepmania song.
	/// An EditorSong can have multiple EditorCharts.
	/// </summary>
	internal sealed class EditorSong : Notifier<EditorSong>
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
		private static JsonSerializerOptions CustomSaveDataSerializationOptions = new JsonSerializerOptions()
		{
			Converters =
			{
				new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
			},
			ReadCommentHandling = JsonCommentHandling.Skip,
			IncludeFields = true,
		};

		private Extras OriginalSongExtras;

		public Dictionary<ChartType, List<EditorChart>> Charts = new Dictionary<ChartType, List<EditorChart>>();
		public List<Chart> UnsupportedCharts = new List<Chart>();

		public string FileDirectory;
		public string FileName;
		public string FileFullPath;
		public FileFormat FileFormat;

		public string Title = "";
		public string TitleTransliteration = "";
		public string Subtitle = "";
		public string SubtitleTransliteration = "";
		public string Artist = "";
		public string ArtistTransliteration = "";

		public string Genre = "";
		public string Origin = "";
		public string Credit = "";

		public EditorImageData Banner;
		public EditorImageData Background;
		public EditorImageData Jacket;
		public EditorImageData CDImage;
		public EditorImageData DiscImage;
		public EditorImageData CDTitle;

		public string LyricsPath = "";
		public string PreviewVideoPath = "";

		private string MusicPathInternal = "";

		public string MusicPath
		{
			get => MusicPathInternal;
			set
			{
				MusicPathInternal = value ?? "";
				Notify(NotificationMusicChanged, this);
			}
		}

		private string MusicPreviewPathInternal = "";

		public string MusicPreviewPath
		{
			get => MusicPreviewPathInternal;
			set
			{
				MusicPreviewPathInternal = value ?? "";
				Notify(NotificationMusicPreviewChanged, this);
			}
		}

		private double MusicOffsetInternal;
		public double MusicOffset
		{
			get => MusicOffsetInternal;
			set
			{
				DeletePreviewEvents();
				MusicOffsetInternal = value;
				AddPreviewEvents();
				Notify(NotificationMusicOffsetChanged, this);
			}
		}

		private double SyncOffsetInternal;
		public double SyncOffset
		{
			get => SyncOffsetInternal;
			set
			{
				DeletePreviewEvents();
				SyncOffsetInternal = value;
				AddPreviewEvents();
				Notify(NotificationSyncOffsetChanged, this);
			}
		}


		//Intentionally not set.
		//INSTRUMENTTRACK
		//MUSICLENGTH
		//ANIMATIONS
		//BGCHANGES
		//FGCHANGES
		//KEYSOUNDS
		//ATTACKS

		private double LastSecondHintInternal;
		public double LastSecondHint
		{
			get => LastSecondHintInternal;
			set
			{
				DeleteLastSecondHintEvents();
				LastSecondHintInternal = value;
				AddLastSecondHintEvents();
			}
		}

		private double SampleStartInternal;
		public double SampleStart
		{
			get => SampleStartInternal;
			set
			{
				if (!SampleStartInternal.DoubleEquals(value))
				{
					DeletePreviewEvents();
					SampleStartInternal = value;
					AddPreviewEvents();
				}
			}
		}

		private double SampleLengthInternal;
		public double SampleLength
		{
			get => SampleLengthInternal;
			set
			{
				if (!SampleLengthInternal.DoubleEquals(value))
				{
					SampleLengthInternal = value;
					Notify(NotificationSampleLengthChanged, this);
				}
			}
		}

		public Selectable Selectable = Selectable.YES;

		public EditorSong(
			GraphicsDevice graphicsDevice,
			ImGuiRenderer imGuiRenderer,
			Fumen.IObserver<EditorSong> observer)
		{
			if (observer != null)
				AddObserver(observer);

			Banner = new EditorImageData(FileDirectory, graphicsDevice, imGuiRenderer, (uint)GetBannerWidth(), (uint)GetBannerHeight(), null, false);
			Background = new EditorImageData(FileDirectory, graphicsDevice, imGuiRenderer, (uint)GetBackgroundWidth(), (uint)GetBackgroundHeight(), null, true);
			Jacket = new EditorImageData(null);
			CDImage = new EditorImageData(null);
			DiscImage = new EditorImageData(null);
			CDTitle = new EditorImageData(FileDirectory, graphicsDevice, imGuiRenderer, (uint)GetCDTitleWidth(), (uint)GetCDTitleHeight(), null, false);

			MusicPath = "";
			MusicPreviewPath = "";
			SyncOffset = Preferences.Instance.PreferencesOptions.NewSongSyncOffset;
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

			SetFullFilePath(fullFilePath);
			
			OriginalSongExtras = song.Extras;

			Title = song.Title ?? "";
			TitleTransliteration = song.TitleTransliteration ?? "";
			Subtitle = song.SubTitle ?? "";
			SubtitleTransliteration = song.SubTitleTransliteration ?? "";
			Artist = song.Artist ?? "";
			ArtistTransliteration = song.ArtistTransliteration ?? "";
			Genre = song.Genre ?? "";
			song.Extras.TryGetExtra(TagOrigin, out Origin, true);
			Origin ??= "";
			song.Extras.TryGetExtra(TagCredit, out Credit, true);
			Credit ??= "";

			Banner = new EditorImageData(FileDirectory, graphicsDevice, imGuiRenderer, (uint)GetBannerWidth(), (uint)GetBannerHeight(), song.SongSelectImage, false);
			string tempStr;
			song.Extras.TryGetExtra(TagBackground, out tempStr, true);
			Background = new EditorImageData(FileDirectory, graphicsDevice, imGuiRenderer, (uint)GetBackgroundWidth(), (uint)GetBackgroundHeight(), tempStr, true);
			song.Extras.TryGetExtra(TagJacket, out tempStr, true);
			Jacket = new EditorImageData(tempStr);
			song.Extras.TryGetExtra(TagCDImage, out tempStr, true);
			CDImage = new EditorImageData(tempStr);
			song.Extras.TryGetExtra(TagDiscImage, out tempStr, true);
			DiscImage = new EditorImageData(tempStr);
			song.Extras.TryGetExtra(TagCDTitle, out tempStr, true);
			CDTitle = new EditorImageData(FileDirectory, graphicsDevice, imGuiRenderer, (uint)GetCDTitleWidth(), (uint)GetCDTitleHeight(), tempStr, false);

			song.Extras.TryGetExtra(TagLyricsPath, out LyricsPath, true);
			LyricsPath ??= "";
			song.Extras.TryGetExtra(TagPreviewVid, out PreviewVideoPath, true);
			PreviewVideoPath ??= "";

			song.Extras.TryGetExtra(TagMusic, out string musicPath, true);
			MusicPath = musicPath;

			MusicPreviewPath = song.PreviewMusicFile ?? "";
			song.Extras.TryGetExtra(TagOffset, out double musicOffset, true);
			MusicOffset = musicOffset;

			song.Extras.TryGetExtra(TagLastSecondHint, out double lastSecondHint, true);
			LastSecondHint = lastSecondHint;

			SampleStart = song.PreviewSampleStart;
			SampleLength = song.PreviewSampleLength;

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
				if (!Enum.TryParse(selectableString, true, out Selectable))
				{
					Selectable = Selectable.YES;
					Logger.Warn($"Failed to parse Song {TagSelectable} value: '{selectableString}'.");
				}
			}

			SyncOffset = Preferences.Instance.PreferencesOptions.OpenSongSyncOffset;

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

				if (hasDisplayTempo && !editorChart.HasDisplayTempoFromChart)
				{
					editorChart.CopyDisplayTempo(displayTempo);
				}
			}

			UpdateChartSort();
		}

		public EditorChart AddChart(ChartType chartType, Fumen.IObserver<EditorChart> observer)
		{
			var chart = new EditorChart(this, chartType, observer);
			if (!Charts.ContainsKey(chartType))
				Charts.Add(chartType, new List<EditorChart>());
			Charts[chartType].Add(chart);
			UpdateChartSort();
			return chart;
		}

		public EditorChart AddChart(EditorChart chart)
		{
			if (!Charts.ContainsKey(chart.ChartType))
				Charts.Add(chart.ChartType, new List<EditorChart>());
			Charts[chart.ChartType].Add(chart);
			UpdateChartSort();
			return chart;
		}

		public void DeleteChart(EditorChart chart)
		{
			if (!Charts.ContainsKey(chart.ChartType))
				return;
			Charts[chart.ChartType].Remove(chart);
			if (Charts[chart.ChartType].Count == 0)
				Charts.Remove(chart.ChartType);
			UpdateChartSort();
		}

		public void UpdateChartSort()
		{
			foreach (var kvp in Charts)
			{
				kvp.Value.Sort(new ChartComparer());
			}
		}

		/// <summary>
		/// Sets the full path of the EditorSong.
		/// Updates all relative paths to other assets to be relative to the new full path.
		/// </summary>
		/// <param name="fullFilePath">New full path of the EditorSong.</param>
		public void SetFullFilePath(string fullFilePath)
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
				// Occaisonally paths will be absolute. This can occur if the song is new and hasn't been saved yet.
				// In that case there is no song path to be relative to. If the path is absolute, convert it to be
				// relative to the new full path.
				if (System.IO.Path.IsPathRooted(relativePath))
				{
					relativePath = Fumen.Path.GetRelativePath(System.IO.Path.GetDirectoryName(newFullPath), relativePath);
				}
				// Normally, the relative path exists and is relative to the old full path. In this case, convert
				// the relative path to an absolute path first, then convert that absolute path to be relatiev to
				// the new full path.
				else if (!string.IsNullOrEmpty(oldFullPath))
				{
					var relativeFullPath = Fumen.Path.GetFullPathFromRelativePathToFullPath(oldFullPath, relativePath);
					relativePath = Fumen.Path.GetRelativePath(System.IO.Path.GetDirectoryName(newFullPath), relativeFullPath);
				}
			}
			catch(Exception)
			{ }

			return relativePath;
		}

		public double GetBestChartStartingTempo()
		{
			var histogram = new Dictionary<double, int>();
			foreach(var kvp in Charts)
			{
				foreach(var chart in kvp.Value)
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

		private void DeletePreviewEvents()
		{
			foreach (var kvp in Charts)
			{
				foreach (var chart in kvp.Value)
				{
					chart.DeletePreviewEvent();
				}
			}
		}

		private void AddPreviewEvents()
		{
			foreach (var kvp in Charts)
			{
				foreach (var chart in kvp.Value)
				{
					chart.AddPreviewEvent();
				}
			}
		}

		private void DeleteLastSecondHintEvents()
		{
			foreach (var kvp in Charts)
			{
				foreach (var chart in kvp.Value)
				{
					chart.DeleteLastSecondHintEvent();
				}
			}
		}

		private void AddLastSecondHintEvents()
		{
			foreach (var kvp in Charts)
			{
				foreach (var chart in kvp.Value)
				{
					chart.AddLastSecondHintEvent();
				}
			}
		}

		public Song SaveToSong(SMWriterCustomProperties customProperties)
		{
			Song song = new Song();

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

			foreach (var editorChartsForChartType in Charts)
			{
				foreach (var editorChart in editorChartsForChartType.Value)
				{
					var chartProperties = new Dictionary<string, string>();
					song.Charts.Add(editorChart.SaveToChart(chartProperties));
					customProperties.CustomChartProperties.Add(chartProperties);
				}
			}
			foreach (var unsupportedChart in UnsupportedCharts)
			{
				song.Charts.Add(unsupportedChart);
			}

			return song;
		}

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
				SyncOffset = SyncOffset
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
			if (!int.TryParse(versionString, out int version))
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
				var customSaveData = JsonSerializer.Deserialize<CustomSaveDataV1>(customDataString, CustomSaveDataSerializationOptions);
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
}

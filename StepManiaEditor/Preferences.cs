using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Fumen;
using Fumen.Converters;

namespace StepManiaEditor
{
	/// <summary>
	/// Miscellaneous preferences to save to disk.
	/// </summary>
	public class Preferences
	{
		/// <summary>
		/// File to use for deserializing Preferences.
		/// </summary>
		private const string FileName = "Preferences.json";

		/// <summary>
		/// Serialization options.
		/// </summary>
		private static JsonSerializerOptions SerializationOptions = new JsonSerializerOptions()
		{
			Converters =
			{
				new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
			},
			ReadCommentHandling = JsonCommentHandling.Skip,
			AllowTrailingCommas = true,
			IncludeFields = true,
			WriteIndented = true
		};

		public class SavedSongInformation
		{
			[JsonInclude] public string FileName;
			[JsonInclude] public string LastChartType;
			[JsonInclude] public string LastChartDifficultyType;
			// TODO: Zoom level and position
		}

		/// <summary>
		/// Static Config instance.
		/// </summary>
		public static Preferences Instance { get; private set; } = new Preferences();

		// Window preferences
		[JsonInclude] public int WindowWidth = 1920;
		[JsonInclude] public int WindowHeight = 1080;
		[JsonInclude] public bool WindowFullScreen = false;
		[JsonInclude] public bool WindowMaximized = false;

		// Waveform preferences
		[JsonInclude] public bool ShowWaveFormWindow = false;
		[JsonInclude] public bool ShowWaveForm = true;
		[JsonInclude] public bool WaveFormScaleXWhenZooming = true;
		[JsonInclude] public int WaveFormWindowSparseColorOption = 0;
		[JsonInclude] public float SparseColorScale = 0.8f;
		[JsonInclude] public Vector3 WaveFormDenseColor = new Vector3(0.0f, 0.389f, 0.183f);
		[JsonInclude] public Vector3 WaveFormSparseColor = new Vector3(0.0f, 0.350f, 0.164f);
		[JsonInclude] public float WaveFormMaxXPercentagePerChannel = 0.9f;
		[JsonInclude] public int WaveFormLoadingMaxParallelism = 8;

		// Scroll control preferences
		public const float DefaultTimeBasedPixelsPerSecond = 300.0f;
		public const float DefaultRowBasedPixelsPerRow = 6.0f;
		public const float DefaultVariablePixelsPerSecond = 300.0f;
		public const float DefaultVariableSpeedBPM = 120.0f;

		[JsonInclude] public bool ShowScrollControlWindow = true;
		[JsonInclude] public Editor.ScrollMode ScrollMode = Editor.ScrollMode.Time;
		[JsonInclude] public Editor.SpacingMode SpacingMode = Editor.SpacingMode.ConstantTime;
		[JsonInclude] public float TimeBasedPixelsPerSecond = DefaultTimeBasedPixelsPerSecond;
		[JsonInclude] public float RowBasedPixelsPerRow = DefaultRowBasedPixelsPerRow;
		[JsonInclude] public float VariablePixelsPerSecondAtDefaultBPM = DefaultVariablePixelsPerSecond;
		[JsonInclude] public Editor.WaveFormScrollMode RowBasedWaveFormScrollMode = Editor.WaveFormScrollMode.MostCommonTempo;
		[JsonInclude] public bool StopPlaybackWhenScrolling = false;

		// MiniMap preferences
		public const bool DefaultShowMiniMap = true;
		public const MiniMap.SelectMode DefualtMiniMapSelectMode = MiniMap.SelectMode.MoveEditorToCursor;
		public const bool DefaultMiniMapGrabWhenClickingOutsideEditorArea = true;
		public const bool DefaultMiniMapStopPlaybackWhenScrolling = false;
		public const uint DefaultMiniMapWidth = 90;
		public const uint DefaultMiniMapNoteWidth = 2;
		public const uint DefaultMiniMapNoteSpacing = 3;
		public const MiniMap.Position DefaultMiniMapPosition = MiniMap.Position.RightOfChartArea;
		public const Editor.SpacingMode DefaultMiniMapSpacingModeForVariable = Editor.SpacingMode.ConstantTime;
		public static readonly Editor.SpacingMode[] MiniMapVariableSpacingModes = { Editor.SpacingMode.ConstantTime, Editor.SpacingMode.ConstantRow };
		public const uint DefaultMiniMapVisibleTimeRange = 240;
		public const uint DefaultMiniMapVisibleRowRange = 24576;

		[JsonInclude] public bool ShowMiniMapWindow = false;
		[JsonInclude] public bool ShowMiniMap = DefaultShowMiniMap;
		[JsonInclude] public MiniMap.SelectMode MiniMapSelectMode = DefualtMiniMapSelectMode;
		[JsonInclude] public bool MiniMapGrabWhenClickingOutsideEditorArea = DefaultMiniMapGrabWhenClickingOutsideEditorArea;
		[JsonInclude] public bool MiniMapStopPlaybackWhenScrolling = DefaultMiniMapStopPlaybackWhenScrolling;
		[JsonInclude] public uint MiniMapWidth = DefaultMiniMapWidth;
		[JsonInclude] public uint MiniMapNoteWidth = DefaultMiniMapNoteWidth;
		[JsonInclude] public uint MiniMapNoteSpacing = DefaultMiniMapNoteSpacing;
		[JsonInclude] public MiniMap.Position MiniMapPosition = DefaultMiniMapPosition;
		[JsonInclude] public Editor.SpacingMode MiniMapSpacingModeForVariable = DefaultMiniMapSpacingModeForVariable;
		[JsonInclude] public uint MiniMapVisibleTimeRange = DefaultMiniMapVisibleTimeRange;
		[JsonInclude] public uint MiniMapVisibleRowRange = DefaultMiniMapVisibleRowRange;

		// Log preferences
		[JsonInclude] public bool ShowLogWindow = true;
		[JsonInclude] public int LogWindowDateDisplay = 1;
		[JsonInclude] public LogLevel LogWindowLevel = LogLevel.Info;
		[JsonInclude] public bool LogWindowLineWrap;

		// Option preferences
		[JsonInclude] public bool ShowOptionsWindow = false;


		[JsonInclude] public bool ShowSongPropertiesWindow = false;
		[JsonInclude] public bool ShowChartPropertiesWindow = false;

		// Strings are serialized, but converted to an array of booleans for UI.
		[JsonIgnore] public bool[] StartupStepsTypesBools;
		[JsonInclude] public string[] StartupStepsTypes = { "dance-single", "dance-double" };
		[JsonInclude] public bool OpenLastOpenedFileOnLaunch = false;

		// Misc
		[JsonInclude] public string OpenFileDialogInitialDirectory = @"C:\Games\StepMania 5\Songs\";
		[JsonInclude] public int RecentFilesHistorySize = 10;
		[JsonInclude] public List<SavedSongInformation> RecentFiles = new List<SavedSongInformation>();
		[JsonInclude] public string DefaultStepsType = "dance-single";
		[JsonInclude] public string DefaultDifficultyType = "Challenge";
		[JsonInclude] public double PreviewFadeInTime = 0.0;
		[JsonInclude] public double PreviewFadeOutTime = 1.5;

		// Debug
		[JsonInclude] public double DebugSongTime = 0.0;
		[JsonInclude] public double DebugZoom = 1.0;

		public Preferences()
		{
			PostLoad();
		}

		private void PostLoad()
		{
			// Set up StartupStepsTypesBools from StartupStepsTypes.
			StartupStepsTypesBools = new bool[Enum.GetNames(typeof(SMCommon.ChartType)).Length];
			foreach (var stepsType in StartupStepsTypes)
			{
				if (SMCommon.TryGetChartType(stepsType, out var chartType))
				{
					StartupStepsTypesBools[(int)chartType] = true;
				}
			}
		}

		private void PreSave()
		{
			// Set up StartupStepsTypes from StartupStepsTypesBools.
			var count = 0;
			for (var i = 0; i < StartupStepsTypesBools.Length; i++)
			{
				if (StartupStepsTypesBools[i])
					count++;
			}
			StartupStepsTypes = new string[count];
			count = 0;
			for (var i = 0; i < StartupStepsTypesBools.Length; i++)
			{
				if (StartupStepsTypesBools[i])
				{
					StartupStepsTypes[count++] = SMCommon.ChartTypeString((SMCommon.ChartType)i);
				}
			}
		}

		/// <summary>
		/// Loads the Preferences from the preferences json file.
		/// </summary>
		/// <returns>Preferences Instance.</returns>
		public static async Task<Preferences> LoadAsync()
		{
			Logger.Info($"Loading {FileName}...");

			try
			{
				using (FileStream openStream = File.OpenRead(Fumen.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, FileName)))
				{
					Instance = await JsonSerializer.DeserializeAsync<Preferences>(openStream, SerializationOptions);
					Instance.PostLoad();
				}
			}
			catch (Exception e)
			{
				Logger.Error($"Failed to load {FileName}. {e}");
				return Instance;
			}

			Logger.Info($"Loaded {FileName}.");
			return Instance;
		}

		/// <summary>
		/// Loads the Preferences from the preferences json file.
		/// </summary>
		/// <returns>Preferences Instance.</returns>
		public static Preferences Load()
		{
			Logger.Info($"Loading {FileName}...");

			try
			{
				using (FileStream openStream = File.OpenRead(Fumen.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, FileName)))
				{
					Instance = JsonSerializer.Deserialize<Preferences>(openStream, SerializationOptions);
					Instance.PostLoad();
				}
			}
			catch (Exception e)
			{
				Logger.Error($"Failed to load {FileName}. {e}");
				return Instance;
			}

			Logger.Info($"Loaded {FileName}.");
			return Instance;
		}

		/// <summary>
		/// Save the Preferences to the preferences json file.
		/// </summary>
		public static async Task SaveAsync()
		{
			Logger.Info($"Saving {FileName}...");

			try
			{
				Instance.PreSave();
				var jsonString = JsonSerializer.Serialize(Instance, SerializationOptions);
				await File.WriteAllTextAsync(Fumen.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, FileName), jsonString);
			}
			catch (Exception e)
			{
				Logger.Error($"Failed to save {FileName}. {e}");
				return;
			}

			Logger.Info($"Saved {FileName}.");
		}

		/// <summary>
		/// Save the Preferences to the preferences json file.
		/// </summary>
		public static void Save()
		{
			Logger.Info($"Saving {FileName}...");

			try
			{
				Instance.PreSave();
				var jsonString = JsonSerializer.Serialize(Instance, SerializationOptions);
				File.WriteAllText(Fumen.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, FileName), jsonString);
			}
			catch (Exception e)
			{
				Logger.Error($"Failed to save {FileName}. {e}");
				return;
			}

			Logger.Info($"Saved {FileName}.");
		}
	}
}

using System;
using System.Collections;
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
		[JsonInclude] public bool ShowWaveFormWindow = true;
		[JsonInclude] public bool ShowWaveForm = true;
		[JsonInclude] public bool WaveFormScaleXWhenZooming = true;
		[JsonInclude] public int WaveFormWindowSparseColorOption = 0;
		[JsonInclude] public float SparseColorScale = 0.5f;
		[JsonInclude] public Vector3 WaveFormDenseColor = new Vector3(0.0f, 0.79646015f, 0.3735608f);
		[JsonInclude] public Vector3 WaveFormSparseColor = new Vector3(0.0f, 0.39823008f, 0.1867804f);
		[JsonInclude] public float WaveFormMaxXPercentagePerChannel = 0.9f;

		// Log preferences
		[JsonInclude] public bool ShowLogWindow = true;
		[JsonInclude] public int LogWindowDateDisplay = 1;
		[JsonInclude] public int LogWindowLevel = (int)LogLevel.Info;
		[JsonInclude] public bool LogWindowLineWrap;

		// Option preferences
		[JsonInclude] public bool ShowOptionsWindow = false;

		// Strings are serialized, but converted to an array of booleans for UI.
		[JsonIgnore] public bool[] StartupStepsTypesBools;
		[JsonInclude] public string[] StartupStepsTypes = { "dance-single", "dance-double" };

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

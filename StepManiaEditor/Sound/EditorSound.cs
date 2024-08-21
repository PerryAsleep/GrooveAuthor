using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FMOD;
using Fumen;
using StepManiaLibrary;

namespace StepManiaEditor;

/// <summary>
/// EditorSounds are uncompressed buffers of PCM float data at a specified sample rate.
/// EditorSounds can optionally automatically reload the contents of their underlying files from disk when those files are modified.
/// EditorSounds can optionally populate a SoundMipMap for rendering a waveform of the sound data.
/// EditorSounds load asynchronously, and notify listeners when the underlying sample data has changed.
/// </summary>
internal sealed class EditorSound : Notifier<EditorSound>
{
	/// <summary>
	/// State used for performing async loads of sound data.
	/// </summary>
	private class SoundLoadState
	{
		/// <summary>
		/// The name of the file to load.
		/// </summary>
		private readonly string File;

		/// <summary>
		/// Whether or not the EditorSound's SoundMipMap should be generated when loading.
		/// </summary>
		private readonly bool GenerateMipMap;

		/// <summary>
		/// Whether or not to automatically detect the tempo when the sound is loaded.
		/// </summary>
		private readonly bool DetectTempo;

		/// <summary>
		/// Action to invoke upon load completion.
		/// </summary>
		private readonly Action CompletionAction;

		/// <summary>
		/// Constructor.
		/// </summary>
		/// <param name="file">Name of the file to load.</param>
		/// <param name="generateMipMap">Whether or not the EditorSound's SoundMipMap should be generated when loading.</param>
		/// <param name="detectTempo">Whether or not to automatically detect the tempo when the sound is loaded.</param>
		/// <param name="completionAction">Action to invoke upon load completion.</param>
		public SoundLoadState(string file, bool generateMipMap, bool detectTempo, Action completionAction)
		{
			File = file;
			GenerateMipMap = generateMipMap;
			DetectTempo = detectTempo;
			CompletionAction = completionAction;
		}

		public string GetFile()
		{
			return File;
		}

		public bool ShouldGenerateMipMap()
		{
			return GenerateMipMap;
		}

		public bool ShouldDetectTempo()
		{
			return DetectTempo;
		}

		public void InvokeCompletionAction()
		{
			CompletionAction?.Invoke();
		}
	}

	/// <summary>
	/// CancellableTask for performing async loads of sound data.
	/// </summary>
	private sealed class SoundLoadTask : CancellableTask<SoundLoadState>
	{
		/// <summary>
		/// The EditorSound to load.
		/// </summary>
		private readonly EditorSound Sound;

		/// <summary>
		/// The SoundManager for getting sample data.
		/// </summary>
		private readonly SoundManager SoundManager;

		/// <summary>
		/// Constructor.
		/// </summary>
		/// <param name="sound">EditorSound to load.</param>
		/// <param name="soundManager">SoundManager for getting sample data.</param>
		public SoundLoadTask(EditorSound sound, SoundManager soundManager)
		{
			Sound = sound;
			SoundManager = soundManager;
		}

		/// <summary>
		/// Called when loading should begin.
		/// </summary>
		/// <param name="state">SoundLoadState to use for loading.</param>
		protected override async Task DoWork(SoundLoadState state)
		{
			// Reset the mip map before loading the new sound because loading the sound
			// can take a moment and we don't want to continue to render the old audio.
			Sound.Reset();

			CancellationTokenSource.Token.ThrowIfCancellationRequested();

			var file = state.GetFile();
			if (!string.IsNullOrEmpty(file))
			{
				// For logging, get the file name without the path.
				var fileName = file;
				try
				{
					fileName = System.IO.Path.GetFileName(file);
				}
				catch (Exception)
				{
					// Ignored
				}

				Logger.Info($"Loading {fileName}...");

				// Load the sound file.
				// This is not cancelable. According to FMOD: "you can't cancel it"
				// https://qa.fmod.com/t/reusing-channels/13145/3
				// Normally this is not a problem, but for hour-long files this is unfortunate.
				var fmodSound = await SoundManager.LoadAsync(file);
				CancellationTokenSource.Token.ThrowIfCancellationRequested();
				Logger.Info($"Loaded {fileName}.");

				try
				{
					Logger.Info($"Parsing {fileName}...");

					// Allocate and set the sample buffer immediately so we can start playing it as we write to it.
					SoundManager.AllocateSampleBuffer(fmodSound, Sound.SampleRate, out var samples, out var numChannels);
					Sound.SetSampleData(numChannels, samples);

					CancellationTokenSource.Token.ThrowIfCancellationRequested();

					// Add a task for parsing the samples into the buffer.
					var parsingTasks = new List<Task>
					{
						ParseSamples(fmodSound, samples, numChannels, state),
					};

					// Add a task for setting up the new sound mip map.
					// This operates on the FMOD sound and can run in parallel.
					if (state.ShouldGenerateMipMap() && Sound.MipMap != null)
					{
						parsingTasks.Add(Sound.MipMap.CreateMipMapAsync(fmodSound,
							Utils.WaveFormTextureWidth,
							CancellationTokenSource.Token));
					}

					// Run the parsing tasks.
					await Task.WhenAll(parsingTasks);

					Logger.Info($"Parsed {fileName}.");
				}
				finally
				{
					SoundManager.ErrCheck(fmodSound.release());
				}
			}

			state.InvokeCompletionAction();
		}

		private async Task ParseSamples(Sound fmodSound, float[] samples, int numChannels, SoundLoadState state)
		{
			// Fill the sample buffer.
			await SoundManager.FillSamplesAsync(fmodSound, Sound.SampleRate, samples, numChannels,
				CancellationTokenSource.Token);
			Sound.SetSampleDataFullyLoaded();
			CancellationTokenSource.Token.ThrowIfCancellationRequested();

			// When detecting tempo it has to occur after all samples have been filled so we can read them.
			if (state.ShouldDetectTempo())
			{
				Logger.Info("Analyzing tempo...");
				await Sound.DetectTempo(CancellationTokenSource.Token);
				if (Sound.TempoResults != null)
				{
					Logger.Info($"Analyzed tempo. Best result: {Sound.TempoResults.GetBestTempo():N2} bpm.");
				}
				else
				{
					Logger.Info("Analyzed tempo.");
				}
			}
		}

		/// <summary>
		/// Called when loading has been cancelled.
		/// </summary>
		protected override void Cancel()
		{
			Sound.Reset();
		}
	}

	public const string NotificationSampleDataChanged = "SampleDataChanged";

	/// <summary>
	/// Sample rate of SampleData in Hz.
	/// </summary>
	private readonly uint SampleRate;

	/// <summary>
	/// Whether or not this EditorSound should automatically reload from disk when the file changes.
	/// </summary>
	private readonly bool ShouldAutoReloadFromDisk;

	/// <summary>
	/// Lock for mutating NumChannels and SampleData together.
	/// </summary>
	private readonly object SampleLock = new();

	/// <summary>
	/// Number of channels in the SampleData.
	/// </summary>
	private int NumChannels;

	/// <summary>
	/// PCM float sample data with interleaved channels.
	/// </summary>
	private float[] SampleData;

	/// <summary>
	/// Whether or not the sample data has been fully loaded.
	/// </summary>
	private bool SampleDataLoaded;

	/// <summary>
	/// SoundLoadState for the most recent load.
	/// </summary>
	private SoundLoadState LoadState;

	/// <summary>
	/// SoundLoadTask for loading the sound file.
	/// </summary>
	private readonly SoundLoadTask LoadTask;

	/// <summary>
	/// FileSystemWatcher for observing changes to the underlying file for automatic reloading.
	/// </summary>
	private FileSystemWatcher FileWatcher;

	/// <summary>
	/// Optional SoundMipMap.
	/// </summary>
	private readonly SoundMipMap MipMap;

	/// <summary>
	/// Automatic tempo detection results.
	/// </summary>
	private TempoDetector.IResults TempoResults;

	private bool DetectingTempo;

	/// <summary>
	/// Constructor.
	/// </summary>
	/// <param name="soundManager">SoundManager to use for loading sounds.</param>
	/// <param name="mipMap">Optional SoundMipMap to use with this EditorSound.</param>
	/// <param name="sampleRate">Desired sample rate for storing the sound data.</param>
	/// <param name="shouldAutoReloadFromDisk">Whether or not to automatically reload from disk when the underlying file changes.</param>
	public EditorSound(SoundManager soundManager,
		SoundMipMap mipMap,
		uint sampleRate,
		bool shouldAutoReloadFromDisk)
	{
		ShouldAutoReloadFromDisk = shouldAutoReloadFromDisk;
		SampleRate = sampleRate;

		if (mipMap != null)
		{
			MipMap = mipMap;
			MipMap.SetLoadParallelism(Preferences.Instance.PreferencesWaveForm.WaveFormLoadingMaxParallelism);
		}

		LoadTask = new SoundLoadTask(this, soundManager);
	}

	/// <summary>
	/// Reset's this EditorSound's data. This clears the sample data and reset the SoundMipMap.
	/// </summary>
	private void Reset()
	{
		SetSampleData(0, null);
		MipMap?.Reset();
		TempoResults = null;
		DetectingTempo = false;
	}

	/// <summary>
	/// Sets new sample data.
	/// Samples within the sample data may still be set asynchronously.
	/// </summary>
	/// <param name="numChannels">Number of channels of the data.</param>
	/// <param name="sampleData">PCM float data.</param>
	private void SetSampleData(int numChannels, float[] sampleData)
	{
		lock (SampleLock)
		{
			NumChannels = numChannels;
			SampleData = sampleData;
			SampleDataLoaded = false;
		}

		Notify(NotificationSampleDataChanged, this);
	}

	/// <summary>
	/// Sets the sample data to be fully loaded and parsed into our SampleData array.
	/// </summary>
	private void SetSampleDataFullyLoaded()
	{
		if (SampleData != null)
			SampleDataLoaded = true;
	}

	/// <summary>
	/// Gets this EditorSound's sample data.
	/// </summary>
	/// <returns>
	/// Tuple where the first value is the number of channels of data and the second value is the PCM float data.
	/// </returns>
	public (int, float[]) GetSampleData()
	{
		int numChannels;
		float[] sampleData;
		lock (SampleLock)
		{
			numChannels = NumChannels;
			sampleData = SampleData;
		}

		return (numChannels, sampleData);
	}

	/// <summary>
	/// Load the sound specified by the given file.
	/// </summary>
	/// <param name="file">Sound file to load.</param>
	/// <param name="generateMipMap">Whether a SoundMipMap should be generated for this sound.</param>
	/// <param name="detectTempo">Whether or not to automatically detect the tempo when the sound is loaded.</param>
	public void LoadAsync(string file, bool generateMipMap, bool detectTempo, Action completionAction)
	{
		StopObservingFile();
		LoadState = new SoundLoadState(file, generateMipMap, detectTempo, completionAction);
		StartObservingFile();
		LoadAsyncFromLoadState();
	}

	/// <summary>
	/// Loads the sound from the LoadState.
	/// </summary>
	private async void LoadAsyncFromLoadState()
	{
		if (LoadState == null)
			return;
		await LoadTask.Start(LoadState);
	}

	/// <summary>
	/// Gets the length of the sound as a time in seconds.
	/// </summary>
	/// <returns>Length of the sound as a time in seconds.</returns>
	public double GetLengthInSeconds()
	{
		lock (SampleLock)
		{
			if (SampleData == null)
				return 0.0;
			return (double)SampleData.Length / NumChannels / SampleRate;
		}
	}

	/// <summary>
	/// Returns whether or not the sound data is loaded from disk.
	/// </summary>
	/// <returns>True if the data is loaded and false otherwise.</returns>
	public bool IsLoaded(bool completelyLoaded)
	{
		if (SampleData == null)
			return false;
		if (completelyLoaded)
			return SampleDataLoaded;
		return true;
	}

	/// <summary>
	/// Gets this EditorSound's file name.
	/// </summary>
	/// <returns>File name.</returns>
	public string GetFile()
	{
		return LoadState?.GetFile();
	}

	/// <summary>
	/// Gets this EditorSound's SoundMipMap.
	/// </summary>
	/// <returns>SoundMipMap.</returns>
	public SoundMipMap GetSoundMipMap()
	{
		return MipMap;
	}

	/// <summary>
	/// Asynchronously detect this sound's tempo.
	/// Results will be returned, and stored internally for later access via GetTempoDetectionResults.
	/// </summary>
	/// <param name="token">CancellationToken.</param>
	/// <returns>Tempo detection results.</returns>
	public async Task<TempoDetector.IResults> DetectTempo(CancellationToken token)
	{
		if (DetectingTempo)
		{
			throw new InvalidOperationException("Cannot detect tempo while already detecting tempo.");
		}

		DetectingTempo = true;
		TempoResults = null;
		if (!IsLoaded(true))
		{
			DetectingTempo = false;
			return TempoResults;
		}

		var (numChannels, data) = GetSampleData();
		if (data == null)
		{
			DetectingTempo = false;
			return TempoResults;
		}

		var settings = Preferences.Instance.PreferencesTempoDetection.CreateSettings(numChannels, (int)SampleRate);
		var results = await TempoDetector.DetectTempo(data, settings, token);
		token.ThrowIfCancellationRequested();
		TempoResults = results;
		DetectingTempo = false;
		return TempoResults;
	}

	/// <summary>
	/// Returns whether or not the tempo is currently being detected.
	/// </summary>
	/// <returns>True if the tempo is currently being detected and false otherwise.</returns>
	public bool IsDetectingTempo()
	{
		return DetectingTempo;
	}

	/// <summary>
	/// Gets previously cached tempo detection results.
	/// </summary>
	/// <returns>Cached tempo detection results.</returns>
	public TempoDetector.IResults GetTempoDetectionResults()
	{
		return TempoResults;
	}

	#region File Observation

	/// <summary>
	/// Start observing the file from the LoadState for external changes.
	/// </summary>
	private void StartObservingFile()
	{
		if (!ShouldAutoReloadFromDisk)
			return;

		var loadStateFile = LoadState?.GetFile();
		if (string.IsNullOrEmpty(loadStateFile))
			return;

		try
		{
			var fullPath = System.IO.Path.GetFullPath(loadStateFile);
			var dir = System.IO.Path.GetDirectoryName(fullPath);
			var file = System.IO.Path.GetFileName(fullPath);
			if (!string.IsNullOrEmpty(dir) && !string.IsNullOrEmpty(file))
			{
				FileWatcher = new FileSystemWatcher(dir);
				FileWatcher.NotifyFilter = NotifyFilters.LastWrite;
				FileWatcher.Changed += OnFileChangedNotification;
				FileWatcher.Filter = file;
				FileWatcher.EnableRaisingEvents = true;
			}
		}
		catch (Exception e)
		{
			Logger.Error($"Failed to observe {loadStateFile} for changes: {e}");
		}
	}

	/// <summary>
	/// Callback for handling changes to the underlying file.
	/// </summary>
	private void OnFileChangedNotification(object sender, FileSystemEventArgs e)
	{
		if (e.ChangeType != WatcherChangeTypes.Changed)
			return;
		if (string.IsNullOrEmpty(LoadState?.GetFile()))
			return;
		Logger.Info($"Reloading {LoadState.GetFile()} due to external modification.");
		LoadAsyncFromLoadState();
	}

	/// <summary>
	/// Stop observing the sound file for external changes.
	/// </summary>
	private void StopObservingFile()
	{
		FileWatcher = null;
	}

	#endregion File Observation
}

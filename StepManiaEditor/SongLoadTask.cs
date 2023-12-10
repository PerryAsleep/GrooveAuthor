using System.Threading.Tasks;
using Fumen;
using Fumen.Converters;
using Microsoft.Xna.Framework.Graphics;
using static Fumen.Converters.SMCommon;

namespace StepManiaEditor;

/// <summary>
/// State used for performing async loads of Song files.
/// </summary>
internal sealed class SongLoadState
{
	private readonly string FileName;
	private readonly ChartType ChartType;
	private readonly ChartDifficultyType ChartDifficultyType;

	public SongLoadState(string fileName, ChartType chartType, ChartDifficultyType chartDifficultyType)
	{
		FileName = fileName;
		ChartType = chartType;
		ChartDifficultyType = chartDifficultyType;
	}

	public string GetFileName()
	{
		return FileName;
	}

	public ChartType GetChartType()
	{
		return ChartType;
	}

	public ChartDifficultyType GetChartDifficultyType()
	{
		return ChartDifficultyType;
	}
}

/// <summary>
/// CancellableTask for performing async loads of Song files.
/// </summary>
internal sealed class SongLoadTask : CancellableTask<SongLoadState>
{
	private readonly Editor Editor;
	private readonly GraphicsDevice GraphicsDevice;
	private readonly ImGuiRenderer ImGuiRenderer;

	private readonly object Lock = new();

	private EditorSong ActiveSong;
	private EditorChart ActiveChart;
	private string ActiveFileName;

	public SongLoadTask(Editor editor, GraphicsDevice graphicsDevice, ImGuiRenderer imGuiRenderer)
	{
		Editor = editor;
		GraphicsDevice = graphicsDevice;
		ImGuiRenderer = imGuiRenderer;
	}

	protected override async Task DoWork(SongLoadState state)
	{
		EditorSong newActiveSong = null;

		// Load the song file.
		var fileName = state.GetFileName();
		var reader = Reader.CreateReader(fileName);
		if (reader == null)
		{
			Logger.Error($"Unsupported file format. Cannot parse {fileName}");
			return;
		}

		Logger.Info($"Loading {fileName}...");
		var song = await reader.LoadAsync(CancellationTokenSource.Token);
		if (song == null)
		{
			Logger.Error($"Failed to load {fileName}");
			return;
		}

		Logger.Info($"Loaded {fileName}");

		CancellationTokenSource.Token.ThrowIfCancellationRequested();
		await Task.Run(() =>
		{
			newActiveSong = new EditorSong(
				fileName,
				song,
				GraphicsDevice,
				ImGuiRenderer,
				Editor.IsChartSupported);
		});

		// Select the best Chart to make active.
		var newActiveChart = Editor.SelectBestChart(newActiveSong, state.GetChartType(), state.GetChartDifficultyType());
		CancellationTokenSource.Token.ThrowIfCancellationRequested();

		// Save results
		lock (Lock)
		{
			ActiveSong = newActiveSong;
			ActiveChart = newActiveChart;
			ActiveFileName = fileName;
		}
	}

	/// <summary>
	/// Called when loading has been cancelled.
	/// </summary>
	protected override void Cancel()
	{
		ClearResults();
	}

	public (EditorSong, EditorChart, string) GetResults()
	{
		lock (Lock)
		{
			return (ActiveSong, ActiveChart, ActiveFileName);
		}
	}

	public void ClearResults()
	{
		lock (Lock)
		{
			ActiveFileName = null;
			ActiveSong = null;
			ActiveChart = null;
		}
	}
}

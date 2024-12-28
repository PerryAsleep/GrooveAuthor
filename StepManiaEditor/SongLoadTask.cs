using System.Collections.Generic;
using System.Threading.Tasks;
using Fumen;
using Fumen.Converters;
using Microsoft.Xna.Framework.Graphics;

namespace StepManiaEditor;

/// <summary>
/// State used for performing async loads of Song files.
/// </summary>
internal sealed class SongLoadState
{
	private readonly string FileName;
	private readonly IActiveChartListProvider ChartListProvider;

	public SongLoadState(string fileName, IActiveChartListProvider chartListProvider)
	{
		FileName = fileName;
		ChartListProvider = chartListProvider;
	}

	public string GetFileName()
	{
		return FileName;
	}

	public IActiveChartListProvider GetChartListProvider()
	{
		return ChartListProvider;
	}
}

/// <summary>
/// CancellableTask for performing async loads of Song files.
/// </summary>
internal sealed class SongLoadTask : CancellableTask<SongLoadState>
{
	private readonly GraphicsDevice GraphicsDevice;
	private readonly ImGuiRenderer ImGuiRenderer;

	private readonly object Lock = new();

	private EditorSong ActiveSong;
	private List<EditorChart> ActiveCharts;
	private EditorChart FocusedChart;
	private string ActiveFileName;

	public SongLoadTask(GraphicsDevice graphicsDevice, ImGuiRenderer imGuiRenderer)
	{
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

		// Select the best Charts to make active.
		var focusedChart = state.GetChartListProvider().GetChartToUseForFocusedChart(newActiveSong);
		var activeCharts = state.GetChartListProvider().GetChartsToUseForActiveCharts(newActiveSong);
		CancellationTokenSource.Token.ThrowIfCancellationRequested();

		// Save results
		lock (Lock)
		{
			ActiveSong = newActiveSong;
			FocusedChart = focusedChart;
			ActiveCharts = activeCharts;
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

	public (EditorSong, EditorChart, List<EditorChart>, string) GetResults()
	{
		lock (Lock)
		{
			return (ActiveSong, FocusedChart, ActiveCharts, ActiveFileName);
		}
	}

	public void ClearResults()
	{
		lock (Lock)
		{
			ActiveFileName = null;
			ActiveSong = null;
			FocusedChart = null;
			ActiveCharts = null;
		}
	}
}

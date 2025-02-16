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

internal sealed class SongLoadResult
{
	private readonly EditorSong Song;
	private readonly List<EditorChart> ActiveCharts;
	private readonly EditorChart FocusedChart;
	private readonly string FileName;

	public SongLoadResult(EditorSong song, List<EditorChart> activeCharts, EditorChart focusedChart, string fileName)
	{
		Song = song;
		ActiveCharts = activeCharts;
		FocusedChart = focusedChart;
		FileName = fileName;
	}

	public EditorSong GetSong()
	{
		return Song;
	}

	public List<EditorChart> GetActiveCharts()
	{
		return ActiveCharts;
	}

	public EditorChart GetFocusedChart()
	{
		return FocusedChart;
	}

	public string GetFileName()
	{
		return FileName;
	}
}

/// <summary>
/// CancellableTask for performing async loads of Song files.
/// </summary>
internal sealed class SongLoadTask : CancellableTask<SongLoadState, SongLoadResult>
{
	private readonly GraphicsDevice GraphicsDevice;
	private readonly ImGuiRenderer ImGuiRenderer;

	public SongLoadTask(GraphicsDevice graphicsDevice, ImGuiRenderer imGuiRenderer)
	{
		GraphicsDevice = graphicsDevice;
		ImGuiRenderer = imGuiRenderer;
	}

	protected override async Task<SongLoadResult> DoWork(SongLoadState state)
	{
		EditorSong newSong = null;

		// Load the song file.
		var fileName = state.GetFileName();
		var reader = Reader.CreateReader(fileName);
		if (reader == null)
		{
			Logger.Error($"Unsupported file format. Cannot parse {fileName}");
			return null;
		}

		Logger.Info($"Loading {fileName}...");
		var song = await reader.LoadAsync(CancellationTokenSource.Token);
		if (song == null)
		{
			Logger.Error($"Failed to load {fileName}");
			return null;
		}

		Logger.Info($"Loaded {fileName}");

		CancellationTokenSource.Token.ThrowIfCancellationRequested();
		await Task.Run(() =>
		{
			newSong = new EditorSong(
				fileName,
				song,
				GraphicsDevice,
				ImGuiRenderer,
				Editor.IsChartSupported);
		});

		// Select the best Charts to make active.
		var focusedChart = state.GetChartListProvider().GetChartToUseForFocusedChart(newSong);
		var activeCharts = state.GetChartListProvider().GetChartsToUseForActiveCharts(newSong);
		CancellationTokenSource.Token.ThrowIfCancellationRequested();

		return new SongLoadResult(newSong, activeCharts, focusedChart, fileName);
	}

	/// <summary>
	/// Called when loading has been cancelled.
	/// </summary>
	protected override void Cancel()
	{
	}
}

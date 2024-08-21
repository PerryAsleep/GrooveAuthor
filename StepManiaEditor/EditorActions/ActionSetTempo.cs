using System.Collections.Generic;

namespace StepManiaEditor;

/// <summary>
/// Action to set the starting tempo of all charts which share the given music file.
/// </summary>
internal sealed class ActionSetTempo : EditorAction
{
	private readonly Dictionary<EditorChart, double> OriginalTempos = new();
	private readonly EditorSong Song;
	private readonly bool DoesSongAlreadyHaveTempo;
	private readonly double ExistingSongTempo;
	private readonly double Tempo;
	private readonly string MusicFileName;

	public ActionSetTempo(EditorSong song, string musicFileName, double tempo) : base(false, false)
	{
		Song = song;
		Tempo = tempo;
		MusicFileName = musicFileName;

		DoesSongAlreadyHaveTempo = Song.TryGetTempoForMusicFile(musicFileName, out ExistingSongTempo);

		foreach (var chart in Song.GetCharts())
		{
			var chartMusicFile = Song.GetFullPathToSongResource(chart.GetMusicPathForPlayback());
			if (chartMusicFile == musicFileName)
			{
				OriginalTempos.Add(chart, chart.GetStartingTempo());
			}
		}
	}

	public override string ToString()
	{
		return $"Set chart starting tempos to {Tempo}.";
	}

	public override bool AffectsFile()
	{
		return true;
	}

	protected override void DoImplementation()
	{
		Song.SetTempoForMusicFile(Tempo, MusicFileName);
		foreach (var kvp in OriginalTempos)
		{
			kvp.Key.SetStartingTempo(Tempo);
		}
	}

	protected override void UndoImplementation()
	{
		if (DoesSongAlreadyHaveTempo)
			Song.SetTempoForMusicFile(ExistingSongTempo, MusicFileName);
		else
			Song.RemoveTempoForMusicFile(MusicFileName);

		foreach (var kvp in OriginalTempos)
		{
			kvp.Key.SetStartingTempo(kvp.Value);
		}
	}
}

using Fumen;
using System;

namespace StepManiaEditor;

/// <summary>
/// The user's position in the Chart in the Editor. The position can be represented in multiple ways.
///  - SongTime is the time of the music at the current position. This takes into account the music offset.
///  - ChartTime is the time of the current position. This does not take into account the music offset.
///  - ChartPosition is the integer / row position of the current position.
/// The position can be updated by updating any of these values and the others will update accordingly.
/// </summary>
internal sealed class EditorPosition
{
	public EditorChart ActiveChart;
	private readonly Action OnPositionChanged;

	private double SongTimeInternal;

	public double SongTime
	{
		get => SongTimeInternal;
		set
		{
			SongTimeInternal = value;
			ChartTimeInternal = GetChartTimeFromSongTime(ActiveChart, SongTimeInternal);
			ActiveChart?.TryGetChartPositionFromTime(ChartTimeInternal, ref ChartPositionInternal);
			OnPositionChanged?.Invoke();
		}
	}

	private double ChartTimeInternal;

	public double ChartTime
	{
		get => ChartTimeInternal;
		set
		{
			ChartTimeInternal = value;
			ActiveChart?.TryGetChartPositionFromTime(ChartTimeInternal, ref ChartPositionInternal);
			SongTimeInternal = GetSongTimeFromChartTime(ActiveChart, ChartTimeInternal);
			OnPositionChanged?.Invoke();
		}
	}

	private double ChartPositionInternal;

	public double ChartPosition
	{
		get => ChartPositionInternal;
		set
		{
			ChartPositionInternal = value;
			ActiveChart?.TryGetTimeFromChartPosition(ChartPositionInternal, ref ChartTimeInternal);
			SongTimeInternal = GetSongTimeFromChartTime(ActiveChart, ChartTimeInternal);
			OnPositionChanged?.Invoke();
		}
	}

	private double SongTimeInterpolationTimeStart;
	private double SongTimeAtStartOfInterpolation;
	private double DesiredSongTime;

	private double ChartPositionInterpolationTimeStart;
	private double ChartPositionAtStartOfInterpolation;
	private double DesiredChartPosition;

	public EditorPosition(Action onPositionChanged)
	{
		Reset();
		OnPositionChanged = onPositionChanged;
	}

	public static double GetSongTimeFromChartTime(EditorChart chart, double chartTime)
	{
		return chartTime - (chart?.GetMusicOffset() ?? 0.0) + (chart?.GetEditorSong()?.SyncOffset ?? 0.0);
	}

	public static double GetChartTimeFromSongTime(EditorChart chart, double songTime)
	{
		return songTime + (chart?.GetMusicOffset() ?? 0.0) - (chart?.GetEditorSong()?.SyncOffset ?? 0.0);
	}

	public int GetNearestRow()
	{
		var row = (int)ChartPosition;
		if (ChartPosition - row > 0.5)
			row++;
		return row;
	}

	public void FinishInterpolating()
	{
		ChartPosition = DesiredChartPosition;
		SongTime = DesiredSongTime;
	}

	public void CancelInterpolating()
	{
		SetDesiredPositionToCurrent();
	}

	public void SetDesiredPositionToCurrent()
	{
		DesiredChartPosition = ChartPosition;
		DesiredSongTime = SongTime;
	}

	public void Reset()
	{
		ChartPosition = 0.0;
		CancelInterpolating();
	}

	public void BeginSongTimeInterpolation(double timeNow, double songTimeDelta)
	{
		SongTimeInterpolationTimeStart = timeNow;
		DesiredSongTime += songTimeDelta;
		SongTimeAtStartOfInterpolation = SongTime;
	}

	public void BeginChartPositionInterpolation(double timeNow, double chartPositionDelta)
	{
		ChartPositionInterpolationTimeStart = timeNow;
		DesiredChartPosition += chartPositionDelta;
		ChartPositionAtStartOfInterpolation = ChartPosition;
	}

	public bool IsInterpolatingSongTime()
	{
		return !SongTime.DoubleEquals(DesiredSongTime);
	}

	public bool IsInterpolatingChartPosition()
	{
		return !ChartPosition.DoubleEquals(DesiredChartPosition);
	}

	public void UpdateSongTimeInterpolation(double timeNow)
	{
		var interpolationTime = Preferences.Instance.PreferencesScroll.ScrollInterpolationDuration;
		if (IsInterpolatingSongTime())
		{
			SongTime = Interpolation.Lerp(
				SongTimeAtStartOfInterpolation,
				DesiredSongTime,
				SongTimeInterpolationTimeStart,
				SongTimeInterpolationTimeStart + interpolationTime,
				timeNow);
			DesiredChartPosition = ChartPosition;
		}
	}

	public void UpdateChartPositionInterpolation(double timeNow)
	{
		var interpolationTime = Preferences.Instance.PreferencesScroll.ScrollInterpolationDuration;
		if (IsInterpolatingChartPosition())
		{
			ChartPosition = Interpolation.Lerp(
				ChartPositionAtStartOfInterpolation,
				DesiredChartPosition,
				ChartPositionInterpolationTimeStart,
				ChartPositionInterpolationTimeStart + interpolationTime,
				timeNow);
			DesiredSongTime = SongTime;
		}
	}
}

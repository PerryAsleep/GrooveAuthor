using System;
using Fumen;

namespace StepManiaEditor;

/// <summary>
/// Readonly interface for an EditorPosition.
/// </summary>
internal interface IReadOnlyEditorPosition
{
	public double SongTime { get; }
	public double ChartTime { get; }
	public double ChartPosition { get; }
	public EditorChart ActiveChart { get; }
	public int GetNearestRow();
}

/// <summary>
/// The user's position in the Chart in the Editor. The position can be represented in multiple ways.
///  - SongTime is the time of the music at the current position. This takes into account the music offset.
///  - ChartTime is the time of the current position. This does not take into account the music offset.
///  - ChartPosition is the integer / row position of the current position.
/// The position can be updated by updating any of these values and the others will update accordingly.
/// </summary>
internal sealed class EditorPosition : IReadOnlyEditorPosition
{
	private EditorChart ActiveChartInternal;

	public EditorChart ActiveChart
	{
		get => ActiveChartInternal;
		set
		{
			ActiveChartInternal = value;
			// Different charts may have different timing events and time offsets.
			// When changing charts, reset the Song time to ensure other values are correct.
			ChartTime = ChartTimeInternal;
		}
	}

	private readonly Action OnPositionChanged;

	private double SongTimeInternal;

	public double SongTime
	{
		get => SongTimeInternal;
		set
		{
			var changed = false;
			if (!SongTimeInternal.DoubleEquals(value))
			{
				changed = true;
				SongTimeInternal = value;
			}

			var newChartTime = GetChartTimeFromSongTime(ActiveChart, SongTimeInternal);
			if (!ChartTimeInternal.DoubleEquals(newChartTime))
			{
				changed = true;
				ChartTimeInternal = newChartTime;
			}

			var newChartPosition = ChartPositionInternal;
			ActiveChart?.TryGetChartPositionFromTime(ChartTimeInternal, ref newChartPosition);
			if (!ChartPositionInternal.DoubleEquals(newChartPosition))
			{
				changed = true;
				ChartPositionInternal = newChartPosition;
			}

			if (changed)
				OnPositionChanged?.Invoke();
		}
	}

	private double ChartTimeInternal;

	public double ChartTime
	{
		get => ChartTimeInternal;
		set
		{
			var changed = false;

			if (!ChartTimeInternal.DoubleEquals(value))
			{
				changed = true;
				ChartTimeInternal = value;
			}

			var newChartPosition = ChartPositionInternal;
			if (ActiveChart == null || !ActiveChart.TryGetChartPositionFromTime(ChartTimeInternal, ref newChartPosition))
				newChartPosition = 0.0;
			if (!ChartPositionInternal.DoubleEquals(newChartPosition))
			{
				changed = true;
				ChartPositionInternal = newChartPosition;
			}

			var newSongTime = GetSongTimeFromChartTime(ActiveChart, ChartTimeInternal);
			if (!SongTimeInternal.DoubleEquals(newSongTime))
			{
				changed = true;
				SongTimeInternal = newSongTime;
			}

			if (changed)
				OnPositionChanged?.Invoke();
		}
	}

	private double ChartPositionInternal;

	public double ChartPosition
	{
		get => ChartPositionInternal;
		set
		{
			var changed = false;

			if (!ChartPositionInternal.DoubleEquals(value))
			{
				changed = true;
				ChartPositionInternal = value;
			}

			var newChartTime = ChartTimeInternal;
			if (ActiveChart == null || !ActiveChart.TryGetTimeFromChartPosition(ChartPositionInternal, ref newChartTime))
				newChartTime = 0.0;
			if (!ChartTimeInternal.DoubleEquals(newChartTime))
			{
				changed = true;
				ChartTimeInternal = newChartTime;
			}

			var newSongTime = GetSongTimeFromChartTime(ActiveChart, ChartTimeInternal);
			if (!SongTimeInternal.DoubleEquals(newSongTime))
			{
				changed = true;
				SongTimeInternal = newSongTime;
			}

			if (changed)
				OnPositionChanged?.Invoke();
		}
	}

	private double SongTimeInterpolationTimeStart;
	private double SongTimeAtStartOfInterpolation;
	private double DesiredSongTime;

	private double ChartPositionInterpolationTimeStart;
	private double ChartPositionAtStartOfInterpolation;
	private double DesiredChartPosition;

	/// <summary>
	/// Constructor creating an EditorPosition from an EditorChart.
	/// </summary>
	public EditorPosition(Action onPositionChanged, EditorChart activeChart)
	{
		Reset();
		ActiveChart = activeChart;
		OnPositionChanged = onPositionChanged;
	}

	/// <summary>
	/// Constructor creating an EditorPosition with a 0.0 ChartTime and ChartPosition.
	/// SongTime will be derived from the NewSongSyncOffset.
	/// Intended to get a zero position for when there is no EditorSong or EditorChart.
	/// </summary>
	public EditorPosition()
	{
		ChartPositionInternal = 0.0;
		ChartTimeInternal = 0.0;
		SongTimeInternal = Preferences.Instance.PreferencesOptions.NewSongSyncOffset;
	}

	/// <summary>
	/// Constructor creating an EditorPosition with a 0.0 ChartTime and ChartPosition.
	/// SongTime will be derived from the given EditorSong's SyncOffset.
	/// Intended to get a zero position for an EditorSong when there is no EditorChart.
	/// </summary>
	public EditorPosition(EditorSong activeSong)
	{
		ChartPositionInternal = 0.0;
		ChartTimeInternal = 0.0;
		SongTimeInternal = activeSong.SyncOffset;
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

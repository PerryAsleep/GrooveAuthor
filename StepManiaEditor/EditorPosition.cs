using System;

namespace StepManiaEditor
{
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
				ChartTimeInternal = SongTimeInternal + (ActiveChart?.GetMusicOffset() ?? 0.0);
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
				SongTimeInternal = ChartTimeInternal - (ActiveChart?.GetMusicOffset() ?? 0.0);
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
				SongTimeInternal = ChartTimeInternal - (ActiveChart?.GetMusicOffset() ?? 0.0);
				OnPositionChanged?.Invoke();
			}
		}

		public int GetNearestRow()
		{
			var row = (int)ChartPosition;
			if (ChartPosition - row > 0.5)
				row++;
			return row;
		}

		public EditorPosition(Action onPositionChanged)
		{
			OnPositionChanged = onPositionChanged;
		}
	}
}

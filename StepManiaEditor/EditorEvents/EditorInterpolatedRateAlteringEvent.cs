using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Fumen;
using Fumen.ChartDefinition;
using Fumen.Converters;
using Microsoft.Xna.Framework.Graphics;
using static Fumen.Utils;

namespace StepManiaEditor
{
	public class EditorInterpolatedRateAlteringEvent : EditorEvent, IComparable<EditorInterpolatedRateAlteringEvent>
	{
		/// <summary>
		/// Row of this rate altering event.
		/// </summary>
		public double Row;
		/// <summary>
		/// SongTime of this rate altering event.
		/// </summary>
		public double SongTime;

		public double PreviousScrollRate = 1.0;

		public bool CanBeDeleted;

		private bool WidthDirty;

		public ScrollRateInterpolation ScrollRateInterpolationEvent;

		public string StringValue
		{
			get
			{
				if (ScrollRateInterpolationEvent.PreferPeriodAsTimeMicros)
				{
					var len = ToSeconds(ScrollRateInterpolationEvent.PeriodTimeMicros);
					return $"{ScrollRateInterpolationEvent.Rate}x/{len:G9}s";
				}
				else
				{
					return $"{ScrollRateInterpolationEvent.Rate}x/{ScrollRateInterpolationEvent.PeriodLengthIntegerPosition}rows";
				}
			}
			set
			{
				var (valid, rate, periodInt, periodTime, preferTime) = IsValidScrollRateInterpolationString(value);
				if (valid)
				{
					ScrollRateInterpolationEvent.Rate = rate;
					ScrollRateInterpolationEvent.PeriodLengthIntegerPosition = periodInt;
					ScrollRateInterpolationEvent.PeriodTimeMicros = periodTime;
					ScrollRateInterpolationEvent.PreferPeriodAsTimeMicros = preferTime;
					WidthDirty = true;
					EditorChart.OnInterpolatedRateAlteringEventModified(this);
				}
			}
		}

		public static (bool, double, int, long, bool) IsValidScrollRateInterpolationString(string v)
		{
			double rate = 0.0;
			int periodIntegerPosition = 0;
			long periodTimeMicros = 0L;
			bool preferPeriodAsTimeMicros = false;

			var match = Regex.Match(v, @"^(\d+\.?\d*|\d*\.?\d+)x/(\d+\.?\d*|\d*\.?\d+)(s|rows)$");
			if (!match.Success)
				return (false, rate, periodIntegerPosition, periodTimeMicros, preferPeriodAsTimeMicros);
			if (match.Groups.Count != 4)
				return (false, rate, periodIntegerPosition, periodTimeMicros, preferPeriodAsTimeMicros);
			if (!double.TryParse(match.Groups[1].Captures[0].Value, out rate))
				return (false, rate, periodIntegerPosition, periodTimeMicros, preferPeriodAsTimeMicros);
			if (match.Groups[3].Captures[0].Value == "s")
				preferPeriodAsTimeMicros = true;
			if (preferPeriodAsTimeMicros)
			{
				if (!double.TryParse(match.Groups[2].Captures[0].Value, out var periodSeconds))
					return (false, rate, periodIntegerPosition, periodTimeMicros, preferPeriodAsTimeMicros);
				periodTimeMicros = ToMicros(periodSeconds);
			}
			else
			{
				if (!int.TryParse(match.Groups[2].Captures[0].Value, out periodIntegerPosition))
					return (false, rate, periodIntegerPosition, periodTimeMicros, preferPeriodAsTimeMicros);
			}
			return (true, rate, periodIntegerPosition, periodTimeMicros, preferPeriodAsTimeMicros);
		}

		/// <remarks>
		/// This lazily updates the width if it is dirty.
		/// This is a bit of hack because in order to determine the width we need to call into
		/// ImGui but that is not a thread-safe operation. If we were to set the width when
		/// loading the chart for example, this could crash. By lazily setting it we avoid this
		/// problem as long as we assume the caller of GetW() happens on the main thread.
		/// </remarks>
		public override double GetW()
		{
			if (WidthDirty)
			{
				SetW(ImGuiLayoutUtils.GetMiscEditorEventStringWidth(StringValue));
				WidthDirty = false;
			}
			return base.GetW();
		}

		public EditorInterpolatedRateAlteringEvent(EditorChart editorChart, ScrollRateInterpolation chartEvent) : base(editorChart, chartEvent)
		{
			ScrollRateInterpolationEvent = chartEvent;
			if (ScrollRateInterpolationEvent != null)
				WidthDirty = true;
		}

		public bool InterpolatesByTime()
		{
			return ScrollRateInterpolationEvent.PreferPeriodAsTimeMicros;
		}

		public double GetInterpolatedScrollRateFromTime(double time)
		{
			return Interpolation.Lerp(
				PreviousScrollRate,
				ScrollRateInterpolationEvent.Rate,
				SongTime,
				SongTime + ToSeconds(ScrollRateInterpolationEvent.PeriodTimeMicros),
				time);
		}

		public double GetInterpolatedScrollRateFromRow(double row)
		{
			return Interpolation.Lerp(
				PreviousScrollRate,
				ScrollRateInterpolationEvent.Rate,
				Row,
				Row + ScrollRateInterpolationEvent.PeriodLengthIntegerPosition,
				row);
		}

		private class SortSongTimeHelper : IComparer<EditorInterpolatedRateAlteringEvent>
		{
			int IComparer<EditorInterpolatedRateAlteringEvent>.Compare(EditorInterpolatedRateAlteringEvent e1, EditorInterpolatedRateAlteringEvent e2)
			{
				var c = e1.SongTime.CompareTo(e2.SongTime);
				return c != 0 ? c : e1.CompareTo(e2);
			}
		}

		public static IComparer<EditorInterpolatedRateAlteringEvent> SortSongTime()
		{
			return new SortSongTimeHelper();
		}

		private class SortRowHelper : IComparer<EditorInterpolatedRateAlteringEvent>
		{
			int IComparer<EditorInterpolatedRateAlteringEvent>.Compare(EditorInterpolatedRateAlteringEvent e1, EditorInterpolatedRateAlteringEvent e2)
			{
				var c = e1.Row.CompareTo(e2.Row);
				return c != 0 ? c : e1.CompareTo(e2);
			}
		}

		public static IComparer<EditorInterpolatedRateAlteringEvent> SortRow()
		{
			return new SortRowHelper();
		}

		public int CompareTo(EditorInterpolatedRateAlteringEvent other)
		{
			var comparison = Row.CompareTo(other.Row);
			if (comparison != 0)
				return comparison;
			comparison = SongTime.CompareTo(other.SongTime);
			if (comparison != 0)
				return comparison;
			return SMCommon.SMEventComparer.Compare(ChartEvent, other.ChartEvent);
		}

		public override void Draw(TextureAtlas textureAtlas, SpriteBatch spriteBatch)
		{
			if (GetAlpha() <= 0.0f)
				return;
			ImGuiLayoutUtils.MiscEditorEventScrollRateInterpolationInputWidget(
				GetImGuiId(),
				this,
				nameof(StringValue),
				(int)GetX(), (int)GetY(), (int)GetW(),
				Utils.UISpeedsColorABGR,
				false,
				CanBeDeleted,
				GetAlpha());
		}
	}
}

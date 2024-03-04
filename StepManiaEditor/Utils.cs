using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Fumen;
using Fumen.Converters;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace StepManiaEditor;

internal sealed class Utils
{
	// TODO: Rename / Reorganize. Currently dumping a lot of rendering-related constants in here.

	public const int MaxLogFiles = 10;
	public const string CustomSavePropertyPrefix = "GA";

	public const uint UITempoColorRGBA = 0x8A297A79; // yellow
	public const uint UITimeSignatureColorRGBA = 0x8A297A29; // green
	public const uint UIStopColorRGBA = 0x8A29297A; // red
	public const uint UIDelayColorRGBA = 0x8A295E7A; // light orange
	public const uint UIWarpColorRGBA = 0x8A7A7929; // cyan
	public const uint UIScrollsColorRGBA = 0x8A7A2929; // blue
	public const uint UISpeedsColorRGBA = 0x8A7A294D; // purple

	public const uint UITicksColorRGBA = 0x8A295E7A; // orange
	public const uint UIMultipliersColorRGBA = 0x8A297A63; // lime
	public const uint UIFakesColorRGBA = 0x8A29467A; // dark orange
	public const uint UILabelColorRGBA = 0x8A68297A; // pink

	public const uint UIPatternColorRGBA = 0x8A6A7A29; // teal

	public const uint UIPreviewColorRGBA = 0x8A7A7A7A; // grey
	public const uint UILastSecondHintColorRGBA = 0x8A202020; // dark grey

	public const uint UIDifficultyBeginnerColorRGBA = 0xFF808040;
	public const uint UIDifficultyEasyColorRGBA = 0xFF4D804D;
	public const uint UIDifficultyMediumColorRGBA = 0xFF408080;
	public const uint UIDifficultyHardColorRGBA = 0xFF404080;
	public const uint UIDifficultyChallengeColorRGBA = 0xFF804080;
	public const uint UIDifficultyEditColorRGBA = 0xFF807D7B;

	/// <summary>
	/// Color for sparse area of waveform. BGR565. Red.
	/// The waveform-color shader expects this color to perform recoloring.
	/// </summary>
	public const ushort WaveFormColorSparse = 0xF800;

	/// <summary>
	/// Color for dense area of waveform. BGR565. Green.
	/// The waveform-color shader expects this color to perform recoloring.
	/// </summary>
	public const ushort WaveFormColorDense = 0x7E0;

	public const int MarkerTextureWidth = 128;
	public const int WaveFormTextureWidth = 1024;

	public const float BeatMarkerScaleToStartingFading = 0.15f;
	public const float BeatMarkerMinScale = 0.10f;
	public const float MeasureMarkerScaleToStartingFading = 0.10f;
	public const float MeasureMarkerMinScale = 0.05f;
	public const float MeasureNumberScaleToStartFading = 0.20f;
	public const float MeasureNumberMinScale = 0.10f;
	public const float MiscEventScaleToStartFading = 0.05f;
	public const float MiscEventMinScale = 0.04f;

	public const float NoteScaleToStartFading = 0.02f;
	public const float NoteMinScale = 0.01f;

	public const float ActiveEditEventAlpha = 0.8f;

	public const int MaxMarkersToDraw = 256;
	public const int MaxEventsToDraw = 2048;
	public const int MaxRateAlteringEventsToProcessPerFrame = 256;

	public const int MiniMapMaxNotesToDraw = 6144;

	public const string TextureIdMeasureMarker = "measure-marker";
	public const string TextureIdBeatMarker = "beat-marker";
	public const string TextureIdRegionRect = "region-rect";
	public const string TextureIdLogoAttribution = "logo-attribution";

	public static Color StopRegionColor = new(0x7A, 0x29, 0x29, 0x7F);
	public static Color DelayRegionColor = new(0x7A, 0x5E, 0x29, 0x7F);
	public static Color FakeRegionColor = new(0x7A, 0x46, 0x29, 0x7F);
	public static Color WarpRegionColor = new(0x29, 0x79, 0x7A, 0x7F);
	public static Color PreviewRegionColor = new(0x7A, 0x7A, 0x7A, 0x7F);
	public static Color PatternRegionColor = new(0x29, 0x7A, 0x6A, 0x7F);
	public static Color SelectionRegionColor = new(0xB8, 0xB4, 0x3E, 0x7F);

	public const double StopRegionZOffset = 0.1;
	public const double DelayRegionZOffset = 0.2;
	public const double FakeRegionZOffset = 0.3;
	public const double WarpRegionZOffset = 0.4;
	public const double PreviewRegionZOffset = 0.5;
	public const double PatternRegionZOffset = 0.6;

	public static readonly string[] ExpectedAudioFormats = { "mp3", "oga", "ogg", "wav" };
	public static readonly string[] ExpectedImageFormats = { "bmp", "gif", "jpeg", "jpg", "png", "tif", "tiff", "webp" };

	public static readonly string[] ExpectedVideoFormats =
		{ "avi", "f4v", "flv", "mkv", "mp4", "mpeg", "mpg", "mov", "ogv", "webm", "wmv" };

	public static readonly string[] ExpectedLyricsFormats = { "lrc" };

	private static string AppName;
	private static Version AppVersion;

	public enum HorizontalAlignment
	{
		Left,
		Center,
		Right,
	}

	public enum VerticalAlignment
	{
		Top,
		Center,
		Bottom,
	}

	public static string GetCustomPropertyName(string key)
	{
		return CustomSavePropertyPrefix + key;
	}

	public static string GetAppName()
	{
		return AppName ??= Assembly.GetExecutingAssembly().GetName().Name;
	}

	public static Version GetAppVersion()
	{
		return AppVersion ??= Assembly.GetExecutingAssembly().GetName().Version;
	}

	public static Vector2 GetDrawPos(
		SpriteFont font,
		string text,
		Vector2 anchorPos,
		float scale,
		HorizontalAlignment hAlign = HorizontalAlignment.Left,
		VerticalAlignment vAlign = VerticalAlignment.Top)
	{
		var x = anchorPos.X;
		var y = anchorPos.Y;
		var size = font.MeasureString(text);
		switch (hAlign)
		{
			case HorizontalAlignment.Center:
				x -= size.X * 0.5f * scale;
				break;
			case HorizontalAlignment.Right:
				x -= size.X * scale;
				break;
		}

		switch (vAlign)
		{
			case VerticalAlignment.Center:
				y -= size.Y * 0.5f * scale;
				break;
			case VerticalAlignment.Bottom:
				y -= size.Y * scale;
				break;
		}

		return new Vector2(x, y);
	}

	/// <summary>
	/// Given a reference to an EditorChart, ensure that that reference is still a valid
	/// chart for the active EditorSong. If it isn't, set it to the most appropriate
	/// EditorChart to be used based on the Editor's active chart and song.
	/// </summary>
	/// <param name="chart">The EditorChart to set.</param>
	/// <param name="editor">The Editor to use fallback choices.</param>
	public static void EnsureChartReferencesValidChartFromActiveSong(ref EditorChart chart, Editor editor)
	{
		var song = editor.GetActiveSong();

		// If the chart is set to a valid Chart, ensure that Chart still exists.
		// If the Chart does not exist, set the chart to null.
		if (chart != null)
		{
			if (song == null)
			{
				chart = null;
			}
			else
			{
				var charts = song.GetCharts();
				var sourceChartFound = false;
				foreach (var songChart in charts)
				{
					if (chart == songChart)
					{
						sourceChartFound = true;
						break;
					}
				}

				if (!sourceChartFound)
				{
					chart = null;
				}
			}
		}

		// If the chart is not set, try to set it.
		if (chart == null)
		{
			// Use the active Chart, if one exists.
			chart = editor.GetActiveChart();
			if (chart != null)
				return;

			// Failing that use, use any Chart from the active Song.
			if (song != null)
			{
				var charts = song.GetCharts();
				if (charts?.Count > 0)
				{
					chart = charts[0];
				}
			}
		}
	}

	#region Color Helpers

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static uint ColorRGBAInterpolate(uint startColor, uint endColor, float endPercent)
	{
		return (uint)((startColor & 0xFF) + ((int)(endColor & 0xFF) - (int)(startColor & 0xFF)) * endPercent)
		       | ((uint)(((startColor >> 8) & 0xFF) +
		                 ((int)((endColor >> 8) & 0xFF) - (int)((startColor >> 8) & 0xFF)) * endPercent) << 8)
		       | ((uint)(((startColor >> 16) & 0xFF) +
		                 ((int)((endColor >> 16) & 0xFF) - (int)((startColor >> 16) & 0xFF)) * endPercent) << 16)
		       | ((uint)(((startColor >> 24) & 0xFF) +
		                 ((int)((endColor >> 24) & 0xFF) - (int)((startColor >> 24) & 0xFF)) * endPercent) << 24);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static uint ColorRGBAInterpolateBGR(uint startColor, uint endColor, float endPercent)
	{
		return (uint)((startColor & 0xFF) + ((int)(endColor & 0xFF) - (int)(startColor & 0xFF)) * endPercent)
		       | ((uint)(((startColor >> 8) & 0xFF) +
		                 ((int)((endColor >> 8) & 0xFF) - (int)((startColor >> 8) & 0xFF)) * endPercent) << 8)
		       | ((uint)(((startColor >> 16) & 0xFF) +
		                 ((int)((endColor >> 16) & 0xFF) - (int)((startColor >> 16) & 0xFF)) * endPercent) << 16)
		       | (endColor & 0xFF000000);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static uint ColorRGBAMultiply(uint color, float multiplier)
	{
		return (uint)Math.Min((color & 0xFF) * multiplier, byte.MaxValue)
		       | ((uint)Math.Min(((color >> 8) & 0xFF) * multiplier, byte.MaxValue) << 8)
		       | ((uint)Math.Min(((color >> 16) & 0xFF) * multiplier, byte.MaxValue) << 16)
		       | (color & 0xFF000000);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static ushort ToBGR565(float r, float g, float b)
	{
		return (ushort)(((ushort)(r * 31) << 11) + ((ushort)(g * 63) << 5) + (ushort)(b * 31));
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static ushort ToBGR565(Color c)
	{
		return ToBGR565((float)c.R / byte.MaxValue, (float)c.G / byte.MaxValue, (float)c.B / byte.MaxValue);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static ushort ToBGR565(uint rgba)
	{
		return ToBGR565(
			(byte)((rgba & 0x00FF0000) >> 16) / (float)byte.MaxValue,
			(byte)((rgba & 0x0000FF00) >> 8) / (float)byte.MaxValue,
			(byte)(rgba & 0x000000FF) / (float)byte.MaxValue);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static uint ToRGBA(float r, float g, float b, float a)
	{
		return ((uint)(byte)(a * byte.MaxValue) << 24)
		       + ((uint)(byte)(b * byte.MaxValue) << 16)
		       + ((uint)(byte)(g * byte.MaxValue) << 8)
		       + (byte)(r * byte.MaxValue);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static (float, float, float, float) ToFloats(uint rgba)
	{
		return ((byte)(rgba & 0x000000FF) / (float)byte.MaxValue,
			(byte)((rgba & 0x0000FF00) >> 8) / (float)byte.MaxValue,
			(byte)((rgba & 0x00FF0000) >> 16) / (float)byte.MaxValue,
			(byte)((rgba & 0xFF000000) >> 24) / (float)byte.MaxValue);
	}

	public static uint GetColorForDifficultyType(SMCommon.ChartDifficultyType difficulty)
	{
		switch (difficulty)
		{
			case SMCommon.ChartDifficultyType.Beginner: return UIDifficultyBeginnerColorRGBA;
			case SMCommon.ChartDifficultyType.Easy: return UIDifficultyEasyColorRGBA;
			case SMCommon.ChartDifficultyType.Medium: return UIDifficultyMediumColorRGBA;
			case SMCommon.ChartDifficultyType.Hard: return UIDifficultyHardColorRGBA;
			case SMCommon.ChartDifficultyType.Challenge: return UIDifficultyChallengeColorRGBA;
			case SMCommon.ChartDifficultyType.Edit: return UIDifficultyEditColorRGBA;
		}

		return UIDifficultyEditColorRGBA;
	}

	#endregion Color Helpers

	#region File Open Helpers

	public static string FileOpenFilterForImages(string name, bool includeAllFiles)
	{
		var extenstionTypes = new List<string[]> { ExpectedImageFormats };
		return FileOpenFilter(name, extenstionTypes, includeAllFiles);
	}

	public static string FileOpenFilterForImagesAndVideos(string name, bool includeAllFiles)
	{
		var extenstionTypes = new List<string[]> { ExpectedImageFormats, ExpectedVideoFormats };
		return FileOpenFilter(name, extenstionTypes, includeAllFiles);
	}

	public static string FileOpenFilterForAudio(string name, bool includeAllFiles)
	{
		var extenstionTypes = new List<string[]> { ExpectedAudioFormats };
		return FileOpenFilter(name, extenstionTypes, includeAllFiles);
	}

	public static string FileOpenFilterForVideo(string name, bool includeAllFiles)
	{
		var extenstionTypes = new List<string[]> { ExpectedVideoFormats };
		return FileOpenFilter(name, extenstionTypes, includeAllFiles);
	}

	public static string FileOpenFilterForLyrics(string name, bool includeAllFiles)
	{
		var extenstionTypes = new List<string[]> { ExpectedLyricsFormats };
		return FileOpenFilter(name, extenstionTypes, includeAllFiles);
	}

	private static string FileOpenFilter(string name, List<string[]> extensionTypes, bool includeAllFiles)
	{
		var sb = new StringBuilder();
		sb.Append(name);
		sb.Append(" Files (");
		var first = true;
		foreach (var extensions in extensionTypes)
		{
			foreach (var extension in extensions)
			{
				if (!first)
					sb.Append(",");
				sb.Append("*.");
				sb.Append(extension);
				first = false;
			}
		}

		sb.Append(")|");
		first = true;
		foreach (var extensions in extensionTypes)
		{
			foreach (var extension in extensions)
			{
				if (!first)
					sb.Append(";");
				sb.Append("*.");
				sb.Append(extension);
				first = false;
			}
		}

		if (includeAllFiles)
		{
			sb.Append("|All files (*.*)|*.*");
		}

		return sb.ToString();
	}

	public static string BrowseFile(string name, string initialDirectory, string currentFileRelativePath, string filter)
	{
		string relativePath = null;
		using var openFileDialog = new OpenFileDialog();
		var startInitialDirectory = initialDirectory;
		if (!string.IsNullOrEmpty(currentFileRelativePath))
		{
			initialDirectory = Path.Combine(initialDirectory, currentFileRelativePath);
			initialDirectory = System.IO.Path.GetDirectoryName(initialDirectory);
		}

		openFileDialog.InitialDirectory = initialDirectory ?? "";
		openFileDialog.Filter = filter;
		openFileDialog.FilterIndex = 1;
		openFileDialog.Title = $"Open {name} File";

		if (openFileDialog.ShowDialog() == DialogResult.OK)
		{
			var fileName = openFileDialog.FileName;
			relativePath = Path.GetRelativePath(startInitialDirectory, fileName);
		}

		return relativePath;
	}

	#endregion File Open Helpers

	#region Application Focus

	public static bool IsApplicationFocused()
	{
		var activatedHandle = GetForegroundWindow();
		if (activatedHandle == IntPtr.Zero)
			return false;

		GetWindowThreadProcessId(activatedHandle, out var activeProcId);
		return activeProcId == Process.GetCurrentProcess().Id;
	}

	[DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
	private static extern IntPtr GetForegroundWindow();

	[DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
	private static extern int GetWindowThreadProcessId(IntPtr handle, out int processId);

	#endregion Application Focus

	#region Reflection

	public static void SetFieldOrPropertyToValue<T>(object o, string fieldOrPropertyName, T value)
	{
		if (IsField(o, fieldOrPropertyName))
			o.GetType().GetField(fieldOrPropertyName, BindingFlags.Public | BindingFlags.Instance)?.SetValue(o, value);
		else
			o.GetType().GetProperty(fieldOrPropertyName, BindingFlags.Public | BindingFlags.Instance)?.SetValue(o, value);
	}

	public static T GetValueFromFieldOrProperty<T>(object o, string fieldOrPropertyName)
	{
		var value = default(T);
		if (o == null)
			return value;

		var isField = IsField(o, fieldOrPropertyName);
		if (isField)
			value = (T)(o.GetType().GetField(fieldOrPropertyName)?.GetValue(o) ?? value);
		else
			value = (T)(o.GetType().GetProperty(fieldOrPropertyName)?.GetValue(o) ?? value);
		return value;
	}

	public static bool IsField(object o, string fieldOrPropertyName)
	{
		return o.GetType().GetField(fieldOrPropertyName, BindingFlags.Public | BindingFlags.Instance) != null;
	}

	#endregion Reflection
}

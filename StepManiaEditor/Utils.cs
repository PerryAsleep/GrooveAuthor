using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Fumen;
using Fumen.Converters;
using ImGuiNET;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace StepManiaEditor;

internal sealed class Utils
{
	// TODO: Rename / Reorganize. Currently dumping a lot of rendering-related constants in here.

	public const int MaxLogFiles = 10;
	public const string CustomSavePropertyPrefix = "GA";

	public const ImGuiWindowFlags ChartAreaChildWindowFlags = ImGuiWindowFlags.NoMove
	                                                          | ImGuiWindowFlags.NoDecoration
	                                                          | ImGuiWindowFlags.NoSavedSettings
	                                                          | ImGuiWindowFlags.NoDocking
	                                                          | ImGuiWindowFlags.NoBringToFrontOnFocus
	                                                          | ImGuiWindowFlags.NoFocusOnAppearing
	                                                          | ImGuiWindowFlags.NoScrollWithMouse;

	public const uint UIWindowColor = 0xFF0A0A0A;
	public const uint UITableRowBgActiveChartColor = 0x20FFFFFF;
	public const uint UINonDedicatedTabTextColor = 0xFFE0E0E0;
	public const uint UIFocusedTabBorderColor = 0xFFFFFFFF;
	public const uint UIUnfocusedTabBorderColor = 0xFF808080;
	public const float UIFocusedChartColorMultiplier = 1.25f;
	public const float UIUnfocusedChartColorMultiplier = 0.75f;

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
	public const uint UIAttackColorRGBA = 0x8A297A3F; // olive

	public const uint UIPatternColorRGBA = 0x8A6A7A29; // teal

	public const uint UIPreviewColorRGBA = 0x8A7A7A7A; // grey
	public const uint UILastSecondHintColorRGBA = 0x8A202020; // dark grey

	public const uint UIDifficultyBeginnerColorRGBA = 0xFF808040;
	public const uint UIDifficultyEasyColorRGBA = 0xFF4D804D;
	public const uint UIDifficultyMediumColorRGBA = 0xFF408080;
	public const uint UIDifficultyHardColorRGBA = 0xFF404080;
	public const uint UIDifficultyChallengeColorRGBA = 0xFF804080;
	public const uint UIDifficultyEditColorRGBA = 0xFF807D7B;

	public const uint UIFrameErrorColor = 0x8A29297A;

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

	public const string TextureIdMeasureMarker = "measure-marker";
	public const string TextureIdBeatMarker = "beat-marker";
	public const string TextureIdFocusedChartBoundary = "focused-chart-boundary";
	public const string TextureIdUnfocusedChartBoundary = "unfocused-chart-boundary";
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

	public static readonly string[] ExpectedAudioFormats = ["mp3", "oga", "ogg", "wav"];
	public static readonly string[] ExpectedImageFormats = ["bmp", "gif", "jpeg", "jpg", "png", "tif", "tiff", "webp"];

	public static readonly string[] ExpectedVideoFormats =
		["avi", "f4v", "flv", "mkv", "mp4", "mpeg", "mpg", "mov", "ogv", "webm", "wmv"];

	public static readonly string[] ExpectedLyricsFormats = ["lrc"];

	private static string AppName;
	private static Version AppVersion;
	private static Version AppLatestVersion;

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
		return AppName ??= Assembly.GetEntryAssembly()?.GetName().Name;
	}

	public static Version GetAppVersion()
	{
		if (AppVersion == null)
		{
			var assemblyVersion = Assembly.GetEntryAssembly()?.GetName().Version;
			if (assemblyVersion != null)
				AppVersion = new Version(assemblyVersion.Major, assemblyVersion.Minor, assemblyVersion.Build);
		}

		return AppVersion;
	}

	public static Version GetAppLatestVersion()
	{
		return AppLatestVersion;
	}

	public static async Task RefreshLatestVersion()
	{
		AppLatestVersion = await GetLatestVersionAsync();
	}

	private static async Task<Version> GetLatestVersionAsync()
	{
		try
		{
			HttpClient client = new();
			using var response =
				await client.GetAsync($"{Documentation.GitHubUrl}/releases/latest");
			var path = response?.RequestMessage?.RequestUri?.AbsolutePath;
			if (string.IsNullOrEmpty(path))
				return null;
			var index = path.LastIndexOf('/');
			if (index < 0)
				return null;
			// Add 2 to the index to account for "/v".
			index += 2;
			if (index >= path.Length)
				return null;
			var versionString = path.Substring(index);
			if (Version.TryParse(versionString, out var version))
				return version;
		}
		catch (Exception e)
		{
			Logger.Warn($"Failed to check for latest version. {e}");
		}

		return null;
	}

	public static string GetPrettyVersion(Version v)
	{
		return $"v{v.Major}.{v.Minor}.{v.Build}";
	}

	public static string GetLatestVersionUrl()
	{
		var version = AppLatestVersion ?? GetAppVersion();
		if (version == null)
			return null;
		return $"{Documentation.GitHubUrl}/releases/tag/{GetPrettyVersion(version)}";
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
	/// <param name="withAutogenFeatures">If true then the chart must support autogen features.</param>
	public static void EnsureChartReferencesValidChartFromActiveSong(ref EditorChart chart, Editor editor,
		bool withAutogenFeatures)
	{
		var song = editor.GetActiveSong();

		// If the chart is set to a valid Chart, ensure that Chart still exists.
		// If the Chart does not exist, set the chart to null.
		if (chart != null)
		{
			if (withAutogenFeatures && !chart.SupportsAutogenFeatures())
			{
				chart = null;
			}
			else if (song == null)
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
			// Use the focused Chart, if one exists.
			chart = editor.GetFocusedChart();
			if (chart != null)
			{
				if (withAutogenFeatures && !chart.SupportsAutogenFeatures())
					chart = null;
				else
					return;
			}

			// Failing that use, use any Chart from the active Song.
			if (song != null)
			{
				var charts = song.GetCharts();
				if (charts != null)
				{
					foreach (var existingChart in charts)
					{
						if (withAutogenFeatures)
						{
							if (existingChart.SupportsAutogenFeatures())
							{
								chart = existingChart;
								break;
							}
						}
						else
						{
							chart = existingChart;
							break;
						}
					}
				}
			}
		}
	}

	public static int GetBeatSubdivision(SubdivisionType subdivisionType)
	{
		return SMCommon.ValidDenominators[(int)subdivisionType];
	}

	public static int GetMeasureSubdivision(SubdivisionType subdivisionType)
	{
		return GetBeatSubdivision(subdivisionType) * SMCommon.NumBeatsPerMeasure;
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

	public static List<string[]> GetExtensionsForImages()
	{
		return [ExpectedImageFormats];
	}

	public static List<string[]> GetExtensionsForImagesAndVideos()
	{
		return [ExpectedImageFormats, ExpectedVideoFormats];
	}

	public static List<string[]> GetExtensionsForAudio()
	{
		return [ExpectedAudioFormats];
	}

	public static List<string[]> GetExtensionsForVideo()
	{
		return [ExpectedVideoFormats];
	}

	public static List<string[]> GetExtensionsForLyrics()
	{
		return [ExpectedLyricsFormats];
	}

	#endregion File Open Helpers

	#region FileComparer

	/// <summary>
	/// Comparer for comparing FileInfo objects by name.
	/// </summary>
	internal sealed class FileInfoNameComparer : IComparer<FileInfo>
	{
		public int Compare(FileInfo f1, FileInfo f2)
		{
			if (null == f1 && null == f2)
				return 0;
			if (null == f1)
				return 1;
			if (null == f2)
				return -1;
			return string.Compare(f1.Name, f2.Name, StringComparison.Ordinal);
		}
	}

	#endregion FileComparer

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

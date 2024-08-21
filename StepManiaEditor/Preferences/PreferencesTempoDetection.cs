using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Fumen;
using StepManiaLibrary;
using static StepManiaEditor.PreferencesTempoDetection;

namespace StepManiaEditor;

/// <summary>
/// Preferences for tempo detection.
/// </summary>
internal sealed class PreferencesTempoDetection
{
	// Default values.
	public const double DefaultMinTempo = 60.0;
	public const double DefaultMaxTempo = 200.0;
	public const double DefaultMinSeparationBetweenBestTempos = 5.0;
	public const int DefaultNumTemposToFind = 5;
	public const double DefaultWindowTime = 20.0;
	public const double DefaultCombFilterResolution = 0.1;
	public const double DefaultCombFilterBeats = 4.0;
	public const double DefaultEnvelopeAttackPercent = 0.005;
	public const double DefaultEnvelopeReleasePercent = 0.05;

	public static readonly List<TempoDetector.FrequencyBand> DefaultFrequencyBands = new()
	{
		new TempoDetector.FrequencyBand(0, 200, 100),
		new TempoDetector.FrequencyBand(200, 400, 100),
		new TempoDetector.FrequencyBand(400, 800, 50),
		new TempoDetector.FrequencyBand(800, 1600, 50),
		new TempoDetector.FrequencyBand(1600, 3200, 50),
		new TempoDetector.FrequencyBand(3200, 20000, 100),
	};

	public static readonly List<TempoDetector.Location> DefaultMeasurementLocations = new()
	{
		new TempoDetector.Location(TempoDetector.LocationType.RelativeToStart, 20.0, 0.5),
		new TempoDetector.Location(TempoDetector.LocationType.Percentage, 0.0, 0.5),
		new TempoDetector.Location(TempoDetector.LocationType.RelativeToEnd, 20.0, 0.5),
	};

	public const bool DefaultWriteDebugWavs = false;

	[JsonInclude] public double MinTempo = DefaultMinTempo;
	[JsonInclude] public double MaxTempo = DefaultMaxTempo;
	[JsonInclude] public int NumTemposToFind = DefaultNumTemposToFind;
	[JsonInclude] public double MinSeparationBetweenBestTempos = DefaultMinSeparationBetweenBestTempos;
	[JsonInclude] public double WindowTime = DefaultWindowTime;
	[JsonInclude] public double CombFilterResolution = DefaultCombFilterResolution;
	[JsonInclude] public double CombFilterBeats = DefaultCombFilterBeats;
	[JsonInclude] public double EnvelopeAttackPercent = DefaultEnvelopeAttackPercent;
	[JsonInclude] public double EnvelopeReleasePercent = DefaultEnvelopeReleasePercent;
	[JsonInclude] public List<TempoDetector.FrequencyBand> FrequencyBands;
	[JsonInclude] public List<TempoDetector.Location> MeasurementLocations;
	[JsonInclude] public bool WriteDebugWavs = DefaultWriteDebugWavs;

	public void PostLoad()
	{
		if (FrequencyBands == null || FrequencyBands.Count == 0)
		{
			FrequencyBands = new List<TempoDetector.FrequencyBand>(DefaultFrequencyBands.Count);
			foreach (var defaultBand in DefaultFrequencyBands)
			{
				FrequencyBands.Add(new TempoDetector.FrequencyBand(defaultBand));
			}
		}

		if (MeasurementLocations == null || MeasurementLocations.Count == 0)
		{
			MeasurementLocations = new List<TempoDetector.Location>(DefaultMeasurementLocations.Count);
			foreach (var defaultMeasurementLocation in DefaultMeasurementLocations)
			{
				MeasurementLocations.Add(new TempoDetector.Location(defaultMeasurementLocation));
			}
		}
	}

	public bool IsUsingDefaults()
	{
		if (!(MinTempo.DoubleEquals(DefaultMinTempo)
		      && MaxTempo.DoubleEquals(DefaultMaxTempo)
		      && NumTemposToFind == DefaultNumTemposToFind
		      && MinSeparationBetweenBestTempos.DoubleEquals(DefaultMinSeparationBetweenBestTempos)
		      && WindowTime.DoubleEquals(DefaultWindowTime)
		      && CombFilterResolution.DoubleEquals(DefaultCombFilterResolution)
		      && CombFilterBeats.DoubleEquals(DefaultCombFilterBeats)
		      && EnvelopeAttackPercent.DoubleEquals(DefaultEnvelopeAttackPercent)
		      && EnvelopeReleasePercent.DoubleEquals(DefaultEnvelopeReleasePercent)
		      && WriteDebugWavs == DefaultWriteDebugWavs))
			return false;

		if (FrequencyBands.Count != DefaultFrequencyBands.Count)
			return false;
		for (var i = 0; i < FrequencyBands.Count; i++)
			if (!FrequencyBands[i].Equals(DefaultFrequencyBands[i]))
				return false;
		if (MeasurementLocations.Count != DefaultMeasurementLocations.Count)
			return false;
		for (var i = 0; i < MeasurementLocations.Count; i++)
			if (!MeasurementLocations[i].Equals(DefaultMeasurementLocations[i]))
				return false;
		return true;
	}

	public void RestoreDefaults()
	{
		// Don't enqueue an action if it would not have any effect.
		if (IsUsingDefaults())
			return;
		ActionQueue.Instance.Do(new ActionRestoreTempoDetectionPreferenceDefaults());
	}

	public TempoDetector.Settings CreateSettings(int numChannels, int sampleRate)
	{
		var settings = new TempoDetector.Settings
		{
			MinTempo = MinTempo,
			MaxTempo = MaxTempo,
			NumTemposToFind = NumTemposToFind,
			MinSeparationBetweenBestTempos = MinSeparationBetweenBestTempos,
			WindowTime = WindowTime,
			CombFilterResolution = CombFilterResolution,
			CombFilterBeats = CombFilterBeats,
			NumChannels = numChannels,
			SampleRate = sampleRate,
		};

		settings.SetFrequencyBands(FrequencyBands);
		settings.SetLocations(MeasurementLocations);

		if (WriteDebugWavs)
			settings.SetWriteDebugWavFiles(DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss"));

		// TODO: parameterize this
		settings.SetShouldLog(null);

		return settings;
	}
}

/// <summary>
/// Action to restore tempo detection preferences to their default values.
/// </summary>
internal sealed class ActionRestoreTempoDetectionPreferenceDefaults : EditorAction
{
	private readonly double PreviousMinTempo;
	private readonly double PreviousMaxTempo;
	private readonly int PreviousNumTemposToFind;
	private readonly double PreviousMinSeparationBetweenBestTempos;
	private readonly double PreviousWindowTime;
	private readonly double PreviousCombFilterResolution;
	private readonly double PreviousCombFilterBeats;
	private readonly double PreviousEnvelopeAttackPercent;
	private readonly double PreviousEnvelopeReleasePercent;
	private readonly List<TempoDetector.FrequencyBand> PreviousFrequencyBands;
	private readonly List<TempoDetector.Location> PreviousMeasurementLocations;
	private readonly bool PreviousWriteDebugWavs;

	public ActionRestoreTempoDetectionPreferenceDefaults() : base(false, false)
	{
		var p = Preferences.Instance.PreferencesTempoDetection;
		PreviousMinTempo = p.MinTempo;
		PreviousMaxTempo = p.MaxTempo;
		PreviousNumTemposToFind = p.NumTemposToFind;
		PreviousMinSeparationBetweenBestTempos = p.MinSeparationBetweenBestTempos;
		PreviousWindowTime = p.WindowTime;
		PreviousCombFilterResolution = p.CombFilterResolution;
		PreviousCombFilterBeats = p.CombFilterBeats;
		PreviousEnvelopeAttackPercent = p.EnvelopeAttackPercent;
		PreviousEnvelopeReleasePercent = p.EnvelopeReleasePercent;
		PreviousFrequencyBands = new List<TempoDetector.FrequencyBand>(p.FrequencyBands.Count);
		foreach (var f in p.FrequencyBands)
			PreviousFrequencyBands.Add(new TempoDetector.FrequencyBand(f));
		PreviousMeasurementLocations = new List<TempoDetector.Location>(p.MeasurementLocations.Count);
		foreach (var l in p.MeasurementLocations)
			PreviousMeasurementLocations.Add(new TempoDetector.Location(l));
		PreviousWriteDebugWavs = p.WriteDebugWavs;
	}

	public override bool AffectsFile()
	{
		return false;
	}

	public override string ToString()
	{
		return "Restore Tempo Detection Preferences to default values.";
	}

	protected override void DoImplementation()
	{
		var p = Preferences.Instance.PreferencesTempoDetection;
		p.MinTempo = DefaultMinTempo;
		p.MaxTempo = DefaultMaxTempo;
		p.NumTemposToFind = DefaultNumTemposToFind;
		p.MinSeparationBetweenBestTempos = DefaultMinSeparationBetweenBestTempos;
		p.WindowTime = DefaultWindowTime;
		p.CombFilterResolution = DefaultCombFilterResolution;
		p.CombFilterBeats = DefaultCombFilterBeats;
		p.EnvelopeAttackPercent = DefaultEnvelopeAttackPercent;
		p.EnvelopeReleasePercent = DefaultEnvelopeReleasePercent;
		p.FrequencyBands = new List<TempoDetector.FrequencyBand>(DefaultFrequencyBands.Count);
		foreach (var defaultBand in DefaultFrequencyBands)
			p.FrequencyBands.Add(new TempoDetector.FrequencyBand(defaultBand));
		p.MeasurementLocations = new List<TempoDetector.Location>(DefaultMeasurementLocations.Count);
		foreach (var defaultMeasurementLocation in DefaultMeasurementLocations)
			p.MeasurementLocations.Add(new TempoDetector.Location(defaultMeasurementLocation));
		p.WriteDebugWavs = DefaultWriteDebugWavs;
	}

	protected override void UndoImplementation()
	{
		var p = Preferences.Instance.PreferencesTempoDetection;
		p.MinTempo = PreviousMinTempo;
		p.MaxTempo = PreviousMaxTempo;
		p.NumTemposToFind = PreviousNumTemposToFind;
		p.MinSeparationBetweenBestTempos = PreviousMinSeparationBetweenBestTempos;
		p.WindowTime = PreviousWindowTime;
		p.CombFilterResolution = PreviousCombFilterResolution;
		p.CombFilterBeats = PreviousCombFilterBeats;
		p.EnvelopeAttackPercent = PreviousEnvelopeAttackPercent;
		p.EnvelopeReleasePercent = PreviousEnvelopeReleasePercent;
		p.FrequencyBands = new List<TempoDetector.FrequencyBand>(PreviousFrequencyBands.Count);
		foreach (var previousBand in PreviousFrequencyBands)
			p.FrequencyBands.Add(new TempoDetector.FrequencyBand(previousBand));
		p.MeasurementLocations = new List<TempoDetector.Location>(PreviousMeasurementLocations.Count);
		foreach (var previousMeasurementLocation in PreviousMeasurementLocations)
			p.MeasurementLocations.Add(new TempoDetector.Location(previousMeasurementLocation));
		p.WriteDebugWavs = PreviousWriteDebugWavs;
	}
}

using System.Text.Json.Serialization;
using static StepManiaEditor.PreferencesSelection;

namespace StepManiaEditor;

/// <summary>
/// Preferences for selecting notes.
/// </summary>
internal sealed class PreferencesSelection
{
	public enum SelectionMode
	{
		OverlapAny,
		OverlapCenter,
		OverlapAll,
	}

	public enum SelectionRegionMode
	{
		TimeOrPosition,
		TimeOrPositionAndLane,
	}

	// Default values.
	public const SelectionMode DefaultSelectionMode = SelectionMode.OverlapCenter;
	public const SelectionRegionMode DefaultSelectionRegionMode = SelectionRegionMode.TimeOrPosition;

	// Preferences.
	[JsonInclude] public bool ShowSelectionControlPreferencesWindow;
	[JsonInclude] public SelectionMode Mode = DefaultSelectionMode;
	[JsonInclude] public SelectionRegionMode RegionMode = DefaultSelectionRegionMode;

	public bool IsUsingDefaults()
	{
		return Mode == DefaultSelectionMode
		       && RegionMode == DefaultSelectionRegionMode;
	}

	public void RestoreDefaults()
	{
		// Don't enqueue an action if it would not have any effect.
		if (IsUsingDefaults())
			return;
		ActionQueue.Instance.Do(new ActionRestoreSelectionPreferenceDefaults());
	}
}

/// <summary>
/// Action to restore selection preferences to their default values.
/// </summary>
internal sealed class ActionRestoreSelectionPreferenceDefaults : EditorAction
{
	private readonly SelectionMode PreviousMode;
	private readonly SelectionRegionMode PreviousRegionMode;

	public ActionRestoreSelectionPreferenceDefaults() : base(false, false)
	{
		var p = Preferences.Instance.PreferencesSelection;
		PreviousMode = p.Mode;
		PreviousRegionMode = p.RegionMode;
	}

	public override bool AffectsFile()
	{
		return false;
	}

	public override string ToString()
	{
		return "Restore selection default preferences.";
	}

	protected override void DoImplementation()
	{
		var p = Preferences.Instance.PreferencesSelection;
		p.Mode = DefaultSelectionMode;
		p.RegionMode = DefaultSelectionRegionMode;
	}

	protected override void UndoImplementation()
	{
		var p = Preferences.Instance.PreferencesSelection;
		p.Mode = PreviousMode;
		p.RegionMode = PreviousRegionMode;
	}
}

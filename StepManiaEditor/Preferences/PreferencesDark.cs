using System.Numerics;
using System.Text.Json.Serialization;
using Fumen;
using static StepManiaEditor.PreferencesDark;

namespace StepManiaEditor;

/// <summary>
/// Preferences for the Dark background.
/// </summary>
internal sealed class PreferencesDark
{
	public enum SizeMode
	{
		Charts,
		Window,
	}

	public enum DrawOrder
	{
		AfterBackground,
		AfterWaveForm,
	}

	// Default values.
	public const bool DefaultShowDarkBg = false;
	public const SizeMode DefaultSize = SizeMode.Window;
	public const DrawOrder DefaultDrawOrder = DrawOrder.AfterBackground;
	public static readonly Vector4 DefaultColor = new(0.0f, 0.0f, 0.0f, 0.8f);

	// Preferences.
	[JsonInclude] public bool ShowDarkPreferencesWindow;
	[JsonInclude] public bool ShowDarkBg = DefaultShowDarkBg;
	[JsonInclude] public SizeMode Size = DefaultSize;
	[JsonInclude] public DrawOrder DarkBgDrawOrder = DefaultDrawOrder;
	[JsonInclude] public Vector4 Color = DefaultColor;

	public static void RegisterDefaultsForInvalidEnumValues(PermissiveEnumJsonConverterFactory factory)
	{
		factory.RegisterDefault(DefaultSize);
		factory.RegisterDefault(DefaultDrawOrder);
	}

	public bool IsUsingDefaults()
	{
		return ShowDarkBg == DefaultShowDarkBg
		       && Size == DefaultSize
		       && DarkBgDrawOrder == DefaultDrawOrder
		       && Color.Equals(DefaultColor);
	}

	public void RestoreDefaults()
	{
		// Don't enqueue an action if it would not have any effect.
		if (IsUsingDefaults())
			return;
		ActionQueue.Instance.Do(new ActionRestoreDarkBgPreferenceDefaults());
	}
}

/// <summary>
/// Action to restore Dark background preferences to their default values.
/// </summary>
internal sealed class ActionRestoreDarkBgPreferenceDefaults : EditorAction
{
	private readonly bool PreviousShowDarkBg;
	private readonly SizeMode PreviousSize;
	private readonly DrawOrder PreviousDarkBgDrawOrder;
	private readonly Vector4 PreviousColor;

	public ActionRestoreDarkBgPreferenceDefaults() : base(false, false)
	{
		var p = Preferences.Instance.PreferencesDark;
		PreviousShowDarkBg = p.ShowDarkBg;
		PreviousSize = p.Size;
		PreviousDarkBgDrawOrder = p.DarkBgDrawOrder;
		PreviousColor = p.Color;
	}

	public override bool AffectsFile()
	{
		return false;
	}

	public override string ToString()
	{
		return "Restore Dark Preferences to default values.";
	}

	protected override void DoImplementation()
	{
		var p = Preferences.Instance.PreferencesDark;
		p.ShowDarkBg = DefaultShowDarkBg;
		p.Size = DefaultSize;
		p.DarkBgDrawOrder = DefaultDrawOrder;
		p.Color = DefaultColor;
	}

	protected override void UndoImplementation()
	{
		var p = Preferences.Instance.PreferencesDark;
		p.ShowDarkBg = PreviousShowDarkBg;
		p.Size = PreviousSize;
		p.DarkBgDrawOrder = PreviousDarkBgDrawOrder;
		p.Color = PreviousColor;
	}
}

using System.Collections.Generic;
using System.Reflection;
using Microsoft.Xna.Framework.Input;

namespace StepManiaEditor;

/// <summary>
/// Action for updating key bindings.
/// Clones entire key binding arrays for simplicity because they are small.
/// </summary>
internal sealed class ActionUpdateKeyBinding : EditorAction
{
	private readonly string Name;
	private readonly List<Keys[]> NewValue;
	private readonly List<Keys[]> PreviousValue;
	private readonly PropertyInfo PropertyInfo;

	/// <summary>
	/// Constructor.
	/// </summary>
	/// <param name="id">Keybind id.</param>
	/// <param name="name">Key binding action name for display.</param>
	/// <param name="value">New key binding to set.</param>
	public ActionUpdateKeyBinding(string id, string name, List<Keys[]> value) : base(false, false)
	{
		var p = Preferences.Instance.PreferencesKeyBinds;

		NewValue = PreferencesKeyBinds.CloneKeyBinding(value);
		Name = name;
		PropertyInfo = p.GetType().GetProperty(id, BindingFlags.Public | BindingFlags.Instance);
		PreviousValue = p.CloneKeyBinding(id);
	}

	public override bool AffectsFile()
	{
		return false;
	}

	public override string ToString()
	{
		return $"Update {Name} key binding.";
	}

	protected override void DoImplementation()
	{
		PropertyInfo.SetValue(Preferences.Instance.PreferencesKeyBinds, PreferencesKeyBinds.CloneKeyBinding(NewValue));
	}

	protected override void UndoImplementation()
	{
		PropertyInfo.SetValue(Preferences.Instance.PreferencesKeyBinds, PreferencesKeyBinds.CloneKeyBinding(PreviousValue));
	}
}

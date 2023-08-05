using System;
using System.Text.Json.Serialization;

namespace StepManiaEditor.AutogenConfig;

/// <summary>
/// Interface for configurations managed through ConfigManager.
/// JsonDerivedType attributes are present to support derived type serialization.
/// </summary>
[JsonDerivedType(typeof(EditorExpressedChartConfig))]
[JsonDerivedType(typeof(EditorPerformedChartConfig))]
internal interface IEditorConfig
{
	/// <summary>
	/// Gets the Guid for this configuration.
	/// Guids are used as the primary identifiers.
	/// </summary>
	/// <returns>Guid for this configuration.</returns>
	public Guid GetGuid();

	/// <summary>
	/// Gets the name of this configuration.
	/// Names are editable strings for easy identification.
	/// Names do not need to be unique.
	/// </summary>
	/// <returns>Name of this configuration.</returns>
	public string GetName();

	/// <summary>
	/// Returns whether or not this configuration is a default configuration.
	/// Default configurations cannot be edited.
	/// </summary>
	/// <returns>
	/// True if this configuration is a default configuration and false otherwise.
	/// </returns>
	public bool IsDefault();

	/// <summary>
	/// Clones this configuration using a deep copy and returns the copy.
	/// The type of the configuration returned is the same as the type being cloned.
	/// </summary>
	/// <returns>Newly cloned configuration.</returns>
	public IEditorConfig Clone();

	/// <summary>
	/// Initialize this configuration with reasonable default values for its type.
	/// </summary>
	public void InitializeWithDefaultValues();

	/// <summary>
	/// Sets this configuration to consider its current state as the saved state for determining
	/// if unsaved changes are present.
	/// </summary>
	public void UpdateLastSavedState();

	/// <summary>
	/// Returns whether or not this configuration has unsaved changes.
	/// </summary>
	/// <returns>True if this configuration has unsaved changes and false otherwise.</returns>
	public bool HasUnsavedChanges();
}

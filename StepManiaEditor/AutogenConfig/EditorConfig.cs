using System;
using System.Text.Json.Serialization;
using Fumen;
using StepManiaLibrary;

namespace StepManiaEditor.AutogenConfig;

/// <summary>
/// Editor-specific configuration data for various autogen behaviors.
/// EditorConfig objects wrap StepManiaLibrary IConfig objects, with additional data
/// and functionality for the editor.
/// EditorConfig objects have a Guid, name, and description.
/// Instances of this class are managed through ConfigManager.
/// JsonDerivedType attributes are present to support derived type serialization.
/// </summary>
/// <typeparam name="TConfig">
/// Type of configuration objects implementing the IConfig interface that are
/// wrapped by this class.
/// </typeparam>
[JsonDerivedType(typeof(EditorExpressedChartConfig))]
[JsonDerivedType(typeof(EditorPerformedChartConfig))]
[JsonDerivedType(typeof(EditorPatternConfig))]
internal abstract class EditorConfig<TConfig> where TConfig : IConfig<TConfig>, new()
{
	public const string NewConfigName = "New Config";

	/// <summary>
	/// Guid for this EditorConfig.
	/// Not readonly so that it can be set from deserialization.
	/// </summary>
	[JsonInclude] public Guid Guid;

	[JsonInclude]
	public string Name
	{
		get => NameInternal;
		set
		{
			if (!string.IsNullOrEmpty(NameInternal) && NameInternal.Equals(value))
				return;
			NameInternal = value;
			// Null check around OnNameUpdated because this property is set during deserialization.
			OnNameUpdated?.Invoke();
		}
	}

	private string NameInternal;

	[JsonInclude] public string Description;

	/// <summary>
	/// StepManiaLibrary IConfig object wrapped by this class.
	/// </summary>
	[JsonInclude] public TConfig Config = new();

	/// <summary>
	/// A cloned EditorConfig to use for comparisons to see if this EditorConfig has
	/// unsaved changes or not.
	/// </summary>
	private EditorConfig<TConfig> LastSavedState;

	/// <summary>
	/// Callback function to invoke when the name is updated.
	/// </summary>
	private Action OnNameUpdated;

	/// <summary>
	/// Constructor.
	/// </summary>
	protected EditorConfig()
	{
		Guid = Guid.NewGuid();
	}

	/// <summary>
	/// Constructor taking a previously generated Guid.
	/// </summary>
	/// <param name="guid">Guid for this EditorConfig.</param>
	protected EditorConfig(Guid guid)
	{
		Guid = guid;
	}

	/// <summary>
	/// Gets the Guid for this configuration.
	/// Guids are used as the primary identifiers.
	/// </summary>
	/// <returns>Guid for this configuration.</returns>
	public Guid GetGuid()
	{
		return Guid;
	}

	/// <summary>
	/// Validates this EditorConfig and logs any errors on invalid data.
	/// </summary>
	/// <returns>True if no errors were found and false otherwise.</returns>
	public bool Validate()
	{
		var errors = false;
		if (Guid == Guid.Empty)
		{
			Logger.Error("Config has no Guid.");
			errors = true;
		}

		if (string.IsNullOrEmpty(Name))
		{
			Logger.Error($"Config {Guid} has no name.");
			errors = true;
		}

		errors = !Config.Validate(Name) || errors;
		return !errors;
	}

	/// <summary>
	/// Performs any post-load initialization on the StepManiaLibrary IConfig object managed
	/// by this object.
	/// </summary>
	public void Init()
	{
		Config.Init();
	}

	/// <summary>
	/// Sets function to use for calling back to when the name is updated.
	/// </summary>
	/// <param name="onNameUpdated">Callback function to invoke when the name is updated.</param>
	public void SetNameUpdatedFunction(Action onNameUpdated)
	{
		OnNameUpdated = onNameUpdated;
	}

	/// <summary>
	/// Returns a new EditorConfig that is a clone of this EditorConfig.
	/// </summary>
	/// <param name="snapshot">
	/// If true then everything on this EditorConfig will be cloned.
	/// If false then the Guid and Name will be changed.
	/// </param>
	/// <returns>Cloned EditorConfig.</returns>
	private EditorConfig<TConfig> CloneInternal(bool snapshot)
	{
		// Let the derived class instantiate a new EditorConfig object of the expected type
		// and perform any needed cloning.
		var clone = CloneImplementation(snapshot);

		// Clone base EditorConfig values.
		clone.Config = Config.Clone();
		clone.Name = snapshot ? Name : NewConfigName;
		clone.Description = Description;
		clone.OnNameUpdated = OnNameUpdated;
		return clone;
	}

	/// <summary>
	/// Clones this EditorConfig using a deep copy and returns the copy.
	/// </summary>
	/// <returns>Newly cloned EditorConfig.</returns>
	public EditorConfig<TConfig> Clone()
	{
		return CloneInternal(false);
	}

	/// <summary>
	/// Returns whether this EditorConfig has unsaved changes.
	/// </summary>
	/// <returns>True if this EditorConfig has unsaved changes and false otherwise.</returns>
	public bool HasUnsavedChanges()
	{
		return LastSavedState == null || !EditorConfigEquals(LastSavedState);
	}

	/// <summary>
	/// Updates the last saved state to use for unsaved changes comparisons.
	/// </summary>
	public void UpdateLastSavedState()
	{
		LastSavedState = CloneInternal(true);
	}

	/// <summary>
	/// Returns whether or not this configuration is a default configuration.
	/// Default configurations cannot be edited.
	/// </summary>
	/// <returns>
	/// True if this configuration is a default configuration and false otherwise.
	/// </returns>
	public abstract bool IsDefault();

	/// <summary>
	/// Clone this EditorConfig.
	/// </summary>
	/// <param name="snapshot">
	/// If true then the clone should be a snapshot which maintains the Guid.
	/// </param>
	/// <returns>Cloned EditorConfig object.</returns>
	protected abstract EditorConfig<TConfig> CloneImplementation(bool snapshot);

	/// <summary>
	/// Initialize this EditorConfig with reasonable default values for its type.
	/// </summary>
	public abstract void InitializeWithDefaultValues();

	/// <summary>
	/// Equals method for derived classes to implement so this class can compare instances.
	/// </summary>
	/// <param name="other">Other EditorConfig to compare to.</param>
	/// <returns>True if other equals this and false otherwise.</returns>
	protected abstract bool EditorConfigEquals(EditorConfig<TConfig> other);
}

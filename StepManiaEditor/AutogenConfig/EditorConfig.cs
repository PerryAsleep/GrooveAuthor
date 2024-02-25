using System;
using System.Text.Json.Serialization;
using Fumen;
using StepManiaLibrary;

namespace StepManiaEditor.AutogenConfig;

/// <summary>
/// Editor-specific configuration data for various autogen behaviors.
/// EditorConfig objects wrap StepManiaLibrary Config objects, with additional data
/// and functionality for the editor.
/// EditorConfig objects have a Guid, name, and description.
/// Instances of this class are managed through ConfigManager.
/// JsonDerivedType attributes are present to support derived type serialization.
/// </summary>
/// <typeparam name="TConfig">
/// Type of StepManiaLibrary Config objects wrapped by this class.
/// </typeparam>
[JsonDerivedType(typeof(EditorExpressedChartConfig))]
[JsonDerivedType(typeof(EditorPerformedChartConfig))]
[JsonDerivedType(typeof(EditorPatternConfig))]
internal abstract class EditorConfig<TConfig> :
	Notifier<EditorConfig<TConfig>>,
	Fumen.IObserver<Config>
	where TConfig : Config, new()
{
	public const string NotificationNameChanged = "NameChanged";
	public const string ConfigChanged = "ConfigChanged";

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
			OnNameChanged();
			Notify(NotificationNameChanged, this);
		}
	}

	private string NameInternal;

	[JsonInclude] public string Description;

	/// <summary>
	/// StepManiaLibrary Config object wrapped by this class.
	/// </summary>
	[JsonInclude] public TConfig Config = new();

	/// <summary>
	/// A cloned EditorConfig to use for comparisons to see if this EditorConfig has
	/// unsaved changes or not.
	/// </summary>
	private EditorConfig<TConfig> LastSavedState;

	/// <summary>
	/// Whether or not this EditorConfig is a default configuration that cannot be edited.
	/// </summary>
	private readonly bool DefaultConfig;

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
	/// Constructor taking a previously generated Guid.
	/// </summary>
	/// <param name="guid">Guid for this EditorConfig.</param>
	/// <param name="isDefaultConfig">Whether or not this EditorConfig is a default configuration.</param>
	protected EditorConfig(Guid guid, bool isDefaultConfig)
	{
		Guid = guid;
		DefaultConfig = isDefaultConfig;
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

		errors = !Config.Validate(ToString()) || errors;
		return !errors;
	}

	/// <summary>
	/// Performs any post-load initialization on the StepManiaLibrary Config object managed
	/// by this object.
	/// </summary>
	public virtual void Init()
	{
		Config.Init();
		Config.AddObserver(this);
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
		clone.Config = (TConfig)Config.Clone();
		clone.Name = snapshot ? Name : GetNewConfigName();
		clone.Description = Description;
		clone.Init();
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
	public bool IsDefault()
	{
		return DefaultConfig;
	}

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

	/// <summary>
	/// Returns the name newly created EditorConfigs should use.
	/// </summary>
	/// <returns>The name newly created EditorConfigs should use.</returns>
	public virtual string GetNewConfigName()
	{
		return "New Config";
	}

	/// <summary>
	/// Returns whether or not the string representation of this EditorConfig should be
	/// rendered with color when possible. See also GetStringColor.
	/// </summary>
	/// <returns>
	/// True if the string representation of this EditorConfig should bre rendered with
	/// color when possible and false otherwise.
	/// </returns>
	public virtual bool ShouldUseColorForString()
	{
		return false;
	}

	/// <summary>
	/// The color of the string representation of this EditorConfig when it should be colored.
	/// See also ShouldUseColorForString.
	/// </summary>
	/// <returns>The color of the string representation of this EditorConfig.</returns>
	public virtual uint GetStringColor()
	{
		return 0;
	}

	/// <summary>
	/// Notification handler for underlying StepManiaLibrary Config object changes.
	/// </summary>
	public virtual void OnNotify(string eventId, Config notifier, object payload)
	{
		// Bubble up the notification to any Observers.
		Notify(ConfigChanged, this);
	}

	/// <summary>
	/// Returns the string representation of this EditorConfig.
	/// </summary>
	/// <returns>String representation of this EditorConfig.</returns>
	public override string ToString()
	{
		return Name;
	}

	/// <summary>
	/// Called when this EditorConfig's Name changes.
	/// </summary>
	protected virtual void OnNameChanged()
	{
	}
}

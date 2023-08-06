using System;
using System.Collections.Generic;
using StepManiaLibrary;

namespace StepManiaEditor.AutogenConfig;

/// <summary>
/// Data for all EditorConfig objects of the same type.
/// </summary>
/// <typeparam name="TEditorConfig">
/// Type of EditorConfig objects stored in this instance.
/// </typeparam>
/// <typeparam name="TConfig">
/// Type of configuration objects implementing the IConfig interface that are
/// wrapped by the EditorConfig objects that this class manages.
/// </typeparam>
internal sealed class ConfigData<TEditorConfig, TConfig>
	where TEditorConfig : EditorConfig<TConfig>
	where TConfig : IConfig<TConfig>, new()
{
	/// <summary>
	/// Sorted array of Config guids to use for UI.
	/// </summary>
	public Guid[] SortedConfigGuids;

	/// <summary>
	/// Sorted array of Config names to use for UI.
	/// </summary>
	public string[] SortedConfigNames;

	/// <summary>
	/// All IEditorConfigs of the same type.
	/// </summary>
	private readonly Dictionary<Guid, TEditorConfig> Configs = new();

	public void UpdateSortedConfigs()
	{
		var configList = new List<TEditorConfig>();
		foreach (var kvp in Configs)
		{
			configList.Add(kvp.Value);
		}

		configList.Sort((lhs, rhs) =>
		{
			// The default configs should be sorted first.
			var lhsDefault = lhs.IsDefault();
			var rhsDefault = rhs.IsDefault();
			if (lhsDefault != rhsDefault)
				return lhsDefault ? -1 : 1;

			// Configs should sort alphabetically.
			var comparison = string.Compare(lhs.Name, rhs.Name, StringComparison.CurrentCulture);
			if (comparison != 0)
				return comparison;

			// Finally sort by Guid.
			return lhs.GetGuid().CompareTo(rhs.GetGuid());
		});

		SortedConfigGuids = new Guid[configList.Count];
		SortedConfigNames = new string[configList.Count];
		for (var i = 0; i < configList.Count; i++)
		{
			SortedConfigGuids[i] = configList[i].GetGuid();
			SortedConfigNames[i] = configList[i].Name;
		}
	}

	public void AddConfig(TEditorConfig config)
	{
		Configs[config.GetGuid()] = config;
		UpdateSortedConfigs();
	}

	public void RemoveConfig(Guid guid)
	{
		if (!Configs.TryGetValue(guid, out var config))
			return;
		if (config.IsDefault())
			return;
		Configs.Remove(guid);
		UpdateSortedConfigs();
	}

	public TEditorConfig GetConfig(Guid guid)
	{
		if (!Configs.TryGetValue(guid, out var config))
			return default;
		return config;
	}

	public TEditorConfig CloneConfig(Guid guid)
	{
		var existingConfig = GetConfig(guid);
		return (TEditorConfig)existingConfig?.Clone();
	}

	public IReadOnlyDictionary<Guid, TEditorConfig> GetConfigs()
	{
		return Configs;
	}
}

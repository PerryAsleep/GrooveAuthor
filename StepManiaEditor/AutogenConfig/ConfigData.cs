using System;
using System.Collections.Generic;

namespace StepManiaEditor.AutogenConfig;

/// <summary>
/// Data for all IEditorConfig objects of the same type.
/// </summary>
/// <typeparam name="T">Type of IEditorConfig objects stored in this instance.</typeparam>
internal sealed class ConfigData<T> where T : IEditorConfig
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
	private readonly Dictionary<Guid, T> Configs = new();

	public void UpdateSortedConfigs()
	{
		var configList = new List<T>();
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
			var comparison = string.Compare(lhs.GetName(), rhs.GetName(), StringComparison.CurrentCulture);
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
			SortedConfigNames[i] = configList[i].GetName();
		}
	}

	public void AddConfig(T config)
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

	public T GetConfig(Guid guid)
	{
		if (!Configs.TryGetValue(guid, out var config))
			return default;
		return config;
	}

	public T CloneConfig(Guid guid)
	{
		var existingConfig = GetConfig(guid);
		return (T)existingConfig?.Clone();
	}

	public IReadOnlyDictionary<Guid, T> GetConfigs()
	{
		return Configs;
	}
}

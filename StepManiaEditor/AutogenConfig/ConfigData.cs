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
/// Type of StepManiaLibrary Config objects wrapped by the EditorConfig objects that this class manages.
/// </typeparam>
internal sealed class ConfigData<TEditorConfig, TConfig> :
	Fumen.IObserver<EditorConfig<TConfig>>
	where TEditorConfig : EditorConfig<TConfig>
	where TConfig : Config, new()
{
	/// <summary>
	/// Class to use by default for comparing TEditorConfig instances.
	/// </summary>
	private class DefaultComparer : IComparer<TEditorConfig>
	{
		public int Compare(TEditorConfig lhs, TEditorConfig rhs)
		{
			// The default configs should be sorted first.
			var lhsDefault = lhs!.IsDefault();
			var rhsDefault = rhs!.IsDefault();
			if (lhsDefault != rhsDefault)
				return lhsDefault ? -1 : 1;

			// Configs should sort alphabetically.
			var comparison = string.Compare(lhs.ToString(), rhs.ToString(), StringComparison.CurrentCulture);
			if (comparison != 0)
				return comparison;

			// Finally sort by Guid.
			return lhs.GetGuid().CompareTo(rhs.GetGuid());
		}
	}

	/// <summary>
	/// All IEditorConfigs of the same type by Guid.
	/// </summary>
	private readonly Dictionary<Guid, TEditorConfig> Configs = new();

	/// <summary>
	/// Sorted array of Config guids to use for UI.
	/// </summary>
	public Guid[] SortedConfigGuids;

	/// <summary>
	/// Sorted array of Config names to use for UI.
	/// </summary>
	public string[] SortedConfigNames;

	/// <summary>
	/// All IEditorConfigs of the same type, sorted.
	/// </summary>
	private readonly List<TEditorConfig> SortedConfigs = [];

	/// <summary>
	/// Comparer to use for sorting TEditorConfig instances.
	/// </summary>
	private IComparer<TEditorConfig> Comparer;

	public ConfigData()
	{
		SetComparer(new DefaultComparer());
	}

	public void SetComparer(IComparer<TEditorConfig> comparer)
	{
		Comparer = comparer;
		UpdateSortedConfigs();
	}

	public void UpdateSortedConfigs()
	{
		SortedConfigs.Clear();
		foreach (var kvp in Configs)
			SortedConfigs.Add(kvp.Value);
		SortedConfigs.Sort(Comparer);

		SortedConfigGuids = new Guid[SortedConfigs.Count];
		SortedConfigNames = new string[SortedConfigs.Count];
		for (var i = 0; i < SortedConfigs.Count; i++)
		{
			SortedConfigGuids[i] = SortedConfigs[i].GetGuid();
			SortedConfigNames[i] = SortedConfigs[i].ToString();
		}
	}

	public void AddConfig(TEditorConfig config)
	{
		Configs[config.GetGuid()] = config;
		config.AddObserver(this);
		UpdateSortedConfigs();
	}

	public void RemoveConfig(Guid guid)
	{
		if (!Configs.TryGetValue(guid, out var config))
			return;
		if (config.IsDefault())
			return;
		config.RemoveObserver(this);
		Configs.Remove(guid);
		UpdateSortedConfigs();
	}

	public TEditorConfig GetConfig(Guid guid)
	{
		return Configs.GetValueOrDefault(guid);
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

	public IEnumerable<TEditorConfig> GetSortedConfigs()
	{
		return SortedConfigs;
	}

	public string[] GetSortedConfigNames()
	{
		return SortedConfigNames;
	}

	public Guid[] GetSortedConfigGuids()
	{
		return SortedConfigGuids;
	}

	/// <summary>
	/// Notification handler for underling EditorConfig changes.
	/// </summary>
	public void OnNotify(string eventId, EditorConfig<TConfig> notifier, object payload)
	{
		// If any EditorConfig changes for any reason, update the sort.
		UpdateSortedConfigs();
	}
}

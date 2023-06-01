using Fumen;
using StepManiaLibrary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace StepManiaEditor
{
	internal sealed class PreferencesExpressedChartConfig : Notifier<PreferencesExpressedChartConfig>
	{
		/// <summary>
		/// One ExpressedChartConfig is a special Default config which cannot be edited.
		/// It is identified by this name.
		/// </summary>
		public const string DefaultConfigName = "Default";

		// Notifications.
		public const string NotificationConfigRename = "ConfigRename";

		/// <summary>
		/// Config object with an associated string name.
		/// </summary>
		internal sealed class NamedConfig
		{
			// Preferences.
			[JsonInclude]
			public string Name
			{
				get
				{
					return NameInternal;
				}
				set
				{
					// Null check around IsNewNameValid because this property is set during deserialization.
					if (!(IsNewNameValid?.Invoke(value) ?? true))
						return;
					if (!string.IsNullOrEmpty(NameInternal) && NameInternal.Equals(value))
						return;
					var oldName = NameInternal;
					NameInternal = value;
					// Null check around OnNameUpdated because this property is set during deserialization.
					OnNameUpdated?.Invoke(oldName, NameInternal);
				}
			}
			private string NameInternal;
			[JsonInclude] public ExpressedChartConfig Config = new ExpressedChartConfig();

			// Default values.
			public const BracketParsingMethod DefaultDefaultBracketParsingMethod = BracketParsingMethod.Balanced;
			public const BracketParsingDetermination DefaultBracketParsingDetermination = BracketParsingDetermination.ChooseMethodDynamically;
			public const int DefaultMinLevelForBrackets = 7;
			public const bool DefaultUseAggressiveBracketsWhenMoreSimultaneousNotesThanCanBeCoveredWithoutBrackets = true;
			public const double DefaultBalancedBracketsPerMinuteForAggressiveBrackets = 3.0;
			public const double DefaultBalancedBracketsPerMinuteForNoBrackets = 1.0;

			/// <summary>
			/// Function to determine if a new name is valid.
			/// </summary>
			private Func<string, bool> IsNewNameValid;
			/// <summary>
			/// Callback function to invoke when the name is updated.
			/// </summary>
			private Action<string, string> OnNameUpdated;

			/// <summary>
			/// Sets functions to use for name validation and calling back to when the name is updated.
			/// </summary>
			/// <param name="isNewNameValid">Name validation function.</param>
			/// <param name="onNameUpdated">Callback function to invoke when the name is updated.</param>
			public void SetNameUpdateFunctions(Func<string, bool> isNewNameValid, Action<string, string> onNameUpdated)
			{
				IsNewNameValid = isNewNameValid;
				OnNameUpdated = onNameUpdated;
			}

			/// <summary>
			/// Returns whether this NamedConfig is the Default config.
			/// </summary>
			/// <returns>True if this is the Defaul config and false otherwise.</returns>
			public bool IsDefaultConfig()
			{
				return Name.Equals(DefaultConfigName);
			}
		}

		// Preferences.
		[JsonInclude] public string ActiveExpressedChartConfigForWindow = null;
		[JsonInclude] public bool ShowExpressedChartListWindow = false;
		[JsonInclude] public Dictionary<string, NamedConfig> Configs = new Dictionary<string, NamedConfig>();

		/// <summary>
		/// Sorted array of Config names to use for UI.
		/// </summary>
		private string[] SortedConfigNames;

		/// <summary>
		/// Called by owning Preferences after deserialization.
		/// </summary>
		public void PostLoad()
		{
			// Set the default config. It should never be modified so delete it if it exists and re-add it.
			Configs.Remove(DefaultConfigName);
			var defaultConfig = AddConfig(DefaultConfigName).Config;
			InitializeConfigWithDefaultValues(defaultConfig);

			// Ensure every NamedConfig is configured and valid.
			var invalidConfigNames = new List<string>();
			foreach (var kvp in Configs)
			{
				// Configure the NamedConfig will name update functions.
				kvp.Value.SetNameUpdateFunctions(IsNewConfigNameValid, OnConfigNameUpdated);

				// Validate the ExpressedChartConfig. If this fails, store it for removal.
				if (!kvp.Value.Config.Validate(kvp.Key))
				{
					invalidConfigNames.Add(kvp.Key);
				}
			}
			// Remove all invalid ExpressedChartConfig objects.
			foreach(var invalidConfigNmae in invalidConfigNames)
			{
				Configs.Remove(invalidConfigNmae);
			}

			// Ensure the variables for displaying an ExpressedChartConfig don't point to an unknown config.
			if (ActiveExpressedChartConfigForWindow != null)
			{
				if (!Configs.ContainsKey(ActiveExpressedChartConfigForWindow))
					ActiveExpressedChartConfigForWindow = null;
			}
			if (ActiveExpressedChartConfigForWindow == null)
				ShowExpressedChartListWindow = false;

			// Now that names are loaded, set the sorted array.
			UpdateSortedConfigNames();
		}

		private void InitializeConfigWithDefaultValues(ExpressedChartConfig config)
		{
			config.DefaultBracketParsingMethod = BracketParsingMethod.Balanced;
			config.BracketParsingDetermination = BracketParsingDetermination.ChooseMethodDynamically;
			config.MinLevelForBrackets = 7;
			config.UseAggressiveBracketsWhenMoreSimultaneousNotesThanCanBeCoveredWithoutBrackets = true;
			config.BalancedBracketsPerMinuteForAggressiveBrackets = 3.0;
			config.BalancedBracketsPerMinuteForNoBrackets = 1.0;
		}

		public string GetNewConfigName()
		{
			var configName = "New Config";
			if (Configs.ContainsKey(configName))
			{
				int index = 1;
				do
				{
					configName = $"New Config ({index})";
					if (!Configs.ContainsKey(configName))
						break;
					index++;
				} while (true);
			}
			return configName;
		}

		public NamedConfig AddConfig(string name)
		{
			if (!IsNewConfigNameValid(name))
				return null;

			var config = new NamedConfig();
			config.SetNameUpdateFunctions(IsNewConfigNameValid, OnConfigNameUpdated);
			config.Name = name;
			InitializeConfigWithDefaultValues(config.Config);
			Configs[name] = config;

			UpdateSortedConfigNames();

			return config;
		}

		public void DeleteConfig(string name)
		{
			if (!Configs.TryGetValue(name, out var config))
				return;
			if (config.IsDefaultConfig())
				return;
			Configs.Remove(name);

			// If the actively displayed config is being deleted, remove the variable tracking 
			if (!string.IsNullOrEmpty(ActiveExpressedChartConfigForWindow)
				&& ActiveExpressedChartConfigForWindow.Equals(name))
			{
				ActiveExpressedChartConfigForWindow = null;
				ShowExpressedChartListWindow = false;
			}

			UpdateSortedConfigNames();
		}

		public NamedConfig GetNamedConfig(string name)
		{
			if (!Configs.TryGetValue(name, out var config))
				return null;
			return config;
		}

		public ExpressedChartConfig GetConfig(string name)
		{
			if (!Configs.TryGetValue(name, out var config))
				return null;
			return config.Config;
		}

		public bool DoesConfigExist(string name)
		{
			return !string.IsNullOrEmpty(name) && Configs.ContainsKey(name);
		}

		public bool IsNewConfigNameValid(string name)
		{
			if (string.IsNullOrEmpty(name))
				return false;
			foreach (var kvp in Configs)
			{
				if (name.Equals(kvp.Key))
					return false;
			}
			return true;
		}

		/// <summary>
		/// Called when a NamedConfig's name is updated to a new value.
		/// </summary>
		/// <param name="oldName">Old name.</param>
		/// <param name="newName">New name.</param>
		private void OnConfigNameUpdated(string oldName, string newName)
		{
			if (string.IsNullOrEmpty(oldName))
				return;
			if (!Configs.ContainsKey(oldName))
				return;
			
			// If the preference for the actively displayed config is holding on to the
			// old name, update it to the new name.
			if (!string.IsNullOrEmpty(ActiveExpressedChartConfigForWindow)
				&& ActiveExpressedChartConfigForWindow.Equals(oldName))
			{
				ActiveExpressedChartConfigForWindow = newName;
			}
			
			// Update the Configs Dictionary with the new name.
			var config = Configs[oldName];
			Configs.Remove(oldName);
			Configs[newName] = config;

			// Update the sort since the name has changed.
			UpdateSortedConfigNames();

			// Notify Observers.
			Notify(NotificationConfigRename, this, new Tuple<string, string>(oldName, newName));
		}

		private void UpdateSortedConfigNames()
		{
			var keyList = Configs.Keys.ToList();
			keyList.Sort((lhs, rhs) =>
			{
				// The Default config should be sorted first.
				if (lhs.Equals(DefaultConfigName))
					return -1;
				if (rhs.Equals(DefaultConfigName))
					return 1;
				// All other configs should follow alphabetically.
				return lhs.CompareTo(rhs);
			});
			SortedConfigNames = keyList.ToArray();
		}

		public string[] GetSortedConfigNames()
		{
			return SortedConfigNames;
		}
	}
}

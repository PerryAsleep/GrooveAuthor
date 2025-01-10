using System;
using System.Collections.Generic;
using System.Numerics;
using Fumen;
using ImGuiNET;
using Microsoft.Xna.Framework.Input;
using static StepManiaEditor.ImGuiUtils;

namespace StepManiaEditor;

/// <summary>
/// Class for drawing and remapping controls.
/// Expected Usage:
///  Call AddCommand as needed before first Draw.
///  Call Draw to draw.
///  Categories and commands will be drawn in the order they were added.
/// </summary>
internal sealed class UIControls : UIWindow, Fumen.IObserver<PreferencesKeyBinds>
{
	private static readonly int TitleColumnWidth = UiScaled(260);
	private static readonly Vector2 DefaultSize = new(UiScaled(538), UiScaled(800));
	private static readonly int EditButtonWidth = UiScaled(40);
	private static readonly int DeleteButtonWidth = UiScaled(20);
	private static readonly int AddButtonWidth = UiScaled(20);
	private static readonly int ResetButtonWidth = UiScaled(40);

	#region Commands

	internal interface ICommand
	{
		public void Draw();
	}

	/// <summary>
	/// StaticCommands can't be remapped.
	/// They just show the command name and the inputs.
	/// </summary>
	internal class StaticCommand : ICommand
	{
		private readonly string Name;
		private readonly string InputString;

		public StaticCommand(string name, string inputString)
		{
			Name = name;
			InputString = inputString;
		}

		public void Draw()
		{
			var spacing = ImGui.GetStyle().ItemSpacing.X;

			PushDisabled();
			ImGuiLayoutUtils.DrawRowTitleAndAdvanceColumn(Name);
			ImGui.Button($"Reset##{Name}", new Vector2(ResetButtonWidth, 0.0f));
			ImGui.SameLine();
			ImGui.Button($"+##{Name}", new Vector2(AddButtonWidth, 0.0f));
			ImGui.SameLine();
			var textWidth = ImGui.GetContentRegionAvail().X - (EditButtonWidth + DeleteButtonWidth + spacing * 2);
			Text(InputString, textWidth);
			ImGui.SameLine();
			ImGui.Button($"Edit##{Name}", new Vector2(EditButtonWidth, 0.0f));
			ImGui.SameLine();
			ImGui.Button($"X##{Name}", new Vector2(AddButtonWidth, 0.0f));
			PopDisabled();
		}
	}

	/// <summary>
	/// KeyBindCommands have one or more inputs and can be remapped.
	/// </summary>
	internal class KeyBindCommand : ICommand
	{
		private readonly KeyCommandManager KeyCommandManager;
		private readonly string Name;
		private readonly string AdditionalInputText;
		private readonly string Id;
		private readonly List<string> InputsAsStrings = new();
		private List<Keys[]> Inputs;
		private readonly List<Keys[]> Defaults;
		private readonly List<bool> Conflicts = new();
		private bool Modified;

		public KeyBindCommand(KeyCommandManager keyCommandManager, string name, string id,
			string additionalInputText = null)
		{
			KeyCommandManager = keyCommandManager;
			Name = name;
			Id = id;
			AdditionalInputText = additionalInputText;

			var p = Preferences.Instance.PreferencesKeyBinds;
			Defaults = p.GetDefaults(Id);
			ResetInputFromPreferences();
		}

		public void RefreshConflicts()
		{
			Conflicts.Clear();
			for (var i = 0; i < Inputs.Count; i++)
			{
				Conflicts.Add(KeyCommandManager.GetConflictingCommands(Id, Inputs[i]).Count > 0);
			}
		}

		public void ResetInputFromPreferences()
		{
			// Reset Inputs.
			var p = Preferences.Instance.PreferencesKeyBinds;
			Inputs = p.CloneKeyBinding(Id);

			// Refresh our cached state for if the sate is modified from the Defaults.
			RefreshModifiedState();

			// Refresh our cached input strings.
			InputsAsStrings.Clear();
			foreach (var input in Inputs)
				InputsAsStrings.Add(GetCommandString(input));
		}

		private void RefreshModifiedState()
		{
			Modified = false;
			if (Inputs.Count != Defaults.Count)
			{
				Modified = true;
				return;
			}

			for (var i = 0; i < Inputs.Count; i++)
			{
				var currentInput = Inputs[i];
				var defaultInput = Defaults[i];
				if (currentInput.Length != defaultInput.Length)
				{
					Modified = true;
					return;
				}

				for (var j = 0; j < currentInput.Length; j++)
				{
					if (currentInput[j] != defaultInput[j])
					{
						Modified = true;
						return;
					}
				}
			}
		}

		private void Reset()
		{
			// Set the state to Defaults.
			// We will listen for this change and refresh cached state in response.
			ActionQueue.Instance.Do(new ActionUpdateKeyBinding(Id, Name, Defaults));
			//RefreshState();
		}

		private void Update()
		{
			// Commit our locally modified Inputs to preferences.
			// We will listen for this change and refresh cached state in response.
			ActionQueue.Instance.Do(new ActionUpdateKeyBinding(Id, Name, Inputs));
		}

		public void Draw()
		{
			var canReset = Modified;
			var reset = false;
			var add = false;
			var rebindIndex = -1;
			var deleteIndex = -1;
			var spacing = ImGui.GetStyle().ItemSpacing.X;

			// Can't delete if there is only one input and it is unbound.
			var canDelete = !(Inputs.Count == 1 && (Inputs[0] == null || Inputs[0].Length == 0));

			for (var i = 0; i < Inputs.Count; i++)
			{
				ImGuiLayoutUtils.DrawRowTitleAndAdvanceColumn(Name);

				if (i == 0)
				{
					if (!canReset)
						PushDisabled();
					if (ImGui.Button($"Reset##{Id}", new Vector2(ResetButtonWidth, 0.0f)))
					{
						reset = true;
					}

					if (!canReset)
						PopDisabled();
					ImGui.SameLine();

					if (ImGui.Button($"+##{Id}", new Vector2(AddButtonWidth, 0.0f)))
					{
						add = true;
					}

					ImGui.SameLine();
				}
				else
				{
					ImGui.Dummy(new Vector2(ResetButtonWidth + AddButtonWidth + spacing, 0.0f));
					ImGui.SameLine();
				}

				var textWidth = ImGui.GetContentRegionAvail().X - (EditButtonWidth + DeleteButtonWidth + spacing * 2);
				var text = InputsAsStrings[i];
				if (!string.IsNullOrEmpty(AdditionalInputText))
					text += AdditionalInputText;

				if (i < Conflicts.Count && Conflicts[i])
				{
					TextColored(UILog.GetColor(LogLevel.Warn), text, textWidth);
				}
				else
				{
					Text(text, textWidth);
				}

				ImGui.SameLine();

				if (ImGui.Button($"Edit##{Id}{i}", new Vector2(EditButtonWidth, 0.0f)))
				{
					rebindIndex = i;
				}

				ImGui.SameLine();

				if (!canDelete)
					PushDisabled();
				if (ImGui.Button($"X##{Id}{i}", new Vector2(AddButtonWidth, 0.0f)))
				{
					deleteIndex = i;
				}

				if (!canDelete)
					PopDisabled();
			}

			if (reset)
			{
				Reset();
			}

			if (add)
			{
				Inputs.Add(Array.Empty<Keys>());
				Update();
			}

			if (deleteIndex != -1)
			{
				Inputs.RemoveAt(deleteIndex);
				if (Inputs.Count == 0)
					Inputs.Add(Array.Empty<Keys>());
				Update();
			}

			if (rebindIndex != -1)
			{
				UIKeyRebindModal.Instance.Open(Name, Id, AdditionalInputText, InputsAsStrings[rebindIndex],
					(newInput) =>
					{
						if (rebindIndex < Inputs.Count)
						{
							Inputs[rebindIndex] = (Keys[])newInput.Clone();
							Update();
						}
					});
			}
		}
	}

	#endregion Commands

	/// <summary>
	/// A group of commands under the same category.
	/// </summary>
	internal class Category
	{
		private readonly string Name;
		private readonly List<ICommand> Commands = new();

		public Category(string name)
		{
			Name = name;
		}

		public string GetName()
		{
			return Name;
		}

		public void AddCommand(ICommand command)
		{
			Commands.Add(command);
		}

		public void Draw()
		{
			if (ImGui.CollapsingHeader(Name, ImGuiTreeNodeFlags.DefaultOpen))
			{
				if (ImGuiLayoutUtils.BeginTable(Name, TitleColumnWidth))
				{
					foreach (var command in Commands)
						command.Draw();
					ImGuiLayoutUtils.EndTable();
				}
			}
		}
	}

	public static UIControls Instance { get; } = new();

	/// <summary>
	/// All Categories.
	/// </summary>
	private readonly List<Category> Categories = new();

	private readonly Dictionary<string, KeyBindCommand> AllKeyBindCommands = new();
	public const string MultipleInputsJoinString = " / ";
	public const string MultipleKeysJoinString = "+";
	public const string OrString = "/";
	public const string Unbound = "Unbound";

	private KeyCommandManager KeyCommandManager;

	private UIControls() : base("Controls")
	{
	}

	public void Initialize(KeyCommandManager keyCommandManager)
	{
		KeyCommandManager = keyCommandManager;
		Preferences.Instance.PreferencesKeyBinds.AddObserver(this);
	}

	public static string GetCommandString(List<Keys[]> inputs)
	{
		if (inputs == null || inputs.Count == 0)
			return Unbound;

		var combinedInputString = "";
		var first = true;
		foreach (var input in inputs)
		{
			var inputString = GetCommandString(input);
			if (inputString != Unbound)
			{
				if (!first)
					combinedInputString += MultipleInputsJoinString;
				combinedInputString += GetCommandString(input);
				first = false;
			}
		}

		if (string.IsNullOrEmpty(combinedInputString))
			return Unbound;
		return combinedInputString;
	}

	public static string GetCommandString(Keys[] input)
	{
		if (input == null || input.Length == 0)
			return Unbound;

		var inputString = "";
		var firstInput = true;
		foreach (var key in input)
		{
			if (!firstInput)
				inputString += MultipleKeysJoinString;

			switch (key)
			{
				case Keys.LeftControl:
				case Keys.RightControl:
					inputString += "Ctrl";
					break;
				case Keys.LeftShift:
				case Keys.RightShift:
					inputString += "Shift";
					break;
				case Keys.LeftAlt:
				case Keys.RightAlt:
					inputString += "Alt";
					break;
				case Keys.LeftWindows:
				case Keys.RightWindows:
					inputString += "Win";
					break;
				case Keys.D0:
				case Keys.D1:
				case Keys.D2:
				case Keys.D3:
				case Keys.D4:
				case Keys.D5:
				case Keys.D6:
				case Keys.D7:
				case Keys.D8:
				case Keys.D9:
					inputString += key.ToString()[1..];
					break;
				default:
					inputString += key;
					break;
			}

			firstInput = false;
		}

		if (string.IsNullOrEmpty(inputString))
			return Unbound;

		return inputString;
	}

	public override void Open(bool focus)
	{
		Preferences.Instance.ShowControlsWindow = true;
		if (focus)
			Focus();
	}

	public override void Close()
	{
		Preferences.Instance.ShowControlsWindow = false;
	}

	public void AddCommand(string categoryName, string commandName, string id,
		string additionalInputText = null)
	{
		var newCategory = GetOrCreateCategory(categoryName);
		var newCommand = new KeyBindCommand(KeyCommandManager, commandName, id, additionalInputText);
		newCategory.AddCommand(newCommand);
		AllKeyBindCommands.Add(id, newCommand);
	}

	public void FinishAddingCommands()
	{
		RefreshConflicts();
	}

	public void RefreshConflicts()
	{
		foreach (var kvp in AllKeyBindCommands)
		{
			kvp.Value.RefreshConflicts();
		}
	}

	public void AddStaticCommand(string categoryName, string commandName, string input)
	{
		var category = GetOrCreateCategory(categoryName);
		category.AddCommand(new StaticCommand(commandName, input));
	}

	private Category GetOrCreateCategory(string name)
	{
		foreach (var category in Categories)
		{
			if (category.GetName() == name)
				return category;
		}

		var newCategory = new Category(name);
		Categories.Add(newCategory);
		return newCategory;
	}

	public void Draw()
	{
		if (!Preferences.Instance.ShowControlsWindow)
			return;
		if (BeginWindow(WindowTitle, ref Preferences.Instance.ShowControlsWindow, DefaultSize))
			foreach (var category in Categories)
				category.Draw();
		ImGui.End();
	}

	public void OnNotify(string eventId, PreferencesKeyBinds notifier, object payload)
	{
		if (eventId == PreferencesKeyBinds.NotificationKeyBindingChanged)
		{
			if (AllKeyBindCommands.TryGetValue((string)payload, out var command))
			{
				command.ResetInputFromPreferences();
				RefreshConflicts();
			}
		}
	}
}

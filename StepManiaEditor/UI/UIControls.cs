using System.Collections.Generic;
using System.Numerics;
using ImGuiNET;
using Microsoft.Xna.Framework.Input;
using static StepManiaEditor.ImGuiUtils;

namespace StepManiaEditor;

/// <summary>
/// Class for drawing controls.
/// Expected Usage:
///  Call AddCommand as needed before first Draw.
///  Call Draw to draw.
///  Categories and commands will be drawn in the order they were added.
/// </summary>
internal sealed class UIControls : UIWindow
{
	private static readonly int TitleColumnWidth = UiScaled(260);
	private static readonly Vector2 DefaultSize = new(UiScaled(538), UiScaled(800));

	/// <summary>
	/// A group of commands under the same category.
	/// </summary>
	internal class Category
	{
		public readonly string Name;
		public List<Command> Commands = new();

		public Category(string name)
		{
			Name = name;
		}

		public Command GetOrCreateCommand(string commandName)
		{
			foreach (var command in Commands)
			{
				if (command.Name == commandName)
					return command;
			}

			var newCommand = new Command(commandName);
			Commands.Add(newCommand);
			return newCommand;
		}
	}

	/// <summary>
	/// An individual command with one or more inputs.
	/// </summary>
	internal class Command
	{
		public readonly string Name;
		public string Input = "";

		public Command(string name)
		{
			Name = name;
		}

		public void AddInput(Keys[] input)
		{
			AddInput(GetCommandString(input));
		}

		public void AddInput(string input)
		{
			if (string.IsNullOrEmpty(Input))
				Input = input;
			else
				Input += MultipleInputsJoinString + input;
		}
	}

	/// <summary>
	/// All Categories.
	/// </summary>
	private readonly List<Category> Categories = new();

	public static UIControls Instance { get; } = new();

	public const string MultipleInputsJoinString = " / ";
	public const string MultipleKeysJoinString = "+";
	public const string OrString = "/";
	public const string Unbound = "Unbound";

	private UIControls() : base("Controls")
	{
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

	public void AddCommand(string categoryName, string commandName, Keys[] input)
	{
		var category = GetOrCreateCategory(categoryName);
		var command = category.GetOrCreateCommand(commandName);
		command.AddInput(input);
	}

	public void AddCommand(string categoryName, string commandName, string input)
	{
		var category = GetOrCreateCategory(categoryName);
		var command = category.GetOrCreateCommand(commandName);
		command.AddInput(input);
	}

	private Category GetOrCreateCategory(string name)
	{
		foreach (var category in Categories)
		{
			if (category.Name == name)
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
		{
			foreach (var category in Categories)
			{
				if (ImGui.CollapsingHeader(category.Name, ImGuiTreeNodeFlags.DefaultOpen))
				{
					if (ImGuiLayoutUtils.BeginTable(category.Name, TitleColumnWidth))
					{
						foreach (var command in category.Commands)
						{
							ImGuiLayoutUtils.DrawRowTitleAndText(command.Name, command.Input);
						}

						ImGuiLayoutUtils.EndTable();
					}
				}
			}
		}

		ImGui.End();
	}
}

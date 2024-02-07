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
internal sealed class UIControls
{
	public const string WindowTitle = "Controls";

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
			var inputString = "";
			var firstInput = true;
			foreach (var key in input)
			{
				if (!firstInput)
					inputString += "+";

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
					default:
						inputString += key;
						break;
				}

				firstInput = false;
			}

			AddInput(inputString);
		}

		public void AddInput(string input)
		{
			if (string.IsNullOrEmpty(Input))
				Input = input;
			else
				Input += $" / {input}";
		}
	}

	/// <summary>
	/// All Categories.
	/// </summary>
	private readonly List<Category> Categories = new();

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

		ImGui.SetNextWindowSize(DefaultSize, ImGuiCond.FirstUseEver);
		if (ImGui.Begin(WindowTitle, ref Preferences.Instance.ShowControlsWindow, ImGuiWindowFlags.None))
		{
			foreach (var category in Categories)
			{
				if (ImGui.CollapsingHeader(category.Name, ImGuiTreeNodeFlags.DefaultOpen))
				{
					if (ImGuiLayoutUtils.BeginTable(category.Name, TitleColumnWidth))
					{
						foreach (var command in category.Commands)
						{
							ImGuiLayoutUtils.DrawTitleAndText(command.Name, command.Input);
						}

						ImGuiLayoutUtils.EndTable();
					}
				}
			}
		}

		ImGui.End();
	}
}

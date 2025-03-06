using System;
using System.Collections.Generic;
using System.Numerics;
using Fumen;
using ImGuiNET;
using Microsoft.Xna.Framework.Input;
using static StepManiaEditor.ImGuiUtils;

namespace StepManiaEditor;

/// <summary>
/// Modal to rebind keys to an action.
/// </summary>
internal sealed class UIKeyRebindModal
{
	private const string NoKeysMessage = "Awaiting Input...";
	private static readonly int ResetButtonWidth = UiScaled(40);
	private static readonly int ConflictingBindingsHeight = UiScaled(100);
	private static readonly int ModalHeight = UiScaled(240);

	private bool IsRebinding;
	private List<Keys> NewKeys;
	private string CommandString;
	private KeyCommandManager KeyCommandManager;
	private List<string> ConflictingCommands;

	public static UIKeyRebindModal Instance { get; } = new();

	private UIKeyRebindModal()
	{
	}

	public void SetKeyCommandManager(KeyCommandManager keyCommandManager)
	{
		KeyCommandManager = keyCommandManager;
	}

	/// <summary>
	/// Enqueues the key rebinding modal to be shown.
	/// </summary>
	/// <param name="name">Name of the command to rebind.</param>
	/// <param name="id">Id for the binding.</param>
	/// <param name="additionalInputText">Optional addition input text to append to commands.</param>
	/// <param name="oldInputString">String representation of the old input being rebound.</param>
	/// <param name="onRebindComplete">Action to invoke when the rebinding has been confirmed.</param>
	public void Open(string name, string id, string additionalInputText, string oldInputString,
		Action<Keys[]> onRebindComplete)
	{
		UIModals.OpenModalTwoButtons(
			$"Rebind {name}",
			null,
			"Cancel",
			FinishRebinding,
			"Confirm",
			() =>
			{
				onRebindComplete(NewKeys.ToArray());
				FinishRebinding();
			},
			() => Draw(oldInputString, id, additionalInputText),
			false, 0, ModalHeight);
	}

	private void FinishRebinding()
	{
		if (!IsRebinding)
			return;
		IsRebinding = false;
		NewKeys = null;
		CommandString = null;
		ConflictingCommands = null;
		KeyCommandManager.NotifyRebindingEnd();
	}

	private void Draw(string oldInputString, string id, string additionalInputText)
	{
		// In this action to render our contents we know we are being shown.
		// Set the Rebinding flag now.
		if (!IsRebinding)
		{
			IsRebinding = true;
			NewKeys = new List<Keys>();
			CommandString = NoKeysMessage;
			ConflictingCommands = new List<string>();
			KeyCommandManager.NotifyRebindingStart();
		}

		UpdateKeys(id);

		DrawInput("Old Binding: ", oldInputString, additionalInputText, ImGui.GetContentRegionAvail().X);
		var textWidth = ImGui.GetContentRegionAvail().X - (ImGui.GetStyle().ItemSpacing.X + ResetButtonWidth);
		if (NewKeys.Count > 0)
			DrawInput("New Binding: ", CommandString, additionalInputText, textWidth);
		else
			Text($"New Binding: {CommandString}", textWidth);
		ImGui.SameLine();
		var disabled = NewKeys.Count == 0;
		if (disabled)
			PushDisabled();
		if (ImGui.Button("Reset", new Vector2(ResetButtonWidth, 0.0f)))
		{
			NewKeys.Clear();
			CommandString = NoKeysMessage;
			ConflictingCommands.Clear();
		}

		if (disabled)
			PopDisabled();

		// Draw conflicts.
		ImGui.Separator();
		ImGui.TextUnformatted("Conflicting Bindings:");
		if (ImGui.BeginChild("ConflictingBindings", new Vector2(0, ConflictingBindingsHeight), ImGuiChildFlags.Border))
		{
			if (ConflictingCommands.Count == 0)
			{
				ImGui.TextUnformatted("None");
			}
			else
			{
				foreach (var conflict in ConflictingCommands)
				{
					ImGui.PushStyleColor(ImGuiCol.Text, UILog.GetColor(LogLevel.Warn));
					ImGui.TextUnformatted(conflict);
					ImGui.PopStyleColor();
				}
			}
		}

		ImGui.EndChild();
	}

	private void DrawInput(string prefix, string input, string additionalInputText, float textWidth)
	{
		var additionalTextWidth = 0.0f;
		var text = prefix + input;
		if (!string.IsNullOrEmpty(additionalInputText))
		{
			var baseTextWidth = ImGui.CalcTextSize(text).X;
			additionalTextWidth = textWidth - baseTextWidth;
			if (additionalTextWidth > 0)
				textWidth = baseTextWidth;
		}

		Text(text, textWidth);
		if (additionalTextWidth > 0)
		{
			var spacing = ImGui.GetStyle().ItemSpacing.X;
			ImGui.GetStyle().ItemSpacing.X = 0;
			ImGui.SameLine();
			Text(additionalInputText, additionalTextWidth, true);
			ImGui.GetStyle().ItemSpacing.X = spacing;
		}
	}

	private void UpdateKeys(string id)
	{
		var modified = false;
		foreach (var pressedKey in Keyboard.GetState().GetPressedKeys())
		{
			// Don't allow invalid keys.
			if (!PreferencesKeyBinds.IsValidKeyForBinding(pressedKey))
				continue;

			// Treat right modifier keys the same as their left counterparts.
			var key = pressedKey;
			switch (pressedKey)
			{
				case Keys.RightControl:
					key = Keys.LeftControl;
					break;
				case Keys.RightShift:
					key = Keys.LeftShift;
					break;
				case Keys.RightAlt:
					key = Keys.LeftAlt;
					break;
				case Keys.RightWindows:
					key = Keys.LeftWindows;
					break;
			}

			if (!NewKeys.Contains(key))
			{
				NewKeys.Add(key);
				modified = true;
			}
		}

		// Update cached state.
		if (modified)
		{
			var newKeysArray = NewKeys.ToArray();
			ConflictingCommands = KeyCommandManager.GetConflictingCommands(id, newKeysArray);
			CommandString = UIControls.GetCommandString(newKeysArray);
		}
	}
}

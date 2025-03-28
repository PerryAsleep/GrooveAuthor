using System;
using System.Collections.Generic;
using System.Numerics;
using ImGuiNET;
using static StepManiaEditor.ImGuiUtils;

namespace StepManiaEditor;

/// <summary>
/// Class for drawing modal dialogs.
/// Expected Usage:
///  Call Init once at startup.
///  Call one of the OpenModal methods to start showing a modal.
///  Call Draw once per frame.
/// </summary>
internal sealed class UIModals
{
	private static readonly Vector2 DefaultSize = new(UiScaled(440), UiScaled(140));
	public static readonly float SeparatorHeight = UiScaled(1);
	public static readonly Vector2 ButtonSize = new(UiScaled(80), UiScaled(21));

	/// <summary>
	/// Internal state for a single modal.
	/// </summary>
	private class ModalState
	{
		/// <summary>
		/// Internal state for a single button within a modal.
		/// </summary>
		public class ButtonData
		{
			public ButtonData(string text, Action callback)
			{
				Text = text;
				Callback = callback;
			}

			public readonly string Text;
			public readonly Action Callback;
		}

		public readonly string Title;
		public readonly string Message;
		public readonly List<ButtonData> Buttons;
		public readonly Action CustomBodyUI;
		public readonly Vector2 Size;
		public bool ShouldPlaySound;

		public ModalState(string title, string message,
			string buttonText, Action buttonCallback,
			Vector2 size, Action customBodyUI, bool shouldPlaySound)
		{
			Title = title;
			Message = message;
			CustomBodyUI = customBodyUI;
			Buttons =
			[
				new ButtonData(buttonText, buttonCallback),
			];
			Size = size;
			ShouldPlaySound = shouldPlaySound;
		}

		public ModalState(string title, string message,
			string button1Text, Action button1Callback,
			string button2Text, Action button2Callback,
			Vector2 size, Action customBodyUI, bool shouldPlaySound)
		{
			Title = title;
			Message = message;
			CustomBodyUI = customBodyUI;
			Buttons =
			[
				new ButtonData(button1Text, button1Callback),
				new ButtonData(button2Text, button2Callback),
			];
			Size = size;
			ShouldPlaySound = shouldPlaySound;
		}

		public ModalState(string title, string message,
			string button1Text, Action button1Callback,
			string button2Text, Action button2Callback,
			string button3Text, Action button3Callback,
			Vector2 size, Action customBodyUI, bool shouldPlaySound)
		{
			Title = title;
			Message = message;
			CustomBodyUI = customBodyUI;
			Buttons =
			[
				new ButtonData(button1Text, button1Callback),
				new ButtonData(button2Text, button2Callback),
				new ButtonData(button3Text, button3Callback),
			];
			Size = size;
			ShouldPlaySound = shouldPlaySound;
		}
	}

	/// <summary>
	/// Editor instance. Needed for bounds.
	/// </summary>
	private static Editor Editor;

	/// <summary>
	/// All currently active modals.
	/// </summary>
	private static readonly List<ModalState> Modals = [];

	/// <summary>
	/// Initialization method.
	/// </summary>
	/// <param name="editor">Editor instance.</param>
	public static void Init(Editor editor)
	{
		Editor = editor;
	}

	/// <summary>
	/// Draws any currently active modals.
	/// </summary>
	public static void Draw(IEditorPlatform platformInterface)
	{
		// Draw modals in the order they were enqueued.
		while (Modals.Count > 0)
		{
			var modal = Modals[0];
			var closedModal = false;
			if (BeginModal(modal.Title, modal.Size))
			{
				if (modal.ShouldPlaySound)
				{
					platformInterface.PlayExclamationSound();
					modal.ShouldPlaySound = false;
				}

				var hasMessage = !string.IsNullOrEmpty(modal.Message);
				// Draw the message.
				if (hasMessage)
				{
					ImGui.TextWrapped(EscapeTextForImGui(modal.Message));
				}

				// Draw any custom body UI.
				if (modal.CustomBodyUI != null)
				{
					if (hasMessage)
						ImGui.Separator();
					modal.CustomBodyUI();
				}

				// Draw the buttons. If any button is pressed, close the modal.
				if (DrawButtonsRow(modal.Buttons))
				{
					Modals.RemoveAt(0);
					closedModal = true;
				}

				ImGui.EndPopup();

				if (!closedModal)
					break;
			}
		}
	}

	public static void OpenModalOneButton(string title, string message,
		string buttonText, Action buttonCallback,
		Action customBodyUI = null,
		bool playSound = true,
		int customSizeX = 0,
		int customSizeY = 0)
	{
		var size = DefaultSize;
		if (customSizeX != 0)
			size.X = customSizeX;
		if (customSizeY != 0)
			size.Y = customSizeY;

		Modals.Add(new ModalState(title, message,
			buttonText, buttonCallback, size,
			customBodyUI, playSound));
	}

	public static void OpenModalTwoButtons(string title, string message,
		string button1Text, Action button1Callback,
		string button2Text, Action button2Callback,
		Action customBodyUI = null,
		bool playSound = true,
		int customSizeX = 0,
		int customSizeY = 0)
	{
		var size = DefaultSize;
		if (customSizeX != 0)
			size.X = customSizeX;
		if (customSizeY != 0)
			size.Y = customSizeY;

		Modals.Add(new ModalState(title, message,
			button1Text, button1Callback,
			button2Text, button2Callback,
			size, customBodyUI, playSound));
	}

	public static void OpenModalThreeButtons(string title, string message,
		string button1Text, Action button1Callback,
		string button2Text, Action button2Callback,
		string button3Text, Action button3Callback,
		Action customBodyUI = null,
		bool playSound = true,
		int customSizeX = 0,
		int customSizeY = 0)
	{
		var size = DefaultSize;
		if (customSizeX != 0)
			size.X = customSizeX;
		if (customSizeY != 0)
			size.Y = customSizeY;

		Modals.Add(new ModalState(title, message,
			button1Text, button1Callback,
			button2Text, button2Callback,
			button3Text, button3Callback,
			size, customBodyUI, playSound));
	}

	/// <summary>
	/// Begin drawing a modal window.
	/// </summary>
	/// <param name="title">Title of the window.</param>
	/// <param name="size">Size of the modal.</param>
	/// <returns>True if the window is open.</returns>
	private static bool BeginModal(string title, Vector2 size)
	{
		var screenW = Editor.GetViewportWidth();
		var screenH = Editor.GetViewportHeight();

		var windowPos = new Vector2((screenW - size.X) * 0.5f, (screenH - size.Y) * 0.5f);

		var openFlag = true;
		ImGui.OpenPopup(title);
		ImGui.SetNextWindowSize(size);
		ImGui.SetNextWindowPos(windowPos, ImGuiCond.Always);
		var open = ImGui.BeginPopupModal(title, ref openFlag,
			ImGuiWindowFlags.NoResize |
			ImGuiWindowFlags.Modal |
			ImGuiWindowFlags.NoMove |
			ImGuiWindowFlags.NoDecoration);

		if (open)
		{
			ImGui.TextUnformatted(title);
			ImGui.Separator();
		}

		return open;
	}

	/// <summary>
	/// Draw a list of buttons at the bottom of the modal, spaced evenly.
	/// </summary>
	/// <param name="buttons">Buttons to draw.</param>
	/// <returns>True if any button was clicked and the modal should close.</returns>
	private static bool DrawButtonsRow(List<ModalState.ButtonData> buttons)
	{
		// Add a dummy padding element to get the bottoms to be bottom-justified.
		var yPadding = ImGui.GetContentRegionAvail().Y - ImGui.GetStyle().ItemSpacing.Y * 2 - ButtonSize.Y - SeparatorHeight;
		ImGui.Dummy(new Vector2(1, yPadding));

		// Determine the spacing between the buttons.
		float paddingSize;
		if (buttons.Count == 1)
		{
			paddingSize = ImGui.GetContentRegionAvail().X - ImGui.GetStyle().ItemSpacing.X - ButtonSize.X;
		}
		else
		{
			paddingSize = (ImGui.GetContentRegionAvail().X
			               - (buttons.Count - 1) * (ImGui.GetStyle().ItemSpacing.X * 2)
			               - ButtonSize.X * buttons.Count) / (buttons.Count - 1);
		}

		ImGui.Separator();

		// Right justify if there is only one button.
		if (buttons.Count == 1)
		{
			ImGui.Dummy(new Vector2(paddingSize, 1));
			ImGui.SameLine();
		}

		// Draw each button.
		var index = 0;
		var shouldClose = false;
		foreach (var buttonData in buttons)
		{
			if (index > 0)
			{
				ImGui.SameLine();
				ImGui.Dummy(new Vector2(paddingSize, 1));
				ImGui.SameLine();
			}

			if (ImGui.Button(buttonData.Text, ButtonSize))
			{
				buttonData.Callback();
				ImGui.CloseCurrentPopup();
				shouldClose = true;
			}

			index++;
		}

		return shouldClose;
	}
}

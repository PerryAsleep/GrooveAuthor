using System;
using System.Collections.Generic;
using System.Media;
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

		public ModalState(string title, string message,
			string buttonText, Action buttonCallback,
			Action customBodyUI = null)
		{
			Title = title;
			Message = message;
			CustomBodyUI = customBodyUI;
			Buttons = new List<ButtonData>(1)
			{
				new(buttonText, buttonCallback),
			};
		}

		public ModalState(string title, string message,
			string button1Text, Action button1Callback,
			string button2Text, Action button2Callback,
			Action customBodyUI = null)
		{
			Title = title;
			Message = message;
			CustomBodyUI = customBodyUI;
			Buttons = new List<ButtonData>(2)
			{
				new(button1Text, button1Callback),
				new(button2Text, button2Callback),
			};
		}

		public ModalState(string title, string message,
			string button1Text, Action button1Callback,
			string button2Text, Action button2Callback,
			string button3Text, Action button3Callback,
			Action customBodyUI = null)
		{
			Title = title;
			Message = message;
			CustomBodyUI = customBodyUI;
			Buttons = new List<ButtonData>(3)
			{
				new(button1Text, button1Callback),
				new(button2Text, button2Callback),
				new(button3Text, button3Callback),
			};
		}

		public readonly string Title;
		public readonly string Message;
		public readonly List<ButtonData> Buttons;
		public readonly Action CustomBodyUI;
	}

	/// <summary>
	/// Editor instance. Needed for bounds.
	/// </summary>
	private static Editor Editor;

	/// <summary>
	/// All currently active modals.
	/// </summary>
	private static readonly List<ModalState> Modals = new();

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
	public static void Draw()
	{
		// Draw modals in reverse order.
		for (var i = Modals.Count - 1; i >= 0; i--)
		{
			var modal = Modals[i];
			if (BeginModal(modal.Title))
			{
				// Draw the message.
				ImGui.TextWrapped(modal.Message);

				// Draw any custom body UI.
				if (modal.CustomBodyUI != null)
				{
					ImGui.Separator();
					modal.CustomBodyUI();
				}

				// Draw the buttons. If any button is pressed, close the modal.
				if (DrawButtonsRow(modal.Buttons))
					Modals.RemoveAt(i);
				ImGui.EndPopup();
			}
		}
	}

	public static void OpenModalOneButton(string title, string message,
		string buttonText, Action buttonCallback,
		Action customBodyUI = null)
	{
		SystemSounds.Exclamation.Play();
		Modals.Add(new ModalState(title, message,
			buttonText, buttonCallback,
			customBodyUI));
	}

	public static void OpenModalTwoButtons(string title, string message,
		string button1Text, Action button1Callback,
		string button2Text, Action button2Callback,
		Action customBodyUI = null)
	{
		SystemSounds.Exclamation.Play();
		Modals.Add(new ModalState(title, message,
			button1Text, button1Callback,
			button2Text, button2Callback,
			customBodyUI));
	}

	public static void OpenModalThreeButtons(string title, string message,
		string button1Text, Action button1Callback,
		string button2Text, Action button2Callback,
		string button3Text, Action button3Callback,
		Action customBodyUI = null)
	{
		SystemSounds.Exclamation.Play();
		Modals.Add(new ModalState(title, message,
			button1Text, button1Callback,
			button2Text, button2Callback,
			button3Text, button3Callback,
			customBodyUI));
	}

	/// <summary>
	/// Begin drawing a modal window.
	/// </summary>
	/// <param name="title">Title of the window.</param>
	/// <returns>True if the window is open.</returns>
	private static bool BeginModal(string title)
	{
		var screenW = Editor.GetViewportWidth();
		var screenH = Editor.GetViewportHeight();

		var windowPos = new Vector2((screenW - DefaultSize.X) * 0.5f, (screenH - DefaultSize.Y) * 0.5f);

		var openFlag = true;
		ImGui.OpenPopup(title);
		ImGui.SetNextWindowSize(DefaultSize);
		ImGui.SetNextWindowPos(windowPos, ImGuiCond.Always);
		var open = ImGui.BeginPopupModal(title, ref openFlag,
			ImGuiWindowFlags.NoResize |
			ImGuiWindowFlags.Modal |
			ImGuiWindowFlags.NoMove |
			ImGuiWindowFlags.NoDecoration);

		if (open)
		{
			ImGui.Text(title);
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

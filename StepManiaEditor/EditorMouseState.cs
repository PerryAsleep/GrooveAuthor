﻿using System.Collections.Generic;
using ImGuiNET;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace StepManiaEditor;

internal interface IReadOnlyEditorMouseState
{
	public EditorButtonState GetButtonState(EditorMouseState.Button button);

	public int X();

	public int Y();

	public IReadOnlyEditorPosition GetEditorPosition();

	public int ScrollDeltaSinceLastFrame();
}

/// <summary>
/// State for one mouse button.
/// </summary>
internal sealed class EditorButtonState
{
	private bool IsDown;
	private bool PreviousDown;
	private bool InFocus;
	private Vector2 LastClickDownPosition;
	private Vector2 LastClickUpPosition;
	private readonly int ImGuiMouseButtonIndex;

	public EditorButtonState(int imGuiMouseButtonIndex)
	{
		ImGuiMouseButtonIndex = imGuiMouseButtonIndex;
	}

	public void Update(bool down, bool inFocus, int x, int y)
	{
		var lostFocusWhileDown = InFocus && !inFocus && IsDown;

		PreviousDown = IsDown;
		IsDown = down;
		InFocus = inFocus;
		if (DownThisFrame())
		{
			LastClickDownPosition = new Vector2(x, y);
			ImGui.GetIO().AddMouseButtonEvent(ImGuiMouseButtonIndex, true);
		}

		if (UpThisFrame())
		{
			LastClickUpPosition = new Vector2(x, y);
			ImGui.GetIO().AddMouseButtonEvent(ImGuiMouseButtonIndex, false);
		}

		// Internally Dear ImGui has handling for cancelling input when the application
		// loses focus, but in practice even when calling ClearInputKeys directly ImGui
		// still retains some data as if it thinks a button is down. For example, if the
		// user is holding left the left mouse button to move a Window, then uses alt+tab
		// to background the application, then releases the left mouse button, then
		// alt+tabs back, the Window will continue to move with the mouse. In order to
		// prevent this behavior, tell ImGui to release a button if we lose focus.
		if (lostFocusWhileDown)
		{
			ImGui.GetIO().AddMouseButtonEvent(ImGuiMouseButtonIndex, false);
		}
	}

	public bool DownThisFrame()
	{
		return InFocus && IsDown && !PreviousDown;
	}

	public bool UpThisFrame()
	{
		return InFocus && !IsDown && PreviousDown;
	}

	public bool Down()
	{
		return InFocus && IsDown;
	}

	public bool Up()
	{
		return InFocus && !IsDown;
	}

	public Vector2 GetLastClickDownPosition()
	{
		return LastClickDownPosition;
	}

	public Vector2 GetLastClickUpPosition()
	{
		return LastClickUpPosition;
	}

	public bool ClickedThisFrame()
	{
		return UpThisFrame() && LastClickUpPosition == LastClickDownPosition;
	}
}

/// <summary>
/// Class to hold mouse state for the Editor.
/// Tracks the EditorPosition for the mouse.
/// Forwards mouse input to ImGui on Update.
/// Expected Usage:
///  Call SetActiveChart() when the active chart changes.
///  Call Update() once per frame with the current mouse state.
/// </summary>
internal sealed class EditorMouseState : IReadOnlyEditorMouseState
{
	/// <summary>
	/// Mouse Buttons.
	/// </summary>
	internal enum Button
	{
		Left,
		Right,
		Middle,
		X1,
		X2,
	}

	/// <summary>
	/// The EditorPosition for the mouse based on the current position and the active chart.
	/// </summary>
	private readonly EditorPosition Position = new(null, null);

	// Mouse state.
	private MouseState CurrentMouseState;
	private MouseState PreviousMouseState;

	private readonly Dictionary<Button, EditorButtonState> States = new()
	{
		[Button.Left] = new EditorButtonState(0),
		[Button.Right] = new EditorButtonState(1),
		[Button.Middle] = new EditorButtonState(2),
		[Button.X1] = new EditorButtonState(3),
		[Button.X2] = new EditorButtonState(4),
	};

	public void SetActiveChart(EditorChart activeChart)
	{
		Position.ActiveChart = activeChart;
	}

	public void Update(MouseState currentMouseState, double chartTime, double chartPosition, bool inFocus)
	{
		PreviousMouseState = CurrentMouseState;
		CurrentMouseState = currentMouseState;

		var x = CurrentMouseState.X;
		var y = CurrentMouseState.Y;

		// Forward mouse events to ImGui when in focus.
		if (inFocus)
		{
			var detentVal = GetDefaultScrollDetentValue();
			var horizontalVal =
				(CurrentMouseState.HorizontalScrollWheelValue - PreviousMouseState.HorizontalScrollWheelValue) / detentVal;
			var verticalDelta = (CurrentMouseState.ScrollWheelValue - PreviousMouseState.ScrollWheelValue) / detentVal;
			ImGui.GetIO().AddMousePosEvent(x, y);
			ImGui.GetIO().AddMouseWheelEvent(horizontalVal, verticalDelta);
		}

		// Update each button.
		States[Button.Left].Update(CurrentMouseState.LeftButton == ButtonState.Pressed, inFocus, x,
			y);
		States[Button.Right].Update(CurrentMouseState.RightButton == ButtonState.Pressed, inFocus,
			x, y);
		States[Button.Middle].Update(CurrentMouseState.MiddleButton == ButtonState.Pressed, inFocus,
			x, y);
		States[Button.X1].Update(CurrentMouseState.XButton1 == ButtonState.Pressed, inFocus, x, y);
		States[Button.X2].Update(CurrentMouseState.XButton2 == ButtonState.Pressed, inFocus, x, y);

		// Update the Position.
		if (Position.ActiveChart == null)
		{
			Position.ChartTime = chartTime;
			Position.ChartPosition = chartPosition;
		}
		else if (Preferences.Instance.PreferencesScroll.SpacingMode == Editor.SpacingMode.ConstantTime)
		{
			Position.ChartTime = chartTime;
		}
		else
		{
			Position.ChartPosition = chartPosition;
		}
	}

	public EditorButtonState GetButtonState(Button button)
	{
		return States[button];
	}

	public int X()
	{
		return CurrentMouseState.Position.X;
	}

	public int Y()
	{
		return CurrentMouseState.Position.Y;
	}

	public IReadOnlyEditorPosition GetEditorPosition()
	{
		return Position;
	}

	public int ScrollDeltaSinceLastFrame()
	{
		return CurrentMouseState.ScrollWheelValue - PreviousMouseState.ScrollWheelValue;
	}

	public static int GetDefaultScrollDetentValue()
	{
		// 120 units is the default scroll amount reported by a mouse per detent on Windows.
		// See WHEEL_DELTA and https://learn.microsoft.com/en-us/windows/win32/inputdev/wm-mousewheel
		return 120;
	}
}

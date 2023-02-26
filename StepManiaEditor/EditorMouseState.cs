using ImGuiNET;
using Microsoft.Xna.Framework.Input;
using System.Collections.Generic;
using Vector2 = Microsoft.Xna.Framework.Vector2;

namespace StepManiaEditor
{
	/// <summary>
	/// Class to hold mouse state for the Editor.
	/// Tracks the EditorPosition for the mouse.
	/// Forwards mouse input to ImGui on Update.
	/// Expected Usage:
	///  Call SetActiveChart() when the active chart changes.
	///  Call Update() once per frame with the current mouse state.
	/// </summary>
	internal sealed class EditorMouseState
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
		/// State for one mouse button.
		/// </summary>
		internal sealed class ButtonState
		{
			private bool IsDown;
			private bool PreviousDown;
			private bool InFocus;
			private Vector2 LastClickDownPosition = new Vector2();
			private Vector2 LastClickUpPosition = new Vector2();
			private readonly int ImGuiMouseButtonIndex;

			public ButtonState(int imGuiMouseButtonIndex)
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
		}

		/// <summary>
		/// The EditorPosition for the mouse based on the current position and the active chart.
		/// </summary>
		private EditorPosition Position = new EditorPosition(null);

		// Mouse state.
		private MouseState CurrentMouseState;
		private MouseState PreviousMouseState;
		private Dictionary<Button, ButtonState> States;

		public EditorMouseState()
		{
			States = new Dictionary<Button, ButtonState>();
			States[Button.Left] = new ButtonState(0);
			States[Button.Right] = new ButtonState(1);
			States[Button.Middle] = new ButtonState(2);
			States[Button.X1] = new ButtonState(3);
			States[Button.X2] = new ButtonState(4);
		}

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
				ImGui.GetIO().AddMousePosEvent(x, y);
				ImGui.GetIO().AddMouseWheelEvent(
					(CurrentMouseState.HorizontalScrollWheelValue - PreviousMouseState.HorizontalScrollWheelValue) / GetDefaultScrollDetentValue(),
					(CurrentMouseState.ScrollWheelValue - PreviousMouseState.ScrollWheelValue) / GetDefaultScrollDetentValue());
			}

			// Update each button.
			States[Button.Left].Update(CurrentMouseState.LeftButton == Microsoft.Xna.Framework.Input.ButtonState.Pressed, inFocus, x, y);
			States[Button.Right].Update(CurrentMouseState.RightButton == Microsoft.Xna.Framework.Input.ButtonState.Pressed, inFocus, x, y);
			States[Button.Middle].Update(CurrentMouseState.MiddleButton == Microsoft.Xna.Framework.Input.ButtonState.Pressed, inFocus, x, y);
			States[Button.X1].Update(CurrentMouseState.XButton1 == Microsoft.Xna.Framework.Input.ButtonState.Pressed, inFocus, x, y);
			States[Button.X2].Update(CurrentMouseState.XButton2 == Microsoft.Xna.Framework.Input.ButtonState.Pressed, inFocus, x, y);

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

		public ButtonState GetButtonState(Button button)
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

		public EditorPosition GetEditorPosition()
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
}

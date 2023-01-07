using Microsoft.Xna.Framework.Input;
using Vector2 = Microsoft.Xna.Framework.Vector2;

namespace StepManiaEditor
{
	/// <summary>
	/// Class to hold mouse state for the Editor.
	/// Expected Usage:
	///  Call SetActiveChart() when the active chart changes.
	///  Call UpdateMouseState() once per frame with the current mouse state.
	/// </summary>
	internal sealed class EditorMouseState
	{
		/// <summary>
		/// The EditorPosition for the mouse based on the current position and the active chart.
		/// </summary>
		private EditorPosition Position = new EditorPosition(null);

		// Mouse state.
		private MouseState PreviousMouseState;
		private MouseState CurrentMouseState;
		public Vector2 LastLeftClickDownPosition = new Vector2();
		public Vector2 LastLeftClickUpPosition = new Vector2();
		public Vector2 LastRightClickDownPosition = new Vector2();
		public Vector2 LastRightClickUpPosition = new Vector2();

		public void SetActiveChart(EditorChart activeChart)
		{
			Position.ActiveChart = activeChart;
		}

		public void UpdateMouseState(MouseState currentMouseState, double chartTime, double chartPosition)
		{
			PreviousMouseState = CurrentMouseState;
			CurrentMouseState = currentMouseState;

			if (LeftClickDownThisFrame())
				LastLeftClickDownPosition = new Vector2(X(), Y());
			if (LeftClickUpThisFrame())
				LastLeftClickUpPosition = new Vector2(X(), Y());
			if (RightClickDownThisFrame())
				LastRightClickDownPosition = new Vector2(X(), Y());
			if (RightClickUpThisFrame())
				LastRightClickUpPosition = new Vector2(X(), Y());

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

		public bool LeftClickDownThisFrame()
		{
			return CurrentMouseState.LeftButton == ButtonState.Pressed && PreviousMouseState.LeftButton == ButtonState.Released;
		}
		public bool LeftClickUpThisFrame()
		{
			return CurrentMouseState.LeftButton == ButtonState.Released && PreviousMouseState.LeftButton == ButtonState.Pressed;
		}
		public bool LeftDown()
		{
			return CurrentMouseState.LeftButton == ButtonState.Pressed;
		}
		public bool LeftReleased()
		{
			return CurrentMouseState.LeftButton == ButtonState.Released;
		}

		public bool RightClickDownThisFrame()
		{
			return CurrentMouseState.RightButton == ButtonState.Pressed && PreviousMouseState.RightButton == ButtonState.Released;
		}
		public bool RightClickUpThisFrame()
		{
			return CurrentMouseState.RightButton == ButtonState.Released && PreviousMouseState.RightButton == ButtonState.Pressed;
		}
		public bool RightDown()
		{
			return CurrentMouseState.RightButton == ButtonState.Pressed;
		}
		public bool RightReleased()
		{
			return CurrentMouseState.RightButton == ButtonState.Released;
		}
	}
}

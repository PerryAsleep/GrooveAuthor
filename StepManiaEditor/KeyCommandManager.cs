using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework.Input;

namespace StepManiaEditor
{
	/// <summary>
	/// Handles registering and invoking Actions in response to configurable key presses.
	///
	/// Expected Usage:
	///  Register Commands with Register.
	///  Call Update once each frame.
	/// </summary>
	public class KeyCommandManager
	{
		/// <summary>
		/// For repeatable Commands, number of seconds the keys need to be held for before the first repeat occurs.
		/// </summary>
		private const double RepeatTimeFirst = 0.5;
		/// <summary>
		/// For repeatable Commands, number of seconds the keys need to be held for to trigger the Command again
		/// after the first repeat.
		/// </summary>
		private const double RepeatTimeSubsequent = 0.02;

		/// <summary>
		/// Command configuration. Used to specify a sequence of keys that when pressed
		/// together should invoke a callback Action.
		/// </summary>
		public class Command
		{
			/// <summary>
			/// Action to invoke when the Command is activated.
			/// </summary>
			public readonly Action Callback;
			/// <summary>
			/// Sequence of Keys which much be pressed to activate the Command.
			/// Order is important in that that last Keys must be pressed last.
			/// </summary>
			public readonly Keys[] Input;
			/// <summary>
			/// Whether or not this Command should repeatedly invoke the Callback
			/// while the Input is held.
			/// </summary>
			public readonly bool Repeat;

			public Command(Keys[] input, Action callback, bool repeat = false)
			{
				Input = input;
				Callback = callback;
				Repeat = repeat;
			}
		}

		/// <summary>
		/// State for each Command registered in the KeyCommandManager.
		/// </summary>
		private class CommandState
		{
			public readonly Command Command;

			private const double UnsetTime = -1.0;

			private double StartTimeAnyHeld = UnsetTime;
			private double StartTimeLastHeld = UnsetTime;
			private double NextTriggerTime = UnsetTime;
			private bool Active;

			public CommandState(Command command)
			{
				Command = command;
			}

			/// <summary>
			/// Returns whether or not this Command is active.
			/// </summary>
			/// <returns>True if this Command is active and false otherwise.</returns>
			public bool IsActive()
			{
				return Active;
			}

			/// <summary>
			/// Update the Command.
			/// Will check input, update internal state, and trigger the Command's callback if appropriate.
			/// </summary>
			/// <param name="timeInSeconds">Current time in seconds.</param>
			/// <param name="canActivate">Whether or not this Command can be activated.</param>
			/// <param name="state">KeyboardState for checking input keys.</param>
			public void Update(double timeInSeconds, bool canActivate, ref KeyboardState state)
			{
				// Loop over every key in the inputs and record the current state.
				var allDown = true;
				var allUp = true;
				for (var i = 0; i < Command.Input.Length; i++)
				{
					var last = i == Command.Input.Length - 1;
					if (state.IsKeyDown(Command.Input[i]))
					{
						allUp = false;
						if (StartTimeAnyHeld < 0)
							StartTimeAnyHeld = timeInSeconds;
						if (last && StartTimeLastHeld < 0)
							StartTimeLastHeld = timeInSeconds;
					}
					else
					{
						allDown = false;
						if (last)
							StartTimeLastHeld = UnsetTime;
					}
				}

				// If all the keys are down and the final key in the sequence was the last key pressed,
				// then the inputs for this Command are active.
				var inputsActive = allDown && StartTimeLastHeld >= StartTimeAnyHeld;

				// Handle deactivating due to no longer holding all input keys.
				if (!allDown)
				{
					Active = false;
				}

				if (allUp)
				{
					StartTimeAnyHeld = UnsetTime;
				}

				// Handle activating.
				if (canActivate && inputsActive && !Active)
				{
					// Record that the Command is active.
					Active = true;
					
					// Trigger the callback.
					Command.Callback();

					// Set up the next time to trigger the callback if this Command repeats.
					if (Command.Repeat)
						NextTriggerTime = timeInSeconds + RepeatTimeFirst;
				}

				// Handle repeating Commands.
				if (Command.Repeat && inputsActive && Active && NextTriggerTime < timeInSeconds)
				{
					// Trigger the callback.
					Command.Callback();
					// Set up the next time to trigger.
					NextTriggerTime += RepeatTimeSubsequent;
				}
			}

			/// <summary>
			/// Cancels the command.
			/// </summary>
			public void Cancel()
			{
				StartTimeAnyHeld = UnsetTime;
				StartTimeLastHeld = UnsetTime;
				NextTriggerTime = UnsetTime;
				Active = false;
			}
		}

		/// <summary>
		/// CommandStates for all registered Commands. Processed in order.
		/// </summary>
		private List<CommandState> Commands = new List<CommandState>();
		private bool CommandsDirty = false;

		/// <summary>
		/// Cancels all commands.
		/// </summary>
		public void CancelAllCommands()
		{
			foreach (var command in Commands)
			{
				command.Cancel();
			}
		}

		/// <summary>
		/// Update method. Checks key state and processes commands.
		/// </summary>
		/// <param name="timeInSeconds">
		/// A time value in seconds. Expected to be no less than the value provided previously.
		/// </param>
		public void Update(double timeInSeconds)
		{
			if (CommandsDirty)
			{
				// Sort the Commands by the length of their inputs.
				// This ensures that commands which are subsets of other commands aren't triggered in the wrong order.
				// For example, when holding Ctrl + Shift + Z, Ctrl + Z should not trigger.
				Commands = Commands.OrderByDescending(commandState => commandState.Command.Input.Length).ToList();
				CommandsDirty = false;
			}

			var state = Keyboard.GetState();

			// Check if any Command is active.
			// Active Commands prevent other Commands from activating.
			var anyActive = false;
			foreach (var command in Commands)
			{
				if (command.IsActive())
				{
					anyActive = true;
					break;
				}
			}

			// Handle each Command.
			foreach (var command in Commands)
			{
				command.Update(timeInSeconds, !anyActive, ref state);
				anyActive |= command.IsActive();
			}
		}

		/// <summary>
		/// Registers the given Command.
		/// </summary>
		/// <param name="command">Command to register.</param>
		public void Register(Command command)
		{
			// Add the command at the end of the list and set a flag
			// so that we can sort the commands the next time we update.
			Commands.Add(new CommandState(command));
			CommandsDirty = true;
		}

		public bool IsKeyDown(Keys key)
		{
			return Keyboard.GetState().IsKeyDown(key);
		}
	}
}

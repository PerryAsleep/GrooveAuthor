using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework.Input;

namespace StepManiaEditor;

public interface IReadOnlyKeyCommandManager
{
	public bool IsAnyInputDown(List<Keys[]> inputs);
}

/// <summary>
/// Handles registering and invoking Actions in response to configurable key presses.
///
/// Expected Usage:
///  Register Commands with Register.
///  Call Update once each frame.
/// </summary>
internal sealed class KeyCommandManager : IReadOnlyKeyCommandManager
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
		/// Action to invoke when the Command is deactivated.
		/// </summary>
		public readonly Action ReleaseCallback;

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

		public Command(Keys[] input, Action callback, bool repeat = false, Action releaseCallback = null)
		{
			Input = new Keys[input.Length];
			Array.Copy(input, Input, input.Length);
			Callback = callback;
			ReleaseCallback = releaseCallback;
			Repeat = repeat;
		}

		/// <summary>
		/// Returns whether this Command's Inputs are a subset of the given other Command's Inputs.
		/// </summary>
		/// <param name="other">Other Command to check.</param>
		/// <returns>True if this Command's Inputs are a subset of the other's and false otherwise.</returns>
		public bool IsInputSubsetOfOther(Command other)
		{
			if (Input.Length >= other.Input.Length)
				return false;
			for (var i = 0; i < Input.Length; i++)
			{
				var keyInOther = false;
				for (var j = 0; j < other.Input.Length; j++)
				{
					if (Input[i] == other.Input[j])
					{
						keyInOther = true;
						break;
					}
				}

				if (!keyInOther)
					return false;
			}

			return true;
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
		private bool ActivatedWhileOtherBlockingCommandActive;
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
		/// <param name="activeCommands">All currently active Commands.</param>
		/// <param name="keyCommandManager">Parent KeyCommandManager to use for checking input against previous state.</param>
		/// <param name="state">KeyboardState for checking input keys.</param>
		public void Update(double timeInSeconds, HashSet<CommandState> activeCommands, KeyCommandManager keyCommandManager,
			ref KeyboardState state)
		{
			var canActivate = true;
			foreach (var activeCommand in activeCommands)
			{
				if (activeCommand == this)
					continue;
				if (Command.IsInputSubsetOfOther(activeCommand.Command)
				    || activeCommand.Command.IsInputSubsetOfOther(Command))
				{
					canActivate = false;
					break;
				}
			}

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
					{
						StartTimeAnyHeld = timeInSeconds;
					}

					if (last)
					{
						// If the last key in the sequence is being pressed now, but another command
						// is blocking input then record that scenario. In the future, when keys are
						// released and the other command no longer blocks, we do not want this command
						// to interpret remaining held keys as valid for activation. For example if
						// someone were to hold Ctrl+S, then release Ctrl and S is still held, we do not
						// want to activate a command that just requires pressing S.
						// See also https://github.com/PerryAsleep/GrooveAuthor/issues/7
						if (keyCommandManager.IsKeyDownThisFrame(Command.Input[i]) && !canActivate)
							ActivatedWhileOtherBlockingCommandActive = true;

						if (StartTimeLastHeld < 0)
							StartTimeLastHeld = timeInSeconds;
					}
				}
				else
				{
					allDown = false;
					if (last)
					{
						StartTimeLastHeld = UnsetTime;
						ActivatedWhileOtherBlockingCommandActive = false;
					}
				}
			}

			// Do not activate if this command was activated while another command was active.
			canActivate &= !ActivatedWhileOtherBlockingCommandActive;

			// If all the keys are down and the final key in the sequence was the last key pressed,
			// then the inputs for this Command are active.
			var inputsActive = allDown && StartTimeLastHeld >= StartTimeAnyHeld;

			// Handle deactivating due to no longer holding all input keys.
			if (!allDown)
			{
				if (Active)
				{
					Active = false;
					Command.ReleaseCallback?.Invoke();
				}
			}

			if (allUp)
			{
				StartTimeAnyHeld = UnsetTime;
				ActivatedWhileOtherBlockingCommandActive = false;
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
			ActivatedWhileOtherBlockingCommandActive = false;
		}
	}

	/// <summary>
	/// CommandStates for all registered Commands. Processed in order.
	/// </summary>
	private List<CommandState> Commands = new();

	private bool CommandsDirty;

	private KeyboardState PreviousState;

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
		var activeCommands = new HashSet<CommandState>();
		foreach (var command in Commands)
		{
			if (command.IsActive())
			{
				activeCommands.Add(command);
				break;
			}
		}

		// Handle each Command.
		foreach (var command in Commands)
		{
			command.Update(timeInSeconds, activeCommands, this, ref state);
			if (command.IsActive())
			{
				activeCommands.Add(command);
			}
		}

		PreviousState = state;
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

	private bool IsKeyDownThisFrame(Keys key)
	{
		return IsKeyDown(key) && !PreviousState.IsKeyDown(key);
	}

	private bool IsKeyDown(Keys key)
	{
		switch (key)
		{
			case Keys.LeftControl:
			case Keys.RightControl:
				return Keyboard.GetState().IsKeyDown(Keys.LeftControl) || Keyboard.GetState().IsKeyDown(Keys.RightControl);
			case Keys.LeftShift:
			case Keys.RightShift:
				return Keyboard.GetState().IsKeyDown(Keys.LeftShift) || Keyboard.GetState().IsKeyDown(Keys.RightShift);
			case Keys.LeftAlt:
			case Keys.RightAlt:
				return Keyboard.GetState().IsKeyDown(Keys.LeftAlt) || Keyboard.GetState().IsKeyDown(Keys.RightAlt);
			case Keys.LeftWindows:
			case Keys.RightWindows:
				return Keyboard.GetState().IsKeyDown(Keys.LeftWindows) || Keyboard.GetState().IsKeyDown(Keys.RightWindows);
			default:
				return Keyboard.GetState().IsKeyDown(key);
		}
	}

	public bool IsAnyInputDown(List<Keys[]> inputs)
	{
		foreach (var input in inputs)
		{
			var inputDown = true;
			foreach (var key in input)
			{
				if (!IsKeyDown(key))
				{
					inputDown = false;
					break;
				}
			}

			if (inputDown)
				return true;
		}

		return false;
	}
}

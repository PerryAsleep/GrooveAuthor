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
internal sealed class KeyCommandManager : IReadOnlyKeyCommandManager, Fumen.IObserver<PreferencesKeyBinds>
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

	#region Commands

	/// <summary>
	/// Command configuration. Used to specify a sequence of keys that when pressed
	/// together should invoke a callback Action.
	/// </summary>
	public class Command
	{
		/// <summary>
		/// User-facing command name.
		/// </summary>
		public readonly string Name;

		/// <summary>
		/// Internal key bind identifier.
		/// </summary>
		public readonly string Id;

		/// <summary>
		/// Action to invoke when the Command is activated.
		/// </summary>
		public readonly Action Callback;

		/// <summary>
		/// Action to invoke when the Command is deactivated.
		/// </summary>
		public readonly Action ReleaseCallback;

		/// <summary>
		/// Whether or not this Command should repeatedly invoke the Callback
		/// while the Input is held.
		/// </summary>
		public readonly bool Repeat;

		/// <summary>
		/// Whether or not this command blocks input to other commands when it is active.
		/// </summary>
		public readonly bool BlocksInput;

		public Command(string name, string id, Action callback, bool repeat = false,
			Action releaseCallback = null, bool blocksInput = true)
		{
			Name = name;
			Id = id;
			Callback = callback;
			ReleaseCallback = releaseCallback;
			Repeat = repeat;
			BlocksInput = blocksInput;
		}
	}

	/// <summary>
	/// State for each Command registered in the KeyCommandManager.
	/// </summary>
	private class CommandState
	{
		private const double UnsetTime = -1.0;

		public readonly Command Command;
		public readonly Keys[] Input;
		private double StartTimeAnyHeld = UnsetTime;
		private double StartTimeLastHeld = UnsetTime;
		private double NextTriggerTime = UnsetTime;
		private bool ActivatedWhileOtherBlockingCommandActive;
		private bool Active;

		public CommandState(Command command, Keys[] input)
		{
			Command = command;
			if (input == null || input.Length == 0)
			{
				Input = [];
			}
			else
			{
				Input = new Keys[input.Length];
				Array.Copy(input, Input, input.Length);
			}
		}

		/// <summary>
		/// Returns whether this Command's Inputs are a subset of the given other Command's Inputs.
		/// </summary>
		/// <param name="other">Other Command to check.</param>
		/// <returns>True if this Command's Inputs are a subset of the other's and false otherwise.</returns>
		private bool IsInputSubsetOfOther(CommandState other)
		{
			if (Input.Length >= other.Input.Length)
				return false;
			if (Input.Length == 0)
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


		/// <summary>
		/// Returns true if the given inputs would conflict with the other given input and prevent
		/// it from triggering. This means the given inputs are a subset of the other inputs, and they
		/// would end before the others, and the given inputs are configured to block input.
		/// </summary>
		/// <param name="blocksInput">Whether the given input should block other input.</param>
		/// <param name="input">The input to check to see if it conflicts with the other given input.</param>
		/// <param name="otherInput">The other input to check against the given input.</param>
		/// <returns>True if this Command's Inputs would prevent the other's from triggering and false otherwise.</returns>
		public static bool ConflictsWithOther(bool blocksInput, Keys[] input, Keys[] otherInput)
		{
			if (!blocksInput)
				return false;
			if (input.Length == 0 || otherInput.Length == 0)
				return false;
			if (input.Length > otherInput.Length)
				return false;

			// The given input is the same length as the other.
			// For the given input to conflict with the other they
			// must contain all the same keys.
			if (input.Length == otherInput.Length)
			{
				for (var i = 0; i < input.Length; i++)
				{
					var keyInOther = false;
					for (var j = 0; j < otherInput.Length; j++)
					{
						if (input[i] == otherInput[j])
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

			// The given input is shorter than the other.
			// For the given input to conflict with the other all the inputs
			// from the given input must be in the other before the final input
			// of the other.
			for (var i = 0; i < input.Length; i++)
			{
				var keyInOther = false;
				for (var j = 0; j < otherInput.Length; j++)
				{
					if (input[i] == otherInput[j])
					{
						if (j == otherInput.Length - 1)
							return false;
						keyInOther = true;
						break;
					}
				}

				if (!keyInOther)
					return false;
			}

			return true;
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
			if (Input.Length == 0)
				return;

			var canActivate = true;
			foreach (var activeCommand in activeCommands)
			{
				if (activeCommand == this || !activeCommand.Command.BlocksInput)
					continue;
				if (IsInputSubsetOfOther(activeCommand) || activeCommand.IsInputSubsetOfOther(this))
				{
					canActivate = false;
					break;
				}
			}

			// Loop over every key in the inputs and record the current state.
			var allDown = true;
			var allUp = true;
			for (var i = 0; i < Input.Length; i++)
			{
				var last = i == Input.Length - 1;
				if (IsKeyDown(ref state, Input[i]))
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
						if (keyCommandManager.IsKeyDownThisFrame(Input[i]) && !canActivate)
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

	#endregion Commands

	/// <summary>
	/// CommandStates for all registered Commands. Processed in order.
	/// </summary>
	private List<CommandState> CommandStates = [];

	/// <summary>
	/// All Commands keyed by their Id.
	/// </summary>
	private readonly Dictionary<string, Command> Commands = new();

	private bool CommandsDirty;
	private KeyboardState PreviousState;
	private bool IsRebinding;

	public KeyCommandManager()
	{
		Preferences.Instance.PreferencesKeyBinds.AddObserver(this);
	}

	/// <summary>
	/// Cancels all commands.
	/// </summary>
	public void CancelAllCommands()
	{
		foreach (var state in CommandStates)
			state.Cancel();
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
			CommandStates = CommandStates.OrderByDescending(commandState => commandState.Input.Length).ToList();
			CommandsDirty = false;
		}

		// Do not process any input while rebinding keys.
		// Ideally this would use the observer pattern but UIKeyRebindModal is static.
		if (IsRebinding)
			return;

		var state = Keyboard.GetState();

		// Check if any Command is active.
		// Active Commands prevent other Commands from activating.
		var activeCommands = new HashSet<CommandState>();
		foreach (var command in CommandStates)
		{
			if (command.IsActive())
			{
				activeCommands.Add(command);
				break;
			}
		}

		// Handle each Command.
		foreach (var command in CommandStates)
		{
			command.Update(timeInSeconds, activeCommands, this, ref state);

			// Commands can alter CommandStates. For example a command, Ctrl+Z, might undo an action
			// to change key bindings, which will call KeyBindingsChanged and mutate CommandStates.
			// Use the CommandsDirty flag to detect this and break.
			if (CommandsDirty)
				break;

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
		Commands[command.Id] = command;
		AddStateForCommand(command);
		CommandsDirty = true;
	}

	private void AddStateForCommand(Command command)
	{
		var p = Preferences.Instance.PreferencesKeyBinds;
		var inputs = Utils.GetValueFromFieldOrProperty<List<Keys[]>>(p, command.Id);
		foreach (var input in inputs)
			CommandStates.Add(new CommandState(command, input));
		CommandsDirty = true;
	}

	private bool IsKeyDownThisFrame(Keys key)
	{
		return IsKeyDown(key) && !IsKeyDown(ref PreviousState, key);
	}

	private static bool IsKeyDown(Keys key)
	{
		var state = Keyboard.GetState();
		return IsKeyDown(ref state, key);
	}

	private static bool IsKeyDown(ref KeyboardState state, Keys key)
	{
		switch (key)
		{
			case Keys.LeftControl:
			case Keys.RightControl:
				return state.IsKeyDown(Keys.LeftControl) || state.IsKeyDown(Keys.RightControl);
			case Keys.LeftShift:
			case Keys.RightShift:
				return state.IsKeyDown(Keys.LeftShift) || state.IsKeyDown(Keys.RightShift);
			case Keys.LeftAlt:
			case Keys.RightAlt:
				return state.IsKeyDown(Keys.LeftAlt) || state.IsKeyDown(Keys.RightAlt);
			case Keys.LeftWindows:
			case Keys.RightWindows:
				return state.IsKeyDown(Keys.LeftWindows) || state.IsKeyDown(Keys.RightWindows);
			default:
				return state.IsKeyDown(key);
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

	/// <summary>
	/// Gets all commands which conflict with the given input.
	/// </summary>
	/// <param name="id">Id of the key binding.</param>
	/// <param name="input">Input sequence for the given key binding.</param>
	/// <returns>List of all commands conflicting with the given input, represented by their names.</returns>
	public List<string> GetConflictingCommands(string id, Keys[] input)
	{
		var blocksInput = Preferences.Instance.PreferencesKeyBinds.BlocksInput(id);
		var matches = new List<string>();
		var givenInputIsRegistered = false;
		foreach (var commandState in CommandStates)
		{
			// Ignore the given command. Ideally we only ignore the one specific input being checked rather
			// than all potential inputs for the command, but we don't have a good way of identifying them,
			// and it is rare to have move than one per command, let alone one which conflicts with another
			// from the same command.
			if (commandState.Command.Id == id)
			{
				givenInputIsRegistered = true;
				continue;
			}

			if (!matches.Contains(commandState.Command.Name) && (
				    CommandState.ConflictsWithOther(blocksInput, input, commandState.Input)
				    || CommandState.ConflictsWithOther(commandState.Command.BlocksInput, commandState.Input, input)))
			{
				matches.Add(commandState.Command.Name);
			}
		}

		// If the given input isn't even registered with the KeyCommandManager it can't conflict with anything.
		if (!givenInputIsRegistered)
			matches.Clear();

		return matches;
	}

	#region IObserver

	public void OnNotify(string eventId, PreferencesKeyBinds notifier, object payload)
	{
		if (eventId == PreferencesKeyBinds.NotificationKeyBindingChanged)
		{
			var preferenceName = (string)payload;
			if (!Commands.TryGetValue(preferenceName, out var command))
				return;

			// Remove state associated with the changed command.
			for (var i = CommandStates.Count - 1; i >= 0; i--)
				if (CommandStates[i].Command.Name == command.Name)
					CommandStates.RemoveAt(i);

			// Add state for the new inputs for the command.
			AddStateForCommand(command);
			CommandsDirty = true;
		}
	}

	public void NotifyRebindingStart()
	{
		IsRebinding = true;
	}

	public void NotifyRebindingEnd()
	{
		IsRebinding = false;
	}

	#endregion #IObserver
}

using System;
using System.Reflection;
using Fumen.ChartDefinition;

namespace StepManiaEditor
{
	/// <summary>
	/// An action that can be done and undone.
	/// Meant to be used by ActionQueue.
	/// </summary>
	public abstract class EditorAction
	{
		public abstract void Do();
		public abstract void Undo();
	}

	/// <summary>
	/// EditorAction to set a Field or a Property for a value type on an object.
	/// </summary>
	/// <typeparam name="T">
	/// Reference type of object field or property.
	/// </typeparam>
	public class ActionSetObjectFieldOrPropertyValue<T> : EditorAction where T : struct
	{
		private readonly T Value;
		private readonly T PreviousValue;
		private readonly object O;
		private readonly string FieldOrPropertyName;
		private readonly bool IsField;
		private readonly FieldInfo FieldInfo;
		private readonly PropertyInfo PropertyInfo;

		/// <summary>
		/// Constructor with a given value to set.
		/// It is assumed that a public instance field or property exists on the object with the given fieldOrPropertyName.
		/// </summary>
		/// <param name="o">Object to modify.</param>
		/// <param name="fieldOrPropertyName">Name of Field or Property on the object to modify.</param>
		/// <param name="value">New value to set.</param>
		public ActionSetObjectFieldOrPropertyValue(object o, string fieldOrPropertyName, T value)
		{
			O = o;
			Value = value;
			FieldOrPropertyName = fieldOrPropertyName;

			FieldInfo = O.GetType().GetField(FieldOrPropertyName, BindingFlags.Public | BindingFlags.Instance);
			IsField = FieldInfo != null;
			if (!IsField)
				PropertyInfo = O.GetType().GetProperty(FieldOrPropertyName, BindingFlags.Public | BindingFlags.Instance);

			PreviousValue = IsField ? (T)FieldInfo.GetValue(O) : (T)PropertyInfo.GetValue(O);
		}

		/// <summary>
		/// Constructor with a given value and previous value to set.
		/// It is assumed value is a Clone of the value.
		/// It is assumed previousValue is a Clone of the previous value.
		/// It is assumed that a public instance field or property exists on the object with the given fieldOrPropertyName.
		/// </summary>
		/// <param name="o"></param>
		/// <param name="fieldOrPropertyName"></param>
		/// <param name="value"></param>
		/// <param name="previousValue"></param>
		public ActionSetObjectFieldOrPropertyValue(object o, string fieldOrPropertyName, T value, T previousValue)
		{
			O = o;
			Value = value;
			FieldOrPropertyName = fieldOrPropertyName;

			FieldInfo = O.GetType().GetField(FieldOrPropertyName, BindingFlags.Public | BindingFlags.Instance);
			IsField = FieldInfo != null;
			if (!IsField)
				PropertyInfo = O.GetType().GetProperty(FieldOrPropertyName, BindingFlags.Public | BindingFlags.Instance);

			PreviousValue = previousValue;
		}

		public override string ToString()
		{
			return $"Set {O.GetType()} {FieldOrPropertyName} '{PreviousValue}' > '{Value}'.";
		}

		public override void Do()
		{
			// Set Value on O.
			if (IsField)
				FieldInfo.SetValue(O, Value);
			else
				PropertyInfo.SetValue(O, Value);
		}

		public override void Undo()
		{
			// Set PreviousValue on O.
			if (IsField)
				FieldInfo.SetValue(O, PreviousValue);
			else
				PropertyInfo.SetValue(O, PreviousValue);
		}
	}

	/// <summary>
	/// EditorAction to set a Field or a Property for a reference type on an object.
	/// </summary>
	/// <typeparam name="T">
	/// Reference type of object field or property.
	/// Must be Cloneable to ensure save undo and redo operations.
	/// </typeparam>
	public class ActionSetObjectFieldOrPropertyReference<T> : EditorAction where T : class, ICloneable
	{
		private readonly T Value;
		private readonly T PreviousValue;
		private readonly object O;
		private readonly string FieldOrPropertyName;
		private readonly bool IsField;
		private readonly FieldInfo FieldInfo;
		private readonly PropertyInfo PropertyInfo;

		/// <summary>
		/// Constructor with a given value to set.
		/// It is assumed value is a Clone of the value.
		/// It is assumed that a public instance field or property exists on the object with the given fieldOrPropertyName.
		/// </summary>
		/// <param name="o">Object to modify.</param>
		/// <param name="fieldOrPropertyName">Name of Field or Property on the object to modify.</param>
		/// <param name="value">New value to set.</param>
		public ActionSetObjectFieldOrPropertyReference(object o, string fieldOrPropertyName, T value)
		{
			O = o;
			Value = value;
			FieldOrPropertyName = fieldOrPropertyName;

			FieldInfo = O.GetType().GetField(FieldOrPropertyName, BindingFlags.Public | BindingFlags.Instance);
			IsField = FieldInfo != null;
			if (!IsField)
				PropertyInfo = O.GetType().GetProperty(FieldOrPropertyName, BindingFlags.Public | BindingFlags.Instance);

			// Clone the previous value.
			PreviousValue = IsField ? (T)FieldInfo.GetValue(O) : (T)PropertyInfo.GetValue(O);
			PreviousValue = (T)PreviousValue.Clone();
		}

		/// <summary>
		/// Constructor with a given value and previous value to set.
		/// It is assumed value is a Clone of the value.
		/// It is assumed previousValue is a Clone of the previous value.
		/// It is assumed that a public instance field or property exists on the object with the given fieldOrPropertyName.
		/// </summary>
		/// <param name="o"></param>
		/// <param name="fieldOrPropertyName"></param>
		/// <param name="value"></param>
		/// <param name="previousValue"></param>
		public ActionSetObjectFieldOrPropertyReference(object o, string fieldOrPropertyName, T value, T previousValue)
		{
			O = o;
			Value = value;
			FieldOrPropertyName = fieldOrPropertyName;

			FieldInfo = O.GetType().GetField(FieldOrPropertyName, BindingFlags.Public | BindingFlags.Instance);
			IsField = FieldInfo != null;
			if (!IsField)
				PropertyInfo = O.GetType().GetProperty(FieldOrPropertyName, BindingFlags.Public | BindingFlags.Instance);

			PreviousValue = previousValue;
		}

		public override string ToString()
		{
			return $"Set {O.GetType()} {FieldOrPropertyName} '{PreviousValue}' > '{Value}'.";
		}

		public override void Do()
		{
			// Clone Value to O.
			if (IsField)
				FieldInfo.SetValue(O, (T)Value.Clone());
			else
				PropertyInfo.SetValue(O, (T)Value.Clone());
		}

		public override void Undo()
		{
			// Clone PreviousValue to O.
			if (IsField)
				FieldInfo.SetValue(O, (T)PreviousValue.Clone());
			else
				PropertyInfo.SetValue(O, (T)PreviousValue.Clone());
		}
	}


	public class ActionSetExtrasValue<T> : EditorAction
	{
		private readonly T Value;
		private readonly T PreviousValue;
		private readonly bool PreviousValueSet;
		private readonly Extras Extras;
		private readonly string ExtrasKey;
		private readonly string LogType;

		public ActionSetExtrasValue(string logType, Extras extras, string extrasKey, T value)
		{
			LogType = logType;
			Extras = extras;
			Value = value;
			ExtrasKey = extrasKey;
			PreviousValueSet = Extras.TryGetSourceExtra(ExtrasKey, out PreviousValue);
		}

		public override string ToString()
		{
			return $"Set {LogType} {ExtrasKey} '{PreviousValue}' > '{Value}'.";
		}

		public override void Do()
		{
			Extras.AddSourceExtra(ExtrasKey, Value, true);
		}

		public override void Undo()
		{
			if (PreviousValueSet)
				Extras.AddSourceExtra(ExtrasKey, PreviousValue, true);
			else
				Extras.RemoveSourceExtra(ExtrasKey);
		}
	}
}

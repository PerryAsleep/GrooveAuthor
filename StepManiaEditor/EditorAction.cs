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
	/// EditorAction which sets a property on an object to a new value.
	/// </summary>
	/// <typeparam name="T">Type of the property.</typeparam>
	public class ActionSetObjectProperty<T> : EditorAction
	{
		private readonly T Value;
		private readonly T PreviousValue;
		private readonly object O;
		private readonly string PropertyName;
		private readonly PropertyInfo PropertyInfo;

		public ActionSetObjectProperty(object o, string propertyName, T value)
		{
			O = o;
			Value = value;
			PropertyName = propertyName;

			PropertyInfo = O.GetType().GetProperty(PropertyName, BindingFlags.Public | BindingFlags.Instance);
			PreviousValue = (T)PropertyInfo.GetValue(O);
		}

		public ActionSetObjectProperty(object o, string propertyName, T value, T previousValue)
		{
			O = o;
			Value = value;
			PropertyName = propertyName;

			PropertyInfo = O.GetType().GetProperty(PropertyName, BindingFlags.Public | BindingFlags.Instance);
			PreviousValue = previousValue;
		}

		public override string ToString()
		{
			return $"Set {O.GetType()} {PropertyName} '{PreviousValue}' > '{Value}'.";
		}

		public override void Do()
		{
			PropertyInfo.SetValue(O, Value, null);
		}

		public override void Undo()
		{
			PropertyInfo.SetValue(O, PreviousValue, null);
		}
	}

	/// <summary>
	/// EditorAction which sets a field on an object to a new value.
	/// </summary>
	/// <typeparam name="T">Type of the field.</typeparam>
	public class ActionSetObjectField<T> : EditorAction
	{
		private readonly T Value;
		private readonly T PreviousValue;
		private readonly object O;
		private readonly string FieldName;
		private readonly FieldInfo FieldInfo;

		public ActionSetObjectField(object o, string fieldName, T value)
		{
			O = o;
			Value = value;
			FieldName = fieldName;

			FieldInfo = O.GetType().GetField(FieldName, BindingFlags.Public | BindingFlags.Instance);
			PreviousValue = (T)FieldInfo.GetValue(O);
		}

		public ActionSetObjectField(object o, string fieldName, T value, T previousValue)
		{
			O = o;
			Value = value;
			FieldName = fieldName;

			FieldInfo = O.GetType().GetField(FieldName, BindingFlags.Public | BindingFlags.Instance);
			PreviousValue = previousValue;
		}

		public override string ToString()
		{
			return $"Set {O.GetType()} {FieldInfo} '{PreviousValue}' > '{Value}'.";
		}

		public override void Do()
		{
			FieldInfo.SetValue(O, Value);
		}

		public override void Undo()
		{
			FieldInfo.SetValue(O, PreviousValue);
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

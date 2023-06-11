using System;
using System.Diagnostics;
using System.Reflection;

namespace StepManiaEditor;

/// <summary>
/// EditorAction to set a Field or a Property for a reference type on an object.
/// </summary>
/// <typeparam name="T">
/// Reference type of object field or property.
/// Must be Cloneable to ensure save undo and redo operations.
/// </typeparam>
internal sealed class ActionSetObjectFieldOrPropertyReference<T> : EditorAction where T : class, ICloneable
{
	private readonly T Value;
	private readonly T PreviousValue;
	private readonly object O;
	private readonly string FieldOrPropertyName;
	private readonly bool IsField;
	private readonly FieldInfo FieldInfo;
	private readonly PropertyInfo PropertyInfo;
	private readonly bool DoesAffectFile;

	/// <summary>
	/// Constructor with a given value to set.
	/// It is assumed value is a Clone of the value.
	/// It is assumed that a public instance field or property exists on the object with the given fieldOrPropertyName.
	/// </summary>
	/// <param name="o">Object to modify.</param>
	/// <param name="fieldOrPropertyName">Name of Field or Property on the object to modify.</param>
	/// <param name="value">New value to set.</param>
	/// <param name="affectsFile">Whether or not this action represents a change to the file being edited.</param>
	public ActionSetObjectFieldOrPropertyReference(object o, string fieldOrPropertyName, T value, bool affectsFile) : base(false,
		false)
	{
		O = o;
		Value = value;
		FieldOrPropertyName = fieldOrPropertyName;

		FieldInfo = O.GetType().GetField(FieldOrPropertyName, BindingFlags.Public | BindingFlags.Instance);
		IsField = FieldInfo != null;
		if (!IsField)
			PropertyInfo = O.GetType().GetProperty(FieldOrPropertyName, BindingFlags.Public | BindingFlags.Instance);
		Debug.Assert(FieldInfo != null || PropertyInfo != null);

		// Clone the previous value.
		// ReSharper disable PossibleNullReferenceException
		PreviousValue = IsField ? (T)FieldInfo.GetValue(O) : (T)PropertyInfo.GetValue(O);
		// ReSharper restore PossibleNullReferenceException
		PreviousValue = (T)PreviousValue?.Clone();

		DoesAffectFile = affectsFile;
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
	/// <param name="affectsFile">Whether or not this action represents a change to the file being edited.</param>
	public ActionSetObjectFieldOrPropertyReference(object o, string fieldOrPropertyName, T value, T previousValue,
		bool affectsFile) : base(false, false)
	{
		O = o;
		Value = value;
		FieldOrPropertyName = fieldOrPropertyName;

		FieldInfo = O.GetType().GetField(FieldOrPropertyName, BindingFlags.Public | BindingFlags.Instance);
		IsField = FieldInfo != null;
		if (!IsField)
			PropertyInfo = O.GetType().GetProperty(FieldOrPropertyName, BindingFlags.Public | BindingFlags.Instance);

		PreviousValue = previousValue;

		DoesAffectFile = affectsFile;
	}

	public override bool AffectsFile()
	{
		return DoesAffectFile;
	}

	public override string ToString()
	{
		return $"Set {O.GetType()} {FieldOrPropertyName} '{PreviousValue}' > '{Value}'.";
	}

	protected override void DoImplementation()
	{
		// Clone Value to O.
		if (IsField)
			FieldInfo.SetValue(O, (T)Value?.Clone());
		else
			PropertyInfo.SetValue(O, (T)Value?.Clone());
	}

	protected override void UndoImplementation()
	{
		// Clone PreviousValue to O.
		if (IsField)
			FieldInfo.SetValue(O, (T)PreviousValue?.Clone());
		else
			PropertyInfo.SetValue(O, (T)PreviousValue?.Clone());
	}
}

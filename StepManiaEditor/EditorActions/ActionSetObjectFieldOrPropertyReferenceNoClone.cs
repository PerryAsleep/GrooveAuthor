using System.Diagnostics;
using System.Reflection;
using static StepManiaEditor.EditorActionUtils;

namespace StepManiaEditor;

/// <summary>
/// EditorAction to set a Field or a Property for a reference type on an object.
/// This action does not clone the reference type object. As such the responsibility is on the caller
/// to ensure that no state changes are made to the object outside of the ActionQueue that would
/// cause undo/redo to have unexpected behavior.
/// </summary>
/// <typeparam name="T">
/// Reference type of object field or property.
/// </typeparam>
internal sealed class ActionSetObjectFieldOrPropertyReferenceNoClone<T> : EditorAction where T : class
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
	/// It is assumed that a public instance field or property exists on the object with the given fieldOrPropertyName.
	/// </summary>
	/// <param name="o">Object to modify.</param>
	/// <param name="fieldOrPropertyName">Name of Field or Property on the object to modify.</param>
	/// <param name="value">New value to set.</param>
	/// <param name="affectsFile">Whether or not this action represents a change to the file being edited.</param>
	public ActionSetObjectFieldOrPropertyReferenceNoClone(object o, string fieldOrPropertyName, T value, bool affectsFile) : base(
		false,
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

		// ReSharper disable PossibleNullReferenceException
		PreviousValue = IsField ? (T)FieldInfo.GetValue(O) : (T)PropertyInfo.GetValue(O);
		// ReSharper restore PossibleNullReferenceException

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
	public ActionSetObjectFieldOrPropertyReferenceNoClone(object o, string fieldOrPropertyName, T value, T previousValue,
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
		return GetSetFieldOrPropertyStringForClass(O, FieldOrPropertyName, PreviousValue, Value);
	}

	protected override void DoImplementation()
	{
		if (IsField)
			FieldInfo.SetValue(O, Value);
		else
			PropertyInfo.SetValue(O, Value);
	}

	protected override void UndoImplementation()
	{
		if (IsField)
			FieldInfo.SetValue(O, PreviousValue);
		else
			PropertyInfo.SetValue(O, PreviousValue);
	}
}

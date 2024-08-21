using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using static StepManiaEditor.EditorActionUtils;

namespace StepManiaEditor;

/// <summary>
/// Action to remove an element from a List field or property on an object.
/// </summary>
internal sealed class ActionRemoveFromObjectFieldOrPropertyList<T> : EditorAction
{
	private readonly object O;
	private readonly int Index;
	private readonly List<T> List;
	private readonly string FieldOrPropertyName;
	private readonly bool DoesAffectFile;
	private readonly T RemovedElement;

	public ActionRemoveFromObjectFieldOrPropertyList(object o, string fieldOrPropertyName, int index, bool affectsFile) :
		base(false, false)
	{
		O = o;
		Index = index;
		DoesAffectFile = affectsFile;
		FieldOrPropertyName = fieldOrPropertyName;

		var fieldInfo = o.GetType().GetField(fieldOrPropertyName, BindingFlags.Public | BindingFlags.Instance);
		if (fieldInfo != null)
		{
			var field = fieldInfo.GetValue(o);
			if (field is List<T> list)
			{
				List = list;
			}
		}
		else
		{
			var propertyInfo = o.GetType().GetProperty(fieldOrPropertyName, BindingFlags.Public | BindingFlags.Instance);
			if (propertyInfo != null)
			{
				var property = propertyInfo.GetValue(o);
				if (property is List<T> list)
				{
					List = list;
				}
			}
		}

		Debug.Assert(List != null);

		RemovedElement = List[Index];
	}

	public override bool AffectsFile()
	{
		return DoesAffectFile;
	}

	public override string ToString()
	{
		return $"Remove {GetPrettyLogString(RemovedElement)} from {GetPrettyLogStringForObject(O)} {FieldOrPropertyName}.";
	}

	protected override void DoImplementation()
	{
		List.RemoveAt(Index);
	}

	protected override void UndoImplementation()
	{
		List.Insert(Index, RemovedElement);
	}
}

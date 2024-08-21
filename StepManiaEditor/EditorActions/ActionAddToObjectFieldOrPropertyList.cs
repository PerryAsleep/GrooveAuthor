using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using static StepManiaEditor.EditorActionUtils;

namespace StepManiaEditor;

/// <summary>
/// Action to add an element to a List field or property on an object.
/// </summary>
internal sealed class ActionAddToObjectFieldOrPropertyList<T> : EditorAction
{
	private readonly object O;
	private readonly T Element;
	private readonly List<T> List;
	private readonly string FieldOrPropertyName;
	private readonly bool DoesAffectFile;

	public ActionAddToObjectFieldOrPropertyList(object o, string fieldOrPropertyName, T element, bool affectsFile) : base(false,
		false)
	{
		O = o;
		Element = element;
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
	}

	public override bool AffectsFile()
	{
		return DoesAffectFile;
	}

	public override string ToString()
	{
		return $"Add to {GetPrettyLogString(Element)} to {GetPrettyLogStringForObject(O)} {FieldOrPropertyName}.";
	}

	protected override void DoImplementation()
	{
		List.Add(Element);
	}

	protected override void UndoImplementation()
	{
		List.RemoveAt(List.Count - 1);
	}
}

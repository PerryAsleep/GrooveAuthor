using System;
using System.Collections.Generic;
using StepManiaEditor.AutogenConfig;

namespace StepManiaEditor;

/// <summary>
/// Class for common static utility functions used by EditorActions.
/// </summary>
internal sealed class EditorActionUtils
{
	private const string Empty = "<empty>";

	/// <summary>
	/// Return string representing setting an object's field or property from an old value to a new value.
	/// The majority of EditorActions in practice use this and we want the string to look natural to a user.
	/// </summary>
	public static string GetSetFieldOrPropertyStringForClass<T>(object o, string fieldOrPropertyName, T previousValue,
		T currentValue)
		where T : class
	{
		return GetSetFieldOrPropertyString(
			GetPrettyLogStringForObject(o),
			GetFieldOrPropertyString(fieldOrPropertyName),
			GetPrettyLogString(previousValue),
			GetPrettyLogString(currentValue));
	}

	/// <summary>
	/// Return string representing setting an object's field or property from an old value to a new value.
	/// The majority of EditorActions in practice use this and we want the string to look natural to a user.
	/// </summary>
	public static string GetSetFieldOrPropertyStringForStruct<T>(object o, string fieldOrPropertyName, T previousValue,
		T currentValue)
		where T : struct
	{
		return GetSetFieldOrPropertyString(
			GetPrettyLogStringForObject(o),
			GetFieldOrPropertyString(fieldOrPropertyName),
			GetPrettyLogString(previousValue),
			GetPrettyLogString(currentValue));
	}

	private static string GetSetFieldOrPropertyString(string objectString, string propertyString, string previousString,
		string currentString)
	{
		if (!string.IsNullOrEmpty(previousString) && !string.IsNullOrEmpty(currentString))
		{
			if (!string.IsNullOrEmpty(propertyString))
				return $"Update {objectString} {propertyString} From {previousString} To {currentString}.";
			return $"Update {objectString} From {previousString} To {currentString}.";
		}

		if (!string.IsNullOrEmpty(propertyString))
			return $"Update {objectString} {propertyString}";
		return $"Update {objectString}";
	}

	public static string GetFieldOrPropertyString(string fieldOrPropertyName)
	{
		// Omit the field or property name entirely if it just a simple type holder.
		if (!string.IsNullOrEmpty(fieldOrPropertyName) && (
			    fieldOrPropertyName == "IntValue"
			    || fieldOrPropertyName == "DoubleValue"
			    || fieldOrPropertyName == "StringValue"))
			return null;

		// Otherwise just use the unmodified name.
		return fieldOrPropertyName;
	}

	public static string GetPrettyLogStringForObject(object o)
	{
		// For EditorEvents prefer their short type name representation.
		if (o is EditorEvent e)
			return e.GetShortTypeName();

		// Otherwise get the string from the type.
		return GetPrettyLogStringForType(o.GetType());
	}

	private static string GetPrettyLogStringForType(Type type)
	{
		var name = type.Name;

		// Preferences class names are awkward.
		if (name.StartsWith("Preferences") && name.Length > 11)
		{
			name = name[11..];
			if (name != "Options")
			{
				if (name == "Performance")
					name += " Monitoring";
				name += " Preferences";
			}
		}

		// A lot of class names start with "Editor" like "EditorSong". Make these less awkward.
		if (name.StartsWith("Editor") && name.Length > 6)
			name = name[6..];

		return name;
	}

	public static string GetPrettyLogString<T>(T value)
	{
		if (value == null)
			return Empty;

		// HashSets look nasty when logged and there is no good way to clean them up.
		if (value.GetType().IsGenericType && value.GetType().GetGenericTypeDefinition() == typeof(HashSet<>))
			return null;

		if (value is Guid guid)
			return GetPrettyLogStringForGuid(guid);

		// If the value is a chart get the chart's name.
		if (value is EditorChart chart)
			return chart.GetDescriptiveName();

		// Some enums look bad. Get the nice string representation.
		if (value is Enum)
			return ImGuiUtils.GetPrettyEnumString(value);

		var result = value.ToString();
		if (string.IsNullOrEmpty(result))
			result = Empty;
		return result;
	}

	/// <summary>
	/// Given a Guid, return a pretty log string.
	/// Will attempt to look up objects potentially identified by the Guid for better names.
	/// </summary>
	private static string GetPrettyLogStringForGuid(Guid guid)
	{
		var pcc = PerformedChartConfigManager.Instance.GetConfig(guid);
		if (pcc != null)
		{
			if (!string.IsNullOrEmpty(pcc.GetLogString()))
				return pcc.GetLogString();
		}

		var pc = PatternConfigManager.Instance.GetConfig(guid);
		if (pc != null)
		{
			if (!string.IsNullOrEmpty(pc.Name))
				return pc.Name;
			if (!string.IsNullOrEmpty(pc.GetAbbreviation()))
				return pc.GetAbbreviation();
		}

		var ecc = ExpressedChartConfigManager.Instance.GetConfig(guid);
		if (ecc != null)
		{
			if (!string.IsNullOrEmpty(ecc.Name))
				return ecc.Name;
		}

		return guid.ToString();
	}
}

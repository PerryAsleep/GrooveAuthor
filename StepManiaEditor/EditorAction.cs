using System;
using System.Collections.Generic;
using System.Reflection;
using Fumen.ChartDefinition;
using static Fumen.Converters.SMCommon;

namespace StepManiaEditor
{
	/// <summary>
	/// An action that can be done and undone.
	/// Meant to be used by ActionQueue.
	/// </summary>
	public abstract class EditorAction
	{
		/// <summary>
		/// Do the action.
		/// </summary>
		public abstract void Do();

		/// <summary>
		/// Undo the action.
		/// </summary>
		public abstract void Undo();

		/// <summary>
		/// Returns whether or not this action represents a change to the underlying file.
		/// Used by ActionQueue to determine if there are unsaved changes.
		/// </summary>
		public abstract bool AffectsFile();

		/// <summary>
		/// Returns how many actions up to and including this action affect the underlying file.
		/// Used by ActionQueue to determine if there are unsaved changes.
		/// </summary>
		/// <returns></returns>
		public int GetTotalNumActionsAffectingFile()
		{
			return NumPreviousActionsAffectingFile + (AffectsFile() ? 1 : 0);
		}

		/// <summary>
		/// Sets the number of previous actions which affect the underlying file.
		/// Used by ActionQueue to determine if there are unsaved changes.
		/// </summary>
		public void SetNumPreviousActionsAffectingFile(int actions)
		{
			NumPreviousActionsAffectingFile = actions;
		}

		/// <summary>
		/// Number of previous actions which affect the underlying file.
		/// Used by ActionQueue to determine if there are unsaved changes.
		/// </summary>
		protected int NumPreviousActionsAffectingFile = 0;
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
		private readonly bool DoesAffectFile;

		/// <summary>
		/// Constructor with a given value to set.
		/// It is assumed that a public instance field or property exists on the object with the given fieldOrPropertyName.
		/// </summary>
		/// <param name="o">Object to modify.</param>
		/// <param name="fieldOrPropertyName">Name of Field or Property on the object to modify.</param>
		/// <param name="value">New value to set.</param>
		public ActionSetObjectFieldOrPropertyValue(object o, string fieldOrPropertyName, T value, bool affectsFile)
		{
			O = o;
			Value = value;
			FieldOrPropertyName = fieldOrPropertyName;

			FieldInfo = O.GetType().GetField(FieldOrPropertyName, BindingFlags.Public | BindingFlags.Instance);
			IsField = FieldInfo != null;
			if (!IsField)
				PropertyInfo = O.GetType().GetProperty(FieldOrPropertyName, BindingFlags.Public | BindingFlags.Instance);

			PreviousValue = IsField ? (T)FieldInfo.GetValue(O) : (T)PropertyInfo.GetValue(O);

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
		public ActionSetObjectFieldOrPropertyValue(object o, string fieldOrPropertyName, T value, T previousValue, bool affectsFile)
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
		private readonly bool DoesAffectFile;

		/// <summary>
		/// Constructor with a given value to set.
		/// It is assumed value is a Clone of the value.
		/// It is assumed that a public instance field or property exists on the object with the given fieldOrPropertyName.
		/// </summary>
		/// <param name="o">Object to modify.</param>
		/// <param name="fieldOrPropertyName">Name of Field or Property on the object to modify.</param>
		/// <param name="value">New value to set.</param>
		public ActionSetObjectFieldOrPropertyReference(object o, string fieldOrPropertyName, T value, bool affectsFile)
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
		public ActionSetObjectFieldOrPropertyReference(object o, string fieldOrPropertyName, T value, T previousValue, bool affectsFile)
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

		public override bool AffectsFile()
		{
			return true;
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

	/// <summary>
	/// EditorAction for changing the ShouldAllowEditsOfMax field of a DisplayTempo.
	/// When disabling ShouldAllowEditsOfMax, the max tempo is forced to be the min.
	/// If they were different before setting ShouldAllowEditsOfMax to true, then undoing
	/// that change should restore the max tempo back to what it was previously.
	/// </summary>
	public class ActionSetDisplayTempoAllowEditsOfMax : EditorAction
	{
		private readonly DisplayTempo DisplayTempo;
		private readonly double PreviousMax;
		private readonly bool Allow;

		public ActionSetDisplayTempoAllowEditsOfMax(DisplayTempo displayTempo, bool allow)
		{
			DisplayTempo = displayTempo;
			PreviousMax = DisplayTempo.SpecifiedTempoMax;
			Allow = allow;
		}

		public override bool AffectsFile()
		{
			return true;
		}

		public override string ToString()
		{
			return $"Set display tempo ShouldAllowEditsOfMax '{!Allow}' > '{Allow}'.";
		}

		public override void Do()
		{
			DisplayTempo.ShouldAllowEditsOfMax = Allow;
			if (!DisplayTempo.ShouldAllowEditsOfMax)
				DisplayTempo.SpecifiedTempoMax = DisplayTempo.SpecifiedTempoMin;
		}

		public override void Undo()
		{
			DisplayTempo.ShouldAllowEditsOfMax = !Allow;
			if (DisplayTempo.ShouldAllowEditsOfMax)
				DisplayTempo.SpecifiedTempoMax = PreviousMax;
		}
	}

	public class ActionMultiple : EditorAction
	{
		private readonly List<EditorAction> Actions;

		public ActionMultiple()
		{
			Actions = new List<EditorAction>();
		}

		public ActionMultiple(List<EditorAction> actions)
		{
			Actions = actions;
		}

		public void EnqueueAndDo(EditorAction action)
		{
			action.Do();
			Actions.Add(action);
		}

		public void EnqueueWithoutDoing(EditorAction action)
		{
			Actions.Add(action);
		}

		public List<EditorAction> GetActions()
		{
			return Actions;
		}

		public override bool AffectsFile()
		{
			foreach (var action in Actions)
			{
				if (action.AffectsFile())
					return true;
			}
			return false;
		}

		public override string ToString()
		{
			return string.Join(' ', Actions);
		}

		public override void Do()
		{
			foreach (var action in Actions)
			{
				action.Do();
			}
		}

		public override void Undo()
		{
			var i = Actions.Count - 1;
			while (i >= 0)
			{
				Actions[i--].Undo();
			}
		}

		public void Clear()
		{
			Actions.Clear();
		}
	}
	
	public class ActionAddEditorEvent : EditorAction
	{
		private EditorEvent EditorEvent;

		public ActionAddEditorEvent(EditorEvent editorEvent)
		{
			EditorEvent = editorEvent;
		}

		public void UpdateEvent(EditorEvent editorEvent)
		{
			EditorEvent.GetEditorChart().DeleteEvent(EditorEvent);
			EditorEvent = editorEvent;
			EditorEvent.GetEditorChart().AddEvent(EditorEvent);
		}

		public void SetIsBeingEdited(bool isBeingEdited)
		{
			EditorEvent.SetIsBeingEdited(isBeingEdited);
		}

		public override bool AffectsFile()
		{
			return true;
		}

		public override string ToString()
		{
			// TODO: Nice strings
			return $"Add {EditorEvent.GetType()}.";
		}

		public override void Do()
		{
			EditorEvent.GetEditorChart().AddEvent(EditorEvent);
		}

		public override void Undo()
		{
			EditorEvent.GetEditorChart().DeleteEvent(EditorEvent);
		}
	}

	public class ActionDeleteEditorEvent : EditorAction
	{
		private readonly EditorEvent EditorEvent;

		public ActionDeleteEditorEvent(EditorEvent editorEvent)
		{
			EditorEvent = editorEvent;
		}

		public override bool AffectsFile()
		{
			return true;
		}

		public override string ToString()
		{
			// TODO: Nice strings
			return $"Delete {EditorEvent.GetType()}.";
		}

		public override void Do()
		{
			EditorEvent.GetEditorChart().DeleteEvent(EditorEvent);
		}

		public override void Undo()
		{  
			EditorEvent.GetEditorChart().AddEvent(EditorEvent);
		}
	}

	public class ActionChangeHoldLength : EditorAction
	{
		private EditorHoldStartNoteEvent HoldStart;
		private EditorHoldEndNoteEvent HoldEnd;
		private EditorHoldEndNoteEvent NewHoldEnd;

		public ActionChangeHoldLength(EditorHoldStartNoteEvent holdStart, int length)
		{
			var newHoldEndRow = holdStart.GetRow() + length;
			var newHoldEndTime = 0.0;
			holdStart.GetEditorChart().TryGetTimeFromChartPosition(newHoldEndRow, ref newHoldEndTime);

			HoldStart = holdStart;
			HoldEnd = holdStart.GetHoldEndNote();
			NewHoldEnd = new EditorHoldEndNoteEvent(HoldStart.GetEditorChart(), new LaneHoldEndNote()
			{
				Lane = HoldStart.GetLane(),
				IntegerPosition = HoldStart.GetRow() + length,
				TimeMicros = Fumen.Utils.ToMicros(newHoldEndTime)
			});
			NewHoldEnd.SetHoldStartNote(HoldStart);
		}

		public override bool AffectsFile()
		{
			return true;
		}

		public override string ToString()
		{
			var typeStr = HoldStart.IsRoll() ? "roll" : "hold";
			return $"Change {typeStr} length from to {HoldEnd.GetRow() - HoldStart.GetRow()} to {NewHoldEnd.GetRow() - HoldStart.GetRow()}.";
		}

		public override void Do()
		{
			HoldStart.GetEditorChart().DeleteEvent(HoldEnd);
			HoldStart.SetHoldEndNote(NewHoldEnd);
			HoldStart.GetEditorChart().AddEvent(NewHoldEnd);
		}

		public override void Undo()
		{
			HoldStart.GetEditorChart().DeleteEvent(NewHoldEnd);
			HoldStart.SetHoldEndNote(HoldEnd);
			HoldStart.GetEditorChart().AddEvent(HoldEnd);
		}
	}

	public class ActionAddHoldEvent : EditorAction
	{
		private EditorHoldStartNoteEvent HoldStart;
		private EditorHoldEndNoteEvent HoldEnd;

		public ActionAddHoldEvent(EditorChart chart, int lane, int row, int length, bool roll, bool isBeingEdited)
		{
			var holdStartTime = 0.0;
			chart.TryGetTimeFromChartPosition(row, ref holdStartTime);
			var holdStartNote = new LaneHoldStartNote()
			{
				Lane = lane,
				IntegerPosition = row,
				TimeMicros = Fumen.Utils.ToMicros(holdStartTime)
			};
			HoldStart = new EditorHoldStartNoteEvent(chart, holdStartNote, isBeingEdited);
			HoldStart.SetIsRoll(roll);

			var holdEndTime = 0.0;
			chart.TryGetTimeFromChartPosition(row + length, ref holdEndTime);
			var holdEndNote = new LaneHoldEndNote()
			{
				Lane = lane,
				IntegerPosition = row + length,
				TimeMicros = Fumen.Utils.ToMicros(holdEndTime)
			};
			HoldEnd = new EditorHoldEndNoteEvent(chart, holdEndNote, isBeingEdited);

			HoldStart.SetHoldEndNote(HoldEnd);
			HoldEnd.SetHoldStartNote(HoldStart);
		}

		public EditorHoldStartNoteEvent GetHoldStartEvent()
		{
			return HoldStart;
		}

		public void SetIsRoll(bool roll)
		{
			HoldStart.SetIsRoll(roll);
			HoldEnd.SetIsRoll(roll);
		}

		public void SetIsBeingEdited(bool isBeingEdited)
		{
			HoldStart.SetIsBeingEdited(isBeingEdited);
			HoldEnd.SetIsBeingEdited(isBeingEdited);
		}

		public override bool AffectsFile()
		{
			return true;
		}

		public override string ToString()
		{
			var typeStr = HoldStart.IsRoll() ? "roll" : "hold";
			return $"Add {typeStr}.";
		}

		public override void Do()
		{
			HoldStart.GetEditorChart().AddEvent(HoldStart);
			HoldStart.GetEditorChart().AddEvent(HoldEnd);
		}

		public override void Undo()
		{
			HoldStart.GetEditorChart().DeleteEvent(HoldStart);
			HoldStart.GetEditorChart().DeleteEvent(HoldEnd);
		}
	}

	public class ActionChangeHoldType : EditorAction
	{
		private bool Roll;
		private EditorHoldStartNoteEvent HoldStart;

		public ActionChangeHoldType(EditorHoldStartNoteEvent holdStart, bool roll)
		{
			HoldStart = holdStart;
			Roll = roll;
		}

		public override bool AffectsFile()
		{
			return true;
		}

		public override string ToString()
		{
			var originalType = Roll ? "hold" : "roll";
			var newType = Roll ? "roll" : "hold";
			return $"Change {originalType} to {newType}.";
		}

		public override void Do()
		{
			HoldStart.SetIsRoll(Roll);
		}

		public override void Undo()
		{
			HoldStart.SetIsRoll(!Roll);
		}
	}

	public class ActionDeleteHoldEvent : EditorAction
	{
		private EditorHoldStartNoteEvent HoldStart;

		public ActionDeleteHoldEvent(EditorHoldStartNoteEvent holdStart)
		{
			HoldStart = holdStart;
		}

		public override bool AffectsFile()
		{
			return true;
		}

		public override string ToString()
		{
			var typeStr = HoldStart.IsRoll() ? "roll" : "hold";
			return $"Delete {typeStr}.";
		}

		public override void Do()
		{
			HoldStart.GetEditorChart().DeleteEvent(HoldStart);
			HoldStart.GetEditorChart().DeleteEvent(HoldStart.GetHoldEndNote());
		}

		public override void Undo()
		{
			HoldStart.GetEditorChart().AddEvent(HoldStart);
			HoldStart.GetEditorChart().AddEvent(HoldStart.GetHoldEndNote());
		}
	}

	public class ActionSelectChart : EditorAction
	{
		private Editor Editor;
		private EditorChart Chart;
		private EditorChart PreviousChart;

		public ActionSelectChart(Editor editor, EditorChart chart)
		{
			Editor = editor;
			PreviousChart = Editor.GetActiveChart();
			Chart = chart;
		}

		public override bool AffectsFile()
		{
			return false;
		}

		public override string ToString()
		{
			return $"Select {Utils.GetPrettyEnumString(Chart.ChartType)} {Utils.GetPrettyEnumString(Chart.ChartDifficultyType)} Chart.";
		}

		public override void Do()
		{
			Editor.OnChartSelected(Chart, false);
		}

		public override void Undo()
		{
			Editor.OnChartSelected(PreviousChart, false);
		}
	}

	public class ActionAddChart : EditorAction
	{
		private Editor Editor;
		private ChartType ChartType;
		private EditorChart AddedChart;
		private EditorChart PreivouslyActiveChart;

		public ActionAddChart(Editor editor, ChartType chartType)
		{
			Editor = editor;
			ChartType = chartType;
		}

		public override bool AffectsFile()
		{
			return true;
		}

		public override string ToString()
		{
			return $"Add {Utils.GetPrettyEnumString(ChartType)} Chart.";
		}

		public override void Do()
		{
			PreivouslyActiveChart = Editor.GetActiveChart();
			
			// Through undoing and redoing we may add the same chart multiple times.
			// Other actions like ActionAddEditorEvent reference specific charts.
			// For those actions to work as expected we should restore the same chart instance
			// rather than creating a new one when undoing and redoing.
			if (AddedChart != null)
				Editor.AddChart(AddedChart, true);
			else
				AddedChart = Editor.AddChart(ChartType, true);
		}

		public override void Undo()
		{
			Editor.DeleteChart(AddedChart, PreivouslyActiveChart);
		}
	}

	public class ActionDeleteChart : EditorAction
	{
		private Editor Editor;
		private EditorChart Chart;
		private bool DeletedActiveChart;

		public ActionDeleteChart(Editor editor, EditorChart chart)
		{
			Editor = editor;
			Chart = chart;
		}

		public override bool AffectsFile()
		{
			return true;
		}

		public override string ToString()
		{
			return $"Delete {Utils.GetPrettyEnumString(Chart.ChartType)} Chart.";
		}

		public override void Do()
		{
			DeletedActiveChart = Editor.GetActiveChart() == Chart;
			Editor.DeleteChart(Chart, null);
		}

		public override void Undo()
		{
			Editor.AddChart(Chart, DeletedActiveChart);
		}
	}

	public class ActionMoveFocalPoint : EditorAction
	{
		private int PreviousX;
		private int PreviousY;
		private int NewX;
		private int NewY;

		public ActionMoveFocalPoint(int previousX, int previousY, int newX, int newY)
		{
			PreviousX = previousX;
			PreviousY = previousY;
			NewX = newX;
			NewY = newY;
		}

		public override bool AffectsFile()
		{
			return false;
		}

		public override string ToString()
		{
			return $"Move receptors from ({PreviousX}, {PreviousY}) to ({NewX}, {NewY}).";
		}

		public override void Do()
		{
			Preferences.Instance.PreferencesReceptors.PositionX = NewX;
			Preferences.Instance.PreferencesReceptors.PositionY = NewY;
		}

		public override void Undo()
		{
			Preferences.Instance.PreferencesReceptors.PositionX = PreviousX;
			Preferences.Instance.PreferencesReceptors.PositionY = PreviousY;
		}
	}
}

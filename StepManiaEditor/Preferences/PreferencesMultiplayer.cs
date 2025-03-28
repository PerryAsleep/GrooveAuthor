using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Fumen;
using Microsoft.Xna.Framework;
using static StepManiaEditor.PreferencesMultiplayer;
using static StepManiaEditor.ArrowGraphicManager;
using Vector3 = System.Numerics.Vector3;

namespace StepManiaEditor;

/// <summary>
/// Multiplayer (routine / co-op / team) preferences.
/// Colors are individual properties in order to ease edits through UI.
/// </summary>
internal sealed class PreferencesMultiplayer
{
	private const float SelectionColorMultiplier = 2.0f; // See also SelectionColorMultiplier in StepManiaEditorTextureGenerator.
	private const float HeldColorMultiplier = 2.0f;

	public const float DefaultRoutineNoteColorAlpha = 0.80f;
	public const bool DefaultColorHoldsAndRolls = true;
	public static readonly Vector3 DefaultPlayer0Color = new(0.7109375f, 0.09375f, 0.7109375f);
	public static readonly Vector3 DefaultPlayer1Color = new(0.09375f, 0.7109375f, 0.7109375f);
	public static readonly Vector3 DefaultPlayer2Color = new(0.7109375f, 0.7109375f, 0.09375f);
	public static readonly Vector3 DefaultPlayer3Color = new(0.09375f, 0.7109375f, 0.09375f);
	public static readonly Vector3 DefaultPlayer4Color = new(0.7109375f, 0.390625f, 0.09375f);
	public static readonly Vector3 DefaultPlayer5Color = new(0.7109375f, 0.7109375f, 0.7109375f);
	public static readonly Vector3 DefaultPlayer6Color = new(0.09375f, 0.09375f, 0.09375f);
	public static readonly Vector3 DefaultPlayer7Color = new(0.7109375f, 0.390625f, 0.390625f);
	public static readonly Vector3 DefaultPlayer8Color = new(0.390625f, 0.390625f, 0.7109375f);
	public static readonly Vector3 DefaultPlayer9Color = new(0.390625f, 0.7109375f, 0.390625f);

	[JsonInclude] public bool ShowMultiplayerPreferencesWindow;
	[JsonInclude] public bool ColorHoldsAndRolls = DefaultColorHoldsAndRolls;

	[JsonInclude]
	public float RoutineNoteColorAlpha
	{
		get => RoutineNoteColorAlphaInternal;
		set
		{
			RoutineNoteColorAlphaInternal = value;
			RefreshCachedRoutineColors();
		}
	}

	private float RoutineNoteColorAlphaInternal = DefaultRoutineNoteColorAlpha;

	[JsonInclude]
	public Vector3 Player0Color
	{
		get => Player0ColorInternal;
		set
		{
			Player0ColorInternal = value;
			RefreshCachedRoutineColors();
		}
	}

	private Vector3 Player0ColorInternal = DefaultPlayer0Color;

	[JsonInclude]
	public Vector3 Player1Color
	{
		get => Player1ColorInternal;
		set
		{
			Player1ColorInternal = value;
			RefreshCachedRoutineColors();
		}
	}

	private Vector3 Player1ColorInternal = DefaultPlayer1Color;

	[JsonInclude]
	public Vector3 Player2Color
	{
		get => Player2ColorInternal;
		set
		{
			Player2ColorInternal = value;
			RefreshCachedRoutineColors();
		}
	}

	private Vector3 Player2ColorInternal = DefaultPlayer2Color;

	[JsonInclude]
	public Vector3 Player3Color
	{
		get => Player3ColorInternal;
		set
		{
			Player3ColorInternal = value;
			RefreshCachedRoutineColors();
		}
	}

	private Vector3 Player3ColorInternal = DefaultPlayer3Color;

	[JsonInclude]
	public Vector3 Player4Color
	{
		get => Player4ColorInternal;
		set
		{
			Player4ColorInternal = value;
			RefreshCachedRoutineColors();
		}
	}

	private Vector3 Player4ColorInternal = DefaultPlayer4Color;

	[JsonInclude]
	public Vector3 Player5Color
	{
		get => Player5ColorInternal;
		set
		{
			Player5ColorInternal = value;
			RefreshCachedRoutineColors();
		}
	}

	private Vector3 Player5ColorInternal = DefaultPlayer5Color;

	[JsonInclude]
	public Vector3 Player6Color
	{
		get => Player6ColorInternal;
		set
		{
			Player6ColorInternal = value;
			RefreshCachedRoutineColors();
		}
	}

	private Vector3 Player6ColorInternal = DefaultPlayer6Color;

	[JsonInclude]
	public Vector3 Player7Color
	{
		get => Player7ColorInternal;
		set
		{
			Player7ColorInternal = value;
			RefreshCachedRoutineColors();
		}
	}

	private Vector3 Player7ColorInternal = DefaultPlayer7Color;

	[JsonInclude]
	public Vector3 Player8Color
	{
		get => Player8ColorInternal;
		set
		{
			Player8ColorInternal = value;
			RefreshCachedRoutineColors();
		}
	}

	private Vector3 Player8ColorInternal = DefaultPlayer8Color;

	[JsonInclude]
	public Vector3 Player9Color
	{
		get => Player9ColorInternal;
		set
		{
			Player9ColorInternal = value;
			RefreshCachedRoutineColors();
		}
	}

	private Vector3 Player9ColorInternal = DefaultPlayer9Color;

	private readonly List<Vector3> NoteColors = [];
	private readonly List<Color> XnaNoteColors = [];
	private readonly List<Color> XnaHeldNoteColors = [];
	private readonly List<Color> XnaSelectedNoteColors = [];
	private readonly List<Color> XnaHeldAndSelectedNoteColors = [];
	private readonly List<uint> UINoteColors = [];
	private readonly List<uint> SelectedUINoteColors = [];

	public void PostLoad()
	{
		RefreshCachedRoutineColors();
	}

	private void RefreshCachedRoutineColors()
	{
		NoteColors.Clear();
		NoteColors.Add(Player0Color);
		NoteColors.Add(Player1Color);
		NoteColors.Add(Player2Color);
		NoteColors.Add(Player3Color);
		NoteColors.Add(Player4Color);
		NoteColors.Add(Player5Color);
		NoteColors.Add(Player6Color);
		NoteColors.Add(Player7Color);
		NoteColors.Add(Player8Color);
		NoteColors.Add(Player9Color);

		XnaNoteColors.Clear();
		XnaHeldNoteColors.Clear();
		XnaSelectedNoteColors.Clear();
		XnaHeldAndSelectedNoteColors.Clear();
		UINoteColors.Clear();
		SelectedUINoteColors.Clear();
		foreach (var color in NoteColors)
		{
			XnaNoteColors.Add(new Color(color.X, color.Y, color.Z, RoutineNoteColorAlpha));
			XnaHeldNoteColors.Add(new Color(Math.Min(1.0f, color.X * HeldColorMultiplier),
				Math.Min(1.0f, color.Y * HeldColorMultiplier), Math.Min(1.0f, color.Z * HeldColorMultiplier),
				RoutineNoteColorAlpha));
			XnaSelectedNoteColors.Add(new Color(Math.Min(1.0f, color.X * SelectionColorMultiplier),
				Math.Min(1.0f, color.Y * SelectionColorMultiplier), Math.Min(1.0f, color.Z * SelectionColorMultiplier),
				RoutineNoteColorAlpha));
			XnaHeldAndSelectedNoteColors.Add(new Color(Math.Min(1.0f, color.X * HeldColorMultiplier * SelectionColorMultiplier),
				Math.Min(1.0f, color.Y * HeldColorMultiplier * SelectionColorMultiplier),
				Math.Min(1.0f, color.Z * HeldColorMultiplier * SelectionColorMultiplier), RoutineNoteColorAlpha));
			UINoteColors.Add(ColorUtils.ToRGBA(Math.Min(1.0f, color.X * ArrowUIColorMultiplier),
				Math.Min(1.0f, color.Y * ArrowUIColorMultiplier), Math.Min(1.0f, color.Z * ArrowUIColorMultiplier), 1.0f));
			SelectedUINoteColors.Add(ColorUtils.ToRGBA(Math.Min(1.0f, color.X * ArrowUISelectedColorMultiplier),
				Math.Min(1.0f, color.Y * ArrowUISelectedColorMultiplier),
				Math.Min(1.0f, color.Z * ArrowUISelectedColorMultiplier), 1.0f));
		}
	}

	public Color GetRoutineNoteColor(int player)
	{
		return XnaNoteColors[player % NoteColors.Count];
	}

	public Color GetRoutineHeldNoteColor(int player)
	{
		return XnaHeldNoteColors[player % NoteColors.Count];
	}

	public Color GetRoutineSelectedNoteColor(int player)
	{
		return XnaSelectedNoteColors[player % NoteColors.Count];
	}

	public Color GetRoutineHeldAndSelectedNoteColor(int player)
	{
		return XnaHeldAndSelectedNoteColors[player % NoteColors.Count];
	}

	public uint GetRoutineUINoteColor(int player)
	{
		return UINoteColors[player % NoteColors.Count];
	}

	public uint GetRoutineSelectedUINoteColor(int player)
	{
		return SelectedUINoteColors[player % NoteColors.Count];
	}

	public bool IsUsingDefaults()
	{
		return RoutineNoteColorAlpha.FloatEquals(DefaultRoutineNoteColorAlpha)
		       && ColorHoldsAndRolls == DefaultColorHoldsAndRolls
		       && Player0Color.Equals(DefaultPlayer0Color)
		       && Player1Color.Equals(DefaultPlayer1Color)
		       && Player2Color.Equals(DefaultPlayer2Color)
		       && Player3Color.Equals(DefaultPlayer3Color)
		       && Player4Color.Equals(DefaultPlayer4Color)
		       && Player5Color.Equals(DefaultPlayer5Color)
		       && Player6Color.Equals(DefaultPlayer6Color)
		       && Player7Color.Equals(DefaultPlayer7Color)
		       && Player8Color.Equals(DefaultPlayer8Color)
		       && Player9Color.Equals(DefaultPlayer9Color);
	}

	public void RestoreDefaults()
	{
		// Don't enqueue an action if it would not have any effect.
		if (IsUsingDefaults())
			return;
		ActionQueue.Instance.Do(new ActionRestoreMultiplayerPreferenceDefaults());
	}
}

/// <summary>
/// Action to restore multiplayer preferences to their default values.
/// </summary>
internal sealed class ActionRestoreMultiplayerPreferenceDefaults : EditorAction
{
	private readonly float PreviousRoutineNoteColorAlpha;
	private readonly bool PreviousColorHoldsAndRolls;
	private readonly Vector3 PreviousPlayer0Color;
	private readonly Vector3 PreviousPlayer1Color;
	private readonly Vector3 PreviousPlayer2Color;
	private readonly Vector3 PreviousPlayer3Color;
	private readonly Vector3 PreviousPlayer4Color;
	private readonly Vector3 PreviousPlayer5Color;
	private readonly Vector3 PreviousPlayer6Color;
	private readonly Vector3 PreviousPlayer7Color;
	private readonly Vector3 PreviousPlayer8Color;
	private readonly Vector3 PreviousPlayer9Color;

	public ActionRestoreMultiplayerPreferenceDefaults() : base(false, false)
	{
		var p = Preferences.Instance.PreferencesMultiplayer;
		PreviousRoutineNoteColorAlpha = p.RoutineNoteColorAlpha;
		PreviousColorHoldsAndRolls = p.ColorHoldsAndRolls;
		PreviousPlayer0Color = p.Player0Color;
		PreviousPlayer1Color = p.Player1Color;
		PreviousPlayer2Color = p.Player2Color;
		PreviousPlayer3Color = p.Player3Color;
		PreviousPlayer4Color = p.Player4Color;
		PreviousPlayer5Color = p.Player5Color;
		PreviousPlayer6Color = p.Player6Color;
		PreviousPlayer7Color = p.Player7Color;
		PreviousPlayer8Color = p.Player8Color;
		PreviousPlayer9Color = p.Player9Color;
	}

	public override bool AffectsFile()
	{
		return false;
	}

	public override string ToString()
	{
		return "Restore Multiplayer Preferences to default values.";
	}

	protected override void DoImplementation()
	{
		var p = Preferences.Instance.PreferencesMultiplayer;
		p.RoutineNoteColorAlpha = DefaultRoutineNoteColorAlpha;
		p.ColorHoldsAndRolls = DefaultColorHoldsAndRolls;
		p.Player0Color = DefaultPlayer0Color;
		p.Player1Color = DefaultPlayer1Color;
		p.Player2Color = DefaultPlayer2Color;
		p.Player3Color = DefaultPlayer3Color;
		p.Player4Color = DefaultPlayer4Color;
		p.Player5Color = DefaultPlayer5Color;
		p.Player6Color = DefaultPlayer6Color;
		p.Player7Color = DefaultPlayer7Color;
		p.Player8Color = DefaultPlayer8Color;
		p.Player9Color = DefaultPlayer9Color;
	}

	protected override void UndoImplementation()
	{
		var p = Preferences.Instance.PreferencesMultiplayer;
		p.RoutineNoteColorAlpha = PreviousRoutineNoteColorAlpha;
		p.ColorHoldsAndRolls = PreviousColorHoldsAndRolls;
		p.Player0Color = PreviousPlayer0Color;
		p.Player1Color = PreviousPlayer1Color;
		p.Player2Color = PreviousPlayer2Color;
		p.Player3Color = PreviousPlayer3Color;
		p.Player4Color = PreviousPlayer4Color;
		p.Player5Color = PreviousPlayer5Color;
		p.Player6Color = PreviousPlayer6Color;
		p.Player7Color = PreviousPlayer7Color;
		p.Player8Color = PreviousPlayer8Color;
		p.Player9Color = PreviousPlayer9Color;
	}
}

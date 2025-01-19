using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Fumen;
using Microsoft.Xna.Framework;
using static StepManiaEditor.PreferencesMultiplayer;
using Vector3 = System.Numerics.Vector3;

namespace StepManiaEditor;

/// <summary>
/// Multiplayer (routine / co-op / team) preferences.
/// Colors are individual properties in order to ease edits through UI.
/// </summary>
internal sealed class PreferencesMultiplayer
{
	public const float DefaultRoutineNoteColorAlpha = 0.80f;
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

	private const float SelectionColorMultiplier = 2.0f; // See also SelectionColorMultiplier in StepManiaEditorTextureGenerator.

	private readonly List<Vector3> RoutineNoteColors = new();
	private readonly List<Color> RoutineNoteColorsAsXnaColors = new();
	private readonly List<Color> RoutineSelectedNoteColorsAsXnaColors = new();
	private readonly List<ushort> RoutineNoteColorsAsBgr565 = new();
	private readonly List<ushort> RoutineSelectedNoteColorsAsBgr565 = new();
	private readonly List<uint> RoutineNoteColorsAsRgba = new();
	private readonly List<uint> RoutineSelectedNoteColorsAsRgba = new();

	public void PostLoad()
	{
		RefreshCachedRoutineColors();
	}

	private void RefreshCachedRoutineColors()
	{
		RoutineNoteColors.Clear();
		RoutineNoteColors.Add(Player0Color);
		RoutineNoteColors.Add(Player1Color);
		RoutineNoteColors.Add(Player2Color);
		RoutineNoteColors.Add(Player3Color);
		RoutineNoteColors.Add(Player4Color);
		RoutineNoteColors.Add(Player5Color);
		RoutineNoteColors.Add(Player6Color);
		RoutineNoteColors.Add(Player7Color);
		RoutineNoteColors.Add(Player8Color);
		RoutineNoteColors.Add(Player9Color);

		RoutineNoteColorsAsXnaColors.Clear();
		RoutineSelectedNoteColorsAsXnaColors.Clear();
		RoutineNoteColorsAsBgr565.Clear();
		RoutineSelectedNoteColorsAsBgr565.Clear();
		RoutineNoteColorsAsRgba.Clear();
		RoutineSelectedNoteColorsAsRgba.Clear();
		foreach (var color in RoutineNoteColors)
		{
			var selectedR = Math.Min(1.0f, color.X * SelectionColorMultiplier);
			var selectedG = Math.Min(1.0f, color.Y * SelectionColorMultiplier);
			var selectedB = Math.Min(1.0f, color.Z * SelectionColorMultiplier);
			RoutineNoteColorsAsXnaColors.Add(new Color(color.X, color.Y, color.Z, RoutineNoteColorAlpha));
			RoutineSelectedNoteColorsAsXnaColors.Add(new Color(selectedR, selectedG, selectedB, RoutineNoteColorAlpha));
			RoutineNoteColorsAsBgr565.Add(ColorUtils.ToBGR565(color.X, color.Y, color.Z));
			RoutineSelectedNoteColorsAsBgr565.Add(ColorUtils.ToBGR565(selectedR, selectedG, selectedB));
			RoutineNoteColorsAsRgba.Add(ColorUtils.ToRGBA(color.X, color.Y, color.Z, RoutineNoteColorAlpha));
			RoutineSelectedNoteColorsAsRgba.Add(ColorUtils.ToRGBA(selectedR, selectedG, selectedB, RoutineNoteColorAlpha));
		}
	}

	public Color GetRoutineNoteColor(int player)
	{
		return RoutineNoteColorsAsXnaColors[player % RoutineNoteColors.Count];
	}

	public ushort GetRoutineNoteColorBgr565(int player)
	{
		return RoutineNoteColorsAsBgr565[player % RoutineNoteColors.Count];
	}

	public uint GetRoutineNoteColorRgba(int player)
	{
		return RoutineNoteColorsAsRgba[player % RoutineNoteColors.Count];
	}

	public Color GetRoutineSelectedNoteColor(int player)
	{
		return RoutineSelectedNoteColorsAsXnaColors[player % RoutineNoteColors.Count];
	}

	public ushort GetRoutineSelectedNoteColorBgr565(int player)
	{
		return RoutineSelectedNoteColorsAsBgr565[player % RoutineNoteColors.Count];
	}

	public uint GetRoutineSelectedNoteColorRgba(int player)
	{
		return RoutineSelectedNoteColorsAsRgba[player % RoutineNoteColors.Count];
	}

	public bool IsUsingDefaults()
	{
		return Player0Color.Equals(DefaultPlayer0Color)
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

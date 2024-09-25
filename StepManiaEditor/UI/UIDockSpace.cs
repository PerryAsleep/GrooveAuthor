using System;
using ImGuiNET;
using Microsoft.Xna.Framework;
using Vector2 = System.Numerics.Vector2;

namespace StepManiaEditor;

/// <summary>
/// Class to manage docking UI windows.
/// </summary>
internal sealed class UIDockSpace
{
	private const string RootDockSpaceId = "DockSpace";
	private static Rectangle CentralNodeArea;

	/// <summary>
	/// </summary>
	/// <remarks>
	/// The contents of this function are largely taken from the internals of DockSpaceOverViewport.
	/// </remarks>
	public static void PrepareDockSpace()
	{
		var io = ImGui.GetIO();
		if ((io.ConfigFlags & ImGuiConfigFlags.DockingEnable) == 0)
			return;

		// Submit a window filling the entire viewport.
		var viewport = ImGui.GetMainViewport();
		ImGui.SetNextWindowPos(viewport.Pos);
		ImGui.SetNextWindowSize(viewport.Size);
		ImGui.SetNextWindowViewport(viewport.ID);

		var windowFlags = ImGuiWindowFlags.MenuBar
		                  | ImGuiWindowFlags.NoTitleBar
		                  | ImGuiWindowFlags.NoCollapse
		                  | ImGuiWindowFlags.NoResize
		                  | ImGuiWindowFlags.NoMove
		                  | ImGuiWindowFlags.NoDocking
		                  | ImGuiWindowFlags.NoBringToFrontOnFocus
		                  | ImGuiWindowFlags.NoNavFocus
		                  | ImGuiWindowFlags.NoBackground;

		var dockNodeFlags = ImGuiDockNodeFlags.PassthruCentralNode | ImGuiDockNodeFlags.NoDockingOverCentralNode;

		ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 0.0f);
		ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0.0f);
		ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
		ImGui.Begin($"WindowOverViewport_{viewport.ID}", windowFlags);
		ImGui.PopStyleVar(3);

		// Submit the dockSpace.
		var dockSpaceRootId = ImGui.GetID(RootDockSpaceId);
		ImGui.DockSpace(dockSpaceRootId, Vector2.Zero, dockNodeFlags);

		var rootWindowSize = ImGui.GetWindowSize();
		rootWindowSize.Y -= ImGui.GetFrameHeight();

		// Reset the windows.
		if (Preferences.Instance.PreferencesOptions.ResetWindows)
		{
			Preferences.Instance.PreferencesOptions.ResetWindows = false;

			// Clear previous layout.
			ImGui.DockBuilderRemoveNode(dockSpaceRootId);

			// Add root node encompassing entire viewport.
			ImGui.DockBuilderAddNode(dockSpaceRootId,
				dockNodeFlags | (ImGuiDockNodeFlags)ImGuiDockNodeFlagsPrivate.ImGuiDockNodeFlags_DockSpace);
			ImGui.DockBuilderSetNodePos(dockSpaceRootId, new Vector2(0, rootWindowSize.Y));
			ImGui.DockBuilderSetNodeSize(dockSpaceRootId, rootWindowSize);

			// Split into the left panel with song information, and the remainder.
			var leftPanelWidthAsPercentage = Math.Min(UISongProperties.DefaultSizeSmall.X / rootWindowSize.X, 0.5f);
			var dockSpaceIdLeftPanel = ImGui.DockBuilderSplitNode(dockSpaceRootId, ImGuiDir.Left, leftPanelWidthAsPercentage,
				out var _, out var rootRemainderDockSpaceId);

			// Split the left panel into song properties, chart list, and chart properties.
			var songPropertiesHeightAsPercentage = Math.Min(UISongProperties.DefaultSizeSmall.Y / rootWindowSize.Y, 0.9f);
			var dockSpaceIdSongProperties = ImGui.DockBuilderSplitNode(dockSpaceIdLeftPanel, ImGuiDir.Up,
				songPropertiesHeightAsPercentage, out var _, out var leftPanelRemainderDockSpaceId);
			// The max value for the clamp here is so the chart properties window cuts off at a nice point for 1080.
			var chartPropertiesHeightAsPercentage =
				Math.Clamp(UIChartProperties.DefaultSize.Y / (rootWindowSize.Y - UISongProperties.DefaultSizeSmall.Y), 0.25f,
					0.716854f);
			var dockSpaceIdChartProperties = ImGui.DockBuilderSplitNode(leftPanelRemainderDockSpaceId, ImGuiDir.Down,
				chartPropertiesHeightAsPercentage, out var _, out var dockSpaceIdChartList);

			// Split the root panel vertically to add the log and hotbar on the bottom.
			var bottomWindowHeightAsPercentage = Math.Min(UIChartPosition.DefaultHeight / rootWindowSize.Y, 0.5f);
			var dockSpaceIdBottomPanel = ImGui.DockBuilderSplitNode(rootRemainderDockSpaceId, ImGuiDir.Down,
				bottomWindowHeightAsPercentage, out var _, out rootRemainderDockSpaceId);

			// Split the bottom panel into the hotbar and the log.
			var hotbarWidthAsPercentage = Math.Min(0.9f,
				UIChartPosition.DefaultWidth / (rootWindowSize.X * (1 - leftPanelWidthAsPercentage)));
			var dockSpaceIdHotbar = ImGui.DockBuilderSplitNode(dockSpaceIdBottomPanel, ImGuiDir.Left,
				hotbarWidthAsPercentage, out var _, out var dockSpaceIdLog);

			// Dock windows into nodes.
			UISongProperties.Instance.DockIntoNode(dockSpaceIdSongProperties);
			UIChartProperties.Instance.DockIntoNode(dockSpaceIdChartProperties);
			UIChartList.Instance.DockIntoNode(dockSpaceIdChartList);
			UIChartPosition.Instance.DockIntoNode(dockSpaceIdHotbar);
			UILog.Instance.DockIntoNode(dockSpaceIdLog);
			ImGui.DockBuilderFinish(dockSpaceRootId);

			// Open the windows that have been docked.
			UIWindow.CloseAllWindows();
			UISongProperties.Instance.Open(true);
			UIChartProperties.Instance.Open(false);
			UIChartPosition.Instance.Open(false);
			UIChartList.Instance.Open(false);
			UILog.Instance.Open(false);
		}

		SetCentralNodeArea();

		ImGui.End();
	}

	public static void SetCentralNodeArea()
	{
		var dockSpaceRootId = ImGui.GetID(RootDockSpaceId);
		var node = ImGui.DockBuilderGetCentralNode(dockSpaceRootId);
		unsafe
		{
			if (node.NativePtr == null)
			{
				CentralNodeArea = Rectangle.Empty;
			}
		}

		CentralNodeArea = new Rectangle((int)node.Pos.X, (int)node.Pos.Y, (int)node.Size.X, (int)node.Size.Y);
	}

	public static Rectangle GetCentralNodeArea()
	{
		return CentralNodeArea;
	}
}

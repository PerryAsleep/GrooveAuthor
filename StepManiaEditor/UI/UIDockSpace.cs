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

	private static float WindowBorderSize = 0.0f;

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
		if (Preferences.Instance.PreferencesOptions.ResetLayout != PreferencesOptions.Layout.None)
		{
			var leftPanelWidth = UISongProperties.DefaultSizeSmall.X;
			if (Preferences.Instance.PreferencesOptions.ResetLayout == PreferencesOptions.Layout.Expanded)
				leftPanelWidth = UISongProperties.DefaultSize.X;

			// Clear previous layout.
			ImGui.DockBuilderRemoveNode(dockSpaceRootId);

			// Add root node encompassing entire viewport.
			ImGui.DockBuilderAddNode(dockSpaceRootId,
				dockNodeFlags | (ImGuiDockNodeFlags)ImGuiDockNodeFlagsPrivate.ImGuiDockNodeFlags_DockSpace);
			ImGui.DockBuilderSetNodePos(dockSpaceRootId, new Vector2(0, rootWindowSize.Y));
			ImGui.DockBuilderSetNodeSize(dockSpaceRootId, rootWindowSize);

			// Split into the left panel with song information, and the remainder.
			var leftPanelWidthAsPercentage = Math.Min(leftPanelWidth / rootWindowSize.X, 0.5f);
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
			var bottomWindowHeightAsPercentage = Math.Min(UIHotbar.DefaultHeight / rootWindowSize.Y, 0.5f);
			var dockSpaceIdBottomPanel = ImGui.DockBuilderSplitNode(rootRemainderDockSpaceId, ImGuiDir.Down,
				bottomWindowHeightAsPercentage, out var _, out rootRemainderDockSpaceId);

			// Split the bottom panel into the hotbar and the log.
			var hotbarWidthAsPercentage = Math.Min(0.9f,
				UIHotbar.DefaultWidth / (rootWindowSize.X * (1 - leftPanelWidthAsPercentage)));
			var dockSpaceIdHotbar = ImGui.DockBuilderSplitNode(dockSpaceIdBottomPanel, ImGuiDir.Left,
				hotbarWidthAsPercentage, out var _, out var dockSpaceIdLog);

			// Dock windows into nodes.
			UISongProperties.Instance.DockIntoNode(dockSpaceIdSongProperties);
			UIChartProperties.Instance.DockIntoNode(dockSpaceIdChartProperties);
			UIChartList.Instance.DockIntoNode(dockSpaceIdChartList);
			UIHotbar.Instance.DockIntoNode(dockSpaceIdHotbar);
			UILog.Instance.DockIntoNode(dockSpaceIdLog);
			ImGui.DockBuilderFinish(dockSpaceRootId);

			// Open the windows that have been docked.
			UIWindow.CloseAllWindows();
			UISongProperties.Instance.Open(true);
			UIChartProperties.Instance.Open(false);
			UIHotbar.Instance.Open(false);
			UIChartList.Instance.Open(false);
			UILog.Instance.Open(false);

			Preferences.Instance.PreferencesOptions.ResetLayout = PreferencesOptions.Layout.None;
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

	/// <summary>
	/// Begin a window over the Central Node.
	/// </summary>
	/// <remarks>
	/// The only way in ImGui to use clip rects is to do so in the context of a Window.
	/// You cannot just push an arbitrary clip rect outside of a Window. Because of this
	/// in order to clip Windows to the Central Node area, we need to use an invisible
	/// Window that doesn't capture inputs over the Central Node, and then put child
	/// Windows within that.
	/// </remarks>
	/// <returns>True if the Window began and false otherwise.</returns>
	public static bool BeginCentralNodeAreaWindow()
	{
		ImGui.SetNextWindowPos(new Vector2(CentralNodeArea.X, CentralNodeArea.Y));
		ImGui.SetNextWindowSize(new Vector2(CentralNodeArea.Width, CentralNodeArea.Height));

		WindowBorderSize = ImGui.GetStyle().WindowBorderSize;
		ImGui.GetStyle().WindowBorderSize = 0;
		return ImGui.Begin("##ChartAreaWindow"
			, ImGuiWindowFlags.NoTitleBar
			  | ImGuiWindowFlags.NoBackground
			  | ImGuiWindowFlags.NoResize
			  | ImGuiWindowFlags.NoMove
			  | ImGuiWindowFlags.NoDecoration
			  | ImGuiWindowFlags.NoFocusOnAppearing
			  | ImGuiWindowFlags.NoBringToFrontOnFocus
			  | ImGuiWindowFlags.NoNavInputs
			  | ImGuiWindowFlags.NoNavFocus
			  | ImGuiWindowFlags.NoInputs
			  | ImGuiWindowFlags.NoDocking
			  | ImGuiWindowFlags.NoSavedSettings
			  | ImGuiWindowFlags.NoScrollWithMouse
		);
	}

	/// <summary>
	/// Ends the Window from the previous call to BeginCentralNodeAreaWindow.
	/// </summary>
	public static void EndCentralNodeAreaWindow()
	{
		ImGui.GetStyle().WindowBorderSize = WindowBorderSize;
		ImGui.End();
	}
}

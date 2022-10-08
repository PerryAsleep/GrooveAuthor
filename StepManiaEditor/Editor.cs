using Fumen;
using ImGuiNET;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Fumen.ChartDefinition;
using Fumen.Converters;
using static Fumen.Utils;
using StepManiaLibrary;
using static StepManiaLibrary.Constants;
using static StepManiaEditor.Utils;
using ButtonState = Microsoft.Xna.Framework.Input.ButtonState;
using Keys = Microsoft.Xna.Framework.Input.Keys;
using Path = Fumen.Path;
using Vector2 = Microsoft.Xna.Framework.Vector2;
using static Fumen.Converters.SMCommon;
using System.Text;

namespace StepManiaEditor
{
	public class Editor : Game
	{
		/// <summary>
		/// How to control the position of the Chart when scrolling.
		/// </summary>
		public enum ScrollMode
		{
			/// <summary>
			/// Scrolling moves time.
			/// This is the mode used playing the song.
			/// </summary>
			Time,

			/// <summary>
			/// Scrolling moves rows.
			/// </summary>
			Row,
		}

		/// <summary>
		/// How to space Chart Events when rendering.
		/// </summary>
		public enum SpacingMode
		{
			/// <summary>
			/// Spacing between notes is based on time.
			/// Using a Time ScrollMode with a ConstantTime SpacingMode is effectively a CMOD.
			/// </summary>
			ConstantTime,

			/// <summary>
			/// Spacing between notes is based on row.
			/// </summary>
			ConstantRow,

			/// <summary>
			/// Spacing between nodes varies based on rate altering Events in the Chart.
			/// Using TimeBased ScrollMode with a Variable SpacingMode is effectively an XMOD.
			/// </summary>
			Variable,
		}

		/// <summary>
		/// How the WaveForm should scroll when not using ConstantTime SpacingMode.
		/// </summary>
		public enum WaveFormScrollMode
		{
			/// <summary>
			/// Scroll matching the current tempo.
			/// </summary>
			CurrentTempo,

			/// <summary>
			/// Scroll matching the current tempo and rate multipliers.
			/// </summary>
			CurrentTempoAndRate,

			/// <summary>
			/// Scroll matching the most common tempo of the Chart.
			/// </summary>
			MostCommonTempo
		}

		public class SnapData
		{
			public int Rows;
			public string Texture;
		}
		private SnapData[] SnapLevels;
		private int SnapIndex = 0;
		
		private Vector2 FocalPoint;

		private string PendingOpenFileName;
		private SMCommon.ChartType PendingOpenFileChartType;
		private SMCommon.ChartDifficultyType PendingOpenFileChartDifficultyType;

		private static readonly SMCommon.ChartType[] SupportedChartTypes = new[]
		{
			SMCommon.ChartType.dance_single,
			SMCommon.ChartType.dance_double,
			//dance_couple,
			//dance_routine,
			SMCommon.ChartType.dance_solo,
			SMCommon.ChartType.dance_threepanel,

			SMCommon.ChartType.pump_single,
			SMCommon.ChartType.pump_halfdouble,
			SMCommon.ChartType.pump_double,
			//pump_couple,
			//pump_routine,
			SMCommon.ChartType.smx_beginner,
			SMCommon.ChartType.smx_single,
			SMCommon.ChartType.smx_dual,
			SMCommon.ChartType.smx_full,
			//SMCommon.ChartType.smx_team,
		};

		private GraphicsDeviceManager Graphics;
		private SpriteBatch SpriteBatch;
		private ImGuiRenderer ImGuiRenderer;
		private WaveFormRenderer WaveFormRenderer;
		private SoundManager SoundManager;
		private MusicManager MusicManager;
		private MiniMap MiniMap;
		private ArrowGraphicManager ArrowGraphicManager;
		private UISongProperties UISongProperties;
		private UIChartProperties UIChartProperties;
		private UIChartList UIChartList;
		private UIWaveFormPreferences UIWaveFormPreferences;
		private UIScrollPreferences UIScrollPreferences;
		private UIMiniMapPreferences UIMiniMapPreferences;
		private UIAnimationsPreferences UIAnimationsPreferences;
		private UIOptions UIOptions;

		private TextureAtlas TextureAtlas;

		private Effect FxaaEffect;
		private RenderTarget2D WaveformRenderTarget;

		private CancellationTokenSource LoadSongCancellationTokenSource;
		private Task LoadSongTask;

		private Dictionary<SMCommon.ChartType, PadData> PadDataByChartType = new Dictionary<SMCommon.ChartType, PadData>();
		private Dictionary<SMCommon.ChartType, StepGraph> StepGraphByChartType = new Dictionary<SMCommon.ChartType, StepGraph>();

		private double PlaybackStartTime;
		private Stopwatch PlaybackStopwatch;
		
		private EditorSong EditorSong;
		private EditorChart ActiveChart;

		private List<EditorEvent> VisibleEvents = new List<EditorEvent>();
		private List<EditorMarkerEvent> VisibleMarkers = new List<EditorMarkerEvent>();
		private Receptor[] Receptors = null;

		private EditorEvent[] NextAutoPlayNotes = null;

		private double WaveFormPPS = 1.0;

		// Movement controls.
		private EditorPosition Position;
		private bool UpdatingSongTimeDirectly;
		private double SongTimeInterpolationTimeStart = 0.0;
		private double SongTimeAtStartOfInterpolation = 0.0;
		private double DesiredSongTime = 0.0;

		// Note controls
		private LaneEditState[] LaneEditStates;

		// Zoom controls
		private double ZoomInterpolationTimeStart = 0.0;
		private double Zoom = 1.0;
		private double ZoomAtStartOfInterpolation = 1.0;
		private double DesiredZoom = 1.0;

		private KeyCommandManager KeyCommandManager;
		private bool Playing = false;
		private bool PlayingPreview = false;
		private bool MiniMapCapturingMouse = false;
		private bool StartPlayingWhenMiniMapDone = false;
		private MouseState PreviousMiniMapMouseState;
		private int MouseScrollValue = 0;

		private uint MaxScreenHeight;

		// Fonts
		private ImFontPtr ImGuiFont;
		private SpriteFont MonogameFont_MPlus1Code_Medium;

		// Debug
		private bool ParallelizeUpdateLoop = false;
		private bool RenderChart = true;
		private double UpdateTimeTotal;
		private double UpdateTimeWaveForm;
		private double UpdateTimeMiniMap;
		private double UpdateTimeChartEvents;
		private double DrawTimeTotal;

		// Logger
		private readonly LinkedList<Logger.LogMessage> LogBuffer = new LinkedList<Logger.LogMessage>();
		private readonly object LogBufferLock = new object();

		private bool ShowImGuiTestWindow = true;

		public Editor()
		{
			var logFileName = "StepManiaChartEditor " + DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss") + ".log";
			Logger.StartUp(new Logger.Config
			{
				WriteToConsole = false,

				WriteToFile = true,
				LogFilePath = Path.Combine(@"C:\Users\perry\Projects\Fumen\Logs", logFileName),
				LogFileFlushIntervalSeconds = 20,
				LogFileBufferSizeBytes = 10240,

				WriteToBuffer = true,
				BufferSize = 1024,
				BufferLock = LogBufferLock,
				Buffer = LogBuffer
			});

			// Load Preferences synchronously so they can be used immediately.
			Preferences.Load();

			Position = new EditorPosition(OnPositionChanged);

			FocalPoint = new Vector2(Preferences.Instance.WindowWidth >> 1, 100 + (DefaultArrowWidth >> 1));

			SoundManager = new SoundManager();
			MusicManager = new MusicManager(SoundManager);

			Graphics = new GraphicsDeviceManager(this);
			Graphics.GraphicsProfile = GraphicsProfile.HiDef;

			KeyCommandManager = new KeyCommandManager();
			KeyCommandManager.Register(new KeyCommandManager.Command(new[] { Keys.LeftControl, Keys.Z }, OnUndo, true));
			KeyCommandManager.Register(new KeyCommandManager.Command(new[] { Keys.LeftControl, Keys.LeftShift, Keys.Z }, OnRedo, true));
			KeyCommandManager.Register(new KeyCommandManager.Command(new[] { Keys.LeftControl, Keys.Y }, OnRedo, true));
			KeyCommandManager.Register(new KeyCommandManager.Command(new[] { Keys.LeftControl, Keys.O }, OnOpen, false));
			KeyCommandManager.Register(new KeyCommandManager.Command(new[] { Keys.LeftControl, Keys.S }, OnSave, false));
			KeyCommandManager.Register(new KeyCommandManager.Command(new[] { Keys.LeftControl, Keys.E }, OnExport, false));
			KeyCommandManager.Register(new KeyCommandManager.Command(new[] { Keys.LeftControl, Keys.LeftShift, Keys.E }, OnExportAs, false));
			KeyCommandManager.Register(new KeyCommandManager.Command(new[] { Keys.LeftControl, Keys.N }, OnNew, false));
			KeyCommandManager.Register(new KeyCommandManager.Command(new[] { Keys.Space }, OnTogglePlayback, false));
			KeyCommandManager.Register(new KeyCommandManager.Command(new[] { Keys.P }, OnTogglePlayPreview, false));
			KeyCommandManager.Register(new KeyCommandManager.Command(new[] { Keys.Escape }, OnEscape, false));
			KeyCommandManager.Register(new KeyCommandManager.Command(new[] { Keys.Left }, OnDecreaseSnap, true));
			KeyCommandManager.Register(new KeyCommandManager.Command(new[] { Keys.Right }, OnIncreaseSnap, true));
			KeyCommandManager.Register(new KeyCommandManager.Command(new[] { Keys.Up }, OnMoveUp, true, null, true));
			KeyCommandManager.Register(new KeyCommandManager.Command(new[] { Keys.Down }, OnMoveDown, true, null, true));
			KeyCommandManager.Register(new KeyCommandManager.Command(new[] { Keys.PageUp }, OnMoveToPreviousMeasure, true, null, true));
			KeyCommandManager.Register(new KeyCommandManager.Command(new[] { Keys.PageDown }, OnMoveToNextMeasure, true, null, true));
			KeyCommandManager.Register(new KeyCommandManager.Command(new[] { Keys.Home }, OnMoveToChartStart, false, null, true));
			KeyCommandManager.Register(new KeyCommandManager.Command(new[] { Keys.End }, OnMoveToChartEnd, false, null, true));

			var arrowInputKeys = new []{ Keys.D1, Keys.D2, Keys.D3, Keys.D4, Keys.D5, Keys.D6, Keys.D7, Keys.D8, Keys.D9, Keys.D0 };
			var index = 0;
			foreach (var key in arrowInputKeys)
			{
				var capturedIndex = index;
				void Down() { OnLaneInputDown(capturedIndex); }
				void Up() { OnLaneInputUp(capturedIndex); }
				index++;
				KeyCommandManager.Register(new KeyCommandManager.Command(new[] { key }, Down, false, Up, true));
			}
			KeyCommandManager.Register(new KeyCommandManager.Command(new[] { Keys.LeftShift }, OnShiftDown, false, OnShiftUp, true));

			Content.RootDirectory = "Content";
			IsMouseVisible = true;
			Window.AllowUserResizing = true;
			Window.ClientSizeChanged += OnResize;

			IsFixedTimeStep = false;
			Graphics.SynchronizeWithVerticalRetrace = true;

			// Set up snap levels for all valid denominators.
			SnapLevels = new SnapData[SMCommon.ValidDenominators.Length + 1];
			SnapLevels[0] = new SnapData { Rows = 0 };
			for (var denominatorIndex = 0; denominatorIndex < SMCommon.ValidDenominators.Length; denominatorIndex++)
			{
				SnapLevels[denominatorIndex + 1] = new SnapData
				{
					Rows = SMCommon.MaxValidDenominator / SMCommon.ValidDenominators[denominatorIndex],
					Texture = ArrowGraphicManager.GetSnapIndicatorTexture(SMCommon.ValidDenominators[denominatorIndex])
				};
			}

			UpdateWindowTitle();
		}

		protected override void Initialize()
		{
			var p = Preferences.Instance;
			Graphics.PreferredBackBufferHeight = p.WindowHeight;
			Graphics.PreferredBackBufferWidth = p.WindowWidth;
			Graphics.IsFullScreen = p.WindowFullScreen;
			Graphics.ApplyChanges();

			if (p.WindowMaximized)
			{
				((Form)Control.FromHandle(Window.Handle)).WindowState = FormWindowState.Maximized;
			}

			ImGuiRenderer = new ImGuiRenderer(this);
			// TODO: Load font from install directory
			ImGuiFont = ImGui.GetIO().Fonts.AddFontFromFileTTF(
				@"C:\Users\perry\Projects\Fumen\StepManiaEditor\Content\Mplus1Code-Medium.ttf",
				15,
				null,
				ImGui.GetIO().Fonts.GetGlyphRangesJapanese());
			ImGuiRenderer.RebuildFontAtlas();
			ImGuiLayoutUtils.SetFont(ImGuiFont);

			MonogameFont_MPlus1Code_Medium = Content.Load<SpriteFont>("mplus1code-medium");

			foreach (var adapter in GraphicsAdapter.Adapters)
			{
				MaxScreenHeight = Math.Max(MaxScreenHeight, (uint)adapter.CurrentDisplayMode.Height);
			}

			EditorHoldEndNoteEvent.SetScreenHeight(MaxScreenHeight);

			WaveFormRenderer = new WaveFormRenderer(GraphicsDevice, WaveFormTextureWidth, MaxScreenHeight);
			WaveFormRenderer.SetXPerChannelScale(p.PreferencesWaveForm.WaveFormMaxXPercentagePerChannel);
			WaveFormRenderer.SetSoundMipMap(MusicManager.GetMusicMipMap());
			WaveFormRenderer.SetFocalPointY((int)FocalPoint.Y);
			WaveformRenderTarget = new RenderTarget2D(
				GraphicsDevice,
				WaveFormTextureWidth,
				(int)MaxScreenHeight,
				false,
				GraphicsDevice.PresentationParameters.BackBufferFormat,
				DepthFormat.Depth24);

			MiniMap = new MiniMap(GraphicsDevice, new Rectangle(0, 0, 0, 0));
			MiniMap.SetSelectMode(p.PreferencesMiniMap.MiniMapSelectMode);

			TextureAtlas = new TextureAtlas(GraphicsDevice, 2048, 2048, 1);

			UISongProperties = new UISongProperties(this, GraphicsDevice, ImGuiRenderer);
			UIChartProperties = new UIChartProperties(this);
			UIChartList = new UIChartList(this);
			UIWaveFormPreferences = new UIWaveFormPreferences(this, MusicManager);
			UIScrollPreferences = new UIScrollPreferences();
			UIMiniMapPreferences = new UIMiniMapPreferences(this);
			UIAnimationsPreferences = new UIAnimationsPreferences();
			UIOptions = new UIOptions();

			base.Initialize();
		}

		protected override void EndRun()
		{
			Preferences.Save();
			Logger.Shutdown();
			base.EndRun();
		}

		protected override void LoadContent()
		{
			SpriteBatch = new SpriteBatch(GraphicsDevice);

			// Load textures from disk and add them to the Texture Atlas.
			foreach (var textureId in ArrowGraphicManager.GetAllTextureIds())
				TextureAtlas.AddTexture(textureId, Content.Load<Texture2D>(textureId), true);

			// Generate and add measure marker texture.
			var measureMarkerTexture = new Texture2D(GraphicsDevice, DefaultArrowWidth, 1);
			var textureData = new uint[DefaultArrowWidth];
			for (var i = 0; i < DefaultArrowWidth; i++)
				textureData[i] = 0xFFFFFFFF;
			measureMarkerTexture.SetData(textureData);
			TextureAtlas.AddTexture(TextureIdMeasureMarker, measureMarkerTexture, true);

			// Generate and add beat marker texture.
			var beatMarkerTexture = new Texture2D(GraphicsDevice, DefaultArrowWidth, 1);
			for (var i = 0; i < DefaultArrowWidth; i++)
				textureData[i] = 0xFF7F7F7F;
			beatMarkerTexture.SetData(textureData);
			TextureAtlas.AddTexture(TextureIdBeatMarker, beatMarkerTexture, true);

			InitPadDataAndStepGraphsAsync();

			// If we have a saved file to open, open it now.
			if (Preferences.Instance.PreferencesOptions.OpenLastOpenedFileOnLaunch
			    && Preferences.Instance.RecentFiles.Count > 0)
			{
				OpenSongFileAsync(Preferences.Instance.RecentFiles[0].FileName,
					Preferences.Instance.RecentFiles[0].LastChartType,
					Preferences.Instance.RecentFiles[0].LastChartDifficultyType);
			}

			FxaaEffect = Content.Load<Effect>("fxaa");

			base.LoadContent();
		}

		public void OnResize(object sender, EventArgs e)
		{
			var maximized = ((Form)Control.FromHandle(Window.Handle)).WindowState == FormWindowState.Maximized;

			// Update window preferences.
			if (!maximized)
			{
				Preferences.Instance.WindowWidth = Graphics.GraphicsDevice.Viewport.Width;
				Preferences.Instance.WindowHeight = Graphics.GraphicsDevice.Viewport.Height;
			}

			Preferences.Instance.WindowFullScreen = Graphics.IsFullScreen;
			Preferences.Instance.WindowMaximized = maximized;

			// Update FocalPoint.
			FocalPoint.X = Graphics.GraphicsDevice.Viewport.Width >> 1;
		}

		protected override void Update(GameTime gameTime)
		{
			var stopWatch = new Stopwatch();
			stopWatch.Start();

			ProcessInput(gameTime);

			TextureAtlas.Update();

			if (!Playing)
			{
				if (!Position.SongTime.DoubleEquals(DesiredSongTime))
				{
					UpdatingSongTimeDirectly = true;

					Position.SongTime = Interpolation.Lerp(
						SongTimeAtStartOfInterpolation,
						DesiredSongTime,
						SongTimeInterpolationTimeStart,
						SongTimeInterpolationTimeStart + 0.1,
						gameTime.TotalGameTime.TotalSeconds);

					MusicManager.SetMusicTimeInSeconds(Position.SongTime);

					UpdatingSongTimeDirectly = false;
				}
			}

			if (!Zoom.DoubleEquals(DesiredZoom))
			{
				SetZoom(Interpolation.Lerp(
					ZoomAtStartOfInterpolation,
					DesiredZoom,
					ZoomInterpolationTimeStart,
					ZoomInterpolationTimeStart + 0.1,
					gameTime.TotalGameTime.TotalSeconds), false);
			}

			var pOptions = Preferences.Instance.PreferencesOptions;
			MusicManager.SetPreviewParameters(
				EditorSong?.SampleStart ?? 0.0,
				EditorSong?.SampleLength ?? 0.0,
				pOptions.PreviewFadeInTime,
				pOptions.PreviewFadeOutTime);

			if (Playing)
			{
				UpdatingSongTimeDirectly = true;

				Position.SongTime = PlaybackStartTime + PlaybackStopwatch.Elapsed.TotalSeconds;

				MusicManager.Update(Position.SongTime);
				if (MusicManager.IsMusicLoaded())
				{
					// The goal is to set the SongTime to match the actual time of the music
					// being played through FMOD. Querying the time from FMOD does not have high
					// enough precision, so we need to use our own timer.
					// The best C# timer for this task is a StopWatch, but StopWatches have been
					// reported to drift, sometimes up to a half a second per hour. If we detect
					// it has drifted significantly by comparing it to the time from FMOD, then
					// snap it back.
					var maxDeviation = 0.1;
					var fmodSongTime = MusicManager.GetMusicTimeInSeconds();
					if (Position.SongTime >= 0.0 && Position.SongTime < MusicManager.GetMusicLengthInSeconds())
					{
						if (Position.SongTime - fmodSongTime > maxDeviation)
						{
							PlaybackStartTime -= (0.5 * maxDeviation);
							Position.SongTime = PlaybackStartTime + PlaybackStopwatch.Elapsed.TotalSeconds;
						}
						else if (fmodSongTime - Position.SongTime > maxDeviation)
						{
							PlaybackStartTime += (0.5 * maxDeviation);
							Position.SongTime = PlaybackStartTime + PlaybackStopwatch.Elapsed.TotalSeconds;
						}
					}
				}

				DesiredSongTime = Position.SongTime;

				UpdatingSongTimeDirectly = false;
			}
			else
			{
				MusicManager.Update(Position.SongTime);
			}

			Action timedUpdateChartEvents = () =>
			{
				var s = new Stopwatch();
				s.Start();
				UpdateChartEvents();
				UpdateAutoPlay();
				s.Stop();
				UpdateTimeChartEvents = s.Elapsed.TotalSeconds;
			};
			Action timedUpdateMiniMap = () =>
			{
				var s = new Stopwatch();
				s.Start();
				UpdateMiniMap();
				s.Stop();
				UpdateTimeMiniMap = s.Elapsed.TotalSeconds;
			};
			Action timedUpdateWaveForm = () =>
			{
				var s = new Stopwatch();
				s.Start();
				UpdateWaveFormRenderer();
				s.Stop();
				UpdateTimeWaveForm = s.Elapsed.TotalSeconds;
			};

			// CPU heavy updates.
			// This kind of parallelization isn't helpful as it has to create new threads each frame.
			if (ParallelizeUpdateLoop)
			{
				Parallel.Invoke(
					() => timedUpdateChartEvents(),
					() => timedUpdateMiniMap(),
					() => timedUpdateWaveForm());
			}
			else
			{
				timedUpdateChartEvents();
				timedUpdateMiniMap();
				timedUpdateWaveForm();
			}

			if (Receptors != null)
			{
				foreach (var receptor in Receptors)
				{
					receptor.Update(Playing, Position.ChartPosition);
				}
			}

			base.Update(gameTime);

			stopWatch.Stop();
			UpdateTimeTotal = stopWatch.Elapsed.TotalSeconds;
		}

		private void ProcessInput(GameTime gameTime)
		{
			var inFocus = IsApplicationFocused();

			// ImGui needs to update it's frame even the app is not in focus.
			// There may be a way to decouple input processing and advancing the frame, but for now
			// use the ImGuiRenderer.Update method and give it a flag so it knows to not process input.
			(var imGuiWantMouse, var imGuiWantKeyboard) = ImGuiRenderer.Update(gameTime, inFocus);

			// Do not do any further input processing if the application does not have focus.
			if (!inFocus)
				return;
			
			var pScroll = Preferences.Instance.PreferencesScroll;

			// Process Keyboard Input.
			if (imGuiWantKeyboard)
				KeyCommandManager.CancelAllCommands();
			else
				KeyCommandManager.Update(gameTime.TotalGameTime.TotalSeconds);
			var scrollShouldZoom = !imGuiWantKeyboard && KeyCommandManager.IsKeyDown(Keys.LeftControl);

			// Process Mouse Input
			var mouseState = Mouse.GetState();
			var newMouseScrollValue = mouseState.ScrollWheelValue;

			if (imGuiWantMouse)
			{
				// Update our last tracked mouse scroll value even if imGui captured it so that
				// once we start capturing input again we don't process a scroll.
				MouseScrollValue = newMouseScrollValue;
			}
			else
			{
				ProcessInputForMiniMap(mouseState);


				if (KeyCommandManager.IsKeyDown(Keys.OemPlus))
				{
					Zoom *= 1.0001;
					DesiredZoom = Zoom;
				}
				if (KeyCommandManager.IsKeyDown(Keys.OemMinus))
				{
					Zoom /= 1.0001;
					DesiredZoom = Zoom;
				}

				//mouseState.

				// TODO: wtf are these values
				if (scrollShouldZoom)
				{
					if (MouseScrollValue < newMouseScrollValue)
					{
						DesiredZoom *= 1.2;
						ZoomInterpolationTimeStart = gameTime.TotalGameTime.TotalSeconds;
						ZoomAtStartOfInterpolation = Zoom;
						MouseScrollValue = newMouseScrollValue;
					}

					if (MouseScrollValue > newMouseScrollValue)
					{
						DesiredZoom /= 1.2;
						ZoomInterpolationTimeStart = gameTime.TotalGameTime.TotalSeconds;
						ZoomAtStartOfInterpolation = Zoom;
						MouseScrollValue = newMouseScrollValue;
					}
				}
				else
				{
					if (MouseScrollValue < newMouseScrollValue)
					{
						var delta = 0.25 * (1.0 / Zoom);
						if (Playing)
						{
							PlaybackStartTime -= delta;
							Position.SongTime = PlaybackStartTime + PlaybackStopwatch.Elapsed.TotalSeconds;

							if (pScroll.StopPlaybackWhenScrolling)
							{
								StopPlayback();
							}
							else
							{
								MusicManager.SetMusicTimeInSeconds(Position.SongTime);
							}
							UpdateAutoPlayFromScrolling();
						}
						else
						{
							if (SnapLevels[SnapIndex].Rows == 0)
							{
								DesiredSongTime -= delta;
								SongTimeInterpolationTimeStart = gameTime.TotalGameTime.TotalSeconds;
								SongTimeAtStartOfInterpolation = Position.SongTime;
							}
							else
							{
								OnMoveUp();
							}
						}

						MouseScrollValue = newMouseScrollValue;
					}

					if (MouseScrollValue > newMouseScrollValue)
					{
						var delta = 0.25 * (1.0 / Zoom);
						if (Playing)
						{
							PlaybackStartTime += delta;
							Position.SongTime = PlaybackStartTime + PlaybackStopwatch.Elapsed.TotalSeconds;

							if (pScroll.StopPlaybackWhenScrolling)
							{
								StopPlayback();
							}
							else
							{
								MusicManager.SetMusicTimeInSeconds(Position.SongTime);
							}
							UpdateAutoPlayFromScrolling();
						}
						else
						{
							if (SnapLevels[SnapIndex].Rows == 0)
							{
								DesiredSongTime += delta;
								SongTimeInterpolationTimeStart = gameTime.TotalGameTime.TotalSeconds;
								SongTimeAtStartOfInterpolation = Position.SongTime;
							}
							else
							{
								OnMoveDown();
							}
						}

						MouseScrollValue = newMouseScrollValue;
					}
				}
			}
		}

		private void StartPlayback()
		{
			if (Playing)
				return;

			StopPreview();

			if (!MusicManager.IsMusicLoaded() || Position.SongTime < 0.0 || Position.SongTime > MusicManager.GetMusicLengthInSeconds())
			{
				PlaybackStartTime = Position.SongTime;
			}
			else
			{
				PlaybackStartTime = MusicManager.GetMusicTimeInSeconds();
			}

			PlaybackStopwatch = new Stopwatch();
			PlaybackStopwatch.Start();
			MusicManager.StartPlayback(Position.SongTime);

			Playing = true;
		}

		private void StopPlayback()
		{
			if (!Playing)
				return;

			PlaybackStopwatch.Stop();
			MusicManager.StopPlayback();
			StopAutoPlay();

			Playing = false;
		}

		private void OnPositionChanged()
		{
			// Update events being edited.
			UpdateLaneEditStatesFromPosition();

			// Update the music time
			if (!UpdatingSongTimeDirectly)
			{
				var songTime = Position.SongTime;
				DesiredSongTime = songTime;
				MusicManager.SetMusicTimeInSeconds(songTime);

				if (Playing)
				{
					PlaybackStartTime = songTime;
					PlaybackStopwatch = new Stopwatch();
					PlaybackStopwatch.Start();
				}
			}
		}

		private void SetZoom(double zoom, bool setDesiredZoom)
		{
			Zoom = zoom;
			if (setDesiredZoom)
			{
				DesiredZoom = zoom;
			}
		}

		protected override void Draw(GameTime gameTime)
		{
			var stopWatch = new Stopwatch();
			stopWatch.Start();

			GraphicsDevice.Clear(Color.Black);

			DrawWaveForm();

			ImGui.PushFont(ImGuiFont);

			SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied);

			DrawMiniMap();
			if (RenderChart)
			{
				DrawReceptors();
				DrawSnapIndicators();
				DrawChartEvents();
				DrawReceptorForegroundEffects();
			}
			SpriteBatch.End();

			DrawGui(gameTime);

			ImGui.PopFont();
			ImGuiRenderer.AfterLayout();

			//SpriteBatch.End();

			base.Draw(gameTime);

			stopWatch.Stop();
			DrawTimeTotal = stopWatch.Elapsed.TotalSeconds;
		}

		private void DrawReceptors()
		{
			if (ActiveChart == null || ArrowGraphicManager == null || Receptors == null)
				return;

			foreach(var receptor in Receptors)
				receptor.Draw(FocalPoint, Zoom, TextureAtlas, SpriteBatch);
		}

		private void DrawSnapIndicators()
		{
			if (ActiveChart == null || ArrowGraphicManager == null || Receptors == null)
				return;
			var snapTextureId = SnapLevels[SnapIndex].Texture;
			if (string.IsNullOrEmpty(snapTextureId))
				return;
			var (receptorTextureId, _) = ArrowGraphicManager.GetReceptorTexture(0);
			var (receptorTextureWidth, _) = TextureAtlas.GetDimensions(receptorTextureId);
			var zoom = Zoom;
			if (zoom > 1.0)
				zoom = 1.0;
			var receptorLeftEdge = FocalPoint.X - (ActiveChart.NumInputs * 0.5 * receptorTextureWidth * zoom);

			var (snapTextureWidth, snapTextureHeight) = TextureAtlas.GetDimensions(snapTextureId);
			var leftX = receptorLeftEdge - snapTextureWidth * 0.5 * zoom;
			var y = FocalPoint.Y;

			TextureAtlas.Draw(
				snapTextureId,
				SpriteBatch,
				new Vector2((float)leftX, y),
				new Vector2((float)(snapTextureWidth * 0.5), (float)(snapTextureHeight * 0.5)),
				Color.White,
				(float)zoom,
				0.0f,
				SpriteEffects.None);
		}

		private void DrawReceptorForegroundEffects()
		{
			if (ActiveChart == null || ArrowGraphicManager == null || Receptors == null)
				return;

			foreach (var receptor in Receptors)
				receptor.DrawForegroundEffects(FocalPoint, Zoom, TextureAtlas, SpriteBatch);
		}

		private void UpdateWaveFormRenderer()
		{
			var pWave = Preferences.Instance.PreferencesWaveForm;

			// Performance optimization. Do not update the texture if we won't render it.
			if (!pWave.ShowWaveForm)
				return;

			// Determine the sparse color.
			var sparseColor = pWave.WaveFormSparseColor;
			switch (pWave.WaveFormSparseColorOption)
			{
				case UIWaveFormPreferences.SparseColorOption.DarkerDenseColor:
					sparseColor.X = pWave.WaveFormDenseColor.X * pWave.WaveFormSparseColorScale;
					sparseColor.Y = pWave.WaveFormDenseColor.Y * pWave.WaveFormSparseColorScale;
					sparseColor.Z = pWave.WaveFormDenseColor.Z * pWave.WaveFormSparseColorScale;
					break;
				case UIWaveFormPreferences.SparseColorOption.SameAsDenseColor:
					sparseColor = pWave.WaveFormDenseColor;
					break;
			}

			// Update the WaveFormRenderer.
			WaveFormRenderer.SetFocalPointY((int)FocalPoint.Y);
			WaveFormRenderer.SetXPerChannelScale(pWave.WaveFormMaxXPercentagePerChannel);
			WaveFormRenderer.SetDenseScale(pWave.DenseScale);
			WaveFormRenderer.SetColors(
				pWave.WaveFormDenseColor.X, pWave.WaveFormDenseColor.Y, pWave.WaveFormDenseColor.Z,
				sparseColor.X, sparseColor.Y, sparseColor.Z);
			WaveFormRenderer.SetScaleXWhenZooming(pWave.WaveFormScaleXWhenZooming);
			WaveFormRenderer.Update(Position.SongTime, Zoom, WaveFormPPS);
		}

		private void DrawWaveForm()
		{
			var p = Preferences.Instance.PreferencesWaveForm;
			if (!p.ShowWaveForm)
				return;

			var x = (int)FocalPoint.X - (WaveFormTextureWidth >> 1);

			// No antialiasing, just draw the waveform.
			if (!p.AntiAlias)
			{
				SpriteBatch.Begin();
				WaveFormRenderer.Draw(SpriteBatch, x, 0);
				SpriteBatch.End();
				return;
			}

			// Anti-aliasing. Render the waveform to a render target to run FXAA on.
			GraphicsDevice.SetRenderTarget(WaveformRenderTarget);
			SpriteBatch.Begin();
			WaveFormRenderer.Draw(SpriteBatch, 0, 0);
			SpriteBatch.End();
			GraphicsDevice.SetRenderTarget(null);

			// Configure FXAA.
			FxaaEffect.CurrentTechnique = FxaaEffect.Techniques["fxaa"];
			FxaaEffect.Parameters["fxaaQualitySubpix"].SetValue(p.AntiAliasSubpix);
			FxaaEffect.Parameters["fxaaQualityEdgeThreshold"].SetValue(p.AntiAliasEdgeThreshold);
			FxaaEffect.Parameters["fxaaQualityEdgeThresholdMin"].SetValue(p.AntiAliasEdgeThresholdMin);
			FxaaEffect.Parameters["inverseRenderTargetWidth"].SetValue(1.0f / WaveformRenderTarget.Width);
			FxaaEffect.Parameters["inverseRenderTargetHeight"].SetValue(1.0f / WaveformRenderTarget.Height);
			FxaaEffect.Parameters["renderTargetTexture"].SetValue(WaveformRenderTarget);

			// Draw the render target with FXAA.
			SpriteBatch.Begin(SpriteSortMode.Deferred, null, null, null, null, FxaaEffect);
			SpriteBatch.Draw((Texture2D)WaveformRenderTarget, new Rectangle(x, 0, WaveformRenderTarget.Width, WaveformRenderTarget.Height), Color.White);
			SpriteBatch.End();
		}

		/// <summary>
		/// Given a RedBlackTree and a value, find the greatest preceding value.
		/// If no value precedes the given value, instead find the least value that follows or is
		/// equal to the given value.
		/// </summary>
		/// <remarks>
		/// This is a common pattern when knowing a position or a time and wanting to find the first event to
		/// start enumerator over for rendering.
		/// </remarks>
		/// <typeparam name="T">Type of data in the tree.</typeparam>
		/// <typeparam name="U">Type of data to compare against for finding.</typeparam>
		/// <param name="tree">RedBlackTree to search.</param>
		/// <param name="data">Value to use for comparisons.</param>
		/// <param name="comparer">Comparer function.</param>
		/// <returns>Enumerator to best value or null if a value could not be found.</returns>
		private RedBlackTree<EditorEvent>.Enumerator FindBest(RedBlackTree<EditorEvent> tree, int row)
		{
			// This is a bit hacky. Leveraging TimeSignature events being the first event by row in
			// in SMEventComparer
			var pos = new EditorTimeSignatureEvent(ActiveChart, new TimeSignature(new Fraction(4, 4))
			{
				IntegerPosition = row
			});

			var enumerator = tree.FindGreatestPreceding(pos, false);
			if (enumerator == null)
				enumerator = tree.FindLeastFollowing(pos, true);
			return enumerator;
		}

		private RedBlackTree<EditorRateAlteringEvent>.Enumerator FindBest(RedBlackTree<EditorRateAlteringEvent> tree, int row)
		{
			var pos = new EditorDummyRateAlteringEvent(ActiveChart, null)
			{
				Row = row
			};
			var enumerator = tree.FindGreatestPreceding(pos, false);
			if (enumerator == null)
				enumerator = tree.FindLeastFollowing(pos, true);
			return enumerator;
		}

		/// <summary>
		/// Sets VisibleEvents and VisibleMarkers to lists of all the currently visible objects
		/// based on the current time or position and the SpacingMode.
		/// </summary>
		/// <remarks>
		/// Sets the WaveFormPPS.
		/// </remarks>
		private void UpdateChartEvents()
		{
			// TODO: Cleanup
			// TODO: Crash when switching songs from doubles to singles.

			VisibleEvents.Clear();
			VisibleMarkers.Clear();

			if (ActiveChart == null || ActiveChart.EditorEvents == null || ArrowGraphicManager == null)
				return;

			var pScroll = Preferences.Instance.PreferencesScroll;

			List<EditorEvent> holdBodyEvents = new List<EditorEvent>();
			List<EditorEvent> noteEvents = new List<EditorEvent>();

			var screenHeight = Graphics.GraphicsDevice.Viewport.Height;

			var spacingZoom = Zoom;
			var sizeZoom = Zoom;
			if (sizeZoom > 1.0)
				sizeZoom = 1.0;
			var arrowSize = DefaultArrowWidth * sizeZoom;
			var (holdCapTexture, _) = ArrowGraphicManager.GetHoldEndTexture(0, 0, false);
			var (_, holdCapTextureHeight) = TextureAtlas.GetDimensions(holdCapTexture);
			var holdCapHeight = holdCapTextureHeight * sizeZoom;

			var numArrows = ActiveChart.NumInputs;
			var xStart = FocalPoint.X - (numArrows * arrowSize * 0.5);

			// Set up the MiscEventWidgetLayoutManager
			var miscEventAlpha = (float)Interpolation.Lerp(1.0, 0.0, MiscEventScaleToStartingFading, MiscEventMinScale, sizeZoom);
			const int widgetStartPadding = 10;
			const int widgetMeasureNumberFudge = 10;
			var lMiscWidgetPos = xStart
			                     - widgetStartPadding
			                     + EditorMarkerEvent.GetNumberRelativeAnchorPos(sizeZoom)
			                     - EditorMarkerEvent.GetNumberAlpha(sizeZoom) * widgetMeasureNumberFudge;
			var rMiscWidgetPos = FocalPoint.X
			                     + (numArrows * arrowSize * 0.5) + widgetStartPadding;
			MiscEventWidgetLayoutManager.BeginFrame(lMiscWidgetPos, rMiscWidgetPos);

			// TODO: Common(?) code for determining song time by chart position or vice versa based on scroll mode

			switch (pScroll.SpacingMode)
			{
				case SpacingMode.ConstantTime:
				{
					WaveFormPPS = pScroll.TimeBasedPixelsPerSecond;

					var pps = pScroll.TimeBasedPixelsPerSecond * spacingZoom;
					var time = Position.ChartTime;
					var timeAtTopOfScreen = time - (FocalPoint.Y / pps);
					double chartPosition = 0.0;
					if (!ActiveChart.TryGetChartPositionFromTime(timeAtTopOfScreen, ref chartPosition))
						return;

					// Beat markers.
					if (sizeZoom >= MeasureMarkerMinScale)
					{
						// Find the first rate altering event to use.
						var rateEnumerator = FindBest(ActiveChart.RateAlteringEventsByRow, (int)chartPosition);
						if (rateEnumerator == null)
							return;
						rateEnumerator.MoveNext();

						// Record the current and next rate altering event.
						var currentRateEvent = rateEnumerator.Current;
						EditorRateAlteringEvent nextRateEvent = null;
						if (rateEnumerator.MoveNext())
							nextRateEvent = rateEnumerator.Current;
						var currentRow = (int)chartPosition;
						var lastRecordedRow = -1;

						// Update beat markers for every rate section until we have hit the bottom of the screen.
						while (!UpdateBeatMarkers(
							       currentRateEvent,
							       ref currentRow,
							       ref lastRecordedRow,
							       nextRateEvent,
							       xStart,
							       sizeZoom,
							       pps,
							       timeAtTopOfScreen))
						{
							currentRateEvent = rateEnumerator.Current;
							nextRateEvent = null;
							if (rateEnumerator.MoveNext())
								nextRateEvent = rateEnumerator.Current;
						}
					}

					var enumerator = FindBest(ActiveChart.EditorEvents, (int)chartPosition);
					if (enumerator == null)
						return;

					// Scan backwards until we have checked every lane for a long note which may
					// be extending through the given start row.
					var holdStartNotes = ScanBackwardsForHolds(enumerator, chartPosition);
					foreach (var hsn in holdStartNotes)
					{
						var startY = (ToSeconds(hsn.GetEvent().TimeMicros) - timeAtTopOfScreen) * pps;

						hsn.SetDimensions(
							xStart + hsn.GetLane() * arrowSize,
							startY - (arrowSize * 0.5),
							arrowSize,
							arrowSize,
							sizeZoom);
						noteEvents.Add(hsn);

						var end = hsn.GetHoldEndNote();
						end.SetDimensions(
							xStart + end.GetLane() * arrowSize,
							(ToSeconds(hsn.GetEvent().TimeMicros) - timeAtTopOfScreen) * pps,
							arrowSize,
							((ToSeconds(end.GetEvent().TimeMicros) - timeAtTopOfScreen) * pps) - startY + holdCapHeight,
							sizeZoom);
						holdBodyEvents.Add(end);
					}

					// Scan forward and add notes.
					while (enumerator.MoveNext())
					{
						var e = enumerator.Current;
						var y = (ToSeconds(e.GetEvent().TimeMicros) - timeAtTopOfScreen) * pps;
						var arrowY = y - (arrowSize * 0.5);

						if (arrowY > screenHeight)
							break;

						if (e is EditorHoldEndNoteEvent)
							continue;

						if (e is EditorTapNoteEvent
						    || e is EditorMineNoteEvent
						    || e is EditorHoldStartNoteEvent)
						{
							e.SetDimensions(xStart + e.GetLane() * arrowSize, arrowY, arrowSize, arrowSize, sizeZoom);

							if (e is EditorHoldStartNoteEvent hsn)
							{
								var endY = (ToSeconds(e.GetEvent().TimeMicros) - timeAtTopOfScreen) * pps;
								var end = hsn.GetHoldEndNote();
								holdBodyEvents.Add(end);
								end.SetDimensions(
									xStart + end.GetLane() * arrowSize,
									endY,
									arrowSize,
									((ToSeconds(end.GetEvent().TimeMicros) - timeAtTopOfScreen) * pps) - endY + holdCapHeight,
									sizeZoom);
							}
						}
						else
						{
							e.SetAlpha(miscEventAlpha);
							MiscEventWidgetLayoutManager.PositionEvent(e, y);
						}

						noteEvents.Add(e);

						if (noteEvents.Count + holdBodyEvents.Count > MaxEventsToDraw)
							break;
					}

					break;
				}

				case SpacingMode.ConstantRow:
				{
					var chartPosition = Position.ChartPosition;
					var ppr = pScroll.RowBasedPixelsPerRow * spacingZoom;
					var chartPositionAtTopOfScreen = chartPosition - (FocalPoint.Y / ppr);

					// Update WaveForm scroll scroll rate
					{
						var rateEnumerator = FindBest(ActiveChart.RateAlteringEventsByRow, (int)chartPosition);
						if (rateEnumerator != null)
						{
							rateEnumerator.MoveNext();
							var pps = pScroll.RowBasedPixelsPerRow * rateEnumerator.Current.RowsPerSecond;
							WaveFormPPS = pps;
							if (pScroll.RowBasedWaveFormScrollMode == WaveFormScrollMode.MostCommonTempo)
							{
								WaveFormPPS *= (ActiveChart.MostCommonTempo / rateEnumerator.Current.Tempo);
							}
						}
					}

					// Beat markers.
					if (sizeZoom >= MeasureMarkerMinScale)
					{
						// Find the first rate altering event to use.
						var rateEnumerator = FindBest(ActiveChart.RateAlteringEventsByRow, (int)chartPositionAtTopOfScreen);
						if (rateEnumerator == null)
							return;
						rateEnumerator.MoveNext();

						// Record the current and next rate altering event.
						var currentRateEvent = rateEnumerator.Current;
						EditorRateAlteringEvent nextRateEvent = null;
						if (rateEnumerator.MoveNext())
							nextRateEvent = rateEnumerator.Current;
						var currentRow = (int)chartPositionAtTopOfScreen;
						var lastRecordedRow = -1;

						// Update beat markers for every rate section until we have hit the bottom of the screen.
						while (!UpdateBeatMarkers(
							       currentRateEvent,
							       ref currentRow,
							       ref lastRecordedRow,
							       nextRateEvent,
							       xStart,
							       sizeZoom,
							       ppr,
							       chartPositionAtTopOfScreen * ppr))
						{
							currentRateEvent = rateEnumerator.Current;
							nextRateEvent = null;
							if (rateEnumerator.MoveNext())
								nextRateEvent = rateEnumerator.Current;
						}
					}

					var enumerator = FindBest(ActiveChart.EditorEvents, (int)chartPositionAtTopOfScreen);
					if (enumerator == null)
						return;

					// Scan backwards until we have checked every lane for a long note which may
					// be extending through the given start row.
					var holdStartNotes = ScanBackwardsForHolds(enumerator, chartPositionAtTopOfScreen);
					foreach (var hsn in holdStartNotes)
					{
						hsn.SetDimensions(
							xStart + hsn.GetLane() * arrowSize,
							(hsn.GetRow() - chartPositionAtTopOfScreen) * ppr - (arrowSize * 0.5),
							arrowSize,
							arrowSize,
							sizeZoom);

						var end = hsn.GetHoldEndNote();
						var endY = (hsn.GetRow() - chartPositionAtTopOfScreen) * ppr;
						end.SetDimensions(
							xStart + end.GetLane() * arrowSize,
							endY,
							arrowSize,
							((end.GetRow() - chartPositionAtTopOfScreen) * ppr) - endY + holdCapHeight,
							sizeZoom);
						holdBodyEvents.Add(end);
					}

					// Scan forward and add notes.
					while (enumerator.MoveNext())
					{
						var e = enumerator.Current;
						var y = (e.GetRow() - chartPositionAtTopOfScreen) * ppr;
						var arrowY = y - (arrowSize * 0.5);
						if (arrowY > screenHeight)
							break;

						if (e is EditorHoldEndNoteEvent)
							continue;

						if (e is EditorTapNoteEvent
						    || e is EditorMineNoteEvent
						    || e is EditorHoldStartNoteEvent)
						{
							e.SetDimensions(xStart + e.GetLane() * arrowSize, arrowY, arrowSize, arrowSize, sizeZoom);

							if (e is EditorHoldStartNoteEvent hsn)
							{
								var end = hsn.GetHoldEndNote();
								var endY = (e.GetRow() - chartPositionAtTopOfScreen) * ppr;
								end.SetDimensions(
									xStart + end.GetLane() * arrowSize,
									endY,
									arrowSize,
									((end.GetRow() - chartPositionAtTopOfScreen) * ppr) - endY + holdCapHeight,
									sizeZoom);
								holdBodyEvents.Add(end);
							}
						}
						else
						{
							e.SetAlpha(miscEventAlpha);
							MiscEventWidgetLayoutManager.PositionEvent(e, y);
						}

						noteEvents.Add(e);

						if (noteEvents.Count + holdBodyEvents.Count > MaxEventsToDraw)
							break;
					}

					break;
				}

				case SpacingMode.Variable:
				{
					// TODO: Fix Negative Scrolls resulting in cutting off notes prematurely.
					// If a chart has negative scrolls then we technically need to render notes which come before
					// the chart position at the top of the screen.
					// More likely the most visible problem will be at the bottom of the screen where if we
					// were to detect the first note which falls below the bottom it would prevent us from
					// finding the next set of notes which might need to be rendered because they appear 
					// above.

					var time = Position.ChartTime;
					double chartPosition = 0.0;
					if (!ActiveChart.TryGetChartPositionFromTime(time, ref chartPosition))
						return;
					var ratePosEventForChecking = new EditorInterpolatedRateAlteringEvent(ActiveChart, null)
					{
						Row = chartPosition,
						SongTime = time
					};

					// Find the interpolated scroll rate to use as a multiplier.
					// The interpolated scroll rate to use is the value at the current exact time.
					var interpolatedScrollRate = 1.0;
					var interpolatedScrollRateEnumerator =
						ActiveChart.InterpolatedScrollRateEvents.FindGreatestPreceding(ratePosEventForChecking);
					if (interpolatedScrollRateEnumerator != null)
					{
						interpolatedScrollRateEnumerator.MoveNext();
						var interpolatedRateEvent = interpolatedScrollRateEnumerator.Current;
						if (interpolatedRateEvent.InterpolatesByTime())
							interpolatedScrollRate = interpolatedRateEvent.GetInterpolatedScrollRateFromTime(time);
						else
							interpolatedScrollRate = interpolatedRateEvent.GetInterpolatedScrollRateFromRow(chartPosition);
					}

					// Now, scroll up to the top of the screen so we can start processing events going downwards.
					// We know what time / pos we are drawing at the receptors, but not the rate to get to that time from the top
					// of the screen.
					// We need to find the greatest preceding rate event, and continue until it is beyond the start of the screen.
					// Then we need to find the greatest preceding notes by scanning upwards.
					// Once we find that note, we start iterating downwards while also keeping track of the rate events along the way.

					var rateEnumerator = FindBest(ActiveChart.RateAlteringEventsByRow, (int)chartPosition);
					if (rateEnumerator == null)
						return;

					// Scan upwards to find the earliest rate altering event that should be used to start rendering.
					var previousRateEventPixelPosition = (double)FocalPoint.Y;
					var previousRateEventRow = chartPosition;
					var pps = 1.0;
					var ppr = 1.0;
					EditorRateAlteringEvent rateEvent = null;
					while (previousRateEventPixelPosition > 0.0 && rateEnumerator.MovePrev())
					{
						// On the rate altering event which is active for the current chart position,
						// Record the pixels per second to use for the WaveForm.
						if (rateEvent == null)
						{
							var tempo = ActiveChart.MostCommonTempo;
							if (pScroll.RowBasedWaveFormScrollMode != WaveFormScrollMode.MostCommonTempo)
								tempo = rateEnumerator.Current.Tempo;
							var useRate = pScroll.RowBasedWaveFormScrollMode ==
							              WaveFormScrollMode.CurrentTempoAndRate;
							WaveFormPPS = pScroll.VariablePixelsPerSecondAtDefaultBPM
							              * (tempo / PreferencesScroll.DefaultVariablePixelsPerSecondAtDefaultBPM);
							if (useRate)
							{
								var rate = rateEnumerator.Current.ScrollRate * interpolatedScrollRate;
								if (rate <= 0.0)
									rate = 1.0;
								WaveFormPPS *= rate;
							}
						}

						rateEvent = rateEnumerator.Current;
						var scrollRateForThisSection = rateEvent.ScrollRate * interpolatedScrollRate;
						pps = pScroll.VariablePixelsPerSecondAtDefaultBPM
						      * (rateEvent.Tempo / PreferencesScroll.DefaultVariablePixelsPerSecondAtDefaultBPM)
						      * scrollRateForThisSection
						      * spacingZoom;
						ppr = pps * rateEvent.SecondsPerRow;
						var rateEventPositionInPixels =
							previousRateEventPixelPosition - ((previousRateEventRow - rateEvent.Row) * ppr);
						previousRateEventPixelPosition = rateEventPositionInPixels;
						previousRateEventRow = rateEvent.Row;
					}

					// Now we know the position of first rate altering event to use.
					// We can now determine the chart position at the top of the screen
					var pixelPositionAtTopOfScreen = 0.0;
					var chartPositionAtTopOfScreen =
						rateEvent.Row + (pixelPositionAtTopOfScreen - previousRateEventPixelPosition) *
						rateEvent.RowsPerSecond / pps;

					var beatMarkerRow = (int)chartPositionAtTopOfScreen;
					var beatMarkerLastRecordedRow = -1;

					// Now that we know the position at the start of the screen we can find the first event to start rendering.
					var enumerator = FindBest(ActiveChart.EditorEvents, (int)chartPositionAtTopOfScreen);
					if (enumerator == null)
						return;

					// Scan backwards until we have checked every lane for a long note which may
					// be extending through the given start row. We cannot add the end events yet because
					// we do not know at what position they will end until we scan down.
					var holdEndNotesNeedingToBeAdded = new EditorHoldEndNoteEvent[ActiveChart.NumInputs];
					var holdStartNotes = ScanBackwardsForHolds(enumerator, chartPositionAtTopOfScreen);
					foreach (var hsn in holdStartNotes)
					{
						hsn.SetDimensions(
							xStart + hsn.GetLane() * arrowSize,
							(hsn.GetEvent().IntegerPosition - chartPositionAtTopOfScreen) * ppr - (arrowSize * 0.5),
							arrowSize,
							arrowSize,
							sizeZoom);
						noteEvents.Add(hsn);

						holdEndNotesNeedingToBeAdded[hsn.GetLane()] = hsn.GetHoldEndNote();
					}

					var hasNextRateEvent = rateEnumerator.MoveNext();
					EditorRateAlteringEvent nextRateEvent = hasNextRateEvent ? rateEnumerator.Current : null;

					// Now we can scan forward
					while (enumerator.MoveNext())
					{
						var e = enumerator.Current;

						// Check to see if we have crossed into a new rate altering event section
						if (nextRateEvent != null && e.GetEvent() == nextRateEvent.GetEvent())
						{
							// Add a misc widget for this rate event.
							var rateEventY = previousRateEventPixelPosition + (e.GetEvent().IntegerPosition - rateEvent.Row) * ppr;
							nextRateEvent.SetAlpha(miscEventAlpha);
							MiscEventWidgetLayoutManager.PositionEvent(nextRateEvent, rateEventY);
							noteEvents.Add(nextRateEvent);

							// Update beat markers for the section for the previous rate event.
							UpdateBeatMarkers(
								rateEvent,
								ref beatMarkerRow,
								ref beatMarkerLastRecordedRow,
								nextRateEvent,
								xStart,
								sizeZoom,
								ppr,
								previousRateEventPixelPosition);

							// Update rate parameters.
							rateEvent = nextRateEvent;
							var previousPPR = ppr;
							var scrollRateForThisSection = rateEvent.ScrollRate * interpolatedScrollRate;
							pps = pScroll.VariablePixelsPerSecondAtDefaultBPM
							      * (rateEvent.Tempo / PreferencesScroll.DefaultVariablePixelsPerSecondAtDefaultBPM)
							      * scrollRateForThisSection
							      * spacingZoom;
							ppr = pps * rateEvent.SecondsPerRow;
							var rateEventPositionInPixels = previousRateEventPixelPosition +
							                                ((rateEvent.Row - previousRateEventRow) * previousPPR);
							previousRateEventPixelPosition = rateEventPositionInPixels;
							previousRateEventRow = rateEvent.Row;

							// Advance next rate altering event.
							hasNextRateEvent = rateEnumerator.MoveNext();
							nextRateEvent = hasNextRateEvent ? rateEnumerator.Current : null;
							continue;
						}

						// Determine y position.
						var y = previousRateEventPixelPosition + (e.GetEvent().IntegerPosition - rateEvent.Row) * ppr;
						var arrowY = y - (arrowSize * 0.5);
						if (arrowY > screenHeight)
							break;

						// Record note.
						if (e is EditorTapNoteEvent
						    || e is EditorHoldStartNoteEvent
						    || e is EditorHoldEndNoteEvent
						    || e is EditorMineNoteEvent)
						{
							var noteY = arrowY;
							var noteH = arrowSize;

							if (e is EditorHoldEndNoteEvent hen)
							{
								var start = hen.GetHoldStartNote();
								var endY = previousRateEventPixelPosition
								           + (e.GetEvent().IntegerPosition - rateEvent.Row) * ppr
								           + holdCapHeight;

								noteY = start.GetY() + arrowSize * 0.5f;
								noteH = endY - (start.GetY() + (arrowSize * 0.5f));

								holdBodyEvents.Add(e);

								// Remove from holdEndNotesNeedingToBeAdded.
								holdEndNotesNeedingToBeAdded[e.GetLane()] = null;
							}
							else if (e is EditorHoldStartNoteEvent hsn)
							{
								// Record that there is in an in-progress hold that will need to be ended.
								holdEndNotesNeedingToBeAdded[e.GetLane()] = hsn.GetHoldEndNote();
								noteEvents.Add(e);
							}
							else
							{
								noteEvents.Add(e);
							}

							e.SetDimensions(xStart + e.GetLane() * arrowSize, noteY, arrowSize, noteH, sizeZoom);
						}
						else
						{
							e.SetAlpha(miscEventAlpha);
							MiscEventWidgetLayoutManager.PositionEvent(e, y);
							noteEvents.Add(e);
						}

						if (noteEvents.Count + holdBodyEvents.Count > MaxEventsToDraw)
							break;
					}

					// Now we need to wrap up any holds which started before the top of the screen and still are not yet complete.
					// We do not need to scan forward for more rate events.
					foreach (var holdEndNote in holdEndNotesNeedingToBeAdded)
					{
						if (holdEndNote == null)
							continue;

						var start = holdEndNote.GetHoldStartNote();
						var endY = previousRateEventPixelPosition
						           + (holdEndNote.GetEvent().IntegerPosition - rateEvent.Row) * ppr
						           + holdCapHeight;
						var noteH = endY - (start.GetY() + (arrowSize * 0.5f));

						holdEndNote.SetDimensions(
							xStart + holdEndNote.GetLane() * arrowSize,
							start.GetY() + arrowSize * 0.5,
							arrowSize,
							noteH,
							sizeZoom);

						holdBodyEvents.Add(holdEndNote);
					}

					// Also, wrap up any beats.
					UpdateBeatMarkers(
						rateEvent,
						ref beatMarkerRow,
						ref beatMarkerLastRecordedRow,
						nextRateEvent,
						xStart,
						sizeZoom,
						ppr,
						previousRateEventPixelPosition);

					break;
				}
			}

			VisibleEvents.AddRange(holdBodyEvents);
			VisibleEvents.AddRange(noteEvents);
		}

		/// <summary>
		/// Helper method to update beat marker events.
		/// Adds new MarkerEvents to VisibleMarkers.
		/// Almost all of the logic for updating beat marker placement is independent of SpacingMode.
		/// Expected to be called in a loop over EditorRateAlteringEvents which encompass the visible area.
		/// </summary>
		/// <param name="currentRateEvent">
		/// The current EditorRateAlteringEvent.
		/// MarkerEvents will be filled for the region in this event up until the given
		/// nextRateEvent, or end of the visible area defined by the viewport's height.
		/// </param>
		/// <param name="currentRow">
		/// The current row to start with. This row may not be on a beat boundary. If it is not on a beat
		/// boundary then MarkerEvents will be added starting with the following beat.
		/// This parameter is passed by reference so the beat marker logic can maintain state about where
		/// it left off.
		/// </param>
		/// <param name="lastRecordedRow">
		/// The last row that this method recorded a beat for.
		/// This parameter is passed by reference so the beat marker logic can maintain state about where
		/// it left off.
		/// </param>
		/// <param name="nextRateEvent">
		/// The EditorRateAlteringEvent following currentRateEvent or null if no such event follows it.
		/// </param>
		/// <param name="x">X position in pixels to set on the MarkerEvents.</param>
		/// <param name="sizeZoom">Current zoom level to use for setting the width and scale of the MarkerEvents.</param>
		/// <param name="spacingRate">
		///	For ConstantTime this rate is pixels per second.
		/// For ConstantRow this rate is is pixels per row.
		/// For Variable this rate is is pixels per row.
		/// </param>
		/// <param name="spacingBaseValue">
		///	For ConstantTime this value is the time at the top of the screen.
		/// For ConstantRow this value is the pixel position of row at the top of the screen.
		/// For Variable this value is the pixel position of the previous rate altering event.
		/// </param>
		private bool UpdateBeatMarkers(
			EditorRateAlteringEvent currentRateEvent,
			ref int currentRow,
			ref int lastRecordedRow,
			EditorRateAlteringEvent nextRateEvent,
			double x,
			double sizeZoom,
			double spacingRate,
			double spacingBaseValue)
		{
			if (sizeZoom < MeasureMarkerMinScale)
				return true;
			if (VisibleMarkers.Count >= MaxMarkersToDraw)
				return true;

			var pScroll = Preferences.Instance.PreferencesScroll;

			// Based on the current rate altering event, determine the beat spacing and snap the current row to a beat.
			var beatsPerMeasure = currentRateEvent.LastTimeSignature.Signature.Numerator;
			var rowsPerBeat = (SMCommon.MaxValidDenominator * SMCommon.NumBeatsPerMeasure * beatsPerMeasure)
			                  / currentRateEvent.LastTimeSignature.Signature.Denominator / beatsPerMeasure;

			// Determine which integer measure and beat we are on. Clamped due to warps.
			var rowRelativeToTimeSignatureStart = Math.Max(0,
				currentRow - currentRateEvent.LastTimeSignature.IntegerPosition);
			// We need to snap the row forward since we are starting with a row that might not be on a beat boundary.
			var beatRelativeToTimeSignatureStart = rowRelativeToTimeSignatureStart / rowsPerBeat;
			currentRow = currentRateEvent.LastTimeSignature.IntegerPosition +
			             beatRelativeToTimeSignatureStart * rowsPerBeat;

			var markerWidth = ActiveChart.NumInputs * DefaultArrowWidth * sizeZoom;

			while (true)
			{
				// When changing time signatures we don't want to render the same row twice.
				if (currentRow == lastRecordedRow)
				{
					currentRow += rowsPerBeat;
					continue;
				}

				// Determine the y position of this marker.
				// Clamp due to warps.
				var rowRelativeToLastRateChangeEvent = Math.Max(0, currentRow - currentRateEvent.RowForFollowingEvents);
				rowRelativeToLastRateChangeEvent = Math.Max(0, rowRelativeToLastRateChangeEvent);
				var y = 0.0;
				switch (pScroll.SpacingMode)
				{
					case SpacingMode.ConstantTime:
					{
						// Y is the time of the row times the pixels per second, shifted by the time at the top of the screen.
						var beatTime = currentRateEvent.SongTimeForFollowingEvents +
						               rowRelativeToLastRateChangeEvent * currentRateEvent.SecondsPerRow;
						y = (beatTime - spacingBaseValue) * spacingRate;
						break;
					}

					case SpacingMode.ConstantRow:
					{
						// Y is the current row times the pixels per row minus the pixel position at the top of the screen
						y = currentRow * spacingRate - spacingBaseValue;
						break;
					}

					case SpacingMode.Variable:
					{
						// Y is the previous rate event's pixel position plus the relative row times the pixels per row.
						y = spacingBaseValue + rowRelativeToLastRateChangeEvent * spacingRate;
						break;
					}
				}

				// If advancing this beat forward moved us over the next rate altering event boundary, loop again.
				if (nextRateEvent != null && currentRow > nextRateEvent.Row)
				{
					currentRow = (int)nextRateEvent.Row;
					return false;
				}

				// If advancing moved beyond the end of the screen then we are done.
				if (y > Graphics.GraphicsDevice.Viewport.Height)
				{
					return true;
				}

				// Determine if this marker is a measure marker instead of a beat marker.
				rowRelativeToTimeSignatureStart =
					currentRow - currentRateEvent.LastTimeSignature.IntegerPosition;
				beatRelativeToTimeSignatureStart = rowRelativeToTimeSignatureStart / rowsPerBeat;
				var measureMarker = beatRelativeToTimeSignatureStart % beatsPerMeasure == 0;
				var measure = currentRateEvent.LastTimeSignature.MetricPosition.Measure +
				              (beatRelativeToTimeSignatureStart / beatsPerMeasure);

				// Record the marker.
				if (measureMarker || sizeZoom > BeatMarkerMinScale)
				{
					VisibleMarkers.Add(new EditorMarkerEvent(
						x,
						y,
						markerWidth,
						1,
						sizeZoom,
						measureMarker,
						measure));
				}

				lastRecordedRow = currentRow;

				if (VisibleMarkers.Count >= MaxMarkersToDraw)
					return true;

				// Advance one beat.
				currentRow += rowsPerBeat;
			}
		}

		#region Autoplay

		private void UpdateAutoPlay()
		{
			if (!Playing || ActiveChart == null || NextAutoPlayNotes == null || Receptors == null)
				return;

			var nextEvents = ActiveChart.GetNextInputNotes(Position.ChartPosition);

			for (var lane = 0; lane < ActiveChart.NumInputs; lane++)
			{
				// If the next has changed it means we have just passed over an event.
				if (NextAutoPlayNotes[lane] != nextEvents[lane])
				{
					// Since we have already passed a note, we should offset any animations so they begin
					// as if they started at the precise moment the event passed.
					var timeDelta = NextAutoPlayNotes[lane] == null ? 0.0 : 
						Position.ChartTime - NextAutoPlayNotes[lane].GetChartTime();

					// The new next event can be null at the end of the chart. We need to release any
					// held input in this case.
					if (nextEvents[lane] == null && Receptors[lane].IsAutoplayHeld())
					{
						Receptors[lane].OnAutoplayInputUp(timeDelta);
					}

					// Only process inputs if the current input is not null.
					// This helps ensure that when starting playing in the middle of a chart
					// we don't incorrectly show input immediately.
					if (NextAutoPlayNotes[lane] != null)
					{
						// If the event that just passed is a hold end, release input.
						if (NextAutoPlayNotes[lane].GetEvent() is LaneHoldEndNote)
						{
							Receptors[lane].OnAutoplayInputUp(timeDelta);
						}
						else
						{
							// For both taps an hold starts, press input.
							Receptors[lane].OnAutoplayInputDown(timeDelta);

							// For taps, release them immediately.
							if (NextAutoPlayNotes[lane].GetEvent() is LaneTapNote)
							{
								Receptors[lane].OnAutoplayInputUp(timeDelta);
							}
						}
					}

					// If the next event is a hold end (i.e. we are in a hold) and the currently
					// tracked note is null (i.e. we just started playback), then start input to
					// hold the note.
					else if (NextAutoPlayNotes[lane] == null
					         && nextEvents[lane] != null
					         && nextEvents[lane].GetEvent() is LaneHoldEndNote)
					{
						Receptors[lane].OnAutoplayInputDown(timeDelta);
					}
				}

				// Cache the next event for next time.
				NextAutoPlayNotes[lane] = nextEvents[lane];
			}
		}

		private void UpdateAutoPlayFromScrolling()
		{
			StopAutoPlay();
		}

		private void StopAutoPlay()
		{
			if (ActiveChart == null || NextAutoPlayNotes == null || Receptors == null)
				return;

			for (var lane = 0; lane < ActiveChart.NumInputs; lane++)
			{
				//if (NextAutoPlayNotes[lane] is EditorHoldEndNoteEvent hen)
				//	hen.Active = false;
				NextAutoPlayNotes[lane] = null;
				Receptors[lane].OnAutoplayInputCancel();
			}
		}

		#endregion Autoplay

		#region MiniMap

		private void ProcessInputForMiniMap(MouseState mouseState)
		{
			var pScroll = Preferences.Instance.PreferencesScroll;
			var pMiniMap = Preferences.Instance.PreferencesMiniMap;

			var mouseDownThisFrame = mouseState.LeftButton == ButtonState.Pressed &&
			                         PreviousMiniMapMouseState.LeftButton != ButtonState.Pressed;
			var mouseUpThisFrame = mouseState.LeftButton != ButtonState.Pressed &&
			                       PreviousMiniMapMouseState.LeftButton == ButtonState.Pressed;
			var miniMapCapturingMouseLastFrame = MiniMapCapturingMouse;

			var miniMapNeedsMouseThisFrame = false;
			if (mouseDownThisFrame)
			{
				miniMapNeedsMouseThisFrame = MiniMap.MouseDown(mouseState.X, mouseState.Y);
			}

			MiniMap.MouseMove(mouseState.X, mouseState.Y);
			if (mouseUpThisFrame)
			{
				MiniMap.MouseUp(mouseState.X, mouseState.Y);
			}

			MiniMapCapturingMouse = MiniMap.WantsMouse();

			// Set the Song Position based on the MiniMap position
			MiniMapCapturingMouse |= miniMapNeedsMouseThisFrame;
			if (MiniMapCapturingMouse)
			{
				// When moving the MiniMap, pause or stop playback.
				if (mouseDownThisFrame && Playing)
				{
					// Set a flag to unpause playback unless the preference is to completely stop when scrolling.
					StartPlayingWhenMiniMapDone = !pMiniMap.MiniMapStopPlaybackWhenScrolling;
					StopPlayback();
				}

				// Set the music position based off of the MiniMap editor area.
				var editorPosition = MiniMap.GetEditorPosition();
				switch (GetMiniMapSpacingMode())
				{
					case SpacingMode.ConstantTime:
					{
						Position.ChartTime =
							editorPosition + (FocalPoint.Y / (pScroll.TimeBasedPixelsPerSecond * Zoom));
						break;
					}
					case SpacingMode.ConstantRow:
					{
						Position.ChartPosition =
							editorPosition + (FocalPoint.Y / (pScroll.RowBasedPixelsPerRow * Zoom));
						break;
					}
				}
				UpdateAutoPlayFromScrolling();
			}

			// When letting go of the MiniMap, start playing again.
			if (miniMapCapturingMouseLastFrame && !MiniMapCapturingMouse && StartPlayingWhenMiniMapDone)
			{
				StartPlayingWhenMiniMapDone = false;
				StartPlayback();
			}

			PreviousMiniMapMouseState = mouseState;
		}

		private void UpdateMiniMapBounds()
		{
			var p = Preferences.Instance;
			var x = 0;
			var zoom = Math.Min(1.0, Zoom);
			switch (p.PreferencesMiniMap.MiniMapPosition)
			{
				case MiniMap.Position.RightSideOfWindow:
				{
					x = Graphics.PreferredBackBufferWidth - (int)p.PreferencesMiniMap.MiniMapXPadding - (int)p.PreferencesMiniMap.MiniMapWidth;
					break;
				}
				case MiniMap.Position.RightOfChartArea:
				{
					x = (int)(FocalPoint.X + (WaveFormTextureWidth >> 1) + (int)p.PreferencesMiniMap.MiniMapXPadding);
					break;
				}
				case MiniMap.Position.MountedToWaveForm:
				{
					if (p.PreferencesWaveForm.WaveFormScaleXWhenZooming)
					{
						x = (int)(FocalPoint.X + (WaveFormTextureWidth >> 1) * zoom + (int)p.PreferencesMiniMap.MiniMapXPadding);
					}
					else
					{
						x = (int)(FocalPoint.X + (WaveFormTextureWidth >> 1) + (int)p.PreferencesMiniMap.MiniMapXPadding);
					}

					break;
				}
				case MiniMap.Position.MountedToChart:
				{
					x = (int)(FocalPoint.X + ((ActiveChart.NumInputs * DefaultArrowWidth) >> 1) * zoom + (int)p.PreferencesMiniMap.MiniMapXPadding);
					break;
				}
			}

			var h = Math.Max(0, Graphics.PreferredBackBufferHeight - MiniMapYPaddingFromTop - MiniMapYPaddingFromBottom);

			MiniMap.UpdateBounds(
				GraphicsDevice,
				new Rectangle(x, MiniMapYPaddingFromTop, (int)p.PreferencesMiniMap.MiniMapWidth, h));
		}

		private void UpdateMiniMapLaneSpacing()
		{
			MiniMap.SetLaneSpacing(
				Preferences.Instance.PreferencesMiniMap.MiniMapNoteWidth,
				Preferences.Instance.PreferencesMiniMap.MiniMapNoteSpacing);
		}

		private SpacingMode GetMiniMapSpacingMode()
		{
			if (Preferences.Instance.PreferencesScroll.SpacingMode == SpacingMode.Variable)
				return Preferences.Instance.PreferencesMiniMap.MiniMapSpacingModeForVariable;
			return Preferences.Instance.PreferencesScroll.SpacingMode;
		}

		private void UpdateMiniMap()
		{
			var pMiniMap = Preferences.Instance.PreferencesMiniMap;
			// Performance optimization. Do not update the MiniMap if we won't render it.
			if (!pMiniMap.ShowMiniMap)
				return;

			if (ActiveChart == null || ActiveChart.EditorEvents == null || ArrowGraphicManager == null)
				return;

			UpdateMiniMapBounds();
			UpdateMiniMapLaneSpacing();

			var pScroll = Preferences.Instance.PreferencesScroll;

			MiniMap.SetSelectMode(pMiniMap.MiniMapSelectMode);

			switch (GetMiniMapSpacingMode())
			{
				case SpacingMode.ConstantTime:
				{
					var screenHeight = Graphics.GraphicsDevice.Viewport.Height;
					var spacingZoom = Zoom;
					var pps = pScroll.TimeBasedPixelsPerSecond * spacingZoom;
					var time = Position.ChartTime;

					// Editor Area. The visible time range.
					var editorAreaTimeStart = time - (FocalPoint.Y / pps);
					var editorAreaTimeEnd = editorAreaTimeStart + (screenHeight / pps);
					var editorAreaTimeRange = editorAreaTimeEnd - editorAreaTimeStart;

					// Determine the end time.
					var maxTimeFromChart = ActiveChart.GetEndTime(false);

					// Full Area. The time from the chart, extended in both directions by the editor range.
					var fullAreaTimeStart = ActiveChart.GetStartTime(false) - editorAreaTimeRange;
					var fullAreaTimeEnd = maxTimeFromChart + editorAreaTimeRange;

					// Content Area. The time from the chart.
					var contentAreaTimeStart = ActiveChart.GetStartTime(false);
					var contentAreaTimeEnd = maxTimeFromChart;

					// Update the MiniMap with the ranges.
					MiniMap.SetNumLanes((uint)ActiveChart.NumInputs);
					MiniMap.UpdateBegin(
						fullAreaTimeStart, fullAreaTimeEnd,
						contentAreaTimeStart, contentAreaTimeEnd,
						pMiniMap.MiniMapVisibleTimeRange,
						editorAreaTimeStart, editorAreaTimeEnd,
						ArrowGraphicManager);

					// Add notes
					double chartPosition = 0.0;
					if (!ActiveChart.TryGetChartPositionFromTime(MiniMap.GetMiniMapAreaStart(), ref chartPosition))
						break;
					var enumerator = FindBest(ActiveChart.EditorEvents, (int)chartPosition);
					if (enumerator == null)
						break;

					var numNotesAdded = 0;

					// Scan backwards until we have checked every lane for a long note which may
					// be extending through the given start row.
					var holdStartNotes = ScanBackwardsForHolds(enumerator, chartPosition);
					foreach (var hsn in holdStartNotes)
					{
						MiniMap.AddHold(
							(LaneHoldStartNote)hsn.GetEvent(),
							ToSeconds(hsn.GetEvent().TimeMicros),
							ToSeconds(hsn.GetHoldEndNote().GetEvent().TimeMicros),
							hsn.IsRoll());
						numNotesAdded++;
					}

					while (enumerator.MoveNext())
					{
						var e = enumerator.Current;
						if (e is EditorHoldEndNoteEvent)
							continue;

						if (e is EditorTapNoteEvent)
						{
							numNotesAdded++;
							if (MiniMap.AddNote((LaneNote)e.GetEvent(), ToSeconds(e.GetEvent().TimeMicros)) ==
							    MiniMap.AddResult.BelowBottom)
								break;
						}
						else if (e is EditorMineNoteEvent)
						{
							numNotesAdded++;
							if (MiniMap.AddMine((LaneNote)e.GetEvent(), ToSeconds(e.GetEvent().TimeMicros)) ==
							    MiniMap.AddResult.BelowBottom)
								break;
						}
						else if (e is EditorHoldStartNoteEvent hsn)
						{
							numNotesAdded++;
							if (MiniMap.AddHold(
								    (LaneHoldStartNote)e.GetEvent(),
								    ToSeconds(e.GetEvent().TimeMicros),
								    ToSeconds(hsn.GetHoldEndNote().GetEvent().TimeMicros),
								    hsn.IsRoll()) == MiniMap.AddResult.BelowBottom)
								break;
						}

						if (numNotesAdded > MiniMapMaxNotesToDraw)
							break;
					}

					break;
				}

				case SpacingMode.ConstantRow:
				{
					var screenHeight = Graphics.GraphicsDevice.Viewport.Height;

					var chartPosition = Position.ChartPosition;
					var spacingZoom = Zoom;
					var ppr = pScroll.RowBasedPixelsPerRow * spacingZoom;

					// Editor Area. The visible row range.
					var editorAreaRowStart = chartPosition - (FocalPoint.Y / ppr);
					var editorAreaRowEnd = editorAreaRowStart + (screenHeight / ppr);
					var editorAreaRowRange = editorAreaRowEnd - editorAreaRowStart;

					// Determine the end row.
					var lastEvent = ActiveChart.EditorEvents.Last();
					var maxRowFromChart = 0.0;
					if (lastEvent.MoveNext())
						maxRowFromChart = lastEvent.Current.GetEvent().IntegerPosition;

					if (EditorSong.LastSecondHint > 0.0)
					{
						var lastSecondChartPosition = 0.0;
						if (ActiveChart.TryGetChartPositionFromTime(EditorSong.LastSecondHint, ref lastSecondChartPosition))
						{
							maxRowFromChart = Math.Max(lastSecondChartPosition, maxRowFromChart);
						}
					}

					// Full Area. The area from the chart, extended in both directions by the editor range.
					var fullAreaRowStart = 0.0 - editorAreaRowRange;
					var fullAreaRowEnd = maxRowFromChart + editorAreaRowRange;

					// Content Area. The rows from the chart.
					var contentAreaTimeStart = 0.0;
					var contentAreaTimeEnd = maxRowFromChart;

					// Update the MiniMap with the ranges.
					MiniMap.SetNumLanes((uint)ActiveChart.NumInputs);
					MiniMap.UpdateBegin(
						fullAreaRowStart, fullAreaRowEnd,
						contentAreaTimeStart, contentAreaTimeEnd,
						pMiniMap.MiniMapVisibleRowRange,
						editorAreaRowStart, editorAreaRowEnd,
						ArrowGraphicManager);

					// Add notes
					chartPosition = MiniMap.GetMiniMapAreaStart();
					var enumerator = FindBest(ActiveChart.EditorEvents, (int)chartPosition);
					if (enumerator == null)
						break;

					var numNotesAdded = 0;

					// Scan backwards until we have checked every lane for a long note which may
					// be extending through the given start row.
					var holdStartNotes = ScanBackwardsForHolds(enumerator, chartPosition);
					foreach (var hsn in holdStartNotes)
					{
						MiniMap.AddHold(
							(LaneHoldStartNote)hsn.GetEvent(),
							hsn.GetEvent().IntegerPosition,
							hsn.GetHoldEndNote().GetEvent().IntegerPosition,
							hsn.IsRoll());
						numNotesAdded++;
					}

					while (enumerator.MoveNext())
					{
						var e = enumerator.Current;
						if (e is EditorHoldEndNoteEvent)
							continue;

						if (e is EditorTapNoteEvent)
						{
							numNotesAdded++;
							if (MiniMap.AddNote((LaneNote)e.GetEvent(), e.GetEvent().IntegerPosition) ==
							    MiniMap.AddResult.BelowBottom)
								break;
						}
						else if (e is EditorMineNoteEvent)
						{
							numNotesAdded++;
							if (MiniMap.AddMine((LaneNote)e.GetEvent(), e.GetEvent().IntegerPosition) ==
							    MiniMap.AddResult.BelowBottom)
								break;
						}
						else if (e is EditorHoldStartNoteEvent hsn)
						{
							numNotesAdded++;
							if (MiniMap.AddHold(
								    (LaneHoldStartNote)e.GetEvent(),
								    e.GetEvent().IntegerPosition,
								    hsn.GetHoldEndNote().GetEvent().IntegerPosition,
								    hsn.IsRoll()) == MiniMap.AddResult.BelowBottom)
								break;
						}

						if (numNotesAdded > MiniMapMaxNotesToDraw)
							break;
					}


					break;
				}
			}

			MiniMap.UpdateEnd();
		}

		private void DrawMiniMap()
		{
			if (!Preferences.Instance.PreferencesMiniMap.ShowMiniMap)
				return;
			MiniMap.Draw(SpriteBatch);
		}

		#endregion MiniMap

		/// <summary>
		/// Given a chart position, scans backwards for hold notes which begin earlier and end later.
		/// </summary>
		/// <param name="enumerator">Enumerator to use for scanning backwards.</param>
		/// <param name="chartPosition">Chart position to use for checking.</param>
		/// <returns>List of EditorHoldStartNotes.</returns>
		private List<EditorHoldStartNoteEvent> ScanBackwardsForHolds(RedBlackTree<EditorEvent>.Enumerator enumerator,
			double chartPosition)
		{
			var lanesChecked = new bool[ActiveChart.NumInputs];
			var numLanesChecked = 0;
			var holds = new List<EditorHoldStartNoteEvent>();
			var current = new RedBlackTree<EditorEvent>.Enumerator(enumerator);
			while (current.MovePrev() && numLanesChecked < ActiveChart.NumInputs)
			{
				var e = current.Current;
				var lane = e.GetLane();
				if (lane >= 0)
				{
					if (!lanesChecked[lane])
					{
						lanesChecked[lane] = true;
						numLanesChecked++;

						if (e.GetRow() + e.GetLength() > chartPosition)
						{
							if (e is EditorHoldStartNoteEvent hsn)
							{
								holds.Add(hsn);
							}
						}
					}
				}
			}

			foreach (var editState in LaneEditStates)
			{
				if (!editState.IsActive())
					continue;
				if (!(editState.GetEventBeingEdited() is EditorHoldStartNoteEvent hsn))
					continue;
				if (hsn.GetRow() < chartPosition && hsn.GetRow() + hsn.GetLength() > chartPosition)
					holds.Add(hsn);
			}

			return holds;
		}

		private void DrawChartEvents()
		{
			var eventsBeingEdited = new List<EditorEvent>();
			// Draw the measure and beat markers.
			foreach (var visibleMarker in VisibleMarkers)
			{
				visibleMarker.Draw(TextureAtlas, SpriteBatch, MonogameFont_MPlus1Code_Medium);
			}

			foreach (var visibleEvent in VisibleEvents)
			{
				// Capture events being edited to draw after all events not being edited.
				if (visibleEvent.IsBeingEdited())
				{
					eventsBeingEdited.Add(visibleEvent);
					continue;
				}

				if (Playing)
				{
					// Skip events entirely above the receptors.
					if (Preferences.Instance.PreferencesAnimations.AutoPlayHideArrows
						&& visibleEvent.GetChartTime() < Position.ChartTime
					    && (visibleEvent is EditorTapNoteEvent
					        || visibleEvent is EditorHoldStartNoteEvent 
					        || visibleEvent is EditorHoldEndNoteEvent))
						continue;

					// Cut off hold end notes which intersect the receptors.
					if (visibleEvent is EditorHoldEndNoteEvent hen)
					{
						if (Playing && hen.GetChartTime() > Position.ChartTime
						            && hen.GetHoldStartNote().GetChartTime() < Position.ChartTime)
						{
							hen.SetNextDrawActive(true, FocalPoint.Y);
						}
					}
				}
				
				// Draw the event.
				visibleEvent.Draw(TextureAtlas, SpriteBatch, ArrowGraphicManager);
			}

			// Draw events being edited.
			foreach (var visibleEvent in eventsBeingEdited)
			{
				visibleEvent.Draw(TextureAtlas, SpriteBatch, ArrowGraphicManager);
			}
		}


		#region Gui Rendering

		private void DrawGui(GameTime gameTime)
		{
			DrawMainMenuUI();

			DrawDebugUI();

			if (ShowImGuiTestWindow)
			{
				ImGui.SetNextWindowPos(new System.Numerics.Vector2(650, 20), ImGuiCond.FirstUseEver);
				ImGui.ShowDemoWindow(ref ShowImGuiTestWindow);
			}

			UILog.Draw(LogBuffer, LogBufferLock);
			UIScrollPreferences.Draw();
			UIWaveFormPreferences.Draw();
			UIMiniMapPreferences.Draw();
			UIAnimationsPreferences.Draw();
			UIOptions.Draw();

			UISongProperties.Draw(EditorSong);
			UIChartProperties.Draw(ActiveChart);
			UIChartList.Draw(ActiveChart);
			
			UIChartPosition.Draw(
				(int)FocalPoint.X,
				Graphics.PreferredBackBufferHeight - ChartPositionUIYPAddingFromBottom - (int)(UIChartPosition.Height * 0.5),
				Position,
				SnapLevels[SnapIndex],
				ArrowGraphicManager);
		}

		private void DrawMainMenuUI()
		{
			var p = Preferences.Instance;
			if (ImGui.BeginMainMenuBar())
			{
				if (ImGui.BeginMenu("File"))
				{
					if (ImGui.MenuItem("Open", "Ctrl+O"))
					{
						OpenSongFile();
					}

					if (ImGui.BeginMenu("Open Recent", p.RecentFiles.Count > 0))
					{
						foreach (var recentFile in p.RecentFiles)
						{
							var fileNameWithPath = recentFile.FileName;
							var fileName = System.IO.Path.GetFileName(fileNameWithPath);
							if (ImGui.MenuItem(fileName))
							{
								OpenSongFileAsync(fileNameWithPath,
									recentFile.LastChartType,
									recentFile.LastChartDifficultyType);
							}
						}

						ImGui.EndMenu();
					}

					if (ImGui.MenuItem("Reload", "Ctrl+R", false,
						    EditorSong != null && p.RecentFiles.Count > 0))
					{
						// TODO: Need to make sure this right, also need to hook up key command.
						OpenSongFileAsync(p.RecentFiles[0].FileName,
							p.RecentFiles[0].LastChartType,
							p.RecentFiles[0].LastChartDifficultyType);
					}

					var editorFileName = EditorSong?.FileName;
					if (!string.IsNullOrEmpty(editorFileName))
					{
						if (ImGui.MenuItem($"Export {editorFileName}", "Ctrl+E"))
						{
							OnExport();
						}
					}
					else
					{
						if (ImGui.MenuItem("Export", "Ctrl+E", false, EditorSong != null))
						{
							OnExport();
						}
					}
					if (ImGui.MenuItem("Export As...", "Ctrl+Shift+E", false, EditorSong != null))
					{
						OnExportAs();
					}

					if (ImGui.MenuItem("Exit", "Alt+F4"))
					{
						Exit();
					}

					ImGui.EndMenu();
				}

				if (ImGui.BeginMenu("View"))
				{
					if (ImGui.MenuItem("Options"))
						p.PreferencesOptions.ShowOptionsWindow = true;

					ImGui.Separator();
					if (ImGui.MenuItem("Song Properties"))
						p.ShowSongPropertiesWindow = true;
					if (ImGui.MenuItem("Chart Properties"))
						p.ShowChartPropertiesWindow = true;
					if (ImGui.MenuItem("Chart List"))
						p.ShowChartListWindow = true;

					ImGui.Separator();
					if (ImGui.MenuItem("Waveform Preferences"))
						p.PreferencesWaveForm.ShowWaveFormPreferencesWindow = true;
					if (ImGui.MenuItem("Scroll Preferences"))
						p.PreferencesScroll.ShowScrollControlPreferencesWindow = true;
					if (ImGui.MenuItem("Mini Map Preferences"))
						p.PreferencesMiniMap.ShowMiniMapPreferencesWindow = true;
					if (ImGui.MenuItem("Animation Preferences"))
						p.PreferencesAnimations.ShowAnimationsPreferencesWindow = true;

					ImGui.Separator();
					if (ImGui.MenuItem("Log"))
						p.ShowLogWindow = true;
					if (ImGui.MenuItem("ImGui Demo Window"))
						ShowImGuiTestWindow = true;
					ImGui.EndMenu();
				}

				ImGui.EndMainMenuBar();
			}
		}

		private void DrawDebugUI()
		{
			ImGui.Begin("Debug");
			{
				ImGui.Checkbox("Parallelize Update Loop", ref ParallelizeUpdateLoop);
				ImGui.Checkbox("Render Chart", ref RenderChart);
				ImGui.Text($"Update Time:       {UpdateTimeTotal:F6} seconds");
				ImGui.Text($"  Waveform:        {UpdateTimeWaveForm:F6} seconds");
				ImGui.Text($"  Mini Map:        {UpdateTimeMiniMap:F6} seconds");
				ImGui.Text($"  Chart Events:    {UpdateTimeChartEvents:F6} seconds");
				ImGui.Text($"Draw Time:         {DrawTimeTotal:F6} seconds");
				ImGui.Text($"Total Time:        {(UpdateTimeTotal + DrawTimeTotal):F6} seconds");
				ImGui.Text($"Total FPS:         {(1.0f / (UpdateTimeTotal + DrawTimeTotal)):F6}");
				ImGui.Text($"Actual Time:       {1.0f / ImGui.GetIO().Framerate:F6} seconds");
				ImGui.Text($"Actual FPS:        {ImGui.GetIO().Framerate:F6}");

				if (ImGui.Button("Reload Song"))
				{
					StopPreview();
					MusicManager.LoadMusicAsync(GetFullPathToMusicFile(), GetSongTime, true);
				}

				if (ImGui.Button("Save Time and Zoom"))
				{
					Preferences.Instance.DebugSongTime = Position.SongTime;
					Preferences.Instance.DebugZoom = Zoom;
				}

				if (ImGui.Button("Load Time and Zoom"))
				{
					Position.SongTime = Preferences.Instance.DebugSongTime;
					SetZoom(Preferences.Instance.DebugZoom, true);
				}
			}
			ImGui.End();
		}

		#endregion Gui Rendering

		#region Loading

		/// <summary>
		/// Initializes all PadData and creates corresponding StepGraphs for all ChartTypes
		/// specified in the StartupChartTypes.
		/// </summary>
		private async void InitPadDataAndStepGraphsAsync()
		{
			var pOptions = Preferences.Instance.PreferencesOptions;
			var tasks = new Task<bool>[pOptions.StartupChartTypes.Length];
			for (var i = 0; i < pOptions.StartupChartTypes.Length; i++)
			{
				tasks[i] = LoadPadDataAndCreateStepGraph(pOptions.StartupChartTypes[i]);
			}

			await Task.WhenAll(tasks);
		}

		/// <summary>
		/// Loads PadData and creates a StepGraph for the given StepMania StepsType.
		/// </summary>
		/// <returns>
		/// True if no errors were generated and false otherwise.
		/// </returns>
		private async Task<bool> LoadPadDataAndCreateStepGraph(SMCommon.ChartType chartType)
		{
			if (PadDataByChartType.ContainsKey(chartType))
				return true;

			PadDataByChartType[chartType] = null;
			StepGraphByChartType[chartType] = null;

			// Load the PadData.
			PadDataByChartType[chartType] = await LoadPadData(chartType);
			if (PadDataByChartType[chartType] == null)
			{
				PadDataByChartType.Remove(chartType);
				StepGraphByChartType.Remove(chartType);
				return false;
			}

			// Create the StepGraph.
			await Task.Run(() =>
			{
				Logger.Info($"Creating {chartType} StepGraph.");
				StepGraphByChartType[chartType] = StepGraph.CreateStepGraph(
					PadDataByChartType[chartType],
					PadDataByChartType[chartType].StartingPositions[0][0][L],
					PadDataByChartType[chartType].StartingPositions[0][0][R]);
				Logger.Info($"Finished creating {chartType} StepGraph.");
			});

			return true;
		}

		/// <summary>
		/// Loads PadData for the given ChartType.
		/// </summary>
		/// <param name="chartType">ChartType to load PadData for.</param>
		/// <returns>Loaded PadData or null if any errors were generated.</returns>
		private static async Task<PadData> LoadPadData(SMCommon.ChartType chartType)
		{
			var chartTypeString = SMCommon.ChartTypeString(chartType);
			var fileName = $"{chartTypeString}.json";
			Logger.Info($"Loading PadData from {fileName}.");
			var padData = await PadData.LoadPadData(chartTypeString, fileName);
			if (padData == null)
				return null;
			Logger.Info($"Finished loading {chartTypeString} PadData.");
			return padData;
		}

		/// <summary>
		/// Starts the process of opening a Song file by presenting a dialog to choose a Song.
		/// </summary>
		private void OpenSongFile()
		{
			var pOptions = Preferences.Instance.PreferencesOptions;
			using (OpenFileDialog openFileDialog = new OpenFileDialog())
			{
				openFileDialog.InitialDirectory = Preferences.Instance.OpenFileDialogInitialDirectory;
				openFileDialog.Filter = "StepMania Files (*.sm,*.ssc)|*.sm;*.ssc|All files (*.*)|*.*";
				openFileDialog.FilterIndex = 1;

				if (openFileDialog.ShowDialog() == DialogResult.OK)
				{
					var fileName = openFileDialog.FileName;
					Preferences.Instance.OpenFileDialogInitialDirectory = System.IO.Path.GetDirectoryName(fileName);
					OpenSongFileAsync(openFileDialog.FileName,
						pOptions.DefaultStepsType,
						pOptions.DefaultDifficultyType);
				}
			}
		}

		/// <summary>
		/// Starts the process of opening a selected Song.
		/// Will cancel previous OpenSongFileAsync requests if called while already loading.
		/// </summary>
		/// <param name="fileName">File name of the Song file to open.</param>
		/// <param name="chartType">Desired ChartType (StepMania StepsType) to default to once open.</param>
		/// <param name="chartDifficultyType">Desired DifficultyType to default to once open.</param>
		private async void OpenSongFileAsync(
			string fileName,
			SMCommon.ChartType chartType,
			SMCommon.ChartDifficultyType chartDifficultyType)
		{
			try
			{
				// Store the song file we want to load.
				PendingOpenFileName = fileName;
				PendingOpenFileChartType = chartType;
				PendingOpenFileChartDifficultyType = chartDifficultyType;

				// If we are already loading a song file, cancel that operation so
				// we can start the new load.
				if (LoadSongCancellationTokenSource != null)
				{
					// If we are already cancelling something then return. We don't want
					// calls to this method to collide. We will end up using the most recently
					// requested song file due to the PendingOpenFileName variable.
					if (LoadSongCancellationTokenSource.IsCancellationRequested)
						return;

					// Start the cancellation and wait for it to complete.
					LoadSongCancellationTokenSource?.Cancel();
					await LoadSongTask;
				}

				// Use the most recently requested song file.
				fileName = PendingOpenFileName;
				chartType = PendingOpenFileChartType;
				chartDifficultyType = PendingOpenFileChartDifficultyType;

				UnloadSongResources();

				// Start an asynchronous series of operations to load the song.
				EditorChart newActiveChart = null;
				LoadSongCancellationTokenSource = new CancellationTokenSource();
				LoadSongTask = Task.Run(async () =>
				{
					try
					{
						// Load the song file.
						var reader = Reader.CreateReader(fileName);
						if (reader == null)
						{
							Logger.Error($"Unsupported file format. Cannot parse {fileName}");
							return;
						}

						Logger.Info($"Loading {fileName}...");
						var song = await reader.LoadAsync(LoadSongCancellationTokenSource.Token);
						if (song == null)
						{
							Logger.Error($"Failed to load {fileName}");
							return;
						}

						Logger.Info($"Loaded {fileName}");

						LoadSongCancellationTokenSource.Token.ThrowIfCancellationRequested();
						EditorSong = new EditorSong(
							this,
							fileName,
							song,
							GraphicsDevice,
							ImGuiRenderer);

						// Select the best Chart to make active.
						newActiveChart = SelectBestChart(EditorSong, chartType, chartDifficultyType);
						LoadSongCancellationTokenSource.Token.ThrowIfCancellationRequested();
					}
					catch (OperationCanceledException)
					{
						// Upon cancellation null out the Song and ActiveChart.
						UnloadSongResources();
					}
					finally
					{
						LoadSongCancellationTokenSource?.Dispose();
						LoadSongCancellationTokenSource = null;
					}
				}, LoadSongCancellationTokenSource.Token);
				await LoadSongTask;
				if (EditorSong == null)
				{
					return;
				}

				// Insert a new entry at the top of the saved recent files.
				var p = Preferences.Instance;
				var pOptions = p.PreferencesOptions;
				var savedSongInfo = new Preferences.SavedSongInformation
				{
					FileName = fileName,
					LastChartType = ActiveChart?.ChartType ?? pOptions.DefaultStepsType,
					LastChartDifficultyType = ActiveChart?.ChartDifficultyType ?? pOptions.DefaultDifficultyType,
				};
				p.RecentFiles.RemoveAll(info => info.FileName == fileName);
				p.RecentFiles.Insert(0, savedSongInfo);
				if (p.RecentFiles.Count > pOptions.RecentFilesHistorySize)
				{
					p.RecentFiles.RemoveRange(
						pOptions.RecentFilesHistorySize,
						p.RecentFiles.Count - pOptions.RecentFilesHistorySize);
				}

				// Find a better spot for this
				Position.ChartPosition = 0.0;
				DesiredSongTime = Position.SongTime;

				OnChartSelected(newActiveChart, false);

				SetZoom(1.0, true);
			}
			catch (Exception e)
			{
				Logger.Error($"Failed to load {fileName}. {e}");
			}
		}

		/// <summary>
		/// Helper method when loading a Song to select the best Chart to be the active Chart.
		/// </summary>
		/// <param name="song">Song.</param>
		/// <param name="preferredChartType">The preferred ChartType (StepMania StepsType) to use.</param>
		/// <param name="preferredChartDifficultyType">The preferred DifficultyType to use.</param>
		/// <returns>Best Chart to use or null if no Charts exist.</returns>
		private EditorChart SelectBestChart(EditorSong song, SMCommon.ChartType preferredChartType, SMCommon.ChartDifficultyType preferredChartDifficultyType)
		{
			if (song.Charts.Count == 0)
				return null;
			var hasChartsOfPreferredType = song.Charts.TryGetValue(preferredChartType, out var preferredChartsByType);

			// Choose the preferred chart, if it exists.
			if (hasChartsOfPreferredType)
			{
				foreach (var chart in preferredChartsByType)
				{
					if (chart.ChartDifficultyType == preferredChartDifficultyType)
						return chart;
				}
			}

			var orderedDifficultyTypes = new[]
			{
				SMCommon.ChartDifficultyType.Challenge,
				SMCommon.ChartDifficultyType.Hard,
				SMCommon.ChartDifficultyType.Medium,
				SMCommon.ChartDifficultyType.Easy,
				SMCommon.ChartDifficultyType.Beginner,
				SMCommon.ChartDifficultyType.Edit,
			};

			// If the preferred chart doesn't exist, try to choose the highest difficulty type
			// of the preferred chart type.
			if (hasChartsOfPreferredType)
			{
				foreach (var currDifficultyType in orderedDifficultyTypes)
				{
					foreach (var chart in preferredChartsByType)
					{
						if (chart.ChartDifficultyType == currDifficultyType)
							return chart;
					}
				}
			}

			// No charts of the specified type exist. Try the next best type.
			SMCommon.ChartType nextBestChartType = SMCommon.ChartType.dance_single;
			var hasNextBestChartType = true;
			if (preferredChartType == SMCommon.ChartType.dance_single)
				nextBestChartType = SMCommon.ChartType.dance_double;
			else if (preferredChartType == SMCommon.ChartType.dance_double)
				nextBestChartType = SMCommon.ChartType.dance_single;
			else if (preferredChartType == SMCommon.ChartType.pump_single)
				nextBestChartType = SMCommon.ChartType.pump_double;
			else if (preferredChartType == SMCommon.ChartType.pump_double)
				nextBestChartType = SMCommon.ChartType.pump_single;
			else
				hasNextBestChartType = false;
			if (hasNextBestChartType)
			{
				if (song.Charts.TryGetValue(nextBestChartType, out var nextBestChartsByType))
				{
					foreach (var currDifficultyType in orderedDifficultyTypes)
					{
						foreach (var chart in nextBestChartsByType)
						{
							if (chart.ChartDifficultyType == currDifficultyType)
								return chart;
						}
					}
				}
			}

			// At this point, just return the first chart we have.
			foreach (var charts in song.Charts)
			{
				return charts.Value[0];
			}
			return null;
		}

		private string GetFullPathToMusicFile()
		{
			string musicFile = null;

			// If the active chart has a music file defined, use that.
			if (ActiveChart != null)
				musicFile = ActiveChart.MusicPath;

			// If the active chart does not have a music file defined, fall back to use the song's music file.
			if (string.IsNullOrEmpty(musicFile))
				musicFile = EditorSong?.MusicPath;

			return GetFullPathToSongResource(musicFile);
		}

		private string GetFullPathToMusicPreviewFile()
		{
			return GetFullPathToSongResource(EditorSong?.MusicPreviewPath);
		}

		private string GetFullPathToSongResource(string relativeFile)
		{
			// Most Charts in StepMania use relative paths for their music files.
			// Determine the absolute path.
			string fullPath = null;
			if (!string.IsNullOrEmpty(relativeFile))
			{
				if (System.IO.Path.IsPathRooted(relativeFile))
					fullPath = relativeFile;
				else
					fullPath = Path.Combine(EditorSong.FileDirectory, relativeFile);
			}

			return fullPath;
		}

		private void UnloadSongResources()
		{
			LaneEditStates = Array.Empty<LaneEditState>();
			EditorSong = null;
			ActiveChart = null;
			ArrowGraphicManager = null;
			Receptors = null;
			NextAutoPlayNotes = null;
			UpdateWindowTitle();
			ActionQueue.Instance.Clear();
		}

		private void UpdateWindowTitle()
		{
			var hasUnsavedChanges = false; // TODO
			var appName = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name;
			var sb = new StringBuilder();
			var title = "New File";
			if (ActiveChart != null && !string.IsNullOrEmpty(ActiveChart.EditorSong.FileName))
			{
				title = ActiveChart.EditorSong.FileName;
			}
			sb.Append(title);
			if (hasUnsavedChanges)
			{
				sb.Append(" * ");
			}
			if (ActiveChart != null)
			{ 
				sb.Append($" [{GetPrettyEnumString(ActiveChart.ChartType)} - {GetPrettyEnumString(ActiveChart.ChartDifficultyType)}]");
			}
			sb.Append(" - ");
			sb.Append(appName);
			Window.Title = sb.ToString();
		}

		#endregion Loading

		public bool IsChartSupported(Chart chart)
		{
			if (!SMCommon.TryGetChartType(chart.Type, out var chartType))
				return false;
			var typeSupported = false;
			foreach (var supportedType in SupportedChartTypes)
			{
				if (supportedType == chartType)
				{
					typeSupported = true;
					break;
				}
			}

			if (!typeSupported)
				return false;

			if (!Enum.TryParse<SMCommon.ChartDifficultyType>(chart.DifficultyType, out _))
				return false;

			return true;
		}

		private void OnDecreaseSnap()
		{
			SnapIndex--;
			if (SnapIndex < 0)
				SnapIndex = SnapLevels.Length - 1;

			if (SnapLevels[SnapIndex].Rows == 0)
				Logger.Info("Snap off");
			else
				Logger.Info($"Snap to 1/{SMCommon.MaxValidDenominator / SnapLevels[SnapIndex].Rows * 4}");
		}

		private void OnIncreaseSnap()
		{
			SnapIndex++;
			if (SnapIndex >= SnapLevels.Length)
				SnapIndex = 0;

			if (SnapLevels[SnapIndex].Rows == 0)
				Logger.Info("Snap off");
			else
				Logger.Info($"Snap to 1/{SMCommon.MaxValidDenominator / SnapLevels[SnapIndex].Rows * 4}");
		}

		private void OnMoveUp()
		{
			if (Preferences.Instance.PreferencesScroll.StopPlaybackWhenScrolling)
				StopPlayback();

			var rows = SnapLevels[SnapIndex].Rows;
			if (rows == 0)
			{
				OnMoveToPreviousMeasure();
			}
			else
			{
				var newChartPosition = ((int)Position.ChartPosition / rows) * rows;
				if (newChartPosition == (int)Position.ChartPosition)
					newChartPosition -= rows;
				Position.ChartPosition = newChartPosition;
				UpdateAutoPlayFromScrolling();
			}
		}

		private void OnMoveDown()
		{
			if (Preferences.Instance.PreferencesScroll.StopPlaybackWhenScrolling)
				StopPlayback();

			var rows = SnapLevels[SnapIndex].Rows;
			if (rows == 0)
			{
				OnMoveToNextMeasure();
			}
			else
			{
				Position.ChartPosition = ((int)Position.ChartPosition / rows) * rows + rows;
				UpdateAutoPlayFromScrolling();
			}
		}

		private void OnMoveToPreviousMeasure()
		{
			var rate = ActiveChart?.GetActiveRateAlteringEventForPosition(Position.ChartPosition);
			if (rate == null)
				return;
			var sig = rate.LastTimeSignature.Signature;
			var rows = sig.Numerator * (SMCommon.MaxValidDenominator * SMCommon.NumBeatsPerMeasure / sig.Denominator);
			Position.ChartPosition -= rows;

			UpdateAutoPlayFromScrolling();
		}

		private void OnMoveToNextMeasure()
		{
			var rate = ActiveChart?.GetActiveRateAlteringEventForPosition(Position.ChartPosition);
			if (rate == null)
				return;
			var sig = rate.LastTimeSignature.Signature;
			var rows = sig.Numerator * (SMCommon.MaxValidDenominator * SMCommon.NumBeatsPerMeasure / sig.Denominator);
			Position.ChartPosition += rows;

			UpdateAutoPlayFromScrolling();
		}

		private void OnMoveToChartStart()
		{
			if (Preferences.Instance.PreferencesScroll.StopPlaybackWhenScrolling)
				StopPlayback();

			Position.ChartPosition = 0.0;

			UpdateAutoPlayFromScrolling();
		}

		private void OnMoveToChartEnd()
		{
			if (Preferences.Instance.PreferencesScroll.StopPlaybackWhenScrolling)
				StopPlayback();
			
			if (ActiveChart != null)
				Position.ChartPosition = ActiveChart.GetEndPosition();
			else
				Position.ChartPosition = 0.0;

			UpdateAutoPlayFromScrolling();
		}

		private void OnShiftDown()
		{
			foreach (var laneEditState in LaneEditStates)
			{
				if (laneEditState.IsActive() && laneEditState.GetEventBeingEdited() != null)
				{
					laneEditState.Shift(false);
				}
			}
		}

		private void OnShiftUp()
		{
			foreach (var laneEditState in LaneEditStates)
			{
				if (laneEditState.IsActive() && laneEditState.GetEventBeingEdited() != null)
				{
					laneEditState.Shift(true);
				}
			}
		}

		private void OnLaneInputDown(int lane)
		{
			if (ActiveChart == null)
				return;
			if (lane < 0 || lane >= ActiveChart.NumInputs)
				return;

			Receptors?[lane].OnInputDown();

			if (Position.ChartPosition < 0)
				return;
			
			// TODO: If playing we should take sync into account and adjust the position.

			var row = Position.GetNearestRow();
			if (SnapLevels[SnapIndex].Rows != 0)
			{
				var snappedRow = (row / SnapLevels[SnapIndex].Rows) * SnapLevels[SnapIndex].Rows;
				if (row - snappedRow >= (SnapLevels[SnapIndex].Rows * 0.5))
					snappedRow += SnapLevels[SnapIndex].Rows;
				row = snappedRow;
			}

			// If there is a tap, mine, or hold start at this location, delete it now.
			// Deleting immediately feels more responsive than deleting on the input up event.
			EditorAction deleteAction = null;
			var existingEvent = ActiveChart.FindNoteAt(row, lane, true);
			if (existingEvent is EditorMineNoteEvent || existingEvent is EditorTapNoteEvent)
				deleteAction = new ActionDeleteEditorEvent(existingEvent);
			else if (existingEvent is EditorHoldStartNoteEvent hsn && hsn.GetRow() == row)
				deleteAction = new ActionDeleteHoldEvent(hsn);
			if (deleteAction != null)
			{
				LaneEditStates[lane].StartEditingWithDelete(existingEvent.GetRow(), deleteAction);
			}

			SetLaneInputDownNote(lane, row);

			// If we are playing, immediately commit the note so it comes out as a tap and not a short hold.
			if (Playing)
			{
				OnLaneInputUp(lane);
			}
		}

		private void SetLaneInputDownNote(int lane, int row)
		{
			// If the existing state is only a delete, revert back to that delete operation.
			if (LaneEditStates[lane].IsOnlyDelete())
			{
				LaneEditStates[lane].Clear(false);
			}

			// Otherwise, set the state to be editing a tap or a mine.
			else
			{
				if (KeyCommandManager.IsKeyDown(Keys.LeftShift))
				{
					LaneEditStates[lane].SetEditingTapOrMine(new EditorMineNoteEvent(ActiveChart, new LaneNote
					{
						Lane = lane,
						IntegerPosition = row,
						TimeMicros = ToMicros(Position.ChartTime),
						SourceType = SMCommon.NoteChars[(int)SMCommon.NoteType.Mine].ToString()
					}, true));
				}
				else
				{
					LaneEditStates[lane].SetEditingTapOrMine(new EditorTapNoteEvent(ActiveChart, new LaneTapNote
					{
						Lane = lane,
						IntegerPosition = row,
						TimeMicros = ToMicros(Position.ChartTime),
					}, true));
				}
			}
		}

		private void OnLaneInputUp(int lane)
		{
			if (ActiveChart == null)
				return;
			if (lane < 0 || lane >= ActiveChart.NumInputs)
				return;

			Receptors?[lane].OnInputUp();

			if (!LaneEditStates[lane].IsActive())
				return;

			// If this action is only a delete, just commit the existing delete action.
			if (LaneEditStates[lane].IsOnlyDelete())
			{
				LaneEditStates[lane].Commit();
				return;
			}

			var row = LaneEditStates[lane].GetEventBeingEdited().GetRow();
			var existingEvent = ActiveChart.FindNoteAt(row, lane, true);

			var newNoteIsMine = LaneEditStates[lane].GetEventBeingEdited() is EditorMineNoteEvent;
			var newNoteIsTap = LaneEditStates[lane].GetEventBeingEdited() is EditorTapNoteEvent;

			// Handle a new tap note overlapping an existing note.
			if (newNoteIsMine || newNoteIsTap)
			{
				if (existingEvent != null)
				{
					var existingIsTap = existingEvent is EditorTapNoteEvent;
					var existingIsMine = existingEvent is EditorMineNoteEvent;

					// Tap note over existing tap note.
					if (existingIsTap || existingIsMine)
					{
						// If the existing note is a tap and this note is a mine, then replace it with the mine.
						if (!existingIsMine && newNoteIsMine)
						{
							LaneEditStates[lane].SetEditingTapOrMine(LaneEditStates[lane].GetEventBeingEdited(), new List<EditorAction>
							{
								new ActionDeleteEditorEvent(existingEvent)
							});
						}

						// In all other cases, just delete the existing note and don't add anything else.
						else
						{
							LaneEditStates[lane].Clear(true);
							ActionQueue.Instance.Do(new ActionDeleteEditorEvent(existingEvent));
							return;
						}
					}

					// Tap note over hold note.
					else if (existingEvent is EditorHoldStartNoteEvent || existingEvent is EditorHoldEndNoteEvent)
					{
						var holdStart = existingEvent is EditorHoldStartNoteEvent
							? (EditorHoldStartNoteEvent)existingEvent
							: ((EditorHoldEndNoteEvent)existingEvent).GetHoldStartNote();

						// If the tap note starts at the beginning of the hold, delete the hold.
						if (row == holdStart.GetRow())
						{
							LaneEditStates[lane].SetEditingTapOrMine(LaneEditStates[lane].GetEventBeingEdited(), new List<EditorAction>
							{
								new ActionDeleteHoldEvent(holdStart)
							});
						}

						// If the tap note is in the in the middle of the hold, shorten the hold.
						else
						{
							// Move the hold up by a 16th.
							var newHoldEndRow = row - (SMCommon.MaxValidDenominator / 4);

							// If the hold would have a non-positive length, delete it and replace it with a tap.
							if (newHoldEndRow <= holdStart.GetRow())
							{
								var deleteHold = new ActionDeleteHoldEvent(holdStart);
								var insertNewNoteAtHoldStart = new ActionAddEditorEvent(new EditorTapNoteEvent(ActiveChart, new LaneTapNote
								{
									Lane = lane,
									IntegerPosition = holdStart.GetEvent().IntegerPosition,
									TimeMicros = holdStart.GetEvent().TimeMicros,
								}));

								LaneEditStates[lane].SetEditingTapOrMine(LaneEditStates[lane].GetEventBeingEdited(), new List<EditorAction>
								{
									deleteHold,
									insertNewNoteAtHoldStart
								});
							}

							// Otherwise, the new length is valid. Update it.
							else
							{
								var changeLength = new ActionChangeHoldLength(holdStart, newHoldEndRow - holdStart.GetRow());
								LaneEditStates[lane].SetEditingTapOrMine(LaneEditStates[lane].GetEventBeingEdited(), new List<EditorAction>
								{
									changeLength
								});
							}
						}
					}
				}
			}

			// Handle a new hold note overlapping any existing notes
			else if (LaneEditStates[lane].GetEventBeingEdited() is EditorHoldStartNoteEvent editHsn)
			{
				var length = editHsn.GetLength();
				var roll = editHsn.IsRoll();

				if (existingEvent is EditorHoldEndNoteEvent hen)
					existingEvent = hen.GetHoldStartNote();

				// If the hold is completely within another hold, do not add or delete notes, but make sure the outer
				// hold is the same type (hold/roll) as the new type.
				if (existingEvent != null
				    && existingEvent is EditorHoldStartNoteEvent hsnFull
				    && hsnFull.GetRow() <= row
				    && hsnFull.GetRow() + hsnFull.GetLength() >= row + length)
				{
					LaneEditStates[lane].Clear(true);
					if (hsnFull.IsRoll() != roll)
						ActionQueue.Instance.Do(new ActionChangeHoldType(hsnFull, roll));
					return;
				}

				var deleteActions = new List<EditorAction>();

				// If existing holds overlap with only the start or end of the new hold, delete them and extend the
				// new hold to cover their range. We just need to extend the new event now. The deletion of the
				// old event will will be handled below when we check for events fully contained within the new
				// hold region.
				if (existingEvent != null
				    && existingEvent is EditorHoldStartNoteEvent hsnStart
				    && hsnStart.GetRow() < row
				    && hsnStart.GetRow() + hsnStart.GetLength() >= row
				    && hsnStart.GetRow() + hsnStart.GetLength() < row + length)
				{
					row = hsnStart.GetRow();
					length = editHsn.GetHoldEndNote().GetRow() - hsnStart.GetRow();
				}
				existingEvent = ActiveChart.FindNoteAt(row + length, lane, true);
				if (existingEvent != null
				    && existingEvent is EditorHoldStartNoteEvent hsnEnd
				    && hsnEnd.GetRow() <= row + length
					&& hsnEnd.GetRow() + hsnEnd.GetLength() >= row + length
				    && hsnEnd.GetRow() > row)
				{
					length = hsnEnd.GetRow() + hsnEnd.GetLength() - row;
				}

				// For any event in the same lane within the region of the new hold, delete them.
				var e = FindBest(ActiveChart.EditorEvents, row);
				if (e != null)
				{
					while (e.MoveNext() && e.Current.GetRow() <= row + length)
					{
						if (e.Current.GetRow() < row)
							continue;
						if (e.Current.GetLane() != lane)
							continue;
						if (e.Current.IsBeingEdited())
							continue;
						if (e.Current is EditorTapNoteEvent || e.Current is EditorMineNoteEvent)
							deleteActions.Add(new ActionDeleteEditorEvent(e.Current));
						else if (e.Current is EditorHoldStartNoteEvent innerHsn && innerHsn.GetRow() + innerHsn.GetLength() <= row + length)
							deleteActions.Add(new ActionDeleteHoldEvent(innerHsn));
					}
				}

				// Set the state to be editing a new hold after running the delete actions.
				LaneEditStates[lane].SetEditingHold(ActiveChart, lane, row, LaneEditStates[lane].GetStartingRow(), length, roll, deleteActions);
			}

			LaneEditStates[lane].Commit();
		}

		private void UpdateLaneEditStatesFromPosition()
		{
			var row = Math.Max(0, Position.GetNearestRow());
			for (var lane = 0; lane < LaneEditStates.Length; lane++)
			{
				var laneEditState = LaneEditStates[lane];
				if (!laneEditState.IsActive())
					continue;

				// If moving back to the starting position.
				// In other words, the current state of the note being edited should be a tap.
				if (laneEditState.GetStartingRow() == row)
				{
					// If the event is a hold, convert it to a tap.
					// This will also convert holds to tap even if the starting action was deleting an existing note.
					if (laneEditState.GetEventBeingEdited() is EditorHoldStartNoteEvent)
					{
						SetLaneInputDownNote(lane, row);
					}
				}

				// If the current position is different than the starting position.
				// In other words, the current state of the note being edited should be a hold.
				else
				{
					var holdStartRow = laneEditState.GetStartingRow() < row ? laneEditState.GetStartingRow() : row;
					var holdEndRow = laneEditState.GetStartingRow() > row ? laneEditState.GetStartingRow() : row;

					// If the event is a tap, mine, deletion, or it is a hold with different bounds, convert it to a new hold.
					if (laneEditState.GetEventBeingEdited() is null
					    || laneEditState.GetEventBeingEdited() is EditorTapNoteEvent
					    || laneEditState.GetEventBeingEdited() is EditorMineNoteEvent
					    || (laneEditState.GetEventBeingEdited() is EditorHoldStartNoteEvent h
					        && (holdStartRow != h.GetRow() || holdEndRow != h.GetHoldEndNote().GetRow())))
					{
						var roll = KeyCommandManager.IsKeyDown(Keys.LeftShift);
						LaneEditStates[lane].SetEditingHold(ActiveChart, lane, holdStartRow, laneEditState.GetStartingRow(), holdEndRow - holdStartRow, roll);
					}
				}
			}
		}

		private bool CancelLaneInput()
		{
			var anyCancelled = false;
			foreach (var laneEditState in LaneEditStates)
			{
				if (laneEditState.IsActive())
				{
					laneEditState.Clear(true);
					anyCancelled = true;
				}
			}
			return anyCancelled;
		}

		private void OnUndo()
		{
			var action = ActionQueue.Instance.Undo();
		}

		private void OnRedo()
		{
			var action = ActionQueue.Instance.Redo();
		}

		private void OnEscape()
		{
			if (CancelLaneInput())
				return;
			if (IsPlayingPreview())
				StopPreview();
			else if (Playing)
				StopPlayback();
		}

		private void OnOpen()
		{
			OpenSongFile();
		}

		private void OnNew()
		{
			// Prompt for saving first.

		}
		private void OnSave()
		{
			// TODO: Export vs Save
			if (EditorSong != null)
			{
				var song = EditorSong.SaveToSong();

				//var saveFile = Fumen.Path.GetWin32FileSystemFullPath(Fumen.Path.Combine(songArgs.SaveDir, songArgs.FileInfo.Name));
				//var config = new SMWriterBase.SMWriterBaseConfig
				//{
				//	FilePath = saveFile,
				//	Song = song,
				//	MeasureSpacingBehavior = SMWriterBase.MeasureSpacingBehavior.UseLeastCommonMultiple,
				//	PropertyEmissionBehavior = SMWriterBase.PropertyEmissionBehavior.Stepmania,
				//};
				//var fileFormat = FileFormat.GetFileFormatByExtension(songArgs.FileInfo.Extension);
				//switch (fileFormat.Type)
				//{
				//	case FileFormatType.SM:
				//		new SMWriter(config).Save();
				//		break;
				//	case FileFormatType.SSC:
				//		new SSCWriter(config).Save();
				//		break;
				//	default:
				//		LogError("Unsupported file format. Cannot save.", songArgs.FileInfo, songArgs.RelativePath);
				//		break;
				//}
			}
		}

		private void OnExport()
		{
			if (EditorSong == null)
				return;

			if (EditorSong.FileFormat == null)
				return;

			Export(EditorSong.FileFormat.Type, EditorSong.FileFullPath, EditorSong);
		}

		private void OnExportAs()
		{
			if (EditorSong == null)
				return;

			SaveFileDialog saveFileDialog1 = new SaveFileDialog();
			saveFileDialog1.Filter = "SSC File|*.ssc|SM File|*.sm";
			saveFileDialog1.Title = "Export As...";
			if (saveFileDialog1.ShowDialog() != DialogResult.OK)
				return;
			
			var fullPath = saveFileDialog1.FileName;
			var extension = System.IO.Path.GetExtension(fullPath);
			var fileFormat = FileFormat.GetFileFormatByExtension(extension);
			if (fileFormat == null)
				return;

			Export(fileFormat.Type, fullPath, EditorSong);
		}

		private void Export(FileFormatType fileType, string fullPath, EditorSong editorSong)
		{
			// TODO: Check for incompatible features with SM format.
			if (fileType == FileFormatType.SM)
			{

			}

			// Temp hack to not overwrite original file.
			var start = fullPath.Substring(0, fullPath.LastIndexOf('.'));
			var end = fullPath.Substring(fullPath.LastIndexOf('.'));
			fullPath = $"{start}-exported{end}";

			var song = EditorSong.SaveToSong();
			var config = new SMWriterBase.SMWriterBaseConfig
			{
				FilePath = fullPath,
				Song = song,
				MeasureSpacingBehavior = SMWriterBase.MeasureSpacingBehavior.UseLeastCommonMultiple,
				PropertyEmissionBehavior = SMWriterBase.PropertyEmissionBehavior.Stepmania,
			};
			switch (fileType)
			{
				case FileFormatType.SM:
					new SMWriter(config).Save();
					break;
				case FileFormatType.SSC:
					new SSCWriter(config).Save();
					break;
				default:
					Logger.Error("Unsupported file format. Cannot save.");
					break;
			}

			// Update the EditorSong's file path information.
			editorSong.SetFullFilePath(fullPath);
		}

		private void OnTogglePlayback()
		{
			if (Playing)
				StopPlayback();
			else
				StartPlayback();
		}

		public void OnTogglePlayPreview()
		{
			if (Playing)
				StopPlayback();

			if (!PlayingPreview)
				StartPreview();
			else
				StopPreview();
		}

		public void StartPreview()
		{
			var success = MusicManager.StartPreviewPlayback();
			if (success)
				PlayingPreview = true;
		}

		public void StopPreview()
		{
			if (!PlayingPreview)
				return;

			MusicManager.StopPreviewPlayback();
			PlayingPreview = false;
		}

		public bool IsPlayingPreview()
		{
			return PlayingPreview;
		}

		public void OnSongMusicChanged()
		{
			StopPreview();
			MusicManager.LoadMusicAsync(GetFullPathToMusicFile(), GetSongTime);
		}

		public void OnSongMusicPreviewChanged()
		{
			StopPreview();
			MusicManager.LoadMusicPreviewAsync(GetFullPathToMusicPreviewFile());
		}

		public void OnMusicOffsetChanged()
		{
			// Re-set the position to recompute the chart and song times.
			Position.ChartPosition = Position.ChartPosition;
		}

		private double GetSongTime()
		{
			return Position.SongTime;
		}

		public EditorChart GetActiveChart()
		{
			return ActiveChart;
		}

		public void OnChartSelected(EditorChart chart, bool undoable = true)
		{
			if (ActiveChart == chart)
				return;

			// If the active chart is being changed as an undoable action, enqueue the action and return.
			// The ActionSelectChart will invoke this method again with undoable set to false.
			if (undoable)
			{
				ActionQueue.Instance.Do(new ActionSelectChart(this, chart));
				return;
			}

			ActiveChart = chart;

			// Update the recent file entry for the current song so that tracks the selected cha
			var p = Preferences.Instance;
			if (p.RecentFiles.Count > 0 && p.RecentFiles[0].FileName == EditorSong.FileName)
			{
				p.RecentFiles[0].LastChartType = ActiveChart.ChartType;
				p.RecentFiles[0].LastChartDifficultyType = ActiveChart.ChartDifficultyType;
			}

			// The Position needs to know about the active chart for doing time and row calculations.
			Position.ActiveChart = ActiveChart;

			// The receptors and arrow graphics depend on the active chart.
			ArrowGraphicManager = ArrowGraphicManager.CreateArrowGraphicManager(ActiveChart.ChartType);
			var laneEditStates = new LaneEditState[ActiveChart.NumInputs];
			var receptors = new Receptor[ActiveChart.NumInputs];
			NextAutoPlayNotes = new EditorEvent[ActiveChart.NumInputs];
			for (var i = 0; i < ActiveChart.NumInputs; i++)
			{
				laneEditStates[i] = new LaneEditState();
				receptors[i] = new Receptor(i, ArrowGraphicManager, ActiveChart);
			}
			Receptors = receptors;
			LaneEditStates = laneEditStates;

			// Window title depends on the active chart.
			UpdateWindowTitle();

			// Start loading music for this Chart.
			OnSongMusicChanged();
			OnSongMusicPreviewChanged();
		}
	}
}

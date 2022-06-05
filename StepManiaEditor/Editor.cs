using Fumen;
using ImGuiNET;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using FMOD;
using Fumen.ChartDefinition;
using Fumen.Converters;
using StepManiaLibrary;
using static StepManiaLibrary.Constants;
using static StepManiaEditor.Utils;
using ButtonState = Microsoft.Xna.Framework.Input.ButtonState;
using Keys = Microsoft.Xna.Framework.Input.Keys;
using Path = Fumen.Path;
using Vector2 = Microsoft.Xna.Framework.Vector2;

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

		private Vector2 FocalPoint;
		

		private string PendingOpenFileName;
		private SMCommon.ChartType PendingOpenFileChartType;
		private SMCommon.ChartDifficultyType PendingOpenFileChartDifficultyType;

		private static readonly SMCommon.ChartType[] SupportedChartTypes = new[]
		{
			SMCommon.ChartType.dance_single,
			SMCommon.ChartType.dance_double,
		};

		private GraphicsDeviceManager Graphics;
		private SpriteBatch SpriteBatch;
		private ImGuiRenderer ImGuiRenderer;
		private WaveFormRenderer WaveFormRenderer;
		private SoundManager SoundManager;
		private MusicManager MusicManager;
		private MiniMap MiniMap;
		private UISongProperties UISongProperties;
		private UIChartProperties UIChartProperties;
		private UIWaveFormPreferences UIWaveFormPreferences;
		private UIScrollPreferences UIScrollPreferences;
		private UIMiniMapPreferences UIMiniMapPreferences;
		private UIOptions UIOptions;

		private TextureAtlas TextureAtlas;

		private CancellationTokenSource LoadSongCancellationTokenSource;
		private Task LoadSongTask;

		private Dictionary<SMCommon.ChartType, PadData> PadDataByChartType = new Dictionary<SMCommon.ChartType, PadData>();
		private Dictionary<SMCommon.ChartType, StepGraph> StepGraphByChartType = new Dictionary<SMCommon.ChartType, StepGraph>();

		private double PlaybackStartTime;
		private Stopwatch PlaybackStopwatch;
		
		private EditorSong EditorSong;
		private EditorChart ActiveChart;
		
		private double ChartPosition = 0.0;

		private List<EditorEvent> VisibleEvents = new List<EditorEvent>();
		private List<MarkerEvent> VisibleMarkers = new List<MarkerEvent>();

		private double WaveFormPPS = 1.0;

		// temp controls
		private int MouseScrollValue = 0;
		private double SongTimeInterpolationTimeStart = 0.0;
		private double SongTime = 0.0;
		private double SongTimeAtStartOfInterpolation = 0.0;
		private double DesiredSongTime = 0.0;
		private double ZoomInterpolationTimeStart = 0.0;
		private double Zoom = 1.0;
		private double ZoomAtStartOfInterpolation = 1.0;
		private double DesiredZoom = 1.0;

		private KeyCommandManager KeyCommandManager;
		private bool Playing = false;
		private bool PlayingPreview = false;
		private bool MiniMapCapturingMouse = false;
		private bool StartPlayingWhenMiniMapDone = false;
		private MouseState PreviousMouseState;

		private uint MaxScreenHeight;

		private ImFontPtr ImGuiFont;
		private SpriteFont MonogameFont_MPlus1Code_Medium;

		// Debug
		private bool ParallelizeUpdateLoop = false;
		private double UpdateTimeTotal;
		private double UpdateTimeWaveForm;
		private double UpdateTimeMiniMap;
		private double UpdateTimeChartEvents;
		private double DrawTimeTotal;

		// Logger GUI
		private readonly LinkedList<Logger.LogMessage> LogBuffer = new LinkedList<Logger.LogMessage>();
		private readonly object LogBufferLock = new object();
		private readonly string[] LogWindowDateStrings = { "None", "HH:mm:ss", "HH:mm:ss.fff", "yyyy-MM-dd HH:mm:ss.fff" };

		private readonly System.Numerics.Vector4[] LogWindowLevelColors =
		{
			new System.Numerics.Vector4(1.0f, 1.0f, 1.0f, 1.0f),
			new System.Numerics.Vector4(1.0f, 1.0f, 0.0f, 1.0f),
			new System.Numerics.Vector4(1.0f, 0.0f, 0.0f, 1.0f),
		};

		// WaveForm GUI
		private readonly string[] WaveFormWindowSparseColorOptions =
			{ "Darker Dense Color", "Same As Dense Color", "Unique Color" };

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

			FocalPoint = new Vector2(Preferences.Instance.WindowWidth >> 1, 100 + (DefaultArrowWidth >> 1));

			SoundManager = new SoundManager();
			MusicManager = new MusicManager(SoundManager);

			Graphics = new GraphicsDeviceManager(this);

			KeyCommandManager = new KeyCommandManager();
			KeyCommandManager.Register(new KeyCommandManager.Command(new[] { Keys.LeftControl, Keys.Z }, OnUndo, true));
			KeyCommandManager.Register(new KeyCommandManager.Command(new[] { Keys.LeftControl, Keys.LeftShift, Keys.Z }, OnRedo, true));
			KeyCommandManager.Register(new KeyCommandManager.Command(new[] { Keys.LeftControl, Keys.Y }, OnRedo, true));
			KeyCommandManager.Register(new KeyCommandManager.Command(new[] { Keys.LeftControl, Keys.O }, OnOpen, false));
			KeyCommandManager.Register(new KeyCommandManager.Command(new[] { Keys.Space }, OnTogglePlayback, false));
			KeyCommandManager.Register(new KeyCommandManager.Command(new[] { Keys.P }, OnTogglePlayPreview, false));

			Content.RootDirectory = "Content";
			IsMouseVisible = true;
			Window.AllowUserResizing = true;
			Window.ClientSizeChanged += OnResize;

			IsFixedTimeStep = false;
			Graphics.SynchronizeWithVerticalRetrace = true;
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

			MonogameFont_MPlus1Code_Medium = Content.Load<SpriteFont>("mplus1code-medium");

			foreach (var adapter in GraphicsAdapter.Adapters)
			{
				MaxScreenHeight = Math.Max(MaxScreenHeight, (uint)adapter.CurrentDisplayMode.Height);
			}

			WaveFormRenderer = new WaveFormRenderer(GraphicsDevice, WaveFormTextureWidth, MaxScreenHeight);
			WaveFormRenderer.SetXPerChannelScale(p.PreferencesWaveForm.WaveFormMaxXPercentagePerChannel);
			WaveFormRenderer.SetSoundMipMap(MusicManager.GetMusicMipMap());
			WaveFormRenderer.SetFocalPoint(FocalPoint);

			MiniMap = new MiniMap(GraphicsDevice, new Rectangle(0, 0, 0, 0));
			MiniMap.SetSelectMode(p.PreferencesMiniMap.MiniMapSelectMode);

			TextureAtlas = new TextureAtlas(GraphicsDevice, 2048, 2048, 1);

			UISongProperties = new UISongProperties(this, GraphicsDevice, ImGuiRenderer);
			UIChartProperties = new UIChartProperties(this);
			UIWaveFormPreferences = new UIWaveFormPreferences(this, MusicManager);
			UIScrollPreferences = new UIScrollPreferences();
			UIMiniMapPreferences = new UIMiniMapPreferences(this);
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
			foreach (var kvp in ArrowTextureByBeatSubdivision)
				TextureAtlas.AddTexture(kvp.Value, Content.Load<Texture2D>(kvp.Value));
			TextureAtlas.AddTexture(TextureIdMine, Content.Load<Texture2D>(TextureIdMine));
			TextureAtlas.AddTexture(TextureIdReceptor, Content.Load<Texture2D>(TextureIdReceptor));
			TextureAtlas.AddTexture(TextureIdReceptorFlash, Content.Load<Texture2D>(TextureIdReceptorFlash));
			TextureAtlas.AddTexture(TextureIdReceptorGlow, Content.Load<Texture2D>(TextureIdReceptorGlow));
			TextureAtlas.AddTexture(TextureIdHoldActive, Content.Load<Texture2D>(TextureIdHoldActive));
			TextureAtlas.AddTexture(TextureIdHoldActiveCap, Content.Load<Texture2D>(TextureIdHoldActiveCap));
			TextureAtlas.AddTexture(TextureIdHoldInactive, Content.Load<Texture2D>(TextureIdHoldInactive));
			TextureAtlas.AddTexture(TextureIdHoldInactiveCap, Content.Load<Texture2D>(TextureIdHoldInactiveCap));
			TextureAtlas.AddTexture(TextureIdRollActive, Content.Load<Texture2D>(TextureIdRollActive));
			TextureAtlas.AddTexture(TextureIdRollActiveCap, Content.Load<Texture2D>(TextureIdRollActiveCap));
			TextureAtlas.AddTexture(TextureIdRollInactive, Content.Load<Texture2D>(TextureIdRollInactive));
			TextureAtlas.AddTexture(TextureIdRollInactiveCap, Content.Load<Texture2D>(TextureIdRollInactiveCap));

			var measureMarkerTexture = new Texture2D(GraphicsDevice, DefaultArrowWidth, 1);
			var textureData = new uint[DefaultArrowWidth];
			for (var i = 0; i < DefaultArrowWidth; i++)
				textureData[i] = 0xFFFFFFFF;
			measureMarkerTexture.SetData(textureData);
			TextureAtlas.AddTexture(TextureIdMeasureMarker, measureMarkerTexture);

			var beatMarkerTexture = new Texture2D(GraphicsDevice, DefaultArrowWidth, 1);
			for (var i = 0; i < DefaultArrowWidth; i++)
				textureData[i] = 0xFF7F7F7F;
			beatMarkerTexture.SetData(textureData);
			TextureAtlas.AddTexture(TextureIdBeatMarker, beatMarkerTexture);

			InitPadDataAndStepGraphsAsync();

			// If we have a saved file to open, open it now.
			if (Preferences.Instance.PreferencesOptions.OpenLastOpenedFileOnLaunch
			    && Preferences.Instance.RecentFiles.Count > 0)
			{
				OpenSongFileAsync(Preferences.Instance.RecentFiles[0].FileName,
					Preferences.Instance.RecentFiles[0].LastChartType,
					Preferences.Instance.RecentFiles[0].LastChartDifficultyType);
			}

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
				if (!SongTime.DoubleEquals(DesiredSongTime))
				{
					SongTime = Interpolation.Lerp(
						SongTimeAtStartOfInterpolation,
						DesiredSongTime,
						SongTimeInterpolationTimeStart,
						SongTimeInterpolationTimeStart + 0.1,
						gameTime.TotalGameTime.TotalSeconds);

					MusicManager.SetMusicTimeInSeconds(SongTime);
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
				SongTime = PlaybackStartTime + PlaybackStopwatch.Elapsed.TotalSeconds;

				MusicManager.Update(SongTime);
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
					if (SongTime >= 0.0 && SongTime < MusicManager.GetMusicLengthInSeconds())
					{
						if (SongTime - fmodSongTime > maxDeviation)
						{
							PlaybackStartTime -= (0.5 * maxDeviation);
							SongTime = PlaybackStartTime + PlaybackStopwatch.Elapsed.TotalSeconds;
						}
						else if (fmodSongTime - SongTime > maxDeviation)
						{
							PlaybackStartTime += (0.5 * maxDeviation);
							SongTime = PlaybackStartTime + PlaybackStopwatch.Elapsed.TotalSeconds;
						}
					}
				}

				DesiredSongTime = SongTime;
			}
			else
			{
				MusicManager.Update(SongTime);
			}

			// If using SongTime
			UpdateChartPositionFromSongTime();

			Action timedUpdateChartEvents = () =>
			{
				var s = new Stopwatch();
				s.Start();
				UpdateChartEvents();
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

			base.Update(gameTime);

			stopWatch.Stop();
			UpdateTimeTotal = stopWatch.Elapsed.TotalSeconds;
		}

		private void ProcessInput(GameTime gameTime)
		{
			var pScroll = Preferences.Instance.PreferencesScroll;

			// Let imGui process input so we can see if we should ignore it.
			(bool imGuiWantMouse, bool imGuiWantKeyboard) = ImGuiRenderer.Update(gameTime);

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
							SongTime = PlaybackStartTime + PlaybackStopwatch.Elapsed.TotalSeconds;

							if (pScroll.StopPlaybackWhenScrolling)
							{
								StopPlayback();
							}
							else
							{
								MusicManager.SetMusicTimeInSeconds(SongTime);
							}
						}
						else
						{
							DesiredSongTime -= delta;
							SongTimeInterpolationTimeStart = gameTime.TotalGameTime.TotalSeconds;
							SongTimeAtStartOfInterpolation = SongTime;
						}

						MouseScrollValue = newMouseScrollValue;
					}

					if (MouseScrollValue > newMouseScrollValue)
					{
						var delta = 0.25 * (1.0 / Zoom);
						if (Playing)
						{
							PlaybackStartTime += delta;
							SongTime = PlaybackStartTime + PlaybackStopwatch.Elapsed.TotalSeconds;

							if (pScroll.StopPlaybackWhenScrolling)
							{
								StopPlayback();
							}
							else
							{
								MusicManager.SetMusicTimeInSeconds(SongTime);
							}
						}
						else
						{
							DesiredSongTime += delta;
							SongTimeInterpolationTimeStart = gameTime.TotalGameTime.TotalSeconds;
							SongTimeAtStartOfInterpolation = SongTime;
						}

						MouseScrollValue = newMouseScrollValue;
					}
				}
			}

			PreviousMouseState = mouseState;
		}

		private void StartPlayback()
		{
			if (Playing)
				return;

			StopPreview();

			if (!MusicManager.IsMusicLoaded() || SongTime < 0.0 || SongTime > MusicManager.GetMusicLengthInSeconds())
			{
				PlaybackStartTime = SongTime;
			}
			else
			{
				PlaybackStartTime = MusicManager.GetMusicTimeInSeconds();
			}

			PlaybackStopwatch = new Stopwatch();
			PlaybackStopwatch.Start();
			MusicManager.StartPlayback(SongTime);

			Playing = true;
		}

		private void StopPlayback()
		{
			if (!Playing)
				return;

			PlaybackStopwatch.Stop();
			MusicManager.StopPlayback();

			Playing = false;
		}

		private void SetSongTime(double songTime)
		{
			SongTime = songTime;
			DesiredSongTime = songTime;
			MusicManager.SetMusicTimeInSeconds(SongTime);

			if (Playing)
			{
				PlaybackStartTime = SongTime;
				PlaybackStopwatch = new Stopwatch();
				PlaybackStopwatch.Start();
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

			SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied);
			if (Preferences.Instance.PreferencesWaveForm.ShowWaveForm)
			{
				WaveFormRenderer.Draw(SpriteBatch);
			}

			DrawMiniMap();

			DrawReceptors();
			DrawChartEvents();
			SpriteBatch.End();

			DrawGui(gameTime);

			base.Draw(gameTime);

			stopWatch.Stop();
			DrawTimeTotal = stopWatch.Elapsed.TotalSeconds;
		}

		private void DrawReceptors()
		{
			if (ActiveChart == null)
				return;

			var numArrows = ActiveChart.NumInputs;
			var zoom = Zoom;
			if (zoom > 1.0)
				zoom = 1.0;
			var arrowSize = DefaultArrowWidth * zoom;
			var xStart = FocalPoint.X - (numArrows * arrowSize * 0.5);
			var y = FocalPoint.Y - (arrowSize * 0.5);

			var rot = new[] { (float)Math.PI * 0.5f, 0.0f, (float)Math.PI, (float)Math.PI * 1.5f };
			for (var i = 0; i < numArrows; i++)
			{
				var x = xStart + i * arrowSize;
				TextureAtlas.Draw(
					TextureIdReceptor,
					SpriteBatch,
					new Rectangle((int)x, (int)y, (int)arrowSize, (int)arrowSize),
					rot[i % rot.Length],
					1.0f);
			}
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
			WaveFormRenderer.SetFocalPoint(FocalPoint);
			WaveFormRenderer.SetXPerChannelScale(pWave.WaveFormMaxXPercentagePerChannel);
			WaveFormRenderer.SetColors(
				pWave.WaveFormDenseColor.X, pWave.WaveFormDenseColor.Y, pWave.WaveFormDenseColor.Z,
				sparseColor.X, sparseColor.Y, sparseColor.Z);
			WaveFormRenderer.SetScaleXWhenZooming(pWave.WaveFormScaleXWhenZooming);
			WaveFormRenderer.Update(SongTime, Zoom, WaveFormPPS);
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
		private RedBlackTree<T>.Enumerator FindBest<T, U>(RedBlackTree<T> tree, U data, Func<U, T, int> comparer)
			where T : IComparable<T>
		{
			var enumerator = tree.FindGreatestPreceding(data, comparer);
			if (enumerator == null)
				enumerator = tree.FindLeastFollowing(data, comparer, true);
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

			if (ActiveChart == null || ActiveChart.EditorEvents == null)
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
			var holdCapHeight = DefaultHoldCapHeight * sizeZoom;

			var numArrows = ActiveChart.NumInputs;
			var xStart = FocalPoint.X - (numArrows * arrowSize * 0.5);

			// TODO: Common(?) code for determining song time by chart position or vice versa based on scroll mode

			switch (pScroll.SpacingMode)
			{
				case SpacingMode.ConstantTime:
				{
					WaveFormPPS = pScroll.TimeBasedPixelsPerSecond;

					var pps = pScroll.TimeBasedPixelsPerSecond * spacingZoom;
					var time = SongTime + GetMusicOffset();
					var timeAtTopOfScreen = time - (FocalPoint.Y / pps);
					double chartPosition = 0.0;
					if (!ActiveChart.TryGetChartPositionFromTime(timeAtTopOfScreen, ref chartPosition))
						return;

					// Beat markers.
					if (sizeZoom >= MeasureMarkerMinScale)
					{
						// Find the first rate altering event to use.
						var rateEnumerator = FindBest(ActiveChart.RateAlteringEventsByRow, chartPosition,
							EditorRateAlteringEvent.CompareToRow);
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

					var enumerator = FindBest(ActiveChart.EditorEvents, chartPosition, EditorEvent.CompareToRow);
					if (enumerator == null)
						return;

					// Scan backwards until we have checked every lane for a long note which may
					// be extending through the given start row.
					var holdStartNotes = ScanBackwardsForHolds(enumerator, chartPosition);
					foreach (var hsn in holdStartNotes)
					{
						hsn.X = xStart + hsn.GetLane() * arrowSize;
						hsn.Y = ((hsn.ChartEvent.TimeMicros / 1000000.0) - timeAtTopOfScreen) * pps - (arrowSize * 0.5);
						hsn.W = arrowSize;
						hsn.H = arrowSize;
						hsn.Scale = sizeZoom;
						noteEvents.Add(hsn);

						var end = hsn.GetHoldEndNote();
						holdBodyEvents.Add(end);
						end.X = xStart + end.GetLane() * arrowSize;
						end.Y = ((hsn.ChartEvent.TimeMicros / 1000000.0) - timeAtTopOfScreen) * pps;
						end.W = arrowSize;
						end.H = (((end.ChartEvent.TimeMicros / 1000000.0) - timeAtTopOfScreen) * pps) - end.Y + holdCapHeight;
						end.Scale = sizeZoom;
					}

					// Scan forward and add notes.
					while (enumerator.MoveNext())
					{
						var e = enumerator.Current;
						var y = ((e.ChartEvent.TimeMicros / 1000000.0) - timeAtTopOfScreen) * pps - (arrowSize * 0.5);
						if (y > screenHeight)
							break;

						if (e is EditorHoldEndNote)
							continue;

						if (e is EditorTapNote
						    || e is EditorMineNote
						    || e is EditorHoldStartNote)
						{
							e.X = xStart + e.GetLane() * arrowSize;
							e.Y = y;
							e.W = arrowSize;
							e.H = arrowSize;
							e.Scale = sizeZoom;

							if (e is EditorHoldStartNote hsn)
							{
								var end = hsn.GetHoldEndNote();
								holdBodyEvents.Add(end);
								end.X = xStart + end.GetLane() * arrowSize;
								end.Y = ((e.ChartEvent.TimeMicros / 1000000.0) - timeAtTopOfScreen) * pps;
								end.W = arrowSize;
								end.H = (((end.ChartEvent.TimeMicros / 1000000.0) - timeAtTopOfScreen) * pps) - end.Y +
								        holdCapHeight;
								end.Scale = sizeZoom;
							}
						}

						noteEvents.Add(e);

						if (noteEvents.Count + holdBodyEvents.Count > MaxEventsToDraw)
							break;
					}

					break;
				}

				case SpacingMode.ConstantRow:
				{
					var time = SongTime + GetMusicOffset();
					double chartPosition = 0.0;
					if (!ActiveChart.TryGetChartPositionFromTime(time, ref chartPosition))
						return;
					var ppr = pScroll.RowBasedPixelsPerRow * spacingZoom;
					var chartPositionAtTopOfScreen = chartPosition - (FocalPoint.Y / ppr);

					// Update WaveForm scroll scroll rate
					{
						var rateEnumerator = FindBest(ActiveChart.RateAlteringEventsByRow, chartPosition,
							EditorRateAlteringEvent.CompareToRow);
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
						var rateEnumerator = FindBest(ActiveChart.RateAlteringEventsByRow, chartPositionAtTopOfScreen,
							EditorRateAlteringEvent.CompareToRow);
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

					var enumerator = FindBest(ActiveChart.EditorEvents, chartPositionAtTopOfScreen, EditorEvent.CompareToRow);
					if (enumerator == null)
						return;

					// Scan backwards until we have checked every lane for a long note which may
					// be extending through the given start row.
					var holdStartNotes = ScanBackwardsForHolds(enumerator, chartPositionAtTopOfScreen);
					foreach (var hsn in holdStartNotes)
					{
						hsn.X = xStart + hsn.GetLane() * arrowSize;
						hsn.Y = (hsn.GetRow() - chartPositionAtTopOfScreen) * ppr - (arrowSize * 0.5);
						hsn.W = arrowSize;
						hsn.H = arrowSize;
						hsn.Scale = sizeZoom;
						noteEvents.Add(hsn);

						var end = hsn.GetHoldEndNote();
						holdBodyEvents.Add(end);
						end.X = xStart + end.GetLane() * arrowSize;
						end.Y = (hsn.GetRow() - chartPositionAtTopOfScreen) * ppr;
						end.W = arrowSize;
						end.H = ((end.GetRow() - chartPositionAtTopOfScreen) * ppr) - end.Y + holdCapHeight;
						end.Scale = sizeZoom;
					}

					// Scan forward and add notes.
					while (enumerator.MoveNext())
					{
						var e = enumerator.Current;
						var y = (e.GetRow() - chartPositionAtTopOfScreen) * ppr - (arrowSize * 0.5);
						if (y > screenHeight)
							break;

						if (e is EditorHoldEndNote)
							continue;

						if (e is EditorTapNote
						    || e is EditorMineNote
						    || e is EditorHoldStartNote)
						{
							e.X = xStart + e.GetLane() * arrowSize;
							e.Y = y;
							e.W = arrowSize;
							e.H = arrowSize;
							e.Scale = sizeZoom;

							if (e is EditorHoldStartNote hsn)
							{
								var end = hsn.GetHoldEndNote();
								holdBodyEvents.Add(end);
								end.X = xStart + end.GetLane() * arrowSize;
								end.Y = (e.GetRow() - chartPositionAtTopOfScreen) * ppr;
								end.W = arrowSize;
								end.H = ((end.GetRow() - chartPositionAtTopOfScreen) * ppr) - end.Y + holdCapHeight;
								end.Scale = sizeZoom;
							}
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

					var time = SongTime + GetMusicOffset();
					double chartPosition = 0.0;
					if (!ActiveChart.TryGetChartPositionFromTime(time, ref chartPosition))
						return;
					var ratePosEventForChecking = new EditorInterpolatedRateAlteringEvent
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

					var rateEnumerator = FindBest(ActiveChart.RateAlteringEventsByRow, chartPosition, EditorRateAlteringEvent.CompareToRow);
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
					var enumerator = FindBest(ActiveChart.EditorEvents, chartPositionAtTopOfScreen, EditorEvent.CompareToRow);
					if (enumerator == null)
						return;

					// Scan backwards until we have checked every lane for a long note which may
					// be extending through the given start row. We cannot add the end events yet because
					// we do not know at what position they will end until we scan down.
					var holdEndNotesNeedingToBeAdded = new EditorHoldEndNote[ActiveChart.NumInputs];
					var holdStartNotes = ScanBackwardsForHolds(enumerator, chartPositionAtTopOfScreen);
					foreach (var hsn in holdStartNotes)
					{
						hsn.X = xStart + hsn.GetLane() * arrowSize;
						hsn.Y = (hsn.ChartEvent.IntegerPosition - chartPositionAtTopOfScreen) * ppr -
						        (arrowSize * 0.5);
						hsn.W = arrowSize;
						hsn.H = arrowSize;
						hsn.Scale = sizeZoom;
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
						if (nextRateEvent != null && e.ChartEvent == nextRateEvent.ChartEvent)
						{
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
						var y = previousRateEventPixelPosition +
							(e.ChartEvent.IntegerPosition - rateEvent.Row) * ppr - (arrowSize * 0.5);
						if (y > screenHeight)
							break;

						// Record note.
						if (e is EditorTapNote
						    || e is EditorHoldStartNote
						    || e is EditorHoldEndNote
						    || e is EditorMineNote)
						{
							e.X = xStart + e.GetLane() * arrowSize;
							e.Y = y;
							e.W = arrowSize;
							e.H = arrowSize;
							e.Scale = sizeZoom;

							if (e is EditorHoldEndNote hen)
							{
								var start = hen.GetHoldStartNote();
								var endY = previousRateEventPixelPosition +
								           (e.ChartEvent.IntegerPosition - rateEvent.Row) * ppr;

								e.Y = start.Y + arrowSize * 0.5f;
								e.H = endY - start.Y;

								holdBodyEvents.Add(e);

								// Remove from holdEndNotesNeedingToBeAdded.
								holdEndNotesNeedingToBeAdded[e.GetLane()] = null;
							}

							else if (e is EditorHoldStartNote hsn)
							{
								// Record that there is in an in-progress hold that will need to be ended.
								holdEndNotesNeedingToBeAdded[e.GetLane()] = hsn.GetHoldEndNote();
								noteEvents.Add(e);
							}
							else
							{
								noteEvents.Add(e);
							}
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

						holdEndNote.X = xStart + holdEndNote.GetLane() * arrowSize;
						holdEndNote.W = arrowSize;
						holdEndNote.Scale = sizeZoom;

						var start = holdEndNote.GetHoldStartNote();
						var endY = previousRateEventPixelPosition +
						           (holdEndNote.ChartEvent.IntegerPosition - rateEvent.Row) * ppr;

						holdEndNote.Y = start.Y + arrowSize * 0.5;
						holdEndNote.H = endY - start.Y;

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
					VisibleMarkers.Add(new MarkerEvent
					{
						X = x,
						Y = y,
						W = ActiveChart.NumInputs * DefaultArrowWidth * sizeZoom,
						H = 1,
						MeasureMarker = measureMarker,
						Measure = measure,
						Scale = sizeZoom
					});
				}

				lastRecordedRow = currentRow;

				if (VisibleMarkers.Count >= MaxMarkersToDraw)
					return true;

				// Advance one beat.
				currentRow += rowsPerBeat;
			}
		}

		#region MiniMap

		private void ProcessInputForMiniMap(MouseState mouseState)
		{
			var pScroll = Preferences.Instance.PreferencesScroll;
			var pMiniMap = Preferences.Instance.PreferencesMiniMap;

			var mouseDownThisFrame = mouseState.LeftButton == ButtonState.Pressed &&
			                         PreviousMouseState.LeftButton != ButtonState.Pressed;
			var mouseUpThisFrame = mouseState.LeftButton != ButtonState.Pressed &&
			                       PreviousMouseState.LeftButton == ButtonState.Pressed;
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
			miniMapNeedsMouseThisFrame |= MiniMapCapturingMouse;
			if (miniMapNeedsMouseThisFrame)
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
						SetSongTime(editorPosition + (FocalPoint.Y / (pScroll.TimeBasedPixelsPerSecond * Zoom)) -
						            GetMusicOffset());
						break;
					}
					case SpacingMode.ConstantRow:
					{
						var chartPosition =
							editorPosition + (FocalPoint.Y / (pScroll.RowBasedPixelsPerRow * Zoom));
						var songTime = 0.0;
						if (ActiveChart.TryGetTimeFromChartPosition(chartPosition, ref songTime))
						{
							SetSongTime(songTime);
						}

						break;
					}
				}
			}

			// When letting go of the MiniMap, start playing again.
			if (miniMapCapturingMouseLastFrame && !MiniMapCapturingMouse && StartPlayingWhenMiniMapDone)
			{
				StartPlayingWhenMiniMapDone = false;
				StartPlayback();
			}
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
					x = Graphics.PreferredBackBufferWidth - MiniMapXPadding - (int)p.PreferencesMiniMap.MiniMapWidth;
					break;
				}
				case MiniMap.Position.RightOfChartArea:
				{
					x = (int)(FocalPoint.X + (WaveFormTextureWidth >> 1) + MiniMapXPadding);
					break;
				}
				case MiniMap.Position.MountedToWaveForm:
				{
					if (p.PreferencesWaveForm.WaveFormScaleXWhenZooming)
					{
						x = (int)(FocalPoint.X + (WaveFormTextureWidth >> 1) * zoom + MiniMapXPadding);
					}
					else
					{
						x = (int)(FocalPoint.X + (WaveFormTextureWidth >> 1) + MiniMapXPadding);
					}

					break;
				}
				case MiniMap.Position.MountedToChart:
				{
					x = (int)(FocalPoint.X + ((ActiveChart.NumInputs * DefaultArrowWidth) >> 1) * zoom + MiniMapXPadding);
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

			if (ActiveChart == null || ActiveChart.EditorEvents == null)
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
					var time = SongTime + GetMusicOffset();

					// Editor Area. The visible time range.
					var editorAreaTimeStart = time - (FocalPoint.Y / pps);
					var editorAreaTimeEnd = editorAreaTimeStart + (screenHeight / pps);
					var editorAreaTimeRange = editorAreaTimeEnd - editorAreaTimeStart;

					// Determine the end time.
					var lastEvent = ActiveChart.EditorEvents.Last();
					var maxTimeFromChart = 0.0;
					if (lastEvent.MoveNext())
						maxTimeFromChart = lastEvent.Current.ChartEvent.TimeMicros / 1000000.0;
					maxTimeFromChart = Math.Max(maxTimeFromChart, EditorSong.LastSecondHint);

					// Full Area. The time from the chart, extended in both directions by the editor range.
					var fullAreaTimeStart = 0.0 - editorAreaTimeRange;
					var fullAreaTimeEnd = maxTimeFromChart + editorAreaTimeRange;

					// Content Area. The time from the chart.
					var contentAreaTimeStart = 0.0;
					var contentAreaTimeEnd = maxTimeFromChart;

					// Update the MiniMap with the ranges.
					MiniMap.SetNumLanes((uint)ActiveChart.NumInputs);
					MiniMap.UpdateBegin(
						fullAreaTimeStart, fullAreaTimeEnd,
						contentAreaTimeStart, contentAreaTimeEnd,
						pMiniMap.MiniMapVisibleTimeRange,
						editorAreaTimeStart, editorAreaTimeEnd);

					// Add notes
					double chartPosition = 0.0;
					if (!ActiveChart.TryGetChartPositionFromTime(MiniMap.GetMiniMapAreaStart(), ref chartPosition))
						break;
					var enumerator = FindBest(ActiveChart.EditorEvents, chartPosition, EditorEvent.CompareToRow);
					if (enumerator == null)
						break;

					var numNotesAdded = 0;

					// Scan backwards until we have checked every lane for a long note which may
					// be extending through the given start row.
					var holdStartNotes = ScanBackwardsForHolds(enumerator, chartPosition);
					foreach (var hsn in holdStartNotes)
					{
						MiniMap.AddHold(
							(LaneHoldStartNote)hsn.ChartEvent,
							hsn.ChartEvent.TimeMicros / 1000000.0,
							hsn.GetHoldEndNote().ChartEvent.TimeMicros / 1000000.0,
							hsn.IsRoll());
						numNotesAdded++;
					}

					while (enumerator.MoveNext())
					{
						var e = enumerator.Current;
						if (e is EditorHoldEndNote)
							continue;

						if (e is EditorTapNote)
						{
							numNotesAdded++;
							if (MiniMap.AddNote((LaneNote)e.ChartEvent, e.ChartEvent.TimeMicros / 1000000.0) ==
							    MiniMap.AddResult.BelowBottom)
								break;
						}
						else if (e is EditorMineNote)
						{
							numNotesAdded++;
							if (MiniMap.AddMine((LaneNote)e.ChartEvent, e.ChartEvent.TimeMicros / 1000000.0) ==
							    MiniMap.AddResult.BelowBottom)
								break;
						}
						else if (e is EditorHoldStartNote hsn)
						{
							numNotesAdded++;
							if (MiniMap.AddHold(
								    (LaneHoldStartNote)e.ChartEvent,
								    e.ChartEvent.TimeMicros / 1000000.0,
								    hsn.GetHoldEndNote().ChartEvent.TimeMicros / 1000000.0,
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
					var time = SongTime + GetMusicOffset();
					var screenHeight = Graphics.GraphicsDevice.Viewport.Height;
					double chartPosition = 0.0;
					if (!ActiveChart.TryGetChartPositionFromTime(time, ref chartPosition))
						return;
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
						maxRowFromChart = lastEvent.Current.ChartEvent.IntegerPosition;

					if (EditorSong.LastSecondHint > 0.0)
					{
						var lastSecondChartPosition = 0.0;
						if (ActiveChart.TryGetChartPositionFromTime(time, ref lastSecondChartPosition))
						{
							maxRowFromChart = Math.Max(lastSecondChartPosition, EditorSong.LastSecondHint);
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
						editorAreaRowStart, editorAreaRowEnd);

					// Add notes
					chartPosition = MiniMap.GetMiniMapAreaStart();
					var enumerator = FindBest(ActiveChart.EditorEvents, chartPosition, EditorEvent.CompareToRow);
					if (enumerator == null)
						break;

					var numNotesAdded = 0;

					// Scan backwards until we have checked every lane for a long note which may
					// be extending through the given start row.
					var holdStartNotes = ScanBackwardsForHolds(enumerator, chartPosition);
					foreach (var hsn in holdStartNotes)
					{
						MiniMap.AddHold(
							(LaneHoldStartNote)hsn.ChartEvent,
							hsn.ChartEvent.IntegerPosition,
							hsn.GetHoldEndNote().ChartEvent.IntegerPosition,
							hsn.IsRoll());
						numNotesAdded++;
					}

					while (enumerator.MoveNext())
					{
						var e = enumerator.Current;
						if (e is EditorHoldEndNote)
							continue;

						if (e is EditorTapNote)
						{
							numNotesAdded++;
							if (MiniMap.AddNote((LaneNote)e.ChartEvent, e.ChartEvent.IntegerPosition) ==
							    MiniMap.AddResult.BelowBottom)
								break;
						}
						else if (e is EditorMineNote)
						{
							numNotesAdded++;
							if (MiniMap.AddMine((LaneNote)e.ChartEvent, e.ChartEvent.IntegerPosition) ==
							    MiniMap.AddResult.BelowBottom)
								break;
						}
						else if (e is EditorHoldStartNote hsn)
						{
							numNotesAdded++;
							if (MiniMap.AddHold(
								    (LaneHoldStartNote)e.ChartEvent,
								    e.ChartEvent.IntegerPosition,
								    hsn.GetHoldEndNote().ChartEvent.IntegerPosition,
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
		private List<EditorHoldStartNote> ScanBackwardsForHolds(RedBlackTree<EditorEvent>.Enumerator enumerator,
			double chartPosition)
		{
			var lanesChecked = new bool[ActiveChart.NumInputs];
			var numLanesChecked = 0;
			var holds = new List<EditorHoldStartNote>();
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
							if (e is EditorHoldStartNote hsn)
							{
								holds.Add(hsn);
							}
						}
					}
				}
			}

			return holds;
		}

		private void DrawChartEvents()
		{
			foreach (var visibleMarker in VisibleMarkers)
			{
				visibleMarker.Draw(TextureAtlas, SpriteBatch, MonogameFont_MPlus1Code_Medium);
			}

			foreach (var visibleEvent in VisibleEvents)
			{
				visibleEvent.Draw(TextureAtlas, SpriteBatch);
			}
		}


		#region Gui Rendering

		private void DrawGui(GameTime gameTime)
		{
			ImGui.PushFont(ImGuiFont);

			DrawMainMenuUI();

			DrawDebugUI();

			if (ShowImGuiTestWindow)
			{
				ImGui.SetNextWindowPos(new System.Numerics.Vector2(650, 20), ImGuiCond.FirstUseEver);
				ImGui.ShowDemoWindow(ref ShowImGuiTestWindow);
			}

			DrawLogUI();
			UIScrollPreferences.Draw();
			UIWaveFormPreferences.Draw();
			UIMiniMapPreferences.Draw();
			UIOptions.Draw();

			UISongProperties.Draw(EditorSong);
			UIChartProperties.Draw(ActiveChart);

			ImGui.PopFont();

			ImGuiRenderer.AfterLayout();
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
						OpenSongFileAsync(p.RecentFiles[0].FileName,
							p.RecentFiles[0].LastChartType,
							p.RecentFiles[0].LastChartDifficultyType);
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

					ImGui.Separator();
					if (ImGui.MenuItem("Waveform Preferences"))
						p.PreferencesWaveForm.ShowWaveFormPreferencesWindow = true;
					if (ImGui.MenuItem("Scroll Preferences"))
						p.PreferencesScroll.ShowScrollControlPreferencesWindow = true;
					if (ImGui.MenuItem("Mini Map Preferences"))
						p.PreferencesMiniMap.ShowMiniMapPreferencesWindow = true;

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
				ImGui.Text($"Update Time:       {UpdateTimeTotal:F6} seconds");
				ImGui.Text($"  Waveform:        {UpdateTimeWaveForm:F6} seconds");
				ImGui.Text($"  Mini Map:        {UpdateTimeMiniMap:F6} seconds");
				ImGui.Text($"  Chart Events:    {UpdateTimeChartEvents:F6} seconds");
				ImGui.Text($"Draw Time:         {DrawTimeTotal:F6} seconds");
				ImGui.Text($"Total Time:        {(UpdateTimeTotal + DrawTimeTotal):F6} seconds");
				ImGui.Text($"Total FPS:         {(1.0f / (UpdateTimeTotal + DrawTimeTotal)):F6}");
				ImGui.Text($"Actual Time:       {1.0f / ImGui.GetIO().Framerate:F6} seconds");
				ImGui.Text($"Actual FPS:        {ImGui.GetIO().Framerate:F6}");

				if (ImGui.Button("Save Time and Zoom"))
				{
					Preferences.Instance.DebugSongTime = SongTime;
					Preferences.Instance.DebugZoom = Zoom;
				}

				if (ImGui.Button("Load Time and Zoom"))
				{
					SetSongTime(Preferences.Instance.DebugSongTime);
					SetZoom(Preferences.Instance.DebugZoom, true);
				}
			}
			ImGui.End();
		}

		private void DrawLogUI()
		{
			if (!Preferences.Instance.ShowLogWindow)
				return;

			ImGui.SetNextWindowSize(new System.Numerics.Vector2(200, 100), ImGuiCond.FirstUseEver);
			ImGui.Begin("Log", ref Preferences.Instance.ShowLogWindow, ImGuiWindowFlags.NoScrollbar);
			lock (LogBufferLock)
			{
				ImGui.PushItemWidth(60);
				ComboFromEnum("Level", ref Preferences.Instance.LogWindowLevel);
				ImGui.PopItemWidth();

				ImGui.SameLine();
				ImGui.PushItemWidth(186);
				ImGui.Combo("Time", ref Preferences.Instance.LogWindowDateDisplay, LogWindowDateStrings,
					LogWindowDateStrings.Length);
				ImGui.PopItemWidth();

				ImGui.SameLine();
				ImGui.Checkbox("Wrap", ref Preferences.Instance.LogWindowLineWrap);

				ImGui.Separator();

				var flags = Preferences.Instance.LogWindowLineWrap ? ImGuiWindowFlags.None : ImGuiWindowFlags.HorizontalScrollbar;
				ImGui.BeginChild("LogMessages", new System.Numerics.Vector2(), false, flags);
				{
					foreach (var message in LogBuffer)
					{
						if (message.Level < Preferences.Instance.LogWindowLevel)
							continue;

						if (Preferences.Instance.LogWindowDateDisplay != 0)
						{
							ImGui.Text(message.Time.ToString(LogWindowDateStrings[Preferences.Instance.LogWindowDateDisplay]));
							ImGui.SameLine();
						}

						if (Preferences.Instance.LogWindowLineWrap)
							ImGui.PushTextWrapPos();
						ImGui.TextColored(LogWindowLevelColors[(int)message.Level], message.Message);
						if (Preferences.Instance.LogWindowLineWrap)
							ImGui.PopTextWrapPos();
					}
				}
				ImGui.EndChild();
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
							System.IO.Path.GetDirectoryName(fileName),
							song,
							GraphicsDevice,
							ImGuiRenderer);

						// Select the best Chart to make active.
						ActiveChart = SelectBestChart(EditorSong, chartType, chartDifficultyType);
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
				DesiredSongTime = 0.0;
				SongTime = 0.0;
				SetZoom(1.0, true);

				// Start loading music for this Chart.
				OnSongMusicChanged();
				OnSongMusicPreviewChanged();
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
			EditorSong = null;
			ActiveChart = null;
			ActionQueue.Instance.Clear();
		}

		#endregion Loading

		private void UpdateChartPositionFromSongTime()
		{
			ActiveChart?.TryGetChartPositionFromTime(SongTime, ref ChartPosition);
		}

		private void UpdateSongTimeFromChartPosition()
		{
			ActiveChart?.TryGetTimeFromChartPosition(ChartPosition, ref SongTime);
		}



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




		private void OnUndo()
		{
			var action = ActionQueue.Instance.Undo();
		}

		private void OnRedo()
		{
			var action = ActionQueue.Instance.Redo();
		}

		private void OnOpen()
		{
			OpenSongFile();
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

		private double GetMusicOffset()
		{
			if (ActiveChart != null && ActiveChart.UsesChartMusicOffset)
				return ActiveChart.MusicOffset;
			return EditorSong.MusicOffset;
		}

		private double GetSongTime()
		{
			return SongTime;
		}
	}
}

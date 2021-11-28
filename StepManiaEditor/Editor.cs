using FMOD;
using Fumen;
using ImGuiNET;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Numerics;
using Vector2 = Microsoft.Xna.Framework.Vector2;
using Vector4 = Microsoft.Xna.Framework.Vector4;

namespace StepManiaEditor
{
	public class Editor : Game
	{
		private const int DefaultWindowWidth = 1920;
		private const int DefaultWindowHeight = 1080;
		private const int DefaultArrowWidth = 128;
		private Vector2 FocalPoint = new Vector2(DefaultWindowWidth >> 1, 100 + (DefaultArrowWidth >> 1));

		private const int WaveFormTextureWidth = DefaultArrowWidth * 8;

		private GraphicsDeviceManager Graphics;
		private SpriteBatch SpriteBatch;
		private ImGuiRenderer ImGuiRenderer;
		private WaveFormRenderer WaveFormRenderer;
		private SoundManager SoundManager;
		private SoundMipMap SongMipMap;

		private TextureAtlas TextureAtlas;

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

		private bool SpacePressed = false;
		private bool Playing = false;

		private uint MaxScreenHeight;

		private ImFontPtr Font;

		// Logger GUI
		private readonly LinkedList<Logger.LogMessage> LogBuffer = new LinkedList<Logger.LogMessage>();
		private readonly object LogBufferLock = new object();
		private bool ShowLogWindow = true;
		private int LogWindowDateDisplay = 0;
		private readonly string[] LogWindowDateStrings = { "None", "HH:mm:ss", "HH:mm:ss.fff", "yyyy-MM-dd HH:mm:ss.fff" };
		private readonly int[] LogWindowDateWidths = { 0, 70, 100, 176 };
		private int LogWindowLevel = (int)LogLevel.Info;
		private readonly string[] LogWindowLevelStrings = {"Info", "Warn", "Error"};
		private readonly System.Numerics.Vector4[] LogWindowLevelColors = {
			new System.Numerics.Vector4(1.0f, 1.0f, 1.0f, 1.0f),
			new System.Numerics.Vector4(1.0f, 1.0f, 0.0f, 1.0f),
			new System.Numerics.Vector4(1.0f, 0.0f, 0.0f, 1.0f),
		};
		private bool LogWindowLineWrap;

		// WaveForm GUI
		private bool ShowWaveFormWindow = true;
		private bool ShowWaveForm = true;
		private bool WaveFormScaleXWhenZooming = true;
		private readonly string[] WaveFormWindowSparseColorOptions = {"Darker Dense Color", "Same As Dense Color", "Unique Color"};
		private int WaveFormWindowSparseColorOption = 0;
		private float SparseColorScale = 0.5f;
		private System.Numerics.Vector3 WaveFormDenseColor;
		private System.Numerics.Vector3 WaveFormSparseColor;
		private float WaveFormMaxXPercentagePerChannel = 0.9f;

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

			SoundManager = new SoundManager();
			SongMipMap = new SoundMipMap(SoundManager);

			Graphics = new GraphicsDeviceManager(this);

			Content.RootDirectory = "Content";
			IsMouseVisible = true;
			Window.AllowUserResizing = true;

			IsFixedTimeStep = false;

			//IsFixedTimeStep = true;
			//TargetElapsedTime = TimeSpan.FromMilliseconds(1000.0f / 200.0f);
			//Graphics.SynchronizeWithVerticalRetrace = false;
		}

		protected override void Initialize()
		{
			Graphics.PreferredBackBufferHeight = DefaultWindowHeight;
			Graphics.PreferredBackBufferWidth = DefaultWindowWidth;
			Graphics.ApplyChanges();

			ImGuiRenderer = new ImGuiRenderer(this);
			Font = ImGui.GetIO().Fonts.AddFontFromFileTTF(
				@"C:\Users\perry\Projects\Fumen\StepManiaEditor\Mplus1Code-Medium.ttf",
				15,
				null,
				ImGui.GetIO().Fonts.GetGlyphRangesJapanese());
			ImGuiRenderer.RebuildFontAtlas();

			foreach (var adapter in GraphicsAdapter.Adapters)
			{
				MaxScreenHeight = Math.Max(MaxScreenHeight, (uint)adapter.CurrentDisplayMode.Height);
			}

			WaveFormRenderer = new WaveFormRenderer(GraphicsDevice, WaveFormTextureWidth, MaxScreenHeight);
			WaveFormRenderer.SetXScale(WaveFormMaxXPercentagePerChannel);
			WaveFormRenderer.SetSoundMipMap(SongMipMap);
			WaveFormRenderer.SetFocalPoint(FocalPoint);

			TextureAtlas = new TextureAtlas(GraphicsDevice, 2048, 2048, 1);

			base.Initialize();
		}

		protected override void LoadContent()
		{
			SpriteBatch = new SpriteBatch(GraphicsDevice);

			TextureAtlas.AddTexture("1_4", Content.Load<Texture2D>("1_4"));
			TextureAtlas.AddTexture("1_8", Content.Load<Texture2D>("1_8"));
			TextureAtlas.AddTexture("receptor", Content.Load<Texture2D>("receptor"));
			
			LoadSongAsync();
			
			base.LoadContent();
		}

		protected override void Update(GameTime gameTime)
		{
			ProcessInput(gameTime);

			TextureAtlas.Update();

			// Time-dependent updates
			if (SongTime != DesiredSongTime)
			{
				SongTime = Interpolation.Lerp(
					SongTimeAtStartOfInterpolation,
					DesiredSongTime,
					SongTimeInterpolationTimeStart,
					SongTimeInterpolationTimeStart + 0.1,
					gameTime.TotalGameTime.TotalSeconds);
			}

			if (Zoom != DesiredZoom)
			{
				Zoom = Interpolation.Lerp(
					ZoomAtStartOfInterpolation,
					DesiredZoom,
					ZoomInterpolationTimeStart,
					ZoomInterpolationTimeStart + 0.1,
					gameTime.TotalGameTime.TotalSeconds);
			}

			if (Playing)
			{
				SongTime += gameTime.ElapsedGameTime.TotalSeconds;
				DesiredSongTime = SongTime;
			}

			if (SongMipMap.IsMipMapDataLoaded())
			{
				// smooth zooming in and out
				//Zoom = 4000;
				//const double startTime = 0.0;
				//if (gameTime.TotalGameTime.TotalSeconds > startTime)
				//{
				//	var period = 30;
				//	var time = (gameTime.TotalGameTime.TotalSeconds - startTime) % period;
				//	if (time > (double)period / 2)
				//		time = period - time;
					
				//	Zoom /= Math.Pow(2.0, time);
				//}
			}

			WaveFormRenderer.SetXScale(WaveFormMaxXPercentagePerChannel);
			WaveFormRenderer.SetColors(
				WaveFormDenseColor.X, WaveFormDenseColor.Y, WaveFormDenseColor.Z,
				WaveFormSparseColor.X, WaveFormSparseColor.Y, WaveFormSparseColor.Z);
			WaveFormRenderer.SetScaleXWhenZooming(WaveFormScaleXWhenZooming);
			WaveFormRenderer.Update(SongTime, Zoom);

			base.Update(gameTime);
		}

		private void ProcessInput(GameTime gameTime)
		{
			if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed ||
				Keyboard.GetState().IsKeyDown(Keys.Escape))
				Exit();

			// Let imGui process input so we can see if we should ignore it.
			(bool imGuiWantMouse, bool imGuiWantKeyboard) = ImGuiRenderer.Update(gameTime);

			// Process Keyboard Input
			var scrollShouldZoom = false;
			if (!imGuiWantKeyboard)
			{
				var bHoldingCtrl = Keyboard.GetState().IsKeyDown(Keys.LeftControl);
				scrollShouldZoom = bHoldingCtrl;

				var bHoldingSpace = Keyboard.GetState().IsKeyDown(Keys.Space);
				if (!SpacePressed && bHoldingSpace)
				{
					Playing = !Playing;
				}
				SpacePressed = bHoldingSpace;
			}

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
						DesiredSongTime -= 0.25 * (1.0 / Zoom);
						SongTimeInterpolationTimeStart = gameTime.TotalGameTime.TotalSeconds;
						SongTimeAtStartOfInterpolation = SongTime;
						MouseScrollValue = newMouseScrollValue;
					}

					if (MouseScrollValue > newMouseScrollValue)
					{
						DesiredSongTime += 0.25 * (1.0 / Zoom);
						SongTimeInterpolationTimeStart = gameTime.TotalGameTime.TotalSeconds;
						SongTimeAtStartOfInterpolation = SongTime;
						MouseScrollValue = newMouseScrollValue;
					}
				}
			}
		}

		protected override void Draw(GameTime gameTime)
		{
			GraphicsDevice.Clear(Color.Black);

			SpriteBatch.Begin();
			if (ShowWaveForm)
			{
				WaveFormRenderer.Draw(SpriteBatch);
			}
			DrawReceptors();
			SpriteBatch.End();

			DrawGui(gameTime);

			base.Draw(gameTime);
		}
		
		private void DrawGui(GameTime gameTime)
		{
			ImGui.PushFont(Font);

			DrawMainMenuUI();

			// Debug UI
			{
				ImGui.Text("Hello, world!");
				ImGui.Text(string.Format("Application average {0:F3} ms/frame ({1:F1} FPS)", 1000f / ImGui.GetIO().Framerate, ImGui.GetIO().Framerate));
			}
			if (ShowImGuiTestWindow)
			{
				ImGui.SetNextWindowPos(new System.Numerics.Vector2(650, 20), ImGuiCond.FirstUseEver);
				ImGui.ShowDemoWindow(ref ShowImGuiTestWindow);
			}

			DrawLogUI();
			DrawWaveFormUI();

			ImGui.PopFont();

			// Call AfterLayout now to finish up and draw all the things
			ImGuiRenderer.AfterLayout();
		}

		private void DrawReceptors()
		{
			var numArrows = 8;
			var zoom = Zoom;
			if (zoom > 1.0)
				zoom = 1.0;
			var arrowSize = DefaultArrowWidth * zoom;
			var xStart = FocalPoint.X - (numArrows * arrowSize * 0.5);
			var y = FocalPoint.Y - (arrowSize * 0.5);
			
			var rot = new [] {(float) Math.PI * 0.5f, 0.0f, (float) Math.PI, (float) Math.PI * 1.5f};
			for (var i = 0; i < numArrows; i++)
			{
				var x = xStart + i * arrowSize;
				TextureAtlas.Draw("receptor", SpriteBatch, new Rectangle((int)x, (int)y, (int)arrowSize, (int)arrowSize), rot[i % rot.Length]);
			}
		}

		private void DrawMainMenuUI()
		{
			if (ImGui.BeginMainMenuBar())
			{
				if (ImGui.BeginMenu("File"))
				{
					ImGui.EndMenu();
				}
				if (ImGui.BeginMenu("View"))
				{
					ImGui.Checkbox("Log", ref ShowLogWindow);
					ImGui.Checkbox("Waveform Controls", ref ShowWaveFormWindow);
					ImGui.Checkbox("ImGui Demo Window", ref ShowImGuiTestWindow);
					ImGui.EndMenu();
				}
				ImGui.EndMainMenuBar();
			}
		}

		private void DrawLogUI()
		{
			if (!ShowLogWindow)
				return;

			ImGui.SetNextWindowSize(new System.Numerics.Vector2(200, 100), ImGuiCond.FirstUseEver);
			ImGui.Begin("Log", ref ShowLogWindow, ImGuiWindowFlags.NoScrollbar);
			lock (LogBufferLock)
			{
				ImGui.PushItemWidth(60);
				ImGui.Combo("Level", ref LogWindowLevel, LogWindowLevelStrings, LogWindowLevelStrings.Length);
				ImGui.PopItemWidth(); 
				
				ImGui.SameLine();
				ImGui.PushItemWidth(186);
				ImGui.Combo("Time", ref LogWindowDateDisplay, LogWindowDateStrings, LogWindowDateStrings.Length);
				ImGui.PopItemWidth();
				
				ImGui.SameLine();
				ImGui.Checkbox("Wrap", ref LogWindowLineWrap);

				ImGui.Separator();

				var flags = LogWindowLineWrap ? ImGuiWindowFlags.None : ImGuiWindowFlags.HorizontalScrollbar;
				ImGui.BeginChild("LogMessages", new System.Numerics.Vector2(), false, flags);
				{
					foreach (var message in LogBuffer)
					{
						if ((int) message.Level < LogWindowLevel)
							continue;

						if (LogWindowDateDisplay != 0)
						{
							ImGui.Text(message.Time.ToString(LogWindowDateStrings[LogWindowDateDisplay]));
							ImGui.SameLine();
						}

						if (LogWindowLineWrap)
							ImGui.PushTextWrapPos();
						ImGui.TextColored(LogWindowLevelColors[(int) message.Level], message.Message);
						if (LogWindowLineWrap)
							ImGui.PopTextWrapPos();
					}
				}
				ImGui.EndChild();
			}
			ImGui.End();
		}

		private void DrawWaveFormUI()
		{
			if (!ShowWaveFormWindow)
				return;

			ImGui.SetNextWindowSize(new System.Numerics.Vector2(0, 0), ImGuiCond.FirstUseEver);
			ImGui.Begin("Waveform", ref ShowWaveFormWindow, ImGuiWindowFlags.NoScrollbar);

			ImGui.Checkbox("Show Waveform", ref ShowWaveForm);
			ImGui.Checkbox("Scale X When Zooming", ref WaveFormScaleXWhenZooming);
			ImGui.SliderFloat("X Scale", ref WaveFormMaxXPercentagePerChannel, 0.0f, 1.0f);

			ImGui.ColorEdit3("Dense Color", ref WaveFormDenseColor, ImGuiColorEditFlags.NoAlpha);

			ImGui.Combo("Sparse Color Mode", ref WaveFormWindowSparseColorOption, WaveFormWindowSparseColorOptions, WaveFormWindowSparseColorOptions.Length);
			if (WaveFormWindowSparseColorOption == 0)
			{
				ImGui.SliderFloat("Sparse Color Scale", ref SparseColorScale, 0.0f, 1.0f);
				WaveFormSparseColor.X = WaveFormDenseColor.X * SparseColorScale;
				WaveFormSparseColor.Y = WaveFormDenseColor.Y * SparseColorScale;
				WaveFormSparseColor.Z = WaveFormDenseColor.Z * SparseColorScale;
			}
			else if (WaveFormWindowSparseColorOption == 1)
			{
				WaveFormSparseColor = WaveFormDenseColor;
			}
			else
			{
				ImGui.ColorEdit3("Sparse Color", ref WaveFormSparseColor, ImGuiColorEditFlags.NoAlpha);
			}
			ImGui.End();
		}

		private async void LoadSongAsync()
		{
			//SongSound = await SoundManager.Load(@"C:\Games\StepMania 5\Songs\Customs\Acid Wall\Acid Wall.ogg");
			//SongSound = await SoundManager.Load(@"C:\Games\StepMania 5\Songs\Customs\ASYS Live for Shiny People & TechoV\ASYS Live.ogg");

			await SongMipMap.LoadSoundAsync(SoundManager, @"C:\Games\StepMania 5\Songs\Customs\Acid Wall\Acid Wall.ogg");
			await SongMipMap.CreateMipMapAsync(WaveFormTextureWidth);
		}
	}
}

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

namespace StepManiaEditor
{
	public class Editor : Game
	{
		private const int ReceptorY = 100;
		private const int WaveFormTextureWidth = 128 * 8;

		private GraphicsDeviceManager Graphics;
		private SpriteBatch SpriteBatch;
		private Texture2D TextureReceptor;
		private ImGuiRenderer ImGuiRenderer;
		private WaveFormRenderer WaveFormRenderer;
		private SoundManager SoundManager;
		private SoundMipMap SongMipMap;

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

		// Logger
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
			Graphics.PreferredBackBufferHeight = 1080;
			Graphics.PreferredBackBufferWidth = 1920;
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
			WaveFormRenderer.SetSoundMipMap(SongMipMap);
			WaveFormRenderer.SetYFocusOffset(128);

			base.Initialize();
		}

		protected override void LoadContent()
		{
			SpriteBatch = new SpriteBatch(GraphicsDevice);

			// TODO: use this.Content to load your game content here
			TextureReceptor = Content.Load<Texture2D>("receptor");

			// Texture loading example

			LoadSongAsync();
			
			base.LoadContent();
		}

		private int LastLogSecond = 0;

		protected override void Update(GameTime gameTime)
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

			WaveFormRenderer.Update(SongTime, Zoom);

			base.Update(gameTime);
		}

		

		protected override void Draw(GameTime gameTime)
		{
			GraphicsDevice.Clear(Color.CornflowerBlue);

			SpriteBatch.Begin();
			WaveFormRenderer.Draw(SpriteBatch);
			DrawReceptors();
			SpriteBatch.End();

			DrawGui(gameTime);

			base.Draw(gameTime);
		}

		// TEST DATA
		private float f = 0.0f;
		private bool show_test_window = false;
		private bool show_another_window = false;
		private System.Numerics.Vector3 clear_color = new System.Numerics.Vector3(114f / 255f, 144f / 255f, 154f / 255f);
		private byte[] _textBuffer = new byte[100];

		private void DrawGui(GameTime gameTime)
		{
			// Call BeforeLayout first to set things up
			ImGuiRenderer.BeforeLayout(gameTime);

			ImGui.PushFont(Font);

			// 1. Show a simple window
			// Tip: if we don't call ImGui.Begin()/ImGui.End() the widgets appears in a window automatically called "Debug"
			{
				ImGui.Text("Hello, world!");
				ImGui.SliderFloat("float", ref f, 0.0f, 1.0f, string.Empty);
				ImGui.ColorEdit3("clear color", ref clear_color);
				if (ImGui.Button("Test Window")) show_test_window = !show_test_window;
				if (ImGui.Button("Another Window")) show_another_window = !show_another_window;
				ImGui.Text(string.Format("Application average {0:F3} ms/frame ({1:F1} FPS)", 1000f / ImGui.GetIO().Framerate, ImGui.GetIO().Framerate));

				ImGui.InputText("Text input", _textBuffer, 100);
			}

			// 2. Show another simple window, this time using an explicit Begin/End pair
			if (show_another_window)
			{
				ImGui.SetNextWindowSize(new System.Numerics.Vector2(200, 100), ImGuiCond.FirstUseEver);
				ImGui.Begin("Another Window", ref show_another_window);
				ImGui.Text("Hello");
				ImGui.End();
			}

			// 3. Show the ImGui test window. Most of the sample code is in ImGui.ShowTestWindow()
			if (show_test_window)
			{
				ImGui.SetNextWindowPos(new System.Numerics.Vector2(650, 20), ImGuiCond.FirstUseEver);
				ImGui.ShowDemoWindow(ref show_test_window);
			}

			DrawLog();

			ImGui.PopFont();

			// Call AfterLayout now to finish up and draw all the things
			ImGuiRenderer.AfterLayout();
		}

		private void DrawReceptors()
		{
			//SpriteBatch.Draw(
			//	TextureReceptor,
			//	new Vector2(0, ReceptorY),
			//	null,
			//	Color.White,
			//	0.1f,//(float)Math.PI * 0.5f,
			//	Vector2.Zero,
			//	1.0f,
			//	SpriteEffects.None,
			//	1.0f);

			SpriteBatch.Draw(
				TextureReceptor,
				new Vector2(128, ReceptorY),
				null,
				Color.White,
				0.0f,
				Vector2.Zero,
				1.0f,
				SpriteEffects.None,
				1.0f);

		}

		private void DrawLog()
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

		private async void LoadSongAsync()
		{
			//SongSound = await SoundManager.Load(@"C:\Games\StepMania 5\Songs\Customs\Acid Wall\Acid Wall.ogg");
			//SongSound = await SoundManager.Load(@"C:\Games\StepMania 5\Songs\Customs\ASYS Live for Shiny People & TechoV\ASYS Live.ogg");

			await SongMipMap.LoadSoundAsync(SoundManager, @"C:\Games\StepMania 5\Songs\Customs\Acid Wall\Acid Wall.ogg");
			await SongMipMap.CreateMipMapAsync(WaveFormTextureWidth);
		}
	}
}

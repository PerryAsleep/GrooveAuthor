using Fumen;
using ImGuiNET;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using Vector2 = Microsoft.Xna.Framework.Vector2;

namespace StepManiaEditor
{
	public class Editor : Game
	{
		private const int DefaultArrowWidth = 128;
		private Vector2 FocalPoint;

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
		private readonly string[] LogWindowDateStrings = { "None", "HH:mm:ss", "HH:mm:ss.fff", "yyyy-MM-dd HH:mm:ss.fff" };
		private readonly int[] LogWindowDateWidths = { 0, 70, 100, 176 };
		private readonly string[] LogWindowLevelStrings = {"Info", "Warn", "Error"};
		private readonly System.Numerics.Vector4[] LogWindowLevelColors = {
			new System.Numerics.Vector4(1.0f, 1.0f, 1.0f, 1.0f),
			new System.Numerics.Vector4(1.0f, 1.0f, 0.0f, 1.0f),
			new System.Numerics.Vector4(1.0f, 0.0f, 0.0f, 1.0f),
		};

		// WaveForm GUI
		private readonly string[] WaveFormWindowSparseColorOptions = {"Darker Dense Color", "Same As Dense Color", "Unique Color"};

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
			SongMipMap = new SoundMipMap(SoundManager);

			Graphics = new GraphicsDeviceManager(this);

			Content.RootDirectory = "Content";
			IsMouseVisible = true;
			Window.AllowUserResizing = true;
			Window.ClientSizeChanged += OnResize;

			IsFixedTimeStep = false;
		}

		protected override void Initialize()
		{
			Graphics.PreferredBackBufferHeight = Preferences.Instance.WindowHeight;
			Graphics.PreferredBackBufferWidth = Preferences.Instance.WindowWidth;
			Graphics.IsFullScreen = Preferences.Instance.WindowFullScreen;
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
			WaveFormRenderer.SetXPerChannelScale(Preferences.Instance.WaveFormMaxXPercentagePerChannel);
			WaveFormRenderer.SetSoundMipMap(SongMipMap);
			WaveFormRenderer.SetFocalPoint(FocalPoint);

			TextureAtlas = new TextureAtlas(GraphicsDevice, 2048, 2048, 1);

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

			TextureAtlas.AddTexture("1_4", Content.Load<Texture2D>("1_4"));
			TextureAtlas.AddTexture("1_8", Content.Load<Texture2D>("1_8"));
			TextureAtlas.AddTexture("receptor", Content.Load<Texture2D>("receptor"));
			
			LoadSongAsync();
			
			base.LoadContent();
		}

		public void OnResize(Object sender, EventArgs e)
		{
			Preferences.Instance.WindowWidth = Graphics.GraphicsDevice.Viewport.Width;
			Preferences.Instance.WindowHeight = Graphics.GraphicsDevice.Viewport.Height;
			Preferences.Instance.WindowFullScreen = Graphics.IsFullScreen;

			FocalPoint.X = Graphics.GraphicsDevice.Viewport.Width >> 1;

			//var form = (Form)Form.FromHandle(Window.Handle);
			//form.WindowState = FormWindowState.Maximized;
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

			// Update WaveFormRenderer
			WaveFormRenderer.SetFocalPoint(FocalPoint);
			WaveFormRenderer.SetXPerChannelScale(Preferences.Instance.WaveFormMaxXPercentagePerChannel);
			WaveFormRenderer.SetColors(
				Preferences.Instance.WaveFormDenseColor.X, Preferences.Instance.WaveFormDenseColor.Y, Preferences.Instance.WaveFormDenseColor.Z,
				Preferences.Instance.WaveFormSparseColor.X, Preferences.Instance.WaveFormSparseColor.Y, Preferences.Instance.WaveFormSparseColor.Z);
			WaveFormRenderer.SetScaleXWhenZooming(Preferences.Instance.WaveFormScaleXWhenZooming);
			WaveFormRenderer.Update(SongTime, Zoom);

			base.Update(gameTime);
		}

		private void ProcessInput(GameTime gameTime)
		{
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
			if (Preferences.Instance.ShowWaveForm)
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
					ImGui.Checkbox("Log", ref Preferences.Instance.ShowLogWindow);
					ImGui.Checkbox("Waveform Controls", ref Preferences.Instance.ShowWaveFormWindow);
					ImGui.Checkbox("ImGui Demo Window", ref ShowImGuiTestWindow);
					ImGui.EndMenu();
				}
				ImGui.EndMainMenuBar();
			}
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
				ImGui.Combo("Level", ref Preferences.Instance.LogWindowLevel, LogWindowLevelStrings, LogWindowLevelStrings.Length);
				ImGui.PopItemWidth(); 
				
				ImGui.SameLine();
				ImGui.PushItemWidth(186);
				ImGui.Combo("Time", ref Preferences.Instance.LogWindowDateDisplay, LogWindowDateStrings, LogWindowDateStrings.Length);
				ImGui.PopItemWidth();
				
				ImGui.SameLine();
				ImGui.Checkbox("Wrap", ref Preferences.Instance.LogWindowLineWrap);

				ImGui.Separator();

				var flags = Preferences.Instance.LogWindowLineWrap ? ImGuiWindowFlags.None : ImGuiWindowFlags.HorizontalScrollbar;
				ImGui.BeginChild("LogMessages", new System.Numerics.Vector2(), false, flags);
				{
					foreach (var message in LogBuffer)
					{
						if ((int) message.Level < Preferences.Instance.LogWindowLevel)
							continue;

						if (Preferences.Instance.LogWindowDateDisplay != 0)
						{
							ImGui.Text(message.Time.ToString(LogWindowDateStrings[Preferences.Instance.LogWindowDateDisplay]));
							ImGui.SameLine();
						}

						if (Preferences.Instance.LogWindowLineWrap)
							ImGui.PushTextWrapPos();
						ImGui.TextColored(LogWindowLevelColors[(int) message.Level], message.Message);
						if (Preferences.Instance.LogWindowLineWrap)
							ImGui.PopTextWrapPos();
					}
				}
				ImGui.EndChild();
			}
			ImGui.End();
		}

		private void DrawWaveFormUI()
		{
			if (!Preferences.Instance.ShowWaveFormWindow)
				return;

			ImGui.SetNextWindowSize(new System.Numerics.Vector2(0, 0), ImGuiCond.FirstUseEver);
			ImGui.Begin("Waveform", ref Preferences.Instance.ShowWaveFormWindow, ImGuiWindowFlags.NoScrollbar);

			ImGui.Checkbox("Show Waveform", ref Preferences.Instance.ShowWaveForm);
			ImGui.Checkbox("Scale X When Zooming", ref Preferences.Instance.WaveFormScaleXWhenZooming);
			ImGui.SliderFloat("X Scale", ref Preferences.Instance.WaveFormMaxXPercentagePerChannel, 0.0f, 1.0f);

			ImGui.ColorEdit3("Dense Color", ref Preferences.Instance.WaveFormDenseColor, ImGuiColorEditFlags.NoAlpha);

			ImGui.Combo("Sparse Color Mode", ref Preferences.Instance.WaveFormWindowSparseColorOption, WaveFormWindowSparseColorOptions, WaveFormWindowSparseColorOptions.Length);
			if (Preferences.Instance.WaveFormWindowSparseColorOption == 0)
			{
				ImGui.SliderFloat("Sparse Color Scale", ref Preferences.Instance.SparseColorScale, 0.0f, 1.0f);
				Preferences.Instance.WaveFormSparseColor.X = Preferences.Instance.WaveFormDenseColor.X * Preferences.Instance.SparseColorScale;
				Preferences.Instance.WaveFormSparseColor.Y = Preferences.Instance.WaveFormDenseColor.Y * Preferences.Instance.SparseColorScale;
				Preferences.Instance.WaveFormSparseColor.Z = Preferences.Instance.WaveFormDenseColor.Z * Preferences.Instance.SparseColorScale;
			}
			else if (Preferences.Instance.WaveFormWindowSparseColorOption == 1)
			{
				Preferences.Instance.WaveFormSparseColor = Preferences.Instance.WaveFormDenseColor;
			}
			else
			{
				ImGui.ColorEdit3("Sparse Color", ref Preferences.Instance.WaveFormSparseColor, ImGuiColorEditFlags.NoAlpha);
			}
			ImGui.End();
		}

		private async void LoadSongAsync()
		{
			var file = @"C:\Games\StepMania 5\Songs\Customs\Acid Wall\Acid Wall.ogg";
			await SongMipMap.LoadSoundAsync(file);
			await SongMipMap.CreateMipMapAsync(WaveFormTextureWidth);
		}
	}
}

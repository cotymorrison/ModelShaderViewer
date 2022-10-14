using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.GamerServices;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;
using System.Text;
using ShaderLibrary;
using System.IO;
using System.Threading;

namespace ModelShaderViewer
{
    /// <summary>
    /// This is the main type for your game
    /// </summary>
    public class ModelViewer : Microsoft.Xna.Framework.Game
    {
		// Device Manager
        public static GraphicsDeviceManager graphics;

		// Camera
		CameraComponent camera;
		public CameraComponent Camera { get { return camera; } }

		// Skybox
		public Skybox skybox;

		// Screen Variables
		Point DefaultWindowLocation = new Point(960, 270);
		Color CustomBlue = new Color(0, 0, 5, 1);
		int DefaultScreenWidth = 1920;
		int DefaultScreenHeight = 1080;

		// Content Variables
        SpriteBatch spriteBatch;
		SpriteFont debugFont;
		Texture2D bridgeSpecular;
		Texture2D bridgeDiffuse;
		Texture2D bridgeBump;
		Model bridge;

		// Keyboard Variables
		KeyboardState prevKeyboardState;
		KeyboardState currentKeyboardState;

		// Shader Control Variables
		Renderer renderer;
		Guid MoonLight;
		Guid UserLight;
		Guid FlashLight;

		// Temporary
		Vector3 bridgePosition = new Vector3(-5, 0, -5);

		// Accessors
		public Vector3 UserLocation { get { return Camera.Position; } }
		public Vector3 FlashLightLocation { get { return Camera.Position - 0.75f*Camera.UpAxis + 0.3f*Camera.RightAxis; } }
		public Vector3 FlashLightDirection { get { return Camera.Look; } }
		public bool Paused { get; set; }
		public bool Debugging { get {return true;} }

		// Debugging Log
		StringBuilder debugLog = new StringBuilder();
		public void AddDebugText(string text)
		{	debugLog.AppendLine(text);	}

		// Deferred Shading
		public RenderTarget2D ActiveRenderTarget;


		/// <summary>
		/// Game Constructor
		/// </summary>
        public ModelViewer()
        {
            graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";

            camera = new CameraComponent(this);
			skybox = new Skybox(this);
			renderer = new Renderer(this);
			Components.Add(renderer);
			Components.Add(camera);
			Components.Add(skybox);

            Window.Title = "Model Shader Viewer";
            IsFixedTimeStep = false;
        }

        /// <summary>
        /// Allows the game to perform any initialization it needs to before starting to run.
        /// This is where it can query for any required services and load any non-graphic
        /// related content.  Calling base.Initialize will enumerate through any components
        /// and initialize them as well.
        /// </summary>
        protected override void Initialize()
        {
			// Set the camera look vector
			Camera.LookAt(bridgePosition);

			// Setup frame buffer
			graphics.SynchronizeWithVerticalRetrace = false;
			graphics.PreferredBackBufferWidth = DefaultScreenWidth;
			graphics.PreferredBackBufferHeight = DefaultScreenHeight;
			graphics.PreferredDepthStencilFormat = DepthFormat.Depth24Stencil8;
			graphics.PreferMultiSampling = true;
			graphics.ApplyChanges();

			// Set the game to unpaused
			Paused = false;
			skybox.Visible = false;

            base.Initialize();

			// Set the active render target
			ActiveRenderTarget = renderer.Scene;
        }

        /// <summary>
        /// LoadContent will be called once per game and is the place to load
        /// all of your content.
        /// </summary>
        protected override void LoadContent()
        {
            // Create a new SpriteBatch, which can be used to draw textures.
            spriteBatch = new SpriteBatch(GraphicsDevice);

			skybox.LoadSkybox("Skyboxes/SunInSpace");
			Model floor = Content.Load<Model>("Floor");
			Model girl = Content.Load<Model>("Girl");
			bridge = Content.Load<Model>("Bridge");
			//customEffect = Content.Load<Effect>("Effects/CustomEffect");
			bridgeSpecular = Content.Load<Texture2D>("Textures/Specular Map");
			Texture2D floorTex = Content.Load<Texture2D>("Textures/Grass");
			//Texture2D floorBump = Content.Load<Texture2D>("Textures/GroundBump");
			bridgeDiffuse = Content.Load<Texture2D>("Textures/Diffuse Map");
			bridgeBump = Content.Load<Texture2D>("Textures/Bump Map");
			debugFont = Content.Load<SpriteFont>("DebugFont");

			Texture2D[][] diffuse = new Texture2D[1][];
			Texture2D[][] specular = new Texture2D[1][];
			Texture2D[][] normal = new Texture2D[1][];

			diffuse[0] = new Texture2D[1] { bridgeDiffuse };
			specular[0] = new Texture2D[1] { bridgeSpecular };
			normal[0] = new Texture2D[1] { bridgeBump };
			
			//int hashcode = Content.Load<Texture2D>("FEMALE").GetHashCode();
			//Texture2D girlBump = Content.Load<Texture2D>("FEMALE_BUMP");
			//Texture2D[][] girlBumpMaps = new Texture2D[girl.Meshes.Count][];
			//for (int i = 0; i < girl.Meshes.Count; i++)
			//{
			//    girlBumpMaps[i] = new Texture2D[girl.Meshes[i].MeshParts.Count];

			//    for (int j = 0; j < girl.Meshes[i].MeshParts.Count; j++)
			//    {
			//        if (((BasicEffect)girl.Meshes[i].MeshParts[j].Effect).TextureEnabled && ((BasicEffect)girl.Meshes[i].MeshParts[j].Effect).Texture.GetHashCode() == hashcode)
			//            girlBumpMaps[i][j] = girlBump;
			//        else
			//            girlBumpMaps[i][j] = null;
			//    }
			//}

			GameModel girlModel = Renderer.CreateGameModel(girl, null, null, null, null, null, null);
			GameModel bridgeModel = Renderer.CreateGameModel(bridge, null, null, null, diffuse, specular, normal);
			GameModel floorModel = Renderer.CreateGameModel(floor, null, null, floorTex, null);

			girlModel.ScaleModel(1.6f);
			bridgeModel.ScaleModel(20);
			floorModel.ScaleModel(60);

			renderer.AddModel(girlModel);
			renderer.AddModel(bridgeModel);
			renderer.AddModel(floorModel);

			/* Backup Code
			 * UserLight = renderer.AddLight(Renderer.CreatePointLight(Vector3.One, UserLocation, 1.0f));
			 * MoonLight = renderer.AddLight(Renderer.CreateDirectionalLight(new Vector3(0.4f, 0.4f, 0.6f), Vector3.Normalize(new Vector3(-1, -3, -1)), 1.0f, 0.01f));
			 * FlashLight = renderer.AddLight(Renderer.CreateSpotLight(Vector3.One, FlashLightLocation, Camera.Look, 1.0f, MathHelper.ToRadians(10), MathHelper.ToRadians(35), 2));
			 */

			UserLight = renderer.AddLight(Renderer.CreatePointLight(Vector3.One, UserLocation, 1.0f));
			MoonLight = renderer.AddLight(Renderer.CreateDirectionalLight(new Vector3(0.4f, 0.4f, 0.6f), Vector3.Normalize(new Vector3(-1, -3, -1)), 5.0f, 0.01f));
			FlashLight = renderer.AddLight(Renderer.CreateSpotLight(Vector3.One, FlashLightLocation, Camera.Look, 1.0f, 10, 35, 2));

			renderer.AddBillboard(new Billboard(GraphicsDevice) { Texture = Content.Load<Texture2D>("Textures/Circle"), Height = 0.25f, Width = 0.25f });

			//shader.Lights[MoonLight].Active = false;
			//shader.Lights[UserLight].Active = false;
			//shader.Lights[FlashLight].Active = false;

			base.LoadContent();

			LoadConfig();
		}

        /// <summary>
        /// UnloadContent will be called once per game and is the place to unload
        /// all content.
        /// </summary>
        protected override void UnloadContent()
        {
            // TODO: Unload any non ContentManager content here
        }

        /// <summary>
        /// Allows the game to run logic such as updating the world,
        /// checking for collisions, gathering input, and playing audio.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Update(GameTime gameTime)
        {
			// Process global keyboard commands
			ProcessKeyboard();

			if (renderer.Lights.ContainsKey(UserLight))
				renderer.Lights[UserLight].Position = UserLocation;

			if (renderer.Lights.ContainsKey(FlashLight))
			{
				renderer.Lights[FlashLight].Position = FlashLightLocation;
				renderer.Lights[FlashLight].Direction = FlashLightDirection;
			}

			// Allow other components to update
			base.Update(gameTime);
        }

		public double maxFPS = 60;
		TimeSpan lastRender = new TimeSpan();

        /// <summary>
        /// This is called when the game should draw itself.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Draw(GameTime gameTime)
		{
			if ((gameTime.TotalGameTime.TotalSeconds - lastRender.TotalSeconds) < (1 / maxFPS)) // 1000 miliseconds in a second.
				return;
			lastRender = gameTime.TotalGameTime;

			// Clear the background
			GraphicsDevice.Clear(CustomBlue);

			// Draw Skybox and other Drawable Game Components
			base.Draw(gameTime);

			// Build debugging text
			StringBuilder displayText = new StringBuilder();
			displayText.AppendLine("FPS: " + (1/gameTime.ElapsedGameTime.TotalSeconds).ToString("0.00"));
			displayText.AppendLine("Game Time: " + gameTime.TotalGameTime.TotalSeconds.ToString("0.00"));
			displayText.AppendLine("Models In View: " + renderer.inViewCount);
			displayText.AppendLine("Start Pos: " + Camera.InitialPosition.ToString());
			displayText.AppendLine("Camera: " + Camera.Position.ToString());
			displayText.AppendLine("Look: " + Camera.Look.ToString());
			displayText.AppendLine("Normals: " + (renderer.NormalMapping ? "on" : "off"));
			displayText.AppendLine("Shadows: " + (renderer.ShadowMapping ? "on" : "off"));
			displayText.AppendLine("Deferred: " + (renderer.DeferredShading ? "on" : "off"));
			displayText.AppendLine("Moonlight: " + (renderer.Lights[MoonLight].Active ? "on" : "off"));
			displayText.AppendLine("Userlight: " + (renderer.Lights[UserLight].Active ? "on" : "off"));
			displayText.AppendLine("Flashlight: " + (renderer.Lights[FlashLight].Active ? "on" : "off"));
			displayText.AppendLine("Mod 1: " + (renderer.One ? "on" : "off"));
			displayText.AppendLine("Mod 2: " + (renderer.Two ? "on" : "off"));
			displayText.AppendLine("Mod 3: " + (renderer.Three ? "on" : "off"));
			displayText.Append(debugLog);

			// Draw debugging text
			Rectangle fullScreen = GraphicsDevice.PresentationParameters.Bounds;
			Rectangle leftHalf = fullScreen;
			leftHalf.Width /= 2;
			Rectangle rightHalf = leftHalf;
			rightHalf.X += rightHalf.Width;

			spriteBatch.Begin();
			if (renderer.DeferredShading)
			//{
			//    spriteBatch.Draw(shader.normalMap, fullScreen, Color.White);
			//    spriteBatch.Draw(renderer.normalMap, leftHalf, Color.White);
			//    spriteBatch.Draw(renderer.scene, rightHalf, Color.White);
			//}
				spriteBatch.Draw(ActiveRenderTarget, fullScreen, Color.White);
			spriteBatch.DrawString(debugFont, displayText, Vector2.Zero, Color.Green);
			spriteBatch.End();
        }

		/// <summary>
		/// Executed when the game is exiting
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="args"></param>
		protected override void OnExiting(object sender, EventArgs args)
		{
			SaveConfig();

			base.OnExiting(sender, args);
		}

		/// <summary>
		/// Save the current state of the program
		/// </summary>
		private void SaveConfig()
		{
			StreamWriter configfile = new StreamWriter(Directory.GetCurrentDirectory() + "config.cfg");

			configfile.WriteLine(renderer.TextureMapping);
			configfile.WriteLine(renderer.NormalMapping);
			configfile.WriteLine(renderer.ShadowMapping);
			configfile.WriteLine(renderer.DeferredShading);
			configfile.WriteLine(renderer.Lights[FlashLight].Active);
			configfile.WriteLine(renderer.Lights[UserLight].Active);
			configfile.WriteLine(renderer.Lights[MoonLight].Active);

			configfile.Close();
		}

		/// <summary>
		/// Load state from config file (this method should mirror SaveConfig)
		/// </summary>
		private void LoadConfig()
		{
			try
			{
				StreamReader configfile = new StreamReader(Directory.GetCurrentDirectory() + "config.cfg");

				renderer.TextureMapping = Convert.ToBoolean(configfile.ReadLine());
				renderer.NormalMapping = Convert.ToBoolean(configfile.ReadLine());
				renderer.ShadowMapping = Convert.ToBoolean(configfile.ReadLine());
				renderer.DeferredShading = Convert.ToBoolean(configfile.ReadLine());
				renderer.Lights[FlashLight].Active = Convert.ToBoolean(configfile.ReadLine());
				renderer.Lights[UserLight].Active = Convert.ToBoolean(configfile.ReadLine());
				renderer.Lights[MoonLight].Active = Convert.ToBoolean(configfile.ReadLine());

				configfile.Close();
			}
			catch (Exception e)
			{
				debugLog.Append(e);
			}
		}


		/// <summary>
		/// Process any global keyboard commands
		/// </summary>
		private void ProcessKeyboard()
		{
			// Update the keyboard states
			prevKeyboardState = currentKeyboardState;
			currentKeyboardState = Keyboard.GetState();

            // Exit
			if (KeyJustPressed(Keys.Escape))
				Exit();

			// Pause
			if (KeyJustPressed(Keys.P))
			{
				Paused = !Paused;
				IsMouseVisible = Paused;
				Mouse.SetPosition(Window.ClientBounds.Width/2, Window.ClientBounds.Height/2);
			}

			// Full-screen
			if (KeyJustPressed(Keys.F11))
			{
				graphics.ToggleFullScreen();
				graphics.ApplyChanges();
			}

			// Mod keys
			//	Visibility
			if (KeyJustPressed(Keys.F1))
				skybox.Visible = !skybox.Visible;

			if (KeyJustPressed(Keys.F2))
				renderer.DefaultLighting = !renderer.DefaultLighting;

			if (KeyJustPressed(Keys.Tab))
				renderer.WireFrame = !renderer.WireFrame;

			//	Texturing/Shading
			if (KeyJustPressed(Keys.T))
				renderer.TextureMapping = !renderer.TextureMapping;

			if (KeyJustPressed(Keys.N))
				renderer.NormalMapping = !renderer.NormalMapping;

			if (KeyJustPressed(Keys.X))
				renderer.ShadowMapping = !renderer.ShadowMapping;

			if (KeyJustPressed(Keys.Z))
				renderer.DeferredShading = !renderer.DeferredShading;

			//	Shader Modifications
			if (KeyJustPressed(Keys.D1))
				renderer.One = !renderer.One;

			if (KeyJustPressed(Keys.D2))
				renderer.Two = !renderer.Two;

			if (KeyJustPressed(Keys.D3))
				renderer.Three = !renderer.Three;

			//	Light control
			if (KeyJustPressed(Keys.F))
				renderer.Lights[FlashLight].Active = !renderer.Lights[FlashLight].Active;

			if (KeyJustPressed(Keys.M))
				renderer.Lights[MoonLight].Active = !renderer.Lights[MoonLight].Active;

			if (KeyJustPressed(Keys.U))
				renderer.Lights[UserLight].Active = !renderer.Lights[UserLight].Active;

			// Render Targets
			if (KeyJustPressed(Keys.F5))
				ActiveRenderTarget = renderer.Scene;

			if (KeyJustPressed(Keys.F6))
				ActiveRenderTarget = renderer.normalMap;

			if (KeyJustPressed(Keys.F7))
				ActiveRenderTarget = renderer.shadowMap;

		}


		/// <summary>
		/// Returns true if the key was just pressed
		/// </summary>
		/// <param name="key"></param>
		/// <returns></returns>
		private bool KeyJustPressed(Keys key)
		{
			return currentKeyboardState.IsKeyDown(key) && !prevKeyboardState.IsKeyDown(key);
		}
    }
}

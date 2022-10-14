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

using float2 = Microsoft.Xna.Framework.Vector2;
using float3 = Microsoft.Xna.Framework.Vector3;
using float4 = Microsoft.Xna.Framework.Vector4;
using float4x4 = Microsoft.Xna.Framework.Matrix;
using System.IO;
using ModelShaderViewer;
using System.Diagnostics;
using System.Threading;

namespace ShaderLibrary
{
	#region Lights
	/// <summary>
	/// Basic Light
	/// </summary>
	public class Light
	{
		public enum LightType { None, Directional, Point, Spot };

		/// <summary>
		/// Gets the light's globally unique identifier
		/// </summary>
		public Guid GUID { get; private set; }
		public LightType Type = LightType.None;
		public Matrix LightView = Matrix.Identity;
		public Matrix LightProj = Matrix.Identity;
		public float3 Direction = Vector3.Zero;
		public float3 Position = Vector3.Zero;
		public float3 Color = new float3(1, 1, 1);
		public float Intensity = 1;
		public int mapChannel = -1;
		public bool OutOfRange = false;
		public bool Active = true;

		// Range
		public float RangeMin = 0;
		public float RangeMax = float.PositiveInfinity;

		// Falloff
		public float a0 = 1;
		public float a1 = 0;
		public float a2 = 0;

		// Spotlight Factor
		public float cosphi;
		public float costheta;
		public float spotfactor;

		public Light()
		{
			GUID = Guid.NewGuid();
		}
	}
	#endregion

	#region Billboards
	public class Billboard
	{
		float width = 1;
		float height = 1;
		Vector3 position = Vector3.Zero;
		VertexBuffer buffer;
		VertexPositionTexture[] vertices = new VertexPositionTexture[]
		{
			new VertexPositionTexture { Position = Vector3.Zero, TextureCoordinate = new Vector2(0, 0) },
			new VertexPositionTexture { Position = Vector3.Zero, TextureCoordinate = new Vector2(1, 0) },
			new VertexPositionTexture { Position = Vector3.Zero, TextureCoordinate = new Vector2(1, 1) },

			new VertexPositionTexture { Position = Vector3.Zero, TextureCoordinate = new Vector2(0, 0) },
			new VertexPositionTexture { Position = Vector3.Zero, TextureCoordinate = new Vector2(1, 1) },
			new VertexPositionTexture { Position = Vector3.Zero, TextureCoordinate = new Vector2(0, 1) },
		};

		public Billboard(GraphicsDevice device)
		{
			GUID = Guid.NewGuid();
			buffer = new VertexBuffer(device, typeof(VertexPositionTexture), vertices.Length, BufferUsage.WriteOnly);
			buffer.SetData(vertices);
		}

		public Guid GUID { get; private set; }
		public float Width { get { return width; } set { width = value; } }
		public float Height { get { return height; } set { height = value; } }
		public Vector3 Position { get { return position; } set { position = value; } }
		public Texture2D Texture { get; set; }
		public VertexBuffer VertexBuffer { get { return buffer; } }
	}
	#endregion

	#region Model Properties
	/// <summary>
	/// Model Animation
	/// </summary>
	public struct Animation
	{

	}

	/// <summary>
	/// Model material used for shading
	/// </summary>
	public struct ModelMaterial
	{
		public float3 DiffuseColor { get; set; }
		public float3 SpecularColor { get; set; }
		public float3 AmbientColor { get; set; }
		public float Smoothness { get; set; }

		/// <summary>
		/// Default Model Material
		/// </summary>
		public static ModelMaterial Default
		{
			get
			{
				ModelMaterial m = new ModelMaterial();
				m.DiffuseColor = float3.One;
				m.SpecularColor = float3.One;
				m.AmbientColor = float3.One;
				m.Smoothness = 1;
				return m;
			}
		}
	}

	/// <summary>
	/// Custom GameModel struct
	/// </summary>
	public class GameModel
	{
		#region Static Methods
		/// <summary>
		/// Returns an array structure with [Mesh][MeshPart] indices of type T
		/// </summary>
		/// <typeparam name="T">The type of array to return</typeparam>
		/// <param name="model">The model to get an array structure from</param>
		/// <returns></returns>
		public static T[][] GetModelLayout<T>(Model model)
		{
			T[][] layout = new T[model.Meshes.Count][];
			for (int i = 0; i < model.Meshes.Count; i++)
				layout[i] = new T[model.Meshes[i].MeshParts.Count];

			return layout;
		}
		#endregion

		#region Fields
		/// <summary>
		/// Model's total bounding sphere
		/// </summary>
		BoundingSphere? boundSphere = null;
		#endregion

		#region Properties
		/// <summary>
		/// Gets the model's globally unique identifier
		/// </summary>
		public Guid GUID { get; set; }
		public Model Model { get; private set; }
		public Matrix World { get; set; }
		public Animation[][] Animation { get; set; }
		public ModelMaterial[][] Material { get; set; }
		public Texture2D[][] TextureMap { get; set; }
		public Texture2D[][] DiffuseMap { get; set; }
		public Texture2D[][] SpecularMap { get; set; }
		public Texture2D[][] NormalMap { get; set; }
		#endregion

		#region Mutators
		/// <summary>
		/// Set all mesh color textures to the same map
		/// </summary>
		/// <param name="tex">The texture for all parts</param>
		public void SetTextureMap(Texture2D tex)
		{ TextureMap = FillLayoutArray(tex); }

		/// <summary>
		/// Set all mesh diffuse textures to the same map
		/// </summary>
		/// <param name="tex">The texture for all parts</param>
		public void SetDiffuseMap(Texture2D tex)
		{ DiffuseMap = FillLayoutArray(tex); }

		/// <summary>
		/// Set all mesh specular textures to the same map
		/// </summary>
		/// <param name="tex">The texture for all parts</param>
		public void SetSpecularMap(Texture2D tex)
		{ SpecularMap = FillLayoutArray(tex); }

		/// <summary>
		/// Set all mesh normal textures to the same map
		/// </summary>
		/// <param name="tex">The texture for all parts</param>
		public void SetNormalMap(Texture2D tex)
		{ NormalMap = FillLayoutArray(tex); }

		/// <summary>
		/// Scales the model uniformaly so the longest dimension is 1
		/// </summary>
		public void NormalizeModel()
		{
			Vector3 size = GetSize();
			Matrix scale = Matrix.CreateScale(1/Math.Max(size.X, Math.Max(size.Y, size.Z)));

			Matrix[] transforms = new Matrix[Model.Bones.Count];
			Model.CopyAbsoluteBoneTransformsTo(transforms);

			for (int i = 0; i < transforms.Length; i++)
				transforms[i] = transforms[i] * scale;

			Model.CopyBoneTransformsFrom(transforms);

			boundSphere = null;
		}

		/// <summary>
		/// Translates the model so its center point is at the origin
		/// </summary>
		public void CenterModel()
		{
			Vector3 center = GetCenter();
			Matrix translation = Matrix.CreateTranslation(-center);

			Matrix[] transforms = new Matrix[Model.Bones.Count];
			Model.CopyAbsoluteBoneTransformsTo(transforms);

			for (int i = 0; i < transforms.Length; i++)
				transforms[i] = transforms[i] * translation;

			Model.CopyBoneTransformsFrom(transforms);

			boundSphere = null;
		}

		/// <summary>
		/// Scale the entire model by the scale factor
		/// </summary>
		/// <param name="scale">Amount to scale</param>
		public void ScaleModel(float scale)
		{
			Matrix scaleMatrix = Matrix.CreateScale(scale);

			Matrix[] transforms = new Matrix[Model.Bones.Count];
			Model.CopyAbsoluteBoneTransformsTo(transforms);

			for (int i = 0; i < transforms.Length; i++)
				transforms[i] = scaleMatrix * transforms[i];

			Model.CopyBoneTransformsFrom(transforms);

			boundSphere = null;
		}
		#endregion

		#region Constructors
		/// <summary>
		/// Model constructor
		/// </summary>
		/// <param name="model">The model to use</param>
		public GameModel(Model model)
		{
			Model = model;
			GUID = Guid.NewGuid();
			World = Matrix.Identity;
			Material = null;
			Animation = null;
			TextureMap = null;
			DiffuseMap = null;
			SpecularMap = null;
			NormalMap = null;
		}
		#endregion

		#region Public Methods
		/// <summary>
		/// Constructs an array with the proper model layout and fills each entry with the specified object
		/// </summary>
		/// <typeparam name="T">The object type</typeparam>
		/// <param name="obj">The object to fill the array with</param>
		/// <returns></returns>
		public T[][] FillLayoutArray<T>(T obj)
		{
			T[][] layout = new T[Model.Meshes.Count][];
			for (int i = 0; i < Model.Meshes.Count; i++)
			{
				layout[i] = new T[Model.Meshes[i].MeshParts.Count];
				for (int j = 0; j < Model.Meshes[i].MeshParts.Count; j++)
					layout[i][j] = obj;
			}

			return layout;
		}

		/// <summary>
		/// Returns an array structure with [Mesh][MeshPart] indices of type T
		/// </summary>
		/// <typeparam name="T">The type of array to return</typeparam>
		/// <returns></returns>
		public T[][] GetModelLayout<T>()
		{
			T[][] layout = new T[Model.Meshes.Count][];
			for (int i = 0; i < Model.Meshes.Count; i++)
				layout[i] = new T[Model.Meshes[i].MeshParts.Count];

			return layout;
		}

		/// <summary>
		/// Get textures imported with the model
		/// </summary>
		public Texture2D[][] GetTexturesFromModel()
		{
			Texture2D[][] textures = new Texture2D[Model.Meshes.Count][];
			for (int i = 0; i < Model.Meshes.Count; i++)
			{
				textures[i] = new Texture2D[Model.Meshes[i].MeshParts.Count];

				for (int j = 0; j < Model.Meshes[i].MeshParts.Count; j++)
				{
					if (((BasicEffect)Model.Meshes[i].MeshParts[j].Effect).TextureEnabled)
						textures[i][j] = ((BasicEffect)Model.Meshes[i].MeshParts[j].Effect).Texture;
					else
						textures[i][j] = null;
				}
			}

			return textures;
		}

		/// <summary>
		/// Returns the max diameter of the model
		/// </summary>
		/// <returns></returns>
		public Vector3 GetSize()
		{
			Vector3 size = new Vector3();
			var points = getPoints();
			size.X = points.Max(i => i.X) - points.Min(i => i.X);
			size.Y = points.Max(i => i.Y) - points.Min(i => i.Y);
			size.Z = points.Max(i => i.Z) - points.Min(i => i.Z);
			return size;
		}

		/// <summary>
		/// Returns the center of the model
		/// </summary>
		/// <returns></returns>
		public Vector3 GetCenter()
		{
			var points = getPoints();

			Vector3 center;
			float minX = points.Min(i => i.X);
			float maxX = points.Max(i => i.X);
			float minY = points.Min(i => i.Y);
			float maxY = points.Max(i => i.Y);
			float minZ = points.Min(i => i.Z);
			float maxZ = points.Max(i => i.Z);
			center.X = (maxX + minX)/2;
			center.Y = (maxY + minY)/2;
			center.Z = (maxZ + minZ)/2;

			return center;
		}

		/// <summary>
		/// Returns the center bottom of the model
		/// </summary>
		/// <returns></returns>
		public Vector3 GetCenterBottom()
		{
			var points = getPoints();

			Vector3 center;
			float minX = points.Min(i => i.X);
			float maxX = points.Max(i => i.X);
			float minY = points.Min(i => i.Y);
			float minZ = points.Min(i => i.Z);
			float maxZ = points.Max(i => i.Z);
			center.X = (maxX + minX)/2;
			center.Y = minY;
			center.Z = (maxZ + minZ)/2;

			return center;
		}
		#endregion

		#region Private Methods
		/// <summary>
		/// Get the collection of points in the model
		/// </summary>
		/// <returns></returns>
		private ICollection<Vector3> getPoints()
		{
			int num_vertices = 0;
			foreach (ModelMesh mesh in Model.Meshes)
				foreach (ModelMeshPart part in mesh.MeshParts)
					num_vertices += part.NumVertices;

			Vector3[] points = new Vector3[num_vertices];
			Matrix[] transforms = new Matrix[Model.Bones.Count];
			Model.CopyAbsoluteBoneTransformsTo(transforms);

			int index = 0;
			for (int i = 0; i < Model.Meshes.Count; i++)
			{
				ModelMesh mesh = Model.Meshes[i];
				Matrix meshWorld = transforms[mesh.ParentBone.Index] * World;

				for (int j = 0; j < mesh.MeshParts.Count; j++)
				{
					ModelMeshPart part = mesh.MeshParts[j];
					VertexPositionTexture[] vertices = new VertexPositionTexture[part.NumVertices];
					part.VertexBuffer.GetData(vertices, 0, part.NumVertices);

					for (int k = 0; k < part.NumVertices; k++)
						points[index++] = (Vector3.Transform(vertices[k].Position, meshWorld));
				}
			}
			Debug.Assert(index == num_vertices);
			return points;
		}
		#endregion

		#region Accessors
		/// <summary>
		/// Gets the model's bounding sphere in world coordinates
		/// </summary>
		public BoundingSphere BoundingSphere
		{
			get
			{
				if (!boundSphere.HasValue)
					boundSphere = BoundingSphere.CreateFromPoints(getPoints());

				return new BoundingSphere(Vector3.Transform(boundSphere.Value.Center, World), boundSphere.Value.Radius);
			}
		}
		#endregion
	}
	#endregion

	/// <summary>
	/// Shader class encompasses all code for rendering a Model
	/// </summary>
	public class Renderer : DrawableGameComponent
	{
		#region Static Methods
		/// <summary>
		/// Create a GameModel from given parameters
		/// </summary>
		/// <param name="model">A Microsoft.Xna.Framework.Graphics.Model</param>
		/// <param name="world">Initial world matrix</param>
		/// <param name="material">Model Material</param>
		/// <param name="animation">Model Animation</param>
		/// <param name="diffMap">Diffuse texture map for the Model</param>
		/// <param name="specMap">Specular texture map for the Model</param>
		/// <param name="normMap">Normal texture map for the Model</param>
		/// <returns>The newly created GameModel</returns>
		public static GameModel CreateGameModel(Model model, Matrix? world, ModelMaterial[][] material, Animation[][] animation, Texture2D[][] diffMap, Texture2D[][] specMap, Texture2D[][] normMap)
		{
			GameModel gameModel = new GameModel(model);
			//Vector3 size = gameModel.GetSize();
			//gameModel.World = Matrix.CreateScale(1/Math.Max(size.X, Math.Max(size.Y, size.Z)));
			gameModel.NormalizeModel();
			gameModel.CenterModel();
			gameModel.World *= (world.HasValue ? world.Value : gameModel.World);
			gameModel.Material = (material != null ? material : gameModel.Material);
			gameModel.Animation = animation;
			gameModel.DiffuseMap = diffMap;
			gameModel.SpecularMap = specMap;
			gameModel.NormalMap = normMap;

			gameModel.TextureMap = gameModel.GetTexturesFromModel();

			return gameModel;
		}

		/// <summary>
		/// Create a GameModel from given parameters
		/// </summary>
		/// <param name="model">A Microsoft.Xna.Framework.Graphics.Model</param>
		/// <param name="world">Initial world matrix</param>
		/// <param name="material">Model Material</param>
		/// <param name="animation">Model Animation</param>
		/// <param name="diffMap">Diffuse texture map for the Model</param>
		/// <param name="specMap">Specular texture map for the Model</param>
		/// <param name="normMap">Normal texture map for the Model</param>
		/// <returns>The newly created GameModel</returns>
		public static GameModel CreateGameModel(Model model, Matrix? world, ModelMaterial[][] material, Animation[][] animation, Texture2D[][] texMap, Texture2D[][] diffMap, Texture2D[][] specMap, Texture2D[][] normMap)
		{
			throw new NotImplementedException();

			GameModel gameModel = new GameModel(model);
			//Vector3 size = gameModel.GetSize();
			//gameModel.World = Matrix.CreateScale(1/Math.Max(size.X, Math.Max(size.Y, size.Z)));
			gameModel.NormalizeModel();
			gameModel.CenterModel();
			gameModel.World *= (world.HasValue ? world.Value : gameModel.World);
			gameModel.Material = (material != null ? material : gameModel.Material);
			gameModel.Animation = animation;
			gameModel.DiffuseMap = diffMap;
			gameModel.SpecularMap = specMap;
			gameModel.NormalMap = normMap;

			gameModel.TextureMap = gameModel.GetTexturesFromModel();

			return gameModel;
		}

		/// <summary>
		/// Create a GameModel from given parameters
		/// </summary>
		/// <param name="model">A Microsoft.Xna.Framework.Graphics.Model</param>
		/// <param name="world">Initial world matrix</param>
		/// <param name="material">Model Material</param>
		/// <param name="animation">Model Animation</param>
		/// <param name="diffMap">Diffuse texture map for the Model</param>
		/// <param name="specMap">Specular texture map for the Model</param>
		/// <param name="normMap">Normal texture map for the Model</param>
		/// <returns>The newly created GameModel</returns>
		public static GameModel CreateGameModel(Model model, Matrix? world, ModelMaterial[][] material, Texture2D texMap, Texture2D normMap)
		{
			GameModel gameModel = new GameModel(model);
			gameModel.NormalizeModel();
			gameModel.CenterModel();
			gameModel.World *= (world.HasValue ? world.Value : Matrix.Identity);
			gameModel.Material = (material != null ? material : gameModel.Material);

			if (texMap != null)
				gameModel.SetTextureMap(texMap);
			else
				gameModel.TextureMap = null;

			if (normMap != null)
				gameModel.SetNormalMap(normMap);
			else
				gameModel.NormalMap = null;

			return gameModel;
		}

		/// <summary>
		/// Create a Directional Light
		/// </summary>
		public static Light CreateDirectionalLight(float3 Color, float3 Direction, float Intensity, float Ambience)
		{
			return new Light
			{
				Type = Light.LightType.Directional,
				Color = Color,
				Direction = Direction,
				Intensity = Intensity,
			};
		}

		/// <summary>
		/// Create a Point Light
		/// </summary>
		public static Light CreatePointLight(float3 Color, float3 Position, float Intensity)
		{
			return new Light
			{
				Type = Light.LightType.Point,
				Color = Color,
				Position = Position,
				Intensity = Intensity,
			};
		}

		/// <summary>
		/// Create a Spot Light
		/// </summary>
		public static Light CreateSpotLight(float3 Color, float3 Position, float3 Direction, float Intensity, float Penumbra, float Umbra, int Exponent)
		{
			return new Light 
			{ 
				Type = Light.LightType.Spot,
				Color = Color,
				Position = Position,
				Direction = Direction,
				Intensity = Intensity,
				cosphi = (float)Math.Cos(MathHelper.ToRadians(Penumbra)),
				costheta = (float)Math.Cos(MathHelper.ToRadians(Umbra)),
				spotfactor = Exponent,
			};
		}
		#endregion

		#region Fields
		public const int MAXLIGHTS = 3;
		//public Color BackgroundColor { get { return new Color(0, 0, 5); } }
		public Color BackgroundColor { get { return new Color(255, 255, 255); } }
		Color FogColor { get { return new Color(0, 20, 5); } }
		RasterizerState wire = new RasterizerState { FillMode = FillMode.WireFrame };
		RasterizerState solid = new RasterizerState { FillMode = FillMode.Solid };
		RasterizerState alias = new RasterizerState { MultiSampleAntiAlias = false };

		// Effects
		Effect effect = null;
		Effect motionBlur = null;
		Effect billboardEffect = null;

		// Game Objects
		Dictionary<Guid, Light> lights = new Dictionary<Guid, Light>();
		Dictionary<Guid, GameModel> models = new Dictionary<Guid, GameModel>();
		Dictionary<Guid, Billboard> billboards = new Dictionary<Guid, Billboard>();

		// Deferred Shading
		public const int NUM_BLUR_FRAMES = 8;
		public int numFrames = 0;
		public int currentFrame = 0;
		public RenderTarget2D[] blurStartImage = new RenderTarget2D[NUM_BLUR_FRAMES];
		public RenderTarget2D[] accumulationBuffer = new RenderTarget2D[2];
		public RenderTarget2D shadowMap;
		public RenderTarget2D normalMap;
		public RenderTarget2D scene;
		public Texture2D blank;
		int shadowMapChannels = 0;

		// Scene Variables
		Matrix View = Matrix.Identity;
		Matrix Proj = Matrix.Identity;
		ICollection<Guid> inView = new List<Guid>();
		BoundingSphere sceneSphere = new BoundingSphere();
		Light DefaultLight = new Light { Type = Light.LightType.Directional, Color = Color.White.ToVector3(), Active = false };
		#endregion

		#region Constructors
		/// <summary>
		/// Default Constructor
		/// </summary>
		public Renderer(Game game)
			: base(game)
		{
			DrawOrder = 2;
			UpdateOrder = 2;
			One = false;
			Two = false;
			Three = false;
			NormalMapping = true;
			TextureMapping = false;
			ShadowMapping = true;
			DeferredShading = true;
		}
		#endregion

		#region Initialize
		/// <summary>
		/// Allows the game component to perform any initialization it needs to before starting
		/// to run.  This is where it can query for any required services and load content.
		/// </summary>
		public override void Initialize()
		{
			// Initialize the graphics device
			base.Initialize();

			//int width = GraphicsDevice.PresentationParameters.BackBufferWidth;
			//int height = GraphicsDevice.PresentationParameters.BackBufferHeight;
			int width = GraphicsDevice.PresentationParameters.BackBufferWidth;
			int height = GraphicsDevice.PresentationParameters.BackBufferHeight;

			normalMap = new RenderTarget2D(GraphicsDevice, width, height, false, SurfaceFormat.Rgba64, DepthFormat.Depth24Stencil8, GraphicsDevice.PresentationParameters.MultiSampleCount, RenderTargetUsage.DiscardContents);
			shadowMap = new RenderTarget2D(GraphicsDevice, width, height, false, SurfaceFormat.Rgba64, DepthFormat.Depth24Stencil8, GraphicsDevice.PresentationParameters.MultiSampleCount, RenderTargetUsage.DiscardContents);
			scene = new RenderTarget2D(GraphicsDevice, width, height, false, SurfaceFormat.Rgba64, DepthFormat.Depth24Stencil8, GraphicsDevice.PresentationParameters.MultiSampleCount, RenderTargetUsage.DiscardContents);

			for (int i = 0; i < NUM_BLUR_FRAMES; i++)
				blurStartImage[i] = new RenderTarget2D(GraphicsDevice, width, height, false, SurfaceFormat.Rgba64, DepthFormat.Depth24Stencil8, GraphicsDevice.PresentationParameters.MultiSampleCount, RenderTargetUsage.PreserveContents);

			for (int i = 0; i < 2; i++)
				accumulationBuffer[i] = new RenderTarget2D(GraphicsDevice, width, height, false, SurfaceFormat.Rgba64, DepthFormat.Depth24Stencil8, GraphicsDevice.PresentationParameters.MultiSampleCount, RenderTargetUsage.PreserveContents);
		}
		#endregion

		#region LoadContent
		/// <summary>
		/// Load the Shader content
		/// </summary>
		/// <param name="Content"></param>
		protected override void LoadContent()
		{
			effect = Game.Content.Load<Effect>("Effects/Effect2");
			motionBlur = Game.Content.Load<Effect>("Effects/MotionBlur");
			billboardEffect = Game.Content.Load<Effect>("Effects/Billboard");

			blank = Game.Content.Load<Texture2D>("Textures/Blank");

			foreach (GameModel gameModel in models.Values)
				foreach (ModelMesh mesh in gameModel.Model.Meshes)
					foreach (ModelMeshPart part in mesh.MeshParts)
						part.Effect = effect.Clone();
		}
		#endregion

		#region Add/Remove Components
		/// <summary>
		/// Add a model to the shader
		/// </summary>
		/// <param name="gameModel">The GameModel to add</param>
		/// <returns>A Guid for referencing the model</returns>
		public Guid AddModel(GameModel gameModel)
		{
			models.Add(gameModel.GUID, gameModel);

			if (effect != null)
				foreach (ModelMesh mesh in gameModel.Model.Meshes)
					foreach (ModelMeshPart part in mesh.MeshParts)
						part.Effect = effect.Clone();

			return gameModel.GUID;
		}

		/// <summary>
		/// Add a light to the shader
		/// </summary>
		/// <param name="light"></param>
		public Guid AddLight(Light light)
		{
			lights.Add(light.GUID, light);

			if (lights.Count(i => i.Value.GetType() == light.GetType()) > MAXLIGHTS)
				throw new Exception("Too many lights");

			return light.GUID;
		}

		/// <summary>
		/// Add a billboard to the shader
		/// </summary>
		/// <param name="light"></param>
		public Guid AddBillboard(Billboard board)
		{
			billboards.Add(board.GUID, board);
			return board.GUID;
		}


		/// <summary>
		/// Removes a Model from the collection
		/// </summary>
		/// <param name="guid">Model's Guid</param>
		public void RemoveModel(Guid guid)
		{
			models.Remove(guid);
		}

		/// <summary>
		/// Removes a Light from the collection
		/// </summary>
		/// <param name="guid">Light's Guid</param>
		public void RemoveLight(Guid guid)
		{
			lights.Remove(guid);
		}

		/// <summary>
		/// Removes a billboard from the shader
		/// </summary>
		/// <param name="light"></param>
		public void RemoveBillboard(Billboard board)
		{
			billboards.Remove(board.GUID);
		}
		#endregion

		#region Update
		/// <summary>
		/// Allows the game component to update itself.
		/// </summary>
		/// <param name="gameTime">Provides a snapshot of timing values.</param>
		public override void Update(GameTime gameTime)
		{
			inView = GetModelsInView(Camera.ViewMatrix, Camera.ProjectionMatrix);
			sceneSphere = GetBoundingSphere(inView);

			float Zfar = Math.Min(Vector3.Distance(sceneSphere.Center, Camera.Position) + sceneSphere.Radius, CameraComponent.ZFAR);
			View = Camera.ViewMatrix;
			Proj = Matrix.CreatePerspectiveFieldOfView(CameraComponent.FOV_RADIANS, GraphicsDevice.DisplayMode.AspectRatio, CameraComponent.ZNEAR, Zfar);

			if (ShadowMapping && inView.Count > 0)
			{
				foreach (Light l in lights.Values)
				{
					if (l.Type == Light.LightType.Spot)
					{
						float near = Math.Max(Vector3.Distance(sceneSphere.Center, l.Position) - sceneSphere.Radius, CameraComponent.ZNEAR);
						float far = Math.Min(Vector3.Distance(sceneSphere.Center, l.Position) + sceneSphere.Radius, l.RangeMax);
						if (near > far)
							l.OutOfRange = true;
						else
						{
							//float width = 2*near*(float)Math.Tan(spotlight.Umbra);
							//float height = 2*near*(float)Math.Tan(spotlight.Umbra);
							float width = 2*near*((float)Math.PI - l.costheta)/l.costheta;
							float height = 2*near*((float)Math.PI - l.costheta)/l.costheta;
							l.LightView = Matrix.CreateLookAt(l.Position, l.Position + l.Direction, Camera.Up);
							l.LightProj = Matrix.CreatePerspective(width, height, near, far);
							l.OutOfRange = false;
						}
					}
					else if (l.Type == Light.LightType.Directional)
					{
						float near = CameraComponent.ZNEAR;
						float far = Math.Max(CameraComponent.ZNEAR + 2*sceneSphere.Radius, CameraComponent.ZFAR);
						float width = 2*sceneSphere.Radius;
						float height = 2*sceneSphere.Radius;
						l.LightView = Matrix.CreateLookAt(sceneSphere.Center - 2*l.Direction*(sceneSphere.Radius + near), sceneSphere.Center, Vector3.Up);
						l.LightProj = Matrix.CreateOrthographic(width, height, near, far);
					}
					else if (l.Type == Light.LightType.Point)
					{
						//PointLight pointlight = (PointLight)l;
						//float near = Math.Max(Vector3.Distance(sceneSphere.Center, pointlight.Position) - sceneSphere.Radius, CameraComponent.ZNEAR_PLANE);
						//float far = Math.Min(Vector3.Distance(sceneSphere.Center, pointlight.Position) + sceneSphere.Radius, CameraComponent.ZFAR_PLANE);
						//float width = 2 * near * (float)Math.Tan(pointlight.Umbra);
						//float height = 2 * near * (float)Math.Tan(pointlight.Umbra);
						//l.lightView = Matrix.CreateLookAt(pointlight.Position, pointlight.Position + pointlight.Direction, ((ModelViewer)Game).Camera.Up);
						//l.lightProj = Matrix.CreatePerspective(width, height, near, far);
					}
				}
			}


			base.Update(gameTime);
		}
		#endregion

		#region Rendering

		public double maxFPS = 60;
		TimeSpan lastRender = new TimeSpan();
		/// <summary>
		/// Draws the scene.
		/// </summary>
		/// <param name="gameTime">Provides a snapshot of timing values.</param>
		public override void Draw(GameTime gameTime)
		{
			if ((gameTime.TotalGameTime.TotalSeconds - lastRender.TotalSeconds) < (1 / maxFPS)) // 1000 miliseconds in a second.
				return;
			lastRender = gameTime.TotalGameTime;

			//if (gameTime.ElapsedGameTime.TotalSeconds > 1/maxFPS)
			//    return;
			    //Thread.Sleep((int)((1/maxFPS - gameTime.ElapsedGameTime.TotalSeconds)*1000));

			//GraphicsDevice.Clear(ClearOptions.DepthBuffer | ClearOptions.Stencil, Color.White, CameraComponent.ZFAR_PLANE, 0);

			// Generate shadow map
			if (ShadowMapping)
			{
				GraphicsDevice.RasterizerState = RasterizerState.CullNone;
				GenerateShadowMaps(inView);
				GraphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
			}

			GraphicsDevice.Clear(ClearOptions.DepthBuffer | ClearOptions.Stencil | ClearOptions.Target, BackgroundColor, 1, 0);

			// Normal map deferred shading
			//if (NormalMapping)
			//GraphicsDevice.RasterizerState = new RasterizerState { MultiSampleAntiAlias = false };
			RenderNormalMap(inView, normalMap);

			if (WireFrame)
				GraphicsDevice.RasterizerState = wire;
			else
				GraphicsDevice.RasterizerState = solid;

			GraphicsDevice.SetRenderTarget(blurStartImage[currentFrame]);
			//if (DeferredShading)
				//GraphicsDevice.SetRenderTarget(blurStartImage[currentFrame]);
			//else
				//GraphicsDevice.SetRenderTarget(null);

			//GraphicsDevice.Clear(ClearOptions.DepthBuffer | ClearOptions.Stencil | ClearOptions.Target, BackgroundColor, 1, 0);

			// Render the scene with ambient lighting
			//RenderSceneAmbientLighting(inView);

			GraphicsDevice.Clear(ClearOptions.DepthBuffer | ClearOptions.Stencil | ClearOptions.Target, BackgroundColor, 1, 0);

			// Render full scene
			RenderScene(inView);

			// Render billboards
			//RenderBillboards();

			//GraphicsDevice.SetRenderTarget(null);

			//if (DeferredShading)
				MotionBlur();

			GraphicsDevice.SetRenderTarget(null);
		}

		private void MotionBlur()
		{
			//RenderTargetBinding[] temp = GraphicsDevice.GetRenderTargets();

			//if (DeferredShading)
				GraphicsDevice.SetRenderTarget(accumulationBuffer[1]);
			//else
			//	GraphicsDevice.SetRenderTarget(null);
			GraphicsDevice.Clear(Color.White);

			// Set Effect Params
			if (numFrames < NUM_BLUR_FRAMES)
			{
				motionBlur.CurrentTechnique = motionBlur.Techniques[0];
				numFrames += 1;
			}
			else
				motionBlur.CurrentTechnique = motionBlur.Techniques[1];

			motionBlur.Parameters["NewScene"].SetValue(blurStartImage[currentFrame]);
			motionBlur.Parameters["AccumulationBuffer"].SetValue(accumulationBuffer[0]);
			motionBlur.Parameters["OldScene"].SetValue(blurStartImage[(currentFrame+1) % blurStartImage.Length]);

			SpriteBatch sb = new SpriteBatch(GraphicsDevice);
			sb.Begin(SpriteSortMode.Immediate, BlendState.Opaque, SamplerState.PointClamp, DepthStencilState.Default, RasterizerState.CullCounterClockwise, motionBlur);
			//motionBlur.CurrentTechnique.Passes[0].Apply();
			sb.Draw(blank, Vector2.Zero, Color.White);
			sb.End();

			// Increase Queue Position
			currentFrame = (currentFrame + 1) % NUM_BLUR_FRAMES;

			Swap(accumulationBuffer[0], accumulationBuffer[1], out accumulationBuffer[0], out accumulationBuffer[1]);
			//RenderTarget2D temp = accumulationBuffer[0];
			//accumulationBuffer[0] = accumulationBuffer[1];
			//accumulationBuffer[1] = accumulationBuffer[0];
		}


		private void Swap<T>(T a, T b, out T c, out T d)
		{
			c = b;
			d = a;
		}


		public void RenderBillboards()
		{
			//GraphicsDevice.BlendState = new BlendState { AlphaSourceBlend = Blend.SourceAlpha, AlphaDestinationBlend = Blend.InverseSourceAlpha };
			GraphicsDevice.BlendState = BlendState.Opaque;

			billboardEffect.Parameters["View"].SetValue(View);
			billboardEffect.Parameters["Projection"].SetValue(Proj);
			billboardEffect.Parameters["CamPos"].SetValue(Camera.Position);
			billboardEffect.Parameters["AllowedRotDir"].SetValue(Camera.Up);

			foreach (Billboard board in billboards.Values)
			{
				Matrix world = Matrix.CreateTranslation(board.Position);
				billboardEffect.Parameters["World"].SetValue(world);
				billboardEffect.Parameters["BillboardTexture"].SetValue(board.Texture);
				billboardEffect.Parameters["HalfWidth"].SetValue(board.Width/2);
				billboardEffect.Parameters["HalfHeight"].SetValue(board.Height/2);

				GraphicsDevice.SetVertexBuffer(board.VertexBuffer);

				foreach (EffectPass pass in billboardEffect.CurrentTechnique.Passes)
				{
					pass.Apply();
					GraphicsDevice.DrawPrimitives(PrimitiveType.TriangleList, 0, 6);
				}
			}
		}

		/// <summary>
		/// Precompute normals
		/// </summary>
		/// <param name="model_ids"></param>
		private void RenderNormalMap(ICollection<Guid> model_ids, RenderTarget2D renderTarget)
		{
			GraphicsDevice.SetRenderTarget(renderTarget);

			if (model_ids.Count == 0)
				return;

			GraphicsDevice.BlendState = new BlendState { AlphaDestinationBlend = Blend.SourceAlpha, ColorDestinationBlend = Blend.SourceColor };
			GraphicsDevice.DepthStencilState = DepthStencilState.Default;

			foreach (Guid id in model_ids)
			{
				GameModel gameModel = models[id];
				Matrix[] transforms = new Matrix[gameModel.Model.Bones.Count];
				gameModel.Model.CopyAbsoluteBoneTransformsTo(transforms);

				for (int i = 0; i < gameModel.Model.Meshes.Count; i++)
				{
					ModelMesh mesh = gameModel.Model.Meshes[i];
					Matrix meshWorld = transforms[mesh.ParentBone.Index] * gameModel.World;

					for (int j = 0; j < mesh.MeshParts.Count; j++)
					{
						ModelMeshPart part = mesh.MeshParts[j];
						part.Effect.CurrentTechnique = part.Effect.Techniques["NormalMapping"];

						part.Effect.Parameters["World"].SetValue(meshWorld);
						part.Effect.Parameters["ViewProj"].SetValue(View*Proj);
						part.Effect.Parameters["WorldViewProj"].SetValue(meshWorld*View*Proj);
						part.Effect.Parameters["WorldInverseTranspose"].SetValue(Matrix.Invert(Matrix.Transpose(meshWorld)));

						part.Effect.Parameters["pixel_width"].SetValue(1f/GraphicsDevice.PresentationParameters.BackBufferWidth);
						part.Effect.Parameters["pixel_height"].SetValue(1f/GraphicsDevice.PresentationParameters.BackBufferHeight);

						part.Effect.Parameters["normal_mapping"].SetValue(NormalMapping);

						// Set normal map (if available)
						if (NormalMapping && gameModel.NormalMap != null && gameModel.NormalMap[i][j] != null)
						{
							part.Effect.Parameters["NormalMap"].SetValue(gameModel.NormalMap[i][j]);
							part.Effect.Parameters["normal_mapped"].SetValue(true);
						}
						else
							part.Effect.Parameters["normal_mapped"].SetValue(false);
					}

					mesh.Draw();
				}
			}

			GraphicsDevice.SetRenderTarget(null);
		}

		/// <summary>
		/// Render scene with only ambient lighting
		/// </summary>
		/// <param name="model_ids"></param>
		private void RenderSceneAmbientLighting(ICollection<Guid> model_ids)
		{
			throw new NotImplementedException();

			if (model_ids.Count == 0)
				return;

			GraphicsDevice.BlendState = BlendState.Opaque;
			GraphicsDevice.DepthStencilState = DepthStencilState.Default;

			foreach (Guid id in model_ids)
			{
				GameModel gameModel = models[id];
				Matrix[] transforms = new Matrix[gameModel.Model.Bones.Count];
				gameModel.Model.CopyAbsoluteBoneTransformsTo(transforms);

				for (int i = 0; i < gameModel.Model.Meshes.Count; i++)
				{
					ModelMesh mesh = gameModel.Model.Meshes[i];
					Matrix meshWorld = transforms[mesh.ParentBone.Index] * gameModel.World;

					for (int j = 0; j < mesh.MeshParts.Count; j++)
					{
						ModelMeshPart part = mesh.MeshParts[j];
						part.Effect.CurrentTechnique = part.Effect.Techniques["AmbientShading"];

						// Set lights
						int directionalCount = 0;
						foreach (Light l in lights.Values.Where(x => x.Type == Light.LightType.Directional && x.Active))
						{
							part.Effect.Parameters["directional"].Elements[directionalCount].StructureMembers["Color"].SetValue(l.Color);
							//part.Effect.Parameters["directional"].Elements[directionalCount].StructureMembers["Ambience"].SetValue(((DirectionalLight)l).Ambience);
							directionalCount += 1;
						}
						part.Effect.Parameters["directionalCount"].SetValue(directionalCount);

						part.Effect.Parameters["World"].SetValue(meshWorld);
						part.Effect.Parameters["WorldViewProj"].SetValue(meshWorld*View*Proj);
						part.Effect.Parameters["WorldInverseTranspose"].SetValue(Matrix.Invert(Matrix.Transpose(meshWorld)));

						// Set texture map (if available)
						if (TextureMapping && gameModel.TextureMap != null && gameModel.TextureMap[i][j] != null)
						{
							part.Effect.Parameters["TextureMap"].SetValue(gameModel.TextureMap[i][j]);
							part.Effect.Parameters["texture_mapped"].SetValue(true);
						}
						else
							part.Effect.Parameters["texture_mapped"].SetValue(false);
					}

					mesh.Draw();
				}
			}
		}

		private void GenerateShadowMaps(ICollection<Guid> model_ids)
		{
			if (model_ids.Count == 0)
				return;

			// Save original states
			BlendState originalBlendState = GraphicsDevice.BlendState;
			DepthStencilState originalDepthStencilState = GraphicsDevice.DepthStencilState;

			BlendState shadowBlend = new BlendState();
			shadowBlend.AlphaSourceBlend = Blend.One;
			shadowBlend.AlphaDestinationBlend = Blend.One;
			shadowBlend.AlphaBlendFunction = BlendFunction.Max;
			shadowBlend.ColorSourceBlend = Blend.One;
			shadowBlend.ColorDestinationBlend = Blend.One;
			shadowBlend.ColorBlendFunction = BlendFunction.Max;

			// Set graphics states
			GraphicsDevice.BlendState = shadowBlend;
			GraphicsDevice.DepthStencilState = DepthStencilState.Default;
			GraphicsDevice.SetRenderTarget(shadowMap);
			GraphicsDevice.Clear(Color.Black);

			shadowMapChannels = 0;

			// Render shadow maps for each active spotlight
			foreach (Light l in lights.Values.Where(i => (i.Type == Light.LightType.Spot || i.Type == Light.LightType.Directional) && i.Active))
			{
				GraphicsDevice.Clear(ClearOptions.DepthBuffer | ClearOptions.Stencil, Color.Black, 1, 0);

				if (shadowMapChannels > 2)
					break;

				l.mapChannel = shadowMapChannels;

				foreach (Guid id in model_ids)
				{
					GameModel gameModel = models[id];
					Matrix[] transforms = new Matrix[gameModel.Model.Bones.Count];
					gameModel.Model.CopyAbsoluteBoneTransformsTo(transforms);

					foreach (ModelMesh mesh in gameModel.Model.Meshes)
					{
						Matrix meshWorld = transforms[mesh.ParentBone.Index] * gameModel.World;

						foreach (ModelMeshPart part in mesh.MeshParts)
						{
							part.Effect.CurrentTechnique = part.Effect.Techniques["ShadowMapShading"];
							part.Effect.Parameters["World"].SetValue(meshWorld);
							part.Effect.Parameters["WorldViewProj"].SetValue(meshWorld * l.LightView * l.LightProj);
							part.Effect.Parameters["LightViewProj"].Elements[shadowMapChannels].SetValue(l.LightView * l.LightProj);
							part.Effect.Parameters["shadow_map_channels"].SetValue(shadowMapChannels);
						}

						mesh.Draw();
					}
				}

				shadowMapChannels += 1;
			}

			// Reset states
			GraphicsDevice.SetRenderTarget(null);
			if (originalBlendState != null)
				GraphicsDevice.BlendState = originalBlendState;
			if (originalDepthStencilState != null)
				GraphicsDevice.DepthStencilState = originalDepthStencilState;
		}

		/// <summary>
		/// Renders the full scene
		/// </summary>
		/// <param name="model_ids"></param>
		private void RenderScene(ICollection<Guid> model_ids)
		{
			if (model_ids.Count == 0)
				return;

			BlendState shadowBlend = new BlendState
			{
				AlphaSourceBlend = Blend.One,
				AlphaDestinationBlend = Blend.One,
				AlphaBlendFunction = BlendFunction.Add,
				ColorSourceBlend = Blend.One,
				ColorDestinationBlend = Blend.One,
				ColorBlendFunction = BlendFunction.Add
			};

			GraphicsDevice.BlendState = BlendState.Additive;
			GraphicsDevice.DepthStencilState = DepthStencilState.Default;

			// Render the scene a final time
			foreach (Guid id in model_ids)
			{
				GameModel gameModel = models[id];
				Matrix[] transforms = new Matrix[gameModel.Model.Bones.Count];
				gameModel.Model.CopyAbsoluteBoneTransformsTo(transforms);

				for (int i = 0; i < gameModel.Model.Meshes.Count; i++)
				{
					ModelMesh mesh = gameModel.Model.Meshes[i];
					Matrix meshWorld = transforms[mesh.ParentBone.Index] * gameModel.World;

					for (int j = 0; j < mesh.MeshParts.Count; j++)
					{
						ModelMeshPart part = mesh.MeshParts[j];

						// Set current technique
						part.Effect.CurrentTechnique = part.Effect.Techniques["MultiPassShading"];

						// Set world parameters
						part.Effect.Parameters["World"].SetValue(meshWorld);
						part.Effect.Parameters["ViewProj"].SetValue(View*Proj);
						part.Effect.Parameters["WorldViewProj"].SetValue(meshWorld*View*Proj);
						part.Effect.Parameters["WorldInverseTranspose"].SetValue(Matrix.Invert(Matrix.Transpose(meshWorld)));

						// Set pixel width/height
						part.Effect.Parameters["pixel_width"].SetValue(1f/GraphicsDevice.PresentationParameters.BackBufferWidth);
						part.Effect.Parameters["pixel_height"].SetValue(1f/GraphicsDevice.PresentationParameters.BackBufferHeight);

						// Set mods
						part.Effect.Parameters["shadow_mapping"].SetValue(ShadowMapping);
						part.Effect.Parameters["normal_mapping"].SetValue(NormalMapping);
						part.Effect.Parameters["one"].SetValue(One);
						part.Effect.Parameters["two"].SetValue(Two);
						part.Effect.Parameters["three"].SetValue(Three);

						// Set lights
						int lightCount = 0;
						foreach (Light l in lights.Values)
						{
							if (l.Active && !l.OutOfRange)
							{
								part.Effect.Parameters["lights"].Elements[lightCount].StructureMembers["Type"].SetValue((int)l.Type);
								part.Effect.Parameters["lights"].Elements[lightCount].StructureMembers["Color"].SetValue(l.Color);
								part.Effect.Parameters["lights"].Elements[lightCount].StructureMembers["Position"].SetValue(l.Position);
								part.Effect.Parameters["lights"].Elements[lightCount].StructureMembers["Direction"].SetValue(l.Direction);
								part.Effect.Parameters["lights"].Elements[lightCount].StructureMembers["Intensity"].SetValue(l.Intensity);
								part.Effect.Parameters["lights"].Elements[lightCount].StructureMembers["a0"].SetValue(l.a0);
								part.Effect.Parameters["lights"].Elements[lightCount].StructureMembers["a1"].SetValue(l.a1);
								part.Effect.Parameters["lights"].Elements[lightCount].StructureMembers["a2"].SetValue(l.a2);
								part.Effect.Parameters["lights"].Elements[lightCount].StructureMembers["cosphi"].SetValue(l.cosphi);
								part.Effect.Parameters["lights"].Elements[lightCount].StructureMembers["costheta"].SetValue(l.costheta);
								part.Effect.Parameters["lights"].Elements[lightCount].StructureMembers["spotfactor"].SetValue(l.spotfactor);
								lightCount += 1;

								//// Set the shadow map texture
								//if (ShadowMapping)
								//    part.Effect.Parameters["LightViewProj"].Elements[l.mapChannel].SetValue(l.LightView*l.LightProj);
								//	  part.Effect.Parameters["lights"].Elements[lightCount].StructureMembers["ShadowChannel"].SetValue(l.mapChannel);
							}
						}

						part.Effect.Parameters["lightCount"].SetValue(lightCount);

						if (ShadowMapping)
						{
							part.Effect.Parameters["ShadowMap"].SetValue(shadowMap);
							part.Effect.Parameters["shadow_map_channels"].SetValue(shadowMapChannels);
						}

						// Set camera position/look
						part.Effect.Parameters["camPos"].SetValue(Camera.Position);
						part.Effect.Parameters["camLook"].SetValue(Camera.Look);

						// Set texture map (if available)
						if (TextureMapping && gameModel.TextureMap != null && gameModel.TextureMap[i][j] != null)
						{
							part.Effect.Parameters["TextureMap"].SetValue(gameModel.TextureMap[i][j]);
							part.Effect.Parameters["texture_mapped"].SetValue(true);
						}
						else
							part.Effect.Parameters["texture_mapped"].SetValue(false);

						// Set diffuse map (if available)
						if (TextureMapping && gameModel.DiffuseMap != null && gameModel.DiffuseMap[i][j] != null)
						{
							part.Effect.Parameters["DiffuseMap"].SetValue(gameModel.DiffuseMap[i][j]);
							part.Effect.Parameters["diffuse_mapped"].SetValue(true);
						}
						else
							part.Effect.Parameters["diffuse_mapped"].SetValue(false);

						// Set specular map (if available)
						if (TextureMapping && gameModel.SpecularMap != null && gameModel.SpecularMap[i][j] != null)
						{
							part.Effect.Parameters["SpecularMap"].SetValue(gameModel.SpecularMap[i][j]);
							part.Effect.Parameters["specular_mapped"].SetValue(true);
						}
						else
							part.Effect.Parameters["specular_mapped"].SetValue(false);

						// Set normal map (if available)
						if (NormalMapping && gameModel.NormalMap != null && gameModel.NormalMap[i][j] != null)
						{
							//part.Effect.Parameters["NormalMap"].SetValue(normalMap);
							part.Effect.Parameters["NormalMap"].SetValue(gameModel.NormalMap[i][j]);
							part.Effect.Parameters["normal_mapped"].SetValue(true);
						}
						else
							part.Effect.Parameters["normal_mapped"].SetValue(false);

						//// Set normal map (if available)
						//if (NormalMapping && gameModel.NormalMap != null && gameModel.NormalMap.Length > i)
						//{
						//    part.Effect.Parameters["NormalMap"].SetValue(gameModel.NormalMap[i][j]);
						//    part.Effect.Parameters["normal_mapped"].SetValue(true);
						//}
						//else
						//    part.Effect.Parameters["normal_mapped"].SetValue(false);

						// Set material parameters
						ModelMaterial material;
						if (gameModel.Material != null && gameModel.Material.Length > i && gameModel.Material[i].Length > j)
							material = gameModel.Material[i][j];
						else
							material = ModelMaterial.Default;

						part.Effect.Parameters["Kd"].SetValue(material.DiffuseColor/(float)Math.PI);
						part.Effect.Parameters["Ks"].SetValue((material.Smoothness+8)/(float)(8*Math.PI) * material.SpecularColor);
						part.Effect.Parameters["m"].SetValue(material.Smoothness);
					}

					mesh.Draw();
				}
			}
		}
		#endregion

		#region Shadow Volumes
		/// <summary>
		/// Compute Shadow Volumes for each mesh 
		/// </summary>
		private void GenerateShadowVolumes(ICollection<Guid> model_ids, out IndexBuffer lightIndexBuffer, out VertexBuffer lightVertexBuffer, out IndexBuffer shadowIndexBuffer, out VertexBuffer shadowVertexBuffer)
		{
			float Infinity = 2 * CameraComponent.ZFAR;
			List<short> inLightIndices = new List<short>();
			List<short> inShadowIndices = new List<short>();
			List<VertexPositionColor> inLightVertices = new List<VertexPositionColor>();
			List<VertexPositionColor> inShadowVertices = new List<VertexPositionColor>();

			foreach (Light light in lights.Values)
			{
				if (light.Active && light.Type == Light.LightType.Point)
				{
					VertexPositionColor lightVertex = new VertexPositionColor(light.Position, Color.Black);
					short lightIndex = (short)inLightVertices.Count;
					inLightVertices.Add(lightVertex);
					inShadowVertices.Add(lightVertex);

					foreach (Guid id in model_ids)
					{
						GameModel gameModel = models[id];
						Matrix[] transforms = new Matrix[gameModel.Model.Bones.Count];
						gameModel.Model.CopyAbsoluteBoneTransformsTo(transforms);

						foreach (ModelMesh mesh in gameModel.Model.Meshes)
						{
							Matrix meshWorld = transforms[mesh.ParentBone.Index] * gameModel.World;

							foreach (ModelMeshPart part in mesh.MeshParts)
							{
								short startVertex = (short)inLightVertices.Count;
								short[] index = new short[part.IndexBuffer.IndexCount];
								VertexPositionColor[] vertex = new VertexPositionColor[part.NumVertices];
								part.VertexBuffer.GetData(part.VertexOffset, vertex, part.StartIndex, part.NumVertices, part.VertexBuffer.VertexDeclaration.VertexStride);
								part.IndexBuffer.GetData(index);

								for (int i = 0; i < vertex.Length; i++)
								{
									VertexPositionColor transformedVertex = new VertexPositionColor(Vector3.Transform(vertex[i].Position, meshWorld), vertex[i].Color);
									inLightVertices.Add(transformedVertex);
									inShadowVertices.Add(new VertexPositionColor(lightVertex.Position + Infinity*Vector3.Normalize(transformedVertex.Position - lightVertex.Position), transformedVertex.Color));
								}

								if (light.Type == Light.LightType.Point)
								{
									for (int i = 0; i < index.Length; i+=3)
									{
										// Light Pyramid
										inLightIndices.Add(lightIndex);
										inLightIndices.Add((short)(startVertex + index[i+1]));
										inLightIndices.Add((short)(startVertex + index[i]));

										inLightIndices.Add(lightIndex);
										inLightIndices.Add((short)(startVertex + index[i+2]));
										inLightIndices.Add((short)(startVertex + index[i+1]));

										inLightIndices.Add(lightIndex);
										inLightIndices.Add((short)(startVertex + index[i]));
										inLightIndices.Add((short)(startVertex + index[i+2]));

										// Shadow Pyramid
										inShadowIndices.Add(lightIndex);
										inShadowIndices.Add((short)(startVertex + index[i]));
										inShadowIndices.Add((short)(startVertex + index[i+1]));

										inShadowIndices.Add(lightIndex);
										inShadowIndices.Add((short)(startVertex + index[i+1]));
										inShadowIndices.Add((short)(startVertex + index[i+2]));

										inShadowIndices.Add(lightIndex);
										inShadowIndices.Add((short)(startVertex + index[i+2]));
										inShadowIndices.Add((short)(startVertex + index[i]));
									}
								}
							}
						}
					}
				}
			}

			lightIndexBuffer = new IndexBuffer(GraphicsDevice, IndexElementSize.SixteenBits, inLightIndices.Count, BufferUsage.None);
			lightVertexBuffer = new VertexBuffer(GraphicsDevice, typeof(VertexPositionColor), inLightVertices.Count, BufferUsage.None);
			shadowIndexBuffer = new IndexBuffer(GraphicsDevice, IndexElementSize.SixteenBits, inShadowIndices.Count, BufferUsage.None);
			shadowVertexBuffer = new VertexBuffer(GraphicsDevice, typeof(VertexPositionColor), inShadowVertices.Count, BufferUsage.None);
			lightIndexBuffer.SetData(inLightIndices.ToArray());
			lightVertexBuffer.SetData(inLightVertices.ToArray());
			shadowIndexBuffer.SetData(inShadowIndices.ToArray());
			shadowVertexBuffer.SetData(inShadowVertices.ToArray());
		}

		/// <summary>
		/// Compute and render shadow volumes for all models
		/// </summary>
		private void RenderShadowVolumes(ICollection<Guid> model_ids)
		{
			// Save original states
			BlendState originalBlendState = GraphicsDevice.BlendState;
			DepthStencilState originalDepthStencilState = GraphicsDevice.DepthStencilState;

			// No color blend state
			BlendState NoColorBlending = new BlendState();
			NoColorBlending.ColorSourceBlend = Blend.DestinationColor;
			NoColorBlending.AlphaSourceBlend = Blend.DestinationAlpha;

			// Front facing polygons state (increment stencil buffer)
			DepthStencilState FrontFaceStencil = new DepthStencilState();
			FrontFaceStencil.DepthBufferWriteEnable = false;
			FrontFaceStencil.StencilEnable = true;
			FrontFaceStencil.TwoSidedStencilMode = true;
			FrontFaceStencil.StencilPass = StencilOperation.Increment;

			// Back facing polygons state (decrement stencil buffer)
			DepthStencilState BackFaceStencil = new DepthStencilState();
			BackFaceStencil.DepthBufferWriteEnable = false;
			BackFaceStencil.StencilEnable = true;
			BackFaceStencil.TwoSidedStencilMode = true;
			BackFaceStencil.CounterClockwiseStencilPass = StencilOperation.Decrement;


			// Compute shadow volumes (this is likely a bottleneck)
			IndexBuffer lightIndexBuffer, shadowIndexBuffer;
			VertexBuffer lightVertexBuffer, shadowVertexBuffer;
			GenerateShadowVolumes(model_ids, out lightIndexBuffer, out lightVertexBuffer, out shadowIndexBuffer, out shadowVertexBuffer);

			// Render frontfacing polygons of shadow volumes
			GraphicsDevice.BlendState = NoColorBlending;
			GraphicsDevice.DepthStencilState = FrontFaceStencil;
			GraphicsDevice.RasterizerState = RasterizerState.CullNone;
			RenderShadowVolumePrimitives(lightIndexBuffer, lightVertexBuffer);
			RenderShadowVolumePrimitives(shadowIndexBuffer, shadowVertexBuffer);

			// Render backfacing polygons of shadow volumes
			GraphicsDevice.DepthStencilState = BackFaceStencil;
			RenderShadowVolumePrimitives(lightIndexBuffer, lightVertexBuffer);
			RenderShadowVolumePrimitives(shadowIndexBuffer, shadowVertexBuffer);

			// Reset states
			GraphicsDevice.BlendState = originalBlendState;
			GraphicsDevice.DepthStencilState = originalDepthStencilState;
		}

		/// <summary>
		/// Render primitives of a shadow volume
		/// </summary>
		/// <param name="indexBuffer"></param>
		/// <param name="vertexBuffer"></param>
		private void RenderShadowVolumePrimitives(IndexBuffer indexBuffer, VertexBuffer vertexBuffer)
		{
			// Get vertices
			VertexPositionColor[] vertices = new VertexPositionColor[vertexBuffer.VertexCount];
			vertexBuffer.GetData(vertices);

			// Get indices (DrawUserIndexedPrimitives takes 32 bit indices)
			int[] indices32 = new int[indexBuffer.IndexCount];
			if (indexBuffer.IndexElementSize == IndexElementSize.SixteenBits)
			{
				short[] indices16 = new short[indexBuffer.IndexCount];
				indexBuffer.GetData(indices16);

				// Convert the 16 bit indices to 32 bit indices
				for (int i = 0; i < indices32.Length; i++)
					indices32[i] = (int)indices16[i];
			}
			else
				indexBuffer.GetData(indices32);

			// Apply the current technique
			effect.Parameters["WorldViewProj"].SetValue(((ModelViewer)Game).Camera.ViewMatrix*((ModelViewer)Game).Camera.ProjectionMatrix);
			effect.CurrentTechnique = effect.Techniques["SlimShading"];
			effect.CurrentTechnique.Passes[0].Apply();

			// Draw the primitives
			GraphicsDevice.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, vertices, 0, vertexBuffer.VertexCount, indices32, 0, indexBuffer.IndexCount/3, vertexBuffer.VertexDeclaration);
		}
		#endregion

		#region Private Methods
		/// <summary>
		/// Get all models in view
		/// </summary>
		/// <returns></returns>
		private ICollection<Guid> GetModelsInView(Matrix view, Matrix proj)
		{
			ICollection<Guid> inView = new List<Guid>();
			BoundingFrustum viewFrustum = new BoundingFrustum(view * proj);

			foreach (GameModel gameModel in models.Values)
				if (viewFrustum.Contains(gameModel.BoundingSphere) == ContainmentType.Contains || viewFrustum.Contains(gameModel.BoundingSphere) == ContainmentType.Intersects)
					inView.Add(gameModel.GUID);

			return inView;
		}

		/// <summary>
		/// Get bound sphere containing the whole scene in view
		/// </summary>
		/// <returns></returns>
		private BoundingSphere GetBoundingSphere(ICollection<Guid> ids)
		{
			if (ids.Count <= 0)
				return new BoundingSphere { Center = new Vector3 { X = float.NaN, Y = float.NaN, Z = float.NaN }, Radius = float.NaN };

			var IDs = ids.ToArray();
			BoundingSphere sphere = models[IDs[0]].BoundingSphere;

			for (int i = 1; i < IDs.Length; i++)
				sphere = BoundingSphere.CreateMerged(sphere, models[IDs[i]].BoundingSphere);

			return sphere;
		}
		#endregion

		#region Accessors
		/// <summary>
		/// Gets the collection of lights
		/// </summary>
		public IDictionary<Guid, Light> Lights { get { return lights; } }

		/// <summary>
		/// Gets the collection of models
		/// </summary>
		public IDictionary<Guid, GameModel> Models { get { return models; } }

		/// <summary>
		/// Gets the collection of billboards
		/// </summary>
		public IDictionary<Guid, Billboard> Billboards { get { return billboards; } }

		/// <summary>
		/// Gets the scene
		/// </summary>
		public RenderTarget2D Scene { get { return accumulationBuffer[1]; } }

		/// <summary>
		/// Turn normal mapping on/off
		/// </summary>
		public bool NormalMapping { get; set; }

		/// <summary>
		/// Turn texture mapping on/off
		/// </summary>
		public bool TextureMapping { get; set; }

		/// <summary>
		/// Turn shadow mapping on/off
		/// </summary>
		public bool ShadowMapping { get; set; }

		/// <summary>
		/// Turn deferred shading on/off
		/// </summary>
		public bool DeferredShading { get; set; }

		/// <summary>
		/// Turn wireframe rendering on/off
		/// </summary>
		public bool WireFrame { get; set; }

		/// <summary>
		/// Turns default lighting on/off
		/// </summary>
		public bool DefaultLighting { get { return DefaultLight.Active; } set { DefaultLight.Active = value; } }

		/// <summary>
		/// Turn this shader mod on/off
		/// </summary>
		public bool One { get; set; }

		/// <summary>
		/// Turn this shader mod on/off
		/// </summary>
		public bool Two { get; set; }

		/// <summary>
		/// Turn this shader mod on/off
		/// </summary>
		public bool Three { get; set; }

		/// <summary>
		/// Get the number of models in view
		/// </summary>
		public int inViewCount { get { return inView.Count; } }

		/// <summary>
		/// Gets the game's camera
		/// </summary>
		CameraComponent Camera { get { return ((ModelViewer)Game).Camera; } }
		#endregion
	}
}

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

using float3 = Microsoft.Xna.Framework.Vector3;
using float4 = Microsoft.Xna.Framework.Vector4;
using System.IO;
using ModelShaderViewer;

namespace ShaderLibrary
{
	#region Lights
	/// <summary>
	/// Basic Light
	/// </summary>
	public class Light
	{
		public Guid GUID;
		public float4 Color;
		public float Intensity;
		public bool Active;
	}

	/// <summary>
	/// Directional Light
	/// </summary>
	public class DirectionalLight : Light
	{
		public float3 Direction;
		public float Ambience;
	}

	/// <summary>
	/// Point Light
	/// </summary>
	public class PointLight : Light
	{
		public float3 Position;
	}

	/// <summary>
	/// Spotlight
	/// </summary>
	public class SpotLight : Light
	{
		public float3 Position;
		public float3 Direction;
		public float Penumbra;
		public float Umbra;
		public int Exponent;
	}
	#endregion


	#region Model Properties
	/// <summary>
	/// Custom GameModel struct
	/// </summary>
	public class GameModel
	{
		public Guid GUID;
		public Model model;
		public Matrix world;
		public Material material;
		public Animation? animation;
		public Texture2D diffMap;
		public Texture2D specMap;
		public Texture2D normMap;
	}

	/// <summary>
	/// Model Material
	/// </summary>
	public struct Material
	{
		public float3 DiffuseColor;
		public float3 SpecularColor;
		public float3 AmbientColor;
		public float Smoothness;
	}

	/// <summary>
	/// Model Animation
	/// </summary>
	public struct Animation
	{
		
	}
	#endregion


	/// <summary>
	/// Shader class encompasses all code for rendering a Model
	/// </summary>
	public class Shader : DrawableGameComponent
	{
		Effect effect;
		Dictionary<Guid, Light> lights = new Dictionary<Guid,Light>();
		Dictionary<Guid, GameModel> models = new Dictionary<Guid,GameModel>();


		/// <summary>
		/// Default Constructor
		/// </summary>
		public Shader(Game game)
			: base(game)
		{
			NormalMapping = true;
			One = false;
			Two = false;
			Three = false;
		}

		/// <summary>
		/// Allows the game component to perform any initialization it needs to before starting
		/// to run.  This is where it can query for any required services and load content.
		/// </summary>
		public override void Initialize()
		{
			// TODO: Add your initialization code here

			base.Initialize();
		}

		/// <summary>
		/// Allows the game component to update itself.
		/// </summary>
		/// <param name="gameTime">Provides a snapshot of timing values.</param>
		public override void Update(GameTime gameTime)
		{
			// TODO: Add your update code here

			base.Update(gameTime);
		}

		/// <summary>
		/// Load the Shader effect
		/// </summary>
		/// <param name="Content"></param>
		public void LoadEffect()
		{
			effect = Game.Content.Load<Effect>("Effects/Effect");

			foreach (GameModel m in models.Values)
				foreach (ModelMesh mesh in m.model.Meshes)
					foreach (ModelMeshPart part in mesh.MeshParts)
						part.Effect = effect;
		}

		
		/// <summary>
		/// Add a model to the shader
		/// </summary>
		/// <param name="gameModel">The GameModel to add</param>
		/// <returns>A Guid for referencing the model</returns>
		public Guid AddModel(GameModel gameModel)
		{
			models.Add(gameModel.GUID, gameModel);

			foreach (ModelMesh mesh in gameModel.model.Meshes)
				foreach (ModelMeshPart part in mesh.MeshParts)
					part.Effect = effect;

			return gameModel.GUID;
		}

		/// <summary>
		/// Add a light to the shader
		/// </summary>
		/// <param name="light"></param>
		public Guid AddLight(Light light)
		{
			lights.Add(light.GUID, light);

			if (lights.Count > MAXLIGHTS)
				throw new Exception("Too many lights");

			return light.GUID;
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
		/// Does the actual drawing of the skybox with our skybox effect.
		/// There is no world matrix, because we're assuming the skybox won't
		/// be moved around.  The size of the skybox can be changed with the size
		/// variable.
		/// </summary>
		/// <param name="gameTime">Provides a snapshot of timing values.</param>
		public override void Draw(GameTime gameTime)
		{
			foreach (GameModel m in models.Values)
			{
				Matrix[] transforms = new Matrix[m.model.Bones.Count];
				m.model.CopyAbsoluteBoneTransformsTo(transforms);

				foreach (ModelMesh mesh in m.model.Meshes)
				{
					Effect effect = mesh.Effects[0];
					Matrix meshWorld = transforms[mesh.ParentBone.Index] * m.world;

					effect.Parameters["World"].SetValue(meshWorld);
					effect.Parameters["WorldViewProj"].SetValue(meshWorld*((ModelViewer)Game).Camera.ViewMatrix*((ModelViewer)Game).Camera.ProjectionMatrix);
					effect.Parameters["WorldInverseTranspose"].SetValue(Matrix.Invert(Matrix.Transpose(meshWorld)));
					effect.Parameters["SpecularMap"].SetValue(m.specMap);
					effect.Parameters["DiffuseMap"].SetValue(m.diffMap);
					effect.Parameters["NormalMap"].SetValue(m.normMap);
					effect.Parameters["Kd"].SetValue(m.material.DiffuseColor/(float)Math.PI);
					effect.Parameters["Ks"].SetValue((m.material.Smoothness+8)/(float)(8*Math.PI) * m.material.SpecularColor);
					effect.Parameters["m"].SetValue(m.material.Smoothness);

					effect.Parameters["normal_mapping"].SetValue(NormalMapping);
					effect.Parameters["one"].SetValue(One);
					effect.Parameters["two"].SetValue(Two);
					effect.Parameters["three"].SetValue(Three);

					effect.Parameters["camera"].StructureMembers["Position"].SetValue(((ModelViewer)Game).Camera.Position);

					int pointlightCount = 0;
					int spotlightCount = 0;
					int directionalCount = 0;
					foreach (Light l in lights.Values)
					{
						if (l.Active)
						{
							if (l.GetType() == typeof(PointLight))
							{
								effect.Parameters["pointlight"].Elements[pointlightCount].StructureMembers["Position"].SetValue(((PointLight)l).Position);
								effect.Parameters["pointlight"].Elements[pointlightCount].StructureMembers["Intensity"].SetValue(((PointLight)l).Color);
								pointlightCount += 1;
							}

							if (l.GetType() == typeof(SpotLight))
							{
								effect.Parameters["spotlight"].Elements[spotlightCount].StructureMembers["Position"].SetValue(((SpotLight)l).Position);
								effect.Parameters["spotlight"].Elements[spotlightCount].StructureMembers["Direction"].SetValue(((SpotLight)l).Direction);
								effect.Parameters["spotlight"].Elements[spotlightCount].StructureMembers["Intensity"].SetValue(((SpotLight)l).Intensity);
								effect.Parameters["spotlight"].Elements[spotlightCount].StructureMembers["Penumbra"].SetValue(((SpotLight)l).Penumbra);
								effect.Parameters["spotlight"].Elements[spotlightCount].StructureMembers["Umbra"].SetValue(((SpotLight)l).Umbra);
								effect.Parameters["spotlight"].Elements[spotlightCount].StructureMembers["rstart"].SetValue(1);
								effect.Parameters["spotlight"].Elements[spotlightCount].StructureMembers["rend"].SetValue(300);
								effect.Parameters["spotlight"].Elements[spotlightCount].StructureMembers["Exponent"].SetValue(((SpotLight)l).Exponent);
								spotlightCount += 1;
							}

							if (l.GetType() == typeof(DirectionalLight))
							{
								effect.Parameters["directional"].Elements[directionalCount].StructureMembers["Direction"].SetValue(((DirectionalLight)l).Direction);
								effect.Parameters["directional"].Elements[directionalCount].StructureMembers["Color"].SetValue(((DirectionalLight)l).Color);
								effect.Parameters["directional"].Elements[directionalCount].StructureMembers["Intensity"].SetValue(((DirectionalLight)l).Intensity);
								effect.Parameters["directional"].Elements[directionalCount].StructureMembers["Ambience"].SetValue(((DirectionalLight)l).Ambience);
								directionalCount += 1;
							}
						}
					}

					effect.Parameters["pointlightCount"].SetValue(pointlightCount);
					effect.Parameters["spotlightCount"].SetValue(spotlightCount);
					effect.Parameters["directionalCount"].SetValue(directionalCount);

					mesh.Draw();
				}
			}
		}


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
		public static GameModel CreateGameModel(Model model, Matrix? world, Material? material, Animation? animation, Texture2D diffMap, Texture2D specMap, Texture2D normMap)
		{
			GameModel gameModel = new GameModel();
			gameModel.GUID = Guid.NewGuid();
			gameModel.model = model;
			gameModel.world = world != null ? world.Value : Matrix.Identity;
			gameModel.material = material != null ? material.Value : DefaultMaterial;
			gameModel.animation = animation;
			gameModel.diffMap = diffMap;
			gameModel.specMap = specMap;
			gameModel.normMap = normMap;

			return gameModel;
		}


		/// <summary>
		/// Create a Directional Light
		/// </summary>
		/// <param name="Color"></param>
		/// <param name="Direction"></param>
		/// <param name="DiffuseIntensity"></param>
		/// <param name="AmbientIntensity"></param>
		/// <returns></returns>
		public static DirectionalLight CreateDirectionalLight(float4 Color, float3 Direction, float Intensity, float Ambience)
		{
			DirectionalLight directional = new DirectionalLight();
			directional.GUID = Guid.NewGuid();
			directional.Color = Color;
			directional.Direction = Direction;
			directional.Intensity = Intensity;
			directional.Ambience = Ambience;
			directional.Active = true;

			return directional;
		}


		/// <summary>
		/// Create a Point Light
		/// </summary>
		/// <param name="Color"></param>
		/// <param name="Position"></param>
		/// <param name="Intensity"></param>
		/// <returns></returns>
		public static PointLight CreatePointLight(float4 Color, float3 Position, float Intensity)
		{
			PointLight point = new PointLight();
			point.GUID = Guid.NewGuid();
			point.Color = Color;
			point.Position = Position;
			point.Intensity = Intensity;
			point.Active = true;

			return point;
		}


		/// <summary>
		/// Create a Spot Light
		/// </summary>
		/// <param name="Color"></param>
		/// <param name="Position"></param>
		/// <param name="Direction"></param>
		/// <param name="Intensity"></param>
		/// <param name="Penumbra"></param>
		/// <param name="Umbra"></param>
		/// <param name="Exponent"></param>
		/// <returns></returns>
		public static SpotLight CreateSpotLight(float4 Color, float3 Position, float3 Direction, float Intensity, float Penumbra, float Umbra, int Exponent)
		{
			SpotLight spot = new SpotLight();
			spot.GUID = Guid.NewGuid();
			spot.Color = Color;
			spot.Position = Position;
			spot.Direction = Direction;
			spot.Intensity = Intensity;
			spot.Penumbra = Penumbra;
			spot.Umbra = Umbra;
			spot.Exponent = Exponent;
			spot.Active = true;

			return spot;
		}
		#endregion


		#region Accessors
		/// <summary>
		/// Default Model Material
		/// </summary>
		public static Material DefaultMaterial
		{
			get
			{
				Material m;
				m.DiffuseColor = float3.One;
				m.SpecularColor = float3.One;
				m.AmbientColor = float3.One;
				m.Smoothness = 1;
				return m;
			}
		}

		public IDictionary<Guid, Light> Lights { get { return lights; } }
		public IDictionary<Guid, GameModel> Models { get { return models; } }

		/// <summary>
		/// Turn normal mapping on/off
		/// </summary>
		public bool NormalMapping { get; set; }
		
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
		#endregion
	}
}

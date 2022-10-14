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


namespace ModelShaderViewer
{
    /// <summary>
    /// Handles all of the aspects of working with a skybox.
    /// </summary>
	public class Skybox : DrawableGameComponent
	{
		/// <summary>
        /// The skybox model, which will just be a cube
        /// </summary>
        private Model skyBox;
 
        /// <summary>
        /// The actual skybox texture
        /// </summary>
        private TextureCube skyBoxTexture;
 
        /// <summary>
        /// The effect file that the skybox will use to render
        /// </summary>
        private Effect skyBoxEffect;
 
        /// <summary>
        /// The size of the cube, used so that we can resize the box
        /// for different sized environments.
        /// </summary>
        private float size = 50f;


		/// <summary>
        /// Creates a new skybox
        /// </summary>
		/// <param name="skyboxTexture">the name of the skybox texture to use</param>
		public Skybox(Game game)
			: base(game)
        {
			DrawOrder = 3;
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
		/// Loads skybox content
		/// </summary>
		/// <param name="skyboxTexture"></param>
		public void LoadSkybox(string skyboxTexture)
		{
			skyBox = Game.Content.Load<Model>("Skyboxes/cube");
			skyBoxTexture = Game.Content.Load<TextureCube>(skyboxTexture);
			skyBoxEffect = Game.Content.Load<Effect>("Skyboxes/Skybox");
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
		/// Does the actual drawing of the skybox with our skybox effect.
		/// There is no world matrix, because we're assuming the skybox won't
		/// be moved around.  The size of the skybox can be changed with the size
		/// variable.
		/// </summary>
		/// <param name="gameTime">Provides a snapshot of timing values.</param>
		public override void Draw(GameTime gameTime)
		{
			//GraphicsDevice.SetRenderTarget(((ModelViewer)Game).ActiveRenderTarget);

			// Save the current rasterizer state
			RasterizerState original = Game.GraphicsDevice.RasterizerState;

			// The skybox has clockwise culling, so it needs a new rasterizer state
			Game.GraphicsDevice.RasterizerState = RasterizerState.CullClockwise;

			// Go through each pass in the effect, but we know there is only one...
			foreach (EffectPass pass in skyBoxEffect.CurrentTechnique.Passes)
			{
				// Draw all of the components of the mesh, but we know the cube really
				// only has one mesh
				foreach (ModelMesh mesh in skyBox.Meshes)
				{
					// Assign the appropriate values to each of the parameters
					foreach (ModelMeshPart part in mesh.MeshParts)
					{
						part.Effect = skyBoxEffect;
						part.Effect.Parameters["World"].SetValue(Matrix.CreateScale(size) * Matrix.CreateTranslation(((ModelViewer)Game).Camera.Position));
						part.Effect.Parameters["View"].SetValue(((ModelViewer)Game).Camera.ViewMatrix);
						part.Effect.Parameters["Projection"].SetValue(((ModelViewer)Game).Camera.ProjectionMatrix);
						part.Effect.Parameters["SkyBoxTexture"].SetValue(skyBoxTexture);
						part.Effect.Parameters["CameraPosition"].SetValue(((ModelViewer)Game).Camera.Position);
					}

					// Draw the mesh with the skybox effect
					mesh.Draw();
				}
			}

			Game.GraphicsDevice.RasterizerState = original;
			//GraphicsDevice.SetRenderTarget(null);
		}
	}
}

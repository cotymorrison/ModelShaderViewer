using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace ModelShaderViewer
{
    public class CameraComponent : GameComponent
    {
		public const float START_X = 0.0f;
		public const float START_Y = 1.7f;
		public const float START_Z = 10.0f;
		public const float FOV_DEGREES = 75.0f;
		public static float FOV_RADIANS { get { return MathHelper.ToRadians(FOV_DEGREES); } }
		public const float ZNEAR = 0.1f;
		public const float ZFAR = 100.0f;
		public const float ROTATION_SPEED = 0.2f;
		public const float WALK_SPEED = 1.38889f;
		public const float RUN_SPEED = 6.25856f;

		public Keys MoveForward    = Keys.W;
		public Keys MoveLeft       = Keys.A;
		public Keys MoveBackward   = Keys.S;
		public Keys MoveRight      = Keys.D;
		public Keys MoveUp         = Keys.Q;
		public Keys MoveDown       = Keys.E;

		public Vector3 UpAxis { get { return WORLD_Y_AXIS; } }
		public Vector3 RightAxis { get { return localXAxis; } }
		public Vector3 ForwardAxis { get { return Vector3.Normalize(Vector3.Cross(localXAxis, WORLD_Y_AXIS)); } }

        private MouseState prevMouseState;
		private MouseState currentMouseState;
		private KeyboardState prevKeyboardState;
		private KeyboardState currentKeyboardState;

		private Quaternion orientation = Quaternion.Identity;
		private Vector3 localXAxis = Vector3.Right;
		private Vector3 localYAxis = Vector3.Up;
		private Vector3 localZAxis = Vector3.Backward;
		private static Vector3 WORLD_X_AXIS = Vector3.Right;
		private static Vector3 WORLD_Y_AXIS = Vector3.Up;
		private static Vector3 WORLD_Z_AXIS = Vector3.Backward;


        public CameraComponent(Game game) : base(game)
		{
			UpdateOrder = 1;
			Up = Vector3.Zero;
			Look = Vector3.Zero;
			Position = Vector3.Zero;
			ViewMatrix = Matrix.Identity;
			ProjectionMatrix = Matrix.Identity;
        }

		public override void Initialize()
        {
			// Initialize default quantities
			Up = Vector3.Up;
			Look = Vector3.Zero;
			Position = InitialPosition;

			// Create the view and projection matrices
			ViewMatrix = Matrix.CreateLookAt(Position, Look, Up);
			ProjectionMatrix = Matrix.CreatePerspectiveFieldOfView(MathHelper.ToRadians(FOV_DEGREES), Game.GraphicsDevice.Viewport.AspectRatio, ZNEAR, ZFAR);

			// Get the initial rotation quaternion
			Vector3 temp;
			ViewMatrix.Decompose(out temp, out orientation, out temp);

			// Set the mouse position to center-screen
			Mouse.SetPosition(Game.Window.ClientBounds.Width / 2, Game.Window.ClientBounds.Height / 2);
        }

		public override void Update(GameTime gameTime)
        {
			if (((ModelViewer)Game).Paused)
				return;

            // Mouse Rotation
            prevMouseState = currentMouseState;
            currentMouseState = Mouse.GetState();

            Rectangle clientBounds = Game.Window.ClientBounds;

            int centerX = clientBounds.Width / 2;
            int centerY = clientBounds.Height / 2;
            int deltaX = centerX - currentMouseState.X;
            int deltaY = centerY - currentMouseState.Y;

            Mouse.SetPosition(centerX, centerY);

            RotateCamera(deltaX, deltaY);


            // Keyboard Movement
            prevKeyboardState = currentKeyboardState;
            currentKeyboardState = Keyboard.GetState();

			Vector3 delta = Vector3.Zero;

            if (currentKeyboardState.IsKeyDown(MoveForward))
				delta += Vector3.Forward;

			if (currentKeyboardState.IsKeyDown(MoveLeft))
                delta += Vector3.Left;

			if (currentKeyboardState.IsKeyDown(MoveBackward))
                delta += Vector3.Backward;

			if (currentKeyboardState.IsKeyDown(MoveRight))
                delta += Vector3.Right;

			if (currentKeyboardState.IsKeyDown(MoveUp))
                delta += Vector3.Up;

			if (currentKeyboardState.IsKeyDown(MoveDown))
                delta += Vector3.Down;

			if (delta != Vector3.Zero)
				delta.Normalize();

			Move(delta * RUN_SPEED * (float)gameTime.ElapsedGameTime.TotalSeconds);
        }

		/// <summary>
		/// Point the camera at a point in world space
		/// </summary>
		/// <param name="position"></param>
		public void LookAt(Vector3 position)
		{
			Vector3 temp;
			Look = position;
			ViewMatrix = Matrix.CreateLookAt(Position, Look, Up);
			ViewMatrix.Decompose(out temp, out orientation, out temp);
		}

		/// <summary>
		/// Move the camera in the delta direction
		/// </summary>
		/// <param name="delta"></param>
		private void Move(Vector3 delta)
        {
            // Calculate the forwards direction. Can't just use the
            // camera's view direction as doing so will cause the camera to
            // move more slowly as the camera's view approaches 90 degrees
            // straight up and down.

            Vector3 forwards = Vector3.Normalize(Vector3.Cross(localXAxis, WORLD_Y_AXIS));

			Position += localXAxis * delta.X;
			Position += WORLD_Y_AXIS * delta.Y;
			Position += forwards * delta.Z;
		}

		/// <summary>
		/// Rotates the camera in world Y and local X axes
		/// </summary>
		/// <param name="deltaX"></param>
		/// <param name="deltaY"></param>
		private void RotateCamera(int deltaX, int deltaY)
        {
			float headingDegrees = -deltaX * ROTATION_SPEED;
			float pitchDegrees = -deltaY * ROTATION_SPEED;

            float heading = MathHelper.ToRadians(headingDegrees);
            float pitch = MathHelper.ToRadians(pitchDegrees);
			Quaternion rotation = Quaternion.Identity;

            // Rotate the camera about the world Y axis.
            if (heading != 0.0f)
            {
                Quaternion.CreateFromAxisAngle(ref WORLD_Y_AXIS, heading, out rotation);
                Quaternion.Concatenate(ref rotation, ref orientation, out orientation);
            }

            // Rotate the camera about its local X axis.
            if (pitch != 0.0f)
            {
                Quaternion.CreateFromAxisAngle(ref WORLD_X_AXIS, pitch, out rotation);
                Quaternion.Concatenate(ref orientation, ref rotation, out orientation);
            }

			UpdateViewMatrix();
        }

		/// <summary>
		/// Updates the view matrix for the current orientation
		/// </summary>
		private void UpdateViewMatrix()
		{
			ViewMatrix = Matrix.CreateFromQuaternion(orientation);

			localXAxis.X = ViewMatrix.M11;
			localXAxis.Y = ViewMatrix.M21;
			localXAxis.Z = ViewMatrix.M31;

			localYAxis.X = ViewMatrix.M12;
			localYAxis.Y = ViewMatrix.M22;
			localYAxis.Z = ViewMatrix.M32;

			localZAxis.X = ViewMatrix.M13;
			localZAxis.Y = ViewMatrix.M23;
			localZAxis.Z = ViewMatrix.M33;

			ViewMatrix = Matrix.CreateTranslation(-Position) * ViewMatrix;
			Look = new Vector3(-localZAxis.X, -localZAxis.Y, -localZAxis.Z);
		}

        public Vector3 Up { get; private set; }
        public Vector3 Look { get; private set; }
        public Vector3 Position { get; private set; }
		public Vector3 InitialPosition { get { return new Vector3(START_X, START_Y, START_Z); } }
		public Matrix ViewMatrix { get; private set; }
		public Matrix ProjectionMatrix { get; private set; }
	}
}

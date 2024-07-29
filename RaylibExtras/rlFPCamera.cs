using System;
using System.Collections.Generic;
using System.Numerics;
using Raylib_cs;

namespace raylibExtras
{
    public class rlFPCamera
    {
        public enum CameraControls
        {
            MOVE_FRONT = 0,
            MOVE_BACK,
            MOVE_RIGHT,
            MOVE_LEFT,
            MOVE_UP,
            MOVE_DOWN,
            TURN_LEFT,
            TURN_RIGHT,
            TURN_UP,
            TURN_DOWN,
            SPRINT,
        }

        public Dictionary<CameraControls, KeyboardKey> ControlsKeys = new Dictionary<CameraControls, KeyboardKey>();

        public Vector3 MoveSpeed = new Vector3(1, 1, 1);
        public Vector2 TurnSpeed = new Vector2(90, 90);

        public float MouseSensitivity = 600;

        public float MinimumViewY = -65.0f;
        public float MaximumViewY = 89.0f;

        public float ViewBobbleFreq = 0.0f;
        public float ViewBobbleMagnatude = 0.02f;
        public float ViewBobbleWaverMagnitude = 0.002f;

        public delegate bool PositionCallback(rlFPCamera camera, Vector3 newPosition, Vector3 oldPosition);
        public PositionCallback ValidateCamPosition = null;

        public Camera3D Camera { get { return ViewCamera; } }

        public bool UseMouseX = true;
        public bool UseMouseY = true;

        public bool InvertY = false;

        public bool UseKeyboard = true;

        public bool UseController = true;
        public bool ControlerID = false;

        //clipping planes
        // note must use BeginMode3D and EndMode3D on the camera object for clipping planes to work
        public double NearPlane = 0.01;
        public double FarPlane = 1000;

        public bool HideCursor = true;

        protected bool Focused = true;
        protected Vector3 CameraPosition = new Vector3(0.0f, 0.0f, 0.0f);

        public Camera3D ViewCamera = new Camera3D();
        protected Vector2 FOV = new Vector2(0.0f, 0.0f);

        protected Vector2 PreviousMousePosition = new Vector2(0.0f, 0.0f);

        protected float TargetDistance = 0;               // Camera distance from position to target
        protected float PlayerEyesPosition = 0.5f;       // Player eyes position from ground (in meters)
        protected Vector2 Angle = new Vector2(0, 0);                // Camera angle in plane XZ

        protected float CurrentBobble = 0;

        public rlFPCamera()
        {
            ControlsKeys.Add(CameraControls.MOVE_FRONT, KeyboardKey.W);
            ControlsKeys.Add(CameraControls.MOVE_BACK, KeyboardKey.S);
            ControlsKeys.Add(CameraControls.MOVE_LEFT, KeyboardKey.A);
            ControlsKeys.Add(CameraControls.MOVE_RIGHT, KeyboardKey.D);
            ControlsKeys.Add(CameraControls.MOVE_UP, KeyboardKey.E);
            ControlsKeys.Add(CameraControls.MOVE_DOWN, KeyboardKey.Q);

            ControlsKeys.Add(CameraControls.TURN_LEFT, KeyboardKey.Left);
            ControlsKeys.Add(CameraControls.TURN_RIGHT, KeyboardKey.Right);
            ControlsKeys.Add(CameraControls.TURN_UP, KeyboardKey.Up);
            ControlsKeys.Add(CameraControls.TURN_DOWN, KeyboardKey.Down);
            ControlsKeys.Add(CameraControls.SPRINT, KeyboardKey.LeftShift);

            PreviousMousePosition = Raylib.GetMousePosition();
        }

        public void Setup(float fovY, Vector3 position)
        {
            CameraPosition = new Vector3(position.X, position.Y, position.Z);
            ViewCamera.Position = new Vector3(position.X, position.Y, position.Z);
            ViewCamera.Position.Y += PlayerEyesPosition;
            ViewCamera.Target = ViewCamera.Position + new Vector3(0, 0, 1 );
            ViewCamera.Up = new Vector3(0, 1, 0);
            ViewCamera.FovY = fovY;
            ViewCamera.Projection = CameraProjection.Perspective;

            Focused = Raylib.IsWindowFocused();
            if (HideCursor && Focused && (UseMouseX || UseMouseY))
                Raylib.DisableCursor();

            TargetDistance = 1;

            ViewResized();
        }

        public void ViewResized()
        {
            float width = (float)Raylib.GetScreenWidth();
            float height = (float)Raylib.GetScreenHeight();

            FOV.Y = ViewCamera.FovY;

            if (height != 0)
                FOV.X = FOV.Y * (width / height);
        }

        protected float GetSpeedForAxis(CameraControls axis, float speed)
        {
            if (!UseKeyboard || !ControlsKeys.ContainsKey(axis))
                return 0;

            KeyboardKey key = ControlsKeys[axis];
            if (key == KeyboardKey.Null)
                return 0;

            float factor = 1.0f;
            if (Raylib.IsKeyDown(ControlsKeys[CameraControls.SPRINT]))
                factor = 2;

            if (Raylib.IsKeyDown(key))
                return speed * Raylib.GetFrameTime() * factor;

            return 0.0f;
        }


        public void Update()
        {
            if (HideCursor && Raylib.IsWindowFocused() != Focused && (UseMouseX || UseMouseY))
            {
                Focused = Raylib.IsWindowFocused();
                if (Focused)
                {
                    Raylib.DisableCursor();
                }
                else
                {
                    Raylib.EnableCursor();
                }
            }

            Vector2 mousePositionDelta = Raylib.GetMousePosition() - PreviousMousePosition;
            PreviousMousePosition = Raylib.GetMousePosition();

            // Mouse movement detection
            float mouseWheelMove = Raylib.GetMouseWheelMove();

            // Keys input detection
            Dictionary<CameraControls, float> directions = new Dictionary<CameraControls, float>();
            directions[CameraControls.MOVE_FRONT] = GetSpeedForAxis(CameraControls.MOVE_FRONT, MoveSpeed.Z);
            directions[CameraControls.MOVE_BACK] = GetSpeedForAxis(CameraControls.MOVE_BACK, MoveSpeed.Z);
            directions[CameraControls.MOVE_RIGHT] = GetSpeedForAxis(CameraControls.MOVE_RIGHT, MoveSpeed.X);
            directions[CameraControls.MOVE_LEFT] = GetSpeedForAxis(CameraControls.MOVE_LEFT, MoveSpeed.X);
            directions[CameraControls.MOVE_UP] = GetSpeedForAxis(CameraControls.MOVE_UP, MoveSpeed.Y);
            directions[CameraControls.MOVE_DOWN] = GetSpeedForAxis(CameraControls.MOVE_DOWN, MoveSpeed.Y);


            Vector3 forward = ViewCamera.Target - ViewCamera.Position;
            forward.Y = 0;
            forward = Vector3.Normalize(forward);

            Vector3 right = new Vector3( forward.Z * -1.0f, 0, forward.X );

            Vector3 oldPosition = CameraPosition;

            CameraPosition += (forward * (directions[CameraControls.MOVE_FRONT] - directions[CameraControls.MOVE_BACK]));
            CameraPosition += (right * (directions[CameraControls.MOVE_RIGHT] - directions[CameraControls.MOVE_LEFT]));

            CameraPosition.Y += directions[CameraControls.MOVE_UP] - directions[CameraControls.MOVE_DOWN];

            // let someone modify the projected position
            if (ValidateCamPosition != null)
                ValidateCamPosition(this, CameraPosition, oldPosition);

            // Camera orientation calculation
            float turnRotation = GetSpeedForAxis(CameraControls.TURN_RIGHT, TurnSpeed.X) - GetSpeedForAxis(CameraControls.TURN_LEFT, TurnSpeed.X);
            float tiltRotation = GetSpeedForAxis(CameraControls.TURN_UP, TurnSpeed.Y) - GetSpeedForAxis(CameraControls.TURN_DOWN, TurnSpeed.Y);

            float yFactor = InvertY ? -1 : 1;

            if (turnRotation != 0)
                Angle.X -= turnRotation * (MathF.PI/180.0f);
            else if (UseMouseX && Focused)
                Angle.X += (mousePositionDelta.X / -MouseSensitivity);

            if (tiltRotation != 0)
                Angle.Y += yFactor * tiltRotation * (MathF.PI / 180.0f);
            else if (UseMouseY && Focused)
                Angle.Y += (yFactor * mousePositionDelta.Y / -MouseSensitivity);

            // Angle clamp
            if (Angle.Y < MinimumViewY * (MathF.PI / 180.0f))
                Angle.Y = MinimumViewY * (MathF.PI / 180.0f);
            else if (Angle.Y > MaximumViewY * (MathF.PI / 180.0f))
                Angle.Y = MaximumViewY * (MathF.PI / 180.0f);

            // Recalculate camera target considering translation and rotation
            Vector3 target = Raymath.Vector3Transform(new Vector3( 0, 0, 1 ), Raymath.MatrixRotateZYX(new Vector3(-Angle.Y, Angle.X, 0 )));

            ViewCamera.Position = CameraPosition;

            float eyeOfset = PlayerEyesPosition;

            if (ViewBobbleFreq > 0)
            {
                float swingDelta = MathF.Max(MathF.Abs(directions[CameraControls.MOVE_FRONT] - directions[CameraControls.MOVE_BACK]), MathF.Abs(directions[CameraControls.MOVE_RIGHT] - directions[CameraControls.MOVE_LEFT]));

                // If movement detected (some key pressed), increase swinging
                CurrentBobble += swingDelta * ViewBobbleFreq;

                const float ViewBobbleDampen = 8.0f;

                eyeOfset -= MathF.Sin(CurrentBobble / ViewBobbleDampen) * ViewBobbleMagnatude;

                ViewCamera.Up.X = MathF.Sin(CurrentBobble / (ViewBobbleDampen * 2)) * ViewBobbleWaverMagnitude;
                ViewCamera.Up.Z = -MathF.Sin(CurrentBobble / (ViewBobbleDampen * 2)) * ViewBobbleWaverMagnitude;
            }
            else
            {
                CurrentBobble = 0;
                ViewCamera.Up.X = 0;
                ViewCamera.Up.Z = 0;
            }

            ViewCamera.Position.Y += eyeOfset;

            ViewCamera.Target = ViewCamera.Position + target;
        }

        public float GetFOVX()
        {
            return FOV.X;
        }

        public Vector3 GetCameraPosition()
        {
            return CameraPosition;
        }

        public void SetCameraPosition(Vector3 pos)
        {
            CameraPosition = pos;
            Vector3 forward = ViewCamera.Target - ViewCamera.Position;
            ViewCamera.Position = CameraPosition;
            ViewCamera.Target = CameraPosition + forward;
        }

        public Vector2 GetViewAngles()
        {
            return Raymath.Vector2Scale(Angle, (float)(Math.PI / 180.0f));
        }

        // start drawing using the camera, with near/far plane support
        public void BeginMode3D()
        {
            float aspect = (float)Raylib.GetScreenWidth() / (float)Raylib.GetScreenHeight();

            Rlgl.DrawRenderBatchActive();           // Draw Buffers (Only OpenGL 3+ and ES2)
            Rlgl.MatrixMode(MatrixMode.Projection); // Switch to projection matrix
            Rlgl.PushMatrix();                      // Save previous matrix, which contains the settings for the 2d ortho projection
            Rlgl.LoadIdentity();                    // Reset current matrix (projection)

            if (ViewCamera.Projection == CameraProjection.Perspective)
            {
                // Setup perspective projection
                double top = Rlgl.CULL_DISTANCE_NEAR * System.MathF.Tan(ViewCamera.FovY * 0.5f * (MathF.PI / 180.0f));
                double right = top * aspect;

                Rlgl.Frustum(-right, right, -top, top, NearPlane, FarPlane);
            }
            else if (ViewCamera.Projection == CameraProjection.Orthographic)
            {
                // Setup orthographic projection
                double top = ViewCamera.FovY / 2.0;
                double right = top * aspect;

                Rlgl.Ortho(-right, right, -top, top, NearPlane, FarPlane);
            }

            // NOTE: zNear and zFar values are important when computing depth buffer values

            Rlgl.MatrixMode(MatrixMode.ModelView); // Switch back to modelview matrix
            Rlgl.LoadIdentity();                   // Reset current matrix (modelview)

            // Setup Camera view
            Matrix4x4 matView = Raymath.MatrixLookAt(ViewCamera.Position, ViewCamera.Target, ViewCamera.Up);

            Rlgl.MultMatrixf(matView);      // Multiply modelview matrix by view matrix (camera)

            Rlgl.EnableDepthTest();    // Enable DEPTH_TEST for 3D
        }

        // end drawing with the camera
        public void EndMode3D()
        {
            Raylib.EndMode3D();
        }
    }
}
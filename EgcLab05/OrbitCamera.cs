using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace Egc
{
     public class OrbitCamera : Camera
    {
        //Limite si parametrii
        public const float MinDistance = 1.5f;
        public const float MaxDistance = 40.0f;
        public const float MaxElevation = 89.0f;
        public float MouseSensitivity = 0.3f;
        public float KeyTurnSpeed = 90.0f;
        public float ZoomFactor = 0.9f;
        public float PanSpeed = 4.0f;
        private static readonly Vector3 WorldUp = Vector3.UnitY;
        private static readonly Vector3 StartTarget = new(0.0f, 1.0f, 0.0f);
        private const float StartDistance = 8.0f;
        private const float StartAzimuth = 90.0f;
        private const float StartElevation = 20.0f;

        //Starea camerei

        public Vector3 Target { get; private set; }
        public float Distance { get; private set; }
        public float Azimuth { get; private set; }
        public float Elevation { get; private set; }

        public OrbitCamera()
        {
            Reset();
        }


        private Vector3 Direction
        {
            get
            {
                float az = MathHelper.DegreesToRadians(Azimuth);
                float el = MathHelper.DegreesToRadians(Elevation);
                return new Vector3(
                    MathF.Cos(el) * MathF.Cos(az),
                    MathF.Sin(el),
                    MathF.Cos(el) * MathF.Sin(az));

            }
        }

        public override string Name => "Orbita";
        public override Vector3 Position => Target + Distance * Direction;
        public override Vector3 Front => -Direction;
        public override Matrix4 ViewMatrix => Matrix4.LookAt(Position, Target, WorldUp);
        public override void Update(KeyboardState keyboard, MouseState mouse, Vector2 mouseDelta, float dt)
        {
            //rotatia cu mouse-ul
            if (mouse.IsButtonDown(MouseButton.Left))
            {
                Azimuth += mouseDelta.X * MouseSensitivity;
                Elevation += mouseDelta.Y * MouseSensitivity;
            }

            //rotatia cu sagetile
            float turn = KeyTurnSpeed * dt;
            if (keyboard.IsKeyDown(Keys.Right)) Azimuth += turn;
            if (keyboard.IsKeyDown(Keys.Left)) Azimuth -= turn;
            if (keyboard.IsKeyDown(Keys.Up)) Elevation += turn;
            if (keyboard.IsKeyDown(Keys.Down)) Elevation -= turn;

            Elevation = Math.Clamp(Elevation, -MaxElevation, MaxElevation);
            Azimuth %= 360.0f;

            //rotitat schimba distanta
            Distance = Math.Clamp(Distance * MathF.Pow(ZoomFactor, mouse.ScrollDelta.Y), MinDistance, MaxDistance);

            //W A S D muta tinta in palnul orizontal
            Vector3 front = Front;
            Vector3 forward = Vector3.Normalize(new Vector3(front.X, 0.0f, front.Z));
            Vector3 right = Vector3.Cross(forward, WorldUp);

            Vector3 move = Vector3.Zero;
            if (keyboard.IsKeyDown(Keys.W)) move += forward;
            if (keyboard.IsKeyDown(Keys.S)) move -= forward;
            if (keyboard.IsKeyDown(Keys.D)) move += right;
            if (keyboard.IsKeyDown(Keys.A)) move -= right;

            if (move.LengthSquared > 0.0f)
            {
                Target += Vector3.Normalize(move) * PanSpeed * dt;
            }
        }

        //FPS->orbita

             public void LookFrom(Vector3 position, Vector3 front, float distance = 5.0f)
        {
            Target = position + front * distance;
            Distance = Math.Clamp(distance, MinDistance, MaxDistance);

            Vector3 o = -front;
            Elevation = Math.Clamp(MathHelper.RadiansToDegrees(MathF.Asin(o.Y)), -MaxElevation, MaxElevation);
            Azimuth = MathHelper.RadiansToDegrees(MathF.Atan2(o.Z, o.X));
        }

        public override void Reset()
        {
            Target = StartTarget;
            Distance = StartDistance;
            Azimuth = StartAzimuth;
            Elevation = StartElevation;
            FieldOfView = 60.0f;
        }

        public override IEnumerable<string> Describe()
        {
            Vector3 p = Position;
            yield return $"Țintă          : ({Target.X,6:F2}, {Target.Y,6:F2}, {Target.Z,6:F2})";
            yield return $"Distanță       : {Distance,6:F2}";
            yield return $"Azimut / elev. : {Azimuth,7:F1}° / {Elevation,6:F1}°";
            yield return $"Poziție        : ({p.X,6:F2}, {p.Y,6:F2}, {p.Z,6:F2})";
        }
    }
}
    
    


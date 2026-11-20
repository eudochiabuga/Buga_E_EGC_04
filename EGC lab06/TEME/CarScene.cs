using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;
namespace Egc.TEME
{
    public sealed class CarScene : Scene
    {
        private const string Restart = "reporneste masina";
        private const string ReverseOrbit= "inverseaza sensul sferei";

        private const float StartX = -4.5f, EndX = 4.5f; //capetele drumului
        private const float TravelTime = 3.0f; //secunde pentru un drum in sens invers
        private const float WheelRadius = 0.3f;
        private const float OrbitRadius = 2.2f; //distanta sferei fata de centrul masinii
        private const float OrbitSpeed = 90.0f;  //grade pe secunda
        private Tween _drive = null!;
        private SceneObject _car = null!;
        private SceneObject _orbit = null!;
        private SceneObject _sphere = null!;
        private readonly List<SceneObject> _wheelPivots = new();
        public override string Name => "Masina ";

        public override IEnumerable<string> ActionNames => new[] { Restart, ReverseOrbit };

        public override void Load(Input input)
        {
            CameraPosition = new Vector3(0.0f, 5.5f, 11.0f);
            CameraTarget = new Vector3(0.0f, 0.8f, 0.0f);
            input.Bind(Restart, Keys.R);
            input.Bind(ReverseOrbit, Keys.O);

            Mesh grid = AddMesh(Shapes.Grid(10, 1.0f, new Color4(0.30f, 0.33f, 0.38f, 1.0f)));
            Mesh cube = AddMesh(Shapes.ShadedCube());
            Mesh sphere = AddMesh(Shapes.ShadedSphere(1.0f, 32, 16));
            Mesh cylinder = AddMesh(Shapes.ShadedCylinder(0.5f, 1.0f, 24));   // diametru 1, înălțime 1, axa pe Y

            AddObject("podea", grid);

            SceneObject road = AddObject("drum", cube);
            road.Transform.Position = new Vector3(0.0f, 0.01f, 0.0f);
            road.Transform.Scale = new Vector3(13.0f, 0.02f, 2.6f);
            road.Tint = new Color4(0.25f, 0.25f, 0.28f, 1.0f);

            BuildCar(cube, cylinder);
            BuildOrbit(sphere);
            // Mișcarea dus-întors: de la StartX la EndX și înapoi, la nesfârșit.
            _drive = new Tween(new Vector3(StartX, 0.0f, 0.0f), new Vector3(EndX, 0.0f, 0.0f),
                               TravelTime, Easing.EaseInOut, pingPong: true);
            _car.Transform.Position = _drive.From;
        }

        // --- Mașina: un pivot cu forme-copil --------------------------------------------------------

        private void BuildCar(Mesh cube, Mesh cylinder)
        {
            _car = AddObject("mașina", null);            // pivot: doar poartă poziția; nu se vede

            SceneObject body = AddObject("caroserie", cube, _car);
            body.Transform.Position = new Vector3(0.0f, 0.6f, 0.0f);
            body.Transform.Scale = new Vector3(2.2f, 0.5f, 1.0f);
            body.Tint = new Color4(0.9f, 0.2f, 0.2f, 1.0f);

            SceneObject cabin = AddObject("cabină", cube, _car);
            cabin.Transform.Position = new Vector3(-0.2f, 1.05f, 0.0f);
            cabin.Transform.Scale = new Vector3(1.1f, 0.4f, 0.9f);
            cabin.Tint = new Color4(0.6f, 0.8f, 1.0f, 1.0f);

            // Faruri pe partea din față (+X), ca să se vadă încotro "privește" mașina.
            foreach (float z in new[] { -0.3f, 0.3f })
            {
                SceneObject light = AddObject("far", cube, _car);
                light.Transform.Position = new Vector3(1.1f, 0.6f, z);
                light.Transform.Scale = new Vector3(0.1f);
                light.Tint = new Color4(1.0f, 0.95f, 0.5f, 1.0f);
            }

            foreach (float x in new[] { -0.7f, 0.7f })
            {
                foreach (float z in new[] { -0.62f, 0.62f })
                {
                    BuildWheel(cube, cylinder, new Vector3(x, WheelRadius, z));
                }
            }
        }

        private void BuildWheel(Mesh cube, Mesh cylinder, Vector3 position)
        {
            // Pivotul roții stă în centrul ei și se rotește în jurul axei Z (axa roților).
            SceneObject pivot = AddObject("pivot roată", null, _car);
            pivot.Transform.Position = position;
            _wheelPivots.Add(pivot);

            // Cilindrul are axa pe Y: îl culcăm cu o rotație de 90° în jurul lui X, ca axa lui să devină Z.
            SceneObject tire = AddObject("roată", cylinder, pivot);
            tire.Transform.Rotation = new Vector3(90.0f, 0.0f, 0.0f);
            tire.Transform.Scale = new Vector3(2.0f * WheelRadius, 0.22f, 2.0f * WheelRadius);
            tire.Tint = new Color4(0.12f, 0.12f, 0.14f, 1.0f);

            // O bară deschisă la culoare, care iese puțin pe ambele fețe: fără ea rotirea roții nu s-ar vedea.
            SceneObject bar = AddObject("spiță", cube, pivot);
            bar.Transform.Scale = new Vector3(0.5f, 0.08f, 0.26f);
            bar.Tint = new Color4(0.85f, 0.85f, 0.9f, 1.0f);
        }

            // --- Sfera pe orbită ------------------------------------------------------------------------

    private void BuildOrbit(Mesh sphere)
        {
            // Pivotul orbitei: copilul mașinii, în centrul ei. Spin pe Y = se rotește continuu.
            _orbit = AddObject("orbita sferei", null, _car);
            _orbit.Transform.Position = new Vector3(0.0f, 0.6f, 0.0f);
            _orbit.Spin = new Vector3(0.0f, OrbitSpeed, 0.0f);

            // Sfera: copilul pivotului, la distanța OrbitRadius de centru.
            _sphere = AddObject("sferă", sphere, _orbit);
            _sphere.Transform.Position = new Vector3(OrbitRadius, 0.0f, 0.0f);
            _sphere.Transform.Scale = new Vector3(0.35f);
            _sphere.Tint = new Color4(1.0f, 0.55f, 0.2f, 1.0f);
        }

        // --- Actualizarea, la fiecare cadru -----------------------------------------------------------

        public override void Update(float dt, Input input)
        {
            base.Update(dt, input);   // Spin: rotește pivotul orbitei

            if (input.WasPressed(Restart))
            {
                _drive.Restart();
                _orbit.Transform.Rotation = Vector3.Zero;
                ConsolePanel.PrintMessage("mașina a repornit");
            }
            if (input.WasPressed(ReverseOrbit))
            {
                _orbit.Spin = -_orbit.Spin;
                ConsolePanel.PrintMessage(_orbit.Spin.Y > 0.0f ? "sfera: sens trigonometric" : "sfera: sens orar");
            }

            // Mașina: valoarea tween-ului devine poziția pivotului (formele-copil o urmează).
            _drive.Update(dt);
            _car.Transform.Position = _drive.Value;

            // Roțile: rostogolire fără alunecare, arcul parcurs = rază × unghi, deci unghi = distanță / rază.
            // Semnul minus: rotația pozitivă în jurul lui Z este trigonometrică, iar o roată care merge spre +X
            // se rotește în sens orar (privind dinspre +Z).
            float distance = _car.Transform.Position.X - StartX;
            float wheelAngle = -MathHelper.RadiansToDegrees(distance / WheelRadius);
            foreach (SceneObject pivot in _wheelPivots)
            {
                pivot.Transform.Rotation = new Vector3(0.0f, 0.0f, wheelAngle);
            }
        }

        public override IEnumerable<string> Describe()
        {
            foreach (string line in base.Describe()) yield return line;

            float orbitAngle = (_orbit.Transform.Rotation.Y % 360.0f + 360.0f) % 360.0f;
            yield return $"Mașina        : x = {_car.Transform.Position.X:F2}   progres liniar {_drive.Progress:F2}";
            yield return $"Sfera         : unghi {orbitAngle:F0}°   poziție în lume {Fmt(_sphere.WorldPosition)}";
        }

        private static string Fmt(Vector3 v) => $"({v.X:F2}, {v.Y:F2}, {v.Z:F2})";
    }

}

    

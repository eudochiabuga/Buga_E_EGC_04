using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace Egc;

public sealed class ObstacleScene : Scene
{
    //Actiunile scenei
    private const string MoveLeft = "banda la stanga";
    private const string MoveRight = "banda la dreapta";
    private const string Jump = "salt";
    private const string NewGame = "joc nou";

    //drumul si jucatorul
    private const int LaneCount = 3;
    private const float LaneWidth = 2.5f;
    private const float GroundY = 0.02f; //suprafata drumului
    private const float PlayerZ = 2.5f; //jucatorul sta pe loc
    private const float PlayerHalf = 0.4f; //jumatate din latura cubului jucatorului
    private const float LaneChangeTime = 0.15f; //sub 0.2 s

    //saltul
    private const float Gravity = 20.0f;
    private const float JumpSpeed = 8.0f;

    //lumea
    private const float BaseSpeed = 10.0f, SpeedGrowth = 0.5f, MaxSpeed = 32.0f;  //unitati/s
    private const float StartInterval = 1.3f, IntervalShrink = 0.015f, MinInterval = 0.5f; //secunde
    private const float FirstSpawnDelay = 1.0f;
    private const float SpawnZ = -55.0f, DespawnZ = 7.0f;

    //obstacolele
    private const float LowHeight = 0.7f, HighHeight = 2.5f;
    private const float ObstacleWidth = 1.6f, ObstacleDepth = 0.8f;
    private const int PoolSize = 12;

    //maracaje pe drum
    private const int MarkersPerLine = 10;
    private const float MarkerSpacing = 7.0f;
    private const float MarkerEndZ = 8.0f;

    private sealed class Obstacle
    {
        public SceneObject Body = null!;
        public bool Active;
        public float HalfHeight;
    }

    private readonly List<Obstacle> _obstacles = new();
    private readonly List<SceneObject> _markers = new();
    private readonly Random _rng = new();

    private SceneObject _player = null!;
    private SceneObject _shadow = null!;
    private Tween _laneTween = null!;


    private int _lane; //0,1,2
    private float _y; //inaltimea fata de drum(0 pe sol)
    private float _vy; //viteaza verticala
    private float _time; //scorul:timpul de joc supravietuit
    private float _best; //recordul
    private float _spawnTimer;
    private bool _over;

    public override string Name => "evita obstacolele";

    public override string Status => _over
        ? $"GAME OVER | scor {_time:F1} s | record {_best:F1} s | R= joc nou "
        : $"scor {_time:F1} s | record {_best:F1 s}";

    public override IEnumerable<string> ActionNames => new[] { MoveLeft, MoveRight, Jump, NewGame };

    private bool OnGround => _y <= 1e-4f;
    private float WorldSpeed => MathF.Min(MaxSpeed, BaseSpeed + SpeedGrowth * _time);
    private float SpawnInterval => MathF.Max(MinInterval, StartInterval - IntervalShrink * _time);
    private static float LaneX(int lane) => (lane - 1) * LaneWidth;

    public override void Load(Input input)
    {
        CameraPosition = new Vector3(0.0f, 4.2f, 9.5f);
        CameraTarget = new Vector3(0.0f, 0.6f, -8.0f);

        input.Bind(MoveLeft, Keys.A, Keys.Left);
        input.Bind(MoveRight, Keys.D, Keys.Right);
        input.Bind(Jump, Keys.Space, Keys.W, Keys.Up);
        input.Bind(NewGame, Keys.R);

        Mesh cube = AddMesh(Shapes.ShadedCube());
        Mesh disk = AddMesh(Shapes.ShadedCylinder(0.5f, 0.02f, 24));

        SceneObject ground = AddObject("pamant", cube);
        ground.Transform.Position = new Vector3(0.0f, -0.0f, -30.0f);
        ground.Transform.Scale = new Vector3(40.0f, 0.02f, 90.0f);
        ground.Tint = new Color4(0.16f, 0.22f, 0.18f, 1.0f);

        SceneObject round = AddObject("drum", cube);
        round.Transform.Position = new Vector3(0.0f, 0.01f, -30f);
        round.Transform.Scale = new Vector3(LaneCount * LaneWidth + 0.5f, 0.02f, 80.0f);
        round.Tint = new Color4(0.28f, 0.28f, 0.32f, 1.0f);

        BuildMarkers(cube);
        BuildObstaclePool(cube);
        BuildPlayer(cube, disk);

        _laneTween = new Tween(Vector3.Zero, Vector3.Zero, LaneChangeTime, Easing.EaseOut);

        StartGame();


    }

    //construirea scenei
    private void BuildMarkers(Mesh cube)
    {
        // Linii întrerupte între benzi (x = ±LaneWidth / 2). Se deplasează odată cu lumea.
        for (int k = 0; k < MarkersPerLine; k++)
        {
            foreach (float x in new[] { -0.5f * LaneWidth, 0.5f * LaneWidth })
            {
                SceneObject mark = AddObject("marcaj", cube);
                mark.Transform.Position = new Vector3(x, 0.03f, MarkerEndZ - 1.0f - k * MarkerSpacing);
                mark.Transform.Scale = new Vector3(0.1f, 0.02f, 1.2f);
                mark.Tint = new Color4(0.9f, 0.9f, 0.9f, 1.0f);
                _markers.Add(mark);
            }
        }
    }

    private void BuildObstaclePool(Mesh cube)
    {
        // Un "pool": obiecte create o dată, refolosite. Cele inactive sunt invizibile.
        for (int i = 0; i < PoolSize; i++)
        {
            SceneObject body = AddObject("obstacol", cube);
            body.Visible = false;
            _obstacles.Add(new Obstacle { Body = body });
        }
    }

    private void BuildPlayer(Mesh cube, Mesh disk)
    {
        _shadow = AddObject("umbra jucătorului", disk);
        _shadow.Tint = new Color4(0.05f, 0.05f, 0.08f, 1.0f);

        // Ierarhie: pivotul jucătorului (la baza cubului) are doi copii: corpul și casca.
        _player = AddObject("jucător", null);

        SceneObject body = AddObject("corp", cube, _player);
        body.Transform.Position = new Vector3(0.0f, PlayerHalf, 0.0f);
        body.Transform.Scale = new Vector3(2.0f * PlayerHalf);
        body.Tint = new Color4(0.3f, 0.85f, 1.0f, 1.0f);

        SceneObject hat = AddObject("cască", cube, _player);
        hat.Transform.Position = new Vector3(0.0f, 2.0f * PlayerHalf + 0.1f, 0.0f);
        hat.Transform.Scale = new Vector3(0.5f, 0.2f, 0.5f);
        hat.Tint = new Color4(1.0f, 0.85f, 0.25f, 1.0f);
    }

    //pornirea unui joc nou
    private void StartGame()
    {
        _time = 0.0f;
        _over = false;
        _lane = 1;
        _y = 0.0f;
        _vy = 0.0f;
        _spawnTimer = FirstSpawnDelay;

        _laneTween.From = Vector3.Zero;
        _laneTween.To = Vector3.Zero;
        _laneTween.Restart();

        foreach (Obstacle o in _obstacles) Deactivate(o);
        UpdatePlayerVisual();
        ConsolePanel.PrintMessage("joc nou");
    }

    //update la fiecare cadru
    public override void Update(float dt, Input input)
    {
        base.Update(dt, input);

        if (input.WasPressed(NewGame))
        {
            StartGame();
        }
        if (_over) return;   // jocul este oprit: doar R mai are efect

        // 1. Input: schimbarea benzii (apăsare) și saltul (apăsare).
        if (input.WasPressed(MoveLeft) && _lane > 0) ChangeLane(-1);
        if (input.WasPressed(MoveRight) && _lane < LaneCount - 1) ChangeLane(+1);
        if (OnGround && input.WasPressed(Jump)) _vy = JumpSpeed;

        // 2. Timpul și lumea: obstacolele și marcajele vin spre jucător.
        _time += dt;
        float step = WorldSpeed * dt;            // cât se deplasează lumea în acest cadru
        MoveWorld(step, dt);

        // 3. Jucătorul: tween-ul benzii și gravitația.
        _laneTween.Update(dt);
        if (!OnGround || _vy > 0.0f)
        {
            _vy -= Gravity * dt;                 // întâi viteza ...
            _y += _vy * dt;                      // ... apoi poziția (Euler semi-implicit)
            if (_y <= 0.0f)
            {
                _y = 0.0f;
                _vy = 0.0f;                      // a aterizat
            }
        }
        UpdatePlayerVisual();

        // 4. Coliziunile (după ce totul s-a mutat).
        CheckCollisions(step);
    }

    private void ChangeLane(int direction)
    {
        _lane += direction;
        // GoTo pornește din poziția curentă (Value), deci o schimbare întreruptă continuă lin.
        _laneTween.GoTo(new Vector3(LaneX(_lane), 0.0f, 0.0f), LaneChangeTime);
    }

    private void MoveWorld(float step, float dt)
    {
        _spawnTimer -= dt;
        if (_spawnTimer <= 0.0f)
        {
            Spawn();
            _spawnTimer = SpawnInterval;
        }

        foreach (Obstacle o in _obstacles)
        {
            if (!o.Active) continue;
            Vector3 p = o.Body.Transform.Position;
            p.Z += step;
            o.Body.Transform.Position = p;
            if (p.Z > DespawnZ) Deactivate(o);
        }

        // Marcajele: la ieșirea din dreptul camerei revin în capătul îndepărtat.
        foreach (SceneObject mark in _markers)
        {
            Vector3 p = mark.Transform.Position;
            p.Z += step;
            if (p.Z > MarkerEndZ) p.Z -= MarkersPerLine * MarkerSpacing;
            mark.Transform.Position = p;
        }
    }

    private void Spawn()
    {
        Obstacle? free = _obstacles.FirstOrDefault(o => !o.Active);
        if (free == null) return;

        bool high = _rng.NextDouble() < 0.4;
        float height = high ? HighHeight : LowHeight;
        int lane = _rng.Next(LaneCount);

        free.Active = true;
        free.HalfHeight = 0.5f * height;
        free.Body.Visible = true;
        free.Body.Transform.Scale = new Vector3(ObstacleWidth, height, ObstacleDepth);
        free.Body.Transform.Position = new Vector3(LaneX(lane), GroundY + free.HalfHeight, SpawnZ);
        free.Body.Tint = high ? new Color4(0.9f, 0.2f, 0.25f, 1.0f)     // înalt: roșu (se ocolește)
                              : new Color4(1.0f, 0.65f, 0.15f, 1.0f);   // jos: portocaliu (se sare)
    }

    private static void Deactivate(Obstacle o)
    {
        o.Active = false;
        o.Body.Visible = false;
    }


    private void UpdatePlayerVisual()
    {
        float x = _laneTween.Value.X;
        _player.Transform.Position = new Vector3(x, GroundY + _y, PlayerZ);

        // Umbra rămâne pe drum și se micșorează cu înălțimea (ajută la judecarea saltului).
        float shadowSize = 0.9f / (1.0f + 0.4f * _y);
        _shadow.Transform.Position = new Vector3(x, 0.05f, PlayerZ);
        _shadow.Transform.Scale = new Vector3(shadowSize, 1.0f, shadowSize);
    }

    //coliziunea
    private static bool Overlap(float centerA, float halfA, float centerB, float halfB) =>
        MathF.Abs(centerA - centerB) < halfA + halfB;

    private void CheckCollisions(float step)
    {
        float px = _laneTween.Value.X;
        float py = GroundY + _y + PlayerHalf;    // centrul cubului jucătorului

        foreach (Obstacle o in _obstacles)
        {
            if (!o.Active) continue;
            Vector3 c = o.Body.Transform.Position;

            // Pe axa Z cutia obstacolului este "întinsă" pe drumul parcurs în cadru (de la z − step la z),
            // ca un obstacol rapid să nu poată trece prin jucător între două cadre.
            float zCenter = c.Z - 0.5f * step;
            float zHalf = 0.5f * ObstacleDepth + 0.5f * step;

            if (Overlap(px, PlayerHalf, c.X, 0.5f * ObstacleWidth) &&
                Overlap(py, PlayerHalf, c.Y, o.HalfHeight) &&
                Overlap(PlayerZ, PlayerHalf, zCenter, zHalf))
            {
                GameOver();
                return;
            }
        }
    }

    private void GameOver()
    {
        _over = true;
        if (_time > _best) _best = _time;
        ConsolePanel.PrintMessage($"coliziune! scor {_time:F1} s, record {_best:F1} s (R = joc nou)");
    }



    //afisare
    public override IEnumerable<string> Describe()
    {
        foreach (string line in base.Describe()) yield return line;

        int active = _obstacles.Count(o => o.Active);
        yield return $"Joc           : {(_over ? "GAME OVER" : "în desfășurare")}   scor {_time:F1} s   record {_best:F1} s";
        yield return $"Lume          : viteză {WorldSpeed:F1} u/s   interval între obstacole {SpawnInterval:F2} s   active {active}";
        yield return $"Jucător       : banda {_lane}   x = {_laneTween.Value.X:F2}   y = {_y:F2}   v_y = {_vy:F2}   {(OnGround ? "pe sol" : "în aer")}";
    }
}

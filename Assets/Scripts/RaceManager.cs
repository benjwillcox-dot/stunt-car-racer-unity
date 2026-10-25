using UnityEngine;

namespace StuntCar
{
    /// <summary>
    /// Menu, countdown, race flow and the HUD. Races are three laps, head to head against one
    /// rival, scored as in the original: two points for the win, one for the fastest lap.
    /// </summary>
    public class RaceManager : MonoBehaviour
    {
        enum State { Menu, Countdown, Racing, Finished }
        enum GameMode { Race, Practice }

        const int Laps = 3;
        const float MphPerMs = 2.23694f;

        // Rivals get faster with the division (4 = easiest).
        static readonly string[] RivalNames = { "Steel Eye", "Max Boost", "Crusher Clyde", "Jumping Jess" };
        static readonly float[] RivalSkill = { 0.98f, 0.94f, 0.89f, 0.84f };

        GameBootstrap assets;
        CameraRig rig;
        Track track;
        RaceCar player, rival;
        State state;
        GameMode mode = GameMode.Race;
        int selected;
        float stateStart;
        float raceStart;
        string banner;
        float bannerUntil;
        string resultTitle, resultBody;
        Vector3 trackCentre;
        float trackRadius;

        void Start()
        {
            assets = GameBootstrap.Instance;
            var cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
            }
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 3000f;
            rig = cam.gameObject.AddComponent<CameraRig>();
            ShowMenu();
        }

        // ---------------------------------------------------------------- flow

        void ShowMenu()
        {
            ClearCars();
            LoadTrack(selected);
            rig.Target = null;
            rig.enabled = false;
            state = State.Menu;
        }

        void LoadTrack(int i)
        {
            if (track && track.Definition == TrackLibrary.All[i]) return;
            if (track) Destroy(track.gameObject);
            track = new GameObject("Track").AddComponent<Track>();
            track.Build(TrackLibrary.All[i], assets);

            var b = new Bounds(track.Center(0), Vector3.zero);
            for (int s = 0; s < track.Count; s++) b.Encapsulate(track.Center(s));
            trackCentre = b.center;
            trackRadius = b.extents.magnitude;
        }

        void ClearCars()
        {
            if (player) Destroy(player.gameObject);
            if (rival) Destroy(rival.gameObject);
            player = rival = null;
        }

        void StartRace()
        {
            ClearCars();
            LoadTrack(selected);

            if (mode == GameMode.Race)
            {
                int division = TrackLibrary.All[selected].Division;
                int r = Mathf.Clamp(division - 1, 0, RivalNames.Length - 1);
                player = SpawnCar("You", assets.playerPaint, true, -1.9f, 0f);
                rival = SpawnCar(RivalNames[r], assets.opponentPaint, false, 1.9f, RivalSkill[r]);
            }
            else
            {
                player = SpawnCar("You", assets.playerPaint, true, 0f, 0f);
            }

            rig.enabled = true;
            rig.Target = player.transform;
            rig.Snap();
            SetLocked(true);
            state = State.Countdown;
            stateStart = Time.time;
            banner = null;
        }

        RaceCar SpawnCar(string name, Material paint, bool isPlayer, float lateral, float skill)
        {
            var car = CarFactory.Create(name, paint, assets);
            var rc = car.gameObject.AddComponent<RaceCar>();
            rc.Init(track, car, name, isPlayer);
            if (isPlayer) car.gameObject.AddComponent<PlayerDriver>();
            else car.gameObject.AddComponent<AIDriver>().Init(track, skill);
            car.gameObject.AddComponent<CarAudio>().Init(car, isPlayer);
            rc.PlaceOnGrid(2, lateral);
            rc.LapCompleted += OnLapCompleted;
            if (isPlayer) car.Damaged += d => rig.Shake(d / 12f);
            return rc;
        }

        void SetLocked(bool locked)
        {
            if (player) player.Car.Locked = locked;
            if (rival) rival.Car.Locked = locked;
        }

        void OnLapCompleted(RaceCar rc, float lapTime)
        {
            if (mode == GameMode.Practice)
            {
                rc.Car.BoostRemaining = rc.Car.boostCapacity;
                Banner($"LAP {FormatTime(lapTime)}", 2.5f);
                return;
            }
            if (state != State.Racing) return;

            if (rc.LapsCompleted >= Laps && !rc.Finished)
            {
                rc.Finished = true;
                rc.FinishTime = Time.time - raceStart;
                rc.Car.Locked = true;
                if (rc == player) FinishRace();
                else Banner($"{rc.DriverName} HAS FINISHED", 3f);
            }
            else if (rc == player)
            {
                Banner(rc.LapsCompleted == Laps - 1 ? "FINAL LAP" : $"LAP {rc.LapsCompleted + 1}", 2f);
            }
        }

        void FinishRace()
        {
            state = State.Finished;
            SetLocked(true);

            bool playerFinished = player.Finished && !player.Car.Wrecked;
            bool rivalFinished = rival && rival.Finished && !rival.Car.Wrecked;
            bool won = mode == GameMode.Race && playerFinished && (!rivalFinished || player.FinishTime <= rival.FinishTime);

            float rivalBest = rival ? rival.BestLap : float.PositiveInfinity;
            bool fastestLap = player.BestLap < rivalBest;
            int points = (won ? 2 : 0) + (fastestLap && playerFinished ? 1 : 0);

            if (player.Car.Wrecked) resultTitle = "WRECKED";
            else resultTitle = won ? "YOU WIN!" : "YOU LOSE";

            resultBody =
                $"Your time:  {(playerFinished ? FormatTime(player.FinishTime) : "DNF")}\n" +
                (rival ? $"{rival.DriverName}:  {(rivalFinished ? FormatTime(rival.FinishTime) : "still racing")}\n" : "") +
                $"Your best lap:  {FormatTime(player.BestLap)}\n" +
                (rival ? $"{rival.DriverName} best lap:  {FormatTime(rival.BestLap)}\n" : "") +
                $"Crane recoveries:  {player.Recoveries}\n" +
                $"Chassis damage:  {Mathf.RoundToInt(player.Car.Damage)}%\n\n" +
                $"POINTS:  {points}  (win 2, fastest lap 1)";
        }

        void Banner(string text, float seconds)
        {
            banner = text;
            bannerUntil = Time.time + seconds;
        }

        void Update()
        {
            switch (state)
            {
                case State.Menu: UpdateMenu(); break;
                case State.Countdown: UpdateCountdown(); break;
                case State.Racing: UpdateRacing(); break;
                case State.Finished:
                    if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) StartRace();
                    if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.M)) ShowMenu();
                    break;
            }
        }

        void UpdateMenu()
        {
            int n = TrackLibrary.All.Length;
            if (Input.GetKeyDown(KeyCode.DownArrow)) { selected = (selected + 1) % n; LoadTrack(selected); }
            if (Input.GetKeyDown(KeyCode.UpArrow)) { selected = (selected - 1 + n) % n; LoadTrack(selected); }
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.RightArrow))
                mode = mode == GameMode.Race ? GameMode.Practice : GameMode.Race;
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) { StartRace(); return; }

            // Slow orbit around the selected circuit.
            var cam = rig.transform;
            float a = Time.time * 0.08f;
            float dist = trackRadius * 1.1f + 40f;
            cam.position = trackCentre + new Vector3(Mathf.Cos(a) * dist, trackRadius * 0.45f + 25f, Mathf.Sin(a) * dist);
            cam.rotation = Quaternion.LookRotation(trackCentre - cam.position);
        }

        void UpdateCountdown()
        {
            if (Input.GetKeyDown(KeyCode.Escape)) { ShowMenu(); return; }
            if (Time.time - stateStart >= 3f)
            {
                state = State.Racing;
                raceStart = Time.time;
                SetLocked(false);
                if (player) player.StartTiming(raceStart);
                if (rival) rival.StartTiming(raceStart);
                Banner("GO!", 1f);
            }
        }

        void UpdateRacing()
        {
            if (Input.GetKeyDown(KeyCode.Escape)) { ShowMenu(); return; }

            if (player.Car.Wrecked)
            {
                FinishRace();
                return;
            }
            if (rival && rival.Car.Wrecked && !rival.Finished)
            {
                rival.Finished = true;
                rival.FinishTime = float.PositiveInfinity;
                Banner($"{rival.DriverName} IS WRECKED!", 3f);
            }
        }

        // ---------------------------------------------------------------- HUD

        GUIStyle big, medium, small, left, right;
        static readonly float[] Crack = MakeCrack();

        static float[] MakeCrack()
        {
            var rng = new System.Random(42);
            var c = new float[80];
            float y = 0f;
            for (int i = 0; i < c.Length; i++)
            {
                y = Mathf.Clamp(y + (float)(rng.NextDouble() * 2 - 1) * 5f, -11f, 11f);
                c[i] = y;
            }
            return c;
        }

        void EnsureStyles()
        {
            if (big != null) return;
            big = new GUIStyle(GUI.skin.label) { fontSize = 64, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            medium = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            small = new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.MiddleCenter };
            left = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperLeft };
            right = new GUIStyle(left) { alignment = TextAnchor.UpperRight };
            foreach (var s in new[] { big, medium, small, left, right }) s.normal.textColor = Color.white;
        }

        void OnGUI()
        {
            EnsureStyles();
            float scale = Screen.height / 720f;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float w = Screen.width / scale, h = 720f;

            if (state == State.Menu) { DrawMenu(w, h); return; }

            DrawHud(w, h);

            if (state == State.Countdown)
            {
                int n = 3 - Mathf.FloorToInt(Time.time - stateStart);
                Shadowed(new Rect(0, h * 0.3f, w, 100), Mathf.Max(1, n).ToString(), big);
            }
            if (banner != null && Time.time < bannerUntil)
                Shadowed(new Rect(0, h * 0.3f, w, 100), banner, big);
            if (player && player.Recovery.InProgress)
                Shadowed(new Rect(0, h * 0.45f, w, 40), "CRANE RECOVERY", medium);

            if (state == State.Finished)
            {
                Box(new Rect(w / 2 - 260, h / 2 - 190, 520, 380), new Color(0, 0, 0, 0.75f));
                Shadowed(new Rect(0, h / 2 - 180, w, 70), resultTitle, big);
                GUI.Label(new Rect(w / 2 - 230, h / 2 - 100, 460, 250), resultBody, left);
                Shadowed(new Rect(0, h / 2 + 150, w, 30), "Enter: race again     Esc: menu", small);
            }
        }

        void DrawHud(float w, float h)
        {
            if (!player) return;
            var car = player.Car;

            // Chassis damage: the crack spreads across the top of the screen.
            var panel = new Rect(w / 2 - 260, 14, 520, 44);
            Box(panel, new Color(0, 0, 0, 0.45f));
            GUI.Label(new Rect(panel.x + 8, panel.y + 2, 200, 20), "CHASSIS", new GUIStyle(small) { alignment = TextAnchor.UpperLeft, fontSize = 12 });
            int segs = Mathf.RoundToInt(car.Damage / 100f * Crack.Length);
            float segW = (panel.width - 20) / Crack.Length;
            for (int i = 0; i < segs; i++)
            {
                float y = panel.center.y + 4 + Crack[i];
                Box(new Rect(panel.x + 10 + i * segW, y - 2, segW + 1, 4), new Color(1f, 0.25f, 0.15f));
            }

            // Lap and times.
            float now = Time.time;
            string lapText = mode == GameMode.Race
                ? $"LAP {Mathf.Min(player.LapsCompleted + 1, Laps)}/{Laps}"
                : $"PRACTICE  LAP {player.LapsCompleted + 1}";
            float current = state == State.Racing ? now - player.CurrentLapStart : 0f;
            GUI.Label(new Rect(20, 14, 400, 140),
                $"{lapText}\nTIME  {FormatTime(current)}\nLAST  {FormatTime(player.LastLap)}\nBEST  {FormatTime(player.BestLap)}", left);

            // Position against the rival.
            if (rival)
            {
                bool ahead = player.Progress >= rival.Progress || (rival.Car.Wrecked && !player.Car.Wrecked);
                if (player.Finished && rival.Finished) ahead = player.FinishTime <= rival.FinishTime;
                GUI.Label(new Rect(w - 420, 14, 400, 80), $"{(ahead ? "1ST" : "2ND")}\nv {rival.DriverName}", right);
            }

            // Boost.
            var boostRect = new Rect(20, h - 70, 260, 26);
            Box(boostRect, new Color(0, 0, 0, 0.45f));
            float frac = car.boostCapacity > 0 ? car.BoostRemaining / car.boostCapacity : 0;
            Box(new Rect(boostRect.x + 3, boostRect.y + 3, (boostRect.width - 6) * frac, boostRect.height - 6),
                car.IsBoosting ? new Color(1f, 0.85f, 0.2f) : new Color(1f, 0.5f, 0.1f));
            GUI.Label(new Rect(20, h - 100, 260, 26), "BOOST", left);

            // Speed.
            int mph = Mathf.RoundToInt(Mathf.Abs(car.Speed) * MphPerMs);
            GUI.Label(new Rect(w - 300, h - 110, 280, 70), mph.ToString(), new GUIStyle(right) { fontSize = 56 });
            GUI.Label(new Rect(w - 300, h - 45, 280, 30), "MPH", right);

            Shadowed(new Rect(0, h - 28, w, 24), "Arrows/WASD drive   Space boost   C camera   R crane   Esc menu", small);
        }

        void DrawMenu(float w, float h)
        {
            Shadowed(new Rect(0, 40, w, 80), "STUNT CAR RACER", big);
            Shadowed(new Rect(0, 110, w, 30), "Unity remake - prototype", small);

            var defs = TrackLibrary.All;
            float y = 170;
            for (int i = 0; i < defs.Length; i++)
            {
                var r = new Rect(w / 2 - 230, y, 460, 44);
                Box(r, i == selected ? new Color(0.85f, 0.15f, 0.1f, 0.85f) : new Color(0, 0, 0, 0.55f));
                if (GUI.Button(r, GUIContent.none, GUIStyle.none)) { selected = i; LoadTrack(i); }
                GUI.Label(new Rect(r.x + 14, r.y + 8, 300, 30), defs[i].Name, left);
                GUI.Label(new Rect(r.x, r.y + 8, r.width - 14, 30), $"Division {defs[i].Division}", right);
                y += 52;
            }

            Shadowed(new Rect(0, y + 4, w, 44), defs[selected].Description, small);

            var modeRect = new Rect(w / 2 - 230, y + 50, 460, 44);
            Box(modeRect, new Color(0, 0, 0, 0.55f));
            if (GUI.Button(modeRect, GUIContent.none, GUIStyle.none)) mode = mode == GameMode.Race ? GameMode.Practice : GameMode.Race;
            Shadowed(modeRect, mode == GameMode.Race ? "<  RACE v CPU (3 laps)  >" : "<  PRACTICE  >", medium);

            var startRect = new Rect(w / 2 - 130, y + 110, 260, 54);
            Box(startRect, new Color(0.85f, 0.15f, 0.1f, 0.9f));
            if (GUI.Button(startRect, GUIContent.none, GUIStyle.none)) StartRace();
            Shadowed(startRect, "START", medium);

            Shadowed(new Rect(0, h - 60, w, 24), "Up/Down: track    Left/Right: mode    Enter: start", small);
            Shadowed(new Rect(0, h - 34, w, 24), "Drive: Arrows/WASD   Boost: Space   Camera: C   Crane: R", small);
        }

        static void Box(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }

        static void Shadowed(Rect r, string text, GUIStyle style)
        {
            var old = style.normal.textColor;
            style.normal.textColor = new Color(0, 0, 0, 0.7f);
            GUI.Label(new Rect(r.x + 2, r.y + 2, r.width, r.height), text, style);
            style.normal.textColor = old;
            GUI.Label(r, text, style);
        }

        static string FormatTime(float t)
        {
            if (float.IsInfinity(t) || float.IsNaN(t)) return "--:--.--";
            int m = (int)(t / 60f);
            float s = t - m * 60;
            return $"{m}:{s:00.00}";
        }
    }
}

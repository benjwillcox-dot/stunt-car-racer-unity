using UnityEngine;

namespace StuntCar
{
    /// <summary>
    /// Menu, league table, countdown, race flow and the HUD. Races are three laps, head to
    /// head against one rival, scored as in the original: two points for the win, one for
    /// the fastest lap.
    /// </summary>
    public class RaceManager : MonoBehaviour
    {
        enum State { Menu, LeagueTable, Countdown, Racing, Finished }
        enum GameMode { League, Race, Practice }

        const int Laps = 3;
        const float MphPerMs = 2.23694f;

        // Single-race rivals get faster with the division (index 0 = Division 1).
        static readonly string[] RivalNames = { "Steel Eye", "Turbo Tess", "Crusher Clyde", "Jumping Jess" };
        static readonly float[] RivalSkill = { 0.98f, 0.93f, 0.88f, 0.84f };

        GameBootstrap assets;
        CameraRig rig;
        Track track;
        RaceCar player, rival;
        League league;
        State state;
        GameMode mode = GameMode.League;
        int selected;
        float stateStart;
        float raceStart;
        string banner;
        float bannerUntil;
        string resultTitle, resultBody;
        Vector3 trackCentre;
        float trackRadius;
        bool confirmReset;

        void Start()
        {
            assets = GameBootstrap.Instance;
            league = League.LoadOrCreate();
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

        TrackDefinition MenuTrack => mode == GameMode.League ? league.NextTrack : TrackLibrary.All[selected];

        void ShowMenu()
        {
            ClearCars();
            LoadTrack(MenuTrack);
            rig.Target = null;
            rig.enabled = false;
            state = State.Menu;
        }

        void ShowLeagueTable()
        {
            ClearCars();
            LoadTrack(league.NextTrack);
            rig.Target = null;
            rig.enabled = false;
            confirmReset = false;
            state = State.LeagueTable;
        }

        void LoadTrack(TrackDefinition def)
        {
            if (track && track.Definition == def) return;
            if (track) Destroy(track.gameObject);
            track = new GameObject("Track").AddComponent<Track>();
            track.Build(def, assets);

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
            LoadTrack(MenuTrack);

            if (mode == GameMode.League)
            {
                var r = league.NextRival;
                player = SpawnCar("You", assets.playerPaint, true, -1.9f, 0f);
                player.Car.SetDamage(league.carriedDamage);
                rival = SpawnCar(r.name, assets.opponentPaint, false, 1.9f, r.skill);
            }
            else if (mode == GameMode.Race)
            {
                int r = Mathf.Clamp(track.Definition.Division - 1, 0, RivalNames.Length - 1);
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
            car.gameObject.AddComponent<CarEffects>().Init(car);
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

        void FinishRace(bool retired = false)
        {
            state = State.Finished;
            SetLocked(true);

            bool playerFinished = !retired && player.Finished && !player.Car.Wrecked;
            bool rivalFinished = rival && rival.Finished && !rival.Car.Wrecked;
            bool won = mode != GameMode.Practice && playerFinished && (!rivalFinished || player.FinishTime <= rival.FinishTime);

            float rivalBest = rival ? rival.BestLap : float.PositiveInfinity;
            bool fastestLap = !retired && player.BestLap < rivalBest;
            int points = (won ? 2 : 0) + (fastestLap ? 1 : 0);

            if (retired) resultTitle = "RETIRED";
            else if (player.Car.Wrecked) resultTitle = "WRECKED";
            else resultTitle = won ? "YOU WIN!" : "YOU LOSE";

            resultBody =
                $"Your time:  {(playerFinished ? FormatTime(player.FinishTime) : "DNF")}\n" +
                (rival ? $"{rival.DriverName}:  {(rivalFinished ? FormatTime(rival.FinishTime) : rival.Car.Wrecked ? "wrecked" : "still racing")}\n" : "") +
                $"Your best lap:  {FormatTime(player.BestLap)}\n" +
                (rival ? $"{rival.DriverName} best lap:  {FormatTime(rival.BestLap)}\n" : "") +
                $"Crane recoveries:  {player.Recoveries}\n" +
                $"Chassis damage:  {Mathf.RoundToInt(player.Car.Damage)}%\n\n" +
                $"POINTS:  {points}  (win 2, fastest lap 1)";

            if (mode == GameMode.League)
            {
                league.RecordPlayerRace(won, fastestLap, player.Car.Damage, player.Car.Wrecked);
                resultBody += league.raceIndex == 0 ? "\n\nSEASON OVER - see the league table" : "\nDamage carries into your next race.";
            }
        }

        void Banner(string text, float seconds)
        {
            banner = text;
            bannerUntil = Time.time + seconds;
        }

        void Update()
        {
            bool racing = state == State.Countdown || state == State.Racing;
            bool enter = !racing && GameInput.ConfirmPressed();
            bool back = racing ? GameInput.PausePressed() : GameInput.BackPressed();

            switch (state)
            {
                case State.Menu: UpdateMenu(enter); OrbitCamera(); break;
                case State.LeagueTable:
                    if (enter) StartRace();
                    else if (back) ShowMenu();
                    OrbitCamera();
                    break;
                case State.Countdown:
                    if (back) { LeaveRace(); return; }
                    UpdateCountdown();
                    break;
                case State.Racing:
                    if (back) { LeaveRace(); return; }
                    UpdateRacing();
                    break;
                case State.Finished:
                    if (enter) { if (mode == GameMode.League) ShowLeagueTable(); else StartRace(); }
                    else if (back) { if (mode == GameMode.League) ShowLeagueTable(); else ShowMenu(); }
                    break;
            }
        }

        // Leaving a league race counts as retiring; elsewhere it just goes back to the menu.
        void LeaveRace()
        {
            if (mode == GameMode.League && state == State.Racing) FinishRace(true);
            else if (mode == GameMode.League) ShowLeagueTable();
            else ShowMenu();
        }

        void UpdateMenu(bool enter)
        {
            int n = TrackLibrary.All.Length;
            int v = GameInput.MenuVertical(), h = GameInput.MenuHorizontal();
            if (mode != GameMode.League)
            {
                if (v < 0) { selected = (selected + 1) % n; LoadTrack(MenuTrack); }
                if (v > 0) { selected = (selected - 1 + n) % n; LoadTrack(MenuTrack); }
            }
            if (h != 0) CycleMode(h > 0 ? 1 : -1);
            if (enter) StartFromMenu();
        }

        void CycleMode(int dir)
        {
            mode = (GameMode)(((int)mode + dir + 3) % 3);
            LoadTrack(MenuTrack);
        }

        void StartFromMenu()
        {
            if (mode == GameMode.League) ShowLeagueTable();
            else StartRace();
        }

        void OrbitCamera()
        {
            var cam = rig.transform;
            float a = Time.time * 0.08f;
            float dist = trackRadius * 1.1f + 40f;
            cam.position = trackCentre + new Vector3(Mathf.Cos(a) * dist, trackRadius * 0.45f + 25f, Mathf.Sin(a) * dist);
            cam.rotation = Quaternion.LookRotation(trackCentre - cam.position);
        }

        void UpdateCountdown()
        {
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
            if (state == State.LeagueTable) { DrawLeague(w, h); return; }

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
                Box(new Rect(w / 2 - 270, h / 2 - 210, 540, 420), new Color(0, 0, 0, 0.75f));
                Shadowed(new Rect(0, h / 2 - 200, w, 70), resultTitle, big);
                GUI.Label(new Rect(w / 2 - 240, h / 2 - 120, 480, 290), resultBody, left);
                string hint = mode == GameMode.League ? "Enter: league table" : "Enter: race again     Esc: menu";
                Shadowed(new Rect(0, h / 2 + 175, w, 30), hint, small);
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
            string lapText = mode == GameMode.Practice
                ? $"PRACTICE  LAP {player.LapsCompleted + 1}"
                : $"LAP {Mathf.Min(player.LapsCompleted + 1, Laps)}/{Laps}";
            float current = state == State.Racing ? Time.time - player.CurrentLapStart : 0f;
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

            string esc = mode == GameMode.League ? "Esc retire" : "Esc menu";
            Shadowed(new Rect(0, h - 28, w, 24), $"Arrows/WASD or pad: drive   Space/A: boost   C/Y: camera   R/B: crane   {esc}", small);
        }

        void DrawMenu(float w, float h)
        {
            Shadowed(new Rect(0, 30, w, 80), "STUNT CAR RACER", big);
            Shadowed(new Rect(0, 98, w, 30), "Unity remake", small);

            var modeRect = new Rect(w / 2 - 260, 140, 520, 46);
            Box(modeRect, new Color(0, 0, 0, 0.6f));
            if (GUI.Button(new Rect(modeRect.x, modeRect.y, 60, modeRect.height), GUIContent.none, GUIStyle.none)) CycleMode(-1);
            if (GUI.Button(new Rect(modeRect.xMax - 60, modeRect.y, 60, modeRect.height), GUIContent.none, GUIStyle.none)) CycleMode(1);
            string modeName = mode == GameMode.League ? "LEAGUE" : mode == GameMode.Race ? "SINGLE RACE v CPU" : "PRACTICE";
            Shadowed(modeRect, $"<   {modeName}   >", medium);

            float y = 200;
            if (mode == GameMode.League)
            {
                var info = new Rect(w / 2 - 260, y, 520, 150);
                Box(info, new Color(0, 0, 0, 0.55f));
                GUI.Label(new Rect(info.x + 16, info.y + 12, info.width - 32, info.height - 20),
                    $"Season {league.season}  -  Division {league.PlayerDivision}\n" +
                    $"Race {league.raceIndex + 1} of {League.Races}: {league.NextTrack.Name}\n" +
                    $"Rival: {league.NextRival.name}\n" +
                    $"Chassis damage carried: {Mathf.RoundToInt(league.carriedDamage)}%", left);
                y += 165;
            }
            else
            {
                var defs = TrackLibrary.All;
                for (int i = 0; i < defs.Length; i++)
                {
                    var r = new Rect(w / 2 - 260, y, 520, 34);
                    Box(r, i == selected ? new Color(0.85f, 0.15f, 0.1f, 0.85f) : new Color(0, 0, 0, 0.55f));
                    if (GUI.Button(r, GUIContent.none, GUIStyle.none)) { selected = i; LoadTrack(MenuTrack); }
                    GUI.Label(new Rect(r.x + 14, r.y + 4, 300, 30), defs[i].Name, left);
                    GUI.Label(new Rect(r.x, r.y + 4, r.width - 14, 30), $"Division {defs[i].Division}", right);
                    y += 38;
                }
                Shadowed(new Rect(0, y, w, 30), defs[selected].Description, small);
                y += 36;
            }

            var startRect = new Rect(w / 2 - 130, y, 260, 54);
            Box(startRect, new Color(0.85f, 0.15f, 0.1f, 0.9f));
            if (GUI.Button(startRect, GUIContent.none, GUIStyle.none)) StartFromMenu();
            Shadowed(startRect, mode == GameMode.League ? "LEAGUE TABLE" : "START", medium);

            Shadowed(new Rect(0, h - 60, w, 24), "Up/Down: track    Left/Right: mode    Enter or pad A: start", small);
            Shadowed(new Rect(0, h - 34, w, 24), "Drive: Arrows/WASD or gamepad   Boost: Space   Camera: C   Crane: R", small);
        }

        void DrawLeague(float w, float h)
        {
            Shadowed(new Rect(0, 24, w, 60), $"SEASON {league.season}", big);

            if (!string.IsNullOrEmpty(league.lastSeasonSummary) && league.raceIndex == 0)
            {
                Box(new Rect(w / 2 - 330, 92, 660, 40), new Color(0.85f, 0.15f, 0.1f, 0.85f));
                Shadowed(new Rect(w / 2 - 330, 92, 660, 40), league.lastSeasonSummary, small);
            }

            // Your division, large; the others small underneath.
            float y = 145;
            DrawTable(new Rect(w / 2 - 330, y, 660, 150), league.PlayerDivision, true);
            y += 165;

            var next = new Rect(w / 2 - 330, y, 660, 60);
            Box(next, new Color(0, 0, 0, 0.6f));
            GUI.Label(new Rect(next.x + 16, next.y + 6, next.width - 32, 50),
                $"Race {league.raceIndex + 1} of {League.Races}:  {league.NextTrack.Name}  v  {league.NextRival.name}\n" +
                $"Chassis damage carried: {Mathf.RoundToInt(league.carriedDamage)}%", new GUIStyle(left) { fontSize = 18 });
            y += 72;

            float tw = 210, gap = 15, x0 = w / 2 - (tw * 3 + gap * 2) / 2;
            int col = 0;
            for (int div = 1; div <= 4; div++)
            {
                if (div == league.PlayerDivision) continue;
                DrawTable(new Rect(x0 + col * (tw + gap), y, tw, 110), div, false);
                col++;
            }
            y += 125;

            var raceRect = new Rect(w / 2 - 280, y, 260, 50);
            Box(raceRect, new Color(0.85f, 0.15f, 0.1f, 0.9f));
            if (GUI.Button(raceRect, GUIContent.none, GUIStyle.none)) StartRace();
            Shadowed(raceRect, "RACE", medium);

            var menuRect = new Rect(w / 2 + 20, y, 120, 50);
            Box(menuRect, new Color(0, 0, 0, 0.6f));
            if (GUI.Button(menuRect, GUIContent.none, GUIStyle.none)) ShowMenu();
            Shadowed(menuRect, "MENU", medium);

            var resetRect = new Rect(w / 2 + 150, y, 130, 50);
            Box(resetRect, new Color(0.3f, 0, 0, 0.6f));
            if (GUI.Button(resetRect, GUIContent.none, GUIStyle.none))
            {
                if (confirmReset) { league = League.CreateNew(); ShowLeagueTable(); }
                else confirmReset = true;
            }
            Shadowed(resetRect, confirmReset ? "SURE?" : "NEW LEAGUE", new GUIStyle(small) { fontStyle = FontStyle.Bold });

            Shadowed(new Rect(0, h - 34, w, 24), "Enter: race    Esc: menu", small);
        }

        void DrawTable(Rect r, int division, bool large)
        {
            Box(r, new Color(0, 0, 0, large ? 0.65f : 0.5f));
            var head = new GUIStyle(left) { fontSize = large ? 20 : 14 };
            var row = new GUIStyle(left) { fontSize = large ? 22 : 14, fontStyle = FontStyle.Normal };
            var rowR = new GUIStyle(row) { alignment = TextAnchor.UpperRight };
            float lh = large ? 32 : 22;
            GUI.Label(new Rect(r.x + 12, r.y + 8, r.width, lh), $"DIVISION {division}", head);
            if (large) GUI.Label(new Rect(r.x, r.y + 8, r.width - 12, lh), "WINS   PTS", new GUIStyle(head) { alignment = TextAnchor.UpperRight });
            var table = league.Division(division);
            for (int i = 0; i < table.Count; i++)
            {
                var d = table[i];
                float y = r.y + 10 + lh * (i + 1);
                var c = row.normal.textColor;
                if (d.isPlayer) { row.normal.textColor = new Color(1f, 0.8f, 0.2f); rowR.normal.textColor = row.normal.textColor; }
                GUI.Label(new Rect(r.x + 12, y, r.width, lh), $"{i + 1}. {d.name}", row);
                GUI.Label(new Rect(r.x, y, r.width - 12, lh), large ? $"{d.wins}      {d.points}" : $"{d.points}", rowR);
                row.normal.textColor = c; rowR.normal.textColor = c;
            }
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

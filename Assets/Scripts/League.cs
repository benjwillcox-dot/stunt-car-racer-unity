using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace StuntCar
{
    [Serializable]
    public class LeagueDriver
    {
        public string name;
        public float skill;
        public int division;
        public int points;
        public int wins;
        public bool isPlayer;
    }

    /// <summary>
    /// The league, as in the original: four divisions of three drivers, two tracks per
    /// division. Each season you race both rivals on both tracks (four races); the rivals
    /// also race each other. 2 points a win, 1 for the fastest lap. Top of each division
    /// goes up, bottom goes down. Damage to your car carries from race to race within a
    /// season and is repaired between seasons. Progress is saved (PlayerPrefs, which is
    /// browser storage in the WebGL build).
    /// </summary>
    [Serializable]
    public class League
    {
        const string SaveKey = "StuntCar.League.v1";
        public const int Races = 4;

        public int season = 1;
        public int raceIndex;               // 0..3 within the season
        public float carriedDamage;
        public string lastSeasonSummary = "";
        public List<LeagueDriver> drivers = new List<LeagueDriver>();

        static readonly System.Random Rng = new System.Random();

        public static League LoadOrCreate()
        {
            string json = PlayerPrefs.GetString(SaveKey, "");
            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    var loaded = JsonUtility.FromJson<League>(json);
                    if (loaded != null && loaded.drivers.Count == 12) return loaded;
                }
                catch (Exception e)
                {
                    Debug.LogWarning("League save unreadable, starting a new one: " + e.Message);
                }
            }
            return CreateNew();
        }

        public static League CreateNew()
        {
            var l = new League();
            void Add(string n, float s, int d) => l.drivers.Add(new LeagueDriver { name = n, skill = s, division = d });
            l.drivers.Add(new LeagueDriver { name = "You", division = 4, isPlayer = true });
            Add("Rocket Ron", 0.80f, 4); Add("Jumping Jess", 0.84f, 4);
            Add("Dodgy Dave", 0.86f, 3); Add("Crusher Clyde", 0.88f, 3); Add("Hot Wheels Hana", 0.90f, 3);
            Add("Big Air Bill", 0.91f, 2); Add("Turbo Tess", 0.93f, 2); Add("Daredevil Dev", 0.95f, 2);
            Add("Max Boost", 0.96f, 1); Add("High Flyer Flo", 0.98f, 1); Add("Steel Eye", 1.00f, 1);
            l.Save();
            return l;
        }

        public void Save()
        {
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(this));
            PlayerPrefs.Save();
        }

        public LeagueDriver Player => drivers.First(d => d.isPlayer);
        public int PlayerDivision => Player.division;

        public List<LeagueDriver> Division(int division) =>
            drivers.Where(d => d.division == division).OrderByDescending(d => d.points).ThenByDescending(d => d.wins).ToList();

        List<LeagueDriver> Rivals => drivers.Where(d => d.division == PlayerDivision && !d.isPlayer).OrderBy(d => d.name).ToList();

        /// <summary>Race order: track A v rival 1, track A v rival 2, track B v rival 1, track B v rival 2.</summary>
        public TrackDefinition NextTrack => TrackLibrary.ForDivision(PlayerDivision)[raceIndex / 2];
        public LeagueDriver NextRival => Rivals[raceIndex % 2];

        public void RecordPlayerRace(bool won, bool fastestLap, float damageAfter, bool wrecked)
        {
            var player = Player;
            var rival = NextRival;
            Award(won ? player : rival, 2);
            if (fastestLap) Award(player, 1); else Award(rival, 1);
            if (won) player.wins++; else rival.wins++;

            carriedDamage = wrecked ? 50f : damageAfter; // a wrecked car is patched up, but not fully
            // After the player's second race on a track, the two rivals race each other there.
            if (raceIndex % 2 == 1)
            {
                var r = Rivals;
                SimulateRace(r[0], r[1]);
            }
            raceIndex++;
            if (raceIndex >= Races) EndSeason();
            Save();
        }

        static void Award(LeagueDriver d, int points) => d.points += points;

        static void SimulateRace(LeagueDriver a, LeagueDriver b)
        {
            float pA = Mathf.Clamp(0.5f + (a.skill - b.skill) * 4f, 0.1f, 0.9f);
            bool aWins = Rng.NextDouble() < pA;
            var winner = aWins ? a : b;
            var loser = aWins ? b : a;
            winner.points += 2;
            winner.wins++;
            (Rng.NextDouble() < 0.7 ? winner : loser).points += 1;
        }

        void EndSeason()
        {
            // Finish the other divisions' seasons: every pair meets on both tracks.
            for (int div = 1; div <= 4; div++)
            {
                if (div == PlayerDivision) continue;
                var ds = drivers.Where(d => d.division == div).ToList();
                for (int track = 0; track < 2; track++)
                    for (int i = 0; i < ds.Count; i++)
                        for (int j = i + 1; j < ds.Count; j++)
                            SimulateRace(ds[i], ds[j]);
            }

            var tables = new Dictionary<int, List<LeagueDriver>>();
            for (int div = 1; div <= 4; div++) tables[div] = Division(div);

            int playerPos = tables[PlayerDivision].IndexOf(Player) + 1;
            int oldDiv = PlayerDivision;

            for (int div = 1; div <= 4; div++)
            {
                var t = tables[div];
                if (div > 1) t[0].division = div - 1;            // promoted
                if (div < 4) t[t.Count - 1].division = div + 1;  // relegated
            }

            string outcome;
            if (oldDiv == 1 && playerPos == 1) outcome = "CHAMPION OF DIVISION 1! You are the Stunt Car champion.";
            else if (Player.division < oldDiv) outcome = $"PROMOTED to Division {Player.division}!";
            else if (Player.division > oldDiv) outcome = $"Relegated to Division {Player.division}.";
            else outcome = $"You stay in Division {Player.division}.";
            lastSeasonSummary = $"Season {season}: you finished {Ordinal(playerPos)} in Division {oldDiv}. {outcome}";

            foreach (var d in drivers) { d.points = 0; d.wins = 0; }
            season++;
            raceIndex = 0;
            carriedDamage = 0f; // repaired between seasons
        }

        public static string Ordinal(int n) => n == 1 ? "1st" : n == 2 ? "2nd" : n == 3 ? "3rd" : n + "th";
    }
}

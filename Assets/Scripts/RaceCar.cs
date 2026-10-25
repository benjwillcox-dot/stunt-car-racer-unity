using System.Collections.Generic;
using UnityEngine;

namespace StuntCar
{
    /// <summary>
    /// Per-car race state: where it is on the track, laps and lap times, and calling the
    /// crane when the car falls off, gets stranded upside down, or the driver asks for it.
    /// </summary>
    public class RaceCar : MonoBehaviour
    {
        public string DriverName;
        public bool IsPlayer;

        public CarController Car { get; private set; }
        public CarRecovery Recovery { get; private set; }
        public int Index { get; private set; }
        public int LapsCompleted { get; private set; }
        public float CurrentLapStart { get; private set; }
        public float BestLap { get; private set; } = float.PositiveInfinity;
        public float LastLap { get; private set; } = float.PositiveInfinity;
        public List<float> LapTimes { get; } = new List<float>();
        public bool Finished { get; set; }
        public float FinishTime { get; set; }
        public int Recoveries { get; private set; }

        /// <summary>Distance covered in the race, in samples; used to rank cars.</summary>
        public float Progress => LapsCompleted * track.Count + Index;

        /// <summary>Raised when a lap is completed, with the lap time.</summary>
        public event System.Action<RaceCar, float> LapCompleted;

        Track track;
        bool passedHalfway;
        float offTrackTimer, upsideDownTimer;

        public void Init(Track t, CarController c, string driverName, bool isPlayer)
        {
            track = t;
            Car = c;
            Recovery = GetComponent<CarRecovery>();
            DriverName = driverName;
            IsPlayer = isPlayer;
        }

        public void PlaceOnGrid(int index, float lateral)
        {
            Index = track.Wrap(index);
            Vector3 p = track.Center(Index) + track.Right(Index) * lateral + track.Up(Index) * 0.2f;
            transform.SetPositionAndRotation(p, track.Rotation(Index));
            Car.Body.position = p;
            Car.Body.rotation = track.Rotation(Index);
            Car.ResetForRace();
            LapsCompleted = 0;
            LapTimes.Clear();
            BestLap = LastLap = float.PositiveInfinity;
            Finished = false;
            passedHalfway = false;
            Recoveries = 0;
        }

        public void StartTiming(float time) => CurrentLapStart = time;

        public void RequestRecovery()
        {
            if (Recovery.InProgress || Car.Wrecked) return;
            Recoveries++;
            Recovery.Begin(track, Index);
        }

        void FixedUpdate()
        {
            if (track == null) return;
            UpdateIndex();
            if (!Recovery.InProgress && !Car.Wrecked && !Finished) CheckNeedsCrane();
        }

        void UpdateIndex()
        {
            int n = track.Count;
            int newIndex = track.FindNearest(transform.position, Index, 12);
            int delta = newIndex - Index;

            if (Mathf.Abs(newIndex - n / 2) < n / 8) passedHalfway = true;

            // Crossed the start line going forwards.
            if (delta < -n / 2 && passedHalfway && !Finished)
            {
                float now = Time.time;
                float lap = now - CurrentLapStart;
                CurrentLapStart = now;
                LapsCompleted++;
                LastLap = lap;
                LapTimes.Add(lap);
                if (lap < BestLap) BestLap = lap;
                passedHalfway = false;
                LapCompleted?.Invoke(this, lap);
            }
            Index = newIndex;
        }

        void CheckNeedsCrane()
        {
            Vector3 rel = transform.position - track.Center(Index);
            float height = Vector3.Dot(rel, track.Up(Index));
            float lateral = Mathf.Abs(Vector3.Dot(rel, track.Right(Index)));
            float dt = Time.fixedDeltaTime;

            // Below the deck (fallen off) – give it a moment to hit the ground first.
            bool fallen = height < -2.5f || (lateral > track.HalfWidth + 2.5f && height < 1.5f);
            offTrackTimer = fallen ? offTrackTimer + dt : 0f;

            bool upsideDown = transform.up.y < 0.25f && Car.Body.linearVelocity.magnitude < 2f;
            upsideDownTimer = upsideDown ? upsideDownTimer + dt : 0f;

            if (offTrackTimer > 1.2f || upsideDownTimer > 1.5f)
            {
                offTrackTimer = upsideDownTimer = 0f;
                RequestRecovery();
            }
        }
    }
}

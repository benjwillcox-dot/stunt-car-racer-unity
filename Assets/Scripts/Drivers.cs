using UnityEngine;

namespace StuntCar
{
    /// <summary>Feeds keyboard / gamepad / wheel input (see <see cref="GameInput"/>) to the car.</summary>
    public class PlayerDriver : MonoBehaviour
    {
        CarController car;
        RaceCar raceCar;

        void Awake()
        {
            car = GetComponent<CarController>();
            raceCar = GetComponent<RaceCar>();
        }

        void Update()
        {
            GameInput.ReadDriving(out float steer, out float throttle, out float brake, out bool boost);
            car.Steer = steer;
            car.Throttle = throttle;
            car.Brake = brake;
            car.Boost = boost;
            if (GameInput.CranePressed() && raceCar) raceCar.RequestRecovery();
        }
    }

    /// <summary>
    /// Follows the centre line using a look-ahead point and the track's precomputed safe speeds.
    /// Skill scales how close to those speeds it drives.
    /// </summary>
    public class AIDriver : MonoBehaviour
    {
        public float Skill = 0.9f;        // 0.75 (easy) .. 1.0 (hard)
        public float LaneOffset = 0f;

        CarController car;
        RaceCar raceCar;
        Track track;

        public void Init(Track t, float skill)
        {
            track = t;
            Skill = skill;
            car = GetComponent<CarController>();
            raceCar = GetComponent<RaceCar>();
        }

        void FixedUpdate()
        {
            if (track == null) return;
            int i = raceCar.Index;
            float speed = car.Speed;

            // Steer at a point ahead, further at speed.
            int aimIndex = track.IndexAhead(i, 7f + Mathf.Max(0f, speed) * 0.45f);
            Vector3 aim = track.Center(aimIndex) + track.Right(aimIndex) * LaneOffset;
            Vector3 local = transform.InverseTransformPoint(aim);
            float angle = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
            car.Steer = Mathf.Clamp(angle / Mathf.Max(4f, car.CurrentMaxSteer), -1f, 1f);

            // Plan speed from a bit further ahead so it brakes before bends and crests.
            float target = Mathf.Min(track.AiSpeed(track.IndexAhead(i, 6f)), track.AiSpeed(track.IndexAhead(i, 20f)) + 6f) * Skill;
            // Jumps need a minimum speed whatever the skill, or the car drops into the gap.
            float minSpeed = Mathf.Max(track.AiMinSpeed(track.IndexAhead(i, 4f)), track.AiMinSpeed(track.IndexAhead(i, 30f)));
            target = Mathf.Max(target, minSpeed * 1.05f);
            if (car.GroundedWheels == 0)
            {
                car.Throttle = 0f; car.Brake = 0f;
            }
            else if (speed < target - 1f)
            {
                car.Throttle = 1f; car.Brake = 0f;
            }
            else if (speed > target + 2f)
            {
                car.Throttle = 0f; car.Brake = Mathf.Clamp01((speed - target) / 6f);
            }
            else
            {
                car.Throttle = 0.35f; car.Brake = 0f;
            }

            // Boost on long fast stretches, saving some for the last lap.
            float straightAhead = Mathf.Min(track.AiSpeed(track.IndexAhead(i, 30f)), track.AiSpeed(track.IndexAhead(i, 60f)));
            car.Boost = car.BoostRemaining > 0f && straightAhead > 50f && speed > 30f && Mathf.Abs(angle) < 4f && Skill > 0.85f;
        }
    }
}

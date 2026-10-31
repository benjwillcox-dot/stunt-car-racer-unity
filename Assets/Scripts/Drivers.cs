using UnityEngine;

namespace StuntCar
{
    /// <summary>
    /// Feeds keyboard / gamepad / wheel input (see <see cref="GameInput"/>) to the car, and
    /// rumbles the pad on the kerbs, when you hit the other car and when the tyres slide.
    /// </summary>
    public class PlayerDriver : MonoBehaviour
    {
        const float SlipStart = 3.5f, SlipFull = 12f;   // wheel slip (m/s); smoke starts at 3.5 too

        CarController car;
        RaceCar raceCar;
        int kerbWheels;
        float knock, clunk, clunkLength, clunkStrength;

        void Awake()
        {
            car = GetComponent<CarController>();
            raceCar = GetComponent<RaceCar>();
            car.HitCar += OnHitCar;
        }

        void OnDestroy()
        {
            if (car) car.HitCar -= OnHitCar;
        }

        // A short clunk from the heavy motor, harder and longer the faster the cars met.
        void OnHitCar(float speed)
        {
            float s = Mathf.InverseLerp(1f, 12f, speed);
            float strength = 0.45f + 0.55f * s;
            if (s <= 0f || (clunk > 0f && strength < clunkStrength * clunk / clunkLength)) return;
            clunkStrength = strength;
            clunk = clunkLength = 0.1f + 0.15f * s;
        }

        void Update()
        {
            GameInput.ReadDriving(out float steer, out float throttle, out float brake, out bool boost);
            car.Steer = steer;
            car.Throttle = throttle;
            car.Brake = brake;
            car.Boost = boost;
            if (GameInput.CranePressed() && raceCar) raceCar.RequestRecovery();
            Rumble();
        }

        // Each motor runs at whichever of these wants it most:
        // - kerbs: a buzz, stronger with speed and with both wheels on, plus a knock from the
        //   heavy motor each time another wheel climbs on
        // - hitting the other car: the clunk, fading out
        // - tyres sliding, spinning or locked: a gentle buzz from the light motor
        void Rumble()
        {
            float dt = Time.deltaTime;
            int onKerb = 0;
            float slip = 0f;
            if (car.Body && !car.Body.isKinematic)
            {
                for (int i = 0; i < car.WheelCount; i++)
                {
                    if (car.WheelOnKerb(i)) onKerb++;
                    if (car.WheelGrounded(i)) slip = Mathf.Max(slip, car.WheelSlip(i));
                }
            }
            if (onKerb > kerbWheels) knock = 0.09f;
            kerbWheels = onKerb;
            knock = Mathf.Max(0f, knock - dt);
            clunk = Mathf.Max(0f, clunk - dt);

            float low = 0f, high = 0f;
            float pace = Mathf.InverseLerp(2f, 35f, Mathf.Abs(car.Speed));
            if (onKerb > 0 && pace > 0f)
            {
                float amount = (0.3f + 0.7f * pace) * (onKerb >= 2 ? 1f : 0.6f);
                low = amount * 0.35f + (knock > 0f ? 0.5f * pace : 0f);
                high = amount * 0.8f;
            }
            float slide = Mathf.InverseLerp(SlipStart, SlipFull, slip);
            if (slide > 0f) high = Mathf.Max(high, 0.08f + 0.17f * slide);
            if (clunk > 0f)
            {
                float fade = clunk / clunkLength;
                low = Mathf.Max(low, clunkStrength * fade);
                high = Mathf.Max(high, clunkStrength * 0.4f * fade);
            }
            GameInput.SetRumble(low, high);
        }

        void OnDisable() => GameInput.StopRumble();
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

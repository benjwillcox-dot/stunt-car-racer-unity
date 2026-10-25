using UnityEngine;

namespace StuntCar
{
    /// <summary>Keyboard (and gamepad via the default Input Manager axes).</summary>
    public class PlayerDriver : MonoBehaviour
    {
        CarController car;
        RaceCar raceCar;
        bool axesAvailable = true;

        void Awake()
        {
            car = GetComponent<CarController>();
            raceCar = GetComponent<RaceCar>();
        }

        void Update()
        {
            float steer = 0f, accel = 0f;
            if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A)) steer -= 1f;
            if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D)) steer += 1f;
            if (Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W)) accel += 1f;
            if (Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S)) accel -= 1f;
            bool boost = Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.JoystickButton0);

            // Gamepad: left stick steers, stick up/down for throttle/brake.
            if (axesAvailable)
            {
                try
                {
                    float h = Input.GetAxis("Horizontal"), v = Input.GetAxis("Vertical");
                    if (Mathf.Abs(h) > Mathf.Abs(steer)) steer = h;
                    if (Mathf.Abs(v) > Mathf.Abs(accel)) accel = v;
                }
                catch (System.ArgumentException)
                {
                    axesAvailable = false;
                }
            }

            car.Steer = steer;
            car.Throttle = Mathf.Max(0f, accel);
            car.Brake = Mathf.Max(0f, -accel);
            car.Boost = boost;

            if (Input.GetKeyDown(KeyCode.R) && raceCar) raceCar.RequestRecovery();
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

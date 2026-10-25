using UnityEngine;

namespace StuntCar
{
    /// <summary>
    /// Raycast-suspension car. Each wheel is a spring/damper with a simple tyre model
    /// (grip limited by a friction circle), rear-wheel drive, limited boost, and chassis
    /// damage from hard landings and impacts, like the original.
    /// Drivers (player or AI) write Throttle/Brake/Steer/Boost every frame.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class CarController : MonoBehaviour
    {
        // --- Controls, written by a driver component ---
        [HideInInspector] public float Throttle;   // 0..1
        [HideInInspector] public float Brake;      // 0..1 (reverses when nearly stopped)
        [HideInInspector] public float Steer;      // -1..1
        [HideInInspector] public bool Boost;
        [HideInInspector] public bool Locked;      // held on the grid / by the crane

        [Header("Chassis")]
        public float mass = 1100f;
        public Vector3 centreOfMass = new Vector3(0f, 0.35f, 0.1f);

        [Header("Suspension")]
        public float restLength = 0.45f;
        public float wheelRadius = 0.36f;
        public float springRate = 36000f;
        public float damperRate = 3200f;
        public float bumpStopRate = 140000f;

        [Header("Tyres")]
        public float tyreGrip = 1.45f;          // friction coefficient
        public float lateralStiffness = 9f;     // how hard tyres resist sliding sideways
        public float rollingResistance = 25f;
        public float tyreForceLift = 0.35f;     // apply tyre forces this far above the contact (less roll)

        [Header("Engine")]
        public float engineForce = 9000f;
        public float topSpeed = 58f;            // m/s (~130 mph)
        public float boostForce = 6500f;
        public float boostTopSpeed = 74f;       // m/s (~165 mph)
        public float brakeForce = 14000f;
        public float reverseForce = 4000f;
        public float dragCoefficient = 0.42f;
        public float downforce = 0.25f;

        [Header("Steering")]
        public float maxSteerAngle = 32f;
        public float highSpeedSteerAngle = 5f;
        public float steerSpeed = 3.5f;

        [Header("Boost and damage")]
        public float boostCapacity = 12f;       // seconds of boost per race
        public float landingDamageThreshold = 5.5f;
        public float landingDamageScale = 3.2f;
        public float impactDamageThreshold = 7f;
        public float impactDamageScale = 3.5f;

        public Rigidbody Body { get; private set; }
        public float BoostRemaining { get; set; }
        public float Damage { get; private set; }          // 0..100
        public bool Wrecked => Damage >= 100f;
        public bool IsBoosting { get; private set; }
        public int GroundedWheels { get; private set; }
        public float Speed => Body ? Vector3.Dot(Body.linearVelocity, transform.forward) : 0f;
        public float CurrentSteerAngle { get; private set; }
        public float CurrentMaxSteer => Mathf.Lerp(maxSteerAngle, highSpeedSteerAngle, Mathf.Clamp01(Mathf.Abs(Speed) / 50f));

        /// <summary>Raised with the amount of damage taken, for sound and camera shake.</summary>
        public event System.Action<float> Damaged;
        /// <summary>Hard contact: point and closing speed (landings and crashes), for dust.</summary>
        public event System.Action<Vector3, float> Impact;
        /// <summary>Body sliding along something: contact point and sliding speed, for sparks.</summary>
        public event System.Action<Vector3, float> Scrape;

        public int WheelCount => wheels == null ? 0 : wheels.Length;
        public bool WheelGrounded(int i) => wheels[i].grounded;
        public Vector3 WheelContact(int i) => wheels[i].contact;
        public float WheelSlip(int i) => wheels[i].slip;
        public bool WheelDriven(int i) => wheels[i].driven;

        class Wheel
        {
            public Vector3 mount;          // local position of the top of the suspension
            public bool front, driven;
            public Transform visual;
            public float lastCompression;
            public float springLength;
            public float spin;
            public bool grounded;
            public Vector3 contact;
            public float slip;      // sideways sliding speed plus wheelspin, m/s
        }

        Wheel[] wheels;
        float smoothSteer;
        int rayMask;

        public void Init(Vector3[] mounts, Transform[] wheelVisuals)
        {
            Body = GetComponent<Rigidbody>();
            Body.mass = mass;
            Body.centerOfMass = centreOfMass;
            Body.interpolation = RigidbodyInterpolation.Interpolate;
            Body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            Body.linearDamping = 0f;
            Body.angularDamping = 0.4f;
            Body.maxAngularVelocity = 12f;

            wheels = new Wheel[mounts.Length];
            for (int i = 0; i < mounts.Length; i++)
            {
                wheels[i] = new Wheel
                {
                    mount = mounts[i],
                    front = mounts[i].z > 0f,
                    driven = mounts[i].z < 0f,
                    visual = wheelVisuals[i],
                    springLength = restLength,
                };
            }
            // Cars sit on the Ignore Raycast layer so wheel rays only see the track and ground.
            rayMask = Physics.DefaultRaycastLayers;
            BoostRemaining = boostCapacity;
        }

        public void ResetForRace()
        {
            Damage = 0f;
            BoostRemaining = boostCapacity;
            Body.linearVelocity = Vector3.zero;
            Body.angularVelocity = Vector3.zero;
        }

        /// <summary>Starts a race already damaged (league damage carries over).</summary>
        public void SetDamage(float amount) => Damage = Mathf.Clamp(amount, 0f, 99f);

        public void AddDamage(float amount)
        {
            if (amount <= 0f || Wrecked) return;
            Damage = Mathf.Min(100f, Damage + amount);
            Damaged?.Invoke(amount);
        }

        void FixedUpdate()
        {
            if (wheels == null || Body.isKinematic) return;

            float dt = Time.fixedDeltaTime;
            Vector3 up = transform.up;
            float massPerWheel = mass / wheels.Length;
            float fwdSpeed = Speed;

            // Steering eases in and narrows with speed.
            float steerTarget = Locked ? 0f : Mathf.Clamp(Steer, -1f, 1f);
            smoothSteer = Mathf.MoveTowards(smoothSteer, steerTarget, steerSpeed * dt);
            CurrentSteerAngle = smoothSteer * CurrentMaxSteer;

            // Boost.
            IsBoosting = Boost && !Locked && !Wrecked && BoostRemaining > 0f && Throttle > 0.1f;
            if (IsBoosting) BoostRemaining = Mathf.Max(0f, BoostRemaining - dt);

            // Engine / brakes / reverse.
            float drive = 0f, braking = 0f;
            if (!Locked && !Wrecked)
            {
                float vmax = IsBoosting ? boostTopSpeed : topSpeed;
                float ratio = Mathf.Clamp01(Mathf.Max(0f, fwdSpeed) / vmax);
                drive = Throttle * engineForce * (1f - ratio * ratio);
                if (Brake > 0.05f)
                {
                    if (fwdSpeed > 1.5f) braking = Brake * brakeForce;
                    else drive = -Brake * reverseForce;
                }
            }
            else if (Locked)
            {
                braking = brakeForce;
            }

            int drivenGrounded = 0;
            foreach (var w in wheels) if (w.driven && w.grounded) drivenGrounded++;
            float drivePerWheel = drivenGrounded > 0 ? drive / drivenGrounded : 0f;

            int grounded = 0;
            float worstLanding = 0f;

            foreach (var w in wheels)
            {
                Vector3 origin = transform.TransformPoint(w.mount);
                float maxDist = restLength + wheelRadius;

                if (!Physics.Raycast(origin, -up, out RaycastHit hit, maxDist, rayMask, QueryTriggerInteraction.Ignore))
                {
                    w.grounded = false;
                    w.lastCompression = 0f;
                    w.springLength = Mathf.MoveTowards(w.springLength, restLength, dt * 2f);
                    continue;
                }

                grounded++;
                w.grounded = true;
                w.springLength = Mathf.Max(0f, hit.distance - wheelRadius);
                float compression = 1f - w.springLength / restLength;            // 0 = extended, 1 = fully compressed
                // Rate the mount is closing on the (static) road. Using velocity rather than the change in
                // compression avoids a huge damper spike on the first frame after landing.
                float compressionSpeed = -Vector3.Dot(Body.GetPointVelocity(origin), up);
                w.lastCompression = compression;

                float load = compression * restLength * springRate + compressionSpeed * damperRate;
                if (compression > 0.85f)
                {
                    load += (compression - 0.85f) * restLength * bumpStopRate;
                    worstLanding = Mathf.Max(worstLanding, compressionSpeed);
                }
                load = Mathf.Max(0f, load);
                Body.AddForceAtPosition(up * load, origin);

                // Tyre axes on the road surface.
                Vector3 wheelFwd = w.front ? Quaternion.AngleAxis(CurrentSteerAngle, up) * transform.forward : transform.forward;
                wheelFwd = Vector3.ProjectOnPlane(wheelFwd, hit.normal).normalized;
                Vector3 wheelRight = Vector3.Cross(hit.normal, wheelFwd);

                Vector3 pointVel = Body.GetPointVelocity(hit.point);
                float vLong = Vector3.Dot(pointVel, wheelFwd);
                float vLat = Vector3.Dot(pointVel, wheelRight);

                float latForce = -vLat * lateralStiffness * massPerWheel;
                float longForce = w.driven ? drivePerWheel : 0f;
                longForce -= vLong * rollingResistance;
                if (braking > 0f)
                {
                    // Never more than would stop the wheel this step, so it doesn't jitter at rest.
                    float stop = Mathf.Abs(vLong) * massPerWheel / dt;
                    longForce -= Mathf.Sign(vLong) * Mathf.Min(braking / wheels.Length, stop);
                }

                float maxForce = tyreGrip * Mathf.Min(load, massPerWheel * 9.81f * 3f);
                Vector2 f = new Vector2(longForce, latForce);
                if (f.sqrMagnitude > maxForce * maxForce) f = f.normalized * maxForce;

                Vector3 forcePoint = hit.point + up * tyreForceLift;
                Body.AddForceAtPosition(wheelFwd * f.x + wheelRight * f.y, forcePoint);

                w.spin += vLong / wheelRadius * Mathf.Rad2Deg * dt;
                w.contact = hit.point;
                bool spinning = w.driven && drivePerWheel > maxForce * 0.98f && Mathf.Abs(vLong) < 12f;
                w.slip = Mathf.Abs(vLat) + (spinning ? 6f : 0f) + (braking > 0f && f.sqrMagnitude >= maxForce * maxForce * 0.95f ? Mathf.Abs(vLong) * 0.3f : 0f);
            }
            GroundedWheels = grounded;

            if (worstLanding > 3f) Impact?.Invoke(transform.position, worstLanding);
            // Hard landings crack the chassis.
            if (worstLanding > landingDamageThreshold)
                AddDamage((worstLanding - landingDamageThreshold) * landingDamageScale);

            Vector3 v = Body.linearVelocity;
            Body.AddForce(-v * v.magnitude * dragCoefficient);

            if (grounded > 0)
            {
                Body.AddForce(-up * v.sqrMagnitude * downforce);
                if (IsBoosting) Body.AddForce(transform.forward * boostForce);
            }
            else
            {
                // A little help to land wheels-first, as the arcade original did.
                Vector3 axis = Vector3.Cross(up, Vector3.up);
                Body.AddTorque(axis * 3f - Body.angularVelocity * 0.4f, ForceMode.Acceleration);
            }
        }

        void Update()
        {
            if (wheels == null) return;
            foreach (var w in wheels)
            {
                if (!w.visual) continue;
                w.visual.localPosition = w.mount - Vector3.up * w.springLength;
                float steer = w.front ? CurrentSteerAngle : 0f;
                w.visual.localRotation = Quaternion.Euler(0f, steer, 0f) * Quaternion.Euler(w.spin, 0f, 0f);
            }
        }

        void OnCollisionEnter(Collision c)
        {
            if (c.contactCount == 0) return;
            var contact = c.GetContact(0);
            float impact = Mathf.Abs(Vector3.Dot(c.relativeVelocity, contact.normal));
            if (impact > 3f) Impact?.Invoke(contact.point, impact);
            bool otherCar = c.rigidbody != null;
            float threshold = otherCar ? impactDamageThreshold * 1.6f : impactDamageThreshold;
            if (impact > threshold) AddDamage((impact - threshold) * impactDamageScale);
        }

        void OnCollisionStay(Collision c)
        {
            if (c.contactCount == 0 || c.rigidbody != null) return;
            var contact = c.GetContact(0);
            Vector3 slide = Vector3.ProjectOnPlane(c.relativeVelocity, contact.normal);
            if (slide.sqrMagnitude > 16f) Scrape?.Invoke(contact.point, slide.magnitude);
        }
    }
}

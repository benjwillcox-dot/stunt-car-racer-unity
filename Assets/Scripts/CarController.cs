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
        public float wheelRadius = 0.36f;       // default; Init can give each wheel its own
        public float springRate = 36000f;
        public float damperRate = 3200f;
        public float bumpStopRate = 140000f;

        [Header("Anti-roll bars")]
        // Resist the body leaning by moving load from the outside wheel to the inside one, as a
        // spring would (N per metre of left-right difference in compression). The stiffer end
        // takes more of the load transfer and so lets go first: a stiffer front is steadier, a
        // stiffer rear turns in harder.
        public float frontAntiRoll = 22000f;
        public float rearAntiRoll = 11000f;

        [Header("Tyres")]
        public float tyreGrip = 1.45f;          // peak friction coefficient, at a tyre's share of the car's weight
        public float peakSlipAngle = 7f;        // degrees: sideways grip builds to here, then eases off as the tyre slides
        public float tyreShape = 1.3f;          // curve shape (Pacejka's C): 1.3 keeps about 89% of the peak when well past it
        public float loadSensitivity = 0.85f;   // grip grows as load to this power, so the more loaded tyre gets less per newton
        public float rollingResistance = 25f;
        public float tyreForceLift = 0.1f;      // apply tyre forces this far above the contact (a little less roll)

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
        /// <summary>Ran into another car: closing speed, for the pad's clunk.</summary>
        public event System.Action<float> HitCar;

        public int WheelCount => wheels == null ? 0 : wheels.Length;
        public bool WheelGrounded(int i) => wheels[i].grounded;
        public Vector3 WheelContact(int i) => wheels[i].contact;
        public float WheelSlip(int i) => wheels[i].slip;
        public bool WheelDriven(int i) => wheels[i].driven;
        public bool WheelOnKerb(int i) => wheels[i].onKerb;

        class Wheel
        {
            public Vector3 mount;          // local position of the top of the suspension
            public float radius;
            public bool front, driven;
            public Transform visual;
            public float lastCompression;
            public float springLength;
            public float spin;
            public bool grounded;
            public bool onKerb;
            public Vector3 contact;
            public float slip;      // sideways sliding speed plus wheelspin, m/s
            // This step's suspension, worked out before the tyres.
            public Vector3 origin, normal;
            public float load;      // newtons pressing the tyre into the road, springs and anti-roll bar
        }

        Wheel[] wheels;
        float smoothSteer;
        float landingCooldown, landingPeak;
        int rayMask;

        /// <summary>Spring length with the car sitting still on level ground.</summary>
        public float RestingSpringLength(int wheelCount) => restLength - mass * 9.81f / wheelCount / springRate;

        public void Init(Vector3[] mounts, Transform[] wheelVisuals, float[] radii = null)
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
                    radius = radii != null ? radii[i] : wheelRadius,
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

            // The suspension first: where each wheel meets the road and how hard its spring pushes.
            foreach (var w in wheels)
            {
                w.origin = transform.TransformPoint(w.mount);
                float maxDist = restLength + w.radius;

                if (!Physics.Raycast(w.origin, -up, out RaycastHit hit, maxDist, rayMask, QueryTriggerInteraction.Ignore))
                {
                    w.grounded = false;
                    w.onKerb = false;
                    w.lastCompression = 0f;
                    w.load = 0f;
                    w.springLength = Mathf.MoveTowards(w.springLength, restLength, dt * 2f);
                    continue;
                }

                grounded++;
                w.grounded = true;
                w.onKerb = hit.collider.TryGetComponent(out KerbSurface _);
                w.contact = hit.point;
                w.normal = hit.normal;
                w.springLength = Mathf.Max(0f, hit.distance - w.radius);
                float compression = 1f - w.springLength / restLength;            // 0 = extended, 1 = fully compressed
                // Rate the mount is closing on the (static) road. Using velocity rather than the change in
                // compression avoids a huge damper spike on the first frame after landing.
                float compressionSpeed = -Vector3.Dot(Body.GetPointVelocity(w.origin), up);
                w.lastCompression = compression;

                float load = compression * restLength * springRate + compressionSpeed * damperRate;
                if (compression > 0.85f)
                {
                    load += (compression - 0.85f) * restLength * bumpStopRate;
                    worstLanding = Mathf.Max(worstLanding, compressionSpeed);
                }
                w.load = Mathf.Max(0f, load);
            }
            GroundedWheels = grounded;
            AntiRollBar(true, frontAntiRoll);
            AntiRollBar(false, rearAntiRoll);

            // Then the tyres, on the loads the springs and bars have settled.
            float nominalLoad = massPerWheel * 9.81f;
            foreach (var w in wheels)
            {
                if (!w.grounded) continue;
                Body.AddForceAtPosition(up * w.load, w.origin);

                // Tyre axes on the road surface.
                Vector3 wheelFwd = w.front ? Quaternion.AngleAxis(CurrentSteerAngle, up) * transform.forward : transform.forward;
                wheelFwd = Vector3.ProjectOnPlane(wheelFwd, w.normal).normalized;
                Vector3 wheelRight = Vector3.Cross(w.normal, wheelFwd);

                Vector3 pointVel = Body.GetPointVelocity(w.contact);
                float vLong = Vector3.Dot(pointVel, wheelFwd);
                float vLat = Vector3.Dot(pointVel, wheelRight);

                // Grip grows less than in proportion to load, so loading one tyre of a pair up costs the pair.
                float load = Mathf.Min(w.load, nominalLoad * 3f);
                float peakForce = tyreGrip * nominalLoad * Mathf.Pow(load / nominalLoad, loadSensitivity);

                // Sideways force from the slip angle, with a floor on the speed so it stays sane
                // when nearly stopped, and never more than would stop the slide this step.
                float slipAngle = Mathf.Atan2(vLat, Mathf.Max(Mathf.Abs(vLong), 3f));
                float stopSlide = Mathf.Abs(vLat) * massPerWheel / dt;
                float latForce = Mathf.Clamp(-peakForce * TyreCurve(slipAngle), -stopSlide, stopSlide);

                float longForce = w.driven ? drivePerWheel : 0f;
                longForce -= vLong * rollingResistance;
                if (braking > 0f)
                {
                    // Never more than would stop the wheel this step, so it doesn't jitter at rest.
                    float stop = Mathf.Abs(vLong) * massPerWheel / dt;
                    longForce -= Mathf.Sign(vLong) * Mathf.Min(braking / wheels.Length, stop);
                }

                // Driving, braking and cornering share the one grip limit.
                Vector2 f = new Vector2(longForce, latForce);
                if (f.sqrMagnitude > peakForce * peakForce) f = f.normalized * peakForce;

                Vector3 forcePoint = w.contact + up * tyreForceLift;
                Body.AddForceAtPosition(wheelFwd * f.x + wheelRight * f.y, forcePoint);

                w.spin += vLong / w.radius * Mathf.Rad2Deg * dt;
                bool spinning = w.driven && drivePerWheel > peakForce * 0.98f && Mathf.Abs(vLong) < 12f;
                w.slip = Mathf.Abs(vLat) + (spinning ? 6f : 0f) + (braking > 0f && f.sqrMagnitude >= peakForce * peakForce * 0.95f ? Mathf.Abs(vLong) * 0.3f : 0f);
            }

            if (worstLanding > 3f) Impact?.Invoke(transform.position, worstLanding);
            // Hard landings crack the chassis. A landing lasts several physics steps, so only
            // charge for its worst moment.
            landingCooldown = Mathf.Max(0f, landingCooldown - dt);
            if (worstLanding > landingDamageThreshold)
            {
                float amount = (worstLanding - landingDamageThreshold) * landingDamageScale;
                if (landingCooldown > 0f)
                {
                    if (amount > landingPeak) { AddDamage(amount - landingPeak); landingPeak = amount; }
                }
                else
                {
                    AddDamage(amount);
                    landingPeak = amount;
                }
                landingCooldown = 0.3f;
            }

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

        // An axle's anti-roll bar: it pushes the more compressed side up and the other side down, in
        // proportion to the difference, moving load across the axle. A wheel in the air counts as
        // fully extended and takes no force (there's nothing for it to push against).
        void AntiRollBar(bool front, float stiffness)
        {
            if (stiffness <= 0f) return;
            Wheel left = null, right = null;
            foreach (var w in wheels)
                if (w.front == front) { if (w.mount.x < 0f) left = w; else right = w; }
            if (left == null || right == null || (!left.grounded && !right.grounded)) return;
            float cl = left.grounded ? 1f - left.springLength / restLength : 0f;
            float cr = right.grounded ? 1f - right.springLength / restLength : 0f;
            float force = (cl - cr) * restLength * stiffness;
            if (left.grounded) left.load = Mathf.Max(0f, left.load + force);
            if (right.grounded) right.load = Mathf.Max(0f, right.load - force);
        }

        // Sideways grip for a slip angle (radians), as a fraction of the tyre's peak: Pacejka's
        // sin(C atan(B a)), with B set so the peak falls at peakSlipAngle. Rises almost straight at
        // first, peaks, then eases down to sin(C pi/2) as the tyre slides.
        float TyreCurve(float slipAngle)
        {
            float peak = Mathf.Max(0.5f, peakSlipAngle) * Mathf.Deg2Rad;
            float b = Mathf.Tan(Mathf.PI / (2f * tyreShape)) / peak;
            return Mathf.Sin(tyreShape * Mathf.Atan(b * slipAngle));
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
            if (otherCar) HitCar?.Invoke(impact);
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

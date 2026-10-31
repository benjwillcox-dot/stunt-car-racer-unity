using UnityEngine;

namespace StuntCar
{
    /// <summary>
    /// Builds a stunt car: the Blender model (Assets/Models/StuntCar) when the build script has
    /// hooked it up, otherwise a primitive stand-in with the same layout, so a fresh clone still runs.
    /// </summary>
    public static class CarFactory
    {
        const int IgnoreRaycastLayer = 2;
        static readonly string[] ModelWheels = { "Wheel_FL", "Wheel_FR", "Wheel_RL", "Wheel_RR" };
        static readonly string[] ModelCalipers = { "Caliper_FL", "Caliper_FR", "Caliper_RL", "Caliper_RR" };

        public static CarController Create(string name, Material paint, GameBootstrap a)
        {
            var go = new GameObject(name);
            go.layer = IgnoreRaycastLayer;
            go.AddComponent<Rigidbody>();
            return HasWheels(a.carModel) ? FromModel(go, paint, a) : FromPrimitives(go, paint, a);
        }

        static bool HasWheels(GameObject model)
        {
            if (!model) return false;
            foreach (var w in ModelWheels)
                if (!FindDeep(model.transform, w)) return false;
            return true;
        }

        // The FBX may or may not keep its own root node, so look at any depth.
        static Transform FindDeep(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }

        static void AddBox(GameObject go, Vector3 center, Vector3 size, GameBootstrap a)
        {
            var box = go.AddComponent<BoxCollider>();
            box.center = center;
            box.size = size;
            box.sharedMaterial = a.BodyPhysics;
        }

        /// <summary>
        /// The Blender car. Wheel mounts and radii are read off its Wheel_* objects so the physics
        /// matches what you see; the livery follows the paint (player yellow, rival white).
        /// </summary>
        static CarController FromModel(GameObject go, Material paint, GameBootstrap a)
        {
            // Physics shape: chassis plus roll cage, so roof landings hit something. The chassis box
            // keeps the primitive car's height so landings and scrapes feel the same.
            AddBox(go, new Vector3(0f, 0.6f, -0.03f), new Vector3(1.8f, 0.4f, 4.2f), a);
            AddBox(go, new Vector3(0f, 1.12f, -0.46f), new Vector3(0.96f, 0.62f, 1.0f), a);

            var model = Object.Instantiate(a.carModel, go.transform, false).transform;
            model.name = "Visual";
            // The controller treats +Z as the front; turn the model round if it imported facing back.
            if (go.transform.InverseTransformPoint(FindDeep(model, "Wheel_FL").position).z < 0f)
                model.localRotation = Quaternion.Euler(0f, 180f, 0f) * model.localRotation;
            var skin = paint == a.opponentPaint ? a.opponentCarSkin : a.playerCarSkin;
            foreach (var r in model.GetComponentsInChildren<Renderer>())
            {
                r.sharedMaterial = skin ? skin : paint;
                r.gameObject.layer = IgnoreRaycastLayer;
            }

            var car = go.AddComponent<CarController>();
            float resting = car.RestingSpringLength(ModelWheels.Length);
            var mounts = new Vector3[ModelWheels.Length];
            var radii = new float[ModelWheels.Length];
            var pivots = new Transform[ModelWheels.Length];
            var calipers = new Transform[ModelWheels.Length];
            for (int i = 0; i < ModelWheels.Length; i++)
            {
                // The controller moves and steers a pivot at the hub; the mesh hangs under it as modelled.
                var wheel = FindDeep(model, ModelWheels[i]);
                var pivot = new GameObject("Wheel" + i).transform;
                pivot.SetParent(go.transform, false);
                pivot.localPosition = go.transform.InverseTransformPoint(wheel.position);
                wheel.SetParent(pivot, true);
                var e = wheel.GetComponent<Renderer>().bounds.extents;
                radii[i] = Mathf.Max(e.y, e.z);
                mounts[i] = pivot.localPosition + Vector3.up * resting;
                pivots[i] = pivot;

                calipers[i] = FindDeep(model, ModelCalipers[i]);
                if (calipers[i]) calipers[i].SetParent(go.transform, true);
            }

            // Boost flames out of the turned-out side-pipe tips, just ahead of the rear wheels.
            var flames = new Transform[2];
            for (int s = 0; s < 2; s++)
            {
                float side = s == 0 ? 1f : -1f;
                var dir = new Vector3(side * 0.914f, 0f, -0.406f);
                var tip = new Vector3(side * 0.782f, 0.32f, -0.707f);
                flames[s] = Part(go.transform, PrimitiveType.Cube, tip + dir * 0.18f, new Vector3(0.1f, 0.1f, 0.45f),
                    Quaternion.LookRotation(dir).eulerAngles, a.boostFlame);
            }

            car.Init(mounts, pivots, radii);
            go.AddComponent<BoostFlames>().Init(car, flames[0], flames[1]);
            go.AddComponent<CarModelRig>().Init(car, pivots, calipers, FindDeep(model, "SteeringWheel"), FindDeep(model, "Driver"), FindDeep(model, "Engine"));
            go.AddComponent<CarRecovery>();
            return car;
        }

        /// <summary>
        /// Stand-in made of primitives: low wedge chassis, exposed V8 with chrome intake,
        /// roll cage, rear wing and big exposed wheels.
        /// </summary>
        static CarController FromPrimitives(GameObject go, Material paint, GameBootstrap a)
        {
            // Physics shape: chassis plus roll cage, so roof landings hit something.
            AddBox(go, new Vector3(0f, 0.6f, 0f), new Vector3(1.8f, 0.4f, 3.9f), a);
            AddBox(go, new Vector3(0f, 1.05f, -0.4f), new Vector3(1.3f, 0.5f, 1.5f), a);

            var visual = new GameObject("Visual").transform;
            visual.SetParent(go.transform, false);

            // Chassis and bodywork.
            Part(visual, PrimitiveType.Cube, new Vector3(0, 0.56f, 0), new Vector3(1.8f, 0.3f, 3.9f), Vector3.zero, paint);
            Part(visual, PrimitiveType.Cube, new Vector3(0, 0.62f, 2.05f), new Vector3(1.6f, 0.18f, 0.7f), new Vector3(14f, 0, 0), paint);
            Part(visual, PrimitiveType.Cube, new Vector3(0.92f, 0.62f, -0.3f), new Vector3(0.22f, 0.36f, 2.2f), Vector3.zero, a.darkMetal);
            Part(visual, PrimitiveType.Cube, new Vector3(-0.92f, 0.62f, -0.3f), new Vector3(0.22f, 0.36f, 2.2f), Vector3.zero, a.darkMetal);
            Part(visual, PrimitiveType.Cube, new Vector3(0, 0.8f, -0.45f), new Vector3(1.25f, 0.24f, 1.6f), Vector3.zero, a.darkMetal);

            // Exposed V8 on the nose.
            Part(visual, PrimitiveType.Cube, new Vector3(0, 0.86f, 1.15f), new Vector3(0.75f, 0.34f, 1.0f), Vector3.zero, a.darkMetal);
            Part(visual, PrimitiveType.Cube, new Vector3(0.3f, 0.95f, 1.15f), new Vector3(0.18f, 0.2f, 0.95f), Vector3.zero, a.chrome);
            Part(visual, PrimitiveType.Cube, new Vector3(-0.3f, 0.95f, 1.15f), new Vector3(0.18f, 0.2f, 0.95f), Vector3.zero, a.chrome);
            Part(visual, PrimitiveType.Cylinder, new Vector3(0, 1.12f, 1.15f), new Vector3(0.36f, 0.12f, 0.36f), Vector3.zero, a.chrome);

            // Roll cage.
            Part(visual, PrimitiveType.Cube, new Vector3(0.6f, 1.1f, -0.95f), new Vector3(0.07f, 0.6f, 0.07f), Vector3.zero, a.chrome);
            Part(visual, PrimitiveType.Cube, new Vector3(-0.6f, 1.1f, -0.95f), new Vector3(0.07f, 0.6f, 0.07f), Vector3.zero, a.chrome);
            Part(visual, PrimitiveType.Cube, new Vector3(0, 1.4f, -0.95f), new Vector3(1.27f, 0.07f, 0.07f), Vector3.zero, a.chrome);
            Part(visual, PrimitiveType.Cube, new Vector3(0.6f, 1.12f, -0.28f), new Vector3(0.06f, 0.06f, 1.45f), new Vector3(-23f, 0, 0), a.chrome);
            Part(visual, PrimitiveType.Cube, new Vector3(-0.6f, 1.12f, -0.28f), new Vector3(0.06f, 0.06f, 1.45f), new Vector3(-23f, 0, 0), a.chrome);

            // Front bumper bar, headlights, tail lights, mirrors.
            Part(visual, PrimitiveType.Cube, new Vector3(0, 0.5f, 2.42f), new Vector3(1.7f, 0.1f, 0.1f), Vector3.zero, a.chrome);
            Part(visual, PrimitiveType.Cube, new Vector3(0.6f, 0.66f, 2.36f), new Vector3(0.3f, 0.1f, 0.05f), Vector3.zero, a.floodlight);
            Part(visual, PrimitiveType.Cube, new Vector3(-0.6f, 0.66f, 2.36f), new Vector3(0.3f, 0.1f, 0.05f), Vector3.zero, a.floodlight);
            Part(visual, PrimitiveType.Cube, new Vector3(0.7f, 0.62f, -1.97f), new Vector3(0.3f, 0.12f, 0.05f), Vector3.zero, a.tailLight);
            Part(visual, PrimitiveType.Cube, new Vector3(-0.7f, 0.62f, -1.97f), new Vector3(0.3f, 0.12f, 0.05f), Vector3.zero, a.tailLight);
            Part(visual, PrimitiveType.Cube, new Vector3(0.78f, 0.98f, 0.15f), new Vector3(0.18f, 0.1f, 0.04f), new Vector3(0, -15f, 0), a.darkMetal);
            Part(visual, PrimitiveType.Cube, new Vector3(-0.78f, 0.98f, 0.15f), new Vector3(0.18f, 0.1f, 0.04f), new Vector3(0, 15f, 0), a.darkMetal);
            // Exhaust headers running from the V8 down the sides.
            Part(visual, PrimitiveType.Cube, new Vector3(0.5f, 0.75f, 0.35f), new Vector3(0.08f, 0.08f, 1.3f), new Vector3(-8f, 0, 0), a.chrome);
            Part(visual, PrimitiveType.Cube, new Vector3(-0.5f, 0.75f, 0.35f), new Vector3(0.08f, 0.08f, 1.3f), new Vector3(-8f, 0, 0), a.chrome);
            // Side skirts and a racing stripe.
            Part(visual, PrimitiveType.Cube, new Vector3(0, 0.715f, 0.2f), new Vector3(0.35f, 0.012f, 3.3f), Vector3.zero, a.kerbWhite);

            // Driver.
            Part(visual, PrimitiveType.Sphere, new Vector3(0, 1.12f, -0.5f), new Vector3(0.34f, 0.34f, 0.36f), Vector3.zero, a.helmet);

            // Rear wing.
            Part(visual, PrimitiveType.Cube, new Vector3(0, 1.22f, -1.8f), new Vector3(1.9f, 0.05f, 0.5f), new Vector3(-6f, 0, 0), paint);
            Part(visual, PrimitiveType.Cube, new Vector3(0.6f, 0.95f, -1.8f), new Vector3(0.05f, 0.5f, 0.3f), Vector3.zero, a.darkMetal);
            Part(visual, PrimitiveType.Cube, new Vector3(-0.6f, 0.95f, -1.8f), new Vector3(0.05f, 0.5f, 0.3f), Vector3.zero, a.darkMetal);

            // Exhausts and boost flames.
            Part(visual, PrimitiveType.Cylinder, new Vector3(0.45f, 0.55f, -2.0f), new Vector3(0.14f, 0.15f, 0.14f), new Vector3(90f, 0, 0), a.chrome);
            Part(visual, PrimitiveType.Cylinder, new Vector3(-0.45f, 0.55f, -2.0f), new Vector3(0.14f, 0.15f, 0.14f), new Vector3(90f, 0, 0), a.chrome);
            var flameL = Part(visual, PrimitiveType.Cube, new Vector3(0.45f, 0.55f, -2.45f), new Vector3(0.12f, 0.12f, 0.6f), Vector3.zero, a.boostFlame);
            var flameR = Part(visual, PrimitiveType.Cube, new Vector3(-0.45f, 0.55f, -2.45f), new Vector3(0.12f, 0.12f, 0.6f), Vector3.zero, a.boostFlame);

            // Wheels: a pivot the controller moves and steers, with the tyre turned on its side underneath.
            var mounts = new[]
            {
                new Vector3(1.0f, 0.73f, 1.35f), new Vector3(-1.0f, 0.73f, 1.35f),
                new Vector3(1.0f, 0.73f, -1.35f), new Vector3(-1.0f, 0.73f, -1.35f),
            };
            var wheelPivots = new Transform[mounts.Length];
            for (int i = 0; i < mounts.Length; i++)
            {
                var pivot = new GameObject("Wheel" + i).transform;
                pivot.SetParent(go.transform, false);
                pivot.localPosition = mounts[i];
                bool rear = mounts[i].z < 0;
                float width = rear ? 0.21f : 0.17f;
                Part(pivot, PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.72f, width, 0.72f), new Vector3(0, 0, 90f), a.tyre);
                Part(pivot, PrimitiveType.Cylinder, new Vector3(Mathf.Sign(mounts[i].x) * 0.02f, 0, 0), new Vector3(0.42f, width + 0.01f, 0.42f), new Vector3(0, 0, 90f), a.chrome);
                // A spoke so you can see the wheel turn.
                Part(pivot, PrimitiveType.Cube, new Vector3(Mathf.Sign(mounts[i].x) * (width + 0.01f), 0, 0), new Vector3(0.02f, 0.5f, 0.08f), Vector3.zero, a.darkMetal);
                wheelPivots[i] = pivot;
            }

            var car = go.AddComponent<CarController>();
            car.Init(mounts, wheelPivots);
            go.AddComponent<BoostFlames>().Init(car, flameL, flameR);
            go.AddComponent<CarRecovery>();
            return car;
        }

        static Transform Part(Transform parent, PrimitiveType type, Vector3 pos, Vector3 scale, Vector3 euler, Material m)
        {
            var p = GameObject.CreatePrimitive(type);
            var col = p.GetComponent<Collider>();
            col.enabled = false;
            Object.Destroy(col);
            p.layer = IgnoreRaycastLayer;
            p.transform.SetParent(parent, false);
            p.transform.localPosition = pos;
            p.transform.localRotation = Quaternion.Euler(euler);
            p.transform.localScale = scale;
            p.GetComponent<MeshRenderer>().sharedMaterial = m;
            return p.transform;
        }
    }

    /// <summary>
    /// Moving extras on the Blender car: brake calipers follow the wheels and steer without spinning,
    /// the steering wheel turns with the front wheels, and the V8 rocks on its mounts.
    /// </summary>
    public class CarModelRig : MonoBehaviour
    {
        const float SteeringRatio = 3.5f;

        CarController car;
        Transform[] wheels, calipers;
        Quaternion[] caliperRest;
        Transform steeringWheel, engine;
        Quaternion steeringRest, engineRest;
        Vector3 steeringAxis, engineAxis;

        public void Init(CarController c, Transform[] wheelPivots, Transform[] wheelCalipers, Transform wheel, Transform driver, Transform v8)
        {
            car = c;
            wheels = wheelPivots;
            calipers = wheelCalipers;
            caliperRest = new Quaternion[calipers.Length];
            for (int i = 0; i < calipers.Length; i++)
                if (calipers[i]) caliperRest[i] = calipers[i].localRotation;

            steeringWheel = wheel;
            if (steeringWheel && driver)
            {
                steeringRest = steeringWheel.localRotation;
                // The column is whichever local axis points most nearly at the driver's body.
                Vector3 seat = driver.TryGetComponent<Renderer>(out var body) ? body.bounds.center : driver.position;
                Vector3 d = steeringWheel.InverseTransformDirection(seat - steeringWheel.position);
                Vector3 abs = new Vector3(Mathf.Abs(d.x), Mathf.Abs(d.y), Mathf.Abs(d.z));
                if (abs.x >= abs.y && abs.x >= abs.z) steeringAxis = new Vector3(Mathf.Sign(d.x), 0f, 0f);
                else if (abs.y >= abs.z) steeringAxis = new Vector3(0f, Mathf.Sign(d.y), 0f);
                else steeringAxis = new Vector3(0f, 0f, Mathf.Sign(d.z));
            }
            else steeringWheel = null;

            engine = v8;
            if (engine)
            {
                engineRest = engine.localRotation;
                engineAxis = engine.parent.InverseTransformDirection(c.transform.forward); // crank axis
            }
        }

        // After CarController.Update has placed the wheel pivots.
        void LateUpdate()
        {
            if (!car) return;
            float steer = car.CurrentSteerAngle;
            for (int i = 0; i < calipers.Length; i++)
            {
                if (!calipers[i]) continue;
                bool front = wheels[i].localPosition.z > 0f;
                calipers[i].localPosition = wheels[i].localPosition;
                calipers[i].localRotation = Quaternion.Euler(0f, front ? steer : 0f, 0f) * caliperRest[i];
            }

            // Turning clockwise as the driver sees it is a positive angle about the axis pointing at them.
            if (steeringWheel)
                steeringWheel.localRotation = steeringRest * Quaternion.AngleAxis(steer * SteeringRatio, steeringAxis);

            if (engine)
            {
                float rev = car.Wrecked ? 0f : 0.3f + car.Throttle * 0.7f;
                float rock = (Mathf.PerlinNoise(Time.time * 28f, 0.5f) - 0.5f) * 1.6f * rev;
                engine.localRotation = Quaternion.AngleAxis(rock, engineAxis) * engineRest;
            }
        }
    }

    /// <summary>Flickers the exhaust flames while boosting.</summary>
    public class BoostFlames : MonoBehaviour
    {
        CarController car;
        Transform left, right;
        Vector3 baseScale;

        public void Init(CarController c, Transform l, Transform r)
        {
            car = c; left = l; right = r;
            baseScale = l.localScale;
        }

        void Update()
        {
            if (!car) return;
            bool on = car.IsBoosting;
            left.gameObject.SetActive(on);
            right.gameObject.SetActive(on);
            if (on)
            {
                float f = 0.7f + Mathf.PerlinNoise(Time.time * 25f, 0f) * 0.8f;
                left.localScale = new Vector3(baseScale.x, baseScale.y, baseScale.z * f);
                right.localScale = new Vector3(baseScale.x, baseScale.y, baseScale.z * (1.5f - f * 0.5f));
            }
        }
    }
}

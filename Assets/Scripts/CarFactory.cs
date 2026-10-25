using UnityEngine;

namespace StuntCar
{
    /// <summary>
    /// Builds a stunt car out of primitives: low wedge chassis, exposed V8 with chrome intake,
    /// roll cage, rear wing and big exposed wheels. Placeholder art until real models exist.
    /// </summary>
    public static class CarFactory
    {
        const int IgnoreRaycastLayer = 2;

        public static CarController Create(string name, Material paint, GameBootstrap a)
        {
            var go = new GameObject(name);
            go.layer = IgnoreRaycastLayer;
            go.AddComponent<Rigidbody>();

            // Physics shape: chassis plus roll cage, so roof landings hit something.
            var body = go.AddComponent<BoxCollider>();
            body.center = new Vector3(0f, 0.6f, 0f);
            body.size = new Vector3(1.8f, 0.4f, 3.9f);
            body.sharedMaterial = a.BodyPhysics;
            var cage = go.AddComponent<BoxCollider>();
            cage.center = new Vector3(0f, 1.05f, -0.4f);
            cage.size = new Vector3(1.3f, 0.5f, 1.5f);
            cage.sharedMaterial = a.BodyPhysics;

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

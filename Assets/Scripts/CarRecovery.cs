using System.Collections;
using UnityEngine;

namespace StuntCar
{
    /// <summary>
    /// The crane. A lifting frame comes down on its cable with four slings hanging from it, a
    /// steel platform rises out of the ground under the car, and the car is lifted on it, levelled,
    /// swung back over the nearest piece of road and set down. The platform then sinks into the
    /// deck and the frame goes back up. The time this takes is the penalty.
    ///
    /// The rig stays below and above the driver, so the cockpit view shows the platform's
    /// corner posts and the slings beside the car, not a hook in your face.
    /// </summary>
    public class CarRecovery : MonoBehaviour
    {
        public bool InProgress { get; private set; }

        // Platform: deck top at its origin. Frame: hung this far above the deck.
        const float PlatformWidth = 3f, PlatformLength = 5.6f, PlatformThickness = 0.14f, PostHeight = 0.9f;
        const float FrameHeight = 6f, Hidden = 1.2f;
        static readonly Vector3[] SlingCorners =
        {
            new Vector3(-1.42f, 0f, -2.72f), new Vector3(1.42f, 0f, -2.72f), new Vector3(-1.42f, 0f, 2.72f), new Vector3(1.42f, 0f, 2.72f),
        };
        static Mesh platformMesh, frameMesh;

        CarController car;
        Transform rig, platform, frame;
        LineRenderer[] slings;
        LineRenderer mainCable;
        Renderer platformRenderer;
        bool slingsAttached;
        Bounds shape;
        bool haveShape;

        void Awake()
        {
            car = GetComponent<CarController>();
        }

        public void Begin(Track track, int index)
        {
            if (InProgress) return;
            StartCoroutine(Run(track, index));
        }

        IEnumerator Run(Track track, int index)
        {
            InProgress = true;
            var rb = car.Body;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
            rb.interpolation = RigidbodyInterpolation.None; // we move the transform directly below
            if (!haveShape) { shape = LocalShape(); haveShape = true; }
            CreateRig();

            Vector3 startPos = transform.position;
            Quaternion startRot = transform.rotation;
            Vector3 targetPos = track.Center(index) + track.Up(index) * 0.16f;
            Quaternion targetRot = track.Rotation(index);
            float liftHeight = Mathf.Max(startPos.y, targetPos.y) + 6f;
            Vector3 liftedStart = new Vector3(startPos.x, liftHeight, startPos.z);
            Vector3 liftedTarget = new Vector3(targetPos.x, liftHeight, targetPos.z);
            Quaternion levelRot = Quaternion.LookRotation(Vector3.ProjectOnPlane(startRot * Vector3.forward, Vector3.up).normalized + Vector3.forward * 0.001f);

            // Where the platform comes up: the ground or deck under the car (or just under the car,
            // if the driver called the crane in mid-air).
            Vector3 centre0 = startPos + startRot * shape.center;
            float lowest0 = Lowest(startPos, startRot);
            float surface = Physics.Raycast(centre0 + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 60f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                ? hit.point.y : lowest0;
            float deckTop = Mathf.Max(surface, lowest0 - 2f) + 0.02f;
            Vector3 under = new Vector3(centre0.x, deckTop, centre0.z);

            // Frame comes down, slings dangling to the ground round the car; the platform waits out of sight.
            PlacePlatform(under - Vector3.up * Hidden, levelRot);
            platformRenderer.enabled = false;
            yield return Animate(0.7f, t => PlaceFrame(Mathf.Lerp(40f, FrameHeight, t)));

            // Platform rises out of the ground under the car.
            platformRenderer.enabled = true;
            yield return Animate(0.25f, t =>
            {
                PlacePlatform(under - Vector3.up * (Hidden * (1f - t)), levelRot);
                PlaceFrame(FrameHeight);
            });

            // Lift and level the car, the platform staying just under its lowest point.
            Vector3 centre1 = liftedStart + levelRot * shape.center;
            float topEnd = liftHeight + shape.min.y - 0.01f;
            yield return Animate(1f, t =>
            {
                Quaternion rot = Quaternion.Slerp(startRot, levelRot, t);
                Vector3 c = Vector3.Lerp(centre0, centre1, t);
                Vector3 pos = c - rot * shape.center;
                transform.SetPositionAndRotation(pos, rot);
                float top = Mathf.Clamp(Lowest(pos, rot) - 0.01f, deckTop, Mathf.Lerp(deckTop, topEnd, t));
                PlacePlatform(new Vector3(c.x, top, c.z), levelRot);
                PlaceFrame(FrameHeight);
            });
            // Swing over the road, then lower onto the deck, riding on the platform.
            yield return Animate(1.2f, t => Carry(Vector3.Lerp(liftedStart, liftedTarget, t), Quaternion.Slerp(levelRot, targetRot, t)));
            yield return Animate(0.8f, t => Carry(Vector3.Lerp(liftedTarget, targetPos, t), targetRot));

            rb.isKinematic = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            // The platform drops away into the deck (faster than the car falls the last few cm)
            // while the frame lets go and goes back up.
            slingsAttached = false;
            Vector3 platformAt = platform.position, frameAt = frame.position, down = targetRot * Vector3.down;
            yield return Animate(0.5f, t =>
            {
                platform.position = platformAt + down * (1.4f * Mathf.Clamp01(t * 1.6f));
                frame.position = frameAt + Vector3.up * (30f * t);
                UpdateCables();
            });
            DestroyRig();
            InProgress = false;
        }

        static IEnumerator Animate(float duration, System.Action<float> step)
        {
            for (float time = 0f; time < duration; time += Time.deltaTime)
            {
                step(Mathf.SmoothStep(0f, 1f, time / duration));
                yield return null;
            }
            step(1f);
        }

        // The car with the platform fixed underneath it.
        void Carry(Vector3 pos, Quaternion rot)
        {
            transform.SetPositionAndRotation(pos, rot);
            PlacePlatform(pos + rot * new Vector3(shape.center.x, shape.min.y - 0.01f, shape.center.z), rot);
            PlaceFrame(FrameHeight);
        }

        void PlacePlatform(Vector3 deckTop, Quaternion rot) => platform.SetPositionAndRotation(deckTop, rot);

        // The frame hangs straight above the platform, square to it.
        void PlaceFrame(float height)
        {
            frame.SetPositionAndRotation(platform.position + platform.up * height, platform.rotation);
            slingsAttached = true;
            UpdateCables();
        }

        void UpdateCables()
        {
            for (int i = 0; i < slings.Length; i++)
            {
                Vector3 top = frame.TransformPoint(SlingCorners[i]);
                // Once let go, the slings hang free below the frame.
                Vector3 bottom = slingsAttached ? platform.TransformPoint(SlingCorners[i] + Vector3.up * PostHeight) : top + Vector3.down * (FrameHeight - PostHeight);
                slings[i].SetPosition(0, top);
                slings[i].SetPosition(1, bottom);
            }
            Vector3 hook = frame.TransformPoint(new Vector3(0f, 1.5f, 0f));
            mainCable.SetPosition(0, hook);
            mainCable.SetPosition(1, hook + Vector3.up * 40f);
        }

        // The car's extent in its own space, from its meshes, so the platform can sit under it.
        Bounds LocalShape()
        {
            var b = new Bounds(new Vector3(0f, 0.7f, 0f), new Vector3(2f, 1.4f, 4.2f));
            bool first = true;
            foreach (var mf in GetComponentsInChildren<MeshFilter>())
            {
                if (!mf.sharedMesh) continue;
                Matrix4x4 m = transform.worldToLocalMatrix * mf.transform.localToWorldMatrix;
                Bounds mb = mf.sharedMesh.bounds;
                for (int c = 0; c < 8; c++)
                {
                    Vector3 corner = mb.center + Vector3.Scale(mb.extents, new Vector3((c & 1) == 0 ? -1 : 1, (c & 2) == 0 ? -1 : 1, (c & 4) == 0 ? -1 : 1));
                    Vector3 p = m.MultiplyPoint3x4(corner);
                    if (first) { b = new Bounds(p, Vector3.zero); first = false; }
                    else b.Encapsulate(p);
                }
            }
            return b;
        }

        // Height of the car's lowest corner in a given pose.
        float Lowest(Vector3 pos, Quaternion rot)
        {
            float lowest = float.MaxValue;
            for (int c = 0; c < 8; c++)
            {
                Vector3 corner = shape.center + Vector3.Scale(shape.extents, new Vector3((c & 1) == 0 ? -1 : 1, (c & 2) == 0 ? -1 : 1, (c & 4) == 0 ? -1 : 1));
                lowest = Mathf.Min(lowest, (pos + rot * corner).y);
            }
            return lowest;
        }

        void CreateRig()
        {
            var a = GameBootstrap.Instance;
            if (platformMesh == null) platformMesh = BuildPlatformMesh();
            if (frameMesh == null) frameMesh = BuildFrameMesh();

            rig = new GameObject("Crane").transform;
            platform = new GameObject("Platform").transform;
            platform.SetParent(rig, false);
            platform.gameObject.AddComponent<MeshFilter>().sharedMesh = platformMesh;
            platformRenderer = platform.gameObject.AddComponent<MeshRenderer>();
            platformRenderer.sharedMaterials = new[] { a.scaffold, a.helmet, a.darkMetal };

            frame = new GameObject("Lifting Frame").transform;
            frame.SetParent(rig, false);
            frame.gameObject.AddComponent<MeshFilter>().sharedMesh = frameMesh;
            frame.gameObject.AddComponent<MeshRenderer>().sharedMaterials = new[] { a.helmet, a.darkMetal };

            slings = new LineRenderer[SlingCorners.Length];
            for (int i = 0; i < slings.Length; i++) slings[i] = MakeCable("Sling", 0.045f, a.cable);
            mainCable = MakeCable("Cable", 0.07f, a.cable);
        }

        LineRenderer MakeCable(string name, float width, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(rig, false);
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = mat;
            line.positionCount = 2;
            line.startWidth = line.endWidth = width;
            line.useWorldSpace = true;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return line;
        }

        // Chequer-plate deck with hazard-striped edges and a lifting post at each corner.
        // Submeshes: 0 steel, 1 yellow, 2 black.
        static Mesh BuildPlatformMesh()
        {
            var mb = new MeshBuilder(3);
            float hx = PlatformWidth * 0.5f, hz = PlatformLength * 0.5f;
            mb.AddBox(0, Matrix4x4.TRS(new Vector3(0f, -PlatformThickness * 0.5f, 0f), Quaternion.identity, new Vector3(PlatformWidth, PlatformThickness, PlatformLength)));
            const float stripe = 0.35f, kick = 0.1f, lip = 0.08f;
            int along = Mathf.RoundToInt(PlatformLength / stripe), across = Mathf.RoundToInt(PlatformWidth / stripe);
            for (int i = 0; i < along; i++)
            {
                float z = -hz + (i + 0.5f) * PlatformLength / along;
                var size = new Vector3(lip, kick, PlatformLength / along);
                mb.AddBox(1 + (i & 1), Matrix4x4.TRS(new Vector3(-hx + lip * 0.5f, kick * 0.5f, z), Quaternion.identity, size));
                mb.AddBox(1 + (i & 1), Matrix4x4.TRS(new Vector3(hx - lip * 0.5f, kick * 0.5f, z), Quaternion.identity, size));
            }
            for (int i = 0; i < across; i++)
            {
                float x = -hx + lip + (i + 0.5f) * (PlatformWidth - 2f * lip) / across;
                var size = new Vector3((PlatformWidth - 2f * lip) / across, kick, lip);
                mb.AddBox(1 + (i & 1), Matrix4x4.TRS(new Vector3(x, kick * 0.5f, -hz + lip * 0.5f), Quaternion.identity, size));
                mb.AddBox(1 + (i & 1), Matrix4x4.TRS(new Vector3(x, kick * 0.5f, hz - lip * 0.5f), Quaternion.identity, size));
            }
            foreach (var c in SlingCorners)
            {
                mb.AddBox(1, Matrix4x4.TRS(c + Vector3.up * (PostHeight * 0.5f), Quaternion.identity, new Vector3(0.12f, PostHeight, 0.12f)));
                mb.AddBox(2, Matrix4x4.TRS(c + Vector3.up * PostHeight, Quaternion.identity, new Vector3(0.18f, 0.12f, 0.18f)));
            }
            return mb.ToMesh("CranePlatform");
        }

        // Rectangular spreader frame with a bridle up to the hook block. Submeshes: 0 yellow, 1 black.
        static Mesh BuildFrameMesh()
        {
            var mb = new MeshBuilder(2);
            Vector3 a = SlingCorners[0], b = SlingCorners[1], c = SlingCorners[2], d = SlingCorners[3];
            mb.AddBeam(0, a, b, 0.2f);
            mb.AddBeam(0, c, d, 0.2f);
            mb.AddBeam(0, a, c, 0.2f);
            mb.AddBeam(0, b, d, 0.2f);
            mb.AddBeam(0, (a + b) * 0.5f, (c + d) * 0.5f, 0.14f);
            var hook = new Vector3(0f, 1.1f, 0f);
            foreach (var corner in SlingCorners) mb.AddBeam(1, corner, hook, 0.05f);
            mb.AddBox(1, Matrix4x4.TRS(hook + Vector3.up * 0.15f, Quaternion.identity, new Vector3(0.45f, 0.6f, 0.35f)));
            return mb.ToMesh("CraneFrame");
        }

        void DestroyRig()
        {
            if (rig) Destroy(rig.gameObject);
            rig = null;
        }

        void OnDisable()
        {
            DestroyRig();
            InProgress = false;
        }
    }
}

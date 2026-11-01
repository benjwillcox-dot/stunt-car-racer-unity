using UnityEngine;

namespace StuntCar
{
    /// <summary>
    /// A drawbridge over a gap in the deck: two leaves hinged at the ends of the gap, lifting
    /// together tips-up like a bascule bridge and lowering again, round and round. Down, the
    /// road is continuous; part way up, the near leaf is a ramp and the far one a landing; high
    /// up, it throws you a long way. Lamps on the near portal show green while it's down and red
    /// while it's moving or up.
    /// </summary>
    public class MovingBridge : MonoBehaviour
    {
        public const float MaxAngle = 26f;
        const float DownTime = 4f, RiseTime = 2.5f, UpTime = 3f, FallTime = 2.5f;
        const float Period = DownTime + RiseTime + UpTime + FallTime;

        /// <summary>The track sample where the bridge starts (the near hinge).</summary>
        public int StartIndex { get; private set; }
        /// <summary>How far the leaves are lifted, in degrees.</summary>
        public float Angle { get; private set; }

        Rigidbody near, far;
        Quaternion nearRest, farRest;
        Renderer[] lamps;
        Material red, green;
        bool showingRed;

        public void Init(Track track, int startIndex, int endIndex, GameBootstrap a)
        {
            StartIndex = startIndex;
            Vector3 hingeA = track.Center(startIndex), hingeB = track.Center(endIndex);
            Vector3 up = track.Up(startIndex), along = (hingeB - hingeA).normalized;
            float length = Vector3.Distance(hingeA, hingeB) * 0.5f;
            float hw = track.HalfWidth;

            nearRest = Quaternion.LookRotation(along, up);
            farRest = Quaternion.LookRotation(-along, up);
            near = MakeLeaf("Near leaf", hingeA, nearRest, length, hw, track.Distance(startIndex), 1f, a);
            far = MakeLeaf("Far leaf", hingeB, farRest, length, hw, track.Distance(endIndex), -1f, a);

            // A steel portal over each hinge; the near one carries the signal lamps.
            var mb = new MeshBuilder(1);
            Vector3 right = Vector3.Cross(up, along).normalized;
            foreach (Vector3 hinge in new[] { hingeA, hingeB })
            {
                foreach (float side in new[] { -1f, 1f })
                    mb.AddBeam(0, hinge + right * (side * (hw + 0.7f)) - up * Track.Thickness, hinge + right * (side * (hw + 0.7f)) + up * 7.2f, 0.5f);
                mb.AddBeam(0, hinge - right * (hw + 0.95f) + up * 7f, hinge + right * (hw + 0.95f) + up * 7f, 0.45f);
            }
            var portals = new GameObject("Bridge portals");
            portals.transform.SetParent(transform, false);
            portals.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh("Bridge portals");
            portals.AddComponent<MeshRenderer>().sharedMaterial = a.scaffold;

            red = a.tailLight;
            green = new Material(a.tailLight) { name = "Signal green (runtime)" };
            green.SetColor("_BaseColor", new Color(0.05f, 0.8f, 0.2f));
            green.SetColor("_EmissionColor", new Color(0.15f, 3f, 0.5f));
            lamps = new Renderer[2];
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;
                var lamp = GameObject.CreatePrimitive(PrimitiveType.Cube);
                lamp.name = "Signal";
                Destroy(lamp.GetComponent<Collider>());
                lamp.transform.SetParent(transform, false);
                lamp.transform.SetPositionAndRotation(hingeA + right * (side * (hw + 0.7f)) + up * 4.8f - along * 0.35f, nearRest);
                lamp.transform.localScale = new Vector3(0.55f, 0.55f, 0.25f);
                lamps[i] = lamp.GetComponent<Renderer>();
            }
            ShowRed(false, true);
        }

        Rigidbody MakeLeaf(string name, Vector3 hinge, Quaternion rest, float length, float hw, float distance, float dir, GameBootstrap a)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.SetPositionAndRotation(hinge, rest);
            go.AddComponent<MeshFilter>().sharedMesh = LeafMesh(length, hw, distance, dir);
            go.AddComponent<MeshRenderer>().sharedMaterials = new[] { a.AsphaltRuntime, a.scaffold, a.PropsRuntime };
            var box = go.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, -Track.Thickness * 0.5f, length * 0.5f);
            box.size = new Vector3(hw * 2f, Track.Thickness, length);
            box.sharedMaterial = a.RoadPhysics;
            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            return rb;
        }

        // A leaf in hinge space: x across, y up (road surface at 0), z out from the hinge to the tip.
        // The asphalt carries on from the road's (u across, v = distance / 8); 'dir' is -1 for the
        // far leaf, which points back along the track. A chequered strip marks the tip.
        static Mesh LeafMesh(float length, float hw, float distance, float dir)
        {
            var mb = new MeshBuilder(3);
            float inner = hw - 0.7f, tip = length - 0.8f;
            float u0 = 0.5f - dir * hw / (2f * inner), u1 = 0.5f + dir * hw / (2f * inner);
            float v0 = distance / 8f, v1 = (distance + dir * tip) / 8f;
            mb.AddQuad(0, new Vector3(-hw, 0, 0), new Vector3(-hw, 0, tip), new Vector3(hw, 0, tip), new Vector3(hw, 0, 0),
                new Vector2(u0, v0), new Vector2(u0, v1), new Vector2(u1, v1), new Vector2(u1, v0));
            Rect chequer = ProceduralTextures.ChequerUV;
            mb.AddQuad(2, new Vector3(-hw, 0, tip), new Vector3(-hw, 0, length), new Vector3(hw, 0, length), new Vector3(hw, 0, tip),
                new Vector2(chequer.xMin, chequer.yMin), new Vector2(chequer.xMin, chequer.yMin + chequer.height * 0.1f),
                new Vector2(chequer.xMax, chequer.yMin + chequer.height * 0.1f), new Vector2(chequer.xMax, chequer.yMin));
            // Steel underneath, its top tucked just below the road surface.
            mb.AddBox(1, Matrix4x4.TRS(new Vector3(0f, -(Track.Thickness + 0.06f) * 0.5f, length * 0.5f), Quaternion.identity,
                new Vector3(hw * 2f, Track.Thickness - 0.06f, length)));
            return mb.ToMesh("Drawbridge leaf");
        }

        void FixedUpdate()
        {
            if (!near) return;
            float t = Time.fixedTime % Period, lift;
            if (t < DownTime) lift = 0f;
            else if (t < DownTime + RiseTime) lift = Mathf.SmoothStep(0f, 1f, (t - DownTime) / RiseTime);
            else if (t < DownTime + RiseTime + UpTime) lift = 1f;
            else lift = 1f - Mathf.SmoothStep(0f, 1f, (t - DownTime - RiseTime - UpTime) / FallTime);
            Angle = lift * MaxAngle;

            // Tips up: about each leaf's own x axis.
            var raise = Quaternion.Euler(-Angle, 0f, 0f);
            near.MoveRotation(nearRest * raise);
            far.MoveRotation(farRest * raise);
            ShowRed(Angle > 0.5f, false);
        }

        void ShowRed(bool on, bool force)
        {
            if (on == showingRed && !force) return;
            showingRed = on;
            foreach (var l in lamps) l.sharedMaterial = on ? red : green;
        }

        void OnDestroy()
        {
            if (green) Destroy(green);
        }
    }
}

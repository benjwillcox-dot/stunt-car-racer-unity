using UnityEngine;

namespace StuntCar
{
    /// <summary>
    /// Tyre smoke when wheels slide or spin, sparks when the chassis scrapes the deck,
    /// and a burst of dust on hard landings and crashes.
    /// </summary>
    public class CarEffects : MonoBehaviour
    {
        static Texture2D softDot;

        CarController car;
        ParticleSystem smoke, sparks, dust;
        float smokeCarry;

        public void Init(CarController c)
        {
            car = c;
            var a = GameBootstrap.Instance;
            if (softDot == null) softDot = MakeSoftDot();
            if (a.particleSmoke) a.particleSmoke.SetTexture("_BaseMap", softDot);
            if (a.particleSpark) a.particleSpark.SetTexture("_BaseMap", softDot);

            smoke = MakeSystem("TyreSmoke", a.particleSmoke, 400, 1.8f, 1.2f, new Color(0.85f, 0.85f, 0.85f, 0.45f), 0.6f, 3.2f, -0.05f);
            dust = MakeSystem("Dust", a.particleSmoke, 300, 2.2f, 2.0f, new Color(0.62f, 0.56f, 0.45f, 0.55f), 0.8f, 4f, -0.02f);
            sparks = MakeSystem("Sparks", a.particleSpark, 400, 0.45f, 0.12f, new Color(1f, 0.75f, 0.35f, 1f), 1f, 0.3f, 1.2f);

            car.Impact += OnImpact;
            car.Scrape += OnScrape;
        }

        ParticleSystem MakeSystem(string name, Material mat, int max, float life, float size, Color color, float sizeStart, float sizeEnd, float gravity)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = life;
            main.startSpeed = 0f;
            main.startSize = size;
            main.startColor = color;
            main.maxParticles = max;
            main.gravityModifier = gravity;
            main.playOnAwake = false;

            var emission = ps.emission;
            emission.enabled = false;
            var shape = ps.shape;
            shape.enabled = false;

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.6f, 0.4f), new GradientAlphaKey(0f, 1f) });
            col.color = g;

            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, sizeStart, 1f, sizeEnd));

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            if (gravity > 0.5f)
            {
                // Sparks streak along their direction of travel.
                r.renderMode = ParticleSystemRenderMode.Stretch;
                r.velocityScale = 0.04f;
                r.lengthScale = 1.5f;
            }
            ps.Play();
            return ps;
        }

        void Update()
        {
            if (!car || car.Body == null || car.Body.isKinematic) return;

            // Smoke from each sliding wheel, scaled by how hard it slides.
            for (int i = 0; i < car.WheelCount; i++)
            {
                if (!car.WheelGrounded(i)) continue;
                float slip = car.WheelSlip(i);
                if (slip < 3.5f) continue;
                smokeCarry += Mathf.Clamp(slip - 3.5f, 0f, 10f) * 4f * Time.deltaTime;
                while (smokeCarry >= 1f)
                {
                    smokeCarry -= 1f;
                    Emit(smoke, car.WheelContact(i) + Vector3.up * 0.25f + Random.insideUnitSphere * 0.2f,
                         car.Body.linearVelocity * 0.15f + Vector3.up * 0.6f + Random.insideUnitSphere * 0.4f);
                }
            }
        }

        void OnImpact(Vector3 point, float speed)
        {
            int n = Mathf.Clamp(Mathf.RoundToInt(speed * 3f), 4, 50);
            for (int k = 0; k < n; k++)
            {
                Vector3 dir = Random.onUnitSphere;
                dir.y = Mathf.Abs(dir.y) * 0.5f;
                Emit(dust, point + dir * 0.8f, dir * (1.5f + speed * 0.25f));
            }
            if (speed > 6f) OnScrape(point, speed * 1.5f);
        }

        void OnScrape(Vector3 point, float speed)
        {
            int n = Mathf.Clamp(Mathf.RoundToInt(speed * 0.4f), 1, 12);
            Vector3 back = -car.Body.linearVelocity.normalized;
            for (int k = 0; k < n; k++)
            {
                Vector3 v = (back + Random.insideUnitSphere * 0.6f + Vector3.up * 0.5f).normalized * (3f + Random.value * speed * 0.3f);
                Emit(sparks, point, car.Body.linearVelocity * 0.6f + v);
            }
        }

        static void Emit(ParticleSystem ps, Vector3 pos, Vector3 vel)
        {
            var ep = new ParticleSystem.EmitParams { position = pos, velocity = vel, applyShapeToPosition = false };
            ps.Emit(ep, 1);
        }

        static Texture2D MakeSoftDot()
        {
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = "SoftDot", wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                    float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    a = a * a * (3f - 2f * a);
                    px[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }
            tex.SetPixels32(px);
            tex.Apply(true);
            return tex;
        }
    }
}

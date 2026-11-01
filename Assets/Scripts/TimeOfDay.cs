using System.Collections.Generic;
using UnityEngine;

namespace StuntCar
{
    /// <summary>
    /// Day or night. By night the sky goes dark and the sun turns into a dim moon, so the track is
    /// lit by the four corner floodlights (<see cref="StadiumLights"/>), each casting its own
    /// shadow. The lamps and adverts glow, and the cars run with their headlights on.
    /// </summary>
    public static class TimeOfDay
    {
        public static readonly Color AdvertGlowDay = new Color(0.03f, 0.03f, 0.03f);
        public static readonly Color AdvertGlowNight = new Color(0.6f, 0.6f, 0.6f);
        // The grandstands' suite windows, press boxes, clock faces and lamps under the roofs.
        public static readonly Color GrandstandGlowDay = new Color(0.06f, 0.06f, 0.06f);
        public static readonly Color GrandstandGlowNight = new Color(1.6f, 1.6f, 1.6f);
        const float LampGlowNight = 3.5f;   // floodlight lamps' emission, times the day value

        public static bool Night { get; private set; }
        /// <summary>Raised with the new setting whenever it changes.</summary>
        public static event System.Action<bool> Changed;

        struct Look
        {
            public Color sky, equator, ground, fog, sunColor;
            public float fogStart, fogEnd, reflections, sunIntensity, sunShadow;
            public Quaternion sunRotation;
            public Material skybox;
        }

        static bool captured;
        static Look day, night;
        static Light sun;
        static Color lampDay;

        public static void Set(bool isNight)
        {
            if (!captured) Capture();
            else if (isNight == Night) return;
            Night = isNight;
            Apply(isNight ? night : day);
            var a = GameBootstrap.Instance;
            if (a)
            {
                a.FloodlightRuntime.SetColor("_EmissionColor", isNight ? lampDay * LampGlowNight : lampDay);
                a.AdvertsRuntime.SetColor("_EmissionColor", isNight ? AdvertGlowNight : AdvertGlowDay);
                if (a.GrandstandRuntime)
                    a.GrandstandRuntime.SetColor("_EmissionColor", isNight ? GrandstandGlowNight : GrandstandGlowDay);
            }
            Changed?.Invoke(isNight);
        }

        // Day is whatever the scene was built with; night is set against it.
        static void Capture()
        {
            captured = true;
            sun = RenderSettings.sun;
            if (!sun)
                foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                    if (l.type == LightType.Directional) { sun = l; break; }

            day = new Look
            {
                sky = RenderSettings.ambientSkyColor, equator = RenderSettings.ambientEquatorColor, ground = RenderSettings.ambientGroundColor,
                fog = RenderSettings.fogColor, fogStart = RenderSettings.fogStartDistance, fogEnd = RenderSettings.fogEndDistance,
                reflections = RenderSettings.reflectionIntensity, skybox = RenderSettings.skybox,
                sunColor = sun ? sun.color : Color.white, sunIntensity = sun ? sun.intensity : 1f,
                sunShadow = sun ? sun.shadowStrength : 1f, sunRotation = sun ? sun.transform.rotation : Quaternion.identity,
            };

            Material nightSky = null;
            if (day.skybox)
            {
                nightSky = new Material(day.skybox) { name = "Night sky (runtime)" };
                nightSky.SetFloat("_Exposure", 0.07f);
                nightSky.SetColor("_SkyTint", new Color(0.22f, 0.27f, 0.45f));
                nightSky.SetFloat("_AtmosphereThickness", 0.45f);
                nightSky.SetColor("_GroundColor", new Color(0.04f, 0.05f, 0.07f));
                nightSky.SetFloat("_SunSize", 0.025f);   // the moon
            }
            night = new Look
            {
                sky = new Color(0.07f, 0.08f, 0.13f), equator = new Color(0.05f, 0.055f, 0.07f), ground = new Color(0.03f, 0.03f, 0.035f),
                fog = new Color(0.025f, 0.03f, 0.045f), fogStart = 250f, fogEnd = 1500f, reflections = 0.2f, skybox = nightSky,
                sunColor = new Color(0.55f, 0.65f, 1f), sunIntensity = 0.22f, sunShadow = 0.5f, sunRotation = Quaternion.Euler(55f, 150f, 0f),
            };

            var a = GameBootstrap.Instance;
            lampDay = a ? a.FloodlightRuntime.GetColor("_EmissionColor") : Color.white;
        }

        static void Apply(Look look)
        {
            RenderSettings.ambientSkyColor = look.sky;
            RenderSettings.ambientEquatorColor = look.equator;
            RenderSettings.ambientGroundColor = look.ground;
            RenderSettings.fogColor = look.fog;
            RenderSettings.fogStartDistance = look.fogStart;
            RenderSettings.fogEndDistance = look.fogEnd;
            RenderSettings.reflectionIntensity = look.reflections;
            if (look.skybox) RenderSettings.skybox = look.skybox;
            if (sun)
            {
                sun.color = look.sunColor;
                sun.intensity = look.sunIntensity;
                sun.shadowStrength = look.sunShadow;
                sun.transform.rotation = look.sunRotation;
            }
        }
    }

    /// <summary>
    /// The floodlights' night-time half: a shadow-casting spotlight at each mast's lamp head,
    /// aimed at the middle of the arena, and a glow round the lamps for the bloom to catch.
    /// </summary>
    public class StadiumLights : MonoBehaviour
    {
        // Spotlights fall off with distance squared; this lights the middle of a 200 m-radius
        // arena about as brightly as the day's sun once all four masts add up, and scales up
        // for the bigger stadiums round the long courses.
        const float Intensity = 12000f, ReferenceDistance = 200f, Range = 700f, Angle = 95f, InnerAngle = 60f;

        readonly List<Light> spots = new List<Light>();
        readonly List<GameObject> glows = new List<GameObject>();

        public void AddMast(Vector3 lampHead, Vector3 aimAt, bool shadows)
        {
            var go = new GameObject("Floodlight");
            go.transform.SetParent(transform, false);
            go.transform.SetPositionAndRotation(lampHead, Quaternion.LookRotation(aimAt - lampHead));
            var spot = go.AddComponent<Light>();
            spot.type = LightType.Spot;
            spot.color = new Color(1f, 0.97f, 0.9f);
            float reach = Vector3.Distance(lampHead, aimAt) / ReferenceDistance;
            spot.intensity = Intensity * Mathf.Max(1f, reach * reach);
            spot.range = Range * Mathf.Max(1f, reach);
            spot.spotAngle = Angle;
            spot.innerSpotAngle = InnerAngle;
            spot.shadows = shadows ? LightShadows.Soft : LightShadows.None;
            spot.shadowStrength = 0.8f;
            spot.shadowNearPlane = 3f;
            spot.renderMode = LightRenderMode.ForcePixel;
            spots.Add(spot);

            var glow = GameObject.CreatePrimitive(PrimitiveType.Quad);
            glow.name = "Floodlight glow";
            Destroy(glow.GetComponent<Collider>());
            glow.transform.SetParent(transform, false);
            glow.transform.position = lampHead;
            glow.transform.localScale = Vector3.one * 22f;
            var r = glow.GetComponent<MeshRenderer>();
            r.sharedMaterial = GameBootstrap.Instance.HaloRuntime;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            glow.AddComponent<Billboard>();
            glows.Add(glow);

            Apply(TimeOfDay.Night);
        }

        void OnEnable()
        {
            TimeOfDay.Changed += Apply;
            Apply(TimeOfDay.Night);
        }

        void OnDisable() => TimeOfDay.Changed -= Apply;

        void Apply(bool night)
        {
            foreach (var s in spots) s.enabled = night;
            foreach (var g in glows) g.SetActive(night);
        }
    }

    /// <summary>Keeps a quad facing the camera.</summary>
    public class Billboard : MonoBehaviour
    {
        void LateUpdate()
        {
            var cam = Camera.main;
            if (cam) transform.rotation = cam.transform.rotation;
        }
    }

    /// <summary>A car's headlight beam, on at night.</summary>
    public class Headlights : MonoBehaviour
    {
        Light beam;

        void Start()
        {
            var go = new GameObject("Headlights");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0.8f, 2.2f);
            go.transform.localRotation = Quaternion.Euler(6f, 0f, 0f);
            beam = go.AddComponent<Light>();
            beam.type = LightType.Spot;
            beam.color = new Color(1f, 0.97f, 0.9f);
            beam.intensity = 260f;
            beam.range = 80f;
            beam.spotAngle = 70f;
            beam.innerSpotAngle = 35f;
            beam.shadows = LightShadows.None;
            TimeOfDay.Changed += Apply;
            Apply(TimeOfDay.Night);
        }

        void OnDestroy() => TimeOfDay.Changed -= Apply;

        void Apply(bool night)
        {
            if (beam) beam.enabled = night;
        }
    }
}

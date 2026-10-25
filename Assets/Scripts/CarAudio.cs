using UnityEngine;

namespace StuntCar
{
    /// <summary>
    /// Synthesised V8 loop pitched by speed through five fake gears, plus a crunch on damage.
    /// Clips are generated at start (WebGL can't run audio filters, but can play generated clips).
    /// </summary>
    public class CarAudio : MonoBehaviour
    {
        static AudioClip engineClip, crunchClip;
        static readonly float[] GearTop = { 13f, 22f, 32f, 44f, 58f, 999f };

        CarController car;
        AudioSource engine, fx;

        public void Init(CarController c, bool isPlayer)
        {
            car = c;
            if (engineClip == null) engineClip = MakeEngineClip();
            if (crunchClip == null) crunchClip = MakeCrunchClip();

            engine = gameObject.AddComponent<AudioSource>();
            engine.clip = engineClip;
            engine.loop = true;
            engine.spatialBlend = isPlayer ? 0f : 1f;
            engine.minDistance = 8f;
            engine.maxDistance = 200f;
            engine.volume = isPlayer ? 0.45f : 0.6f;
            engine.Play();

            fx = gameObject.AddComponent<AudioSource>();
            fx.spatialBlend = isPlayer ? 0f : 1f;
            fx.playOnAwake = false;

            car.Damaged += amount => fx.PlayOneShot(crunchClip, Mathf.Clamp01(0.3f + amount / 15f));
        }

        void Update()
        {
            if (!car) return;
            float speed = Mathf.Abs(car.Speed);
            int gear = 0;
            while (gear < GearTop.Length - 1 && speed > GearTop[gear]) gear++;
            float low = gear == 0 ? 0f : GearTop[gear - 1];
            float high = gear == GearTop.Length - 1 ? 80f : GearTop[gear];
            float rpm = Mathf.InverseLerp(low * 0.7f, high, speed);
            if (car.GroundedWheels == 0) rpm = Mathf.Min(1f, rpm + car.Throttle * 0.4f); // wheels spin up in the air

            float pitch = Mathf.Lerp(0.75f, 1.9f, rpm) + (car.IsBoosting ? 0.2f : 0f);
            engine.pitch = Mathf.Lerp(engine.pitch, pitch, Time.deltaTime * 8f);
            engine.volume = (engine.spatialBlend > 0f ? 0.6f : 0.35f) + car.Throttle * 0.15f;
        }

        static AudioClip MakeEngineClip()
        {
            // 1 s loop; every component is a whole number of Hz so it loops seamlessly.
            int rate = 44100;
            var data = new float[rate];
            var rng = new System.Random(3);
            for (int i = 0; i < rate; i++)
            {
                float t = i / (float)rate;
                float firing = 60f;
                float saw = 2f * ((t * firing) % 1f) - 1f;
                float s = saw * 0.35f
                          + Mathf.Sin(2f * Mathf.PI * firing * 2f * t) * 0.2f
                          + Mathf.Sin(2f * Mathf.PI * 30f * t) * 0.25f       // V8 burble
                          + Mathf.Sin(2f * Mathf.PI * 15f * t) * 0.1f;
                s *= 0.8f + 0.2f * Mathf.Sin(2f * Mathf.PI * 15f * t);
                s += ((float)rng.NextDouble() * 2f - 1f) * 0.04f;
                data[i] = s * 0.6f;
            }
            var clip = AudioClip.Create("Engine", rate, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static AudioClip MakeCrunchClip()
        {
            int rate = 44100, len = rate / 3;
            var data = new float[len];
            var rng = new System.Random(5);
            float lp = 0f;
            for (int i = 0; i < len; i++)
            {
                float env = Mathf.Exp(-i / (rate * 0.06f));
                float noise = (float)rng.NextDouble() * 2f - 1f;
                lp = Mathf.Lerp(lp, noise, 0.25f);
                data[i] = (lp * 0.8f + Mathf.Sin(i * 0.02f) * 0.3f) * env;
            }
            var clip = AudioClip.Create("Crunch", len, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}

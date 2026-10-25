using System.Collections;
using UnityEngine;

namespace StuntCar
{
    /// <summary>
    /// The crane. Lowers a hook, lifts the car off the ground (or the track), swings it
    /// back over the nearest piece of road and drops it on. The time this takes is the penalty.
    /// </summary>
    public class CarRecovery : MonoBehaviour
    {
        public bool InProgress { get; private set; }

        CarController car;
        Transform hook;
        LineRenderer cableLine;

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
            CreateCrane();

            Vector3 startPos = transform.position;
            Quaternion startRot = transform.rotation;
            Vector3 targetPos = track.Center(index) + track.Up(index) * 0.15f;
            Quaternion targetRot = track.Rotation(index);
            float liftHeight = Mathf.Max(startPos.y, targetPos.y) + 6f;
            Vector3 liftedStart = new Vector3(startPos.x, liftHeight, startPos.z);
            Vector3 liftedTarget = new Vector3(targetPos.x, liftHeight, targetPos.z);
            Quaternion levelRot = Quaternion.LookRotation(Vector3.ProjectOnPlane(startRot * Vector3.forward, Vector3.up).normalized + Vector3.forward * 0.001f);

            // Hook comes down.
            yield return Animate(0.7f, t =>
            {
                SetHook(Vector3.Lerp(startPos + Vector3.up * 30f, startPos + Vector3.up * 1.6f, t));
            });
            // Lift and level the car.
            yield return Animate(1.1f, t =>
            {
                transform.SetPositionAndRotation(Vector3.Lerp(startPos, liftedStart, t), Quaternion.Slerp(startRot, levelRot, t));
                SetHook(transform.position + Vector3.up * 1.6f);
            });
            // Swing over the road.
            yield return Animate(1.2f, t =>
            {
                transform.SetPositionAndRotation(Vector3.Lerp(liftedStart, liftedTarget, t), Quaternion.Slerp(levelRot, targetRot, t));
                SetHook(transform.position + Vector3.up * 1.6f);
            });
            // Lower onto the deck.
            yield return Animate(0.8f, t =>
            {
                transform.position = Vector3.Lerp(liftedTarget, targetPos, t);
                SetHook(transform.position + Vector3.up * 1.6f);
            });

            rb.isKinematic = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            // Hook goes back up.
            Vector3 release = transform.position + Vector3.up * 1.6f;
            yield return Animate(0.5f, t => SetHook(Vector3.Lerp(release, release + Vector3.up * 30f, t)));
            DestroyCrane();
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

        void CreateCrane()
        {
            var a = GameBootstrap.Instance;
            var hookGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(hookGo.GetComponent<Collider>());
            hookGo.name = "Crane Hook";
            hookGo.transform.localScale = new Vector3(0.5f, 0.35f, 0.5f);
            hookGo.GetComponent<MeshRenderer>().sharedMaterial = a.helmet;
            hook = hookGo.transform;

            cableLine = hookGo.AddComponent<LineRenderer>();
            cableLine.sharedMaterial = a.cable;
            cableLine.positionCount = 2;
            cableLine.startWidth = cableLine.endWidth = 0.07f;
            cableLine.useWorldSpace = true;
        }

        void SetHook(Vector3 p)
        {
            if (!hook) return;
            hook.position = p;
            cableLine.SetPosition(0, p + Vector3.up * 40f);
            cableLine.SetPosition(1, p);
        }

        void DestroyCrane()
        {
            if (hook) Destroy(hook.gameObject);
            hook = null;
        }

        void OnDisable()
        {
            DestroyCrane();
            InProgress = false;
        }
    }
}

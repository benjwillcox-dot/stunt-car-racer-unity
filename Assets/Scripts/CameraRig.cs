using UnityEngine;

namespace StuntCar
{
    /// <summary>Chase camera and the original's over-the-bonnet cockpit view. C switches.</summary>
    public class CameraRig : MonoBehaviour
    {
        public enum Mode { Chase, Cockpit }

        public Mode CurrentMode = Mode.Chase;
        public Transform Target;

        Camera cam;
        Vector3 velocity;
        Vector3 smoothedForward = Vector3.forward;
        float shake;

        void Awake()
        {
            cam = GetComponent<Camera>();
        }

        public void Snap()
        {
            if (!Target) return;
            smoothedForward = Flat(Target.forward);
            transform.position = ChasePosition();
            transform.rotation = Quaternion.LookRotation(Target.position + Vector3.up - transform.position);
            velocity = Vector3.zero;
        }

        public void Shake(float amount) => shake = Mathf.Min(1f, shake + amount);

        static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude < 0.001f ? Vector3.forward : v.normalized;
        }

        Vector3 ChasePosition() => Target.position - smoothedForward * 7.5f + Vector3.up * 2.7f;

        void LateUpdate()
        {
            if (GameInput.CameraPressed())
                CurrentMode = CurrentMode == Mode.Chase ? Mode.Cockpit : Mode.Chase;
            if (!Target) return;

            var car = Target.GetComponent<CarController>();
            float speed = car ? Mathf.Abs(car.Speed) : 0f;
            bool boosting = car && car.IsBoosting;

            if (CurrentMode == Mode.Chase)
            {
                smoothedForward = Vector3.Slerp(smoothedForward, Flat(Target.forward), Time.deltaTime * 3f);
                transform.position = Vector3.SmoothDamp(transform.position, ChasePosition(), ref velocity, 0.12f);
                Vector3 look = Target.position + Vector3.up * 1.1f + smoothedForward * 4f;
                transform.rotation = Quaternion.LookRotation(look - transform.position);
                cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, 60f + speed * 0.25f + (boosting ? 6f : 0f), Time.deltaTime * 3f);
            }
            else
            {
                transform.position = Target.TransformPoint(new Vector3(0f, 1.28f, -0.45f));
                transform.rotation = Target.rotation * Quaternion.Euler(7f, 0f, 0f);
                cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, 72f + speed * 0.15f + (boosting ? 5f : 0f), Time.deltaTime * 3f);
            }

            if (shake > 0f)
            {
                transform.position += Random.insideUnitSphere * shake * 0.25f;
                shake = Mathf.MoveTowards(shake, 0f, Time.deltaTime * 2f);
            }
        }
    }
}

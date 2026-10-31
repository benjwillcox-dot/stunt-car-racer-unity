using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace StuntCar
{
    /// <summary>
    /// All player input in one place. Keyboard always works (classic Input Manager).
    /// Gamepads and joystick-style steering wheels come through the Input System package
    /// when it is active; otherwise the classic joystick axes are used as a fallback.
    ///
    /// Gamepad: left stick steer, right trigger accelerate, left trigger brake/reverse,
    /// A boost, Y camera, B crane (back in menus), Start retire/menu, d-pad menus.
    /// Wheel: steering axis steers, trigger button boosts; pedals use the keyboard for now.
    /// Rumble goes back out through here too.
    /// </summary>
    public static class GameInput
    {
#if ENABLE_INPUT_SYSTEM
        static float lastStickY, lastStickX;
#else
        static bool legacyAxesMissing;
#endif
        static float rumbleLow, rumbleHigh;
#if UNITY_WEBGL && !UNITY_EDITOR
        // The Input System can't drive pad motors in a browser, so WebGL builds use the
        // browser's Gamepad API instead (Assets/Plugins/WebGL/Rumble.jslib).
        [System.Runtime.InteropServices.DllImport("__Internal")]
        static extern void StuntCarRumble(float strong, float weak, int milliseconds);
        static float rumbleResendAt, sentLow, sentHigh;
#endif

        /// <summary>
        /// Runs the pad's motors, 0..1: <paramref name="low"/> is the heavy thudding one,
        /// <paramref name="high"/> the light buzzing one. Call it every frame; zeros stop it.
        /// </summary>
        public static void SetRumble(float low, float high)
        {
            if (!Application.isFocused) low = high = 0f;
            low = Mathf.Clamp01(low);
            high = Mathf.Clamp01(high);
            bool wasOn = rumbleLow > 0f || rumbleHigh > 0f, on = low > 0f || high > 0f;
#if UNITY_WEBGL && !UNITY_EDITOR
            // Each call starts a new timed effect, so rather than every frame, resend a little
            // before the last one runs out, or straight away when it starts, stops or kicks up
            // (a clunk).
            bool kick = low > sentLow + 0.15f || high > sentHigh + 0.15f;
            if (on ? (!wasOn || kick || Time.unscaledTime >= rumbleResendAt) : wasOn)
            {
                StuntCarRumble(low, high, on ? 200 : 0);
                rumbleResendAt = Time.unscaledTime + 0.1f;
                sentLow = low;
                sentHigh = high;
            }
#elif ENABLE_INPUT_SYSTEM
            var pad = Gamepad.current;
            if (pad != null && (on || wasOn) && (low != rumbleLow || high != rumbleHigh)) pad.SetMotorSpeeds(low, high);
#endif
            rumbleLow = low;
            rumbleHigh = high;
        }

        public static void StopRumble()
        {
            SetRumble(0f, 0f);
#if ENABLE_INPUT_SYSTEM && !(UNITY_WEBGL && !UNITY_EDITOR)
            InputSystem.ResetHaptics();
#endif
        }

        public static void ReadDriving(out float steer, out float throttle, out float brake, out bool boost)
        {
            steer = 0f; throttle = 0f; brake = 0f;
            if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A)) steer -= 1f;
            if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D)) steer += 1f;
            if (Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W)) throttle = 1f;
            if (Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S)) brake = 1f;
            boost = Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.LeftShift);

#if ENABLE_INPUT_SYSTEM
            var pad = Gamepad.current;
            if (pad != null)
            {
                float sx = Deadzone(pad.leftStick.ReadValue().x);
                if (Mathf.Abs(sx) > Mathf.Abs(steer)) steer = sx;
                throttle = Mathf.Max(throttle, pad.rightTrigger.ReadValue());
                brake = Mathf.Max(brake, pad.leftTrigger.ReadValue());
                boost |= pad.buttonSouth.isPressed;
            }
            var wheel = Joystick.current;
            if (wheel != null && pad == null)
            {
                float wx = Deadzone(wheel.stick.ReadValue().x);
                if (Mathf.Abs(wx) > Mathf.Abs(steer)) steer = wx;
                boost |= wheel.trigger.isPressed;
            }
#else
            if (!legacyAxesMissing)
            {
                try
                {
                    float h = Input.GetAxis("Horizontal");
                    if (Mathf.Abs(h) > Mathf.Abs(steer)) steer = h;
                }
                catch (System.ArgumentException) { legacyAxesMissing = true; }
            }
            boost |= Input.GetKey(KeyCode.JoystickButton0);
#endif
        }

        public static bool CameraPressed()
        {
            bool k = Input.GetKeyDown(KeyCode.C);
#if ENABLE_INPUT_SYSTEM
            k |= Gamepad.current != null && Gamepad.current.buttonNorth.wasPressedThisFrame;
#endif
            return k;
        }

        public static bool CranePressed()
        {
            bool k = Input.GetKeyDown(KeyCode.R);
#if ENABLE_INPUT_SYSTEM
            k |= Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame;
#endif
            return k;
        }

        public static bool ConfirmPressed()
        {
            bool k = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
#if ENABLE_INPUT_SYSTEM
            var pad = Gamepad.current;
            k |= pad != null && (pad.buttonSouth.wasPressedThisFrame || pad.startButton.wasPressedThisFrame);
#endif
            return k;
        }

        /// <summary>Back in menus (Esc / B / Start).</summary>
        public static bool BackPressed()
        {
            bool k = Input.GetKeyDown(KeyCode.Escape);
#if ENABLE_INPUT_SYSTEM
            var pad = Gamepad.current;
            k |= pad != null && pad.buttonEast.wasPressedThisFrame;
#endif
            return k;
        }

        /// <summary>Leave/retire during a race (Esc / Start). B is the crane while driving.</summary>
        public static bool PausePressed()
        {
            bool k = Input.GetKeyDown(KeyCode.Escape);
#if ENABLE_INPUT_SYSTEM
            var pad = Gamepad.current;
            k |= pad != null && pad.startButton.wasPressedThisFrame;
#endif
            return k;
        }

        /// <summary>+1 for up, -1 for down, on the frame it is pressed.</summary>
        public static int MenuVertical()
        {
            int v = 0;
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) v = 1;
            if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) v = -1;
#if ENABLE_INPUT_SYSTEM
            var pad = Gamepad.current;
            if (pad != null)
            {
                if (pad.dpad.up.wasPressedThisFrame) v = 1;
                if (pad.dpad.down.wasPressedThisFrame) v = -1;
                float y = pad.leftStick.ReadValue().y;
                if (y > 0.6f && lastStickY <= 0.6f) v = 1;
                if (y < -0.6f && lastStickY >= -0.6f) v = -1;
                lastStickY = y;
            }
#endif
            return v;
        }

        /// <summary>+1 for right, -1 for left, on the frame it is pressed.</summary>
        public static int MenuHorizontal()
        {
            int h = 0;
            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) h = 1;
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) h = -1;
#if ENABLE_INPUT_SYSTEM
            var pad = Gamepad.current;
            if (pad != null)
            {
                if (pad.dpad.right.wasPressedThisFrame) h = 1;
                if (pad.dpad.left.wasPressedThisFrame) h = -1;
                float x = pad.leftStick.ReadValue().x;
                if (x > 0.6f && lastStickX <= 0.6f) h = 1;
                if (x < -0.6f && lastStickX >= -0.6f) h = -1;
                lastStickX = x;
            }
#endif
            return h;
        }

        static float Deadzone(float v) => Mathf.Abs(v) < 0.08f ? 0f : v;
    }
}

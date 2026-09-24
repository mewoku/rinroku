using UnityEngine;
using UnityEngine.InputSystem;

namespace Ronriku.Presentation.Accessibility
{
    /// <summary>Player motion preferences persisted in PlayerPrefs.</summary>
    public static class MotionSettings
    {
        private const string ReducedKey = "ronriku.reducedMotion";
        private const string GyroKey = "ronriku.gyro";

        public static bool ReducedMotion
        {
            get => PlayerPrefs.GetInt(ReducedKey, 0) == 1;
            set => PlayerPrefs.SetInt(ReducedKey, value ? 1 : 0);
        }

        public static bool GyroEnabled
        {
            get => PlayerPrefs.GetInt(GyroKey, 1) == 1;
            set => PlayerPrefs.SetInt(GyroKey, value ? 1 : 0);
        }
    }

    /// <summary>
    /// Device tilt for parallax, smoothed, in [-1, 1] per axis. Uses the gravity sensor when present;
    /// returns zero when there is no sensor, when disabled, or under reduced motion.
    /// </summary>
    public static class Tilt
    {
        private static Vector2 _smoothed;
        private static bool _initialised;
        private static bool _available;
        private static int _frame = -1;

        public static bool Available
        {
            get
            {
                Init();
                return _available;
            }
        }

        public static Vector2 Current
        {
            get
            {
                if (_frame == Time.frameCount) return _smoothed;
                _frame = Time.frameCount;
                Init();
                Vector2 target = Vector2.zero;
                if (_available && MotionSettings.GyroEnabled && !MotionSettings.ReducedMotion)
                {
                    Vector3 g = GravitySensor.current.gravity.ReadValue();
                    target = new Vector2(Mathf.Clamp(g.x * 1.6f, -1f, 1f), Mathf.Clamp((g.y + 0.6f) * 1.6f, -1f, 1f));
                }
                _smoothed = Vector2.Lerp(_smoothed, target, 0.12f);
                return _smoothed;
            }
        }

        private static void Init()
        {
            if (_initialised) return;
            _initialised = true;
            if (GravitySensor.current == null) return;
            InputSystem.EnableDevice(GravitySensor.current);
            _available = GravitySensor.current.enabled;
        }
    }
}

using UnityEngine;

namespace Ronriku.Presentation.Accessibility
{
    public interface IHapticsService
    {
        bool Enabled { get; set; }
        void Selection();
        void Success();
        void Error();
    }

    /// <summary>
    /// Short, amplitude-controlled ticks via Android VibrationEffect (API 26+).
    /// Falls back to Handheld.Vibrate, which also keeps the VIBRATE permission in the manifest.
    /// </summary>
    public sealed class PlatformHapticsService : IHapticsService
    {
        public bool Enabled { get; set; } = true;

        public void Selection() => Pulse(12, 70);
        public void Success() => Pulse(40, 200);
        public void Error() => Pulse(24, 140);

#if UNITY_ANDROID && !UNITY_EDITOR
        private AndroidJavaObject _vibrator;
        private AndroidJavaClass _effect;
        private bool _nativeFailed;

        private void Pulse(long milliseconds, int amplitude)
        {
            if (!Enabled) return;
            if (!_nativeFailed)
            {
                try
                {
                    if (_vibrator == null)
                    {
                        using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                        using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                        _vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                        _effect = new AndroidJavaClass("android.os.VibrationEffect");
                    }
                    using var effect = _effect.CallStatic<AndroidJavaObject>("createOneShot", milliseconds, amplitude);
                    _vibrator.Call("vibrate", effect);
                    return;
                }
                catch (System.Exception exception)
                {
                    _nativeFailed = true;
                    Debug.LogWarning($"RONRIKU haptics: native vibration unavailable, using fallback. {exception.Message}");
                }
            }
            Handheld.Vibrate();
        }
#else
        private void Pulse(long milliseconds, int amplitude) { }
#endif
    }
}

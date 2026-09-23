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

    public sealed class PlatformHapticsService : IHapticsService
    {
        public bool Enabled { get; set; } = true;

        public void Selection() => Vibrate();
        public void Success() => Vibrate();
        public void Error() => Vibrate();

        private void Vibrate()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (Enabled) Handheld.Vibrate();
#endif
        }
    }
}


using System;

namespace Ronriku.Composition
{
    /// <summary>
    /// Single place for runtime environment settings. Tests and the developer menu may set the overrides
    /// before the bootstrap scene loads.
    /// </summary>
    public static class RuntimeConfig
    {
        /// <summary>Allow connecting to the backend (tests turn this off to stay hermetic).</summary>
        public static bool OnlineEnabled { get; set; } = true;

        /// <summary>True while connected to the backend; results are then server-validated and ranked.</summary>
        public static bool Competitive { get; set; }

        public static string Environment => Competitive ? "online" : "local";

        /// <summary>Profile storage directory. Null means <c>Application.persistentDataPath</c>.</summary>
        public static string ProfileDirectory { get; set; }

        /// <summary>Fixed UTC time for development and tests. Null means the system clock.</summary>
        public static DateTime? UtcNowOverride { get; set; }

        public static DateTime UtcNow => UtcNowOverride ?? DateTime.UtcNow;

        public static void Reset()
        {
            OnlineEnabled = true;
            Competitive = false;
            ProfileDirectory = null;
            UtcNowOverride = null;
        }
    }
}

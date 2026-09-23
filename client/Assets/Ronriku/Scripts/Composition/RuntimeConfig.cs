using System;

namespace Ronriku.Composition
{
    /// <summary>
    /// Single place for runtime environment settings. Everything is local-only until the backend
    /// ships; UI shows local results as noncompetitive. Tests and the developer menu may set the
    /// overrides before the bootstrap scene loads.
    /// </summary>
    public static class RuntimeConfig
    {
        public const string Environment = "local";
        public const bool Competitive = false;

        /// <summary>Profile storage directory. Null means <c>Application.persistentDataPath</c>.</summary>
        public static string ProfileDirectory { get; set; }

        /// <summary>Fixed UTC time for development and tests. Null means the system clock.</summary>
        public static DateTime? UtcNowOverride { get; set; }

        public static DateTime UtcNow => UtcNowOverride ?? DateTime.UtcNow;

        public static void Reset()
        {
            ProfileDirectory = null;
            UtcNowOverride = null;
        }
    }
}

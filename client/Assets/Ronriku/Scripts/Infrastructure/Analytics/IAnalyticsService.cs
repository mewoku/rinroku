using System.Collections.Generic;
using UnityEngine;

namespace Ronriku.Infrastructure.Analytics
{
    public interface IAnalyticsService
    {
        void Track(string eventName, IReadOnlyDictionary<string, string> properties = null);
    }

    /// <summary>
    /// Local stand-in until the analytics backend exists. Logs only in the Editor and development
    /// builds; release builds drop events. Never pass solutions or personal data as properties.
    /// </summary>
    public sealed class LocalAnalyticsService : IAnalyticsService
    {
        private readonly bool _log = Debug.isDebugBuild;

        public void Track(string eventName, IReadOnlyDictionary<string, string> properties = null)
        {
            if (_log) Debug.Log($"[RONRIKU analytics/local] {eventName}");
        }
    }
}


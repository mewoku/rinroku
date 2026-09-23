using System.Collections.Generic;
using UnityEngine;

namespace Ronriku.Infrastructure.Analytics
{
    public interface IAnalyticsService
    {
        void Track(string eventName, IReadOnlyDictionary<string, string> properties = null);
    }

    public sealed class LocalAnalyticsService : IAnalyticsService
    {
        public void Track(string eventName, IReadOnlyDictionary<string, string> properties = null)
        {
            Debug.Log($"[RONRIKU analytics/local] {eventName}");
        }
    }
}


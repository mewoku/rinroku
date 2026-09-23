using System;
using System.IO;
using Ronriku.Domain.Player;
using UnityEngine;

namespace Ronriku.Infrastructure.Persistence
{
    public interface IProfileRepository
    {
        PlayerProfile Load();
        void Save(PlayerProfile profile);
    }

    /// <summary>
    /// Stores the local profile as JSON. Writes go to a temp file first and are then swapped in,
    /// so a crash mid-write never leaves a truncated profile. Unreadable files are kept aside
    /// as <c>profile.corrupt-*.json</c> and a fresh profile is created.
    /// </summary>
    public sealed class JsonFileProfileRepository : IProfileRepository
    {
        private readonly string _path;

        public JsonFileProfileRepository(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentException("Directory required.", nameof(directory));
            Directory.CreateDirectory(directory);
            _path = Path.Combine(directory, "profile.json");
        }

        public string FilePath => _path;

        public PlayerProfile Load()
        {
            if (File.Exists(_path))
            {
                try
                {
                    var profile = JsonUtility.FromJson<PlayerProfile>(File.ReadAllText(_path));
                    if (profile != null && profile.Migrate()) return profile;
                    Debug.LogWarning($"RONRIKU profile: unsupported or incomplete profile at {_path}; starting fresh.");
                }
                catch (Exception exception)
                {
                    Debug.LogWarning($"RONRIKU profile: could not read {_path}; starting fresh. {exception.Message}");
                }
                Quarantine();
            }

            var created = PlayerProfile.CreateNew(Guid.NewGuid().ToString("N"));
            Save(created);
            return created;
        }

        public void Save(PlayerProfile profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            string temp = _path + ".tmp";
            File.WriteAllText(temp, JsonUtility.ToJson(profile, true));
            if (File.Exists(_path)) File.Replace(temp, _path, null);
            else File.Move(temp, _path);
        }

        private void Quarantine()
        {
            try
            {
                string target = Path.Combine(Path.GetDirectoryName(_path) ?? string.Empty,
                    $"profile.corrupt-{DateTime.UtcNow:yyyyMMddHHmmss}.json");
                File.Move(_path, target);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"RONRIKU profile: could not move unreadable profile aside. {exception.Message}");
            }
        }
    }
}

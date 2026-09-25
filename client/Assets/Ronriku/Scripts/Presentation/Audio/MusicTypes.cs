using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Ronriku.EditModeTests")]
[assembly: InternalsVisibleTo("Ronriku.Editor")]

namespace Ronriku.Presentation.Audio
{
    /// <summary>Every piece of music in the game. <see cref="None"/> means silence.</summary>
    public enum MusicTrack { None, Menu, World1, World2, World3, World4, World5, Boss, Daily, HeroRun }

    /// <summary>Short one-shot phrases rendered in the key of the current track.</summary>
    public enum MusicStinger { Victory, Defeat, LevelUp, Combo }
}

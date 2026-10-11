using System;
using UnityEngine;

namespace TalonBridge.Gaze
{
    /// <summary>
    /// A source of gaze positions. Game code depends on this rather than on any
    /// particular transport, so a UDP bridge, a simulated mouse, recorded gaze
    /// or a different tracker are interchangeable.
    /// </summary>
    public interface IGazeProvider
    {
        /// <summary>True while gaze is valid and recent enough to act on.</summary>
        bool IsValid { get; }

        /// <summary>
        /// Latest gaze position, 0,0 at the top-left of the game window. Retains
        /// the last valid position while <see cref="IsValid"/> is false.
        /// </summary>
        Vector2 NormalizedPosition { get; }

        /// <summary>Latest gaze position in Unity screen pixels.</summary>
        Vector2 ScreenPosition { get; }

        /// <summary>Source timestamp of the most recent valid sample, in seconds.</summary>
        double LastTimestamp { get; }

        /// <summary>Most recent sample, valid or not.</summary>
        GazeSample LastSample { get; }

        /// <summary>
        /// Seconds since the last valid sample, or infinity if there has never
        /// been one.
        /// </summary>
        float TimeSinceValidSample { get; }

        /// <summary>Higher priority wins when several providers are active.</summary>
        int Priority { get; }

        /// <summary>Name shown in debug output.</summary>
        string ProviderName { get; }

        /// <summary>Raised on the main thread for every published sample.</summary>
        event Action<GazeSample> SampleReceived;
    }
}

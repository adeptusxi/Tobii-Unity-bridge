using UnityEngine;

namespace TalonBridge.Gaze.Udp
{
    /// <summary>
    /// One decoded gaze datagram from the Talon bridge. See the protocol table in README for the wire format.
    /// </summary>
    public struct GazePacket
    {
        public int Version;

        /// <summary>Talon's timestamp, seconds on its monotonic clock.</summary>
        public double SourceTimestamp;

        /// <summary>Unix time at which Talon sent the packet, for latency.</summary>
        public double WallTime;

        /// <summary>Whether Talon considered this a valid gaze sample.</summary>
        public bool Ok;

        public GazeEyes Eyes;

        /// <summary>Gaze normalized to the main screen, 0,0 at its top-left.</summary>
        public Vector2 ScreenNormalized;
        public bool HasScreenNormalized;

        /// <summary>Gaze normalized to the matched game window, 0,0 at its top-left.</summary>
        public Vector2 WindowNormalized;
        public bool HasWindowNormalized;

        /// <summary>Whether Talon placed the gaze inside the game window.</summary>
        public bool InsideWindow;

        /// <summary>Main screen rect in macOS logical points.</summary>
        public Rect ScreenRect;
        public bool HasScreenRect;

        /// <summary>Matched game window rect in macOS logical points.</summary>
        public Rect WindowRect;
        public bool HasWindowRect;

        /// <summary>Title of the window Talon matched, or an empty string.</summary>
        public string WindowTitle;
    }
}

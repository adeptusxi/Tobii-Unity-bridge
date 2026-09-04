using System;
using UnityEngine;

namespace TalonBridge.Gaze.Udp
{
    /// <summary>Which normalized coordinates from a packet to use.</summary>
    public enum GazeCoordinateSource
    {
        /// <summary>
        /// The Game view's rect in the editor, where the bridge only sees the
        /// whole editor window; window-relative in a player, or screen-relative
        /// when no window was matched.
        /// </summary>
        Auto = 0,

        /// <summary>Window-relative only; gaze is dropped when no window was matched.</summary>
        Window = 1,

        /// <summary>Screen-relative, for a game filling the main screen.</summary>
        Screen = 2,

        /// <summary>
        /// Screen-relative, remapped into the editor's Game view rect, which the
        /// bridge cannot see on its own. Behaves like <see cref="Auto"/> in a
        /// player, where there is no Game view.
        /// </summary>
        GameView = 3,
    }

    /// <summary>
    /// Turns the coordinates in a gaze packet into a position normalized to the
    /// Unity game surface, 0,0 at its top-left.
    /// </summary>
    [Serializable]
    public class GazeCoordinateMapper
    {
        /// <summary>Resolves a surface rect in the bridge's logical points.</summary>
        public delegate bool RectResolver(out Rect rect);

        /// <summary>
        /// Supplies the editor Game view's rect for
        /// <see cref="GazeCoordinateSource.GameView"/>. Installed by the editor
        /// assembly; null in a player.
        /// </summary>
        public static RectResolver GameViewRectResolver;

        [Tooltip("Which coordinates from the packet to use.")]
        [SerializeField]
        private GazeCoordinateSource source = GazeCoordinateSource.Auto;

        [Tooltip("Treat gaze that falls outside the surface as invalid.")]
        [SerializeField]
        private bool requireInsideSurface = true;

        public GazeCoordinateSource Source
        {
            get => source;
            set => source = value;
        }

        public bool RequireInsideSurface
        {
            get => requireInsideSurface;
            set => requireInsideSurface = value;
        }

        /// <summary>
        /// The surface rect the last mapping used, when the source works from a
        /// rect. Zero when no rect was involved or none could be resolved.
        /// </summary>
        public Rect ResolvedSurfaceRect { get; private set; }

        /// <summary>
        /// Maps a packet's gaze into surface-normalized coordinates. Returns
        /// false when the packet carries nothing usable for the chosen source.
        /// </summary>
        public bool TryMap(in GazePacket packet, out Vector2 normalized, out bool insideSurface)
        {
            normalized = Vector2.zero;
            insideSurface = false;

            switch (source)
            {
                case GazeCoordinateSource.Window:
                    if (!packet.HasWindowNormalized)
                    {
                        return false;
                    }

                    normalized = packet.WindowNormalized;
                    break;

                case GazeCoordinateSource.Screen:
                    if (!packet.HasScreenNormalized)
                    {
                        return false;
                    }

                    normalized = packet.ScreenNormalized;
                    break;

                case GazeCoordinateSource.GameView:
                default:
                    if (!TryMapToGameViewOrAutomatically(packet, out normalized))
                    {
                        return false;
                    }

                    break;
            }

            insideSurface = GazeCoordinates.IsInsideWindow(normalized);
            return true;
        }

        /// <summary>
        /// Uses the editor's Game view rect when one can be resolved, and
        /// otherwise the packet's own window- or screen-relative coordinates.
        /// In a player there is no Game view, so this is always the latter.
        /// </summary>
        private bool TryMapToGameViewOrAutomatically(in GazePacket packet, out Vector2 normalized)
        {
            if (Application.isEditor && TryResolveGameViewRect(out Rect gameView))
            {
                ResolvedSurfaceRect = gameView;
                return TryMapToRect(packet, gameView, out normalized);
            }

            ResolvedSurfaceRect = default;
            return TryMapAutomatically(packet, out normalized);
        }

        private static bool TryMapAutomatically(in GazePacket packet, out Vector2 normalized)
        {
            if (packet.HasWindowNormalized)
            {
                normalized = packet.WindowNormalized;
                return true;
            }

            if (packet.HasScreenNormalized)
            {
                normalized = packet.ScreenNormalized;
                return true;
            }

            normalized = Vector2.zero;
            return false;
        }

        private bool TryResolveGameViewRect(out Rect rect)
        {
            rect = default;
            RectResolver resolver = GameViewRectResolver;
            if (resolver == null || !resolver(out rect))
            {
                return false;
            }

            return rect.width > 0f && rect.height > 0f;
        }

        /// <summary>
        /// Converts screen-normalized gaze to a position inside an arbitrary
        /// rect, using the screen rect the bridge reported.
        /// </summary>
        private static bool TryMapToRect(in GazePacket packet, Rect rect, out Vector2 normalized)
        {
            normalized = Vector2.zero;
            if (!packet.HasScreenNormalized || !packet.HasScreenRect)
            {
                return false;
            }

            if (rect.width <= 0f || rect.height <= 0f)
            {
                return false;
            }

            float pointX = packet.ScreenRect.x + packet.ScreenNormalized.x * packet.ScreenRect.width;
            float pointY = packet.ScreenRect.y + packet.ScreenNormalized.y * packet.ScreenRect.height;
            normalized = new Vector2(
                (pointX - rect.x) / rect.width,
                (pointY - rect.y) / rect.height);
            return true;
        }
    }
}

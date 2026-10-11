using UnityEngine;

namespace TalonBridge.Gaze
{
    /// <summary>
    /// Conversions between normalized gaze coordinates and Unity screen pixels.
    ///
    /// Normalized gaze has its origin at the top-left with y increasing
    /// downward; Unity screen space has its origin at the bottom-left with y
    /// increasing upward, so y is flipped in both directions.
    ///
    /// Because the coordinates are normalized against the game window rather
    /// than the display, the conversion is independent of macOS display
    /// scaling: only Unity's own pixel dimensions are involved.
    /// </summary>
    public static class GazeCoordinates
    {
        public static Vector2 NormalizedToScreen(Vector2 normalized)
        {
            return new Vector2(
                normalized.x * Screen.width,
                (1f - normalized.y) * Screen.height);
        }

        public static Vector2 ScreenToNormalized(Vector2 screen)
        {
            float width = Mathf.Max(1, Screen.width);
            float height = Mathf.Max(1, Screen.height);
            return new Vector2(screen.x / width, 1f - (screen.y / height));
        }

        /// <summary>True when a normalized position lies within the window.</summary>
        public static bool IsInsideWindow(Vector2 normalized)
        {
            return normalized.x >= 0f && normalized.x <= 1f
                && normalized.y >= 0f && normalized.y <= 1f;
        }
    }
}

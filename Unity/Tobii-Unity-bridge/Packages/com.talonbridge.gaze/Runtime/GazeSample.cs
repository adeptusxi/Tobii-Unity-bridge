using System;
using UnityEngine;

namespace TalonBridge.Gaze
{
    /// <summary>Which eyes contributed to a gaze sample.</summary>
    [Flags]
    public enum GazeEyes
    {
        None = 0,
        Left = 1,
        Right = 2,
        Both = Left | Right,
    }

    /// <summary>
    /// One gaze reading. Normalized coordinates run from 0 at the left/top edge
    /// to 1 at the right/bottom edge, matching the source convention; Unity
    /// screen coordinates are derived from them by <see cref="GazeCoordinates"/>.
    /// </summary>
    public readonly struct GazeSample
    {
        /// <summary>Gaze position, 0,0 at the top-left of the game window.</summary>
        public readonly Vector2 Normalized;

        /// <summary>False when the source reported no usable gaze.</summary>
        public readonly bool IsValid;

        public readonly GazeEyes Eyes;

        /// <summary>False when the gaze fell outside the game window.</summary>
        public readonly bool InsideWindow;

        /// <summary>Timestamp on the source's clock, in seconds.</summary>
        public readonly double SourceTimestamp;

        /// <summary><see cref="Time.realtimeSinceStartup"/> when this sample was published.</summary>
        public readonly float ReceivedTime;

        public GazeSample(
            Vector2 normalized,
            bool isValid,
            GazeEyes eyes,
            bool insideWindow,
            double sourceTimestamp,
            float receivedTime)
        {
            Normalized = normalized;
            IsValid = isValid;
            Eyes = eyes;
            InsideWindow = insideWindow;
            SourceTimestamp = sourceTimestamp;
            ReceivedTime = receivedTime;
        }

        /// <summary>Position in Unity screen pixels. Main thread only.</summary>
        public Vector2 ScreenPosition => GazeCoordinates.NormalizedToScreen(Normalized);

        /// <summary>Copy of this sample at a different position.</summary>
        public GazeSample WithNormalized(Vector2 normalized)
        {
            return new GazeSample(
                normalized, IsValid, Eyes, InsideWindow, SourceTimestamp, ReceivedTime);
        }

        public static GazeSample Invalid(double sourceTimestamp, float receivedTime)
        {
            return new GazeSample(
                Vector2.zero, false, GazeEyes.None, false, sourceTimestamp, receivedTime);
        }

        public override string ToString()
        {
            return IsValid
                ? $"gaze ({Normalized.x:F3}, {Normalized.y:F3}) eyes={Eyes}"
                : "gaze invalid";
        }
    }
}

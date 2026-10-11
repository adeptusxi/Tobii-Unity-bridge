using UnityEngine;

namespace TalonBridge.Gaze
{
    /// <summary>
    /// The recent path of the gaze point: a fixed-size history of normalized
    /// positions, oldest first. Adding to a full trail drops the oldest point.
    ///
    /// A trail is a series of strokes. <see cref="Break"/> ends the current
    /// one, so a gap in tracking is not drawn as a line.
    /// </summary>
    public sealed class GazeTrail
    {
        public struct Point
        {
            /// <summary>Normalized position, 0,0 at the top-left.</summary>
            public Vector2 Position;

            /// <summary>When the point was added, on the caller's clock.</summary>
            public float Time;

            /// <summary>True when this point is not joined to the one before.</summary>
            public bool StartsStroke;
        }

        private Point[] buffer;
        private int start;
        private int count;
        private bool breakPending = true;

        public GazeTrail(int maxSegments)
        {
            buffer = new Point[Mathf.Max(1, maxSegments) + 1];
        }

        /// <summary>Number of points held.</summary>
        public int Count => count;

        /// <summary>True when the next point added will start a new stroke.</summary>
        public bool BreakPending => breakPending || count == 0;

        /// <summary>Point by age: 0 is the oldest.</summary>
        public Point this[int index] => buffer[(start + index) % buffer.Length];

        /// <summary>
        /// Most line segments the trail can hold. Changing it keeps the newest
        /// points.
        /// </summary>
        public int MaxSegments
        {
            get => buffer.Length - 1;
            set
            {
                int capacity = Mathf.Max(1, value) + 1;
                if (capacity == buffer.Length)
                {
                    return;
                }

                var resized = new Point[capacity];
                int kept = Mathf.Min(count, capacity);
                int skipped = count - kept;
                for (int i = 0; i < kept; i++)
                {
                    resized[i] = this[skipped + i];
                }

                buffer = resized;
                start = 0;
                count = kept;
            }
        }

        public void Add(Vector2 position, float time)
        {
            if (count == buffer.Length)
            {
                start = (start + 1) % buffer.Length;
                count--;
            }

            buffer[(start + count) % buffer.Length] = new Point
            {
                Position = position,
                Time = time,
                StartsStroke = BreakPending,
            };

            count++;
            breakPending = false;
        }

        /// <summary>Ends the current stroke. The next point starts a new one.</summary>
        public void Break()
        {
            breakPending = true;
        }

        /// <summary>
        /// Drops points older than <paramref name="maxAge"/> seconds. 0 or less
        /// keeps everything.
        /// </summary>
        public void Trim(float now, float maxAge)
        {
            if (maxAge <= 0f)
            {
                return;
            }

            while (count > 0 && now - buffer[start].Time > maxAge)
            {
                start = (start + 1) % buffer.Length;
                count--;
            }
        }

        public bool TryGetNewest(out Point point)
        {
            if (count == 0)
            {
                point = default;
                return false;
            }

            point = this[count - 1];
            return true;
        }

        public void Clear()
        {
            start = 0;
            count = 0;
            breakPending = true;
        }
    }
}

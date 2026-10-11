using UnityEngine;

namespace TalonBridge.Gaze
{
    /// <summary>
    /// Averages the last N gaze positions.
    ///
    /// Simpler than <see cref="OneEuroGazeFilter"/> and adds lag proportional to
    /// the window, so it suits a steady pointer more than fast target changes.
    /// </summary>
    [AddComponentMenu("Talon Gaze Bridge/Filtering/Moving Average Gaze Filter")]
    public sealed class MovingAverageGazeFilter : MonoBehaviour, IGazeFilter
    {
        [Tooltip("Number of samples averaged. Larger is smoother and laggier.")]
        [SerializeField]
        [Range(2, 32)]
        private int windowSize = 5;

        private Vector2[] buffer;
        private Vector2 sum;
        private int count;
        private int next;

        public int WindowSize
        {
            get => windowSize;
            set
            {
                windowSize = Mathf.Clamp(value, 2, 32);
                Reset();
            }
        }

        public Vector2 Filter(Vector2 normalized, float deltaTime)
        {
            EnsureBuffer();

            if (count == buffer.Length)
            {
                sum -= buffer[next];
            }
            else
            {
                count++;
            }

            buffer[next] = normalized;
            sum += normalized;
            next = (next + 1) % buffer.Length;

            return sum / count;
        }

        public void Reset()
        {
            sum = Vector2.zero;
            count = 0;
            next = 0;
        }

        private void EnsureBuffer()
        {
            if (buffer != null && buffer.Length == windowSize)
            {
                return;
            }

            buffer = new Vector2[Mathf.Max(2, windowSize)];
            Reset();
        }
    }
}

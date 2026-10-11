using UnityEngine;

namespace TalonBridge.Gaze
{
    /// <summary>
    /// Simulates gaze with the mouse pointer, so gaze-driven systems can be
    /// built and tested without Talon or a tracker running.
    /// </summary>
    [AddComponentMenu("Talon Gaze Bridge/Mouse Gaze Provider")]
    public sealed class MouseGazeProvider : GazeProviderBase
    {
        [Tooltip("Report gaze as invalid while the pointer is outside the game window.")]
        [SerializeField]
        private bool invalidOutsideWindow = true;

        [Tooltip("Adds drift to the simulated gaze, in pixels, to approximate " +
                 "tracker noise. 0 disables it.")]
        [SerializeField]
        [Range(0f, 50f)]
        private float jitterPixels = 0f;

        [Tooltip("Drift speed in cycles per second.")]
        [SerializeField]
        [Min(0.01f)]
        private float jitterFrequency = 1.5f;

        [Tooltip("Reports invalid gaze, as if tracking had been lost.")]
        [SerializeField]
        private bool simulateTrackingLoss;

        private float jitterSeedX;
        private float jitterSeedY;

        public override string ProviderName => $"Mouse ({PointerInput.SourceName})";

        /// <summary>Toggles simulated tracking loss at runtime.</summary>
        public bool SimulateTrackingLoss
        {
            get => simulateTrackingLoss;
            set => simulateTrackingLoss = value;
        }

        private void Awake()
        {
            jitterSeedX = Random.value * 100f;
            jitterSeedY = Random.value * 100f;
        }

        protected override void Poll()
        {
            double timestamp = Time.realtimeSinceStartupAsDouble;
            float now = Time.realtimeSinceStartup;

            if (simulateTrackingLoss || !PointerInput.TryGetPosition(out Vector2 pointer))
            {
                PublishSample(GazeSample.Invalid(timestamp, now));
                return;
            }

            if (jitterPixels > 0f)
            {
                pointer += Jitter(now);
            }

            Vector2 normalized = GazeCoordinates.ScreenToNormalized(pointer);
            bool inside = GazeCoordinates.IsInsideWindow(normalized);
            bool valid = inside || !invalidOutsideWindow;

            PublishSample(new GazeSample(
                normalized, valid, GazeEyes.Both, inside, timestamp, now));
        }

        private Vector2 Jitter(float time)
        {
            float t = time * jitterFrequency;
            return new Vector2(
                (Mathf.PerlinNoise(jitterSeedX, t) - 0.5f) * 2f * jitterPixels,
                (Mathf.PerlinNoise(jitterSeedY, t) - 0.5f) * 2f * jitterPixels);
        }
    }
}

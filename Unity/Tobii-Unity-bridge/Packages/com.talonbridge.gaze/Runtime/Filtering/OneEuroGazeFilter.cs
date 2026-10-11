using UnityEngine;

namespace TalonBridge.Gaze
{
    /// <summary>
    /// One Euro filter, the usual choice for gaze: it smooths a resting gaze
    /// heavily while adding little lag to fast movements.
    ///
    /// Lower <c>Min Cutoff</c> to steady a resting gaze, raise <c>Beta</c> to
    /// reduce lag on fast movement. Speed is measured in normalized units per
    /// second, so the settings do not depend on resolution.
    /// </summary>
    [AddComponentMenu("Talon Gaze Bridge/Filtering/One Euro Gaze Filter")]
    public sealed class OneEuroGazeFilter : MonoBehaviour, IGazeFilter
    {
        [Tooltip("Smoothing applied to a resting gaze. Lower is smoother and " +
                 "laggier.")]
        [SerializeField]
        [Min(0.0001f)]
        private float minCutoff = 1f;

        [Tooltip("How much fast movement loosens the smoothing. Higher follows " +
                 "quick eye movements with less lag; 0 smooths all movement " +
                 "equally.")]
        [SerializeField]
        [Min(0f)]
        private float beta = 2f;

        [Tooltip("Smoothing applied to the speed estimate.")]
        [SerializeField]
        [Min(0.0001f)]
        private float derivativeCutoff = 1f;

        private Vector2 previous;
        private Vector2 previousDerivative;
        private bool hasPrevious;

        public float MinCutoff
        {
            get => minCutoff;
            set => minCutoff = Mathf.Max(0.0001f, value);
        }

        public float Beta
        {
            get => beta;
            set => beta = Mathf.Max(0f, value);
        }

        public float DerivativeCutoff
        {
            get => derivativeCutoff;
            set => derivativeCutoff = Mathf.Max(0.0001f, value);
        }

        public Vector2 Filter(Vector2 normalized, float deltaTime)
        {
            if (!hasPrevious || deltaTime <= 0f)
            {
                hasPrevious = true;
                previous = normalized;
                previousDerivative = Vector2.zero;
                return normalized;
            }

            float rate = 1f / deltaTime;

            Vector2 derivative = (normalized - previous) * rate;
            previousDerivative = Vector2.Lerp(
                previousDerivative, derivative, Alpha(rate, derivativeCutoff));

            float cutoff = minCutoff + (beta * previousDerivative.magnitude);
            previous = Vector2.Lerp(previous, normalized, Alpha(rate, cutoff));
            return previous;
        }

        public void Reset()
        {
            hasPrevious = false;
            previous = Vector2.zero;
            previousDerivative = Vector2.zero;
        }

        private static float Alpha(float rate, float cutoff)
        {
            float tau = 1f / (2f * Mathf.PI * cutoff);
            return 1f / (1f + (tau * rate));
        }
    }
}

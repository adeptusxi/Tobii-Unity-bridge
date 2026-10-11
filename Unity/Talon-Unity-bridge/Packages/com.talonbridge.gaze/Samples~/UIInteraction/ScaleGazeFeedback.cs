using UnityEngine;

namespace TalonBridge.Gaze.Samples
{
    /// <summary>
    /// Scales the target up as dwell progresses and pops it on activation.
    /// </summary>
    [AddComponentMenu("Talon Gaze Bridge/Feedback/Scale Gaze Feedback")]
    public sealed class ScaleGazeFeedback : MonoBehaviour, IGazeFeedback
    {
        [Tooltip("Transform to scale. Leave empty to scale this object.")]
        [SerializeField]
        private Transform target = null;

        [Tooltip("Scale multiplier reached at full dwell progress.")]
        [SerializeField]
        [Min(0.01f)]
        private float dwellScale = 1.1f;

        [Tooltip("Scale multiplier at the moment of activation.")]
        [SerializeField]
        [Min(0.01f)]
        private float activatedScale = 1.25f;

        [Tooltip("Seconds for the activation pop to settle back.")]
        [SerializeField]
        [Min(0f)]
        private float popDuration = 0.15f;

        private Transform Target => target != null ? target : transform;

        private Vector3 baseScale = Vector3.one;
        private bool hasBaseScale;
        private float popUntil;
        private float progress;

        private void OnEnable()
        {
            CacheBaseScale();
            progress = 0f;
            popUntil = 0f;
            Apply();
        }

        private void OnDisable()
        {
            if (hasBaseScale)
            {
                Target.localScale = baseScale;
            }
        }

        private void Update()
        {
            if (popUntil > 0f && Time.unscaledTime >= popUntil)
            {
                popUntil = 0f;
                Apply();
            }
        }

        public void OnGazeEnter(GazeInteractable interactable)
        {
            CacheBaseScale();
        }

        public void OnDwellProgress(GazeInteractable interactable, float value)
        {
            progress = Mathf.Clamp01(value);
            if (popUntil <= 0f)
            {
                Apply();
            }
        }

        public void OnGazeExit(GazeInteractable interactable)
        {
            progress = 0f;
            if (popUntil <= 0f)
            {
                Apply();
            }
        }

        public void OnActivated(GazeInteractable interactable)
        {
            CacheBaseScale();
            popUntil = Time.unscaledTime + popDuration;
            Target.localScale = baseScale * activatedScale;
        }

        private void Apply()
        {
            if (!hasBaseScale)
            {
                return;
            }

            Target.localScale = baseScale * Mathf.LerpUnclamped(1f, dwellScale, progress);
        }

        private void CacheBaseScale()
        {
            if (!hasBaseScale)
            {
                baseScale = Target.localScale;
                hasBaseScale = true;
            }
        }
    }
}

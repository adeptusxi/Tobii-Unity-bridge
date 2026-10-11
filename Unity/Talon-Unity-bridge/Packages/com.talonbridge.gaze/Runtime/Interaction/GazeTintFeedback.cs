using UnityEngine;
using UnityEngine.UI;

namespace TalonBridge.Gaze
{
    /// <summary>
    /// Tints a <see cref="Graphic"/> from its normal color towards a dwell color as gaze progress builds, then flashes on activation.
    /// </summary>
    [AddComponentMenu("Talon Gaze Bridge/Gaze Tint Feedback")]
    public sealed class GazeTintFeedback : MonoBehaviour, IGazeFeedback
    {
        [Tooltip("Graphic to tint. Leave empty to take one from this GameObject.")]
        [SerializeField]
        private Graphic target = null;

        [Tooltip("Colour reached at full dwell progress.")]
        [SerializeField]
        private Color dwellColor = new Color(0.2f, 1f, 0.4f, 1f);

        [Tooltip("Colour shown briefly on activation.")]
        [SerializeField]
        private Color activatedColor = Color.white;

        [Tooltip("Seconds the activation colour is held.")]
        [SerializeField]
        [Min(0f)]
        private float flashDuration = 0.15f;

        private Color normalColor;
        private bool hasNormalColor;
        private float flashUntil;

        private Graphic Target
        {
            get
            {
                if (target == null)
                {
                    target = GetComponent<Graphic>();
                }

                return target;
            }
        }

        private void OnEnable()
        {
            CacheNormalColor();
        }

        private void OnDisable()
        {
            Restore();
        }

        private void Update()
        {
            if (flashUntil > 0f && Time.unscaledTime >= flashUntil)
            {
                flashUntil = 0f;
                Restore();
            }
        }

        public void OnGazeEnter(GazeInteractable interactable)
        {
            CacheNormalColor();
        }

        public void OnDwellProgress(GazeInteractable interactable, float progress)
        {
            if (Target == null || flashUntil > 0f)
            {
                return;
            }

            CacheNormalColor();
            Target.color = Color.Lerp(normalColor, dwellColor, Mathf.Clamp01(progress));
        }

        public void OnGazeExit(GazeInteractable interactable)
        {
            if (flashUntil > 0f)
            {
                return;
            }

            Restore();
        }

        public void OnActivated(GazeInteractable interactable)
        {
            if (Target == null)
            {
                return;
            }

            CacheNormalColor();
            Target.color = activatedColor;
            flashUntil = Time.unscaledTime + flashDuration;
        }

        private void CacheNormalColor()
        {
            if (hasNormalColor || Target == null)
            {
                return;
            }

            normalColor = Target.color;
            hasNormalColor = true;
        }

        private void Restore()
        {
            if (hasNormalColor && Target != null)
            {
                Target.color = normalColor;
            }
        }
    }
}

using UnityEngine;
using UnityEngine.UI;

namespace TalonBridge.Gaze.Samples
{
    /// <summary>
    /// Shows an outline graphic while the target is gazed at, fading it in with dwell progress.
    /// </summary>
    [AddComponentMenu("Talon Gaze Bridge/Feedback/Outline Gaze Feedback")]
    public sealed class OutlineGazeFeedback : MonoBehaviour, IGazeFeedback
    {
        [Tooltip("Graphic shown while the target is gazed at.")]
        [SerializeField]
        private Graphic outline = null;

        [SerializeField]
        private Color idleColor = new Color(0.2f, 1f, 0.4f, 0.25f);

        [SerializeField]
        private Color dwellColor = new Color(0.2f, 1f, 0.4f, 1f);

        [SerializeField]
        private Color activatedColor = Color.white;

        [Tooltip("Seconds the activation colour is held.")]
        [SerializeField]
        [Min(0f)]
        private float flashDuration = 0.15f;

        private float flashUntil;

        private void Reset()
        {
            outline = GetComponentInChildren<Graphic>();
        }

        private void OnEnable()
        {
            Hide();
        }

        private void Update()
        {
            if (flashUntil > 0f && Time.unscaledTime >= flashUntil)
            {
                flashUntil = 0f;
                Hide();
            }
        }

        public void OnGazeEnter(GazeInteractable interactable)
        {
            if (outline == null)
            {
                return;
            }

            outline.enabled = true;
            outline.color = idleColor;
        }

        public void OnDwellProgress(GazeInteractable interactable, float progress)
        {
            if (outline == null || flashUntil > 0f)
            {
                return;
            }

            outline.enabled = true;
            outline.color = Color.Lerp(idleColor, dwellColor, Mathf.Clamp01(progress));
        }

        public void OnGazeExit(GazeInteractable interactable)
        {
            if (flashUntil <= 0f)
            {
                Hide();
            }
        }

        public void OnActivated(GazeInteractable interactable)
        {
            if (outline == null)
            {
                return;
            }

            outline.enabled = true;
            outline.color = activatedColor;
            flashUntil = Time.unscaledTime + flashDuration;
        }

        private void Hide()
        {
            if (outline != null)
            {
                outline.enabled = false;
            }
        }
    }
}

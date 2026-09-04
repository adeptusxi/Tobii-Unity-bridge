using UnityEngine;
using UnityEngine.UI;

namespace TalonBridge.Gaze.Samples
{
    /// <summary>
    /// Fills a radial Image as dwell progresses. 
    /// Assign an Image with Image Type "Filled" and Fill Method "Radial 360".
    /// </summary>
    [AddComponentMenu("Talon Gaze Bridge/Feedback/Radial Fill Gaze Feedback")]
    public sealed class RadialFillGazeFeedback : MonoBehaviour, IGazeFeedback
    {
        [Tooltip("Filled Image showing dwell progress.")]
        [SerializeField]
        private Image fill = null;

        [Tooltip("Hide the fill entirely while the target is not gazed at.")]
        [SerializeField]
        private bool hideWhenIdle = true;

        private void Reset()
        {
            fill = GetComponentInChildren<Image>();
        }

        private void OnEnable()
        {
            SetProgress(0f);
        }

        public void OnGazeEnter(GazeInteractable interactable)
        {
            if (fill != null && hideWhenIdle)
            {
                fill.enabled = true;
            }

            SetProgress(0f);
        }

        public void OnDwellProgress(GazeInteractable interactable, float progress)
        {
            SetProgress(progress);
        }

        public void OnGazeExit(GazeInteractable interactable)
        {
            SetProgress(0f);

            if (fill != null && hideWhenIdle)
            {
                fill.enabled = false;
            }
        }

        public void OnActivated(GazeInteractable interactable)
        {
            SetProgress(1f);
        }

        private void SetProgress(float progress)
        {
            if (fill != null)
            {
                fill.fillAmount = Mathf.Clamp01(progress);
            }
        }
    }
}

using TMPro;
using UnityEngine;

namespace TalonBridge.Gaze.Demo
{
    /// <summary>
    /// Reports which gaze target fired and how many times it has fired.
    ///
    /// Wire a GazeInteractable's On Activated to <see cref="Report"/>.
    /// </summary>
    public sealed class GazeDemoReadout : MonoBehaviour
    {
        [SerializeField]
        private TMP_Text label = null;

        private int total;

        private void Reset()
        {
            label = GetComponent<TMP_Text>();
        }

        private void Start()
        {
            if (label == null)
            {
                label = GetComponent<TMP_Text>();
            }

            Show("No buttons activated yet");
        }

        /// <summary>Called from a GazeInteractable's On Activated event.</summary>
        public void Report(string target)
        {
            total++;
            Show($"{target}  (activation {total})");
        }

        private void Show(string text)
        {
            if (label != null)
            {
                label.text = text;
            }
        }
    }
}

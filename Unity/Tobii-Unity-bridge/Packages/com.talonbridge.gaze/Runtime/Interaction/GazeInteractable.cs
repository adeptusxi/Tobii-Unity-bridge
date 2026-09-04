using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace TalonBridge.Gaze
{
    /// <summary>Dwell progress reported by <see cref="GazeInteractable.onDwellProgress"/>.</summary>
    [System.Serializable]
    public sealed class GazeProgressEvent : UnityEvent<float>
    {
    }

    /// <summary>
    /// Opts a UI element into gaze interaction by making it visible to <see cref="GazePointer"/>.
    /// </summary>
    [AddComponentMenu("Talon Gaze Bridge/Gaze Interactable")]
    public sealed class GazeInteractable : MonoBehaviour
    {
        [Tooltip("Disables gaze interaction without disabling the GameObject.")]
        [SerializeField]
        private bool interactable = true;

        [Tooltip("Dwell duration for this element in seconds. Negative uses the " +
                 "pointer's duration.")]
        [SerializeField]
        private float dwellDurationOverride = -1f;

        [Tooltip("Cooldown for this element in seconds. Negative uses the " +
                 "pointer's cooldown.")]
        [SerializeField]
        private float cooldownOverride = -1f;

        [Tooltip("Keep re-activating while gaze stays on the element, once the " +
                 "cooldown has elapsed. Off means one activation per visit.")]
        [SerializeField]
        private bool repeatWhileHeld = false;

        [Space]
        [SerializeField]
        private UnityEvent onGazeEnter = new UnityEvent();

        [SerializeField]
        private UnityEvent onGazeExit = new UnityEvent();

        [Tooltip("Dwell progress, 0 to 1. Raised every frame while gazed at.")]
        [SerializeField]
        private GazeProgressEvent onDwellProgress = new GazeProgressEvent();

        [SerializeField]
        private UnityEvent onActivated = new UnityEvent();

        private readonly List<IGazeFeedback> feedback = new List<IGazeFeedback>();
        private RectTransform cachedRect;

        public UnityEvent GazeEntered => onGazeEnter;

        public UnityEvent GazeExited => onGazeExit;

        public GazeProgressEvent DwellProgressed => onDwellProgress;

        public UnityEvent Activated => onActivated;

        public bool Interactable
        {
            get => interactable;
            set => interactable = value;
        }

        public bool RepeatWhileHeld
        {
            get => repeatWhileHeld;
            set => repeatWhileHeld = value;
        }

        /// <summary>Negative when the pointer's dwell duration should be used.</summary>
        public float DwellDurationOverride
        {
            get => dwellDurationOverride;
            set => dwellDurationOverride = value;
        }

        /// <summary>Negative when the pointer's cooldown should be used.</summary>
        public float CooldownOverride
        {
            get => cooldownOverride;
            set => cooldownOverride = value;
        }

        /// <summary>True while a pointer counts this element as a target.</summary>
        public bool IsGazedAt { get; private set; }

        /// <summary>Dwell progress from 0 to 1, or 0 when not gazed at.</summary>
        public float DwellProgress { get; private set; }

        /// <summary>The element's RectTransform, or null on a non-UI object.</summary>
        public RectTransform RectTransform
        {
            get
            {
                if (cachedRect == null)
                {
                    cachedRect = transform as RectTransform;
                }

                return cachedRect;
            }
        }

        /// <summary>True when this element can currently be gazed at.</summary>
        public bool CanInteract => interactable && isActiveAndEnabled;

        private void OnEnable()
        {
            RefreshFeedback();
        }

        private void OnDisable()
        {
            // A pointer can't notify an element it can no longer see, so any in-progress dwell is cleared here instead.
            IsGazedAt = false;
            DwellProgress = 0f;
        }

        /// <summary>
        /// Re-scans this GameObject for <see cref="IGazeFeedback"/> components.
        /// Call after adding or removing feedback at runtime.
        /// </summary>
        public void RefreshFeedback()
        {
            feedback.Clear();
            GetComponents(feedback);
        }

        internal void NotifyEnter()
        {
            IsGazedAt = true;
            DwellProgress = 0f;
            onGazeEnter.Invoke();

            for (int i = 0; i < feedback.Count; i++)
            {
                feedback[i].OnGazeEnter(this);
            }
        }

        internal void NotifyProgress(float progress)
        {
            DwellProgress = progress;
            onDwellProgress.Invoke(progress);

            for (int i = 0; i < feedback.Count; i++)
            {
                feedback[i].OnDwellProgress(this, progress);
            }
        }

        internal void NotifyExit()
        {
            IsGazedAt = false;
            DwellProgress = 0f;
            onGazeExit.Invoke();

            for (int i = 0; i < feedback.Count; i++)
            {
                feedback[i].OnGazeExit(this);
            }
        }

        internal void NotifyActivated()
        {
            onActivated.Invoke();

            for (int i = 0; i < feedback.Count; i++)
            {
                feedback[i].OnActivated(this);
            }
        }
    }
}

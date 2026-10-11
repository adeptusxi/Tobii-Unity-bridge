using System;
using System.Collections.Generic;
using UnityEngine;

namespace TalonBridge.Gaze
{
    /// <summary>
    /// Drives gaze interaction: resolves the interactables under the gaze each
    /// frame and feeds them to a <see cref="GazeDwellTracker"/>.
    /// </summary>
    [AddComponentMenu("Talon Gaze Bridge/Gaze Pointer")]
    public sealed class GazePointer : MonoBehaviour
    {
        [Tooltip("Provider to read. Leave empty to follow the active provider.")]
        [SerializeField]
        private GazeProviderBase provider = null;

        [Tooltip("Hit test to use. Leave empty to take one from this GameObject.")]
        [SerializeField]
        private MonoBehaviour hitSource = null;

        [Header("Dwell")]
        [Tooltip("Seconds of gaze needed to activate a target. Individual " +
                 "interactables can override this.")]
        [SerializeField]
        [Min(0.05f)]
        private float dwellDuration = 0.6f;

        [Tooltip("Seconds after an activation before the same target may fire " +
                 "again.")]
        [SerializeField]
        [Min(0f)]
        private float cooldown = 0.5f;

        [Tooltip("Fraction of dwell progress lost per second once gaze leaves a " +
                 "target. 0 discards progress immediately; a small value " +
                 "forgives gaze jitter across a target's edge.")]
        [SerializeField]
        [Min(0f)]
        private float dwellDecayRate = 0f;

        [Header("Region")]
        [Tooltip("Search radius around the gaze point in pixels. 0 uses the " +
                 "gaze point alone.")]
        [SerializeField]
        [Min(0f)]
        private float regionRadius = 0f;

        [Tooltip("Treat every interactable within the radius as a target, so " +
                 "they all dwell and all activate. Off uses the radius only as " +
                 "a fallback when the gaze point itself hits nothing.")]
        [SerializeField]
        private bool activateAllInRadius = false;

        private readonly List<GazeInteractable> hits = new List<GazeInteractable>();
        private readonly GazeDwellTracker tracker = new GazeDwellTracker();

        private const float HitSourceLookupInterval = 1f;

        private IGazeHitSource resolvedHitSource;
        private float nextHitSourceLookup;
        private bool warnedNoHitSource;

        /// <summary>Raised when a target completes its dwell.</summary>
        public event Action<GazeInteractable> TargetActivated;

        /// <summary>Interactables currently under the gaze, nearest first.</summary>
        public IReadOnlyList<GazeInteractable> CurrentTargets => tracker.CurrentTargets;

        /// <summary>Nearest current target, or null.</summary>
        public GazeInteractable PrimaryTarget =>
            tracker.CurrentTargets.Count > 0 ? tracker.CurrentTargets[0] : null;

        /// <summary>Dwell progress of <see cref="PrimaryTarget"/>, 0 to 1.</summary>
        public float PrimaryDwellProgress => tracker.PrimaryProgress;

        /// <summary>The dwell state, exposed for tooling.</summary>
        public GazeDwellTracker Dwell => tracker;

        public float DwellDuration
        {
            get => dwellDuration;
            set => dwellDuration = Mathf.Max(0.05f, value);
        }

        public float Cooldown
        {
            get => cooldown;
            set => cooldown = Mathf.Max(0f, value);
        }

        public float DwellDecayRate
        {
            get => dwellDecayRate;
            set => dwellDecayRate = Mathf.Max(0f, value);
        }

        public float RegionRadius
        {
            get => regionRadius;
            set => regionRadius = Mathf.Max(0f, value);
        }

        public bool ActivateAllInRadius
        {
            get => activateAllInRadius;
            set => activateAllInRadius = value;
        }

        /// <summary>Provider being read, or null when none is active.</summary>
        public IGazeProvider Source => provider != null ? provider : GazeService.Active;

        private void Awake()
        {
            tracker.Activated += OnTargetActivated;
        }

        private void OnDestroy()
        {
            tracker.Activated -= OnTargetActivated;
        }

        private void OnEnable()
        {
            nextHitSourceLookup = 0f;
            warnedNoHitSource = false;
        }

        private void OnDisable()
        {
            // Don't leave interactables stuck in a gazed-at state.
            tracker.Reset();
        }

        private void Update()
        {
            tracker.DwellDuration = dwellDuration;
            tracker.Cooldown = cooldown;
            tracker.DecayRate = dwellDecayRate;

            IGazeProvider source = Source;
            bool valid = source != null && source.IsValid;

            if (valid)
            {
                IGazeHitSource hitTest = ResolveHitSource();
                if (hitTest != null)
                {
                    hitTest.Collect(source.ScreenPosition, regionRadius, activateAllInRadius, hits);
                }
                else
                {
                    hits.Clear();
                }
            }
            else
            {
                hits.Clear();
            }

            // Gaze is unscaled input, so dwell should not follow Time.timeScale.
            tracker.Tick(hits, valid, Time.unscaledDeltaTime);
        }

        /// <summary>
        /// The assigned hit source, or one on this GameObject. Looked up again
        /// when it is destroyed, and at an interval while there is none, rather
        /// than every frame.
        /// </summary>
        private IGazeHitSource ResolveHitSource()
        {
            // A destroyed component compares equal to null without being null.
            if (resolvedHitSource is UnityEngine.Object component && component == null)
            {
                resolvedHitSource = null;
            }

            if (resolvedHitSource != null)
            {
                return resolvedHitSource;
            }

            float now = Time.realtimeSinceStartup;
            if (now < nextHitSourceLookup)
            {
                return null;
            }

            resolvedHitSource = hitSource != null ? hitSource as IGazeHitSource : null;
            if (resolvedHitSource == null)
            {
                resolvedHitSource = GetComponent<IGazeHitSource>();
            }

            if (resolvedHitSource == null)
            {
                nextHitSourceLookup = now + HitSourceLookupInterval;
                if (!warnedNoHitSource)
                {
                    warnedNoHitSource = true;
                    Debug.LogWarning(
                        "GazePointer has no hit source. Add a GazeUIInteractor to " +
                        "this GameObject, or assign one to Hit Source.",
                        this);
                }
            }

            return resolvedHitSource;
        }

        private void OnTargetActivated(GazeInteractable target)
        {
            TargetActivated?.Invoke(target);
        }
    }
}

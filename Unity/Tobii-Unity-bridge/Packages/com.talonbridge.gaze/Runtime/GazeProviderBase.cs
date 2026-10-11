using System;
using UnityEngine;

namespace TalonBridge.Gaze
{
    /// <summary>
    /// Shared behaviour for gaze providers: registration with
    /// <see cref="GazeService"/>, retention of the last valid position, and
    /// timeout-based invalidation.
    ///
    /// Validity is driven by the age of the last valid sample rather than by
    /// the newest sample, so a dropped frame or two does not interrupt gaze,
    /// while a real tracking loss invalidates it once the timeout elapses.
    /// <see cref="LastSample"/> always reports the raw state.
    ///
    /// Subclasses implement <see cref="Poll"/> and call
    /// <see cref="PublishSample"/> from the main thread.
    /// </summary>
    public abstract class GazeProviderBase : MonoBehaviour, IGazeProvider
    {
        [Tooltip("Higher priority wins when several providers are enabled.")]
        [SerializeField]
        private int priority = 0;

        [Tooltip("Gaze is treated as invalid when no valid sample has arrived " +
                 "for this long. Prevents acting on stale gaze after tracking loss.")]
        [SerializeField]
        [Min(0.01f)]
        private float validityTimeout = 0.1f;

        private const float FilterLookupInterval = 1f;

        private GazeSample lastSample;
        private GazeSample rawSample;
        private GazeSample lastValidSample;
        private float lastValidTime = float.NegativeInfinity;
        private float lastFilterTime = float.NegativeInfinity;
        private float nextFilterLookup;
        private IGazeFilter filter;
        private bool filterAssigned;

        public event Action<GazeSample> SampleReceived;

        public bool IsValid { get; private set; }

        public Vector2 NormalizedPosition => lastValidSample.Normalized;

        public Vector2 ScreenPosition =>
            GazeCoordinates.NormalizedToScreen(lastValidSample.Normalized);

        public double LastTimestamp => lastValidSample.SourceTimestamp;

        public GazeSample LastSample => lastSample;

        /// <summary>Newest sample as received, before filtering.</summary>
        public GazeSample RawSample => rawSample;

        /// <summary>
        /// Smoothing applied to valid samples before they are published. Null
        /// publishes gaze unchanged. Taken from a filter component on this
        /// GameObject unless one is assigned here; an assigned value, including
        /// null, replaces the component lookup.
        /// </summary>
        public IGazeFilter Filter
        {
            get
            {
                ResolveFilter();
                return filter;
            }

            set
            {
                filter = value;
                filterAssigned = true;
                ResetFilter();
            }
        }

        public int Priority => priority;

        public virtual string ProviderName => GetType().Name;

        /// <summary>
        /// Seconds since the last valid sample, or infinity if there has never
        /// been one.
        /// </summary>
        public float TimeSinceValidSample =>
            float.IsNegativeInfinity(lastValidTime)
                ? float.PositiveInfinity
                : Time.realtimeSinceStartup - lastValidTime;

        public float ValidityTimeout
        {
            get => validityTimeout;
            set => validityTimeout = Mathf.Max(0.01f, value);
        }

        protected virtual void OnEnable()
        {
            nextFilterLookup = 0f;
            ResolveFilter();
            ResetFilter();
            GazeService.Register(this);
        }

        protected virtual void OnDisable()
        {
            GazeService.Unregister(this);
            IsValid = false;
        }

        protected virtual void Update()
        {
            Poll();
            UpdateValidity();
        }

        /// <summary>
        /// Read the source and publish any new sample. Called once per frame on
        /// the main thread.
        /// </summary>
        protected abstract void Poll();

        /// <summary>Record a sample and notify listeners.</summary>
        protected void PublishSample(GazeSample sample)
        {
            rawSample = sample;
            sample = ApplyFilter(sample);

            lastSample = sample;
            if (sample.IsValid)
            {
                lastValidSample = sample;
                lastValidTime = sample.ReceivedTime;
            }

            UpdateValidity();
            SampleReceived?.Invoke(sample);
            GazeService.Publish(this, sample);
        }

        private GazeSample ApplyFilter(GazeSample sample)
        {
            IGazeFilter active = ActiveFilter();
            if (active == null)
            {
                // Forget the timing so a filter that comes back starts fresh.
                lastFilterTime = float.NegativeInfinity;
                return sample;
            }

            if (!sample.IsValid)
            {
                return sample;
            }

            // History is kept across a blink or a dropped packet, and discarded
            // once the gap is long enough for gaze to have gone invalid.
            float deltaTime = sample.ReceivedTime - lastFilterTime;
            if (deltaTime > validityTimeout)
            {
                active.Reset();
                deltaTime = 0f;
            }

            lastFilterTime = sample.ReceivedTime;

            return sample.WithNormalized(active.Filter(sample.Normalized, deltaTime));
        }

        /// <summary>
        /// The filter to run, or null. A disabled filter component is skipped,
        /// so disabling it bypasses filtering without removing it.
        /// </summary>
        private IGazeFilter ActiveFilter()
        {
            ResolveFilter();
            if (filter == null)
            {
                return null;
            }

            if (filter is Behaviour behaviour && !behaviour.isActiveAndEnabled)
            {
                return null;
            }

            return filter;
        }

        /// <summary>
        /// Finds a filter component on this GameObject. Retried at an interval
        /// while there is none, so one added later is picked up without a
        /// component lookup every frame.
        /// </summary>
        private void ResolveFilter()
        {
            // A destroyed component compares equal to null without being null.
            if (filter is UnityEngine.Object component && component == null)
            {
                filter = null;
            }

            if (filter != null || filterAssigned)
            {
                return;
            }

            float now = Time.realtimeSinceStartup;
            if (now < nextFilterLookup)
            {
                return;
            }

            nextFilterLookup = now + FilterLookupInterval;
            filter = GetComponent<IGazeFilter>();
            if (filter != null)
            {
                ResetFilter();
            }
        }

        private void ResetFilter()
        {
            filter?.Reset();
            lastFilterTime = float.NegativeInfinity;
        }

        private void UpdateValidity()
        {
            IsValid = lastValidSample.IsValid
                && Time.realtimeSinceStartup - lastValidTime <= validityTimeout;
        }
    }
}

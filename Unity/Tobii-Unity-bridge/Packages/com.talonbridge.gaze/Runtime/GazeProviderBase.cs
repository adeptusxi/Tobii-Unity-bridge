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

        private GazeSample lastSample;
        private GazeSample lastValidSample;
        private float lastValidTime = float.NegativeInfinity;

        public event Action<GazeSample> SampleReceived;

        public bool IsValid { get; private set; }

        public Vector2 NormalizedPosition => lastValidSample.Normalized;

        public Vector2 ScreenPosition =>
            GazeCoordinates.NormalizedToScreen(lastValidSample.Normalized);

        public double LastTimestamp => lastValidSample.SourceTimestamp;

        public GazeSample LastSample => lastSample;

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

        private void UpdateValidity()
        {
            IsValid = lastValidSample.IsValid
                && Time.realtimeSinceStartup - lastValidTime <= validityTimeout;
        }
    }
}

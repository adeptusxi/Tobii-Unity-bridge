using System;
using System.Collections.Generic;
using UnityEngine;

namespace TalonBridge.Gaze
{
    /// <summary>
    /// Access point for gaze data. Providers register themselves when enabled
    /// and the highest-priority one becomes active, so game code can read gaze
    /// without holding a reference to a specific provider component.
    /// </summary>
    public static class GazeService
    {
        private static readonly List<IGazeProvider> Providers = new List<IGazeProvider>();

        /// <summary>The provider currently supplying gaze, or null.</summary>
        public static IGazeProvider Active { get; private set; }

        public static event Action<IGazeProvider> ActiveProviderChanged;

        /// <summary>Raised for every sample published by the active provider.</summary>
        public static event Action<GazeSample> SampleReceived;

        public static bool HasValidGaze => Active != null && Active.IsValid;

        public static Vector2 GazePositionNormalized =>
            Active != null ? Active.NormalizedPosition : Vector2.zero;

        public static Vector2 GazePositionScreen =>
            Active != null ? Active.ScreenPosition : Vector2.zero;

        public static double LastGazeTimestamp =>
            Active != null ? Active.LastTimestamp : 0d;

        public static GazeSample LastSample =>
            Active != null ? Active.LastSample : default;

        public static IReadOnlyList<IGazeProvider> RegisteredProviders => Providers;

        public static void Register(IGazeProvider provider)
        {
            if (provider == null || Providers.Contains(provider))
            {
                return;
            }

            Providers.Add(provider);
            SelectActive();
        }

        public static void Unregister(IGazeProvider provider)
        {
            if (provider == null || !Providers.Remove(provider))
            {
                return;
            }

            SelectActive();
        }

        internal static void Publish(IGazeProvider provider, GazeSample sample)
        {
            if (provider == Active)
            {
                SampleReceived?.Invoke(sample);
            }
        }

        private static void SelectActive()
        {
            IGazeProvider best = null;
            for (int i = 0; i < Providers.Count; i++)
            {
                if (best == null || Providers[i].Priority > best.Priority)
                {
                    best = Providers[i];
                }
            }

            if (best == Active)
            {
                return;
            }

            Active = best;
            ActiveProviderChanged?.Invoke(best);
        }

        // Static state survives "enter play mode without domain reload", so it
        // is cleared explicitly on every play.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetState()
        {
            Providers.Clear();
            Active = null;
            ActiveProviderChanged = null;
            SampleReceived = null;
        }
    }
}

using UnityEngine;

namespace TalonBridge.Gaze
{
    /// <summary>
    /// Smooths gaze positions before a provider publishes them.
    ///
    /// Implement this on a component and add it to the provider's GameObject.
    /// A provider with no filter component publishes gaze unchanged.
    /// </summary>
    public interface IGazeFilter
    {
        /// <summary>
        /// Returns the smoothed position for one sample.
        /// </summary>
        /// <param name="normalized">Incoming position, 0,0 at the top-left.</param>
        /// <param name="deltaTime">
        /// Seconds since the previous sample, or 0 for the first one.
        /// </param>
        Vector2 Filter(Vector2 normalized, float deltaTime);

        /// <summary>
        /// Discards accumulated history. Called when tracking is lost, so gaze
        /// does not return from a stale position.
        /// </summary>
        void Reset();
    }
}

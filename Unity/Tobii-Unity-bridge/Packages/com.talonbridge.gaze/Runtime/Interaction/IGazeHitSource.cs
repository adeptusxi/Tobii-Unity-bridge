using System.Collections.Generic;
using UnityEngine;

namespace TalonBridge.Gaze
{
    /// <summary>
    /// Finds the interactables under a gaze point.
    /// </summary>
    public interface IGazeHitSource
    {
        /// <summary>
        /// Fills <paramref name="results"/> with the interactables under the gaze point, nearest first. 
        /// </summary>
        /// <param name="screenPosition">Gaze position in Unity screen pixels.</param>
        /// <param name="radius">
        /// Search radius in pixels around the gaze point. 0 tests only the point.
        /// </param>
        /// <param name="collectAll">
        /// True to return every interactable within the radius; false to return at most one nearest interactable.
        /// </param>
        /// <param name="results">Destination list.</param>
        void Collect(
            Vector2 screenPosition,
            float radius,
            bool collectAll,
            List<GazeInteractable> results);
    }
}

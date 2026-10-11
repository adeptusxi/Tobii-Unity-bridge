using UnityEngine;

namespace TalonBridge.Gaze.Samples
{
    /// <summary>
    /// Parks a marker object at the estimated 3D gaze position, and optionally aligns it to the surface it landed on.
    /// </summary>
    [AddComponentMenu("Talon Gaze Bridge/Samples/Gaze World Marker")]
    public sealed class GazeWorldMarker : MonoBehaviour
    {
        [Tooltip("Pointer supplying the world position. Leave empty to find one.")]
        [SerializeField]
        private GazeWorldPointer pointer = null;

        [Tooltip("Object moved to the gaze position. Leave empty to move this one.")]
        [SerializeField]
        private Transform marker = null;

        [Tooltip("Rotate the marker to lie flat against the surface it hit.")]
        [SerializeField]
        private bool alignToSurface = true;

        [Tooltip("Lift the marker off the surface slightly, so it does not " +
                 "z-fight with the geometry it sits on.")]
        [SerializeField]
        private float surfaceOffset = 0.01f;

        [Tooltip("Hide the marker when the ray hits nothing, rather than " +
                 "showing it on the pointer's fallback plane.")]
        [SerializeField]
        private bool hideWithoutHit = false;

        private Transform Marker => marker != null ? marker : transform;

        private void Awake()
        {
            if (pointer == null)
            {
                pointer = FindAnyObjectByType<GazeWorldPointer>();
            }
        }

        private void LateUpdate()
        {
            if (pointer == null)
            {
                return;
            }

            bool show = hideWithoutHit ? pointer.HasWorldHit : pointer.HasWorldPosition;
            SetVisible(show);

            if (!show)
            {
                return;
            }

            Vector3 position = pointer.GazePositionWorld;

            if (pointer.HasWorldHit)
            {
                position += pointer.HitNormal * surfaceOffset;

                if (alignToSurface)
                {
                    Marker.rotation = Quaternion.LookRotation(pointer.HitNormal);
                }
            }

            Marker.position = position;
        }

        /// <summary>
        /// Toggles the renderers (not the GameObject)
        /// </summary>
        private void SetVisible(bool visible)
        {
            var renderers = Marker.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].enabled = visible;
            }
        }
    }
}

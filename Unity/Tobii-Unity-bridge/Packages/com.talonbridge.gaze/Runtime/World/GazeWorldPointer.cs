using System;
using UnityEngine;

namespace TalonBridge.Gaze
{
    /// <summary>
    /// Guesses where in a 3D scene the player is looking by casting a ray from the camera through the gaze point.
    /// </summary>
    [AddComponentMenu("Talon Gaze Bridge/Gaze World Pointer")]
    public sealed class GazeWorldPointer : MonoBehaviour
    {
        [Tooltip("Camera the gaze ray is cast from. Leave empty to use " +
                 "Camera.main.")]
        [SerializeField]
        private Camera sourceCamera = null;

        [Tooltip("Provider to read. Leave empty to follow the active provider.")]
        [SerializeField]
        private GazeProviderBase provider = null;

        [Header("Raycast")]
        [Tooltip("Layers the gaze ray can hit.")]
        [SerializeField]
        private LayerMask layerMask = ~0;

        [Tooltip("How far the gaze ray reaches, in world units, from the " +
                 "camera's near plane.")]
        [SerializeField]
        [Min(0.01f)]
        private float maxDistance = 100f;

        [Tooltip("Whether the ray hits trigger colliders.")]
        [SerializeField]
        private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;

        [Header("Fallback")]
        [Tooltip("When the ray hits nothing, report a point this far along the " +
                 "gaze ray instead. Off reports no position.")]
        [SerializeField]
        private bool useFallbackPlane = true;

        [Tooltip("Distance along the gaze ray, in world units. Measured from " +
                 "the camera's near plane, where the ray starts.")]
        [SerializeField]
        [Min(0.01f)]
        private float fallbackDistance = 10f;

        private Camera resolvedCamera;

        /// <summary>Raised when the collider under the gaze changes, including to null.</summary>
        public event Action<Collider> HitColliderChanged;

        /// <summary>
        /// Latest world position under the gaze. Valid only while <see cref="HasWorldPosition"/> is true; retains its last value otherwise.
        /// </summary>
        public Vector3 GazePositionWorld { get; private set; }

        /// <summary>True when the gaze ray hit a collider.</summary>
        public bool HasWorldHit { get; private set; }

        /// <summary>
        /// True when <see cref="GazePositionWorld"/> holds a usable point, either from a hit or from the fallback plane.
        /// </summary>
        public bool HasWorldPosition { get; private set; }

        /// <summary>Collider under the gaze, or null.</summary>
        public Collider HitCollider { get; private set; }

        /// <summary>Surface normal at the hit, or <see cref="Vector3.zero"/>.</summary>
        public Vector3 HitNormal { get; private set; }

        /// <summary>Distance along the gaze ray to the hit, or 0.</summary>
        public float HitDistance { get; private set; }

        /// <summary>The gaze ray used this frame.</summary>
        public Ray GazeRay { get; private set; }

        public Camera SourceCamera
        {
            get => sourceCamera != null ? sourceCamera : ResolveCamera();
            set
            {
                sourceCamera = value;
                resolvedCamera = value;
            }
        }

        public LayerMask LayerMask
        {
            get => layerMask;
            set => layerMask = value;
        }

        public float MaxDistance
        {
            get => maxDistance;
            set => maxDistance = Mathf.Max(0.01f, value);
        }

        public bool UseFallbackPlane
        {
            get => useFallbackPlane;
            set => useFallbackPlane = value;
        }

        public float FallbackDistance
        {
            get => fallbackDistance;
            set => fallbackDistance = Mathf.Max(0.01f, value);
        }

        private IGazeProvider Source => provider != null ? provider : GazeService.Active;

        private void OnDisable()
        {
            Clear();
        }

        private void Update()
        {
            IGazeProvider source = Source;
            Camera camera = SourceCamera;

            if (camera == null || source == null || !source.IsValid)
            {
                Clear();
                return;
            }

            Ray ray = camera.ScreenPointToRay(source.ScreenPosition);
            GazeRay = ray;

            if (Physics.Raycast(ray, out RaycastHit hit, maxDistance, layerMask, triggerInteraction))
            {
                GazePositionWorld = hit.point;
                HitNormal = hit.normal;
                HitDistance = hit.distance;
                HasWorldHit = true;
                HasWorldPosition = true;
                SetCollider(hit.collider);
                return;
            }

            HasWorldHit = false;
            HitNormal = Vector3.zero;
            HitDistance = 0f;
            SetCollider(null);

            if (useFallbackPlane)
            {
                GazePositionWorld = ray.GetPoint(fallbackDistance);
                HasWorldPosition = true;
            }
            else
            {
                HasWorldPosition = false;
            }
        }

        /// <summary>
        /// Drops the current hit but keeps the last world position, matching how
        /// providers retain their last valid gaze.
        /// </summary>
        private void Clear()
        {
            HasWorldHit = false;
            HasWorldPosition = false;
            HitNormal = Vector3.zero;
            HitDistance = 0f;
            SetCollider(null);
        }

        private void SetCollider(Collider collider)
        {
            // Compare by reference in case a collider was destroyed 
            if (ReferenceEquals(HitCollider, collider))
            {
                return;
            }

            HitCollider = collider;
            HitColliderChanged?.Invoke(collider);
        }

        private Camera ResolveCamera()
        {
            if (resolvedCamera == null)
            {
                resolvedCamera = Camera.main;
            }

            return resolvedCamera;
        }
    }
}

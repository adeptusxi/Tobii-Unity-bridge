using UnityEngine;

namespace TalonBridge.Gaze
{
    /// <summary>
    /// Publishes gaze as global shader properties, so any shader can react to
    /// where the player is looking.
    ///
    /// <c>ShaderLibrary/Gaze.hlsl</c> declares the same properties and has
    /// helpers for reading them. Positions keep their last valid value when
    /// tracking is lost; <c>_GazeWeight</c> and <c>_GazeWorldPos.w</c> fade to 0
    /// instead, so effects fade out rather than freeze or pop.
    /// </summary>
    [AddComponentMenu("Talon Gaze Bridge/Gaze Shader Globals")]
    public sealed class GazeShaderGlobals : MonoBehaviour
    {
        /// <summary>xy: gaze in 0..1 screen UV, 0,0 at the bottom-left.</summary>
        public static readonly int ScreenUVId = Shader.PropertyToID("_GazeScreenUV");

        /// <summary>xy: gaze in Unity screen pixels, 0,0 at the bottom-left.</summary>
        public static readonly int ScreenPosId = Shader.PropertyToID("_GazeScreenPos");

        /// <summary>xy: screen size in pixels, the units of the radius.</summary>
        public static readonly int ScreenSizeId = Shader.PropertyToID("_GazeScreenSize");

        /// <summary>Gaze radius in pixels.</summary>
        public static readonly int RadiusId = Shader.PropertyToID("_GazeRadius");

        /// <summary>0..1: 1 while gaze is valid, fading to 0 when it is lost.</summary>
        public static readonly int WeightId = Shader.PropertyToID("_GazeWeight");

        /// <summary>xyz: world position under the gaze. w: 0..1 weight.</summary>
        public static readonly int WorldPosId = Shader.PropertyToID("_GazeWorldPos");

        [Tooltip("Provider to read. Leave empty to follow the active provider.")]
        [SerializeField]
        private GazeProviderBase provider = null;

        [Tooltip("Optional. Supplies the 3D gaze position for shaders that " +
                 "react in world space.")]
        [SerializeField]
        private GazeWorldPointer worldPointer = null;

        [Header("Radius")]
        [Tooltip("Radius around the gaze point that shaders react in, in pixels.")]
        [SerializeField]
        [Min(0f)]
        private float radius = 150f;

        [Tooltip("Use the Region Radius of the pointer below instead of Radius, " +
                 "so effects cover the area that can activate UI. Falls back to " +
                 "Radius while the pointer's radius is 0.")]
        [SerializeField]
        private bool useRegionRadius = false;

        [Tooltip("Pointer whose Region Radius is used when Use Region Radius is on.")]
        [SerializeField]
        private GazePointer pointer = null;

        [Header("Fade")]
        [Tooltip("Seconds for effects to fade in when gaze becomes valid and " +
                 "out when it is lost. 0 switches at once.")]
        [SerializeField]
        [Min(0f)]
        private float fadeTime = 0.15f;

        private float weight;
        private float worldWeight;
        private Vector3 worldPosition;

        public float Radius
        {
            get => radius;
            set => radius = Mathf.Max(0f, value);
        }

        public bool UseRegionRadius
        {
            get => useRegionRadius;
            set => useRegionRadius = value;
        }

        public GazePointer Pointer
        {
            get => pointer;
            set => pointer = value;
        }

        public GazeWorldPointer WorldPointer
        {
            get => worldPointer;
            set => worldPointer = value;
        }

        public float FadeTime
        {
            get => fadeTime;
            set => fadeTime = Mathf.Max(0f, value);
        }

        /// <summary>The radius being published: Radius, or the pointer's.</summary>
        public float EffectiveRadius =>
            useRegionRadius && pointer != null && pointer.RegionRadius > 0f
                ? pointer.RegionRadius
                : radius;

        /// <summary>The value published as <c>_GazeWeight</c>.</summary>
        public float Weight => weight;

        /// <summary>The value published as <c>_GazeWorldPos.w</c>.</summary>
        public float WorldWeight => worldWeight;

        private IGazeProvider Source => provider != null ? provider : GazeService.Active;

        private void OnEnable()
        {
            weight = 0f;
            worldWeight = 0f;
        }

        private void OnDisable()
        {
            weight = 0f;
            worldWeight = 0f;
            Shader.SetGlobalFloat(WeightId, 0f);
            Shader.SetGlobalVector(
                WorldPosId,
                new Vector4(worldPosition.x, worldPosition.y, worldPosition.z, 0f));
        }

        // LateUpdate, so providers and the world pointer have updated this frame.
        private void LateUpdate()
        {
            IGazeProvider source = Source;
            bool valid = source != null && source.IsValid;
            bool worldValid = valid && worldPointer != null && worldPointer.HasWorldPosition;

            float step = fadeTime > 0f ? Time.unscaledDeltaTime / fadeTime : 1f;
            weight = Mathf.MoveTowards(weight, valid ? 1f : 0f, step);
            worldWeight = Mathf.MoveTowards(worldWeight, worldValid ? 1f : 0f, step);

            float width = Mathf.Max(1, Screen.width);
            float height = Mathf.Max(1, Screen.height);

            if (source != null)
            {
                // Already bottom-left origin: the provider converts through
                // GazeCoordinates.
                Vector2 screen = source.ScreenPosition;
                Shader.SetGlobalVector(ScreenPosId, new Vector4(screen.x, screen.y, 0f, 0f));
                Shader.SetGlobalVector(
                    ScreenUVId, new Vector4(screen.x / width, screen.y / height, 0f, 0f));
            }

            if (worldValid)
            {
                worldPosition = worldPointer.GazePositionWorld;
            }

            Shader.SetGlobalVector(ScreenSizeId, new Vector4(width, height, 0f, 0f));
            Shader.SetGlobalFloat(RadiusId, EffectiveRadius);
            Shader.SetGlobalFloat(WeightId, weight);
            Shader.SetGlobalVector(
                WorldPosId,
                new Vector4(worldPosition.x, worldPosition.y, worldPosition.z, worldWeight));
        }
    }
}

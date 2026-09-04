using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TalonBridge.Gaze
{
    /// <summary>
    /// Moves a UI element to the gaze position.
    /// Put it on a RectTransform inside a Canvas. Screen Space - Overlay doesn't need a camera; the other render modes use the Canvas's own camera.
    /// </summary>
    [AddComponentMenu("Talon Gaze Bridge/Gaze Canvas Cursor")]
    [RequireComponent(typeof(RectTransform))]
    public sealed class GazeCanvasCursor : MonoBehaviour
    {
        [Tooltip("Provider to follow. Leave empty to follow the active provider.")]
        [SerializeField]
        private GazeProviderBase provider = null;

        [Tooltip("Hide the cursor while gaze is invalid. Off keeps it at the " +
                 "last valid position, tinted with Invalid Color.")]
        [SerializeField]
        private bool hideWhenInvalid = false;

        [Tooltip("Optional. Tinted to show gaze validity. Left empty, the " +
                 "first graphic on this object or its children is used.")]
        [SerializeField]
        private Graphic graphic = null;

        [SerializeField]
        private Color validColor = new Color(0.2f, 1f, 0.4f, 0.9f);

        [SerializeField]
        private Color invalidColor = new Color(1f, 0.35f, 0.2f, 0.5f);

        [Tooltip("Seconds for the cursor to catch up with the gaze position. " +
                 "0 follows it exactly. Smoothing here is cosmetic and does not " +
                 "affect the gaze data other systems read.")]
        [SerializeField]
        [Min(0f)]
        private float smoothing = 0f;

        private RectTransform rectTransform;
        private RectTransform canvasRect;
        private Canvas canvas;
        private readonly List<Graphic> graphics = new List<Graphic>();
        private Vector2 currentPosition;
        private bool hasPosition;
        private bool visible = true;

        private IGazeProvider Source => provider != null ? provider : GazeService.Active;

        private void Awake()
        {
            rectTransform = (RectTransform)transform;
            CacheCanvas();
            CacheGraphics();
        }

        private void OnEnable()
        {
            hasPosition = false;
            visible = true;
            CacheCanvas();
            CacheGraphics();
        }

        /// <summary>
        /// Re-scans for the graphics this cursor shows and hides. Call after building the cursor's visuals at runtime.
        /// </summary>
        public void RefreshGraphics()
        {
            CacheGraphics();
        }

        private void LateUpdate()
        {
            IGazeProvider source = Source;
            if (source == null)
            {
                SetVisible(false);
                return;
            }

            bool valid = source.IsValid;
            bool everValid = !float.IsInfinity(source.TimeSinceValidSample);

            if (!everValid || (!valid && hideWhenInvalid))
            {
                SetVisible(false);
                return;
            }

            SetVisible(true);

            if (graphic != null)
            {
                graphic.color = valid ? validColor : invalidColor;
            }

            Vector2 target = source.ScreenPosition;
            if (!hasPosition || smoothing <= 0f)
            {
                currentPosition = target;
                hasPosition = true;
            }
            else
            {
                // Framerate-independent exponential approach.
                float t = 1f - Mathf.Exp(-Time.unscaledDeltaTime / smoothing);
                currentPosition = Vector2.Lerp(currentPosition, target, t);
            }

            rectTransform.position = ToCursorPosition(currentPosition);
        }

        /// <summary>
        /// Converts a screen position into a world position the RectTransform can be placed at, for whichever render mode the Canvas uses.
        /// </summary>
        private Vector3 ToCursorPosition(Vector2 screenPosition)
        {
            if (canvas == null)
            {
                CacheCanvas();
            }

            if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                return screenPosition;
            }

            Camera camera = canvas.worldCamera != null ? canvas.worldCamera : Camera.main;
            if (canvasRect != null && RectTransformUtility.ScreenPointToWorldPointInRectangle(
                    canvasRect, screenPosition, camera, out Vector3 world))
            {
                return world;
            }

            return rectTransform.position;
        }

        /// <summary>
        /// Shows or hides the cursor's graphics.
        /// </summary>
        private void SetVisible(bool value)
        {
            if (visible == value)
            {
                return;
            }

            visible = value;
            for (int i = 0; i < graphics.Count; i++)
            {
                if (graphics[i] != null)
                {
                    graphics[i].enabled = value;
                }
            }
        }

        private void CacheGraphics()
        {
            graphics.Clear();
            GetComponentsInChildren(true, graphics);

            if (graphic == null && graphics.Count > 0)
            {
                graphic = graphics[0];
            }
        }

        private void CacheCanvas()
        {
            canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                canvas = canvas.rootCanvas;
                canvasRect = canvas.transform as RectTransform;
            }
        }
    }
}

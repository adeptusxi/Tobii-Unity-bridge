using UnityEngine;

namespace TalonBridge.Gaze
{
    /// <summary>
    /// Draws a marker at the current gaze position, with an optional readout of
    /// the coordinates. Used to check orientation, scaling, latency and window
    /// alignment.
    ///
    /// Drawn with IMGUI so it works in any render pipeline and doesn't need a canvas. 
    /// </summary>
    [AddComponentMenu("Talon Gaze Bridge/Gaze Debug Cursor")]
    public sealed class GazeDebugCursor : MonoBehaviour
    {
        [Tooltip("Provider to display. Leave empty to follow the active provider.")]
        [SerializeField]
        private GazeProviderBase provider = null;

        [SerializeField]
        private bool showCursor = true;

        [Tooltip("Show the coordinate readout next to the cursor.")]
        [SerializeField]
        private bool showCoordinates = false;

        [Tooltip("Optional. When set, the readout also names the gaze target " +
                 "and its dwell progress.")]
        [SerializeField]
        private GazePointer pointer = null;

        [Tooltip("Optional. When set, the readout also shows the estimated 3D " +
                 "gaze position.")]
        [SerializeField]
        private GazeWorldPointer worldPointer = null;

        [Tooltip("Keep drawing the marker at the last valid position after " +
                 "tracking is lost, in the invalid colour.")]
        [SerializeField]
        private bool showWhenInvalid = true;

        [SerializeField]
        [Min(4f)]
        private float size = 28f;

        [SerializeField]
        [Min(1f)]
        private float thickness = 2f;

        [SerializeField]
        private Color validColor = new Color(0.2f, 1f, 0.4f, 0.9f);

        [SerializeField]
        private Color invalidColor = new Color(1f, 0.35f, 0.2f, 0.6f);

        private Texture2D pixel;
        private GUIStyle labelStyle;

        public bool ShowCursor
        {
            get => showCursor;
            set => showCursor = value;
        }

        public bool ShowCoordinates
        {
            get => showCoordinates;
            set => showCoordinates = value;
        }

        private IGazeProvider Source => provider != null ? provider : GazeService.Active;

        private void OnDestroy()
        {
            if (pixel != null)
            {
                Destroy(pixel);
            }
        }

        private void OnGUI()
        {
            IGazeProvider source = Source;
            if (!showCursor || source == null)
            {
                return;
            }

            bool valid = source.IsValid;
            if (!valid && (!showWhenInvalid || float.IsInfinity(source.TimeSinceValidSample)))
            {
                if (showCoordinates)
                {
                    DrawStatusBanner(source);
                }

                return;
            }

            // IMGUI shares the top-left origin of normalized gaze coordinates, so no y flip
            Vector2 normalized = source.NormalizedPosition;
            var point = new Vector2(normalized.x * Screen.width, normalized.y * Screen.height);

            Color color = valid ? validColor : invalidColor;
            DrawCrosshair(point, color);

            if (showCoordinates)
            {
                DrawReadout(source, point, color);
            }
        }

        private void DrawCrosshair(Vector2 point, Color color)
        {
            EnsureResources();
            Color previous = GUI.color;
            GUI.color = color;

            float half = size * 0.5f;
            GUI.DrawTexture(
                new Rect(point.x - half, point.y - thickness * 0.5f, size, thickness),
                pixel);
            GUI.DrawTexture(
                new Rect(point.x - thickness * 0.5f, point.y - half, thickness, size),
                pixel);

            GUI.color = previous;
        }

        private void DrawReadout(IGazeProvider source, Vector2 point, Color color)
        {
            EnsureResources();
            Vector2 normalized = source.NormalizedPosition;
            Vector2 screen = source.ScreenPosition;

            string text =
                $"{source.ProviderName}\n" +
                $"norm   {normalized.x:F3}, {normalized.y:F3}\n" +
                $"screen {screen.x:F0}, {screen.y:F0}\n" +
                (source.IsValid ? "valid" : "INVALID");

            if (!string.IsNullOrEmpty(PointerInput.Status))
            {
                text += $"\n{PointerInput.Status}";
            }

            if (pointer != null)
            {
                GazeInteractable target = pointer.PrimaryTarget;
                text += target != null
                    ? $"\ntarget {target.name} {pointer.PrimaryDwellProgress:P0}"
                    : "\ntarget none";
            }

            if (worldPointer != null)
            {
                if (worldPointer.HasWorldPosition)
                {
                    Vector3 world = worldPointer.GazePositionWorld;
                    text += $"\nworld  {world.x:F2}, {world.y:F2}, {world.z:F2}";
                    text += worldPointer.HasWorldHit
                        ? $"\nhit    {worldPointer.HitCollider.name}"
                        : "\nhit    none (fallback plane)";
                }
                else
                {
                    text += "\nworld  none";
                }
            }

            labelStyle.normal.textColor = color;
            Vector2 textSize = labelStyle.CalcSize(new GUIContent(text));
            float x = Mathf.Min(point.x + size, Screen.width - textSize.x - 4f);
            float y = Mathf.Min(point.y + size * 0.5f, Screen.height - textSize.y - 4f);
            GUI.Label(new Rect(x, y, textSize.x, textSize.y), text, labelStyle);
        }

        /// <summary>Shown before any gaze has been received.</summary>
        private void DrawStatusBanner(IGazeProvider source)
        {
            EnsureResources();
            string text = $"{source.ProviderName}: no gaze yet";
            if (!string.IsNullOrEmpty(PointerInput.Status))
            {
                text += $"\n{PointerInput.Status}";
            }

            labelStyle.normal.textColor = invalidColor;
            Vector2 size = labelStyle.CalcSize(new GUIContent(text));
            GUI.Label(new Rect(8f, 8f, size.x, size.y), text, labelStyle);
        }

        private void EnsureResources()
        {
            if (pixel == null)
            {
                pixel = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
                pixel.SetPixel(0, 0, Color.white);
                pixel.Apply();
            }

            if (labelStyle == null)
            {
                labelStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 11,
                    alignment = TextAnchor.UpperLeft,
                    wordWrap = false,
                };
            }
        }
    }
}

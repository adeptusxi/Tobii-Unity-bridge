using UnityEngine;

namespace TalonBridge.Gaze
{
    /// <summary>
    /// Draws a marker at the current gaze position. 
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

        public bool ShowCursor
        {
            get => showCursor;
            set => showCursor = value;
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
                return;
            }

            // IMGUI shares the top-left origin of normalized gaze coordinates, so no y flip
            Vector2 normalized = source.NormalizedPosition;
            var point = new Vector2(normalized.x * Screen.width, normalized.y * Screen.height);

            DrawCrosshair(point, valid ? validColor : invalidColor);
        }

        private void DrawCrosshair(Vector2 point, Color color)
        {
            EnsurePixel();
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

        private void EnsurePixel()
        {
            if (pixel != null)
            {
                return;
            }

            pixel = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            pixel.SetPixel(0, 0, Color.white);
            pixel.Apply();
        }
    }
}

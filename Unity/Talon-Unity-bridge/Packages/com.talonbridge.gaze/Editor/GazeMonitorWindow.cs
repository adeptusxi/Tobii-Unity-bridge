using TalonBridge.Gaze.Udp;
using UnityEditor;
using UnityEngine;

namespace TalonBridge.Gaze.Editor
{
    /// <summary>
    /// Live readout of the gaze bridge: gaze state, the current target, the 3D position and transport diagnostics.
    /// </summary>
    public sealed class GazeMonitorWindow : EditorWindow
    {
        private const double RepaintInterval = 1d / 15d;

        [SerializeField] private UdpGazeProvider provider;
        [SerializeField] private GazePointer pointer;
        [SerializeField] private GazeWorldPointer worldPointer;
        [SerializeField] private bool showSource;

        private Vector2 scroll;
        private double lastRepaint;

        [MenuItem("Window/Talon Gaze Monitor")]
        private static void Open()
        {
            GazeMonitorWindow window = GetWindow<GazeMonitorWindow>();
            window.titleContent = new GUIContent("Talon Gaze Monitor");
            window.minSize = new Vector2(300f, 260f);
            window.Show();
        }

        private void OnEnable()
        {
            Refresh();
            EditorApplication.hierarchyChanged += Refresh;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private void OnDisable()
        {
            EditorApplication.hierarchyChanged -= Refresh;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        }

        /// <summary>
        /// Repaints only while the readout can actually change.
        /// </summary>
        private void Update()
        {
            if (!EditorApplication.isPlaying)
            {
                return;
            }

            double now = EditorApplication.timeSinceStartup;
            if (now - lastRepaint < RepaintInterval)
            {
                return;
            }

            lastRepaint = now;
            Repaint();
        }

        private void OnPlayModeChanged(PlayModeStateChange change)
        {
            Refresh();
        }

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);

            DrawSource();
            EditorGUILayout.Space();

            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "Enter play mode to see live eye tracking monitor.", MessageType.Info);
                EditorGUILayout.EndScrollView();
                return;
            }

            DrawReadout();

            EditorGUILayout.EndScrollView();
        }

        /// <summary>
        /// Which components are being watched. Resolved automatically; shown so
        /// a scene with more than one rig can be pointed at the right one.
        /// </summary>
        private void DrawSource()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                showSource = EditorGUILayout.Foldout(showSource, "Source", true);
                if (GUILayout.Button("Refresh", EditorStyles.miniButton, GUILayout.Width(60f)))
                {
                    Refresh();
                }
            }

            if (!showSource)
            {
                return;
            }

            EditorGUI.indentLevel++;
            provider = (UdpGazeProvider)EditorGUILayout.ObjectField(
                "Provider", provider, typeof(UdpGazeProvider), true);
            pointer = (GazePointer)EditorGUILayout.ObjectField(
                "Pointer", pointer, typeof(GazePointer), true);
            worldPointer = (GazeWorldPointer)EditorGUILayout.ObjectField(
                "World Pointer", worldPointer, typeof(GazeWorldPointer), true);
            EditorGUI.indentLevel--;
        }

        private void DrawReadout()
        {
            IGazeProvider source = provider != null ? provider : GazeService.Active;

            if (source == null)
            {
                EditorGUILayout.HelpBox(
                    "No gaze provider in the scene. Add one with " +
                    "GameObject ▸ Talon Gaze Bridge ▸ Talon Gaze Rig.",
                    MessageType.Warning);
                return;
            }

            EditorGUILayout.LabelField("Gaze", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;

            Row("Provider", source.ProviderName);
            Row("Valid", source.IsValid ? "yes" : "no");
            Row("Age", float.IsInfinity(source.TimeSinceValidSample)
                ? "never"
                : $"{source.TimeSinceValidSample * 1000f:F0} ms");

            Vector2 normalized = source.NormalizedPosition;
            Row("Normalized", $"{normalized.x:F3}, {normalized.y:F3}");

            // Screen.width inside an editor window is the window's width, not
            // the game view's, so the conversion is done against the game view
            // size rather than through GazeCoordinates.
            if (TryGetGameViewSize(out Vector2 view))
            {
                Row("Screen", $"{normalized.x * view.x:F0}, {(1f - normalized.y) * view.y:F0}");
                Row("Resolution", $"{view.x:F0} x {view.y:F0}");
            }
            else
            {
                Row("Screen", "game view size unavailable");
            }

            Row("Eyes", source.LastSample.Eyes.ToString());
            Row("Inside window", source.LastSample.InsideWindow ? "yes" : "no");

            EditorGUI.indentLevel--;

            DrawTarget();
            DrawWorld();
            DrawTransport();
        }

        private void DrawTarget()
        {
            if (pointer == null)
            {
                return;
            }

            EditorGUILayout.Space(2f);
            EditorGUILayout.LabelField("Target", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;

            GazeInteractable target = pointer.PrimaryTarget;
            if (target != null)
            {
                Row("Element", target.name);
                Rect bar = EditorGUILayout.GetControlRect(
                    true, EditorGUIUtility.singleLineHeight);
                float progress = pointer.PrimaryDwellProgress;
                EditorGUI.ProgressBar(bar, progress, $"Dwell {progress:P0}");
            }
            else
            {
                Row("Element", "none");
            }

            EditorGUI.indentLevel--;
        }

        private void DrawWorld()
        {
            if (worldPointer == null)
            {
                return;
            }

            EditorGUILayout.Space(2f);
            EditorGUILayout.LabelField("3D", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;

            if (worldPointer.HasWorldPosition)
            {
                Vector3 world = worldPointer.GazePositionWorld;
                Row("Position", $"{world.x:F2}, {world.y:F2}, {world.z:F2}");
                Row("Hit", worldPointer.HasWorldHit && worldPointer.HitCollider != null
                    ? worldPointer.HitCollider.name
                    : "none (fallback plane)");
            }
            else
            {
                Row("Position", "none");
            }

            EditorGUI.indentLevel--;
        }

        private void DrawTransport()
        {
            if (provider == null)
            {
                return;
            }

            EditorGUILayout.Space(2f);
            EditorGUILayout.LabelField("Transport", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;

            GazeDiagnostics diagnostics = provider.Diagnostics;
            Row("Socket", diagnostics.IsListening
                ? $"listening {diagnostics.Endpoint}"
                : "closed");

            if (!string.IsNullOrEmpty(diagnostics.LastError))
            {
                EditorGUILayout.HelpBox(diagnostics.LastError, MessageType.Warning);
            }

            Row("Rate", $"{diagnostics.PacketsPerSecond:F1} /s");
            Row("Last packet", $"{diagnostics.TimeSinceLastPacket * 1000f:F0} ms ago");
            Row("Latency", $"{diagnostics.LastPacketLatency * 1000d:F1} ms");
            Row("Packets", $"{diagnostics.PacketsReceived}");
            Row("Errors", $"{diagnostics.ParseErrors} parse, " +
                          $"{diagnostics.InvalidPackets} invalid, " +
                          $"{diagnostics.UnmappablePackets} unmappable");

            Rect surface = provider.Mapper.ResolvedSurfaceRect;
            Row("Surface rect", surface == Rect.zero
                ? "not rect-based"
                : $"{surface.x:F0}, {surface.y:F0}  {surface.width:F0} x {surface.height:F0}");

            EditorGUI.indentLevel--;
        }

        private static void Row(string label, string value)
        {
            EditorGUILayout.LabelField(label, value);
        }

        private void Refresh()
        {
            provider = FindFirst(provider);
            pointer = FindFirst(pointer);
            worldPointer = FindFirst(worldPointer);
            Repaint();
        }

        /// <summary>Keeps an explicit assignment; otherwise takes the first in the scene.</summary>
        private static T FindFirst<T>(T current) where T : Object
        {
            if (current != null)
            {
                return current;
            }

            T[] found = FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            return found.Length > 0 ? found[0] : null;
        }

        private static bool TryGetGameViewSize(out Vector2 size)
        {
            string resolution = UnityStats.screenRes;
            if (!string.IsNullOrEmpty(resolution))
            {
                string[] parts = resolution.Split('x');
                if (parts.Length == 2
                    && int.TryParse(parts[0], out int width)
                    && int.TryParse(parts[1], out int height)
                    && width > 0 && height > 0)
                {
                    size = new Vector2(width, height);
                    return true;
                }
            }

            if (GameViewRectResolver.TryGetRect(out Rect rect)
                && rect.width > 0f && rect.height > 0f)
            {
                size = rect.size;
                return true;
            }

            size = Vector2.zero;
            return false;
        }
    }
}

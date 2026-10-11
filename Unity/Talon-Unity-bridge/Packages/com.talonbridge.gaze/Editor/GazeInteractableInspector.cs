using UnityEditor;
using UnityEngine;

namespace TalonBridge.Gaze.Editor
{
    /// <summary>
    /// Adds a live dwell readout to <see cref="GazeInteractable"/>, so a target
    /// can be watched without the Talon Gaze Monitor open, and warns about the
    /// setups that silently do nothing.
    /// </summary>
    [CustomEditor(typeof(GazeInteractable))]
    [CanEditMultipleObjects]
    public sealed class GazeInteractableInspector : UnityEditor.Editor
    {
        public override bool RequiresConstantRepaint()
        {
            return Application.isPlaying;
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var interactable = (GazeInteractable)target;

            if (!Application.isPlaying)
            {
                DrawSetupWarnings(interactable);
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Live", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.Toggle("Gazed At", interactable.IsGazedAt);
            }

            Rect bar = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
            float progress = interactable.DwellProgress;
            EditorGUI.ProgressBar(bar, progress, $"Dwell {progress:P0}");
        }

        /// <summary>
        /// Flags the two ways an interactable ends up inert: nothing listening,
        /// and no pointer in the scene to drive it.
        /// </summary>
        private void DrawSetupWarnings(GazeInteractable interactable)
        {
            if (targets.Length > 1)
            {
                return;
            }

            bool hasFeedback = interactable.GetComponent<IGazeFeedback>() != null;
            bool hasListeners =
                interactable.Activated.GetPersistentEventCount() > 0
                || interactable.DwellProgressed.GetPersistentEventCount() > 0
                || interactable.GazeEntered.GetPersistentEventCount() > 0
                || interactable.GazeExited.GetPersistentEventCount() > 0;

            if (!hasFeedback && !hasListeners)
            {
                EditorGUILayout.HelpBox(
                    "Nothing is wired up: no IGazeFeedback component and no event " +
                    "listeners, so gazing at this element will have no effect.",
                    MessageType.Info);
            }

            if (FindObjectsByType<GazePointer>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 0)
            {
                EditorGUILayout.HelpBox(
                    "No GazePointer in the scene, so no gaze targets are resolved. " +
                    "Add one with GameObject ▸ Talon Gaze Bridge ▸ Gaze Pointer.",
                    MessageType.Warning);
            }
        }
    }
}

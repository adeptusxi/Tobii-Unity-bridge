using TalonBridge.Gaze.Udp;
using UnityEditor;
using UnityEngine;

namespace TalonBridge.Gaze.Editor
{
    /// <summary>Scene creation helpers for the gaze components.</summary>
    internal static class GazeRigMenu
    {
        [MenuItem("GameObject/Talon Gaze Bridge/Talon Gaze Rig", false, 10)]
        private static void CreateTalonRig(MenuCommand command)
        {
            var go = new GameObject("Gaze Rig (Talon)");
            go.AddComponent<UdpGazeProvider>();
            go.AddComponent<GazeDebugCursor>();

            GameObjectUtility.SetParentAndAlign(go, command.context as GameObject);
            Undo.RegisterCreatedObjectUndo(go, "Create Talon Gaze Rig");
            Selection.activeGameObject = go;
        }

        [MenuItem("GameObject/Talon Gaze Bridge/Mouse Gaze Rig", false, 11)]
        private static void CreateMouseRig(MenuCommand command)
        {
            var go = new GameObject("Gaze Rig (Mouse)");
            go.AddComponent<MouseGazeProvider>();
            go.AddComponent<GazeDebugCursor>();

            GameObjectUtility.SetParentAndAlign(go, command.context as GameObject);
            Undo.RegisterCreatedObjectUndo(go, "Create Mouse Gaze Rig");
            Selection.activeGameObject = go;
        }
    }
}

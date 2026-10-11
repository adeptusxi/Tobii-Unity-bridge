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

            Place(go, command, "Create Talon Gaze Rig");
        }

        [MenuItem("GameObject/Talon Gaze Bridge/Mouse Gaze Rig", false, 11)]
        private static void CreateMouseRig(MenuCommand command)
        {
            var go = new GameObject("Gaze Rig (Mouse)");
            go.AddComponent<MouseGazeProvider>();
            go.AddComponent<GazeDebugCursor>();

            Place(go, command, "Create Mouse Gaze Rig");
        }

        [MenuItem("GameObject/Talon Gaze Bridge/Gaze Pointer", false, 12)]
        private static void CreateGazePointer(MenuCommand command)
        {
            var go = new GameObject("Gaze Pointer");
            go.AddComponent<GazeUIInteractor>();
            go.AddComponent<GazePointer>();

            Place(go, command, "Create Gaze Pointer");
        }

        [MenuItem("GameObject/Talon Gaze Bridge/Gaze World Pointer", false, 13)]
        private static void CreateWorldPointer(MenuCommand command)
        {
            var go = new GameObject("Gaze World Pointer");
            go.AddComponent<GazeWorldPointer>();

            Place(go, command, "Create Gaze World Pointer");
        }

        [MenuItem("GameObject/Talon Gaze Bridge/Gaze Shader Globals", false, 14)]
        private static void CreateShaderGlobals(MenuCommand command)
        {
            var go = new GameObject("Gaze Shader Globals");
            go.AddComponent<GazeShaderGlobals>();

            Place(go, command, "Create Gaze Shader Globals");
        }

        [MenuItem("GameObject/Talon Gaze Bridge/Gaze Spectator View", false, 15)]
        private static void CreateSpectatorView(MenuCommand command)
        {
            var go = new GameObject("Gaze Spectator View", typeof(RectTransform));

            // Its own canvas, above everything else. No GraphicRaycaster: the
            // overlay is never a hit target.
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32000;

            go.AddComponent<GazeSpectatorView>();

            Place(go, command, "Create Gaze Spectator View");
        }

        private static void Place(GameObject go, MenuCommand command, string undoName)
        {
            GameObjectUtility.SetParentAndAlign(go, command.context as GameObject);
            Undo.RegisterCreatedObjectUndo(go, undoName);
            Selection.activeGameObject = go;
        }
    }
}

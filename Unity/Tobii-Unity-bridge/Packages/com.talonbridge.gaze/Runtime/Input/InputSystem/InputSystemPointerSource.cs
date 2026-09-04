using TalonBridge.Gaze;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TalonBridge.Gaze.InputSystemSupport
{
    /// <summary>
    /// Supplies <see cref="PointerInput"/> with mouse positions from the Input
    /// System package. Compiled only when that package is present.
    /// </summary>
    internal static class InputSystemPointerSource
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Install()
        {
            PointerInput.SetSource(TryGetPosition, "Input System");
        }

        private static bool TryGetPosition(out Vector2 position)
        {
            position = Vector2.zero;

            Pointer pointer = Mouse.current;
            if (pointer == null || !pointer.added)
            {
                pointer = Pointer.current;
            }

            if (pointer == null || !pointer.added)
            {
                PointerInput.ReportStatus("no pointer device");
                return false;
            }

            // While the Game view is unfocused, the Input System resets pointer
            // devices to zero and disables them, so their position would
            // otherwise read as a stale or origin position.
            if (!pointer.enabled)
            {
                PointerInput.ReportStatus(
                    "pointer input paused: the Game view does not have focus");
                return false;
            }

            position = pointer.position.ReadValue();
            PointerInput.ReportStatus(string.Empty);
            return true;
        }
    }
}

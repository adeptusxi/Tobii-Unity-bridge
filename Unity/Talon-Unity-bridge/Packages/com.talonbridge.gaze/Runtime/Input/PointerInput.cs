using UnityEngine;

namespace TalonBridge.Gaze
{
    /// <summary>
    /// Pointer position in Unity screen pixels, read through whichever input
    /// backend the project uses.
    ///
    /// The Input System path lives in the optional
    /// TalonBridge.Gaze.InputSystem assembly, which compiles only when
    /// com.unity.inputsystem is installed and installs itself here at startup.
    /// The legacy Input Manager is used otherwise.
    /// </summary>
    public static class PointerInput
    {
        /// <summary>
        /// Reads the pointer position. Returns false when no pointer is
        /// available, in which case <see cref="Status"/> explains why.
        /// </summary>
        public delegate bool PositionSource(out Vector2 position);

#if ENABLE_LEGACY_INPUT_MANAGER
        private const string DefaultSourceName = "Input Manager";
#else
        private const string DefaultSourceName = "none";
#endif

        private static PositionSource source;

        /// <summary>Name of the active backend, for diagnostics.</summary>
        public static string SourceName { get; private set; } = DefaultSourceName;

        /// <summary>
        /// Why the last read failed, or an empty string when the pointer is
        /// being read normally.
        /// </summary>
        public static string Status { get; private set; } = string.Empty;

        /// <summary>Installs the backend used by <see cref="TryGetPosition"/>.</summary>
        public static void SetSource(PositionSource positionSource, string sourceName)
        {
            source = positionSource;
            SourceName = positionSource != null ? sourceName : DefaultSourceName;
        }

        /// <summary>Records why the pointer is unavailable, for diagnostics.</summary>
        public static void ReportStatus(string status)
        {
            Status = status ?? string.Empty;
        }

        public static bool TryGetPosition(out Vector2 position)
        {
            if (source != null)
            {
                return source(out position);
            }

#if ENABLE_LEGACY_INPUT_MANAGER
            Vector3 mouse = UnityEngine.Input.mousePosition;
            position = new Vector2(mouse.x, mouse.y);
            ReportStatus(string.Empty);
            return true;
#else
            position = Vector2.zero;
            ReportStatus("no input backend: the Input System package is not installed");
            return false;
#endif
        }
    }
}

using System;
using System.Reflection;
using TalonBridge.Gaze.Udp;
using UnityEditor;
using UnityEngine;

namespace TalonBridge.Gaze.Editor
{
    /// <summary>
    /// Resolves the screen rect of the editor's Game view for
    /// <see cref="GazeCoordinateSource.GameView"/>.
    ///
    /// The bridge can only see the Unity window as a whole, so gaze mapped
    /// against it is offset by wherever the Game view sits inside the editor
    /// layout. `UnityEditor.GameView` is internal, so its rendered-area rect is
    /// read reflectively: `targetInParent` is the rendered target in
    /// window-local points, already accounting for the view's toolbar, zoom and
    /// letterboxing, and `EditorWindow.position` places that window on the
    /// desktop. Both are in points, the same units the bridge reports.
    ///
    /// If a future Unity renames those members, resolution fails and the
    /// coordinate source falls back to window- or screen-relative gaze rather
    /// than breaking.
    /// </summary>
    [InitializeOnLoad]
    public static class GameViewRectResolver
    {
        /// <summary>Re-reading every frame is wasteful; the layout rarely moves.</summary>
        private const double CacheSeconds = 0.2d;

        private static Type gameViewType;
        private static PropertyInfo targetInParentProperty;
        private static PropertyInfo viewInWindowProperty;
        private static MethodInfo getMainPlayModeView;
        private static bool reflectionResolved;

        private static Rect cachedRect;
        private static bool cachedValid;
        private static double cacheTime = double.NegativeInfinity;

        static GameViewRectResolver()
        {
            GazeCoordinateMapper.GameViewRectResolver = TryGetRect;
        }

        /// <summary>Whether the internal members this depends on were found.</summary>
        public static bool ReflectionAvailable
        {
            get
            {
                EnsureReflection();
                return gameViewType != null && targetInParentProperty != null;
            }
        }

        public static bool TryGetRect(out Rect rect)
        {
            double now = EditorApplication.timeSinceStartup;
            if (now - cacheTime < CacheSeconds)
            {
                rect = cachedRect;
                return cachedValid;
            }

            cacheTime = now;
            cachedValid = Resolve(out cachedRect);
            rect = cachedRect;
            return cachedValid;
        }

        private static bool Resolve(out Rect rect)
        {
            rect = default;
            EnsureReflection();

            EditorWindow view = FindGameView();
            if (view == null)
            {
                return false;
            }

            Rect window = view.position;
            if (!TryReadRect(targetInParentProperty, view, out Rect target) &&
                !TryReadRect(viewInWindowProperty, view, out target))
            {
                // Last resort: the whole window, including its toolbar.
                target = new Rect(0f, 0f, window.width, window.height);
            }

            if (target.width <= 0f || target.height <= 0f)
            {
                return false;
            }

            rect = new Rect(
                window.x + target.x,
                window.y + target.y,
                target.width,
                target.height);
            return true;
        }

        private static bool TryReadRect(PropertyInfo property, EditorWindow view, out Rect rect)
        {
            rect = default;
            if (property == null)
            {
                return false;
            }

            try
            {
                rect = (Rect)property.GetValue(view);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static EditorWindow FindGameView()
        {
            if (getMainPlayModeView != null)
            {
                try
                {
                    if (getMainPlayModeView.Invoke(null, null) is EditorWindow main)
                    {
                        return main;
                    }
                }
                catch (Exception)
                {
                    // Fall through to the search below.
                }
            }

            if (gameViewType == null)
            {
                return null;
            }

            UnityEngine.Object[] views = Resources.FindObjectsOfTypeAll(gameViewType);
            return views.Length > 0 ? views[0] as EditorWindow : null;
        }

        private static void EnsureReflection()
        {
            if (reflectionResolved)
            {
                return;
            }

            reflectionResolved = true;

            Assembly editorAssembly = typeof(EditorWindow).Assembly;
            gameViewType = editorAssembly.GetType("UnityEditor.GameView");
            if (gameViewType == null)
            {
                return;
            }

            const BindingFlags instanceMembers =
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
            targetInParentProperty = gameViewType.GetProperty("targetInParent", instanceMembers);
            viewInWindowProperty = gameViewType.GetProperty("viewInWindow", instanceMembers);

            Type playModeViewType = editorAssembly.GetType("UnityEditor.PlayModeView");
            getMainPlayModeView = playModeViewType?.GetMethod(
                "GetMainPlayModeView",
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        }
    }
}

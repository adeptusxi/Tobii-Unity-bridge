using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TalonBridge.Gaze
{
    /// <summary>
    /// Shows viewers of recorded gameplay where the player is looking: a cursor
    /// at the gaze point and an optional trail of where it has been.
    ///
    /// Put it on a Canvas. It draws in the Game view, so a Game view or screen
    /// recording captures it. It never receives raycasts, so it does not block
    /// gaze or mouse interaction with the UI beneath it.
    ///
    /// For diagnosing the gaze pipeline use <see cref="GazeDebugCursor"/>.
    /// </summary>
    [AddComponentMenu("Talon Gaze Bridge/Gaze Spectator View")]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class GazeSpectatorView : MaskableGraphic
    {
        private const int DiscSegments = 32;
        private const int RingSegments = 64;

        // Width in pixels of the transparent edge that anti-aliases each shape.
        private const float Feather = 1f;

        [Header("Source")]
        [Tooltip("Provider to show. Leave empty to follow the active provider.")]
        [SerializeField]
        private GazeProviderBase provider = null;

        [Tooltip("Show gaze as received, before any filter on the provider.")]
        [SerializeField]
        private bool showRawGaze = false;

        [Tooltip("Show the overlay. Off draws nothing but keeps tracking the trail.")]
        [SerializeField]
        private bool visible = true;

        [Tooltip("Hide the cursor while gaze is invalid. Off leaves it at the " +
                 "last valid position.")]
        [SerializeField]
        private bool hideWhenInvalid = true;

        [Header("Cursor")]
        [SerializeField]
        private Color cursorColor = Color.white;

        [SerializeField]
        private bool showDot = true;

        [Tooltip("Dot radius in pixels.")]
        [SerializeField]
        [Min(0.5f)]
        private float dotRadius = 6f;

        [SerializeField]
        private bool showCross = false;

        [Tooltip("Length of each arm of the cross from the centre, in pixels.")]
        [SerializeField]
        [Min(1f)]
        private float crossSize = 14f;

        [Tooltip("Draw a circle outline at the gaze radius.")]
        [SerializeField]
        private bool showRadiusRing = false;

        [Tooltip("Ring radius in pixels, used when there is no pointer below or " +
                 "its Region Radius is 0.")]
        [SerializeField]
        [Min(1f)]
        private float ringRadius = 60f;

        [Tooltip("Pointer whose Region Radius sets the ring size. Leave empty " +
                 "to use one found in the scene.")]
        [SerializeField]
        private GazePointer pointer = null;

        [Tooltip("Line thickness of the cross and ring, in pixels.")]
        [SerializeField]
        [Min(0.5f)]
        private float lineThickness = 2f;

        [Header("Outline")]
        [Tooltip("Outline the cursor and trail so they read on any background.")]
        [SerializeField]
        private bool showOutline = false;

        [SerializeField]
        private Color outlineColor = new Color(0f, 0f, 0f, 0.8f);

        [Tooltip("Outline width in pixels.")]
        [SerializeField]
        [Min(0.5f)]
        private float outlineWidth = 1.5f;

        [Header("Trail")]
        [SerializeField]
        private bool showTrail = true;

        [SerializeField]
        private Color trailColor = new Color(1f, 1f, 1f, 0.6f);

        [Tooltip("Most line segments shown. The oldest is dropped when full.")]
        [SerializeField]
        [Range(1, 1024)]
        private int maxSegments = 64;

        [Tooltip("Seconds before a trail point disappears. 0 keeps points " +
                 "until Max Segments pushes them out.")]
        [SerializeField]
        [Min(0f)]
        private float maxAge = 0f;

        [Tooltip("Distance in pixels gaze must move before a point is added.")]
        [SerializeField]
        [Min(0.5f)]
        private float minSpacing = 4f;

        [Tooltip("Trail width in pixels.")]
        [SerializeField]
        [Min(0.5f)]
        private float trailWidth = 3f;

        [Tooltip("Fade the trail out toward its oldest end.")]
        [SerializeField]
        private bool fadeTail = true;

        private readonly List<Vector2> strokePoints = new List<Vector2>();
        private readonly List<float> strokeAlphas = new List<float>();

        private GazeTrail trail;
        private Vector2 cursorNormalized;
        private bool hasCursor;
        private bool cursorShown;
        private bool gazeValid;
        private bool drewLastFrame;

        // Screen pixels to local space, refreshed each time the mesh is built.
        private Vector2 localOrigin;
        private Vector2 localX;
        private Vector2 localY;
        private float unitsPerPixel = 1f;

        public GazeProviderBase Provider
        {
            get => provider;
            set => provider = value;
        }

        public bool ShowRawGaze
        {
            get => showRawGaze;
            set => showRawGaze = value;
        }

        public bool Visible
        {
            get => visible;
            set => visible = value;
        }

        public bool HideWhenInvalid
        {
            get => hideWhenInvalid;
            set => hideWhenInvalid = value;
        }

        public Color CursorColor
        {
            get => cursorColor;
            set => cursorColor = value;
        }

        public bool ShowDot
        {
            get => showDot;
            set => showDot = value;
        }

        public float DotRadius
        {
            get => dotRadius;
            set => dotRadius = Mathf.Max(0.5f, value);
        }

        public bool ShowCross
        {
            get => showCross;
            set => showCross = value;
        }

        public float CrossSize
        {
            get => crossSize;
            set => crossSize = Mathf.Max(1f, value);
        }

        public bool ShowRadiusRing
        {
            get => showRadiusRing;
            set => showRadiusRing = value;
        }

        public float RingRadius
        {
            get => ringRadius;
            set => ringRadius = Mathf.Max(1f, value);
        }

        public GazePointer Pointer
        {
            get => pointer;
            set => pointer = value;
        }

        public float LineThickness
        {
            get => lineThickness;
            set => lineThickness = Mathf.Max(0.5f, value);
        }

        public bool ShowOutline
        {
            get => showOutline;
            set => showOutline = value;
        }

        public Color OutlineColor
        {
            get => outlineColor;
            set => outlineColor = value;
        }

        public float OutlineWidth
        {
            get => outlineWidth;
            set => outlineWidth = Mathf.Max(0.5f, value);
        }

        public bool ShowTrail
        {
            get => showTrail;
            set => showTrail = value;
        }

        public Color TrailColor
        {
            get => trailColor;
            set => trailColor = value;
        }

        public int MaxSegments
        {
            get => maxSegments;
            set => maxSegments = Mathf.Clamp(value, 1, 1024);
        }

        public float MaxAge
        {
            get => maxAge;
            set => maxAge = Mathf.Max(0f, value);
        }

        public float MinSpacing
        {
            get => minSpacing;
            set => minSpacing = Mathf.Max(0.5f, value);
        }

        public float TrailWidth
        {
            get => trailWidth;
            set => trailWidth = Mathf.Max(0.5f, value);
        }

        public bool FadeTail
        {
            get => fadeTail;
            set => fadeTail = value;
        }

        /// <summary>The ring radius being drawn: the pointer's, or Ring Radius.</summary>
        public float EffectiveRingRadius =>
            pointer != null && pointer.RegionRadius > 0f ? pointer.RegionRadius : ringRadius;

        /// <summary>The trail being drawn.</summary>
        public GazeTrail Trail => trail ??= new GazeTrail(maxSegments);

        /// <summary>Where the cursor is, in normalized coordinates.</summary>
        public Vector2 CursorNormalized => cursorNormalized;

        /// <summary>True while the cursor is being drawn.</summary>
        public bool IsCursorShown => cursorShown && visible;

        private IGazeProvider Source => provider != null ? provider : GazeService.Active;

        protected override void OnEnable()
        {
            base.OnEnable();

            // The overlay must never take a hit meant for the UI beneath it.
            raycastTarget = false;

            if (pointer == null && Application.isPlaying)
            {
                pointer = FindAnyObjectByType<GazePointer>();
            }

            Trail.Clear();
            hasCursor = false;
            cursorShown = false;
            gazeValid = false;
        }

        private void LateUpdate()
        {
            // Graphic runs in edit mode; the overlay only tracks gaze in play.
            if (!Application.isPlaying)
            {
                return;
            }

            IGazeProvider source = Source;
            gazeValid = source != null && source.IsValid;

            GazeTrail points = Trail;
            points.MaxSegments = maxSegments;

            if (gazeValid)
            {
                if (TryReadPosition(source, out Vector2 position) || !hasCursor)
                {
                    cursorNormalized = position;
                    hasCursor = true;
                }

                if (showTrail && ShouldAddPoint(points))
                {
                    points.Add(cursorNormalized, Time.unscaledTime);
                }
            }
            else
            {
                points.Break();
            }

            if (!showTrail)
            {
                points.Clear();
            }

            points.Trim(Time.unscaledTime, maxAge);

            cursorShown = hasCursor && (gazeValid || !hideWhenInvalid);

            bool draws = visible && (cursorShown || points.Count > 0);
            if (draws || drewLastFrame)
            {
                SetVerticesDirty();
            }

            drewLastFrame = draws;
        }

        /// <summary>
        /// The position to show. False when raw gaze is wanted but the newest
        /// raw sample is invalid, so the cursor holds its place.
        /// </summary>
        private bool TryReadPosition(IGazeProvider source, out Vector2 position)
        {
            position = source.NormalizedPosition;

            if (showRawGaze && source is GazeProviderBase withRaw)
            {
                GazeSample raw = withRaw.RawSample;
                if (!raw.IsValid)
                {
                    return false;
                }

                position = raw.Normalized;
            }

            return true;
        }

        private bool ShouldAddPoint(GazeTrail points)
        {
            if (points.BreakPending || !points.TryGetNewest(out GazeTrail.Point newest))
            {
                return true;
            }

            Vector2 moved = GazeCoordinates.NormalizedToScreen(cursorNormalized)
                - GazeCoordinates.NormalizedToScreen(newest.Position);
            return moved.sqrMagnitude >= minSpacing * minSpacing;
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            if (!visible || !Application.isPlaying || trail == null)
            {
                return;
            }

            RefreshScreenMapping();

            float outline = showOutline ? outlineWidth : 0f;

            if (showTrail && trail.Count > 0)
            {
                if (outline > 0f)
                {
                    AddTrail(vh, (trailWidth * 0.5f) + outline, outlineColor);
                }

                AddTrail(vh, trailWidth * 0.5f, trailColor);
            }

            if (!cursorShown)
            {
                return;
            }

            Vector2 centre = GazeCoordinates.NormalizedToScreen(cursorNormalized);

            if (outline > 0f)
            {
                AddCursor(vh, centre, outline, outlineColor);
            }

            AddCursor(vh, centre, 0f, cursorColor);
        }

        private void AddCursor(VertexHelper vh, Vector2 centre, float grow, Color tint)
        {
            float halfLine = (lineThickness * 0.5f) + grow;

            if (showRadiusRing)
            {
                float radius = EffectiveRingRadius;
                BeginStroke();
                for (int i = 0; i < RingSegments; i++)
                {
                    float angle = i * (2f * Mathf.PI / RingSegments);
                    AddStrokePoint(
                        centre + (new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius), 1f);
                }

                EndStroke(vh, halfLine, tint, closed: true);
            }

            if (showCross)
            {
                // Arms grow lengthwise too, so the outline caps their ends.
                float arm = crossSize + grow;

                BeginStroke();
                AddStrokePoint(centre + new Vector2(-arm, 0f), 1f);
                AddStrokePoint(centre + new Vector2(arm, 0f), 1f);
                EndStroke(vh, halfLine, tint, closed: false);

                BeginStroke();
                AddStrokePoint(centre + new Vector2(0f, -arm), 1f);
                AddStrokePoint(centre + new Vector2(0f, arm), 1f);
                EndStroke(vh, halfLine, tint, closed: false);
            }

            if (showDot)
            {
                AddDisc(vh, centre, dotRadius + grow, tint);
            }
        }

        private void AddTrail(VertexHelper vh, float halfWidth, Color tint)
        {
            int count = trail.Count;
            float now = Time.unscaledTime;

            BeginStroke();
            for (int i = 0; i < count; i++)
            {
                GazeTrail.Point point = trail[i];
                if (point.StartsStroke)
                {
                    EndStroke(vh, halfWidth, tint, closed: false);
                    BeginStroke();
                }

                float alpha = 1f;
                if (fadeTail)
                {
                    alpha = (i + 1f) / (count + 1f);
                }

                if (maxAge > 0f)
                {
                    alpha = Mathf.Min(alpha, 1f - Mathf.Clamp01((now - point.Time) / maxAge));
                }

                AddStrokePoint(GazeCoordinates.NormalizedToScreen(point.Position), alpha);
            }

            // Join the newest stroke to the cursor, unless tracking has dropped
            // since its last point.
            if (gazeValid && !trail.BreakPending)
            {
                AddStrokePoint(GazeCoordinates.NormalizedToScreen(cursorNormalized), 1f);
            }

            EndStroke(vh, halfWidth, tint, closed: false);
        }

        // -- mesh helpers ------------------------------------------------------

        /// <summary>
        /// Works out how screen pixels map into this graphic's local space. The
        /// mapping is affine for screen-space canvases, so three points give it.
        /// </summary>
        private void RefreshScreenMapping()
        {
            Canvas owner = canvas;
            Camera camera = owner != null && owner.renderMode != RenderMode.ScreenSpaceOverlay
                ? owner.worldCamera
                : null;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rectTransform, Vector2.zero, camera, out localOrigin);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rectTransform, Vector2.right, camera, out Vector2 right);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rectTransform, Vector2.up, camera, out Vector2 up);

            localX = right - localOrigin;
            localY = up - localOrigin;
            unitsPerPixel = localX.magnitude;
        }

        private Vector2 ToLocal(Vector2 screen)
        {
            return localOrigin + (localX * screen.x) + (localY * screen.y);
        }

        private Color32 Tinted(Color tint, float alpha)
        {
            Color result = tint * color;
            result.a *= alpha;
            return result;
        }

        private void BeginStroke()
        {
            strokePoints.Clear();
            strokeAlphas.Clear();
        }

        private void AddStrokePoint(Vector2 screen, float alpha)
        {
            strokePoints.Add(ToLocal(screen));
            strokeAlphas.Add(alpha);
        }

        /// <summary>
        /// Emits the collected points as a line of the given half width in
        /// pixels. Each point becomes four vertices across the line: the two
        /// inner ones carry the colour and the outer two fade it out.
        /// </summary>
        private void EndStroke(VertexHelper vh, float halfWidth, Color tint, bool closed)
        {
            int count = strokePoints.Count;
            if (count < 2)
            {
                return;
            }

            float inner = halfWidth * unitsPerPixel;
            float outer = (halfWidth + Feather) * unitsPerPixel;
            int first = vh.currentVertCount;

            for (int i = 0; i < count; i++)
            {
                Vector2 point = strokePoints[i];
                Vector2 normal = StrokeNormal(i, count, closed);

                Color32 solid = Tinted(tint, strokeAlphas[i]);
                Color32 clear = solid;
                clear.a = 0;

                vh.AddVert(point - (normal * outer), clear, Vector2.zero);
                vh.AddVert(point - (normal * inner), solid, Vector2.zero);
                vh.AddVert(point + (normal * inner), solid, Vector2.zero);
                vh.AddVert(point + (normal * outer), clear, Vector2.zero);
            }

            int joins = closed ? count : count - 1;
            for (int i = 0; i < joins; i++)
            {
                int a = first + (i * 4);
                int b = first + (((i + 1) % count) * 4);

                for (int lane = 0; lane < 3; lane++)
                {
                    vh.AddTriangle(a + lane, b + lane, b + lane + 1);
                    vh.AddTriangle(a + lane, b + lane + 1, a + lane + 1);
                }
            }
        }

        /// <summary>
        /// Direction across the line at a point: perpendicular to the average
        /// of the segments either side, lengthened at corners so the line keeps
        /// its width.
        /// </summary>
        private Vector2 StrokeNormal(int index, int count, bool closed)
        {
            Vector2 point = strokePoints[index];

            bool hasPrevious = closed || index > 0;
            bool hasNext = closed || index < count - 1;

            Vector2 before = hasPrevious
                ? (point - strokePoints[(index + count - 1) % count]).normalized
                : Vector2.zero;
            Vector2 after = hasNext
                ? (strokePoints[(index + 1) % count] - point).normalized
                : Vector2.zero;

            if (!hasPrevious)
            {
                before = after;
            }

            if (!hasNext)
            {
                after = before;
            }

            Vector2 direction = before + after;
            if (direction.sqrMagnitude < 0.0001f)
            {
                // The line doubles back on itself.
                return new Vector2(-after.y, after.x);
            }

            direction.Normalize();
            var normal = new Vector2(-direction.y, direction.x);

            float cosHalfAngle = Vector2.Dot(normal, new Vector2(-after.y, after.x));
            return normal / Mathf.Max(cosHalfAngle, 0.5f);
        }

        private void AddDisc(VertexHelper vh, Vector2 centre, float radius, Color tint)
        {
            Color32 solid = Tinted(tint, 1f);
            Color32 clear = solid;
            clear.a = 0;

            Vector2 middle = ToLocal(centre);
            float inner = radius * unitsPerPixel;
            float outer = (radius + Feather) * unitsPerPixel;

            int first = vh.currentVertCount;
            vh.AddVert(middle, solid, Vector2.zero);

            for (int i = 0; i < DiscSegments; i++)
            {
                float angle = i * (2f * Mathf.PI / DiscSegments);
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                vh.AddVert(middle + (direction * inner), solid, Vector2.zero);
                vh.AddVert(middle + (direction * outer), clear, Vector2.zero);
            }

            for (int i = 0; i < DiscSegments; i++)
            {
                int a = first + 1 + (i * 2);
                int b = first + 1 + (((i + 1) % DiscSegments) * 2);

                vh.AddTriangle(first, a, b);
                vh.AddTriangle(a, a + 1, b + 1);
                vh.AddTriangle(a, b + 1, b);
            }
        }
    }
}

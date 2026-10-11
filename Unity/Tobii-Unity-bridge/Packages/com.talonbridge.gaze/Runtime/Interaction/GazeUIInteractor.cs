using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace TalonBridge.Gaze
{
    /// <summary>
    /// Hit-tests gaze against Unity UI through the EventSystem's raycasters.
    ///
    /// The raycast is built by hand rather than routed through the active input module, so gaze doesn't conflict with mouse.
    /// A search radius is served by sampling a ring of extra raycasts.
    /// </summary>
    [AddComponentMenu("Talon Gaze Bridge/Gaze UI Interactor")]
    public sealed class GazeUIInteractor : MonoBehaviour, IGazeHitSource
    {
        [Tooltip("Raycasts placed around the gaze point when a search radius is " +
                 "set. More samples find small targets more reliably and cost " +
                 "one raycast each.")]
        [SerializeField]
        [Range(4, 32)]
        private int regionSamples = 8;

        [Tooltip("Also sample a ring at half the radius, which helps when " +
                 "targets are smaller than the radius.")]
        [SerializeField]
        private bool innerRing = true;

        private readonly List<RaycastResult> raycastResults = new List<RaycastResult>();
        private readonly List<GazeInteractable> found = new List<GazeInteractable>();
        private readonly List<float> distances = new List<float>();

        private PointerEventData pointerData;
        private EventSystem cachedEventSystem;
        private bool warnedNoEventSystem;

        public int RegionSamples
        {
            get => regionSamples;
            set => regionSamples = Mathf.Clamp(value, 4, 32);
        }

        public bool InnerRing
        {
            get => innerRing;
            set => innerRing = value;
        }

        public void Collect(
            Vector2 screenPosition,
            float radius,
            bool collectAll,
            List<GazeInteractable> results)
        {
            results.Clear();

            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                if (!warnedNoEventSystem)
                {
                    warnedNoEventSystem = true;
                    Debug.LogWarning(
                        "GazeUIInteractor needs an EventSystem in the scene to " +
                        "raycast against UI. Gaze targets will not be found.",
                        this);
                }

                return;
            }

            warnedNoEventSystem = false;
            EnsurePointerData(eventSystem);

            GazeInteractable centre = RaycastAt(eventSystem, screenPosition);

            if (!collectAll)
            {
                // The exact gaze point wins, so a radius can never steal focus
                // from whatever is actually being looked at.
                if (centre != null)
                {
                    results.Add(centre);
                    return;
                }

                if (radius > 0f)
                {
                    GazeInteractable nearest = SampleRing(
                        eventSystem, screenPosition, radius, false);
                    if (nearest != null)
                    {
                        results.Add(nearest);
                    }
                }

                return;
            }

            found.Clear();
            distances.Clear();

            if (centre != null)
            {
                Add(centre, screenPosition);
            }

            if (radius > 0f)
            {
                SampleRing(eventSystem, screenPosition, radius, true);
            }

            for (int i = 0; i < found.Count; i++)
            {
                results.Add(found[i]);
            }
        }

        /// <summary>
        /// Raycasts a ring of points around the gaze position. In collect-all
        /// mode every hit is accumulated; otherwise the nearest hit is
        /// returned.
        /// </summary>
        private GazeInteractable SampleRing(
            EventSystem eventSystem,
            Vector2 centre,
            float radius,
            bool collectAll)
        {
            GazeInteractable nearest = null;
            float nearestDistance = float.PositiveInfinity;

            int rings = innerRing ? 2 : 1;
            for (int ring = 0; ring < rings; ring++)
            {
                float ringRadius = ring == 0 ? radius : radius * 0.5f;

                for (int i = 0; i < regionSamples; i++)
                {
                    float angle = (Mathf.PI * 2f * i) / regionSamples;
                    var point = new Vector2(
                        centre.x + Mathf.Cos(angle) * ringRadius,
                        centre.y + Mathf.Sin(angle) * ringRadius);

                    GazeInteractable hit = RaycastAt(eventSystem, point);
                    if (hit == null)
                    {
                        continue;
                    }

                    if (collectAll)
                    {
                        Add(hit, centre);
                        continue;
                    }

                    float distance = DistanceTo(hit, centre);
                    if (distance < nearestDistance)
                    {
                        nearestDistance = distance;
                        nearest = hit;
                    }
                }
            }

            return nearest;
        }

        private GazeInteractable RaycastAt(EventSystem eventSystem, Vector2 screenPosition)
        {
            pointerData.Reset();
            pointerData.position = screenPosition;

            raycastResults.Clear();
            eventSystem.RaycastAll(pointerData, raycastResults);

            for (int i = 0; i < raycastResults.Count; i++)
            {
                GameObject go = raycastResults[i].gameObject;
                if (go == null)
                {
                    continue;
                }

                var interactable = go.GetComponentInParent<GazeInteractable>();
                if (interactable != null && interactable.CanInteract)
                {
                    return interactable;
                }
            }

            return null;
        }

        /// <summary>Adds a hit in distance order, skipping duplicates.</summary>
        private void Add(GazeInteractable interactable, Vector2 gazePoint)
        {
            if (found.Contains(interactable))
            {
                return;
            }

            float distance = DistanceTo(interactable, gazePoint);

            int index = 0;
            while (index < distances.Count && distances[index] <= distance)
            {
                index++;
            }

            found.Insert(index, interactable);
            distances.Insert(index, distance);
        }

        /// <summary>
        /// Screen distance from the gaze point to the element's centre. Used
        /// only for ranking, so the rect's centre is precise enough.
        /// </summary>
        private static float DistanceTo(GazeInteractable interactable, Vector2 gazePoint)
        {
            RectTransform rect = interactable.RectTransform;
            if (rect == null)
            {
                return float.PositiveInfinity;
            }

            Vector3 worldCentre = rect.TransformPoint(rect.rect.center);

            Canvas canvas = interactable.Canvas;
            Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;

            Vector2 screenCentre = RectTransformUtility.WorldToScreenPoint(camera, worldCentre);
            return Vector2.Distance(screenCentre, gazePoint);
        }

        private void EnsurePointerData(EventSystem eventSystem)
        {
            if (pointerData == null || cachedEventSystem != eventSystem)
            {
                cachedEventSystem = eventSystem;
                pointerData = new PointerEventData(eventSystem);
            }
        }
    }
}

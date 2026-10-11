namespace TalonBridge.Gaze
{
    /// <summary>
    /// Visual feedback for a gazed-at <see cref="GazeInteractable"/>: e.g. highlight, scale, radial fill, outline
    /// </summary>
    public interface IGazeFeedback
    {
        /// <summary>Gaze arrived on the target.</summary>
        void OnGazeEnter(GazeInteractable interactable);

        /// <summary>Dwell progress, 0 at the start and 1 at activation.</summary>
        void OnDwellProgress(GazeInteractable interactable, float progress);

        /// <summary>Gaze left the target, or gaze became invalid.</summary>
        void OnGazeExit(GazeInteractable interactable);

        /// <summary>The dwell completed and the target fired.</summary>
        void OnActivated(GazeInteractable interactable);
    }
}

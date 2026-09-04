# UI interaction sample

Three interchangeable `IGazeFeedback` components. Each one presents dwell
progress differently and none of them contains detection logic, so swapping the
component on a `GazeInteractable` changes only how the target looks.

| Component | Shows dwell as |
| --- | --- |
| `RadialFillGazeFeedback` | A radial Image filling from 0 to 1 |
| `ScaleGazeFeedback` | The target growing, with a pop on activation |
| `OutlineGazeFeedback` | An outline graphic fading in |

## Using one

Add it to the same GameObject as the `GazeInteractable`, alongside whatever the
component needs:

- **Radial fill** — an Image with Image Type `Filled` and Fill Method
  `Radial 360`, assigned to `Fill`.
- **Scale** — nothing; it scales its own transform unless `Target` is set.
- **Outline** — a Graphic to show, assigned to `Outline`. A second Image behind
  the element works well.

`GazeInteractable` drives every `IGazeFeedback` on its GameObject, so more than
one can run at once.

## Writing your own

Implement `IGazeFeedback` and put it on the interactable:

```csharp
public sealed class MyGazeFeedback : MonoBehaviour, IGazeFeedback
{
    public void OnGazeEnter(GazeInteractable interactable) { }
    public void OnDwellProgress(GazeInteractable interactable, float progress) { }
    public void OnGazeExit(GazeInteractable interactable) { }
    public void OnActivated(GazeInteractable interactable) { }
}
```

`progress` runs from 0 to 1. `OnGazeExit` fires when gaze leaves the target and
also when gaze becomes invalid, so reset any visual state there.

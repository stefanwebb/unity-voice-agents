# RandomTumble — Design

Date: 2026-06-24

## Goal

A `MonoBehaviour` you can attach to any GameObject (e.g. a cube) that continuously tumbles it toward new random orientations.

## Background

This is a standalone, generic component — no dependency on `EventBus`, `SttClient`, or any other part of this project.

## Decisions

- **Animation style**: tumble toward new random orientations (`Quaternion.Slerp` from current to a freshly-picked `Random.rotation`), not a fixed-axis spin or per-frame jitter.
- **Timing**: fixed duration per tumble (seconds, one serialized field). Speed is derived each tumble so every tumble takes the same wall-clock time regardless of how far it has to rotate.
- **Pacing**: continuous — as soon as one tumble reaches its target, immediately pick a new random target and start the next tumble. No pause between tumbles.
- **Snapping**: on reaching `t >= 1`, snap exactly to the target rotation (avoids floating-point drift accumulating over many tumbles) before picking the next target.

## Architecture

New file: `Assets/Scripts/RandomTumble.cs`

```csharp
public class RandomTumble : MonoBehaviour
{
    [SerializeField] private float _tumbleDuration = 2f;

    private Quaternion _startRotation;
    private Quaternion _targetRotation;
    private float _tumbleStartTime;

    private void Start()
    {
        _startRotation = transform.rotation;
        PickNewTarget();
    }

    private void Update()
    {
        var t = (Time.time - _tumbleStartTime) / _tumbleDuration;
        if (t >= 1f)
        {
            transform.rotation = _targetRotation;
            PickNewTarget();
            return;
        }

        transform.rotation = Quaternion.Slerp(_startRotation, _targetRotation, t);
    }

    private void PickNewTarget()
    {
        _startRotation = transform.rotation;
        _targetRotation = Random.rotation;
        _tumbleStartTime = Time.time;
    }
}
```

`Random.rotation` (Unity built-in) returns a uniformly-distributed random `Quaternion` — no manual axis/angle construction needed.

## Data Flow

```
Start() -> record current rotation, pick first random target, record start time
Update() each frame -> t = elapsed / duration
    t < 1  -> transform.rotation = Slerp(start, target, t)
    t >= 1 -> snap to target, pick new target, restart timer
```

## Error Handling

None needed — no external dependencies, no failure modes beyond a misconfigured `_tumbleDuration <= 0` (would make `t` jump to `>= 1` on the very next frame, just snapping immediately and picking a new target every frame — not a crash, just a degenerate "very fast" case).

## Testing

Deferred, consistent with the rest of this project. Manual verification: attach to a cube in a scene, enter Play mode, and confirm it continuously tumbles between random orientations at the configured duration.

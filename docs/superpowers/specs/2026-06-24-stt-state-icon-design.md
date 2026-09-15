# STT State Icon — Design

Date: 2026-06-24

## Goal

A `MonoBehaviour` attached to a `RawImage` that swaps its texture to reflect the STT server's connection status and last-known `state_changed` value, resizing the `RawImage` to each icon's native size.

## Background

- `SttClient` no longer exposes any public connection/state property after its `MonoBehaviour` redesign — the only way to learn connection status or server state is by subscribing to `SttConnectionChangedEvent`/`StateChangedEvent` on the project's static `EventBus`.
- `SttState` (`Assets/Scripts/SttEvents.cs`) has 6 values: `Ready, Loading, Listening, Transcribing, Paused, Error`.
- `SttConnectionChangedEvent { bool Connected }` and `StateChangedEvent { SttState State }` are both already raised by `SttClient`.

## Decisions

- **Icon fields** (all `Texture2D`, serialized): `ListeningIcon`, `DisconnectedIcon`, `SpeakingIcon`, `NotReadyIcon`, `ErrorIcon` — 5 fields, every one used by the mapping below. (Earlier drafts included `LoadingIcon`/`ThinkingIcon`; dropped since the final mapping doesn't reference them — no unused fields.)
- **Mapping** (disconnected always wins, checked first regardless of last-known state):

  | Condition | Icon |
  |---|---|
  | Disconnected | `DisconnectedIcon` |
  | `Loading` | `NotReadyIcon` |
  | `Ready` | `ListeningIcon` |
  | `Listening` | `ListeningIcon` |
  | `Transcribing` | `SpeakingIcon` |
  | `Paused` | `NotReadyIcon` |
  | `Error` | `ErrorIcon` |

- **Visibility**: always visible. (An earlier instruction said "hide on Paused" — superseded once Paused got an explicit icon in the mapping above; there is no remaining condition that hides the `RawImage`.)
- **Resizing**: every time the texture changes, set `RawImage.rectTransform.sizeDelta` to the new texture's native `(width, height)` in pixels.
- **State tracking**: the component keeps its own `_connected`/`_state` fields, updated by subscribing to both events independently; either event triggers a re-evaluation of the combined condition (since "disconnected" can arrive independently of a new state, and vice versa).
- **Initial state**: defaults to `_connected = false`, so before the first `SttConnectionChangedEvent` arrives, `DisconnectedIcon` is shown — a reasonable "not connected yet" default.

## Architecture

New file: `Assets/Scripts/SttStateIcon.cs`

```csharp
[RequireComponent(typeof(RawImage))]
public class SttStateIcon : MonoBehaviour
{
    [SerializeField] private Texture2D _listeningIcon;
    [SerializeField] private Texture2D _disconnectedIcon;
    [SerializeField] private Texture2D _speakingIcon;
    [SerializeField] private Texture2D _notReadyIcon;
    [SerializeField] private Texture2D _errorIcon;

    private RawImage _rawImage;
    private bool _connected;
    private SttState _state = SttState.Ready;

    private void Awake() => _rawImage = GetComponent<RawImage>();

    private void OnEnable()
    {
        EventBus.SubscribeTo<SttConnectionChangedEvent>(OnConnectionChanged);
        EventBus.SubscribeTo<StateChangedEvent>(OnStateChanged);
        UpdateIcon();
    }

    private void OnDisable()
    {
        EventBus.UnsubscribeFrom<SttConnectionChangedEvent>(OnConnectionChanged);
        EventBus.UnsubscribeFrom<StateChangedEvent>(OnStateChanged);
    }

    private void OnConnectionChanged(ref SttConnectionChangedEvent e)
    {
        _connected = e.Connected;
        UpdateIcon();
    }

    private void OnStateChanged(ref StateChangedEvent e)
    {
        _state = e.State;
        UpdateIcon();
    }

    private void UpdateIcon()
    {
        var icon = !_connected ? _disconnectedIcon : _state switch
        {
            SttState.Loading => _notReadyIcon,
            SttState.Ready => _listeningIcon,
            SttState.Listening => _listeningIcon,
            SttState.Transcribing => _speakingIcon,
            SttState.Paused => _notReadyIcon,
            SttState.Error => _errorIcon,
            _ => _disconnectedIcon,
        };

        _rawImage.texture = icon;
        if (icon != null)
            _rawImage.rectTransform.sizeDelta = new Vector2(icon.width, icon.height);
    }
}
```

`[RequireComponent(typeof(RawImage))]` documents the dependency and auto-adds the component if missing when first attached in the Editor; `Awake()` caches the reference once.

## Data Flow

```
EventBus: SttConnectionChangedEvent{Connected} -> OnConnectionChanged -> _connected updated -> UpdateIcon()
EventBus: StateChangedEvent{State}             -> OnStateChanged     -> _state updated     -> UpdateIcon()

UpdateIcon():
    not connected?      -> DisconnectedIcon
    else state switch   -> Loading/Paused -> NotReadyIcon
                           Ready/Listening -> ListeningIcon
                           Transcribing    -> SpeakingIcon
                           Error           -> ErrorIcon
    -> RawImage.texture = icon
    -> RawImage.rectTransform.sizeDelta = (icon.width, icon.height)
```

## Error Handling

- If an icon field is left unassigned (`null`) in the Inspector for some condition, `RawImage.texture` is simply set to `null` (Unity renders nothing) and the resize is skipped (guarded by `if (icon != null)`) — no exception, just a blank image, which is the expected/obvious failure mode for an unconfigured icon slot.
- No dependency on `SttClient` itself — this is a pure `EventBus` consumer, same pattern as `SttTranscriptionDisplay.cs`.

## Testing

Deferred, consistent with the rest of this project. Manual verification: assign all 5 icon textures in the Inspector, enter Play mode, and confirm the `RawImage` shows `DisconnectedIcon` before connecting, `ListeningIcon` once ready/listening, `SpeakingIcon` while transcribing, `NotReadyIcon` while loading or paused, and `ErrorIcon` on an error — resizing to each icon's native dimensions every time.

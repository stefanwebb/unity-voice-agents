# STT Transcription Display — Design

Date: 2026-06-24

## Goal

In Play mode, while the STT server is listening, show the spoken transcription live in a TextMeshPro UI text box.

## Background

- `SttEvents.cs` already defines `SpeechStartEvent` (no fields), `SpeechEvent { string Text; AlignedWord[] Words; }` (the *cumulative* running transcript for the current utterance — confirmed by the wire-protocol example in the `named_pipes.stt` spec: each `speech` event's `text` field already contains everything transcribed so far in the utterance, not just the latest fragment), and `SpeechEndEvent` (no fields), all raised on `EventBus` by `SttClient`.
- The project already has TextMeshPro set up (`Assets/TextMesh Pro/`).
- Existing consumer scripts in this project (`TestButton.cs`) follow the pattern: subscribe in `OnEnable`, unsubscribe in `OnDisable`.

## Decisions

- **Component type**: `TMPro.TextMeshProUGUI` (the Canvas/UI text component), not the 3D world-space `TextMeshPro` component.
- **Event source**: `SpeechEvent.Text` directly (already cumulative) — no need to also consume `TokenEvent` (sub-word fragments) since that would duplicate/fragment what `SpeechEvent` already provides in full.
- **Per-utterance behavior**: show only the current utterance. On `SpeechStartEvent`, clear the text box (defensive — handles any lingering text from a prior utterance). On each `SpeechEvent`, overwrite the text box with the running transcript.
- **Idle/end behavior**: empty until the first utterance; after `SpeechEndEvent`, the completed sentence simply stays visible (no clearing) until the next `SpeechStartEvent`. No subscription to `SpeechEndEvent` is needed since there's no behavior to attach to it.
- **Scope**: this script is a pure `EventBus` consumer — no connection-state awareness, no `[ExecuteAlways]` (Play-mode-only `MonoBehaviour`, like any ordinary UI script). It doesn't need to know about `SttClient`, `IsConnected`, or any state — it only reacts to events that, by construction, only fire while the server is actually listening/transcribing.

## Architecture

New file: `Assets/Scripts/SttTranscriptionDisplay.cs`

```csharp
[RequireComponent(typeof(TextMeshProUGUI))]
public class SttTranscriptionDisplay : MonoBehaviour
{
    private TextMeshProUGUI _text;

    private void Awake() => _text = GetComponent<TextMeshProUGUI>();

    private void OnEnable()
    {
        _text.text = "";
        EventBus.SubscribeTo<SpeechStartEvent>(OnSpeechStart);
        EventBus.SubscribeTo<SpeechEvent>(OnSpeech);
    }

    private void OnDisable()
    {
        EventBus.UnsubscribeFrom<SpeechStartEvent>(OnSpeechStart);
        EventBus.UnsubscribeFrom<SpeechEvent>(OnSpeech);
    }

    private void OnSpeechStart(ref SpeechStartEvent e) => _text.text = "";

    private void OnSpeech(ref SpeechEvent e) => _text.text = e.Text;
}
```

`[RequireComponent(typeof(TextMeshProUGUI))]` documents the dependency and has Unity auto-add the component if missing when this script is first attached in the Editor; `Awake()` caches the reference once.

## Data Flow

```
Server detects speech onset -> SpeechStartEvent -> OnSpeechStart -> text box cleared
Server sends each "speech" event (cumulative text) -> SpeechEvent -> OnSpeech -> text box updated
Server detects speech end -> SpeechEndEvent -> (no handler; text box keeps showing the completed utterance)
```

## Error Handling

None needed beyond what `[RequireComponent]` already guarantees (the `TextMeshProUGUI` is present). All inbound events are already main-thread-safe by the time they reach `EventBus` subscribers (per `SttClient`'s existing `Enqueue`/`Update()` drain design) — this script does nothing thread-sensitive itself.

## Testing

Deferred, consistent with the rest of this project. Manual verification: attach this script to a `TextMeshProUGUI` GameObject in a scene that also has `SttClient`, enter Play mode, start listening, speak, and confirm the text box shows the live running transcript and retains the final sentence after you stop speaking.

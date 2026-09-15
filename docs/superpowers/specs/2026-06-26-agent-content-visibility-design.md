# Agent Content Visibility Design

**Date:** 2026-06-26
**Scope:** `PlayerController.cs` (modified only)

## Overview

"Agent Content" starts disabled. It becomes visible when the player confirms STT input, and is dismissed by a second Enter press once the chat server finishes streaming. All logic lives in `PlayerController`, which already owns all confirm-key behavior.

## Changes to `PlayerController.cs`

### New serialized field

```csharp
[SerializeField] private GameObject _agentContent;
```

Wire to the "Agent Content" GameObject in the Inspector.

### New private field

```csharp
private bool _streamingDone;
```

Set to `true` when `ChatDoneEvent` fires; cleared on confirm or dismiss.

### EventBus subscription

Subscribe to `ChatDoneEvent` in `OnEnable`; unsubscribe in `OnDisable`. Symmetric with the existing `SpeechEndEvent` and `SpeechEvent` pairs.

### New handler

```csharp
private void OnChatDone(ref ChatDoneEvent e) => _streamingDone = true;
```

### Updated `OnConfirmInput`

Two branches replace the single existing branch:

```csharp
private void OnConfirmInput(InputAction.CallbackContext ctx)
{
    if (_hasPendingInput)
    {
        _hasPendingInput = false;
        Time.timeScale = 1f;
        var text = _pendingText;
        if (_speechContent != null) _speechContent.SetActive(false);
        if (_agentContent != null) _agentContent.SetActive(true);
        _streamingDone = false;
        EventBus.Raise(new ConfirmedInputEvent { Text = text });
    }
    else if (_streamingDone)
    {
        _streamingDone = false;
        if (_agentContent != null) _agentContent.SetActive(false);
    }
}
```

## State Machine

```
[idle]
  Space          → start STT, show _speechContent
  SpeechEndEvent → _hasPendingInput = true, pause STT

[pending input]
  Enter          → hide _speechContent, show _agentContent
                   _streamingDone = false
                   raise ConfirmedInputEvent
                 → [streaming]

[streaming]
  ChatDoneEvent  → _streamingDone = true
                 → [streaming done]
  Enter          → ignored (_hasPendingInput=false, _streamingDone=false)

[streaming done]
  Enter          → hide _agentContent, _streamingDone = false
                 → [idle]
```

## Guard Rails

- **Enter while streaming:** silently ignored — both `_hasPendingInput` and `_streamingDone` are false.
- **`_agentContent` null:** all `.SetActive` calls are null-guarded, matching the `_speechContent` pattern.
- **`_streamingDone` reset on confirm:** ensures a new input cycle starts clean even if ChatDoneEvent from the previous turn fires late.

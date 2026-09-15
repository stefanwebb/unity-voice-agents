# ConversationManager Design

**Date:** 2026-06-26
**Scope:** `ChatCommands.cs` (modified), `PlayerController.cs` (modified), `ConversationManager.cs` (new)

## Overview

`ConversationManager` is a play-mode `MonoBehaviour` singleton that bridges confirmed STT input to the chat server and maintains conversation history across turns. When the player confirms a transcription, `PlayerController` raises a `ConfirmedInputEvent`; `ConversationManager` appends the user turn, fires `ChatCommandEvent` with the full history, accumulates streaming reply tokens, and on completion appends the assistant turn.

## Files

| File | Change |
|---|---|
| `Assets/Scripts/ChatCommands.cs` | Add `ConfirmedInputEvent { string Text }` |
| `Assets/Scripts/PlayerController.cs` | `OnConfirmInput` raises `ConfirmedInputEvent` instead of logging |
| `Assets/Scripts/ConversationManager.cs` | New MonoBehaviour singleton |

## Data Flow

```
[Space key]         → PlayerController.OnSpeechStart → StartCommandEvent → STT starts
[Speech ends]       → SpeechEndEvent → PlayerController: sets _hasPendingInput, pauses STT
[Enter/confirm key] → PlayerController.OnConfirmInput → ConfirmedInputEvent { Text }
                    → ConversationManager: appends { role="user", content=Text } to history
                    → raises ChatCommandEvent { Messages = history.ToArray() }
                    → ChatClient sends to server

[Server streaming]  → ChatTokenEvent { Text } → ConversationManager accumulates in StringBuilder
[Server done]       → ChatDoneEvent → ConversationManager appends { role="assistant", content=buffer }
                                    → clears buffer
```

## `ChatCommands.cs` change

Add one new event struct:

```csharp
public struct ConfirmedInputEvent : IEvent
{
    public string Text;
}
```

## `PlayerController.cs` change

Replace the `Debug.Log` in `OnConfirmInput` with raising `ConfirmedInputEvent`:

```csharp
private void OnConfirmInput(InputAction.CallbackContext ctx)
{
    if (!_hasPendingInput) return;

    _hasPendingInput = false;
    var text = _pendingText;

    if (_speechContent != null)
        _speechContent.SetActive(false);

    EventBus.Raise(new ConfirmedInputEvent { Text = text });
}
```

## `ConversationManager.cs`

Play-mode `MonoBehaviour` singleton. No Inspector fields.

### State

```csharp
private List<ChatMessage> _history = new();
private StringBuilder _replyBuffer = new();
private bool _inferring;
```

### EventBus subscriptions

| Event | Handler |
|---|---|
| `ConfirmedInputEvent` | `OnConfirmedInput` |
| `ChatTokenEvent` | `OnChatToken` |
| `ChatDoneEvent` | `OnChatDone` |
| `ChatStateChangedEvent` | `OnChatStateChanged` |

### Handlers

**`OnConfirmedInput`:** If `_inferring`, log a warning and return (history stays consistent). Otherwise append `{ Role = "user", Content = e.Text }` to `_history` and raise `ChatCommandEvent { Messages = _history.ToArray() }`.

**`OnChatToken`:** Append `e.Text` to `_replyBuffer`.

**`OnChatDone`:** If `_replyBuffer.Length > 0`, append `{ Role = "assistant", Content = _replyBuffer.ToString() }` to `_history`. Always clear `_replyBuffer`.

**`OnChatStateChanged`:** Update `_inferring = (e.State == ChatState.Inferring)`.

## Guard Rails

- **Singleton:** A second `ConversationManager` logs an error and disables itself.
- **Confirm while inferring:** Logged as a warning; user turn is not appended.
- **Empty reply:** `OnChatDone` skips the assistant turn if the buffer is empty.
- **History on disconnect/error:** History is preserved. The next successful confirm sends the full history when the connection is restored.
- **History cleared:** History resets only when `ConversationManager` is disabled (e.g., scene unload or exit play mode).

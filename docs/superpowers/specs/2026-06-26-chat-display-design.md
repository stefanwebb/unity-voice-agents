# ChatDisplay Design

**Date:** 2026-06-26
**Scope:** `ChatDisplay.cs` (new), `SttStateIcon.cs` (modified)

## Overview

`ChatDisplay` shows the streaming LLM reply in a TextMeshPro text field — clearing on each new request and appending tokens as they arrive. `SttStateIcon` gains a `_thinkingIcon` field shown while the chat server is inferring. STT and chat inference never run simultaneously, so no priority logic is needed.

## ChatDisplay.cs

A play-mode `MonoBehaviour` following the `SttTranscriptionDisplay` pattern exactly.

**Attachment:** `[RequireComponent(typeof(TextMeshProUGUI))]` — attach to the GameObject holding the text component.

**EventBus subscriptions:**

| Event | Action |
|---|---|
| `ChatCommandEvent` | Clear text (`_text.text = ""`) |
| `ChatTokenEvent` | Append token (`_text.text += e.Text`) |

No `ChatDoneEvent` handling — streaming stops naturally when tokens stop arriving.

**No inspector fields.** No state beyond the `TextMeshProUGUI` reference. Pure consumer.

## SttStateIcon.cs changes

Three additions to the existing file:

**New serialized field:**
```csharp
[SerializeField] private Texture2D _thinkingIcon;
```

**New private field:**
```csharp
private ChatState _chatState = ChatState.Idle;
```

**Subscribe/unsubscribe** `ChatStateChangedEvent` in `OnEnable`/`OnDisable`.

**Handler:**
```csharp
private void OnChatStateChanged(ref ChatStateChangedEvent e)
{
    _chatState = e.State;
    UpdateIcon();
}
```

**`UpdateIcon()` changes — two edits:**

1. Extend the visibility condition so the icon is visible during inference (STT is `Paused` at that point, which would otherwise hide the icon):
   ```csharp
   var visible = !_connected || _state != SttState.Paused || _chatState == ChatState.Inferring;
   ```

2. Add a short-circuit before the STT icon switch — after the visibility guard, before the existing icon selection:
   ```csharp
   if (_chatState == ChatState.Inferring)
   {
       _rawImage.texture = _thinkingIcon;
       if (_thinkingIcon != null)
           _rawImage.rectTransform.sizeDelta = new Vector2(_thinkingIcon.width, _thinkingIcon.height);
       return;
   }
   ```

## Assumptions

- STT and chat inference never run simultaneously — no priority conflict handling needed.
- `ChatDisplay` is a separate GameObject from any STT display components.
- Callers clear the display by raising `ChatCommandEvent` before the new reply arrives.

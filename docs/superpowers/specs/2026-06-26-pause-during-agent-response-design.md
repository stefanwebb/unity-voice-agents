# Pause During Agent Response Design

**Date:** 2026-06-26
**Scope:** `PlayerController.cs` (modified only)

## Overview

The game stays paused (`Time.timeScale = 0`) from the moment the player starts speaking until they dismiss the agent response. Currently `Time.timeScale` is restored to `1f` on confirm input; it must instead be restored only on dismiss.

## Change

In `PlayerController.OnConfirmInput`, remove `Time.timeScale = 1f` from the confirm branch and add it to the dismiss branch:

```csharp
private void OnConfirmInput(InputAction.CallbackContext ctx)
{
    if (_hasPendingInput)
    {
        _hasPendingInput = false;
        // Time.timeScale stays 0 — game remains paused through streaming
        var text = _pendingText;
        if (_speechContent != null) _speechContent.SetActive(false);
        if (_agentContent != null) _agentContent.SetActive(true);
        _streamingDone = false;
        EventBus.Raise(new ConfirmedInputEvent { Text = text });
    }
    else if (_streamingDone)
    {
        _streamingDone = false;
        Time.timeScale = 1f;
        if (_agentContent != null) _agentContent.SetActive(false);
    }
}
```

## Pause Lifecycle

| Event | `Time.timeScale` |
|---|---|
| Space (speech start) | `0f` |
| Enter (confirm input) | stays `0f` |
| Chat streaming | stays `0f` |
| Enter (dismiss agent) | `1f` |

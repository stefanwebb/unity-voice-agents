# Pause STT Server on Exiting Play Mode — Design

Date: 2026-06-23

## Goal

Send a `pause` command to the `named_pipes.stt` server when exiting Play mode, but only if the server's last-known state was `listening` or `transcribing` — i.e. only if it was actually doing something that needs stopping.

## Background

- `SttClient` (`Assets/Scripts/SttClient.cs`) already forwards `state_changed` server events as `StateChangedEvent` (`SttState State`) onto `EventBus`, but doesn't retain the value anywhere itself.
- `SttClient` already hooks `UnityEditor.EditorApplication.playModeStateChanged` (added for the "send start on entering Play mode" feature) and already has a `_clientLock`-guarded `SendCommand` it can reuse directly.
- `ConnectLoop` already has a single place where a disconnect is detected and handled (nulling `_client`, raising `SttConnectionChangedEvent{Connected=false}`).

## Decisions

- **State caching**: add a private field tracking the last `state_changed` value, updated the same way `Devices`/`SelectedDevice` are (subscribe to the event `SttClient` itself already raises).
- **Stale-state reset**: reset the cached state back to `Ready` whenever the connection drops (in `ConnectLoop`'s existing disconnect handling), so a stale `Listening`/`Transcribing` left over from a previous connection can never trigger an incorrect `pause` against a connection that hasn't actually started listening.
- **Trigger**: extend the existing `OnPlayModeStateChanged` handler to also branch on `PlayModeStateChange.ExitingPlayMode` (fires while the Play-mode instance is still alive, before teardown) — send `pause` only if the cached state is `Listening` or `Transcribing`.

## Architecture

In `Assets/Scripts/SttClient.cs`:

- New field: `private SttState _lastKnownState = SttState.Ready;`
- New subscription in `OnEnable`/`OnDisable`: `EventBus.SubscribeTo<StateChangedEvent>(OnStateChangedEvent)` / `UnsubscribeFrom`.
- New handler: `private void OnStateChangedEvent(ref StateChangedEvent e) => _lastKnownState = e.State;`
- In `ConnectLoop`'s existing disconnect block, alongside `lock (_clientLock) { _client = null; }`, add `_lastKnownState = SttState.Ready;`.
- `OnPlayModeStateChanged` gains a second branch:
  ```csharp
  if (state == PlayModeStateChange.ExitingPlayMode)
  {
      if (_lastKnownState == SttState.Listening || _lastKnownState == SttState.Transcribing)
          SendCommand("pause");
      return;
  }
  ```

## Data Flow

```
Server sends state_changed("listening") -> StateChangedEvent -> OnStateChangedEvent -> _lastKnownState = Listening
... (any time later) ...
Exit Play mode -> EditorApplication.playModeStateChanged(ExitingPlayMode)
    -> _lastKnownState is Listening/Transcribing? -> SendCommand("pause")
    -> (server stops listening before the connection tears down in OnDisable)
```

## Error Handling

- Reuses the existing `SendCommand`, which already no-ops with a warning if disconnected — no new error path needed.
- `_lastKnownState` resetting to `Ready` on disconnect means a dead/never-started connection can never trigger a spurious `pause`.

## Testing

Deferred, consistent with the rest of this project. Manual verification: enter Play mode, send `start`, wait for a `state_changed` to `listening` (e.g. speak once), exit Play mode, and confirm (via server-side logs/behavior) that `pause` was sent. Also verify the negative case: exit Play mode without ever starting/listening and confirm no `pause` is sent.

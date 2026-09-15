# STT Client — Global Object Design

Date: 2026-06-22

## Goal

Add a global, auto-connecting client for the `named_pipes.stt` server
(`/tmp/tool-stt`) that wraps `ToolClient`, exposes a command API for
controlling the mic/transcription session, and forwards every server event
onto the existing `EventBus` as a typed struct.

## Background

- `ToolClient.cs` (`Assets/Scripts/ToolClient.cs`) is a generic named-pipe
  protocol client: construct with a tool name, register `On(eventName,
  handler)` callbacks, call `StartListening()` to spin up a background
  listener thread, then `Subscribe()` (blocks until the server confirms).
  Handlers registered via `On(...)` run synchronously on that background
  listener thread — they are not wrapped in try/catch by `ToolClient`, so an
  exception inside one would silently kill the listener thread.
- `EventBus.cs` / `GenericEventBus.cs` (`Assets/Scripts/`) provide a static,
  struct-based pub/sub bus. Events are structs implementing the marker
  interface `IEvent`, raised with `EventBus.Raise(new SomeEvent{...})`, and
  subscribed to with `EventBus.SubscribeTo<TEvent>(handler)` where the
  handler is `void Handler(ref TEvent eventData)`. `EventBus` self-initializes
  via `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]`
  and is a pure static class (no `MonoBehaviour`, no `Update()`).
- The STT server protocol (from the `named_pipes.stt` spec) is:
  - **Commands**: `start`, `pause`, `list_devices`, `get_device`,
    `set_device` (`device: str`).
  - **Broadcast events**: `speech_start`, `token` (`text`), `speech`
    (`text`, optional `words`), `speech_end`.
  - **Response events**: `devices` (`devices: [{index, name, channels}]`),
    `device` (`device: int | null`).
  - **Lifecycle**: `state_changed` (`state`), one of
    `ready|loading|listening|transcribing|paused|error`.
  - The server is a separate Python process (e.g. `cpipe --serve stt`) that
    must be running for `/tmp/tool-stt` to exist; Unity has no control over
    its lifecycle.

## Decisions Made During Brainstorming

- **API scope**: inbound (server → `EventBus`) **and** outbound (Unity →
  server commands). The global object is the only way calling code talks to
  the STT server.
- **Connect timing**: auto-connect at startup, mirroring `EventBus`'s
  self-init. Actual mic capture only starts when something calls
  `SttClient.Start()`.
- **Threading**: `ToolClient`'s `On(...)` handlers only enqueue raw
  `(eventName, JsonObject)` pairs into a thread-safe queue. A
  `MonoBehaviour.Update()` on the main thread drains the queue, converts each
  entry to its typed struct, and calls `EventBus.Raise(...)`. No parsing or
  Unity API calls happen on `ToolClient`'s background listener thread.
- **GameObject lifecycle**: since `Update()` and shutdown disposal are
  needed, this requires a real `GameObject`+`MonoBehaviour`, unlike
  `EventBus`. It is auto-spawned via `[RuntimeInitializeOnLoadMethod]`
  (`DontDestroyOnLoad`) — no manual scene/prefab setup required, consistent
  with `EventBus`'s zero-config self-init.
- **Connect failure handling**: log a warning and retry every 3s on a
  background thread until the connection succeeds or the app quits. No
  unhandled exceptions should surface from a missing server process.
- **Testing**: deferred. No automated tests or manual smoke-test script as
  part of this work.

## Architecture

Two new files in `Assets/Scripts/`:

### `SttEvents.cs`

`IEvent` structs for every server message, plus supporting types:

```csharp
public enum SttState { Ready, Loading, Listening, Transcribing, Paused, Error }

public struct AudioDevice { public int Index; public string Name; public int Channels; }
public struct AlignedWord { public string Word; public double Start; public double End; }

public struct SpeechStartEvent : IEvent { }
public struct SpeechEndEvent : IEvent { }
public struct TokenEvent : IEvent { public string Text; }
public struct SpeechEvent : IEvent { public string Text; public AlignedWord[] Words; } // null unless align=True
public struct StateChangedEvent : IEvent { public SttState State; }
public struct DevicesEvent : IEvent { public AudioDevice[] Devices; }
public struct DeviceEvent : IEvent { public int? Device; }

// Raised by the runner itself (not part of the wire protocol) when the
// underlying ToolClient connects or disconnects.
public struct SttConnectionChangedEvent : IEvent { public bool Connected; }
```

### `SttClient.cs`

A static façade, mirroring `EventBus`'s static-wrapper pattern:

```csharp
public static class SttClient
{
    public static bool IsConnected { get; } // delegates to the runner singleton

    public static void Start()        => SendCommand("start");
    public static void Pause()        => SendCommand("pause");
    public static void ListDevices()  => SendCommand("list_devices");
    public static void GetDevice()    => SendCommand("get_device");
    public static void SetDevice(string device) =>
        SendCommand("set_device", new JsonObject { ["device"] = device });
}
```

Backed internally by a hidden `MonoBehaviour` singleton, `SttClientRunner`:

- `[RuntimeInitializeOnLoadMethod]` spawns the singleton `GameObject`
  (`DontDestroyOnLoad`). If a previous instance/`ToolClient` already exists
  (domain reload disabled in Editor), dispose it first — mirrors
  `EventBus.Init()`'s existing `#if UNITY_EDITOR` guard.
- A background connect/retry loop: `new ToolClient("stt")` → register
  `On(...)` handlers for `speech_start`, `token`, `speech`, `speech_end`,
  `state_changed`, `devices`, `device` → `StartListening()` → `Subscribe()`.
  On success, raise `SttConnectionChangedEvent { Connected = true }`. On
  exception, log a warning and retry after 3s.
- A watcher on the `ManualResetEventSlim` returned by `StartListening()`:
  when it fires (listener thread exited, e.g. server crashed), dispose the
  dead `ToolClient`, raise `SttConnectionChangedEvent { Connected = false }`,
  and restart the connect/retry loop.
- `Update()`: drains the thread-safe queue of raw `(eventName, JsonObject)`
  pairs, converts each to its typed struct (wrapped per-event in try/catch —
  log and skip on failure), and calls `EventBus.Raise(...)`.
- `OnDestroy()` / `OnApplicationQuit()`: stop the retry loop and, if
  connected, `Unsubscribe()` + `Dispose()` the `ToolClient`.
- `SendCommand(...)` (used by `SttClient`'s static methods): if not
  connected, log a warning and no-op rather than queuing for later.

## Data Flow

```
[STT server process]
      |  (named pipe /tmp/tool-stt)
      v
ToolClient background listener thread
      |  On("speech", ...) etc. -> enqueue (eventName, JsonObject)
      v
ConcurrentQueue<(string, JsonObject)>
      |  drained in Update() (main thread)
      v
typed struct (e.g. SpeechEvent) -> EventBus.Raise(...)
      |
      v
any subscriber: EventBus.SubscribeTo<SpeechEvent>(handler)
```

Commands flow the opposite direction: `SttClient.Start()` →
`SttClientRunner.SendCommand("start")` → `ToolClient.SendCommand(...)` →
written to the upstream pipe.

## Error Handling

- **Initial connect failure**: caught, logged once per attempt, retried
  every 3s until success or app quit.
- **Connection lost after connecting**: detected via the listener thread's
  exit signal; triggers disposal, a `Connected = false` event, and restart of
  the retry loop.
- **Malformed event payload**: per-event try/catch around JSON→struct
  conversion in `Update()`; logs and skips, doesn't break the drain loop.
- **Commands sent while disconnected**: warning + no-op.
- **Handlers stay "dumb"**: `On(...)` callbacks only enqueue — no parsing or
  Unity API calls — because `ToolClient` does not guard handler invocation
  with try/catch, so any exception thrown there would silently kill the
  listener thread.

## Testing

Deferred — out of scope for this work. (`com.unity.test-framework` is
already in `Packages/manifest.json` but no `Tests/` asmdef exists yet;
revisit later, possibly with an `SttTestButton.cs` mirroring
`Assets/Scripts/TestButton.cs`.)

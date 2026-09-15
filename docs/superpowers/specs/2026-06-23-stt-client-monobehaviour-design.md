# SttClient as a Configurable MonoBehaviour — Design

Date: 2026-06-23

## Goal

Replace the current static `SttClient` façade + internal auto-spawned `SttClientRunner` (`Assets/Scripts/SttClient.cs`) with a single public `SttClient : MonoBehaviour` that you place in a scene and configure from the Inspector. Both directions of traffic move onto `EventBus`: incoming server events (already true) and now outgoing commands too — any script raises a `*CommandEvent`, and `SttClient` forwards it to the server.

This supersedes the `SttDeviceLogger` design from earlier today (removed — it called the now-gone static `SttClient.ListDevices()`; it can be re-specced trivially against `ListDevicesCommandEvent` once this lands).

## Background

The current `Assets/Scripts/SttClient.cs` has:
- `public static class SttClient` — static façade with `Start()`, `Pause()`, `ListDevices()`, `GetDevice()`, `SetDevice(string)`, `IsConnected`, each forwarding to `SttClientRunner`.
- `internal class SttClientRunner : MonoBehaviour` — auto-spawns itself via `[RuntimeInitializeOnLoadMethod]`, `DontDestroyOnLoad`, with a domain-reload guard for the Unity Editor. Owns the `ToolClient("stt")` connection: a background connect/retry loop (3s retry), a ping/pong heartbeat (5s interval / 10s pong timeout) that force-closes dead connections, and a `ConcurrentQueue<Action>` drained in `Update()` to raise inbound events on `EventBus` from the main thread. `_client` access is guarded by `_clientLock` for thread safety between the connect thread and command senders.

None of that internal machinery is being rebuilt — it's already implemented and reviewed. This design changes how it's *configured* (Inspector fields instead of constants, manual placement instead of auto-spawn) and how commands get *in* (EventBus subscriptions instead of a static method call).

## Decisions

- **Configurable via Inspector**: tool name, reconnect interval, ping interval, and pong timeout become serialized fields instead of hardcoded constants (`"stt"`, 3s, 5s, 10s).
- **Live debug state**: a serialized field mirrors connection status, visible in the Inspector during Play mode.
- **Command dispatch**: fully symmetric with the existing inbound pattern. Each server command gets its own `IEvent` struct; any script raises it on `EventBus`; `SttClient` subscribes and forwards to `ToolClient.SendCommand(...)`. No static API, no instance references needed by callers.
- **Placement**: manually placed on a GameObject, like `TestButton.cs` — no more auto-spawn or domain-reload guard (ordinary Unity scene-object lifecycle is sufficient once it's not a hidden auto-created singleton).

## Architecture

### New file: `Assets/Scripts/SttCommands.cs`

One `IEvent` struct per server command, mirroring `SttEvents.cs`'s pattern for inbound events:

```csharp
public struct StartCommandEvent : IEvent { }
public struct PauseCommandEvent : IEvent { }
public struct ListDevicesCommandEvent : IEvent { }
public struct GetDeviceCommandEvent : IEvent { }
public struct SetDeviceCommandEvent : IEvent { public string Device; }
```

### Rewritten `Assets/Scripts/SttClient.cs`

One class: `public class SttClient : MonoBehaviour`. Replaces both the old static façade and `SttClientRunner`.

**Inspector fields:**
```csharp
[SerializeField] private string _toolName = "stt";
[SerializeField] private float _reconnectIntervalSeconds = 3f;
[SerializeField] private float _pingIntervalSeconds = 5f;
[SerializeField] private float _pongTimeoutSeconds = 10f;
[SerializeField] private bool _isConnectedDebug;
```
`_isConnectedDebug` is written from `Update()` (main thread) by copying the existing `volatile bool _connected` each frame — `_connected` itself stays as-is for the threading logic; the debug field is purely a Play-mode Inspector mirror.

**Lifecycle:**
- `OnEnable()`: subscribes to the 5 command events on `EventBus`, then starts the connect/retry background thread (same `ConnectLoop` as today, just reading `_toolName`/`_reconnectIntervalSeconds` instead of constants).
- `OnDisable()`: unsubscribes from the 5 command events, then tears down the connection (`ShutdownClient()`) — so disabling the component (not just destroying it) now cleanly disconnects, matching ordinary Unity component semantics.
- `OnApplicationQuit()`: also calls `ShutdownClient()` (kept alongside `OnDisable`, since `ShutdownClient()` is already idempotent/lock-guarded — harmless belt-and-suspenders for quit-order edge cases).

**Command handlers** (replace the old static `SendCommand` + `_instance` lookup with plain instance methods, since the handler already runs bound to `this`):
```csharp
private void OnStartCommand(ref StartCommandEvent e) => SendCommand("start");
private void OnPauseCommand(ref PauseCommandEvent e) => SendCommand("pause");
private void OnListDevicesCommand(ref ListDevicesCommandEvent e) => SendCommand("list_devices");
private void OnGetDeviceCommand(ref GetDeviceCommandEvent e) => SendCommand("get_device");
private void OnSetDeviceCommand(ref SetDeviceCommandEvent e) =>
    SendCommand("set_device", new JsonObject { ["device"] = e.Device });
```
`SendCommand` keeps the exact same body as today (snapshot `_client` under `_clientLock`, try/catch around the actual `client.SendCommand(...)` call) — just as a private instance method instead of `internal static`.

**Unchanged internals** (carried over verbatim, only reading config from fields instead of constants): `ConnectLoop()`, `Connect()` (including the `_cts.Token.Register(client.Dispose)` in-flight-disposal fix and the ping/pong heartbeat thread it spawns), `HeartbeatLoop()`, `ShutdownClient()`, `ParseState()`, `ParseSpeechEvent()`, `ParseDevicesEvent()`, and the `_pending`/`Enqueue`/`Update()` drain loop.

**Removed:** the `public static class SttClient` façade, the `_instance` static singleton pointer, the `[RuntimeInitializeOnLoadMethod] Init()` auto-spawn, and the `#if UNITY_EDITOR` domain-reload guard (no longer needed — this is now an ordinary scene-placed component, not a hidden auto-created singleton).

## Data Flow

```
Any script: EventBus.Raise(new StartCommandEvent())
    -> SttClient.OnStartCommand -> SendCommand("start") -> ToolClient.SendCommand
    -> (server processes it, may broadcast events)
    -> ToolClient's background listener -> Enqueue(...) -> Update() drain
    -> EventBus.Raise(new SpeechStartEvent()) / etc.
    -> any subscriber: EventBus.SubscribeTo<SpeechStartEvent>(handler)
```

Both directions are now symmetric: nothing in the codebase holds a reference to `SttClient` itself; everything flows through `EventBus`.

## Error Handling

Identical to the already-reviewed behavior — only the entry point changed:
- Commands raised while disconnected: `SendCommand` logs a warning and no-ops (same as before).
- Connect failure / disconnect / malformed payload / in-flight-connect disposal: unchanged, all carried over from the existing implementation.

## Testing

Deferred, consistent with the rest of this project. Manual verification: place `SttClient` on a GameObject, run against a live `cpipe --serve stt` process, and confirm `EventBus.Raise(new ListDevicesCommandEvent())` from anywhere results in a `DevicesEvent` and console-visible Inspector state changes.

# ChatClient Design

**Date:** 2026-06-26
**Scope:** `ChatClient.cs`, `ChatEvents.cs`, `ChatCommands.cs`

## Overview

`ChatClient` is a Unity `MonoBehaviour` that connects to the `named_pipes.chat` LLM inference server over a named pipe (`/tmp/tool-chat`) and bridges it to the project's `EventBus`. It mirrors the architecture of `SttClient` but is scoped to play mode only (no `[ExecuteAlways]`) since chat inference has no editor-mode use case (no device selection, no live status to monitor).

## Architecture

`ChatClient` is a singleton `MonoBehaviour`. On `OnEnable` it spawns a `ConnectLoop` background thread that opens a `ToolClient` connection, subscribes, and launches a heartbeat thread. The main thread drains a `ConcurrentQueue<Action>` in `Update()` to marshal events from background threads onto the Unity main thread before raising them on `EventBus`.

### Files

| File | Contents |
|---|---|
| `ChatEvents.cs` | `ChatState` enum, `ChatMessage` struct, inbound event structs |
| `ChatCommands.cs` | Outbound command event structs |
| `ChatClient.cs` | MonoBehaviour singleton, connection/reconnect/heartbeat logic |

### Connection lifecycle

1. `OnEnable` → subscribe to `ChatCommandEvent` on EventBus, spawn `ConnectLoop` thread
2. `ConnectLoop` → calls `Connect()`, waits on `done` event, on disconnect raises `ChatConnectionChangedEvent { Connected = false }`, sleeps `_reconnectIntervalSeconds`, retries
3. `Connect()` → creates `ToolClient("chat")`, registers event handlers, calls `StartListening()` + `Subscribe()`, raises `ChatConnectionChangedEvent { Connected = true }`, spawns `HeartbeatLoop` thread
4. `OnDisable` → unsubscribe from EventBus, cancel CTS, unsubscribe ToolClient, dispose

### Heartbeat

Identical to `SttClient`: a background thread sends `ping` on `_pingIntervalSeconds` and force-closes the connection if no `pong` arrives within `_pongTimeoutSeconds`. This detects dead servers, since named pipe FIFOs opened O_RDWR never see EOF when the writer closes.

## Types

### `ChatEvents.cs`

```
enum ChatState { Idle, Inferring, Error }

struct ChatMessage { string Role; string Content; }

struct ChatTokenEvent : IEvent { string Text; }           // per-token streaming
struct ChatDoneEvent : IEvent { }                          // final token (done: true)
struct ChatStateChangedEvent : IEvent { ChatState State; }
struct ChatConnectionChangedEvent : IEvent { bool Connected; }
```

### `ChatCommands.cs`

```
struct ChatCommandEvent : IEvent { ChatMessage[] Messages; }
```

The caller is responsible for providing the full conversation history in `Messages` (OpenAI format: `role` + `content` pairs). `ChatClient` is stateless with respect to conversation content.

## Event mapping

### Server → Unity

| Server event | `done` field | EventBus event raised |
|---|---|---|
| `token` | `false` | `ChatTokenEvent { Text }` |
| `token` | `true` | `ChatDoneEvent`, state → `Idle` |
| `pong` | — | (internal: resets heartbeat timer) |

### Unity → Server

| EventBus event | Server command sent |
|---|---|
| `ChatCommandEvent` | `{"pid": …, "cmd": "chat", "messages": […]}` |

## State tracking

State is inferred client-side; the server does not send state-change events.

```
Idle → Inferring   when ChatCommandEvent is sent to the server
Inferring → Idle   when done: true token arrives
Inferring → Error  when connection drops mid-inference
Error → Idle       on reconnect (connection re-established resets to Idle)
```

`ChatStateChangedEvent` is raised on every transition.

## Inspector fields

| Field | Type | Default | Description |
|---|---|---|---|
| `_toolName` | `string` | `"chat"` | Named pipe tool name |
| `_reconnectIntervalSeconds` | `float` | `3` | Delay between reconnect attempts |
| `_pingIntervalSeconds` | `float` | `5` | Heartbeat ping interval |
| `_pongTimeoutSeconds` | `float` | `10` | Max wait for pong before force-close |

## Guard rails

- Singleton: a second `ChatClient` in the scene logs an error and disables itself.
- Commands sent while disconnected log a warning and are dropped.
- Commands sent while `Inferring` log a warning and are dropped (server enforces one inference at a time; client should mirror this).

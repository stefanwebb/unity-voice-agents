# Voice Agents

Voice Agents (`com.stefanwebb.voiceagents`) is a Unity package for building open-source AI voice agents that run fully locally.

You can use it to build intelligent non-player characters (NPCs), voice-driven game interfaces, and more.

The package connects Unity to local **speech-to-text (STT)** and **LLM chat** servers over named pipes, and exposes everything as typed events on a lightweight event bus so your gameplay code never touches the transport.

> Text-to-speech (TTS) is not yet implemented — see [Limitations](#limitations).

## How it works

```
 Microphone ─► named_pipes.stt server ─┐
                                       │  /tmp/tool-stt, /tmp/tool-chat (FIFOs)
 vLLM / any OpenAI-style LLM ◄─ named_pipes.chat server ─┘
                                       ▲
                                       │ ToolClient (JSON lines)
                                       ▼
   SttClient ──────► EventBus ◄────── ChatClient
                       ▲   │
       ConversationManager │  SttTranscriptionDisplay / ChatDisplay / SttStateIcon
                       your scripts (raise commands, subscribe to events)
```

| Component | Role |
|---|---|
| `ToolClient` | C# client for the Named Pipe Tools protocol (`/tmp/tool-{name}`) |
| `SttClient` | Connects to the `stt` tool; raises `SpeechStartEvent`, `SpeechEvent`, `TokenEvent`, `StateChangedEvent`, `DevicesEvent`…; forwards `StartCommandEvent`, `PauseCommandEvent`, `SetDeviceCommandEvent`…. Runs in Edit mode too, with a device dropdown in the Inspector |
| `ChatClient` | Connects to the `chat` tool; raises streaming `ChatTokenEvent`, `ChatDoneEvent`, `ChatStateChangedEvent`; forwards `ChatCommandEvent` |
| `ConversationManager` | Turns a `ConfirmedInputEvent` into a `ChatCommandEvent` with full history and optional system prompt |
| `EventBus` | Static typed event bus (built on `GenericEventBus`) |
| `SttTranscriptionDisplay`, `ChatDisplay`, `SttStateIcon` | Drop-in uGUI/TextMeshPro views |

## Requirements

- Unity 6000.0 or later (uses `System.Text.Json`, available with the .NET Standard 2.1 profile)
- macOS or Linux — `ToolClient` uses POSIX FIFOs (`mkfifo`) via `libc`
- Running `stt` and `chat` tool servers from the companion `named_pipes` Python package (instructions to follow)
- TextMeshPro essentials imported into your project (Window ▸ TextMeshPro ▸ Import TMP Essential Resources)

## Installation

1. In Unity, open **Window ▸ Package Manager**.
2. Click **+ ▸ Add package from git URL…** and enter:
   ```
   https://github.com/stefanwebb/unity-voice-agents.git
   ```
   (or **Add package from disk…** and pick this folder's `package.json`).
3. Dependencies (`com.unity.ugui`, `com.unity.inputsystem`, `com.unity.render-pipelines.universal`) are pulled in automatically. If prompted to enable the new Input System, accept and restart the Editor.

## Running the sample

1. In Package Manager select **Voice Agents ▸ Samples** and click **Import** next to *Voice Agent Demo*.
2. Start the `stt` and `chat` servers so `/tmp/tool-stt` and `/tmp/tool-chat` exist.
3. Open `Assets/Samples/Voice Agents/<version>/Voice Agent Demo/VoiceAgentDemo.unity`. The sample uses URP — if your project is not on URP, assign a URP asset in Graphics settings or ignore the post-processing volume.
4. Select the **AgentManager** object: the `SttClient` inspector shows the connection state and lets you pick a microphone (works without entering Play mode). Paste a system prompt into `ConversationManager` if you like (`SystemPrompt.Text` in the sample is an example).
5. Press **Play**, press **Space** and speak (the game pauses while you talk; the server detects when you stop), then press **Enter** to send the transcription to the LLM. The reply streams into the on-screen textbox — press **Enter** again to dismiss it and resume. The icon shows disconnected / listening / transcribing / thinking state.

## Using it in your own scene

1. Add `SttClient`, `ChatClient`, and `ConversationManager` to a GameObject.
2. Raise commands and subscribe to events from any script:
   ```csharp
   using GenerativeGamedev;

   void OnEnable()  => EventBus.SubscribeTo<ChatTokenEvent>(OnToken);
   void OnDisable() => EventBus.UnsubscribeFrom<ChatTokenEvent>(OnToken);

   void StartListening() => EventBus.Raise(new StartCommandEvent());
   void Send(string text) => EventBus.Raise(new ConfirmedInputEvent { Text = text });
   void OnToken(ref ChatTokenEvent e) => Debug.Log(e.Text);
   ```

## Limitations

- No text-to-speech yet.
- macOS/Linux only (named pipes).
- No tool calling, memory, or planning on the LLM side — the "agent" is a chat completion with a system prompt.
- Server setup and the `named_pipes` Python package are documented separately.

## License

CC BY-SA 4.0 — see [LICENSE.md](LICENSE.md).

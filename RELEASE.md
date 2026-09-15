### Named-pipe STT + chat clients
- Replaced the WebRTC/Pipecat proof-of-concept with a named-pipe based client (`ToolClient`) that talks to local `stt` and `chat` tool servers
- `SttClient`: streaming speech-to-text with device selection, works in Edit mode and Play mode; custom Inspector with an audio-device dropdown
- `ChatClient` + `ConversationManager`: streams LLM tokens, maintains conversation history, optional system prompt
- Typed `EventBus` (`GenericEventBus`) decouples every component; all server events and commands are event structs
- UI helpers: `SttTranscriptionDisplay`, `ChatDisplay`, `SttStateIcon`
- "Voice Agent Demo" sample scene importable from the Package Manager
- Dropped the `com.unity.webrtc` dependency and the bundled Pipecat `Agent/` Python server

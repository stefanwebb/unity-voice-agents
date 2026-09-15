# Changelog
All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](http://keepachangelog.com/en/1.0.0/)
and this project adheres to [Semantic Versioning](http://semver.org/spec/v2.0.0.html).

## [0.2.3] 2026-09-14
### Bug fixes
- Sample scene: input actions and the post-processing volume profile now resolve after import. The sample's `InputSystem_Actions.inputactions`, `SampleScene.unity` and `SampleSceneProfile.asset` carried the fixed GUIDs Unity's project template assigns to those files, so importing the sample into a template-based project collided with the project's own copies and the scene's references broke. They are now `VoiceAgentDemoActions.inputactions`, `VoiceAgentDemo.unity` and `VoiceAgentDemoProfile.asset` with unique GUIDs. **If you imported an earlier version, remove `Assets/Samples/Voice Agents/<old version>` and re-import.**

## [0.2.2] 2026-09-14
### Bug fixes
- Sample scene: `PlayerController` now resolves `Player/StartSpeech` and `Player/ConfirmInput` by name from the `InputActionAsset` on every `OnEnable`, instead of relying on cached `InputActionReference`s that could stop firing when re-entering Play mode with domain reload disabled (the Unity 6 default). The sample scene wires the asset automatically; the reference fields remain as a fallback.

## [0.2.1] 2026-09-14
### Bug fixes
- Ship `.meta` files for `package.json` and the root `README.md`, `CHANGELOG.md`, `LICENSE.md`, `RELEASE.md`, so installing the package no longer generates untracked `.meta` files in the consumer's project
- Stop shipping the internal design-note markdown (previously `docs/`), which Unity imported as TextAssets

### Infrastructure / Documentation
- `release.yml` workflow: pushing a `vX.Y.Z` tag now verifies it matches `package.json` and creates the GitHub release from `RELEASE.md`
- Release skills updated accordingly

## [0.2.0] 2026-09-14
### Named-pipe STT + chat clients
- Replaced the WebRTC/Pipecat proof-of-concept with a named-pipe based client (`ToolClient`) that talks to local `stt` and `chat` tool servers
- `SttClient`: streaming speech-to-text with device selection, works in Edit mode and Play mode; custom Inspector with an audio-device dropdown
- `ChatClient` + `ConversationManager`: streams LLM tokens, maintains conversation history, optional system prompt
- Typed `EventBus` (`GenericEventBus`) decouples every component; all server events and commands are event structs
- UI helpers: `SttTranscriptionDisplay`, `ChatDisplay`, `SttStateIcon`
- "Voice Agent Demo" sample scene importable from the Package Manager
- Dropped the `com.unity.webrtc` dependency and the bundled Pipecat `Agent/` Python server

## [0.1.0] 2026-03-01

### Pre-release: Proof-of-concept
- Communicates with pre-running Pipecat server from Unity via WebRTC
- Microphone input while in Play Mode is sent to Pipecat
- Displays a transcription of the user's speech as well as the Agent's LLM response
- Assumes a pre-running Pipecat server and inference service

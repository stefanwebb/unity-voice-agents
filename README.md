# UnityVoiceAgents

UnityVoiceAgents is a Unity package for building open-source AI voice agents that run fully locally. 

You can use it to build intelligent non-player characters (NPCs), game interfaces, among many other applications.

It is currently a proof-of-concept and requires several improvements before it's ready for use in game development.

## Instructions

1. Open your Unity project and install UnityVoiceAgents package
2. Open test scene
3. Run Pipecat server (more instructions on this in the near future)
4. Start Play Mode
5. Be amazed as your speech is transcribed and the LLM responds to you
6. Read the source code in lieu of documentation
7. Submit issues, PR contributions, and feedback!

## Limitations

To quickly develop a prototype, I have left the following limitations for future work:

* In the provided Pipecat server, speech-to-text (STT) is "segmented" rather than streaming, which means a user's utterance isn't transcribed until the speaker has finished speaking. This gives the impression of a lower real-time latency.
* In the provided server, there is no text-to-speech or interruption detection
* There is no signal to the user whether the connection to Pipecat is active or not so you have to read the debug console to know when it's ready for input.
* A connection to Pipecat has to be re-established every time Play Mode is entered, which slows down development.
* If disconnected from Pipecat in Play Mode, there is no way to re-connect without restarting Play Mode.
* There is no way to pause the agent.
* In the provided Pipecat server, the "agent" is just an LLM chatbot without tool calling, memory, planning, and so on.
* The library momentarily hangs the main thread while connecting to Pipecat.
* Parameters like the microphone index and server address are hardcoded.

# Voice Agents for Unity

**Voice Agents for Unity** is a package for building open-source AI voice agents that run fully locally with realtime latency.

It is built on top of:
* [Pipecat](https://github.com/pipecat-ai/pipecat); 
* [Local voice agents on MacOS with Pipecat](https://github.com/kwindla/macos-local-voice-agents/tree/main);
* [WebRTC for Unity](https://github.com/Unity-Technologies/com.unity.webrtc);
* Open-source inference libraries such as [vLLM](https://github.com/vllm-project/vllm); and,
* Open-source models from the [Hugging Face Hub](https://huggingface.co/models).

You can use it to build intelligent non-player characters (NPCs), game interfaces, among many other applications.

It is currently a proof-of-concept and requires several improvements before it's ready for use in game development.

## Instructions
### Install package
First, install the package in your project along with the provided sample. Open the Package Manager and under the `+` dropdown select "Install package from git URL". Enter:
```
https://github.com/stefanwebb/unity-voice-agents.git
```
Confirm that the package is present in your Project window under `Packages/com.stefanwebb.voiceagents` and the sample under `Samples/Example`

### Inference Server
Next, you need to launch a server for local LLM inference for use by the Agent. The inference server is called from Pipecat (architectural diagram coming soon!)

You can use any OpenAI `Completions API` compatible server, for example:
* [HuggingFace Transformers](https://huggingface.co/docs/transformers/main/en/serving)
* [LM Studio](https://lmstudio.ai/docs/developer/openai-compat)
* [LocalAI](https://localai.io/features/text-generation/)
* [Ollama](https://docs.ollama.com/cli)
* [vLLM](https://github.com/vllm-project/vllm)
* [vLLM-MLX](https://github.com/waybarrios/vllm-mlx)

Choose the option that is most convenient for your platform and follow the instructions there to install and launch the server. It must be hosted at `http://127.0.0.1:1234` since the configuration is currently hardcoded.

As I am on Mac, I'm using vLLM-MLX and my launch command is:
```bash
vllm-mlx serve mlx-community/Llama-3.2-3B-Instruct-4bit --port 1234
```

### Pipecat Server
After that, you need to launch a Pipecat server, which is where the Agent "lives". [Pipecat](https://github.com/pipecat-ai/pipecat) is a Python framework for building real-time voice and multimodal conversational agents.

Follow the instructions on the Pipecat website to install it (which requires that Python is installed first, of course).

This package provides an example Pipecat server that runs on Mac and the launch command, from the project folder, is:
```bash
uv run Packages/com.stefanwebb.voiceagents/Agent/agent.py
```

### Sample Scene
With the inference and Pipecat servers running, open the test scene in the sample and run Play Mode. If everything is working correctly, your speech will be transcribed and displayed in the Game window, passed to the LLM, and its response displayed as well. The conversation history accumulates as you talk to the agent, so this is effectively a voice chatbot.

## Limitations
To quickly develop a prototype, I have left the following limitations for future work:

* In the provided Pipecat server, speech-to-text (STT) is "segmented" rather than streaming, which means a user's utterance isn't transcribed until the speaker has finished speaking. This gives the impression of a lower real-time latency.
* In the provided server, there is no text-to-speech, interruption detection, or other voice agent components.
* There is no signal to the user whether the connection to Pipecat is active or not so you have to read the debug console to know when it's ready for input.
* A connection to Pipecat has to be re-established every time Play Mode is entered, which slows down development.
* If disconnected from Pipecat in Play Mode, there is no way to re-connect without restarting Play Mode.
* There is no way to pause the agent.
* In the provided Pipecat server, the "agent" is just an LLM chatbot without tool calling, memory, planning, and so on.
* The library momentarily hangs the main thread while connecting to Pipecat.
* Parameters like the microphone index and server address are hardcoded.
* Requires small modifications to Pipecat server to work on Windows and Linux.
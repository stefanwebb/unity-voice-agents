// © 2025–2026, Stefan Webb. Some Rights Reserved.
// Licensed under CC BY-SA 4.0
//
// Event structs raised on EventBus for every named_pipes.stt server message.
// See docs/superpowers/specs/2026-06-22-stt-client-design.md.

namespace GenerativeGamedev {

public enum SttState { Ready, Loading, Listening, Transcribing, Paused, Error }

public struct AudioDevice
{
    public int Index;
    public string Name;
    public int Channels;
}

public struct AlignedWord
{
    public string Word;
    public double Start;
    public double End;
}

public struct SpeechStartEvent : IEvent { }

public struct SpeechEndEvent : IEvent { }

public struct TokenEvent : IEvent
{
    public string Text;
}

public struct SpeechEvent : IEvent
{
    public string Text;
    public AlignedWord[] Words;
}

public struct StateChangedEvent : IEvent
{
    public SttState State;
}

public struct DevicesEvent : IEvent
{
    public AudioDevice[] Devices;
}

public struct DeviceEvent : IEvent
{
    public int? Device;
}

public struct SttConnectionChangedEvent : IEvent
{
    public bool Connected;
}

}

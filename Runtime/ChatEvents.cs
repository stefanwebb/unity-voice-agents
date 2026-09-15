// © 2025–2026, Stefan Webb. Some Rights Reserved.
// Licensed under CC BY-SA 4.0
//
// Event structs raised on EventBus for every named_pipes.chat server message.
// See docs/superpowers/specs/2026-06-26-chat-client-design.md.

namespace GenerativeGamedev {

public enum ChatState { Idle, Inferring, Error }

public struct ChatMessage
{
    public string Role;
    public string Content;
}

public struct ChatTokenEvent : IEvent
{
    public string Text;
}

public struct ChatDoneEvent : IEvent { }

public struct ChatStateChangedEvent : IEvent
{
    public ChatState State;
}

public struct ChatConnectionChangedEvent : IEvent
{
    public bool Connected;
}

}

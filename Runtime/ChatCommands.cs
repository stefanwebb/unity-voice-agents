// © 2025–2026, Stefan Webb. Some Rights Reserved.
// Licensed under CC BY-SA 4.0
//
// Outbound command events for the named_pipes.chat server. Any script raises
// one of these on EventBus; ChatClient forwards it to the server.
// See docs/superpowers/specs/2026-06-26-chat-client-design.md.

namespace GenerativeGamedev {

public struct ChatCommandEvent : IEvent
{
    public ChatMessage[] Messages;
}

public struct ConfirmedInputEvent : IEvent
{
    public string Text;
}

}

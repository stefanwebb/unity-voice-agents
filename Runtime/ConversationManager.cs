// © 2025–2026, Stefan Webb. Some Rights Reserved.
// Licensed under CC BY-SA 4.0
//
// Maintains conversation history and bridges confirmed STT input to the chat server.
// Appends user turns on ConfirmedInputEvent, fires ChatCommandEvent with full history,
// accumulates streaming reply tokens, and closes the assistant turn on ChatDoneEvent.
// See docs/superpowers/specs/2026-06-26-conversation-manager-design.md.

using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace GenerativeGamedev {

public class ConversationManager : MonoBehaviour
{
    [SerializeField, TextArea(3, 15)] private string _systemPrompt;
    [SerializeField] private bool _accumulateHistory = true;

    private static ConversationManager _activeInstance;

    private readonly List<ChatMessage> _history = new();
    private readonly StringBuilder _replyBuffer = new();
    private bool _inferring;

    private void OnEnable()
    {
        if (_activeInstance != null)
        {
            Debug.LogError($"[ConversationManager] another ConversationManager ('{_activeInstance.name}') is already active; " +
                "disabling this one.");
            enabled = false;
            return;
        }
        _activeInstance = this;

        EventBus.SubscribeTo<ConfirmedInputEvent>(OnConfirmedInput);
        EventBus.SubscribeTo<ChatTokenEvent>(OnChatToken);
        EventBus.SubscribeTo<ChatDoneEvent>(OnChatDone);
        EventBus.SubscribeTo<ChatStateChangedEvent>(OnChatStateChanged);
    }

    private void OnDisable()
    {
        if (_activeInstance == this)
            _activeInstance = null;

        EventBus.UnsubscribeFrom<ConfirmedInputEvent>(OnConfirmedInput);
        EventBus.UnsubscribeFrom<ChatTokenEvent>(OnChatToken);
        EventBus.UnsubscribeFrom<ChatDoneEvent>(OnChatDone);
        EventBus.UnsubscribeFrom<ChatStateChangedEvent>(OnChatStateChanged);
    }

    private void OnConfirmedInput(ref ConfirmedInputEvent e)
    {
        if (_inferring)
        {
            Debug.LogWarning("[ConversationManager] cannot send: already inferring.");
            return;
        }

        Debug.Log("In OnConfirmedInput");

        _history.Add(new ChatMessage { Role = "user", Content = e.Text });

        var turns = _accumulateHistory ? _history : _history.GetRange(_history.Count - 1, 1);

        ChatMessage[] messages;
        if (!string.IsNullOrWhiteSpace(_systemPrompt))
        {
            messages = new ChatMessage[turns.Count + 1];
            messages[0] = new ChatMessage { Role = "system", Content = _systemPrompt };
            turns.CopyTo(messages, 1);
        }
        else
        {
            messages = turns.ToArray();
        }
        EventBus.Raise(new ChatCommandEvent { Messages = messages });
    }

    private void OnChatToken(ref ChatTokenEvent e)
    {
        _replyBuffer.Append(e.Text);
    }

    private void OnChatDone(ref ChatDoneEvent e)
    {
        Debug.Log("In OnChatDone");

        if (_replyBuffer.Length > 0)
            _history.Add(new ChatMessage { Role = "assistant", Content = _replyBuffer.ToString() });
        _replyBuffer.Clear();
    }

    private void OnChatStateChanged(ref ChatStateChangedEvent e)
    {
        _inferring = e.State == ChatState.Inferring;
        if (e.State == ChatState.Error)
            _replyBuffer.Clear();
    }
}

}

// © 2025–2026, Stefan Webb. Some Rights Reserved.
// Licensed under CC BY-SA 4.0
//
// Handles speech input actions. Space starts STT; a confirm action accepts the
// transcription. Attach to the PlayerController object and wire up Inspector fields.

using UnityEngine;
using UnityEngine.InputSystem;

namespace GenerativeGamedev {

public class PlayerController : MonoBehaviour
{
    [SerializeField] private InputActionReference _speechStartAction;
    [SerializeField] private InputActionReference _confirmAction;
    [SerializeField] private GameObject _speechContent;
    [SerializeField] private GameObject _agentContent;

    private bool _hasPendingInput;
    private string _pendingText;
    private bool _streamingDone;

    private void OnEnable()
    {
        if (_speechStartAction != null)
        {
            _speechStartAction.action.Enable();
            _speechStartAction.action.performed += OnSpeechStart;
        }

        if (_confirmAction != null)
        {
            _confirmAction.action.Enable();
            _confirmAction.action.performed += OnConfirmInput;
        }

        EventBus.SubscribeTo<SpeechEndEvent>(OnSpeechEnd);
        EventBus.SubscribeTo<SpeechEvent>(OnSpeech);
        EventBus.SubscribeTo<ChatDoneEvent>(OnChatDone);
        EventBus.SubscribeTo<ChatStateChangedEvent>(OnChatStateChanged);
    }

    private void OnDisable()
    {
        Time.timeScale = 1f;
        if (_speechStartAction != null)
        {
            _speechStartAction.action.performed -= OnSpeechStart;
            _speechStartAction.action.Disable();
        }

        if (_confirmAction != null)
        {
            _confirmAction.action.performed -= OnConfirmInput;
            _confirmAction.action.Disable();
        }

        EventBus.UnsubscribeFrom<SpeechEndEvent>(OnSpeechEnd);
        EventBus.UnsubscribeFrom<SpeechEvent>(OnSpeech);
        EventBus.UnsubscribeFrom<ChatDoneEvent>(OnChatDone);
        EventBus.UnsubscribeFrom<ChatStateChangedEvent>(OnChatStateChanged);
    }

    private void OnSpeechStart(InputAction.CallbackContext ctx)
    {
        if (_agentContent != null && _agentContent.activeSelf) return;

        Time.timeScale = 0f;
        if (_speechContent != null)
            _speechContent.SetActive(true);
        EventBus.Raise(new StartCommandEvent());
    }

    private void OnSpeech(ref SpeechEvent e)
    {
        _pendingText = e.Text;
    }

    private void OnSpeechEnd(ref SpeechEndEvent e)
    {
        _hasPendingInput = true;
        EventBus.Raise(new PauseCommandEvent());
    }

    private void OnChatDone(ref ChatDoneEvent e) => _streamingDone = true;

    private void OnChatStateChanged(ref ChatStateChangedEvent e)
    {
        if (e.State != ChatState.Error) return;
        _streamingDone = false;
        _hasPendingInput = false;
        Time.timeScale = 1f;
        if (_speechContent != null) _speechContent.SetActive(false);
        if (_agentContent != null) _agentContent.SetActive(false);
    }

    private void OnConfirmInput(InputAction.CallbackContext ctx)
    {
        if (_hasPendingInput)
        {
            _hasPendingInput = false;
            var text = _pendingText;
            if (_speechContent != null) _speechContent.SetActive(false);
            if (_agentContent != null) _agentContent.SetActive(true);
            _streamingDone = false;
            EventBus.Raise(new ConfirmedInputEvent { Text = text });
        }
        else if (_streamingDone)
        {
            _streamingDone = false;
            Time.timeScale = 1f;
            if (_agentContent != null) _agentContent.SetActive(false);
        }
    }
}

}

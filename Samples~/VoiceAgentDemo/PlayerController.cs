// © 2025–2026, Stefan Webb. Some Rights Reserved.
// Licensed under CC BY-SA 4.0
//
// Handles speech input actions. Space starts STT; a confirm action accepts the
// transcription. Attach to the PlayerController object and wire up Inspector fields.
//
// Actions are resolved by name from the InputActionAsset on every OnEnable rather
// than through cached InputActionReferences: with "Enter Play Mode Options" /
// domain reload disabled (the Unity 6 default), a reference's cached InputAction
// can outlive the Input System's play-mode reset and its callbacks stop firing.
// The InputActionReference fields remain as a fallback.

using UnityEngine;
using UnityEngine.InputSystem;

namespace GenerativeGamedev {

public class PlayerController : MonoBehaviour
{
    [SerializeField] private InputActionAsset _actions;
    [SerializeField] private string _speechStartActionName = "Player/StartSpeech";
    [SerializeField] private string _confirmActionName = "Player/ConfirmInput";
    [Tooltip("Fallback used only if the name lookup above fails.")]
    [SerializeField] private InputActionReference _speechStartAction;
    [Tooltip("Fallback used only if the name lookup above fails.")]
    [SerializeField] private InputActionReference _confirmAction;
    [SerializeField] private GameObject _speechContent;
    [SerializeField] private GameObject _agentContent;

    private InputAction _speechStart;
    private InputAction _confirm;
    private bool _hasPendingInput;
    private string _pendingText;
    private bool _streamingDone;

    private InputAction ResolveAction(string name, InputActionReference fallback)
    {
        var action = _actions != null ? _actions.FindAction(name) : null;
        if (action == null && fallback != null)
            action = fallback.action;
        if (action == null)
            Debug.LogError($"[PlayerController] could not resolve input action '{name}': " +
                "assign an InputActionAsset containing it, or an InputActionReference fallback.", this);
        return action;
    }

    private void OnEnable()
    {
        _speechStart = ResolveAction(_speechStartActionName, _speechStartAction);
        if (_speechStart != null)
        {
            _speechStart.Enable();
            _speechStart.performed += OnSpeechStart;
        }

        _confirm = ResolveAction(_confirmActionName, _confirmAction);
        if (_confirm != null)
        {
            _confirm.Enable();
            _confirm.performed += OnConfirmInput;
        }

        EventBus.SubscribeTo<SpeechEndEvent>(OnSpeechEnd);
        EventBus.SubscribeTo<SpeechEvent>(OnSpeech);
        EventBus.SubscribeTo<ChatDoneEvent>(OnChatDone);
        EventBus.SubscribeTo<ChatStateChangedEvent>(OnChatStateChanged);
    }

    private void OnDisable()
    {
        Time.timeScale = 1f;
        if (_speechStart != null)
        {
            _speechStart.performed -= OnSpeechStart;
            _speechStart.Disable();
            _speechStart = null;
        }

        if (_confirm != null)
        {
            _confirm.performed -= OnConfirmInput;
            _confirm.Disable();
            _confirm = null;
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

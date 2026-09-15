// © 2025–2026, Stefan Webb. Some Rights Reserved.
// Licensed under CC BY-SA 4.0
//
// Swaps a RawImage's texture to reflect STT connection status and
// last-known server state, resizing to each icon's native size. Also shows
// a thinking icon while the chat server is inferring a response. Attach
// directly to the GameObject holding the RawImage component. Pure
// EventBus consumer — no dependency on SttClient itself.

using UnityEngine;
using UnityEngine.UI;

namespace GenerativeGamedev {

[RequireComponent(typeof(RawImage))]
public class SttStateIcon : MonoBehaviour
{
    [SerializeField] private CanvasGroup _container;
    [SerializeField] private Texture2D _listeningIcon;
    [SerializeField] private Texture2D _disconnectedIcon;
    [SerializeField] private Texture2D _speakingIcon;
    [SerializeField] private Texture2D _notReadyIcon;
    [SerializeField] private Texture2D _speechEndedIcon;
    [SerializeField] private Texture2D _errorIcon;
    [SerializeField] private Texture2D _thinkingIcon;

    private RawImage _rawImage;
    private bool _connected;
    private bool _speechEnded;
    private SttState _state = SttState.Paused;
    private ChatState _chatState = ChatState.Idle;

    private void Awake() => _rawImage = GetComponent<RawImage>();

    private void OnEnable()
    {
        EventBus.SubscribeTo<SttConnectionChangedEvent>(OnConnectionChanged);
        EventBus.SubscribeTo<StateChangedEvent>(OnStateChanged);
        EventBus.SubscribeTo<SpeechEndEvent>(OnSpeechEnd);
        EventBus.SubscribeTo<SpeechStartEvent>(OnSpeechStart);
        EventBus.SubscribeTo<ChatStateChangedEvent>(OnChatStateChanged);
        UpdateIcon();
    }

    private void OnDisable()
    {
        EventBus.UnsubscribeFrom<SttConnectionChangedEvent>(OnConnectionChanged);
        EventBus.UnsubscribeFrom<StateChangedEvent>(OnStateChanged);
        EventBus.UnsubscribeFrom<SpeechEndEvent>(OnSpeechEnd);
        EventBus.UnsubscribeFrom<SpeechStartEvent>(OnSpeechStart);
        EventBus.UnsubscribeFrom<ChatStateChangedEvent>(OnChatStateChanged);
    }

    private void OnConnectionChanged(ref SttConnectionChangedEvent e)
    {
        _connected = e.Connected;
        UpdateIcon();
    }

    private void OnStateChanged(ref StateChangedEvent e)
    {
        _state = e.State;
        _speechEnded = false;
        UpdateIcon();
    }

    private void OnSpeechEnd(ref SpeechEndEvent e)
    {
        _speechEnded = true;
        UpdateIcon();
    }

    private void OnSpeechStart(ref SpeechStartEvent e)
    {
        _speechEnded = false;
        UpdateIcon();
    }

    private void OnChatStateChanged(ref ChatStateChangedEvent e)
    {
        _chatState = e.State;
        UpdateIcon();
    }

    private void UpdateIcon()
    {
        var visible = !_connected || _state != SttState.Paused || _chatState == ChatState.Inferring;
        if (_container != null)
        {
            _container.alpha = visible ? 1f : 0f;
            _container.blocksRaycasts = visible;
        }
        if (!visible) return;

        if (_chatState == ChatState.Inferring)
        {
            _rawImage.texture = _thinkingIcon;
            if (_thinkingIcon != null)
                _rawImage.rectTransform.sizeDelta = new Vector2(_thinkingIcon.width, _thinkingIcon.height);
            return;
        }

        var icon = !_connected ? _disconnectedIcon : _speechEnded ? _speechEndedIcon : _state switch
        {
            SttState.Loading => _notReadyIcon,
            SttState.Ready => _notReadyIcon,
            SttState.Listening => _listeningIcon,
            SttState.Transcribing => _speakingIcon,
            SttState.Error => _errorIcon,
            _ => _disconnectedIcon,
        };

        _rawImage.texture = icon;
        if (icon != null)
            _rawImage.rectTransform.sizeDelta = new Vector2(icon.width, icon.height);
    }
}

}

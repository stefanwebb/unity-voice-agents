// © 2025–2026, Stefan Webb. Some Rights Reserved.
// Licensed under CC BY-SA 4.0
//
// Shows the live STT transcription in a TextMeshPro UI text box. Attach
// directly to the GameObject holding the TextMeshProUGUI component. Pure
// EventBus consumer — no connection-state awareness, Play-mode only.
// See docs/superpowers/specs/2026-06-24-stt-transcription-display-design.md.

using TMPro;
using UnityEngine;

namespace GenerativeGamedev {

[RequireComponent(typeof(TextMeshProUGUI))]
public class SttTranscriptionDisplay : MonoBehaviour
{
    private TextMeshProUGUI _text;

    private void Awake() => _text = GetComponent<TextMeshProUGUI>();

    private void OnEnable()
    {
        _text.text = "";
        EventBus.SubscribeTo<SpeechStartEvent>(OnSpeechStart);
        EventBus.SubscribeTo<SpeechEvent>(OnSpeech);
    }

    private void OnDisable()
    {
        EventBus.UnsubscribeFrom<SpeechStartEvent>(OnSpeechStart);
        EventBus.UnsubscribeFrom<SpeechEvent>(OnSpeech);
    }

    private void OnSpeechStart(ref SpeechStartEvent e) => _text.text = " ";

    private void OnSpeech(ref SpeechEvent e) => _text.text = e.Text.Trim();
}

}

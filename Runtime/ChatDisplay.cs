// © 2025–2026, Stefan Webb. Some Rights Reserved.
// Licensed under CC BY-SA 4.0
//
// Shows streaming LLM replies in a TextMeshPro text field. Clears on each
// new ChatCommandEvent and appends tokens as they arrive via ChatTokenEvent.
// Attach to the GameObject holding the TextMeshProUGUI component.

using TMPro;
using UnityEngine;

namespace GenerativeGamedev {

[RequireComponent(typeof(TextMeshProUGUI))]
public class ChatDisplay : MonoBehaviour
{
    private TextMeshProUGUI _text;

    private void Awake() => _text = GetComponent<TextMeshProUGUI>();

    private void OnEnable()
    {
        _text.text = "";
        EventBus.SubscribeTo<ChatCommandEvent>(OnChatCommand);
        EventBus.SubscribeTo<ChatTokenEvent>(OnChatToken);
    }

    private void OnDisable()
    {
        EventBus.UnsubscribeFrom<ChatCommandEvent>(OnChatCommand);
        EventBus.UnsubscribeFrom<ChatTokenEvent>(OnChatToken);
    }

    private void OnChatCommand(ref ChatCommandEvent e) => _text.text = "";

    private void OnChatToken(ref ChatTokenEvent e) => _text.text += e.Text;
}

}

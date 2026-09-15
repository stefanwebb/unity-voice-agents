// © 2025–2026, Stefan Webb. Some Rights Reserved.
// Licensed under CC BY-SA 4.0
//
// Global chat client: connects to the named_pipes.chat server over ToolClient,
// forwards every server event onto EventBus as a typed struct, and forwards
// ChatCommandEvent on EventBus to the server as a 'chat' command. Place this
// on a GameObject in your scene; configure it from the Inspector.

using System;
using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using System.Threading;
using UnityEngine;

namespace GenerativeGamedev {

public class ChatClient : MonoBehaviour
{
    private static ChatClient _activeInstance;

    [SerializeField] private string _toolName = "chat";
    [SerializeField] private float _reconnectIntervalSeconds = 3f;
    [SerializeField] private float _pingIntervalSeconds = 5f;
    [SerializeField] private float _pongTimeoutSeconds = 10f;

    [SerializeField] private bool _isConnectedDebug;

    private readonly ConcurrentQueue<Action> _pending = new();
    private readonly object _clientLock = new();
    private ToolClient _client;
    private CancellationTokenSource _cts;
    private volatile bool _connected;
    private volatile ChatState _lastKnownState = ChatState.Idle;

    private void OnEnable()
    {
        if (_activeInstance != null)
        {
            Debug.LogError($"[ChatClient] another ChatClient ('{_activeInstance.name}') is already active; " +
                "disabling this one to avoid two connections corrupting the same named pipe.");
            enabled = false;
            return;
        }
        _activeInstance = this;

        EventBus.SubscribeTo<ChatCommandEvent>(OnChatCommand);

        _cts = new CancellationTokenSource();
        new Thread(ConnectLoop) { IsBackground = true, Name = "ChatClientConnect" }.Start();
    }

    private void OnDisable()
    {
        if (_activeInstance == this)
            _activeInstance = null;

        EventBus.UnsubscribeFrom<ChatCommandEvent>(OnChatCommand);

        ShutdownClient();
    }

    private void OnApplicationQuit() => ShutdownClient();

    private void OnChatCommand(ref ChatCommandEvent e)
    {
        if (_lastKnownState == ChatState.Inferring)
        {
            Debug.LogWarning("[ChatClient] cannot send 'chat': already inferring.");
            return;
        }

        if (!_connected)
        {
            Debug.LogWarning("[ChatClient] cannot send 'chat': not connected.");
            return;
        }

        var messages = new JsonArray();
        if (e.Messages != null)
        {
            foreach (var msg in e.Messages)
                messages.Add(new JsonObject { ["role"] = msg.Role, ["content"] = msg.Content });
        }

        SendCommand("chat", new JsonObject { ["messages"] = messages });
        _lastKnownState = ChatState.Inferring;
        EventBus.Raise(new ChatStateChangedEvent { State = ChatState.Inferring });
    }

    private void SendCommand(string cmd, JsonObject extra = null)
    {
        ToolClient client;
        lock (_clientLock)
        {
            client = _client;
        }

        if (client == null)
        {
            Debug.LogWarning($"[ChatClient] cannot send '{cmd}': not connected.");
            return;
        }

        try
        {
            client.SendCommand(cmd, extra);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[ChatClient] cannot send '{cmd}': {e.Message}");
        }
    }

    private void Update()
    {
        while (_pending.TryDequeue(out var action))
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        _isConnectedDebug = _connected;
    }

    private void Enqueue(Action action) => _pending.Enqueue(action);

    private void ConnectLoop()
    {
        while (!_cts.IsCancellationRequested)
        {
            ManualResetEventSlim done = null;
            try
            {
                done = Connect();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ChatClient] connect attempt failed: {e.Message}");
            }

            if (done != null)
            {
                done.Wait();
                _connected = false;
                lock (_clientLock) { _client = null; }

                var wasInferring = _lastKnownState == ChatState.Inferring;
                _lastKnownState = ChatState.Idle;

                if (!_cts.IsCancellationRequested)
                {
                    Enqueue(() => EventBus.Raise(new ChatConnectionChangedEvent { Connected = false }));
                    if (wasInferring)
                        Enqueue(() => EventBus.Raise(new ChatStateChangedEvent { State = ChatState.Error }));
                }
            }

            if (_cts.IsCancellationRequested) break;
            Thread.Sleep(TimeSpan.FromSeconds(_reconnectIntervalSeconds));
        }
    }

    private ManualResetEventSlim Connect()
    {
        var client = new ToolClient(_toolName);
        using var disposeOnCancel = _cts.Token.Register(client.Dispose);

        var pongReceived = new ManualResetEventSlim(false);
        client.On("pong", data => pongReceived.Set());

        client.On("token", data =>
        {
            var text = data["text"]?.GetValue<string>() ?? "";
            var isDone = data["done"]?.GetValue<bool>() ?? false;

            if (isDone)
            {
                _lastKnownState = ChatState.Idle;
                Enqueue(() =>
                {
                    EventBus.Raise(new ChatDoneEvent());
                    EventBus.Raise(new ChatStateChangedEvent { State = ChatState.Idle });
                });
            }
            else
            {
                Enqueue(() => EventBus.Raise(new ChatTokenEvent { Text = text }));
            }
        });

        var done = client.StartListening();
        client.Subscribe();

        lock (_clientLock) { _client = client; }
        _connected = true;
        _lastKnownState = ChatState.Idle;
        Enqueue(() =>
        {
            EventBus.Raise(new ChatConnectionChangedEvent { Connected = true });
            EventBus.Raise(new ChatStateChangedEvent { State = ChatState.Idle });
        });

        new Thread(() => HeartbeatLoop(client, done, pongReceived))
        {
            IsBackground = true,
            Name = "ChatClientHeartbeat",
        }.Start();

        return done;
    }

    // See SttClient.HeartbeatLoop for rationale: named pipe FIFOs opened O_RDWR
    // never see EOF when the server closes, so active pings are the only way to
    // detect a dead server.
    private void HeartbeatLoop(ToolClient client, ManualResetEventSlim done, ManualResetEventSlim pongReceived)
    {
        var pingIntervalMs = (int)(_pingIntervalSeconds * 1000);
        var pongTimeoutMs = (int)(_pongTimeoutSeconds * 1000);
        var handles = new WaitHandle[] { done.WaitHandle, pongReceived.WaitHandle };

        while (!_cts.IsCancellationRequested)
        {
            if (WaitHandle.WaitAny(handles, pingIntervalMs) == 0) return;

            pongReceived.Reset();
            try
            {
                client.SendCommand("ping");
            }
            catch (Exception)
            {
                return;
            }

            var signaled = WaitHandle.WaitAny(handles, pongTimeoutMs);
            if (signaled == 0) return;
            if (signaled == WaitHandle.WaitTimeout)
            {
                client.Dispose();
                return;
            }
        }
    }

    private void ShutdownClient()
    {
        _cts?.Cancel();

        ToolClient client;
        lock (_clientLock)
        {
            client = _client;
            _client = null;
        }

        if (client == null) return;

        try { client.Unsubscribe(); } catch (Exception e) { Debug.LogException(e); }
        client.Dispose();
    }
}

}

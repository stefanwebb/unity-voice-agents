// © 2025–2026, Stefan Webb. Some Rights Reserved.
// Licensed under CC BY-SA 4.0
//
// Global STT client: connects to the named_pipes.stt server over ToolClient,
// forwards every server event onto EventBus as a typed struct, and forwards
// every *CommandEvent on EventBus to the server as a command. Place this on
// a GameObject in your scene; configure it from the Inspector. Runs in both
// Editor mode and Play mode ([ExecuteAlways]) so you can connect, pick a
// device, and see live status without pressing Play.
// See docs/superpowers/specs/2026-06-23-stt-client-editor-mode-design.md
// and docs/superpowers/specs/2026-06-23-stt-pause-on-exit-play-design.md.

using System;
using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using System.Threading;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace GenerativeGamedev {

[ExecuteAlways]
public class SttClient : MonoBehaviour
{
    private static SttClient _activeInstance;

    [SerializeField] private string _toolName = "stt";
    [SerializeField] private float _reconnectIntervalSeconds = 3f;
    [SerializeField] private float _pingIntervalSeconds = 5f;
    [SerializeField] private float _pongTimeoutSeconds = 10f;

    [SerializeField] private bool _isConnectedDebug;

    public AudioDevice[] Devices { get; private set; } = Array.Empty<AudioDevice>();
    public int? SelectedDevice { get; private set; }

    private readonly ConcurrentQueue<Action> _pending = new();
    private readonly object _clientLock = new();
    private ToolClient _client;
    private CancellationTokenSource _cts;
    private volatile bool _connected;
    private volatile SttState _lastKnownState = SttState.Ready;

    private void OnEnable()
    {
        if (_activeInstance != null)
        {
            Debug.LogError($"[SttClient] another SttClient ('{_activeInstance.name}') is already active; " +
                "disabling this one to avoid two connections corrupting the same named pipe.");
            enabled = false;
            return;
        }
        _activeInstance = this;

        EventBus.SubscribeTo<StartCommandEvent>(OnStartCommand);
        EventBus.SubscribeTo<PauseCommandEvent>(OnPauseCommand);
        EventBus.SubscribeTo<ListDevicesCommandEvent>(OnListDevicesCommand);
        EventBus.SubscribeTo<GetDeviceCommandEvent>(OnGetDeviceCommand);
        EventBus.SubscribeTo<SetDeviceCommandEvent>(OnSetDeviceCommand);
        EventBus.SubscribeTo<DevicesEvent>(OnDevicesEvent);
        EventBus.SubscribeTo<DeviceEvent>(OnDeviceEvent);
        EventBus.SubscribeTo<StateChangedEvent>(OnStateChangedEvent);

#if UNITY_EDITOR
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
#endif

        _cts = new CancellationTokenSource();
        new Thread(ConnectLoop) { IsBackground = true, Name = "SttClientConnect" }.Start();
    }

    private void OnDisable()
    {
        if (_activeInstance == this)
            _activeInstance = null;

        EventBus.UnsubscribeFrom<StartCommandEvent>(OnStartCommand);
        EventBus.UnsubscribeFrom<PauseCommandEvent>(OnPauseCommand);
        EventBus.UnsubscribeFrom<ListDevicesCommandEvent>(OnListDevicesCommand);
        EventBus.UnsubscribeFrom<GetDeviceCommandEvent>(OnGetDeviceCommand);
        EventBus.UnsubscribeFrom<SetDeviceCommandEvent>(OnSetDeviceCommand);
        EventBus.UnsubscribeFrom<DevicesEvent>(OnDevicesEvent);
        EventBus.UnsubscribeFrom<DeviceEvent>(OnDeviceEvent);
        EventBus.UnsubscribeFrom<StateChangedEvent>(OnStateChangedEvent);

#if UNITY_EDITOR
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
#endif

        ShutdownClient();
    }

    private void OnApplicationQuit() => ShutdownClient();

    private void OnStartCommand(ref StartCommandEvent e) => SendCommand("start");

    private void OnPauseCommand(ref PauseCommandEvent e) => SendCommand("pause");

    private void OnListDevicesCommand(ref ListDevicesCommandEvent e) => SendCommand("list_devices");

    private void OnGetDeviceCommand(ref GetDeviceCommandEvent e) => SendCommand("get_device");

    private void OnSetDeviceCommand(ref SetDeviceCommandEvent e) =>
        SendCommand("set_device", new JsonObject { ["device"] = e.Device });

    private void OnDevicesEvent(ref DevicesEvent e) => Devices = e.Devices;

    private void OnDeviceEvent(ref DeviceEvent e) => SelectedDevice = e.Device;

    private void OnStateChangedEvent(ref StateChangedEvent e) => _lastKnownState = e.State;

#if UNITY_EDITOR
    private void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingPlayMode)
        {
            if (_lastKnownState == SttState.Listening || _lastKnownState == SttState.Transcribing)
                SendCommand("pause");
        }
    }
#endif

    private void SendCommand(string cmd, JsonObject extra = null)
    {
        ToolClient client;
        lock (_clientLock)
        {
            client = _client;
        }

        if (client == null)
        {
            Debug.LogWarning($"[SttClient] cannot send '{cmd}': not connected.");
            return;
        }

        try
        {
            client.SendCommand(cmd, extra);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[SttClient] cannot send '{cmd}': {e.Message}");
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
                Debug.LogWarning($"[SttClient] connect attempt failed: {e.Message}");
            }

            if (done != null)
            {
                done.Wait();
                _connected = false;
                lock (_clientLock) { _client = null; }
                _lastKnownState = SttState.Ready;
                if (!_cts.IsCancellationRequested)
                    Enqueue(() => EventBus.Raise(new SttConnectionChangedEvent { Connected = false }));
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

        client.On("speech_start", data => Enqueue(() => EventBus.Raise(new SpeechStartEvent())));
        client.On("speech_end", data => Enqueue(() => EventBus.Raise(new SpeechEndEvent())));
        client.On("token", data => Enqueue(() => EventBus.Raise(new TokenEvent
        {
            Text = data["text"]?.GetValue<string>() ?? "",
        })));
        client.On("speech", data => Enqueue(() => EventBus.Raise(ParseSpeechEvent(data))));
        client.On("state_changed", data => Enqueue(() => EventBus.Raise(new StateChangedEvent
        {
            State = ParseState(data["state"]?.GetValue<string>()),
        })));
        client.On("devices", data => Enqueue(() => EventBus.Raise(ParseDevicesEvent(data))));
        client.On("device", data => Enqueue(() => EventBus.Raise(new DeviceEvent
        {
            Device = data["device"]?.GetValue<int?>(),
        })));

        var done = client.StartListening();
        client.Subscribe();

        lock (_clientLock) { _client = client; }
        _connected = true;
        Enqueue(() => EventBus.Raise(new SttConnectionChangedEvent { Connected = true }));

        SendCommand("list_devices");
        SendCommand("get_device");

        new Thread(() => HeartbeatLoop(client, done, pongReceived))
        {
            IsBackground = true,
            Name = "SttClientHeartbeat",
        }.Start();

        return done;
    }

    // The server never sends EOF/an error when its end of the pipe closes —
    // ToolClient opens both FIFOs O_RDWR, which makes its own file descriptor
    // count as a writer/reader too, so a blocking read never sees the "all
    // writers closed" condition. Without an active liveness probe, a dead
    // server is indistinguishable from a quiet one. This loop pings on the
    // configured interval and force-closes the connection if no pong arrives
    // within the configured timeout; once closed, ConnectLoop's done.Wait()
    // unblocks and reconnects normally.
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

    private static SttState ParseState(string state) => state switch
    {
        "ready" => SttState.Ready,
        "loading" => SttState.Loading,
        "listening" => SttState.Listening,
        "transcribing" => SttState.Transcribing,
        "paused" => SttState.Paused,
        _ => SttState.Error,
    };

    private static SpeechEvent ParseSpeechEvent(JsonObject data)
    {
        var wordsNode = data["words"]?.AsArray();
        AlignedWord[] words = null;

        if (wordsNode != null)
        {
            words = new AlignedWord[wordsNode.Count];
            for (var i = 0; i < wordsNode.Count; i++)
            {
                var w = wordsNode[i].AsObject();
                words[i] = new AlignedWord
                {
                    Word = w["word"]?.GetValue<string>() ?? "",
                    Start = w["start"]?.GetValue<double>() ?? 0,
                    End = w["end"]?.GetValue<double>() ?? 0,
                };
            }
        }

        return new SpeechEvent
        {
            Text = data["text"]?.GetValue<string>() ?? "",
            Words = words,
        };
    }

    private static DevicesEvent ParseDevicesEvent(JsonObject data)
    {
        var devicesNode = data["devices"]?.AsArray();
        var devices = new AudioDevice[devicesNode?.Count ?? 0];

        if (devicesNode != null)
        {
            for (var i = 0; i < devicesNode.Count; i++)
            {
                var d = devicesNode[i].AsObject();
                devices[i] = new AudioDevice
                {
                    Index = d["index"]?.GetValue<int>() ?? 0,
                    Name = d["name"]?.GetValue<string>() ?? "",
                    Channels = d["channels"]?.GetValue<int>() ?? 0,
                };
            }
        }

        return new DevicesEvent { Devices = devices };
    }
}

}

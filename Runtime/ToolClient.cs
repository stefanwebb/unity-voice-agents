// © 2025–2026, Stefan Webb. Some Rights Reserved.
// Licensed under CC BY-SA 4.0
//
// C# client for the Named Pipe Tools protocol.
// Mirrors the Python ToolClient in the named_pipes package (src/named_pipes/tool_client.py).

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;

namespace GenerativeGamedev {

public class EventReceivedEventArgs : EventArgs
{
    public string Event { get; }
    public JsonObject Data { get; }

    public EventReceivedEventArgs(string @event, JsonObject data)
    {
        Event = @event;
        Data = data;
    }
}

/// <summary>
/// C# client for the Named Pipe Tools protocol.
///
/// Connects to a ToolServer at /tmp/tool-{name} and receives events on a
/// per-process downstream pipe at /tmp/tool-{name}-{pid}.
///
/// Typical usage:
///   using var client = new ToolClient("demo");
///   client.On("pong", _ => Console.WriteLine("pong"));
///   client.StartListening();
///   client.Subscribe();
///   client.SendCommand("ping");
///   client.Unsubscribe();
/// </summary>
public class ToolClient : IDisposable
{
    [DllImport("libc", SetLastError = true)]
    private static extern int mkfifo(string pathname, uint mode);

    [DllImport("libc", SetLastError = true)]
    private static extern int unlink(string pathname);

    private readonly string _downstreamPath;
    private readonly int _pid;

    private readonly StreamWriter _writer;
    private readonly StreamReader _reader;
    private readonly object _writeLock = new();

#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
    private Thread? _listenerThread;
#pragma warning restore CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
    private readonly ManualResetEventSlim _subscribedEvent = new(false);
    private readonly ManualResetEventSlim _done = new(false);
    private bool _disposed;

    // Closing _reader from another thread does not reliably interrupt a
    // concurrent blocking ReadLine() on the listener thread (verified
    // empirically: an in-progress blocking read on a FIFO is not woken by a
    // same-process close of its file descriptor on this platform). Instead,
    // Dispose() wakes the listener by writing this line into the downstream
    // pipe itself (already open O_RDWR), which the listener recognizes and
    // exits on, the same as a normal EOF.
    private const string ShutdownSentinel = "__tool_client_shutdown__";

    private readonly Dictionary<string, Action<JsonObject>> _handlers = new();

    /// <summary>Fired for every event received from the server (except "subscribed").</summary>
#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
    public event EventHandler<EventReceivedEventArgs>? EventReceived;
#pragma warning restore CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.

    public ToolClient(string name)
    {
        var pipeName = $"/tmp/tool-{name}";
        _pid = Process.GetCurrentProcess().Id;
        _downstreamPath = $"{pipeName}-{_pid}";

        // Create the per-client downstream FIFO for server → client messages.
        unlink(_downstreamPath);  // remove stale pipe if present
        if (mkfifo(_downstreamPath, 0x1B6) != 0)  // 0o666 = rw-rw-rw-
            throw new IOException($"mkfifo failed for {_downstreamPath}");

        // Open downstream FIFO O_RDWR (FileAccess.ReadWrite) so the open does
        // not block waiting for a writer — the server opens its write end later
        // when it processes our subscribe command.
        _reader = new StreamReader(new FileStream(
            _downstreamPath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite));

        // Open upstream FIFO O_RDWR — the server already has it open O_RDWR,
        // so this does not block either.
        _writer = new StreamWriter(new FileStream(
            pipeName, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite))
        { AutoFlush = true };
    }

    // --- event handler registration ---

    /// <summary>Register a handler called whenever the server sends the named event.</summary>
    public void On(string eventName, Action<JsonObject> handler) =>
        _handlers[eventName] = handler;

    // --- sending ---

    /// <summary>Send {"pid": ..., "cmd": cmd, ...extra} to the server.</summary>
#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
    public void SendCommand(string cmd, JsonObject? extra = null)
#pragma warning restore CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
    {
        var payload = new JsonObject { ["pid"] = _pid, ["cmd"] = cmd };
        if (extra is not null)
            foreach (var prop in extra)
                payload[prop.Key] = prop.Value?.DeepClone();
        lock (_writeLock)
            _writer.WriteLine(payload.ToJsonString());
    }

    /// <summary>Send subscribe and block until the server confirms.</summary>
    public void Subscribe()
    {
        SendCommand("subscribe");
        _subscribedEvent.Wait();
    }

    /// <summary>Send unsubscribe (no response expected).</summary>
    public void Unsubscribe() => SendCommand("unsubscribe");

    // --- background listener ---

    /// <summary>
    /// Start the background listener thread.
    /// Returns a ManualResetEventSlim that is set when the listener exits.
    /// </summary>
    public ManualResetEventSlim StartListening()
    {
        _done.Reset();
        _listenerThread = new Thread(ListenerLoop)
        {
            IsBackground = true,
            Name = "ToolClientListener",
        };
        _listenerThread.Start();
        return _done;
    }

    private void ListenerLoop()
    {
        try
        {
            while (true)
            {
                var line = _reader.ReadLine();
                if (line is null) break;
                if (line == ShutdownSentinel) break;

                var obj = JsonNode.Parse(line)?.AsObject();
                if (obj is null) continue;
                var eventName = obj["event"]?.GetValue<string>() ?? "";

                if (eventName == "subscribed")
                {
                    _subscribedEvent.Set();
                    continue;
                }

                if (_handlers.TryGetValue(eventName, out var handler))
                    handler(obj);

                EventReceived?.Invoke(this, new EventReceivedEventArgs(eventName, obj));
            }
        }
        catch (ObjectDisposedException) { }
        catch (IOException) { }
        finally
        {
            _done.Set();
        }
    }

    // --- cleanup ---

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try
        {
            // Write through the same underlying stream _reader wraps (opened
            // O_RDWR) so the sentinel actually wakes the listener thread's
            // blocking read — see ShutdownSentinel's comment for why a plain
            // Close() from this thread cannot be relied on to do that. Write
            // raw bytes (not a StreamWriter) so nothing here disposes the
            // shared stream out from under the listener thread.
            var bytes = Encoding.UTF8.GetBytes(ShutdownSentinel + "\n");
            _reader.BaseStream.Write(bytes, 0, bytes.Length);
            _reader.BaseStream.Flush();
        }
        catch { }
        _listenerThread?.Join(TimeSpan.FromSeconds(2));
        try { _writer.Dispose(); } catch { }
        try { _reader.Dispose(); } catch { }
        unlink(_downstreamPath);               // delete the downstream FIFO we created
    }
}

}

# SttClient Editor-Mode Connection & Device Dropdown — Design

Date: 2026-06-23

## Goal

Make `SttClient` connect to the `named_pipes.stt` server in Unity Editor mode (not just Play mode), auto-populate a device dropdown in its Inspector whenever a connection is (re)established, and automatically send a `start` command when entering Play mode.

## Background

- `SttClient` (`Assets/Scripts/SttClient.cs`) is currently a plain `MonoBehaviour`. Unity only calls `OnEnable`/`OnDisable`/`Update` for a plain `MonoBehaviour` while in Play mode — none of its connection logic runs in Edit mode today.
- `SttClient` already forwards `DevicesEvent`/`DeviceEvent` onto `EventBus` (raised from server responses to `list_devices`/`get_device`), but doesn't retain the values itself.
- The project already has a precedent for Editor-only code living in an `Editor` folder, auto-compiled into a separate `Assembly-CSharp-Editor` assembly: `Assets/TutorialInfo/Scripts/Editor/ReadmeEditor.cs`.
- `EventBus.cs` already has precedent for `#if UNITY_EDITOR`-guarded logic inside a runtime script (its domain-reload guard in `Init()`).

## Decisions

- **Editor-mode connection**: add `[ExecuteAlways]` to the `SttClient` class. This is the standard Unity mechanism for running `MonoBehaviour` lifecycle methods (`OnEnable`, `OnDisable`, `Update`) in both Edit and Play mode. It also resolves "keep `_isConnectedDebug` updated in editor mode" for free, since `Update()` already maintains that field.
- **Device dropdown**: lives in a `[CustomEditor(typeof(SttClient))]` script, not the default Inspector (plain serialized fields can't render a dynamic dropdown). New file `Assets/Scripts/Editor/SttClientEditor.cs`, compiled into the existing `Assembly-CSharp-Editor` assembly via Unity's `Editor`-folder convention.
- **Dropdown data source**: both `list_devices` (populates the dropdown's options) and `get_device` (pre-selects the currently active one) — sent automatically every time a connection succeeds, not just once.
- **Start-on-Play timing**: wait for the connection rather than firing immediately. Since Unity's default "Enter Play Mode Options" reload the scene, the Editor-mode connection is torn down and a fresh one starts when Play begins; sending `start` immediately could race ahead of that reconnect. Instead, remember the intent and send `start` as soon as the next successful connection completes.
- **Documented assumption**: this relies on Unity's default Enter Play Mode behavior (both Domain Reload and Scene Reload enabled). Not handling the case where a user has disabled those — same scope decision already made for the existing multi-instance guard not handling an abnormal Editor crash.

## Architecture

### `Assets/Scripts/SttClient.cs` (modified)

```csharp
[ExecuteAlways]
public class SttClient : MonoBehaviour
{
    // ... existing fields unchanged ...

    public AudioDevice[] Devices { get; private set; } = Array.Empty<AudioDevice>();
    public int? SelectedDevice { get; private set; }

    private volatile bool _sendStartOnConnect;
```

- `OnEnable()`: additionally subscribes to `EventBus`'s `DevicesEvent` and `DeviceEvent`, and (wrapped `#if UNITY_EDITOR`) to `UnityEditor.EditorApplication.playModeStateChanged`.
- `OnDisable()`: unsubscribes the same three.
- New handlers (run on the main thread, same as every other inbound event — arrive via the existing `Enqueue` + `Update()` drain):
  ```csharp
  private void OnDevicesEvent(ref DevicesEvent e) => Devices = e.Devices;
  private void OnDeviceEvent(ref DeviceEvent e) => SelectedDevice = e.Device;
  ```
- In `Connect()`, immediately after the existing `_connected = true; Enqueue(() => EventBus.Raise(new SttConnectionChangedEvent { Connected = true }));`:
  ```csharp
  SendCommand("list_devices");
  SendCommand("get_device");

  if (_sendStartOnConnect)
  {
      _sendStartOnConnect = false;
      SendCommand("start");
  }
  ```
  These are direct, synchronous `SendCommand` calls (not enqueued) — sending a command is just a pipe write via `ToolClient`, not a Unity API call, the same reasoning already used for the heartbeat's direct `client.SendCommand("ping")` from a background thread.
- New Editor-only block:
  ```csharp
  #if UNITY_EDITOR
  private void OnPlayModeStateChanged(UnityEditor.PlayModeStateChange state)
  {
      if (state != UnityEditor.PlayModeStateChange.EnteredPlayMode) return;

      if (_connected)
          SendCommand("start");
      else
          _sendStartOnConnect = true;
  }
  #endif
  ```

### `Assets/Scripts/Editor/SttClientEditor.cs` (new)

```csharp
using UnityEditor;
using UnityEngine;

namespace GenerativeGamedev {

[CustomEditor(typeof(SttClient))]
public class SttClientEditor : Editor
{
    private void OnEnable() => EditorApplication.update += Repaint;

    private void OnDisable() => EditorApplication.update -= Repaint;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var sttClient = (SttClient)target;
        var devices = sttClient.Devices;

        EditorGUILayout.Space();

        if (devices == null || devices.Length == 0)
        {
            EditorGUILayout.HelpBox("No devices known yet.", MessageType.Info);
            return;
        }

        var names = new string[devices.Length];
        var currentSelection = -1;
        for (var i = 0; i < devices.Length; i++)
        {
            names[i] = $"[{devices[i].Index}] {devices[i].Name}";
            if (sttClient.SelectedDevice.HasValue && devices[i].Index == sttClient.SelectedDevice.Value)
                currentSelection = i;
        }

        var newSelection = EditorGUILayout.Popup("Device", currentSelection, names);
        if (newSelection != currentSelection && newSelection >= 0)
        {
            EventBus.Raise(new SetDeviceCommandEvent { Device = devices[newSelection].Index.ToString() });
        }
    }
}

}
```

`EditorApplication.update += Repaint` is the standard pattern for an Inspector that needs to reflect plain (non-`SerializedProperty`) runtime data changing on its own — without it, the dropdown would only refresh when something else triggers a repaint (e.g. mouse movement over the Inspector).

## Data Flow

```
Connect() succeeds -> SendCommand("list_devices") + SendCommand("get_device")
    -> server responds -> ToolClient.On("devices"/"device") -> Enqueue -> Update() drain
    -> EventBus.Raise(DevicesEvent / DeviceEvent)
    -> SttClient.OnDevicesEvent / OnDeviceEvent -> Devices / SelectedDevice updated
    -> SttClientEditor.OnInspectorGUI reads target.Devices / target.SelectedDevice each repaint
    -> user picks a different option -> EventBus.Raise(new SetDeviceCommandEvent { Device = ... })
    -> SttClient.OnSetDeviceCommand -> SendCommand("set_device", ...)

Enter Play Mode -> EditorApplication.playModeStateChanged(EnteredPlayMode)
    -> if connected: SendCommand("start") immediately
    -> else: _sendStartOnConnect = true -> next successful Connect() sends "start"
```

## Error Handling

- `SendCommand("list_devices")`/`SendCommand("get_device")`/`SendCommand("start")` reuse the existing private method, which already no-ops with a warning if something's wrong (e.g. called between disconnect and reconnect) — no new error path needed.
- If the server never responds to `list_devices`, `Devices` stays at its initial `Array.Empty<AudioDevice>()`; the dropdown shows "No devices known yet." instead of throwing or showing stale data.
- `SttClientEditor` repaints on every `EditorApplication.update` tick while the Inspector is visible — costs a redraw only, no reconnect or extra network traffic.

## Testing

Deferred, consistent with the rest of this project. Manual verification: open the scene with `SttClient` placed (not playing), confirm it connects within a few seconds and the dropdown populates; change the selected device and confirm it persists across a reconnect (server remembers it, not the client); press Play and confirm a `start` command reaches the server regardless of whether the dropdown had already loaded before Play began.

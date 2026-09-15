// © 2025–2026, Stefan Webb. Some Rights Reserved.
// Licensed under CC BY-SA 4.0
//
// Custom Inspector for SttClient: draws the default fields plus a dropdown
// of audio devices, populated from SttClient.Devices/SelectedDevice. Lives
// in an Editor folder so it compiles into Assembly-CSharp-Editor and never
// ships in a build.
// See docs/superpowers/specs/2026-06-23-stt-client-editor-mode-design.md.

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

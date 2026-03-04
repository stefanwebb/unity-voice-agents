using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Stefanwebb.Voiceagents {

[FilePath("SomeSubFolder/StateFile.foo", FilePathAttribute.Location.PreferencesFolder)]
public class PipecatConnection : ScriptableSingleton<PipecatConnection>
{
    [SerializeField]
    float m_Number = 42;

    [SerializeField]
    List<string> m_Strings = new List<string>();

    /*

    TODO: State variables

    State: Connected, Stopped, Connecting, Retrying
    
    Retry interval: 15 seconds?

    RTCPeerConnection

    RTCDataChannel

    MicrophoneName

    PipecatConnectionConfig (serializable object)

    AudioStreamSender

    */

    // public void Modify()
    // {
    //     m_Number *= 2;
    //     m_Strings.Add("Foo" + m_Number);

    //     Save(true);
    //     Debug.Log("Saved to: " + GetFilePath());
    // }

    // public void Log()
    // {
    //     Debug.Log("MySingleton state: " + JsonUtility.ToJson(this, true));
    // }
}

}
// © 2025–2026, Stefan Webb. Some Rights Reserved.
// Licensed under CC BY-SA 4.0
//
// Outbound command events for the named_pipes.stt server. Any script raises
// one of these on EventBus; SttClient forwards it to the server.

namespace GenerativeGamedev {

public struct StartCommandEvent : IEvent { }

public struct PauseCommandEvent : IEvent { }

public struct ListDevicesCommandEvent : IEvent { }

public struct GetDeviceCommandEvent : IEvent { }

public struct SetDeviceCommandEvent : IEvent
{
    public string Device;
}

}

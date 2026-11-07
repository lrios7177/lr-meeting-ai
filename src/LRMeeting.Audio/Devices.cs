using NAudio.CoreAudioApi;
namespace LRMeeting.Audio;
public sealed record AudioDevice(string Id, string Name) { public override string ToString() => Name; }
public static class Devices
{
    public static List<AudioDevice> List(DataFlow flow)
    {
        using var enumerator = new MMDeviceEnumerator();
        var result = new List<AudioDevice>();
        foreach (var device in enumerator.EnumerateAudioEndPoints(flow, DeviceState.Active))
            using (device) result.Add(new(device.ID, device.FriendlyName));
        return result;
    }
}

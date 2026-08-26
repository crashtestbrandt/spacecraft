// Godot device-id provider. Supplies a stable
// per-device identifier for cold-start Reconnect credential matching.
using xpTURN.Klotho.Network;

namespace xpTURN.Klotho.Godot
{
    public class GodotDeviceIdProvider : IDeviceIdProvider
    {
        public string GetDeviceId() => global::Godot.OS.GetUniqueId();
    }
}

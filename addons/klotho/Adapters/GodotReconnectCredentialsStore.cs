// user://-backed store for cold-start Reconnect credentials. Same Save/Load/Clear/IsValid logic and Newtonsoft.Json
// serialization (PersistedReconnectCredentials uses public fields — Newtonsoft serializes those by
// default); only the storage backend differs (PlayerPrefs -> a single user:// JSON file).
using Newtonsoft.Json;
using xpTURN.Klotho.Network;

namespace xpTURN.Klotho.Godot
{
    public class GodotReconnectCredentialsStore : IReconnectCredentialsStore
    {
        private const string Path = "user://klotho_reconnect.json";

        public void Save(PersistedReconnectCredentials creds)
        {
            if (creds == null)
            {
                Clear();
                return;
            }
            string json = JsonConvert.SerializeObject(creds);
            using var f = global::Godot.FileAccess.Open(Path, global::Godot.FileAccess.ModeFlags.Write);
            if (f == null) return;
            f.StoreString(json);
        }

        public PersistedReconnectCredentials Load()
        {
            if (!global::Godot.FileAccess.FileExists(Path)) return null;

            string json;
            using (var f = global::Godot.FileAccess.Open(Path, global::Godot.FileAccess.ModeFlags.Read))
            {
                if (f == null) return null;
                json = f.GetAsText();
            }
            if (string.IsNullOrEmpty(json)) return null;

            try
            {
                return JsonConvert.DeserializeObject<PersistedReconnectCredentials>(json);
            }
            catch
            {
                Clear();
                return null;
            }
        }

        public void Clear()
        {
            if (!global::Godot.FileAccess.FileExists(Path)) return;
            using var dir = global::Godot.DirAccess.Open("user://");
            dir?.Remove(Path);
        }

        public bool IsValid(PersistedReconnectCredentials creds, long nowUnixMs, string currentAppVersion)
        {
            if (creds == null) return false;
            if (creds.AppVersion != currentAppVersion) return false;
            if (nowUnixMs - creds.SavedAtUnixMs > creds.ReconnectTimeoutMs) return false;
            return true;
        }
    }
}

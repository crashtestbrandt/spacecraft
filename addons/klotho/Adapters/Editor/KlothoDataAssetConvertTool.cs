// Converts FileSystem-dock-selected .json DataAsset file(s) into .bytes.
// plugin.gd instantiates this [GlobalClass] and calls ConvertSelected().
// Wrapped in #if TOOLS so it compiles only into the editor build.
#if TOOLS
using global::Godot;
using xpTURN.Klotho.ECS;          // DataAssetWriter
using xpTURN.Klotho.ECS.Json;     // DataAssetJsonConverter

namespace xpTURN.Klotho.Godot
{
    [Tool]
    [GlobalClass]
    public partial class KlothoDataAssetConvertTool : RefCounted
    {
        public void ConvertSelected()
        {
            var ei = EditorInterface.Singleton;
            string lastOut = null;
            foreach (var resPath in ei.GetSelectedPaths())
            {
                if (!resPath.EndsWith(".json")) continue;
                using var f = FileAccess.Open(resPath, FileAccess.ModeFlags.Read);
                if (f == null) continue;
                var json = f.GetAsText();
                if (string.IsNullOrWhiteSpace(json)) continue;

                byte[] bytes;
                try
                {
                    bytes = DataAssetJsonConverter.ConvertMixedJsonToBytes(json);
                }
                catch (System.Exception ex)
                {
                    GD.PushError($"[Klotho] JsonToBytes failed: {resPath}\n{ex.Message}");
                    continue;
                }

                var outRes = System.IO.Path.ChangeExtension(resPath, ".bytes");
                var outFull = ProjectSettings.GlobalizePath(outRes);
                DataAssetWriter.SaveToFile(outFull, bytes);
                lastOut = outRes;
                GD.Print($"[Klotho] JsonToBytes {resPath} -> {outRes} ({bytes.Length} bytes)");
            }

            ei.GetResourceFilesystem().Scan();
            if (lastOut != null)
                ei.GetFileSystemDock().NavigateToPath(lastOut);
        }
    }
}
#endif

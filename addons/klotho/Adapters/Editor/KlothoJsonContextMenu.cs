// FileSystem dock right-click context menu for .json DataAsset conversion.
// Item appears only when at least one .json file is selected.
// Registered/unregistered by plugin.gd.
#if TOOLS
using global::Godot;

namespace xpTURN.Klotho.Godot
{
    [Tool]
    [GlobalClass]
    public partial class KlothoJsonContextMenu : EditorContextMenuPlugin
    {
        private KlothoDataAssetConvertTool _tool;

        public void Init(KlothoDataAssetConvertTool tool)
        {
            _tool = tool;
        }

        public override void _PopupMenu(string[] paths)
        {
            foreach (var p in paths)
            {
                if (p.EndsWith(".json"))
                {
                    AddContextMenuItem("Convert DataAsset JSON -> bytes", Callable.From<string[]>(OnConvert));
                    return;
                }
            }
        }

        private void OnConvert(string[] paths)
        {
            _tool?.ConvertSelected();
        }
    }
}
#endif

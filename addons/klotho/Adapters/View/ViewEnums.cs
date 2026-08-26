// BindBehaviour / ViewFlags for the Godot view layer.
using System;

namespace xpTURN.Klotho.Godot
{
    // Whether a view tracks the Verified frame or the (predicted) NonVerified frame.
    public enum BindBehaviour
    {
        NonVerified,
        Verified,
    }

    [Flags]
    public enum ViewFlags
    {
        None                          = 0,
        DisableUpdate                 = 1 << 0,
        DisablePositionUpdate         = 1 << 1,
        UseCachedTransform            = 1 << 2,
        EnableSnapshotInterpolation   = 1 << 3,
    }
}

// Per-collider override for the static collider exporter. Attach as a child of a CollisionShape3D
// to override the auto-assigned id and the restitution/friction coefficients.
//
// NOT wrapped in #if TOOLS: this is scene-authored runtime data and must compile in game builds
// too. [GlobalClass] makes it appear in the editor's "Add Node" dialog so it can be attached;
// [Tool] persists the exported values in-editor.
using global::Godot;

namespace xpTURN.Klotho.Godot
{
    [Tool]
    [GlobalClass]
    public partial class FPStaticColliderOverride : Node
    {
        [Export] public int Id;            // Explicit FPStaticCollider.id — 0 uses auto-assignment
        [Export] public float Restitution; // 0: no bounce, 1: perfectly elastic
        [Export] public float Friction;    // Coefficient of friction

        public override string[] _GetConfigurationWarnings()
        {
            if (GetParent() is not CollisionShape3D)
                return new[] { "FPStaticColliderOverride should be a child of a CollisionShape3D." };
            return System.Array.Empty<string>();
        }
    }
}

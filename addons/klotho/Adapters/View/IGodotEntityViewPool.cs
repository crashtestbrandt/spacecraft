// Optional view pool. When supplied to the
// EntityViewUpdaterNode, spawn/despawn reuse EntityViewNode instances instead of
// Instantiate/QueueFree on every entity create/destroy — avoiding per-spawn GC churn.
//
// Rent returns a node that is NOT in the scene tree (the EntityViewUpdaterNode adds it as a child);
// Return detaches the node from its parent and parks it (it stops _Process while out of the tree).
using global::Godot;

namespace xpTURN.Klotho.Godot
{
    public interface IGodotEntityViewPool
    {
        // Reuse an idle instance of the scene if one exists, otherwise instantiate. The returned node
        // is detached (no parent) and ready for the caller to add to the scene tree.
        EntityViewNode Rent(PackedScene scene);

        // Detach the view from its parent and return it to the idle pool for reuse. A view that was
        // not produced by this pool is freed instead.
        void Return(EntityViewNode view);

        // Pre-instantiate count idle instances of the scene so the first spawns are also allocation-free.
        void Prewarm(PackedScene scene, int count);

        // Free all idle instances held by the pool (call on teardown).
        void Dispose();
    }
}

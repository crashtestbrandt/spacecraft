// Default view pool keyed by PackedScene.
// Idle instances are kept detached from the scene tree (so they do not _Process) and reused on Rent.
// A reverse map (view -> source scene) lets Return route a view back to the correct queue without the
// caller tracking its origin; views not produced by this pool are freed instead.
using System.Collections.Generic;
using global::Godot;

namespace xpTURN.Klotho.Godot
{
    public sealed class DefaultGodotEntityViewPool : IGodotEntityViewPool
    {
        private readonly Dictionary<PackedScene, Queue<EntityViewNode>> _idle = new();
        private readonly Dictionary<EntityViewNode, PackedScene>        _source = new();

        public EntityViewNode Rent(PackedScene scene)
        {
            if (scene == null) return null;

            if (_idle.TryGetValue(scene, out var queue) && queue.Count > 0)
                return queue.Dequeue();   // hit — reuse, already detached

            var view = scene.Instantiate<EntityViewNode>();   // miss — instantiate
            if (view == null) return null;
            _source[view] = scene;
            return view;
        }

        public void Return(EntityViewNode view)
        {
            if (view == null) return;

            view.GetParent()?.RemoveChild(view);   // leave the active tree → stops _Process

            if (!_source.TryGetValue(view, out var scene))
            {
                view.QueueFree();   // foreign view (created outside the pool)
                return;
            }

            if (!_idle.TryGetValue(scene, out var queue))
            {
                queue = new Queue<EntityViewNode>();
                _idle[scene] = queue;
            }
            queue.Enqueue(view);
        }

        public void Prewarm(PackedScene scene, int count)
        {
            if (scene == null || count <= 0) return;

            if (!_idle.TryGetValue(scene, out var queue))
            {
                queue = new Queue<EntityViewNode>();
                _idle[scene] = queue;
            }

            for (int i = 0; i < count; i++)
            {
                var view = scene.Instantiate<EntityViewNode>();
                if (view == null) continue;
                _source[view] = scene;
                queue.Enqueue(view);
            }
        }

        public void Dispose()
        {
            foreach (var queue in _idle.Values)
                while (queue.Count > 0)
                    queue.Dequeue().QueueFree();
            _idle.Clear();
            _source.Clear();
        }
    }
}

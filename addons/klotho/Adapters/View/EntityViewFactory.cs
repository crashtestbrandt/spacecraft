// Abstract view factory: resolves a PackedScene per entity and decides BindBehaviour / ViewFlags.
// Game-specific factories override ResolvePrefab + ShouldRender; the decision matrix and instantiate
// paths are virtual defaults.
using global::Godot;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.ECS;

namespace xpTURN.Klotho.Godot
{
    public abstract class EntityViewFactory
    {
        // Injected by the EntityViewUpdaterNode at Initialize time.
        public IKlothoEngine Engine { get; private set; }

        // Optional view pool. When null, Create/Destroy instantiate/free directly (default).
        public IGodotEntityViewPool Pool { get; private set; }

        internal void Attach(IKlothoEngine engine, IGodotEntityViewPool pool = null)
        {
            Engine = engine;
            Pool   = pool;
        }

        // ── Game-specific overrides ──
        protected abstract PackedScene ResolvePrefab(Frame frame, EntityRef entity);
        protected abstract bool ShouldRender(Frame frame, EntityRef entity);

        // ── Framework default decisions ──
        public virtual bool TryGetBindBehaviour(Frame frame, EntityRef entity, out BindBehaviour behaviour)
        {
            if (!ShouldRender(frame, entity))
            {
                behaviour = BindBehaviour.Verified;
                return false;
            }

            if (frame.Has<OwnerComponent>(entity))
            {
                ref readonly var owner = ref frame.GetReadOnly<OwnerComponent>(entity);
                behaviour = IsPredictedRender(owner.OwnerId)
                    ? BindBehaviour.NonVerified
                    : BindBehaviour.Verified;
                return true;
            }

            bool useVerified = UseVerifiedPath() && !Engine.IsReplayMode;
            behaviour = useVerified ? BindBehaviour.Verified : BindBehaviour.NonVerified;
            return true;
        }

        public virtual ViewFlags GetViewFlags(Frame frame, EntityRef entity)
        {
            bool hasOwner = frame.Has<OwnerComponent>(entity);
            int  ownerId  = hasOwner ? frame.GetReadOnly<OwnerComponent>(entity).OwnerId : -1;

            bool useVerifiedPath = UseVerifiedPath() && !Engine.IsReplayMode;
            bool predictedRender = hasOwner
                ? !useVerifiedPath || (ownerId == Engine.LocalPlayerId)
                : !useVerifiedPath;

            return predictedRender ? ViewFlags.None : ViewFlags.EnableSnapshotInterpolation;
        }

        protected bool IsPredictedRender(int ownerId)
            => !UseVerifiedPath() || Engine.IsReplayMode || (ownerId == Engine.LocalPlayerId);

        protected bool UseVerifiedPath()
        {
            bool isSDClient = (Engine.SimulationConfig.Mode == NetworkMode.ServerDriven) && !Engine.IsServer;
            return isSDClient || Engine.IsSpectatorMode;
        }

        // ── Instantiate / Destroy default ──
        // Instantiates the resolved PackedScene (root must be an EntityViewNode). Returns null to skip.
        // The caller (EntityViewUpdaterNode) attaches the node to the scene tree.
        public virtual EntityViewNode Create(Frame frame, EntityRef entity, BindBehaviour behaviour, ViewFlags flags)
        {
            var prefab = ResolvePrefab(frame, entity);
            if (prefab == null) return null;
            return Pool != null ? Pool.Rent(prefab) : prefab.Instantiate<EntityViewNode>();
        }

        public virtual void Destroy(EntityViewNode view)
        {
            if (view == null) return;
            if (Pool != null) Pool.Return(view);
            else              view.QueueFree();
        }
    }
}

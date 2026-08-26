// Single scene orchestrator for entity views.
// Subscribes to Engine.OnTickExecuted: Reconcile (spawn/destroy views matching the present entities)
// then InternalUpdateView per view. Per-frame interpolation (InternalLateUpdateView) runs from this
// node's own _Process — the adapter is a Godot.NET.Sdk project, so its Node lifecycle is dispatched.
// ProcessViews() is also exposed for explicit/headless drive. A high ProcessPriority makes it run after
// the session driver's _Process, so interpolation reads the frame the driver just advanced.
// Spawn pooling is opt-in (see IGodotEntityViewPool); the async/pending spawn paths are not implemented.
using System.Collections.Generic;
using global::Godot;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.ECS;

namespace xpTURN.Klotho.Godot
{
    public partial class EntityViewUpdaterNode : Node
    {
        private EntityViewFactory _factory;
        private IKlothoEngine     _engine;

        private readonly Dictionary<int, EntityViewNode> _viewsByEntityIndex = new();
        private readonly Dictionary<int, int>            _presentEntityVersions = new();
        private readonly List<int>                       _staleIndices = new();
        private EntityRef[] _entityScratch;

        public EntityViewFactory Factory => _factory;

        // playerId -> view registry for Owner-bearing views, auto-populated on spawn/despawn.
        private GodotPlayerViewRegistry<EntityViewNode> _playerViews;
        public GodotPlayerViewRegistry<EntityViewNode> PlayerViews => _playerViews;

        public void Initialize(IKlothoEngine engine, EntityViewFactory factory, IGodotEntityViewPool pool = null)
        {
            Cleanup();
            _engine  = engine;
            _factory = factory;
            _factory.Attach(engine, pool);
            _playerViews = new GodotPlayerViewRegistry<EntityViewNode>(engine, engine.SessionConfig.MaxPlayers);
            _engine.OnTickExecuted += OnTickExecuted;
            // Run interpolation after the session driver's _Process (lower priority runs first).
            ProcessPriority = 1000;
        }

        // Per-frame interpolation, self-driven (the adapter's Node lifecycle is dispatched). No-op until
        // Initialize populates views. Equivalent to the explicit ProcessViews() call kept below.
        public override void _Process(double delta) => ProcessViews(delta);

        public void Cleanup()
        {
            if (_engine != null) _engine.OnTickExecuted -= OnTickExecuted;
            foreach (var view in _viewsByEntityIndex.Values)
            {
                view.OnDeactivate();
                _factory?.Destroy(view);
            }
            _viewsByEntityIndex.Clear();
            _playerViews?.Clear();
            _playerViews = null;
            _engine = null;
        }

        private void OnTickExecuted(int tick)
        {
            Reconcile();
            foreach (var view in _viewsByEntityIndex.Values)
                view.InternalUpdateView();
        }

        // Per-frame interpolation pass. Call once per frame after session.Update.
        // delta is the frame time (seconds), forwarded to each view for the error-visual decay.
        public void ProcessViews(double delta = 0)
        {
            foreach (var view in _viewsByEntityIndex.Values)
                view.InternalLateUpdateView((float)delta);
        }

        private void Reconcile()
        {
            if (_factory == null) return;

            _presentEntityVersions.Clear();

            var verified  = _engine.VerifiedFrame;
            var predicted = _engine.PredictedFrame;

            if (verified.Frame  != null) CollectPresent(verified,  BindBehaviour.Verified);
            if (predicted.Frame != null) CollectPresent(predicted, BindBehaviour.NonVerified);

            if (verified.Frame == null && predicted.Frame == null) return;

            DestroyStale();
        }

        private void CollectPresent(FrameRef frameRef, BindBehaviour matchBehaviour)
        {
            var frame = frameRef.Frame;
            int maxEntities = frame.MaxEntities;
            if (_entityScratch == null || _entityScratch.Length < maxEntities)
                _entityScratch = new EntityRef[maxEntities];

            int count = frame.GetAllLiveEntities(_entityScratch);
            for (int i = 0; i < count; i++)
            {
                var entity = _entityScratch[i];

                if (!_factory.TryGetBindBehaviour(frame, entity, out var behaviour)) continue;
                if (behaviour != matchBehaviour) continue;

                _presentEntityVersions[entity.Index] = entity.Version;
                TrySpawn(entity, frameRef, behaviour);
            }
        }

        private void TrySpawn(EntityRef entity, FrameRef frame, BindBehaviour behaviour)
        {
            if (_viewsByEntityIndex.TryGetValue(entity.Index, out var existing))
            {
                bool versionMatch = existing.EntityRef.Version == entity.Version;
                bool ownerMatch   = OwnersMatch(existing, entity, frame.Frame);
                if (versionMatch && ownerMatch) return; // same entity — keep the view

                existing.OnDeactivate();
                TryUnregisterPlayerView(existing);   // unbind site 1: rebind
                _factory.Destroy(existing);
                _viewsByEntityIndex.Remove(entity.Index);
            }

            ViewFlags flags = _factory.GetViewFlags(frame.Frame, entity);
            var view = _factory.Create(frame.Frame, entity, behaviour, flags);
            if (view == null) return;

            view.EntityRef = entity;
            view.Engine    = _engine;
            view.SetBindBehaviour(behaviour);
            view.SetViewFlags(flags);

            AddChild(view);
            _viewsByEntityIndex[entity.Index] = view;

            view.EnsureInitialized();
            view.InternalActivate(frame);
            TryRegisterPlayerView(entity, view, frame);
        }

        // Spawn-side: read OwnerComponent from the spawn-decision frame, cache it on the view (the stable
        // unregister key), and register. Owner-agnostic views (no OwnerComponent) are not registered.
        private void TryRegisterPlayerView(EntityRef entity, EntityViewNode view, FrameRef frame)
        {
            if (_playerViews == null) return;
            var f = frame.Frame;
            if (f == null || !f.Has<OwnerComponent>(entity)) return;
            int ownerId = f.GetReadOnly<OwnerComponent>(entity).OwnerId;
            view.SetCachedOwner(ownerId);
            _playerViews.Register(ownerId, view);
        }

        // Unbind-side: OwnerComponent may already be absent on the live frame at despawn, so the cached
        // owner is the unregister key. Called from rebind and DestroyStale.
        private void TryUnregisterPlayerView(EntityViewNode view)
        {
            if (_playerViews == null) return;
            if (!view.TryGetCachedOwner(out int ownerId)) return;
            _playerViews.Unregister(ownerId, view);
            view.ClearCachedOwner();
        }

        private static bool OwnersMatch(EntityViewNode view, EntityRef entity, Frame frame)
        {
            if (!frame.Has<OwnerComponent>(entity)) return true;
            int currentOwner = frame.GetReadOnly<OwnerComponent>(entity).OwnerId;
            return view.OwnerMatches(currentOwner);
        }

        private void DestroyStale()
        {
            _staleIndices.Clear();
            foreach (var kvp in _viewsByEntityIndex)
                if (!_presentEntityVersions.ContainsKey(kvp.Key))
                    _staleIndices.Add(kvp.Key);

            foreach (var key in _staleIndices)
            {
                var view = _viewsByEntityIndex[key];
                view.OnDeactivate();
                TryUnregisterPlayerView(view);   // unbind site 2: stale despawn
                _factory.Destroy(view);
                _viewsByEntityIndex.Remove(key);
            }
        }
    }
}

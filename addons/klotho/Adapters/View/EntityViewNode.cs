// Godot view node for a single entity. The EntityViewUpdaterNode manages its lifecycle and
// drives InternalUpdateView (per tick) and InternalLateUpdateView (per frame: interpolate + apply transform).
using global::Godot;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace xpTURN.Klotho.Godot
{
    public partial class EntityViewNode : Node3D
    {
        public EntityRef     EntityRef { get; set; }
        public IKlothoEngine Engine    { get; set; }

        public BindBehaviour BindBehaviour { get; private set; } = BindBehaviour.Verified;
        public ViewFlags     ViewFlags     { get; private set; } = ViewFlags.None;

        internal void SetBindBehaviour(BindBehaviour value) => BindBehaviour = value;
        internal void SetViewFlags(ViewFlags value)         => ViewFlags = value;

        // Cached OwnerComponent.OwnerId, written at spawn-register time. Used as the stable unregister
        // key because OwnerComponent may already be absent on the live frame at despawn (GodotPlayerViewRegistry).
        private int  _cachedOwner;
        private bool _hasCachedOwner;
        internal void SetCachedOwner(int ownerId) { _cachedOwner = ownerId; _hasCachedOwner = true; }
        internal bool TryGetCachedOwner(out int ownerId) { ownerId = _cachedOwner; return _hasCachedOwner; }
        internal void ClearCachedOwner() => _hasCachedOwner = false;

        // Override required for any entity carrying OwnerComponent; the default returns false so a
        // missing override surfaces as continuous rebind churn rather than a silent owner-swap bug.
        public virtual bool OwnerMatches(int ownerId) => false;

        private bool _hasInitialized;

        internal void EnsureInitialized()
        {
            if (_hasInitialized) return;
            _hasInitialized = true;
            OnInitialize();
        }

        // Per-view desync error-blending state. Config is preserved across pool reuse; accumulation is reset on activate.
        private ErrorVisualState _errorVisual = ErrorVisualState.Default;

        internal void InternalActivate(FrameRef frame)
        {
            _errorVisual.Reset();
            OnActivate(frame);
        }

        // ── Lifecycle callbacks (override in game-specific views) ──
        public virtual void OnInitialize() { }
        public virtual void OnActivate(FrameRef frame) { }
        public virtual void OnUpdateView() { }
        public virtual void OnLateUpdateView() { }
        public virtual void OnDeactivate() { }

        internal virtual void InternalUpdateView()
        {
            if ((ViewFlags & ViewFlags.DisableUpdate) != 0) return;
            if (Engine == null || !EntityRef.IsValid) return;
            OnUpdateView();
        }

        // Per-frame interpolation + transform apply.
        // dt is the frame delta (seconds), forwarded from EntityViewUpdaterNode._Process for the error-visual decay.
        internal virtual void InternalLateUpdateView(float dt = 0f)
        {
            if ((ViewFlags & ViewFlags.DisableUpdate) != 0) return;
            if (Engine == null || !EntityRef.IsValid) return;

            var predicted = Engine.PredictedFrame.Frame;
            if (predicted == null) return;
            if (!predicted.Has<TransformComponent>(EntityRef)) return;

            ref readonly var curr = ref predicted.GetReadOnly<TransformComponent>(EntityRef);
            Vector3    currPos = curr.Position.ToVector3();
            Quaternion currRot = Quaternion.FromEuler(new Vector3(0f, curr.Rotation.ToFloat(), 0f));

            Vector3    newPos = currPos;
            Quaternion newRot = currRot;

            bool snapshot = (ViewFlags & ViewFlags.EnableSnapshotInterpolation) != 0;
            if (snapshot)
            {
                // Smooth between two adjacent Verified frames.
                newPos = VerifiedFrameInterpolator.InterpolatePosition(EntityRef, Engine, currPos);
                newRot = VerifiedFrameInterpolator.InterpolateRotation(EntityRef, Engine, currRot);
            }
            else
            {
                // Lerp between PredictedPrevious and Predicted by the per-frame render alpha.
                var prev = Engine.PredictedPreviousFrame.Frame;
                if (prev != null && prev.Has<TransformComponent>(EntityRef))
                {
                    ref readonly var prevT = ref prev.GetReadOnly<TransformComponent>(EntityRef);
                    float alpha = Engine.RenderClock.PredictedAlpha;
                    newPos = prevT.Position.ToVector3().Lerp(currPos, alpha);
                    newRot = Quaternion.FromEuler(new Vector3(
                        0f, Mathf.LerpAngle(prevT.Rotation.ToFloat(), curr.Rotation.ToFloat(), alpha), 0f));
                }
            }

            // Blend the rollback-induced offset on top of the predicted interpolation so a corrected entity
            // drifts smoothly instead of snapping. Gated on EnableErrorCorrection so non-EC games pay nothing.
            if (Engine.SimulationConfig.EnableErrorCorrection)
                ApplyErrorVisual(ref newPos, ref newRot, snapshot, dt);

            if ((ViewFlags & ViewFlags.DisablePositionUpdate) == 0)
                Position = newPos;
            Quaternion = newRot;

            OnLateUpdateView();
        }

        // Read-before-tick: apply the previous frame's smoothed error to this frame's transform, then advance the
        // accumulation with this frame's rollback delta. skipPosError follows DisablePositionUpdate|snapshot;
        // skipYawError follows snapshot only — rotation error still applies under DisablePositionUpdate.
        // The yaw error is radians, applied as a pre-multiplied quaternion (error * rotation).
        private void ApplyErrorVisual(ref Vector3 newPos, ref Quaternion newRot, bool snapshot, float dt)
        {
            bool skipPosError = snapshot || (ViewFlags & ViewFlags.DisablePositionUpdate) != 0;

            if (!skipPosError)
                newPos += _errorVisual.SmoothedPosError;
            if (!snapshot)
                newRot = Quaternion.FromEuler(new Vector3(0f, _errorVisual.SmoothedYawError, 0f)) * newRot;

            int idx = EntityRef.Index;
            var (dx, dy, dz) = Engine.GetPositionDelta(idx);
            _errorVisual.Tick(new Vector3(dx, dy, dz), Engine.GetYawDelta(idx), dt, Engine.HasEntityTeleported(idx));
        }
    }
}

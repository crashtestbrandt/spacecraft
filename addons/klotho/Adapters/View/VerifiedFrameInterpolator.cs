// Interpolates position/yaw between two adjacent Verified frames.
//   (a) both frames valid -> Lerp(ta, tb, VerifiedAlpha)
//   (b) only baseTick valid     -> ta value
//   (c) only baseTick+1 valid   -> tb value
//   (d) neither valid           -> fallback
using global::Godot;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace xpTURN.Klotho.Godot
{
    public static class VerifiedFrameInterpolator
    {
        public static Vector3 InterpolatePosition(EntityRef entity, IKlothoEngine engine, Vector3 fallbackPos)
        {
            int baseTick = engine.RenderClock.VerifiedBaseTick;
            bool hasA = engine.TryGetFrameAtTick(baseTick,     out var a);
            bool hasB = engine.TryGetFrameAtTick(baseTick + 1, out var b);

            if (hasA && !a.Has<TransformComponent>(entity)) hasA = false;
            if (hasB && !b.Has<TransformComponent>(entity)) hasB = false;

            if (!hasA && !hasB) return fallbackPos;                                                  // (d)
            if ( hasA && !hasB) return a.GetReadOnly<TransformComponent>(entity).Position.ToVector3(); // (b)
            if (!hasA &&  hasB) return b.GetReadOnly<TransformComponent>(entity).Position.ToVector3(); // (c)

            ref readonly var ta = ref a.GetReadOnly<TransformComponent>(entity);
            ref readonly var tb = ref b.GetReadOnly<TransformComponent>(entity);
            return ta.Position.ToVector3().Lerp(tb.Position.ToVector3(), engine.RenderClock.VerifiedAlpha); // (a)
        }

        public static Quaternion InterpolateRotation(EntityRef entity, IKlothoEngine engine, Quaternion fallbackRot)
        {
            int baseTick = engine.RenderClock.VerifiedBaseTick;
            bool hasA = engine.TryGetFrameAtTick(baseTick,     out var a);
            bool hasB = engine.TryGetFrameAtTick(baseTick + 1, out var b);

            if (hasA && !a.Has<TransformComponent>(entity)) hasA = false;
            if (hasB && !b.Has<TransformComponent>(entity)) hasB = false;

            if (!hasA && !hasB) return fallbackRot;                                            // (d)
            if ( hasA && !hasB) return YawRotation(a.GetReadOnly<TransformComponent>(entity).Rotation); // (b)
            if (!hasA &&  hasB) return YawRotation(b.GetReadOnly<TransformComponent>(entity).Rotation); // (c)

            ref readonly var ta = ref a.GetReadOnly<TransformComponent>(entity);
            ref readonly var tb = ref b.GetReadOnly<TransformComponent>(entity);
            float yaw = Mathf.LerpAngle(ta.Rotation.ToFloat(), tb.Rotation.ToFloat(), engine.RenderClock.VerifiedAlpha);
            return Quaternion.FromEuler(new Vector3(0f, yaw, 0f)); // (a)
        }

        // TransformComponent.Rotation is a single yaw angle in radians (not a full quaternion).
        private static Quaternion YawRotation(FP64 yawRad)
            => Quaternion.FromEuler(new Vector3(0f, yawRad.ToFloat(), 0f));
    }
}

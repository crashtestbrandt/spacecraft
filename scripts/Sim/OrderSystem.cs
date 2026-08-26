// Applies MoveOrderCommand: every selected ship of the issuing player gets a target that keeps the
// group's current formation (target + offset from the selection's centroid), clamped to the arena.
using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Spacecraft.Sim
{
    public sealed class OrderSystem : ISystem, ICommandSystem
    {
        public void OnCommand(ref Frame frame, ICommand command)
        {
            if (command is not MoveOrderCommand order) return;
            if (order.SelectionMask == 0) return;

            // Pass 1: centroid of the selected, alive ships.
            FP64 sumX = FP64.Zero, sumY = FP64.Zero;
            int count = 0;
            var f1 = frame.Filter<ShipComponent, OwnerComponent, TransformComponent>();
            while (f1.Next(out var e))
            {
                if (!Selected(ref frame, e, order)) continue;
                ref readonly var t = ref frame.GetReadOnly<TransformComponent>(e);
                sumX += t.Position.x;
                sumY += t.Position.y;
                count++;
            }
            if (count == 0) return;
            FP64 inv = FP64.One / FP64.FromInt(count);
            FP64 cx = sumX * inv, cy = sumY * inv;

            // Pass 2: per-ship target = order target + (ship - centroid).
            var f2 = frame.Filter<ShipComponent, OwnerComponent, TransformComponent>();
            while (f2.Next(out var e))
            {
                if (!Selected(ref frame, e, order)) continue;
                ref readonly var t = ref frame.GetReadOnly<TransformComponent>(e);
                ref var ship = ref frame.Get<ShipComponent>(e);
                var target = Rules.Clamp(new FPVector2(order.TargetX + (t.Position.x - cx),
                                                       order.TargetY + (t.Position.y - cy)));
                ship.TargetX = target.x;
                ship.TargetY = target.y;
                ship.HasTarget = 1;
            }
        }

        static bool Selected(ref Frame frame, EntityRef e, MoveOrderCommand order)
        {
            ref readonly var owner = ref frame.GetReadOnly<OwnerComponent>(e);
            if (owner.OwnerId != order.PlayerId) return false;
            ref readonly var ship = ref frame.GetReadOnly<ShipComponent>(e);
            return (order.SelectionMask & (1 << ship.Slot)) != 0;
        }

        public void Update(ref Frame frame) { }
    }
}

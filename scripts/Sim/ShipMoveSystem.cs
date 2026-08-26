// Moves ships toward their target at the kind's speed, faces the travel direction, stops on arrival.
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Spacecraft.Sim
{
    public sealed class ShipMoveSystem : ISystem
    {
        static readonly FP64 Thousand = FP64.FromInt(1000);

        public void Update(ref Frame frame)
        {
            FP64 dt = FP64.FromInt(frame.DeltaTimeMs) / Thousand;
            var f = frame.Filter<ShipComponent, TransformComponent>();
            while (f.Next(out var e))
            {
                ref var ship = ref frame.Get<ShipComponent>(e);
                if (ship.HasTarget == 0) continue;
                ref var t = ref frame.Get<TransformComponent>(e);

                var pos = new FPVector2(t.Position.x, t.Position.y);
                var to  = new FPVector2(ship.TargetX, ship.TargetY) - pos;
                FP64 dist = to.magnitude;
                FP64 step = Rules.Speed[ship.Kind] * dt;
                if (dist <= step)
                {
                    pos = new FPVector2(ship.TargetX, ship.TargetY);
                    ship.HasTarget = 0;
                }
                else
                {
                    var dir = to / dist;
                    pos += dir * step;
                    t.Rotation = FP64.Atan2(dir.y, dir.x);
                }
                t.Position = new FPVector3(pos.x, pos.y, FP64.Zero);
            }
        }
    }
}

// Auto-fire: each ship with a ready cooldown shoots at the nearest enemy ship in range. Spawns are
// collected first and created after the filter walk so the walk never sees storage it is mutating.
using System.Collections.Generic;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Spacecraft.Sim
{
    public sealed class TargetingSystem : ISystem
    {
        struct Shot { public int Owner; public int Kind; public FPVector2 From; public FPVector2 Dir; }

        readonly List<Shot> _shots = new(64);
        readonly List<EntityRef> _ships = new(64);

        public void Update(ref Frame frame)
        {
            _shots.Clear();
            _ships.Clear();
            var all = frame.Filter<ShipComponent, OwnerComponent, TransformComponent>();
            while (all.Next(out var e)) _ships.Add(e);

            for (int i = 0; i < _ships.Count; i++)
            {
                var e = _ships[i];
                ref var ship = ref frame.Get<ShipComponent>(e);
                if (ship.FireCooldown > 0) { ship.FireCooldown--; continue; }

                int owner = frame.GetReadOnly<OwnerComponent>(e).OwnerId;
                ref readonly var t = ref frame.GetReadOnly<TransformComponent>(e);
                var pos = new FPVector2(t.Position.x, t.Position.y);
                FP64 range = Rules.Range[ship.Kind];
                FP64 bestSq = range * range;
                bool found = false;
                FPVector2 best = default;

                for (int j = 0; j < _ships.Count; j++)
                {
                    if (j == i) continue;
                    var o = _ships[j];
                    if (frame.GetReadOnly<OwnerComponent>(o).OwnerId == owner) continue;
                    ref readonly var ot = ref frame.GetReadOnly<TransformComponent>(o);
                    var opos = new FPVector2(ot.Position.x, ot.Position.y);
                    FP64 dsq = (opos - pos).sqrMagnitude;
                    if (dsq < bestSq) { bestSq = dsq; best = opos; found = true; }
                }
                if (!found) continue;

                var dir = (best - pos).normalized;
                _shots.Add(new Shot { Owner = owner, Kind = ship.Kind, From = pos + dir * Rules.MuzzleOffset[ship.Kind], Dir = dir });
                ship.FireCooldown = Rules.CooldownTicks[ship.Kind];
                frame.Get<TransformComponent>(e).Rotation = FP64.Atan2(dir.y, dir.x);
            }

            for (int i = 0; i < _shots.Count; i++)
            {
                var s = _shots[i];
                var b = frame.CreateEntity();
                frame.Add(b, new TransformComponent
                {
                    Position = new FPVector3(s.From.x, s.From.y, FP64.Zero),
                    Rotation = FP64.Atan2(s.Dir.y, s.Dir.x),
                    Scale = FPVector3.One,
                });
                frame.Add(b, new OwnerComponent { OwnerId = s.Owner });
                FP64 speed = Rules.BulletSpeed[s.Kind];
                frame.Add(b, new BulletComponent
                {
                    VelX = s.Dir.x * speed,
                    VelY = s.Dir.y * speed,
                    Ttl = Rules.BulletTtlTicks,
                    Damage = Rules.Damage[s.Kind],
                });
            }
        }
    }
}

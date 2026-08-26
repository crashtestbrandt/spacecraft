// Moves bullets, expires them, and applies damage on contact with an enemy ship's hit radius.
using System.Collections.Generic;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Spacecraft.Sim
{
    public sealed class BulletSystem : ISystem
    {
        static readonly FP64 Thousand = FP64.FromInt(1000);

        readonly List<EntityRef> _ships = new(64);
        readonly List<EntityRef> _dead  = new(64);

        public void Update(ref Frame frame)
        {
            FP64 dt = FP64.FromInt(frame.DeltaTimeMs) / Thousand;

            _ships.Clear();
            var sf = frame.Filter<ShipComponent, OwnerComponent, TransformComponent>();
            while (sf.Next(out var s)) _ships.Add(s);

            _dead.Clear();
            var bf = frame.Filter<BulletComponent, OwnerComponent, TransformComponent>();
            while (bf.Next(out var b))
            {
                ref var bullet = ref frame.Get<BulletComponent>(b);
                ref var t = ref frame.Get<TransformComponent>(b);
                t.Position = new FPVector3(t.Position.x + bullet.VelX * dt, t.Position.y + bullet.VelY * dt, FP64.Zero);
                bullet.Ttl--;
                if (bullet.Ttl <= 0 || OutOfArena(t.Position)) { _dead.Add(b); continue; }

                int owner = frame.GetReadOnly<OwnerComponent>(b).OwnerId;
                var pos = new FPVector2(t.Position.x, t.Position.y);
                for (int i = 0; i < _ships.Count; i++)
                {
                    var s = _ships[i];
                    if (frame.GetReadOnly<OwnerComponent>(s).OwnerId == owner) continue;
                    ref var ship = ref frame.Get<ShipComponent>(s);
                    if (ship.Hp <= 0) continue;
                    ref readonly var st = ref frame.GetReadOnly<TransformComponent>(s);
                    FP64 r = Rules.Radius[ship.Kind];
                    var d = new FPVector2(st.Position.x, st.Position.y) - pos;
                    if (d.sqrMagnitude > r * r) continue;
                    ship.Hp -= bullet.Damage;
                    _dead.Add(b);
                    break;
                }
            }

            for (int i = 0; i < _dead.Count; i++) frame.DestroyEntity(_dead[i]);
        }

        static bool OutOfArena(FPVector3 p)
            => p.x < FP64.Zero || p.y < FP64.Zero
            || p.x > FP64.FromInt(Rules.ArenaWidth) || p.y > FP64.FromInt(Rules.ArenaHeight);
    }
}

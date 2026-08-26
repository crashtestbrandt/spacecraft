// Destroys ships whose Hp reached zero. Collected first, destroyed after the walk.
using System.Collections.Generic;
using xpTURN.Klotho.ECS;

namespace Spacecraft.Sim
{
    public sealed class DeathSystem : ISystem
    {
        readonly List<EntityRef> _dead = new(64);

        public void Update(ref Frame frame)
        {
            _dead.Clear();
            var f = frame.Filter<ShipComponent>();
            while (f.Next(out var e))
                if (frame.GetReadOnly<ShipComponent>(e).Hp <= 0) _dead.Add(e);
            for (int i = 0; i < _dead.Count; i++) frame.DestroyEntity(_dead[i]);
        }
    }
}

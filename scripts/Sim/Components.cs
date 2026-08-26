// ECS components. Position/rotation live in Klotho's own TransformComponent (x/y used, z = 0;
// Rotation = facing in radians). Ownership uses Klotho's OwnerComponent.
using System.Runtime.InteropServices;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.ECS;

namespace Spacecraft.Sim
{
    [KlothoComponent(100)]
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public partial struct ShipComponent : IComponent
    {
        public int  Slot;          // index within the owner's army (0 = flagship)
        public int  Kind;          // Rules.KindFlagship / KindFighter
        public int  Hp;
        public int  HasTarget;     // 1 while moving toward Target
        public FP64 TargetX;
        public FP64 TargetY;
        public int  FireCooldown;  // ticks until the next shot
    }

    [KlothoComponent(101)]
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public partial struct BulletComponent : IComponent
    {
        public FP64 VelX;
        public FP64 VelY;
        public int  Ttl;
        public int  Damage;
    }

    // Singleton: match bookkeeping so GameOverEvent fires exactly once.
    [KlothoComponent(102)]
    [KlothoSingletonComponent]
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public partial struct MatchStateComponent : IComponent
    {
        public int Ended;
        public int WinnerPlayerId;   // -1 = draw
    }
}

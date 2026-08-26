// Fixed rules for the demo: arena size, per-kind ship stats, and each player's fixed army layout.
// Everything here is FP64 / int so systems can read it without crossing the float boundary.
using xpTURN.Klotho.Deterministic.Math;

namespace Spacecraft.Sim
{
    public static class Rules
    {
        public const int MaxPlayers    = 4;
        public const int ArmySize      = 9;    // slot 0 = flagship, slots 1..8 = fighters
        public const int KindFlagship  = 0;
        public const int KindFighter   = 1;

        // Arena in pixels (1 sim unit = 1 pixel at the 640x360 base resolution). Top 24 px is HUD.
        public const int ArenaWidth  = 640;
        public const int ArenaHeight = 360;
        public static readonly FP64 MinX = FP64.FromInt(8);
        public static readonly FP64 MaxX = FP64.FromInt(ArenaWidth - 8);
        public static readonly FP64 MinY = FP64.FromInt(32);
        public static readonly FP64 MaxY = FP64.FromInt(ArenaHeight - 8);

        // No winner is declared before this tick, so a match never ends on its first frame.
        public const int MinMatchTicks = 30;

        // Per-kind stats, indexed by Kind.
        public static readonly int[]  Hp            = { 40, 6 };
        public static readonly FP64[] Speed         = { FP64.FromInt(36), FP64.FromInt(80) };    // px/s
        public static readonly FP64[] Range         = { FP64.FromInt(110), FP64.FromInt(72) };   // px
        public static readonly int[]  CooldownTicks = { 18, 14 };
        public static readonly int[]  Damage        = { 3, 1 };
        public static readonly FP64[] BulletSpeed   = { FP64.FromInt(170), FP64.FromInt(220) };  // px/s
        public static readonly FP64[] Radius        = { FP64.FromInt(9), FP64.FromInt(5) };      // hit radius, px
        public static readonly FP64[] MuzzleOffset  = { FP64.FromInt(10), FP64.FromInt(6) };
        public const int BulletTtlTicks = 40;

        // Spawn anchor (flagship position) and forward/side basis per player id. Exact integer bases so
        // formations need no trig. Players face the arena center.
        static readonly int[,] Anchor  = { { 84, 196 }, { 556, 196 }, { 320, 76 }, { 320, 316 } };
        static readonly int[,] Forward = { { 1, 0 },    { -1, 0 },    { 0, 1 },    { 0, -1 } };
        static readonly int[,] Side    = { { 0, 1 },    { 0, -1 },    { -1, 0 },   { 1, 0 } };

        // Formation offsets (forward, side) in px by slot. Flagship at the anchor, fighters in two
        // staggered rows ahead of it.
        static readonly int[,] Formation =
        {
            { 0, 0 },
            { 30, -14 }, { 30, 14 }, { 30, -42 }, { 30, 42 },
            { 14, -28 }, { 14, 28 }, { 14, -56 }, { 14, 56 },
        };

        public static int KindOf(int slot) => slot == 0 ? KindFlagship : KindFighter;

        public static FPVector2 SpawnPosition(int playerId, int slot)
        {
            int p = playerId & 3;
            int f = Formation[slot, 0], s = Formation[slot, 1];
            int x = Anchor[p, 0] + Forward[p, 0] * f + Side[p, 0] * s;
            int y = Anchor[p, 1] + Forward[p, 1] * f + Side[p, 1] * s;
            return new FPVector2(FP64.FromInt(x), FP64.FromInt(y));
        }

        // Facing angle (radians) toward the arena center for the player's spawn side.
        public static FP64 SpawnFacing(int playerId)
        {
            int p = playerId & 3;
            return FP64.Atan2(FP64.FromInt(Forward[p, 1]), FP64.FromInt(Forward[p, 0]));
        }

        public static FPVector2 Clamp(FPVector2 v)
            => new FPVector2(FP64.Clamp(v.x, MinX, MaxX), FP64.Clamp(v.y, MinY, MaxY));
    }
}

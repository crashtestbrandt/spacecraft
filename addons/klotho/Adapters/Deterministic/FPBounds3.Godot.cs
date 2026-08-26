// FPBounds3 <-> Godot.Aabb conversion helpers.
//
// Layout difference:
//   FPBounds3 : center + extents (half-size)
//   Godot.Aabb: Position (min corner) + Size (full size)
//
// FPBounds3(center, size) constructor takes full size and halves it internally.
// Always pass full size — passing extents (half-size) would produce 1/4-size bounds.
using xpTURN.Klotho.Deterministic.Math;

namespace xpTURN.Klotho.Deterministic.Geometry
{
    public static class FPBounds3Extensions
    {
        public static global::Godot.Aabb ToAabb(this FPBounds3 @this)
            => new global::Godot.Aabb(
                @this.min.ToVector3(),    // Position = min corner (center - extents)
                @this.size.ToVector3()    // Size = full size (extents * 2)
            );

        public static FPBounds3 ToFPBounds3(this global::Godot.Aabb @this)
        {
            var center = (@this.Position + @this.Size * 0.5f).ToFPVector3();
            return new FPBounds3(center, @this.Size.ToFPVector3());  // full size
        }
    }
}

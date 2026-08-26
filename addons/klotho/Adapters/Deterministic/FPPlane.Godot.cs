// FPPlane <-> Godot.Plane conversion helpers.
//
// Sign convention difference (NOT related to coordinate system handedness):
//   FPPlane : Dot(normal, p) + distance = 0  →  distance = -Dot(normal, p)  (negative)
//   Godot.Plane : Dot(Normal, p) = D         →  D = Dot(Normal, p)           (positive)
//   Therefore: D = -FPPlane.distance
//
// Do NOT bypass these methods and construct Godot.Plane directly from FPPlane.distance —
// the sign would be wrong and all distance/side queries would return inverted results.
using xpTURN.Klotho.Deterministic.Math;

namespace xpTURN.Klotho.Deterministic.Geometry
{
    public static class FPPlaneExtensions
    {
        public static global::Godot.Plane ToPlane(this FPPlane @this)
            => new global::Godot.Plane(@this.normal.ToVector3(), -@this.distance.ToFloat());

        public static FPPlane ToFPPlane(this global::Godot.Plane @this)
            => new FPPlane(@this.Normal.ToFPVector3(), FP64.FromFloat(-@this.D));
    }
}

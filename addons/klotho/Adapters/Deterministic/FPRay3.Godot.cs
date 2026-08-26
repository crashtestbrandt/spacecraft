// FPRay3 <-> Godot conversion helpers.
// Godot has no dedicated Ray3D struct. Conversions produce a (origin, direction) tuple or a
// PhysicsRayQueryParameters3D for physics queries.
//
// Coordinate note: ToVector3() maps simulation space directly (+Z=forward, no Z-flip).
// ToRayQuery produces a query in simulation coordinate space — only accurate when Godot physics
// objects are placed in the same coordinate frame (as in Klotho samples). Mixing with objects
// placed in Godot-native space (−Z=forward) will produce incorrect intersection results.
using xpTURN.Klotho.Deterministic.Math;

namespace xpTURN.Klotho.Deterministic.Geometry
{
    public static class FPRay3Extensions
    {
        public static (global::Godot.Vector3 origin, global::Godot.Vector3 direction) ToRay(this FPRay3 @this)
            => (@this.origin.ToVector3(), @this.direction.ToVector3());

        public static FPRay3 ToFPRay3(this (global::Godot.Vector3 origin, global::Godot.Vector3 direction) ray)
            => new FPRay3(ray.origin.ToFPVector3(), ray.direction.ToFPVector3());

        public static global::Godot.PhysicsRayQueryParameters3D ToRayQuery(this FPRay3 @this, float maxLength = 1000f)
        {
            var origin = @this.origin.ToVector3();
            return global::Godot.PhysicsRayQueryParameters3D.Create(
                origin,
                origin + @this.direction.ToVector3() * maxLength
            );
        }
    }
}

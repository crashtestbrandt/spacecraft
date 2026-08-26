// FP64 <-> Godot Vector3 conversion helpers.
namespace xpTURN.Klotho.Deterministic.Math
{
    public static class FPVector3Extensions
    {
        public static FPVector3 FromVector3(this ref FPVector3 @this, global::Godot.Vector3 v)
        {
            @this.x = FP64.FromFloat(v.X);
            @this.y = FP64.FromFloat(v.Y);
            @this.z = FP64.FromFloat(v.Z);
            return @this;
        }

        public static FPVector3 ToFPVector3(this global::Godot.Vector3 @this)
        {
            return new FPVector3(
                @this.X.ToFP64(),
                @this.Y.ToFP64(),
                @this.Z.ToFP64()
            );
        }

        public static global::Godot.Vector3 ToVector3(this FPVector3 @this)
        {
            return new global::Godot.Vector3(@this.x.ToFloat(), @this.y.ToFloat(), @this.z.ToFloat());
        }
    }
}

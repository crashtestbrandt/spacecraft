// FP64 <-> Godot Vector4 conversion helpers.
namespace xpTURN.Klotho.Deterministic.Math
{
    public static class FPVector4Extensions
    {
        public static FPVector4 FromVector4(this ref FPVector4 @this, global::Godot.Vector4 v)
        {
            @this.x = FP64.FromFloat(v.X);
            @this.y = FP64.FromFloat(v.Y);
            @this.z = FP64.FromFloat(v.Z);
            @this.w = FP64.FromFloat(v.W);
            return @this;
        }

        public static FPVector4 ToFPVector4(this global::Godot.Vector4 @this)
        {
            return new FPVector4(
                @this.X.ToFP64(),
                @this.Y.ToFP64(),
                @this.Z.ToFP64(),
                @this.W.ToFP64()
            );
        }

        public static global::Godot.Vector4 ToVector4(this FPVector4 @this)
        {
            return new global::Godot.Vector4(@this.x.ToFloat(), @this.y.ToFloat(), @this.z.ToFloat(), @this.w.ToFloat());
        }
    }
}

// FP64 <-> Godot Vector2 conversion helpers.
namespace xpTURN.Klotho.Deterministic.Math
{
    public static class FPVector2Extensions
    {
        public static FPVector2 FromVector2(this ref FPVector2 @this, global::Godot.Vector2 v)
        {
            @this.x = FP64.FromFloat(v.X);
            @this.y = FP64.FromFloat(v.Y);
            return @this;
        }

        public static FPVector2 ToFPVector2(this global::Godot.Vector2 @this)
        {
            return new FPVector2(
                @this.X.ToFP64(),
                @this.Y.ToFP64()
            );
        }

        public static global::Godot.Vector2 ToVector2(this FPVector2 @this)
        {
            return new global::Godot.Vector2(@this.x.ToFloat(), @this.y.ToFloat());
        }
    }
}

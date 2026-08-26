// FP64 <-> Godot Quaternion conversion helpers.
namespace xpTURN.Klotho.Deterministic.Math
{
    public static class FPQuaternionExtensions
    {
        public static FPQuaternion FromQuaternion(this ref FPQuaternion @this, global::Godot.Quaternion q)
        {
            @this.x = FP64.FromFloat(q.X);
            @this.y = FP64.FromFloat(q.Y);
            @this.z = FP64.FromFloat(q.Z);
            @this.w = FP64.FromFloat(q.W);
            return @this;
        }

        public static FPQuaternion ToFPQuaternion(this global::Godot.Quaternion @this)
        {
            return new FPQuaternion(
                FP64.FromFloat(@this.X),
                FP64.FromFloat(@this.Y),
                FP64.FromFloat(@this.Z),
                FP64.FromFloat(@this.W)
            );
        }

        public static global::Godot.Quaternion ToQuaternion(this FPQuaternion @this)
        {
            return new global::Godot.Quaternion(
                @this.x.ToFloat(),
                @this.y.ToFloat(),
                @this.z.ToFloat(),
                @this.w.ToFloat()
            );
        }
    }
}

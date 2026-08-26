using xpTURN.Klotho.Core;

namespace xpTURN.Klotho.Godot
{
    public static class GodotFlowSetupBuilderExtensions
    {
        public static KlothoFlowSetupBuilder WithGodotDefaults(this KlothoFlowSetupBuilder builder)
        {
            var ver = global::Godot.ProjectSettings.GetSetting("application/config/version").AsString();
            if (string.IsNullOrEmpty(ver)) ver = "0.0.0";
            return builder.WithHandshake(ver, new GodotDeviceIdProvider());
        }
    }
}

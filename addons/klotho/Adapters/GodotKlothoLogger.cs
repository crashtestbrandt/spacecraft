using xpTURN.Klotho.Logging;

namespace xpTURN.Klotho.Godot
{
    // Convenience entry point: GodotLogSink (console) + RollingFileSink under user://logs.
    //
    // Factory Dispose limitation: only IKLogger is returned; the internal IKLoggerFactory
    // cannot be Disposed by the caller. For explicit flush/close on shutdown, build the
    // factory directly with KLoggerFactory.Create and store it separately (see samples).
    public static class GodotKlothoLogger
    {
        public static IKLogger CreateDefault(
            KLogLevel level = KLogLevel.Information,
            string filePrefix = "Client",
            string categoryName = "Client",
            int rollingSizeKB = 1024 * 1024,
            string directory = null,  // null → ProjectSettings.GlobalizePath("user://logs")
            string timestampFormat = null)  // null → sink default (yyyy-MM-dd HH:mm:ss.fff)
        {
            var logDir = directory
                ?? global::Godot.ProjectSettings.GlobalizePath("user://logs");
            System.IO.Directory.CreateDirectory(logDir);

            var factory = KLoggerFactory.Create(builder =>
            {
                builder.SetMinimumLevel(level);
                builder.AddSink(new GodotLogSink());
                builder.AddRollingFile(options =>
                {
                    options.FilePrefix = filePrefix;
                    options.RollingSizeKB = rollingSizeKB;
                    options.Directory = logDir;
                    options.TimestampFormat = timestampFormat;
                });
            });

            return factory.CreateLogger(categoryName);
        }
    }
}

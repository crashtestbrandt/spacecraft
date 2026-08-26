using System;
using global::Godot;
using xpTURN.Klotho.Logging;

namespace xpTURN.Klotho.Godot
{
    // IKLogger sink routing Klotho logs to the Godot console.
    public sealed class GodotDebugSink : IKLogger
    {
        private readonly KLogLevel _minLevel;

        public GodotDebugSink(KLogLevel minLevel = KLogLevel.Information)
        {
            _minLevel = minLevel;
        }

        public bool IsEnabled(KLogLevel level) => level >= _minLevel;

        public void Log(KLogLevel level, string message, Exception exception)
        {
            if (!IsEnabled(level))
                return;

            string text = exception == null ? message : $"{message}\n{exception}";
            switch (level)
            {
                case KLogLevel.Error:
                    GD.PushError(text);
                    break;
                case KLogLevel.Warning:
                    GD.PushWarning(text);
                    break;
                default:
                    GD.Print(text);
                    break;
            }
        }
    }

    // IKLogSink routing to the Godot console — composable with other sinks (e.g. RollingFileSink)
    // via KLoggerFactory.Create(b => b.AddSink(new GodotLogSink()).AddRollingFile(...)).
    // Level filtering is handled by the factory; this sink writes what it receives.
    public sealed class GodotLogSink : IKLogSink
    {
        public void Write(KLogLevel level, string message, Exception exception)
        {
            string text = exception == null ? message : $"{message}\n{exception}";
            switch (level)
            {
                case KLogLevel.Error:
                    GD.PushError(text);
                    break;
                case KLogLevel.Warning:
                    GD.PushWarning(text);
                    break;
                default:
                    GD.Print(text);
                    break;
            }
        }

        public void Flush() { }
        public void Dispose() { }
    }

    // Minimal IKLoggerFactory producing GodotDebugSink instances.
    public sealed class GodotKLoggerFactory : IKLoggerFactory
    {
        private readonly KLogLevel _minLevel;

        public GodotKLoggerFactory(KLogLevel minLevel = KLogLevel.Information)
        {
            _minLevel = minLevel;
        }

        public IKLogger CreateLogger(string category) => new GodotDebugSink(_minLevel);

        public void Dispose() { }
    }
}

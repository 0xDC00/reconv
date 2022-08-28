using System;

namespace ExR.Format
{
    public class Logger
    {
        public event LogEventHandler LogEvent;

        public string Name { get; }

        public Logger(string name)
        {
            Name = name;
        }

        public void Log(LogLevel level, string message)
        {
            LogEvent?.Invoke(this, new LogEventArgs(Name, level, message));
        }

        public void Debug(string message)
        {
            Log(LogLevel.Debug, message);
        }

        public void Trace(string message)
        {
            Log(LogLevel.Trace, message);
        }

        public void Info(string message)
        {
            Log(LogLevel.Info, message);
        }

        public void Warning(string message)
        {
            Log(LogLevel.Warning, message);
        }

        public void Error(string message)
        {
            Log(LogLevel.Error, message);
        }

        public void Fatal(string message)
        {
            Log(LogLevel.Fatal, message);
        }
    }

    public delegate void LogEventHandler(object sender, LogEventArgs e);

    [Flags]
    public enum LogLevel
    {
        Debug = 1 << 1,
        Trace = 1 << 2,
        Info = 1 << 3,
        Warning = 1 << 4,
        Error = 1 << 5,
        Fatal = 1 << 6,
        All = Debug | Trace | Info | Warning | Error | Fatal,
    }

#if !BLAZOR
    public class ConsoleLogListener : LogListener
    {
        public bool UseColors { get; set; }

        public ConsoleLogListener(bool useColors, LogLevel filter) : base(filter)
        {
            UseColors = useColors;
        }

        public ConsoleLogListener(string channelName, bool useColors) : base(channelName)
        {
            UseColors = useColors;
        }

        protected override void OnLogCore(object sender, LogEventArgs e)
        {
            ConsoleColor prevColor = 0;

            if (UseColors)
            {
                prevColor = Console.ForegroundColor;
                Console.ForegroundColor = GetConsoleColorForSeverityLevel(e.Level);
            }

            Console.WriteLine($"{DateTime.Now} {e.ChannelName} {e.Level}: {e.Message}");

            if (UseColors)
            {
                Console.ForegroundColor = prevColor;
            }
        }

        private ConsoleColor GetConsoleColorForSeverityLevel(LogLevel level)
        {
            switch (level)
            {
                case LogLevel.Debug:
                    return ConsoleColor.Gray;
                case LogLevel.Info:
                    return ConsoleColor.Green;
                case LogLevel.Warning:
                    return ConsoleColor.Yellow;
                case LogLevel.Error:
                    return ConsoleColor.Red;
                case LogLevel.Fatal:
                    return ConsoleColor.DarkRed;
                default:
                    return ConsoleColor.White;
            }
        }
    }
#endif

    public class LogEventArgs : EventArgs
    {
        public string ChannelName { get; }

        public LogLevel Level { get; }

        public string Message { get; }

        public LogEventArgs(string channelName, LogLevel level, string message)
        {
            Level = level;
            Message = message;
        }
    }

    public abstract class LogListener
    {
        public string ChannelName { get; }

        public LogLevel Filter { get; set; } = LogLevel.All;

        public LogListener()
        {

        }

        public LogListener(LogLevel filter)
        {
            Filter = filter;
        }

        public LogListener(string channelName)
        {
            ChannelName = channelName;
        }

        public void Subscribe(Logger logger)
        {
            logger.LogEvent += OnLog;
        }

        public void Unsubscribe(Logger logger)
        {
            logger.LogEvent -= OnLog;
        }

        protected void OnLog(object sender, LogEventArgs e)
        {
            if (Filter.HasFlag(e.Level))
                OnLogCore(sender, e);
        }

        protected abstract void OnLogCore(object sender, LogEventArgs e);
    }
}

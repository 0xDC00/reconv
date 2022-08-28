using System;
using ExR.Format;

namespace ExR
{
    public class XTermLogListener : LogListener
    {
        Action<object, LogEventArgs> onLogCore;

        public XTermLogListener(bool useColors, LogLevel filter) : base(filter)
        {
            onLogCore = useColors ? onLogCoreWithColor : onLogCoreNoColor;
        }

        public XTermLogListener(string channelName, bool useColors) : base(channelName)
        {
            onLogCore = useColors ? onLogCoreWithColor : onLogCoreNoColor;
        }

        void onLogCoreNoColor(object sender, LogEventArgs e)
        {
            var i = GetConsoleColorForSeverityLevel(e.Level);
            //Console.WriteLine($"{DateTime.Now} {e.ChannelName} {e.Level}: {e.Message}");
            Console.WriteLine($"[{DateTime.Now.ToString("HH:mm:ss", System.Globalization.DateTimeFormatInfo.InvariantInfo)}] {LevelText[i]}: {e.Message}");
        }

        void onLogCoreWithColor(object sender, LogEventArgs e)
        {
            var i = GetConsoleColorForSeverityLevel(e.Level);
            //Console.WriteLine($"{GetConsoleColorForSeverityLevel(e.Level)}{DateTime.Now} {e.ChannelName} {e.Level}: {e.Message}\x1B[0m");
            Console.WriteLine($"{LevelColor[i]}[{DateTime.Now.ToString("HH:mm:ss", System.Globalization.DateTimeFormatInfo.InvariantInfo)}] {LevelText[i]}: {e.Message}\x1B[0m");
        }

        protected override void OnLogCore(object sender, LogEventArgs e)
        {
            onLogCore(sender, e);
        }
        string[] LevelText = new string[]
        {
            "|D|", "|I|", "|W|", "|E|", "|F|", string.Empty
        };
        string[] LevelColor = new string[]
        {
            "\x1b[1;37m", "\x1b[1;32m", "\x1b[1;33m", "\x1b[1;31m", "\x1b[0;31m", "\x1B[0m"
        };

        private int GetConsoleColorForSeverityLevel(LogLevel level)
        {
            switch (level)
            {
                case LogLevel.Debug:
                    return 0;
                case LogLevel.Info:
                    return 1;
                case LogLevel.Warning:
                    return 2;
                case LogLevel.Error:
                    return 3;
                case LogLevel.Fatal:
                    return 4;
                default:
                    return 5;
            }
        }
    }
}

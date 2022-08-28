using ExR.Format;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using Zio;
using Zio.FileSystems;

namespace ExR
{
    class Program
    {
        static Version Version = Assembly.GetExecutingAssembly().GetName().Version;
        static string _inPath;
        static string _outPath;
        static string _command; // init textformat
        static bool _doExtract;
        static Logger Log = new Logger(nameof(ExR));
        static LogListener Listener = new ConsoleLogListener(true, LogLevel.Info | LogLevel.Warning | LogLevel.Error | LogLevel.Fatal | LogLevel.Debug);

        public static void Main(string[] args)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance); // More encoding
            Listener.Subscribe(Log);

            Console.OutputEncoding = Encoding.UTF8;
            Console.Title = "[???]";
            var fistLine = $"[???] {Version.Major}.{Version.Minor} by [DC] (2017)";
            Console.WriteLine(fistLine);
            Console.WriteLine();
            if (args.Length == 0)
            {
                Log.Error("No arguments specified!");
                DisplayUsage();
                return;
            }
            else if (!TryParseArguments(args))
            {
                Log.Error("Failed to parse arguments!");
                DisplayUsage();
                return;
            }
            else
            {
                if (string.IsNullOrEmpty(_command))
                {
                    Log.Error("-e plugin ?");
                    DisplayUsage();
                    return;
                }
                else
                {
                    // init
                    TextFormat _textFormat = InitTextFormat();
                    if (_textFormat == null)
                    {
                        Log.Error("-e plugin ?");
                        DisplayUsage();
                        return;
                    }

                    // Run
                    var stopwatch = new System.Diagnostics.Stopwatch();
                    stopwatch.Start();
                    Console.WriteLine(fistLine);
                    Console.WriteLine();

                    var conv = new TextConv();
                    conv.Log = Log;
                    conv.Convert = _textFormat;

                    if (_doExtract)
                    {
                        if (_inPath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                        {
                            var inZip = File.Open(_inPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

                            if (_outPath == null)
                                _outPath = Path.ChangeExtension(_inPath, null) + "_out";

                            conv.Extract(inZip, _outPath).Wait();
                        }
                        else
                        {
                            if (File.Exists(_inPath))
                            {
                                var fs = new ReadOnlySingleFileSystem(_inPath);
                                var dir = fs.GetDirectory();
                                var fsi = new SubFileSystem(fs, dir);

                                if (_outPath == null)
                                    _outPath = fs.ConvertPathToInternal(dir);

                                conv.Extract(fsi, _outPath).Wait();
                            }
                            else if (Directory.Exists(_inPath))
                            {
                                if (_outPath == null)
                                    _outPath = _inPath + "_out";

                                conv.Extract(_inPath, _outPath).Wait();
                            }
                            else
                            {
                                Log.Error("Input not found!");
                            }
                        }
                    }
                    else
                    {
                        if (_inPath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                        {
                            var inZip = File.Open(_inPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                            if (_outPath == null)
                                _outPath = Path.ChangeExtension(_inPath, null) + "_out";

                            conv.Repack(inZip, _outPath).GetAwaiter().GetResult();
                        }
                        else if (_inPath.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
                        {
                            var inXlsx = File.Open(_inPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                            if (_outPath == null)
                                _outPath = Path.ChangeExtension(_inPath, null) + "_out";

                            conv.RepackXlsx(inXlsx, _outPath).Wait();
                        }
                        else if (_inPath.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                        {
                            using (var client = new System.Net.Http.HttpClient())
                            {
                                var data = client.GetAsync(_inPath).Result.Content.ReadAsStreamAsync().GetAwaiter().GetResult();
                                if (_outPath == null)
                                    _outPath = "xlsx_" + DateTime.Now.Ticks;

                                conv.RepackXlsx(data, _outPath).Wait();
                            }
                        }
                        else if (File.Exists(_inPath))
                        {
                            var fs = new ReadOnlySingleFileSystem(_inPath);
                            var dir = fs.GetDirectory();
                            var fsi = new SubFileSystem(fs, dir);

                            if (_outPath == null)
                                _outPath = Path.ChangeExtension(_inPath, null) + "_out";

                            conv.Repack(fsi, _outPath).Wait();
                        }
                        else
                        {
                            if (_outPath == null)
                                _outPath = _inPath + "_out";

                            conv.Repack(_inPath, _outPath).Wait();
                        }

                    }

                    stopwatch.Stop();
                    Console.WriteLine();
                    Log.Debug(string.Format("Time Elapsed {0:hh\\:mm\\:ss}", stopwatch.Elapsed));
                    Log.Info("Build success");
                }
            }

            Console.Write("Press any key to continue...");
            Console.ReadKey();
        }

        //[System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Calls GetTypes")]
        static TextFormat InitTextFormat()
        {
            TextFormat Text = null;
            var typeInfo = typeof(TextFormat).GetTypeInfo();
            // GetTypesInNamespace
            var types = typeInfo.Assembly.GetTypes()
                .Where(t => string.Equals(t.Namespace, typeInfo.Namespace, StringComparison.Ordinal));

            var typeofPluginAtt = typeof(PluginAttribute);
            foreach (var type in types)
            {
                var att = type.GetCustomAttribute(typeofPluginAtt);
                if (att != null)
                {
                    var meta = (PluginAttribute)att;
                    if (meta.Command.Equals(_command, StringComparison.OrdinalIgnoreCase))
                    {
                        Log.Warning(meta.Name);
                        Log.Warning(meta.Description);
                        Text = (TextFormat)Activator.CreateInstance(type);
                    }
                }
            }

            return Text;
        }

        static void PrintListCommand()
        {
            var typeInfo = typeof(TextFormat).GetTypeInfo();
            // GetTypesInNamespace
            var types = typeInfo.Assembly.GetTypes()
                .Where(t => string.Equals(t.Namespace, typeInfo.Namespace, StringComparison.Ordinal));

            var prevColor = Console.ForegroundColor;
            var typeofPluginAtt = typeof(PluginAttribute);
            foreach (var type in types)
            {
                var att = type.GetCustomAttribute(typeofPluginAtt);
                if (att != null)
                {
                    var meta = (PluginAttribute)att;
                    Console.WriteLine("_________________________________________________________");
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.Write("    ");
                    Console.WriteLine(meta.Command);
                    Console.Write("    ");
                    Console.WriteLine(meta.Name);
                    Console.ForegroundColor = prevColor;
                    Console.Write("    ");
                    Console.WriteLine(meta.Description);
                }
            }
        }

        static bool TryParseArguments(string[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {
                bool isLast = i + 1 == args.Length;
                var arg = args[i].ToLower();
                switch (arg)
                {
                    // General
                    //case "-g":
                    //case "-game":
                    //    _command = args[++i];
                    //    break;

                    case "-i":
                    case "-in":
                        if (isLast)
                        {
                            Log.Error("Missing argument for -i parameter");
                            return false;
                        }

                        _inPath = args[++i];
                        break;

                    case "-o":
                    case "-out":
                        if (isLast)
                        {
                            Log.Error("Missing argument for -o parameter");
                            return false;
                        }

                        _outPath = args[++i];
                        break;

                    case "-e":
                    case "-extract":
                        _doExtract = true;
                        if (isLast)
                        {
                            Log.Error("Missing argument for -e parameter");
                            return false;
                        }
                        _command = args[++i];
                        break;

                    case "-r":
                    case "-repack":
                        _doExtract = false;
                        if (isLast)
                        {
                            Log.Error("Missing argument for -r parameter");
                            return false;
                        }
                        _command = args[++i];
                        break;

                    default:
                        Log.Warning(arg);
                        break;
                }
            }

            return true;
        }

        static void DisplayUsage()
        {
            Console.WriteLine("Parameter overview:");
            Console.WriteLine("    General:");
            Console.WriteLine("        -In            <path>                     Input, path to packfiles or folder.");
            Console.WriteLine("        -Out           <path>                     Output, path to packfiles or folder (place binary to folder if repack mode = Insert)");
            Console.WriteLine("        -Extract       <type>                     Extract text.");
            Console.WriteLine("        -Repack        <type>                     Repack\\Insert text, Game type (see below).");
            Console.WriteLine();
            Console.WriteLine("Parameter detailed info:");
            Console.WriteLine("    Extract|Repack");
            Console.WriteLine("            <type>");
            Console.WriteLine("            ds         [Insert] Demon's Souls, Dark Souls PTD (*.fmg)."); // TODO: read from Attribute.
            Console.WriteLine();
            Console.WriteLine("Examples:");
            Console.WriteLine("        -Extract dc -In target/DraCroFMSB -Out extracted/DraCroCSV");
            Console.WriteLine("        -Repack dc -In extracted/DraCroCSV -Out repacked/DraCroFMSB");
            Console.WriteLine("        -e dc -i DraCro.xlsx -o repacked/DraCroFMSB");
            Console.WriteLine();
            Console.WriteLine("Plugins:");
            PrintListCommand();

            Console.Write("Press any key to continue...");
            Console.ReadKey();
        }
    }
}

using ExR.Format;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Zio;

namespace ExR
{
    public partial class TextConv
    {
        private static readonly UPath INIT_FILE_PATH = "/_init_.yaml";

        private async Task<bool> RunInitAsync(Dictionary<string, object> dict)
        {
            // https://github.com/dotnet/aspnetcore/blob/52eff90fbcfca39b7eb58baad597df6a99a542b0/src/Components/Components/src/ComponentBase.cs#L226
            var result = Convert.Init(dict);
            if (result)
            {
                result = await Convert.InitAsync(dict);
            }
            return result;
        }

        /// <summary>
        /// Return
        ///    1: _init_.yaml loaded
        ///   -1: _init_.yaml error
        ///    2: 0 _init_.yaml loaded
        ///   -2: need _init_.yaml
        /// </summary>
        /// <returns></returns>
        private async Task<int> LoadInitYaml()
        {
            bool isInitExits = Convert.FsIn.FileExists(INIT_FILE_PATH);
            if (isInitExits)
            {
                try
                {
                    // var yaml = fsIn.ReadAllText(INIT_FILE_PATH);
                    Dictionary<string, object> dict;
                    using (var stream = Convert.FsIn.OpenFile(INIT_FILE_PATH, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        //dict = new YamlDotNet.Serialization.DeserializerBuilder().Build().Deserialize<Dictionary<string, object>>(new StreamReader(stream, Encoding.UTF8, true));
                        dict = new SharpYaml.Serialization.Serializer().Deserialize<Dictionary<string, object>>(stream);
                    }

                    DeleteInitYaml();
                    //if (Convert.Init(dict, Convert.RunMode) == false)
                    if (await RunInitAsync(dict) == false)
                    {
                        RetoreInitYaml();
                        Log.Error("_init_.yaml not valid!");
                        return -1;
                    }
                    return 1;
                }
                catch (Exception ex)
                {
                    RetoreInitYaml();
                    Log.Error(ex.Message);
                    return -10;
                }
            }
            else
            {
                try
                {
                    //if (Convert.Init(new Dictionary<string, object>(), Convert.RunMode) == false)
                    if (await RunInitAsync(new Dictionary<string, object>()) == false)
                    {
                        Log.Error("_init_.yaml not found!");
                        return -2;
                    }
                    return 2;
                }
                catch (Exception ex)
                {
                    Log.Error(ex.Message);
                    return -20;
                }
            }

        }
#if !BLAZOR
        byte[] initYaml = null;
#endif
        private void DeleteInitYaml()
        {
            //if (OperatingSystem.IsBrowser() == false) // blazor no need restore
            //    initYaml = Convert.FsIn.ReadAllBytes(INIT_FILE_PATH);
#if !BLAZOR
            initYaml = Convert.FsIn.ReadAllBytes(INIT_FILE_PATH);
#endif
            Convert.FsIn.DeleteFile(INIT_FILE_PATH);
        }
        private void RetoreInitYaml()
        {
#if !BLAZOR
            if (initYaml != null)
                Convert.FsIn.WriteAllBytes(INIT_FILE_PATH, initYaml);
#endif
        }

        private async Task Extract(IFileSystem fsIn, IFileSystem fsOut)
        {
            Log.Info("Extracting...");
            Convert.FsIn = fsIn;
            Convert.FsOut = fsOut;
            Convert.RunMode = TextFormat.Mode.Extract;

            // try load custom args
            if (await LoadInitYaml() < 0)
            {
                return;
            }

            // TODO: sort input
            // TODO: custom _textIO

            // start
            int count = 0;
            foreach (var file in fsIn.EnumerateFiles(UPath.Root, "*", SearchOption.AllDirectories))
            {
                //if (file == INIT_FILE_PATH) // extract skip _init_.yaml
                //    continue;

                if (Convert.Extensions != null && Convert.Extensions.Length > 0)
                {
                    if (!Convert.Extensions.Any(x => file.FullName.EndsWith(x, StringComparison.OrdinalIgnoreCase)))
                    {
                        continue;
                    }
                }
                
                Log.Info($"[Extract.Begin] {file}");
                try
                {
                    // bif file will null
                    byte[] buf = null;
                    if (fsIn.GetFileEntry(file).Length <= int.MaxValue)
                    {
                        buf = fsIn.ReadAllBytes(file);
                    }
                    else
                    {
                        Log.Warning(">2GB file, FsIN");
                    }
                    //await Task.Yield();  // give the UI some time to catch up
                    await Task.Delay(2);

                    Convert.CurrentFilePath = file.FullName;

                    var lines = Convert.ExtractText(buf);
                    if (lines == null)
                    {
                        Log.Error($"[Extract.Null] {file}");
                    }
                    else if (lines.Count == 0)
                    {
                        Log.Warning($"{lines.Count} [Extract.Empty] {file}");
                    }
                    else
                    {
                        Log.Debug($"[Extract.{lines.Count}] {file}");
                        var fileDir = file.GetDirectory();
                        if (fileDir != UPath.Root)
                        {
                            fsOut.CreateDirectory(fileDir);
                        }

                        _textIO.WriteAllLines(fsOut.CreateFile(file + _textIO.Extension), lines);
                        count += lines.Count;
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex.Message);
                    if (ex.StackTrace != string.Empty)
                        Log.Error("\n" + ex.StackTrace);
                }
            }

            // restore yaml
            Convert.End();
            RetoreInitYaml();

            if (count > 0)
            {
                Log.Info($"Extracted: {count} lines");
                // manual or 
                //if (isInitExits)
                //{
                //    _textIO.WriteAllLines(fsOut.CreateFile(INIT_FILE_PATH + _textIO.Extension), new List<Line>()
                //    {
                //        new Line()
                //        {
                //            ID = fsIn.ReadAllText(INIT_FILE_PATH)
                //        }
                //    });
                //}
            }
        }

        private async Task Repack(IFileSystem fsIn, IFileSystem fsOut)
        {
            Log.Info("Repacking...");
            Convert.FsIn = fsIn;
            Convert.FsOut = fsOut;
            Convert.RunMode = TextFormat.Mode.Repack;
            // try load custom args
            if (await LoadInitYaml() < 0)
            {
                return;
            }

            foreach (var pathToCsv in fsIn.EnumerateFiles(UPath.Root, "*", SearchOption.AllDirectories))
            {
                //if (pathToCsv.FullName == INIT_FILE_PATH) // repack skip _init_.yaml
                //    continue;

                if (pathToCsv.FullName.EndsWith(_textIO.Extension, StringComparison.OrdinalIgnoreCase) == false)
                {
                    //Log.Warning("Skip: " + file.FullName); // slient warning
                    continue;
                }

                var outBinName = Path.ChangeExtension(pathToCsv.FullName, null);
                //Convert.CurrentFilePath = outBinName;

                try
                {
                    if (pathToCsv.FullName.EndsWith(".csv.n.csv", StringComparison.OrdinalIgnoreCase))
                    {
                        var linex = ReadCsvLines(pathToCsv);
                        for (int i = 0; i < linex.Count; i++)
                        {
                            var id = linex[i].ID.Split('|', 2);
                            var count = int.Parse(id[0]);
                            var curCsvPath = id[1];
                            var lines = linex.GetRange(i + 1, count);
                            i += count;

                            outBinName = Path.ChangeExtension(curCsvPath, null);
                            await RepackLines(lines, outBinName);
                        }
                    }
                    else if (pathToCsv.FullName.EndsWith(".csv.x.csv", StringComparison.OrdinalIgnoreCase))
                    {
                        // id,english,vietnamese,note
                        // path
                        // count
                        // lines
                        // path
                        // count
                        // lines

                        // TODO: ReWrite
                        var dict = new Dictionary<string, List<Line>>();
                        using (var csvFile = Convert.FsIn.OpenFile(pathToCsv, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        using (var sr = new StreamReader(csvFile))
                        {
                            var priorityQueue = new Priority_Queue.SimplePriorityQueue<Line>(); // SimplePriorityQueue vs FastPriorityQueue
                            var curCsvPath = string.Empty;
                            int count = -1;
                            _ = fastCSV.ReadStream<Line>(sr, true, ',', (line, c) =>
                            {
                                var id = c[0];
                                if (id == string.Empty)
                                    return false; // skip empty row

                                if (curCsvPath == string.Empty)
                                {
                                    curCsvPath = id;
                                    return false;
                                }
                                else if (count == -1)
                                {
                                    count = int.Parse(id);
                                    return false;
                                }
                                else
                                {
                                    count--;
                                    line = new Line(); // No reflection emit
                                    line.ID = id;
                                    line.Note = c[3];
                                    //line.English = c[1];
                                    //line.Vietnamese = c[2];
                                    var eng = c[1];
                                    var vie = c[2];
                                    if (vie != string.Empty)
                                    {
                                        line.English = vie;
                                        line.Vietnamese = vie;
                                    }
                                    else
                                    {
                                        line.English = eng;
                                        line.Vietnamese = eng;
                                    }

                                    var prio = line.TrimIdIndex();
                                    priorityQueue.Enqueue(line, prio);

                                    if (count == 0)
                                    {
                                        outBinName = Path.ChangeExtension(curCsvPath, null);
                                        var lines = priorityQueue.ToList();
                                        dict.Add(((UPath)outBinName).ToAbsolute().FullName, lines);

                                        count = -1;
                                        curCsvPath = string.Empty;
                                        priorityQueue.Clear();
                                    }

                                    return false; // we use PQ
                                }
                            });
                        }
                        foreach (var item in dict)
                        {
                            await RepackLines(item.Value, item.Key);
                        }
                    }
                    else
                    {
                        var lines = ReadCsvLines(pathToCsv);
                        await RepackLines(lines, outBinName);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex.Message);
                    if (ex.StackTrace != string.Empty) 
                        Log.Error("\n" + ex.StackTrace);
                }
            }

            Convert.End();
            RetoreInitYaml();

            Log.Info("All done.");
        }

        private async Task RepackLines(List<Line> lines, string outBinName)
        {
            if (lines != null)
            {
                Convert.CurrentFilePath = outBinName;
                Log.Info($"[Repack.Begin] {outBinName}"); // use outBinName instead CurrentFilePath (public)
                await Task.Delay(2); //await Task.Yield();  // give the UI some time to catch up
                var buf = Convert.RepackText(lines);
                if (buf == null)
                {
                    Log.Warning("[Repack.NULL]");
                }
                else if (buf.Length == 0)
                {
                    Log.Warning("[Repack.0Byte]");
                }
                else
                {
                    var outPath = new UPath(outBinName);
                    var outPathDir = outPath.GetDirectory();
                    if (outPathDir != UPath.Root)
                        Convert.FsOut.CreateDirectory(outPathDir);
                    Convert.FsOut.WriteAllBytes(outPath, buf);
                }
            }
            else
            {
                // input not valid, do nothing
            }
        }

        private List<Line> ReadCsvLines(UPath pathToCsv)
        {
            using (var csvFile = Convert.FsIn.OpenFile(pathToCsv, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                return _textIO.ReadAllLines(csvFile);
            }
        }

        // T4 AutoGen
        //public static List<Type> Formats = new List<Type>()
        //{
        //    typeof(AssassinsCreed2),
        //    typeof(Catherine),
        //    typeof(CatherineJP),
        //    typeof(CatherinePC),
        //    typeof(CrystalDynamics_ShadowoftheTombRaider),
        //    typeof(FinalFantasyXV),
        //    typeof(MSBT),
        //    typeof(NieRAutomata),
        //    typeof(ProjectZero2),
        //    typeof(RegexText),
        //    typeof(Souls),
        //    typeof(Souls_v2),
        //    typeof(TheEvilWithin2),
        //    typeof(VN_PJAdv)
        //};
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using Zio;
using System.Threading.Tasks;

namespace ExR.Format
{
    [Plugin("com.dc.CSVnCSV", "! CSV merge", @"
- Repack: combine multiple csv files to one (many to one: csv.n.csv).
- Extract: one -> many: csv(s)

Note: Only files created by me.")]
    class A_CSVnCSV : TextFormat
    {
        OutputProviders.CsvTextIOProvider _csvIO;
        public override async Task<bool> InitAsync(Dictionary<string, object> dict)
        {
            _csvIO = new OutputProviders.CsvTextIOProvider();

            if (RunMode == Mode.Extract)
            {
                var paths = FsIn.EnumeratePaths(UPath.Root, "*.csv.n.csv", SearchOption.AllDirectories);
                foreach (var path in paths)
                {
                    using (var fsCsv = FsIn.OpenFile(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        // allow sort row
                        var linex = _csvIO.ReadAllLine_NoMerge(fsCsv);
                        for (int i = 0; i < linex.Count; i++)
                        {
                            var id = linex[i].ID.Split('|', 2);
                            var count = int.Parse(id[0]);
                            var curCsvPath = id[1];
                            var lines = linex.GetRange(i+1, count);
                            i += count;

                            Console.WriteLine(count.ToString().PadLeft(6) + ' ' + curCsvPath);
                            await Task.Delay(1);

                            var outPath = new UPath(curCsvPath).ToAbsolute();
                            var outPathDir = outPath.GetDirectory();
                            if (outPathDir != UPath.Root)
                                FsOut.CreateDirectory(outPathDir);
                            using (var fs = FsOut.CreateFile(curCsvPath))
                            {
                                _csvIO.WriteAllLines(fs, lines);
                            }
                        }
                    }
                }
            }
            else
            {
                _csvIO = new OutputProviders.CsvTextIOProvider();

                var paths = FsIn.EnumeratePaths(UPath.Root, "*.csv", SearchOption.AllDirectories);
                var linex = new List<Line>(5000);
                string body;
                int count = 0;
                int i = 0;
                foreach (var path in paths)
                {
                    if (path.FullName.EndsWith(".csv.n.csv"))
                    {
                        Console.WriteLine("Skip: " + path);
                        continue;
                    }
                    i++;
                    
                    using (var fs = FsIn.OpenFile(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        using (var sr = new StreamReader(fs, detectEncodingFromByteOrderMarks: true, leaveOpen: true))
                        {
                            _ = sr.ReadLine(); // skip header
                            body = sr.ReadToEnd().TrimEnd('\n', '\r');

                        }
                        fs.Position = 0;
                        var lines = _csvIO.ReadAllLine_NoMerge(fs);
                        var pathRel = path.ToRelative().FullName;
                        Console.WriteLine(lines.Count.ToString().PadLeft(6) + ' ' + pathRel);
                        await Task.Delay(1);

                        linex.Add(new Line(lines.Count + "|" + pathRel, string.Empty,  string.Empty, "NULL"));
                        linex.AddRange(lines);

                        count += lines.Count;
                    }
                }
                Console.WriteLine("Lines: " + count.ToString() + " / " + (linex.Count+1).ToString());
                if (linex.Count > 0)
                {
                    using (var fs = FsOut.CreateFile($"/_PACK_.csv.n.csv"))
                    {
                        _csvIO.WriteAllLines(fs, linex);
                    }
                }
            }

            //Console.WriteLine($"CSVnCSV Done!");
            //return false;
            Console.WriteLine();
            throw new ExceptionWithoutStackTrace("CSVnCSV Done!\n");
        }

        public override List<Line> ExtractText(byte[] bytes)
        {
            return null;
        }

        public override byte[] RepackText(List<Line> lines)
        {
            return null;
        }

    }
}

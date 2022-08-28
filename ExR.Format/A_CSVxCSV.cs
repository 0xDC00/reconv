using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Zio;

namespace ExR.Format
{
    [Plugin("com.dc.CSVxCSV", "! CSV merge - Obsolete", @"
- Repack: combine multiple csv files to one (many to one: csv.x.csv).
- Extract: one -> many: csv(s)

Note: Only files created by me.")]
    class A_CSVxCSV : TextFormat
    {
        OutputProviders.CsvTextIOProvider _csvIO;
        public override async Task<bool> InitAsync(Dictionary<string, object> dict)
        {
            if (RunMode == Mode.Extract)
            {
                var paths = FsIn.EnumeratePaths(UPath.Root, "*.csv.x.csv", SearchOption.AllDirectories);
                _csvIO = new OutputProviders.CsvTextIOProvider();
                foreach (var path in paths)
                {
                    Extract(path, Save);
                }
            }
            else
            {
                _csvIO = new OutputProviders.CsvTextIOProvider();
                
                var paths = FsIn.EnumeratePaths(UPath.Root, "*.csv", SearchOption.AllDirectories);
                var sb = new StringBuilder(_10MB);
                sb.AppendLine("ID,English,Vietnamese,Note");
                string body;
                int count=0;
                int i = 0;
                int j = 1;
                foreach (var path in paths)
                {
                    if (path.FullName.EndsWith(".csv.x.csv"))
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

                        sb.Append(pathRel);
                        sb.AppendLine(",,,NULL");
                        sb.Append(lines.Count);
                        sb.AppendLine(",,,NULL");
                        sb.AppendLine(body);

                        count += lines.Count;
                        j += lines.Count;
                        j += 2;
                    }
                }
                Console.WriteLine("Lines: " + count.ToString() + " / " + j.ToString());
                if (count > 0)
                {
                    FsOut.WriteAllText($"/_PACK_.csv.x.csv", sb.ToString());
                }
            }

            //Console.WriteLine($"CSVxCSV Done!");
            //return false; //
            Console.WriteLine();
            throw new ExceptionWithoutStackTrace("CSVxCSV Done!\n");
        }

        private void Save(List<Line> lines, string curCsvPath)
        {
            Console.WriteLine(curCsvPath);
            var outPath = new UPath(curCsvPath).ToAbsolute();
            var outPathDir = outPath.GetDirectory();
            if (outPathDir != UPath.Root)
                FsOut.CreateDirectory(outPathDir);
            using (var fs = FsOut.CreateFile(curCsvPath))
            {
                _csvIO.WriteAllLines(fs, lines);
            }
        }

        private void Extract(UPath path, Action<List<Line>, string> actionSave)
        {
            using (var csvFile = FsIn.OpenFile(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
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
                        Console.WriteLine(count.ToString().PadLeft(6) + ' ' + curCsvPath);
                        return false;
                    }
                    else
                    {
                        count--;
                        line = new Line
                        {
                            ID = id,
                            English = c[1],
                            Vietnamese = c[2],
                            Note = c[3]
                        }; // No reflection emit, no replace column

                        var prio = line.TrimIdIndex();
                        priorityQueue.Enqueue(line, prio);

                        if (count == 0)
                        {
                            var lines = priorityQueue.ToList();
                            actionSave(lines, curCsvPath);

                            count = -1;
                            curCsvPath = string.Empty;
                            priorityQueue.Clear();
                        }

                        return false; // we use PQ
                    }
                });
            }
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

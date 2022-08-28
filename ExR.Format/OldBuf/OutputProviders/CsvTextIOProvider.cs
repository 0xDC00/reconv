using ExR.Format;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ExR.OutputProviders
{
    public class CsvTextIOProvider : ITextIOProvider
    {
        public string Extension => ".csv";
        private string[] headers = new string[] { "ID", "English", "Vietnamese", "Note" };

        internal static bool IsValid(Stream stream, bool leaveOpen = true)
        {
            using (var sr = new StreamReader(stream, leaveOpen: leaveOpen))
            {
                var header = sr.ReadLine();
                stream.Position = 0;
                if (header != null)
                {
                    // ID,English,Vietnamese
                    // "\"ID\",English,Vietnamese"
                    header = header.Replace("\"", string.Empty);
                    if (!(header.StartsWith("ID,English,Vietnamese", System.StringComparison.OrdinalIgnoreCase)
                        /*|| header.StartsWith("\"ID\",English,Vietnamese", System.StringComparison.OrdinalIgnoreCase)*/))
                    {
                        if (header.Contains("nglish,") || header.Contains("apanese") || header.Contains("tnamese,"))
                        {
                            System.Console.WriteLine("Header: " + header.Substring(0, header.Length > 30 ? 30 : header.Length));
                            System.Console.WriteLine("Please: ID,English,Vietnamese,Note");
                        }
                        else
                        {
                            return false;
                        }
                    }
                }
            }
            return true;
        }

        public List<Line> ReadAllLines(Stream stream)
        {
            stream.Position = 0;
            if (!IsValid(stream))
            {
                return null;
            }

            var priorityQueue = new Priority_Queue.SimplePriorityQueue<Line>(); // SimplePriorityQueue vs FastPriorityQueue
            using (var sr = new StreamReader(stream))
            {
                _ = fastCSV.ReadStream<Line>(sr, true, ',', (line, c) =>
                {
                    var id = c[0];
                    if (id == string.Empty)
                        return false; // skip empty row
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

                    return false; // we use PQ
                });
            }
            return priorityQueue.ToList();
        }

        public void WriteAllLines(Stream outStream, List<Line> lines)
        {
            int i = 1;
            fastCSV.WriteStream(outStream,
                headers, ',', lines, (o, c) =>
            {
                c.Add(i++.ToString() + "_" + o.ID);
                c.Add(o.English);
                c.Add(o.Vietnamese);
                c.Add(o.Note);
            });
        }

        public List<Line> ReadAllLine_NoMerge(Stream stream)
        {
            var priorityQueue = new Priority_Queue.SimplePriorityQueue<Line>(); // SimplePriorityQueue vs FastPriorityQueue
            using (var sr = new StreamReader(stream))
            {
                _ = fastCSV.ReadStream<Line>(sr, true, ',', (line, c) =>
                {
                    var id = c[0];
                    if (id == string.Empty)
                        return false; // skip empty row

                    line = new Line
                    {
                        ID = id,
                        English = c[1],
                        Vietnamese = c[2],
                        Note = c[3]
                    }; // No reflection emit

                    var prio = line.TrimIdIndex();
                    priorityQueue.Enqueue(line, prio);

                    return false; // we use PQ
                });
            }
            return priorityQueue.ToList();
        }

        public List<Line> ReadAllLine_NoMerge_NoPQ(Stream stream)
        {
            var lines = new List<Line>(); // SimplePriorityQueue vs FastPriorityQueue
            using (var sr = new StreamReader(stream))
            {
                _ = fastCSV.ReadStream<Line>(sr, true, ',', (line, c) =>
                {
                    var id = c[0];
                    if (id == string.Empty)
                        return false; // skip empty row

                    line = new Line
                    {
                        ID = id,
                        English = c[1],
                        Vietnamese = c[2],
                        Note = c[3]
                    }; // No reflection emit

                    lines.Add(line);

                    return false; // we use PQ
                });
            }
            return lines;
        }

        public List<Line> ReadAllLine_NoMerge_NoPQ_All(Stream stream)
        {
            var lines = new List<Line>();
            using (var sr = new StreamReader(stream))
            {
                _ = fastCSV.ReadStream<Line>(sr, true, ',', (line, c) =>
                {
                    line = new Line();
                    for (int i = 0; i < c.Count; i++)
                    {
                        line[i] = c[i];
                    }

                    lines.Add(line);

                    return false; // we use PQ
                });
            }
            return lines;
        }
    }
}

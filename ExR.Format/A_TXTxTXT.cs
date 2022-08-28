using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Zio;

namespace ExR.Format
{
    [Plugin("com.dc.TXTnTXT", "# Plain text merge (.txt)", @"
- Repack: combine multiple txt files to one (many to one: txt.n.txt).
- Extract: one -> many: txt(s)

Note: _init_.yaml (optional)
---
encoding: utf_8 # default (BOM auto detect)
pattern: *.txt # default
...")]
    class A_TXTxTXT : TextFormat
    {
        private readonly string PREFIX = "---|FILE|";
        public override async Task<bool> InitAsync(Dictionary<string, object> dict)
        {
            if (dict.TryGetValue("encoding", out var encodingName))
            {
                _Encoding = Encoding.GetEncoding((string)encodingName);
            }
            else
            {
                _Encoding = Encoding.UTF8;
            }
            if (RunMode == Mode.Extract)
            {
                var pattern = "*.txt.n.txt";
                var paths = FsIn.EnumeratePaths(UPath.Root, pattern, SearchOption.AllDirectories);
                foreach (var path in paths)
                {
                    using (var fs = FsIn.OpenFile(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (var streamReader = new StreamReader(fs, Encoding.UTF8, true))
                    {
                        while (!streamReader.EndOfStream)
                        {
                            var contents = ReadUntil(streamReader, PREFIX);
                            var info = streamReader.ReadLine().Split('|');
                            if (info.Length == 2)
                            {
                                var enc = str2enc(info[0]); // old encoding

                                var curPath = (UPath)info[1];
                                var outPathDir = curPath.GetDirectory();
                                if (outPathDir != UPath.Root)
                                    FsOut.CreateDirectory(outPathDir);
                                Console.WriteLine(curPath.FullName);
                                await Task.Delay(1);
                                FsOut.WriteAllText(curPath, contents, enc);
                            }
                        }
                    }
                }
            }
            else
            {
                var pattern = "*.txt";
                //IEnumerable<UPath> paths;
                if (dict.TryGetValue("pattern", out var _pattern))
                {
                    pattern = (string)_pattern;
                    //paths = FsIn.EnumeratePaths("/", pattern, SearchOption.AllDirectories);
                }
                else
                {
                    //paths = FsIn.EnumeratePaths("/", pattern, SearchOption.AllDirectories);
                }

                var sb = new StringBuilder(_10MB);
                var hasBoom = false;
                var paths = FsIn.EnumeratePaths(UPath.Root, pattern, SearchOption.AllDirectories);
                foreach (var path in paths)
                {
                    Console.WriteLine(path);
                    await Task.Delay(1);
                    using (var fs = FsIn.OpenFile(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        Encoding curEncoding = _Encoding;
                        string txt;
                        using (var sr = new StreamReader(fs, curEncoding, detectEncodingFromByteOrderMarks: true))
                        {
                            txt = sr.ReadToEnd();
                            curEncoding = sr.CurrentEncoding;
                        }
                        // TODO: boom, big endian
                        var hdr = $"{PREFIX}{enc2str(curEncoding, hasBoom)}|{path.FullName}";
                        sb.Append(txt);
                        sb.AppendLine(hdr);
                    }
                }
                var ret = sb.ToString();
                if (ret.Length > 0)
                    FsOut.WriteAllText("/_PACK_.txt.n.txt", ret, Encoding.UTF8);
            }

            Console.WriteLine();
            throw new Exception("TXTnTXT Done!\n");
        }

        public override List<Line> ExtractText(byte[] bytes)
        {
            return null;
        }

        public override byte[] RepackText(List<Line> lines)
        {
            return null;
        }

        private static string enc2str(Encoding e, bool hasBOM)
        {
            string text = e.CodePage.ToString();
            if (hasBOM)
            {
                text += " with BOM";
            }
            return text;
        }

        private static Encoding str2enc(string str)
        {
            string[] array = str.Split(new string[]
            {
                " with "
            }, StringSplitOptions.None);
            if (array.Length == 1)
            {
                if (array[0] == Encoding.Unicode.CodePage.ToString())
                {
                    return new UnicodeEncoding(false, false);
                }
                if (array[0] == Encoding.UTF8.CodePage.ToString())
                {
                    return new UTF8Encoding(false, false);
                }
            }
            return Encoding.GetEncoding(int.Parse(array[0]));
        }

        static string ReadUntil(StreamReader sr, string delim)
        {
            StringBuilder sb = new StringBuilder();
            bool found = false;

            while (!found && !sr.EndOfStream)
            {
                for (int i = 0; i < delim.Length; i++)
                {
                    char c = (char)sr.Read();
                    sb.Append(c);

                    if (c != delim[i])
                        break;

                    if (i == delim.Length - 1)
                    {
                        sb.Remove(sb.Length - delim.Length, delim.Length);
                        found = true;
                    }
                }
            }

            return sb.ToString();
        }

        //static string ReadLine(StreamReader sr, string lineDelimiter)
        //{
        //    StringBuilder line = new StringBuilder();
        //    var matchIndex = 0;

        //    while (sr.Peek() > 0)
        //    {
        //        var nextChar = (char)sr.Read();
        //        line.Append(nextChar);

        //        if (nextChar == lineDelimiter[matchIndex])
        //        {
        //            if (matchIndex == lineDelimiter.Length - 1)
        //            {
        //                return line.ToString().Substring(0, line.Length - lineDelimiter.Length);
        //            }
        //            matchIndex++;
        //        }
        //        else
        //        {
        //            matchIndex = 0;
        //            //did we mistake one of the characters as the delimiter? If so let's restart our search with this character...
        //            if (nextChar == lineDelimiter[matchIndex])
        //            {
        //                if (matchIndex == lineDelimiter.Length - 1)
        //                {
        //                    return line.ToString().Substring(0, line.Length - lineDelimiter.Length);
        //                }
        //                matchIndex++;
        //            }
        //        }
        //    }

        //    return line.Length == 0
        //        ? null
        //        : line.ToString();
        //}
    }
}

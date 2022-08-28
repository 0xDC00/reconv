/*
https://github.com/akintos/UnrealLocres
*/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using BufLib.Common.IO;

namespace ExR.Format
{
    [Plugin("com.dc.unreal4", "# Unreal Engine 4 (.locres)", "Extract/Repack. Note: https://github.com/akintos/UnrealLocres")]
    class UnrealEngine4 : TextFormat
    {
        // static const FGuid LocResMagic = FGuid(0x7574140E, 0xFC034A67, 0x9D90154A, 0x1B7F37C3);
        readonly byte[] LocResMagic = new byte[] { 0x0E, 0x14, 0x74, 0x75, 0x67, 0x4A, 0x03, 0xFC, 0x4A, 0x15, 0x90, 0x9D, 0xC3, 0x37, 0x7F, 0x1B };
        readonly string LegacyMagic = "__LocResLegacy__";

        public override bool Init(Dictionary<string, object> dict)
        {
            Extensions = new string[] { ".locres" };
            return true;
        }
        
        public override List<Line> ExtractText(byte[] bytes)
        {
            return ExtractLocRes(bytes);
        }

        public override byte[] RepackText(List<Line> lines)
        {
            return RepackLocRes(lines);
        }

        List<Line> ExtractLocRes(byte[] bytes)
        {
            if (bytes.Length < 0x20)
                return null;

            // https://docs.microsoft.com/en-us/archive/msdn-magazine/2018/january/csharp-all-about-span-exploring-a-new-net-mainstay
            if (bytes.AsSpan(0, 0x10).SequenceEqual(LocResMagic.AsSpan()))
            {
                var ver = bytes[0x10];
                Console.WriteLine("LocRes v" + ver);
                if (ver == 1)
                {
                    return ExtractLocRes(bytes, ReadLineV1);
                }
                else // v2, v3, try...
                {
                    return ExtractLocRes(bytes, ReadLineV2);
                }
            }
            else
            {
                var f1 = bytes[3] == 0; // numLine
                var f2 = BitConverter.ToInt32(bytes, 4) == 0;
                var f3 = bytes[0x14] != 0; // LocResZero
                var f4 = BitConverter.ToInt32(bytes, 0x15) != 0;
                if (f1 && f2 && f3 && f4)
                {
                    return ExtractLocResLegacy(bytes);
                }
            }

            return null;
        }

        byte[] RepackLocRes(List<Line> lines)
        {
            // legacy detect
            if (lines[0].English == LegacyMagic)
            {
                return RepackLocResLegacy(lines); // UTF16 only
            }

            // default
            using (var ms = new MemoryStream(_10MB))
            using (var bw = new BinaryWriter(ms))
            {
                var header = _PopEnd(lines);
                Action<BinaryWriter, Line> writeLine;
                switch (header[0x10])
                {
                    case 1: writeLine = WriteLineV1; break;
                    case 2: writeLine = WriteLineV2; break; // UTF8 (ASCII?) & UTF16
                    default:
                        writeLine = WriteLineV2; break; // try...
                }

                bw.Write(header);
                header = null;

                bw.Write(lines.Count);
                foreach (var line in lines)
                {
                    writeLine(bw, line);
                }

                return ms.ToArray();
            }
        }

        List<Line> ExtractLocRes(byte[] bytes, Func<BinaryReader, Line> readLine)
        {
            var lines = new List<Line>();

            var offset = BitConverter.ToInt32(bytes, 0x11);
            using (var ms = new MemoryStream(bytes))
            using (var br = new BinaryReader(ms))
            {
                var header = br.ReadBytes(offset);

                var count = br.ReadInt32();
                for (int i = 0; i < count; i++)
                {
                    lines.Add(readLine(br));
                }

                _PushEnd(lines, header);
            }
            return lines;
        }

        byte[] RepackLocResLegacy(List<Line> lines)
        {
            using (var ms = new MemoryStream(_10MB))
            using (var bw = new BinaryWriter(ms))
            {
                var count = int.Parse(lines[0].ID);
                bw.Write(count);

                for (int i = 1; i < lines.Count; i++)
                {
                    // chunk
                    var line = lines[i];
                    var numName = Line.FromId(line.ID);
                    var num = int.Parse(numName[0]);
                    WriteStringUnreal(bw, numName[1]); // name
                    bw.Write(num);


                    //for (int j = 0; j < num; j++)
                    while (num-- > 0)
                    {
                        line = lines[++i];
                        var hashKey = Line.FromId(line.ID);
                        var hash = int.Parse(hashKey[0], System.Globalization.NumberStyles.HexNumber);

                        WriteStringUnreal(bw, hashKey[1]);
                        bw.Write(hash);
                        WriteStringUnreal(bw, line.English);
                    }
                }

                return ms.ToArray();
            }
        }

        List<Line> ExtractLocResLegacy(byte[] bytes)
        {
            var lines = new List<Line>();

            using (var ms = new MemoryStream(bytes))
            using (var br = new BinaryReader(ms))
            {
                var count = br.ReadInt32();
                lines.Add(new Line(count, LegacyMagic));

                // chunk
                for (int i = 0; i < count; i++)
                {
                    var name = ReadStringUnreal(br);
                    var num = br.ReadInt32();
                    lines.Add(new Line(Line.ToId(num, name), string.Empty));

                    // part
                    for (int j = 0; j < num; j++)
                    {
                        var key = ReadStringUnreal(br);
                        var hash = br.ReadUInt32();
                        var str = ReadStringUnreal(br);
                        lines.Add(new Line(Line.ToId(hash.ToString("X8"), key), str));
                    }
                }
            }
            
            return lines;
        }

        string ReadStringUnreal(BinaryReader br)
        {
            var len = br.ReadInt32();
            if (len == 0)
                return string.Empty;
            if (len < 0)
            {
                len = (-len * 2) - 2;
                var raw = br.ReadBytes(len);
                br.BaseStream.Position += 2;
                return Encoding.Unicode.GetString(raw);
            }
            else
            {
                len--;
                var raw = br.ReadBytes(len);
                br.BaseStream.Position += 1;
                return Encoding.UTF8.GetString(raw);
            }
        }

        void WriteStringUnreal(BinaryWriter bw, string s)
        {
            if (s == string.Empty)
            {
                bw.Write(0);
                return;
            }

            s += '\0';
            var raw = Encoding.Unicode.GetBytes(s);
            bw.Write(-s.Length);
            bw.Write(raw);
        }

        Line ReadLineV1(BinaryReader br)
        {
            var s = ReadStringUnreal(br);
            return new Line(eng: s);
        }

        Line ReadLineV2(BinaryReader br)
        {
            var s = ReadStringUnreal(br);
            var n = br.ReadInt32();
            return new Line(id: n, eng: s);
        }

        void WriteLineV1(BinaryWriter bw, Line line)
        {
            WriteStringUnreal(bw, line.English);
        }

        void WriteLineV2(BinaryWriter bw, Line line)
        {
            WriteStringUnreal(bw, line.English);
            bw.Write(int.Parse(line.ID));
        }
    }
}

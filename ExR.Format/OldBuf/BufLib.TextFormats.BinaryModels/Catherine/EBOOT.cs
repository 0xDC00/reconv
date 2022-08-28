// Target: boot.elf
// Mode: Re-Import
// Encoding: BigEndianUnicode
using ExR.Format;
using BufLib.Common.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace BufLib.TextFormats.BinaryModels.Catherine
{
    public static class EBOOT
    {
        // type, offset, size|count
        static List<Tuple<string, long, int>> blocks;
        public static Endian Endian = Endian.BigEndian;
        public static Encoding Encoding = Encoding.BigEndianUnicode;
        public static int Align = 8;

        public static void Init(Platform PF)
        {
            switch (PF)
            {
                case Platform.PS3_EN:
                    blocks = new List<Tuple<string, long, int>>()
                    {
                        new Tuple<string, long, int>(".f38", 0xF0BFC0, 4), // 4 lines 0x38
                        new Tuple<string, long, int>(".f34", 0xF0C0A0, 70), // 70 lines 0x34
                        new Tuple<string, long, int>(".al8", 0xCA1760, 192),
                        new Tuple<string, long, int>(".unk", 0xF081E0, 0xF0BFC0), // 770, size=0x268550, scan text trong vùng -> quy luật
                        new Tuple<string, long, int>(".bmd", 0xEDC7E0, 0), // small bmd
                        new Tuple<string, long, int>(".bmd", 0xECEB70, 0), // jp không có
                    };
                    break;
                case Platform.PS3_JP:
                    blocks = new List<Tuple<string, long, int>>()
                    {
                        new Tuple<string, long, int>(".f20", 0xF0DD20, 74), // en34 & 38
                        new Tuple<string, long, int>(".al8",0xC9F630, 193),
                        new Tuple<string, long, int>(".unk", 0xF0A110, 0xF0DD10),
                        new Tuple<string, long, int>(".al8", 0xF0DD10, 2),
                        new Tuple<string, long, int>(".bmd", 0xEDA880, 0), // small bmd
                        //new Tuple<string, long, int>(".bmd", 0xECEB70, 0), // jp không có
                    };
                    break;
                case Platform.Steam_Classis:
                    blocks = new List<Tuple<string, long, int>>()
                    {
                        new Tuple<string, long, int>(".f38", 0x8DB928, 4), // 4 lines 0x38
                        new Tuple<string, long, int>(".f34", 0x8DAA30, 70), // 70 lines 0x34
                        new Tuple<string, long, int>(".al8", 0x77721C, 192),
                        new Tuple<string, long, int>(".unk", 0x8DB868, 0x8DF768), // 0x8DBA08
                        // nhung ngon ngu khac
                        //new Tuple<string, long, int>(".bmd", 0x8AB4A8, 0),
                        //new Tuple<string, long, int>(".bmd", 0x8ABEC0, 0),
                        //new Tuple<string, long, int>(".bmd", 0x8AC8B0, 0),
                        //new Tuple<string, long, int>(".bmd", 0x8AD2B8, 0),
                        //
                        //new Tuple<string, long, int>(".bmd", 0x8B0200, 0),
                        //new Tuple<string, long, int>(".bmd", 0x8B02B0, 0),
                        //new Tuple<string, long, int>(".bmd", 0x8B0380, 0),
                        //new Tuple<string, long, int>(".bmd", 0x8B0438, 0),
                        new Tuple<string, long, int>(".bmd", 0x8B04F0, 0),
                        new Tuple<string, long, int>(".bmd", 0x8ADC10, 0),
                    };
                    break;
            }
        }

#if !BRIDGE_DOTNET
        public static List<Line> ExtractText(EndianBinaryReader br)
        {
            br.Endianness = Endian;

            var result = new List<Line>();

            foreach (var block in blocks)
            {
                List<Line> lines = new List<Line>();
                br.BaseStream.Position = block.Item2;
                switch (block.Item1)
                {
                    case ".bmd":
                        br.Endianness = Endian.BigEndian;
                        br.BaseStream.Position += 0xC;
                        var bmdSize = br.ReadInt32();
                        br.BaseStream.Position -= 0x10;
                        var bmd = br.ReadBytes(bmdSize);
                        lines = BMD.ExtractText(new EndianBinaryReader(new MemoryStream(bmd)));
                        br.Align(4);
                        br.Endianness = Endian;
                        break;
                    case ".al8":
                        lines = ExtractAlign8(br, block.Item3);
                        break;
                    case ".unk":
                        lines = ExtractUnk(br, block.Item3);
                        break;
                    case ".f20":
                        lines = ExtractFixedLength(br, 0x20, block.Item3);
                        break;
                    case ".f38":
                        lines = ExtractFixedLength(br, 0x38, block.Item3);
                        break;
                    case ".f34":
                        lines = ExtractFixedLength(br, 0x34, block.Item3);
                        break;
                }

                if (lines.Count > 0)
                {
                    var blockSize = br.BaseStream.Position - block.Item2;

                    // cần lưu offset để biết vị trí reimport
                    // Type|Offset|maxSize|lineCount
                    result.Add(new Line($"{block.Item1}|{block.Item2:X}|{blockSize:X}|{lines.Count}", string.Empty));
                    result.AddRange(lines);
                }
            }

            return result;
        }

        static List<Line> ExtractAlign8(EndianBinaryReader br, int count)
        {
            var result = new List<Line>();

            for (int i = 0; i < count; i++)
            {
                var beginLine = br.BaseStream.Position;

                var line = br.ReadTerminatedWideString(Encoding);
                br.Align(Align);

                var lineSize = br.BaseStream.Position - beginLine;
                var maxChar = lineSize / 2 - 1;

                result.Add(new Line($"{lineSize:X}|{maxChar}", line));
            }

            return result;
        }

        static List<Line> ExtractUnk(EndianBinaryReader br, int rangeEnd)
        {
            // mỗi chuổi align 0x10 bytes hoặc có một kiểu align khác nhau.
            var result = new List<Line>();

            /* dump text trong khoảng */
            while (br.BaseStream.Position < rangeEnd)
            {
                var begin = br.BaseStream.Position;
                while (true)
                {
                    // 2 bytes cuối 00 -> câu hiện tại
                    var block = br.ReadBytes(16);
                    if (block[14] == 0 && block[15] == 0)
                    {
                        break;
                    }
                }
                var end = br.BaseStream.Position;
                var safeSize = end - begin;
                var safeMaxChar = safeSize / 2 - 1;

                // read block & decode string
                br.BaseStream.Position = begin;
                var line = br.ReadStringFixedLength((int)safeSize, Encoding).TrimEnd('\0');
                line = line.Replace('￣', ' ');

                // bỏ qua khoảng trống phía sau
                while (true)
                {
                    var block = br.ReadBytes(2);
                    if (block[0] != 0 || block[1] != 0) // 00 XX - BigUnicode
                    {
                        br.BaseStream.Position -= 2;
                        break;
                    }
                }
                end = br.BaseStream.Position;
                var unsafeSize = end - begin; // size string + size padding + size khoảng trống bên dưới (00...)
                var unsafeMaxChar = unsafeSize / 2 - 1;

                // begin = offset reimport
                result.Add(new Line($"{begin:X}|{safeSize:X}|{safeMaxChar}|{unsafeSize:X}|{unsafeMaxChar}", line));
            }

            return result;
        }

        static List<Line> ExtractFixedLength(EndianBinaryReader br, int fixedLength, int count)
        {
            var result = new List<Line>();

            for (int i = 0; i < count; i++)
            {
                var line = br.ReadStringFixedLength(fixedLength, Encoding).TrimEnd('\0');
                line = line.Replace('￣', ' ');

                result.Add(new Line(fixedLength / 2 - 1, line));
            }

            return result;
        }
#endif

        public static byte[] RepackText(List<Line> lines, byte[] eboot)
        {
            // lấy thông tin từ dòng đầu tiên
            var info = lines[0].ID.Split('|');
            var ext = info[0];
            var offset = info[1].HexStringToLong();
            var maxSize = info[2].HexStringToInt();
            var numLine = int.Parse(info[3]);

            //var currentLines = lines.Skip(1).Take(numLine).ToList();
            //lines = lines.Skip(numLine + 1).ToList(); // remainLine
            var currentLines = lines.GetRange(1, numLine);
            lines = lines.GetRange(1 + numLine, lines.Count - numLine - 1);
            Console.WriteLine("> " + ext);

            byte[] result = null;
            using (var ms = new MemoryStream(eboot))
            using (var bw = new EndianBinaryWriter(ms, Endian))
            {
                ms.Position = offset;

                switch (ext)
                {
                    case ".bmd":
                        result = BMD.RepackText(currentLines);
                        if (result.Length <= maxSize)
                        {
                            bw.Write(result);
                        }
                        else
                        {
                            Console.WriteLine("Skip BMD!");
                        }
                        break;

                    case ".al8":
                        ReImportAl8_Strict(bw, currentLines);
                        // ReImportAl8(bw, currentLines, maxSize);
                        break;

                    case ".f38":
                        ReImportFixedLength(bw, currentLines, 0x38);
                        break;

                    case ".f34":
                        ReImportFixedLength(bw, currentLines, 0x34);
                        break;

                    case ".unk":
                        ReImportUnk(bw, currentLines);
                        break;
                }
            }

            if (lines.Count > 0)
            {
                return RepackText(lines, eboot);
            }
            else
            {
                return eboot;
            }
        }

        // UnSafe: đổi offset & độ dài text
        // size block mới k được vượt quá size ban đầu.
        //  -> Tested: mỗi câu đều có ref từ asm -> cách này không dùng được.
        static void ReImportAl8(EndianBinaryWriter bw, List<Line> lines, int maxSize)
        {
            using (var ms = new MemoryStream(maxSize))
            using (var bw2 = new EndianBinaryWriter(ms, Endian))
            {
                foreach (var line in lines)
                {
                    bw2.WriteTerminatedWideString(line.English, Encoding);
                    bw2.Align(Align);
                }

                if (ms.Length > maxSize)
                {
                    // block mới quá to, bỏ qua.
                    Console.WriteLine("[W] Skip: BLOCK_AL8");
                }
                else
                {
                    var block_al8 = ms.ToArray();
                    bw.Write(block_al8);
                }
            }

        }

        // Safe: không đổi offset, size giới hạn
        static void ReImportAl8_Strict(EndianBinaryWriter bw, List<Line> lines)
        {
            foreach (var line in lines)
            {
                var target = line.English;
                var infos = line.ID.Split('|'); // 
                var maxChar = int.Parse(infos[1]);

                if (target.Length > maxChar)
                {
                    Console.WriteLine("[W] Skip: " + target);
                    bw.BaseStream.Position += (maxChar * 2 + 2);
                    bw.Align(Align);
                }
                else
                {
                    line.English = line.English.PadRight(maxChar, '\0'); // offset bị đổi nếu size mới nhỏ hơn quá nhiều -> cân pad resize lại như gôc.
                    bw.WriteTerminatedWideString(line.English, Encoding);
                    bw.Align(Align);
                }
            }
        }

        static void ReImportFixedLength(EndianBinaryWriter bw, List<Line> lines, int fixedLength)
        {
            var safeSize = fixedLength / 2 - 1;
            foreach (var line in lines)
            {
                var target = line.English.Replace(' ', '￣');
                if (target.Length > safeSize)
                {
                    Console.WriteLine("[W] Skip: " + target);
                    bw.BaseStream.Position += fixedLength;
                }
                else
                {
                    bw.WriteStringFixedLength(target, fixedLength, Encoding);
                }
            }
        }

        // TODO: Nếu lỗi sẽ chuyển sang dùng safeMaxChar
        static void ReImportUnk(EndianBinaryWriter bw, List<Line> lines)
        {
            foreach (var line in lines)
            {
                var target = line.English.Replace(' ', '￣');
                var infos = line.ID.Split('|');
                var offset = infos[0].HexStringToLong();
                //var safeSize = infos[1].HexStringToInt();
                //var safeMaxChar = int.Parse(infos[2]);
                var unsafeSize = infos[3].HexStringToInt();
                var unsafeMaxChar = int.Parse(infos[4]);


                if (target.Length > unsafeMaxChar) // TODO: unsafeMaxChar -> safeMaxChar
                {
                    Console.WriteLine("[W] Skip: " + target);
                    bw.BaseStream.Position += unsafeSize;
                }
                else
                {
                    bw.BaseStream.Position = offset;
                    bw.WriteStringFixedLength(target, unsafeSize, Encoding);
                }

            }
        }
    }
}

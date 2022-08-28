using BufLib.Common.Compression;
using BufLib.Common.IO;
using BufLib.TextFormats.DataModels;
using ExR.Format;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace BufLib.TextFormats.BinaryModels.Catherine
{
    public static class BIN
    {
        static int szQuestion;
        static int szChoose;
        static Endian _endian;
        static Encoding _encoding;

        public static void Init(Platform PF)
        {
            switch (PF)
            {
                case Platform.PS3_EN:
                    _endian = Endian.BigEndian;
                    _encoding = Encoding.BigEndianUnicode;
                    szQuestion = 0x8C;
                    szChoose = 0x40;
                    break;
                case Platform.PS3_JP:
                    _endian = Endian.BigEndian;
                    _encoding = Encoding.BigEndianUnicode;
                    szQuestion = 0x44;
                    szChoose = 0x20;
                    break;
                case Platform.Steam_Classis:
                    _endian = Endian.BigEndian;
                    _encoding = Encoding.BigEndianUnicode;
                    szQuestion = 0xD8;
                    szChoose = 0x60;
                    break;
            }
        }
#if !BRIDGE_DOTNET
        public static List<Line> ExtractText(EndianBinaryReader br)
        {
            br.Endianness = _endian;

            br.BaseStream.Position = 0x3684;
            int numLine = 110;
            var result = new List<Line>(numLine);

            for (int i = 0; i < numLine; i++)
            {
                var type = br.ReadInt16();
                var id = br.ReadInt16();
                var question = br.ReadStringFixedLength(szQuestion, _encoding).TrimEnd('\0');
                var choose1 = br.ReadStringFixedLength(szChoose, _encoding).TrimEnd('\0');
                var u1 = br.ReadInt32();
                var choose2 = br.ReadStringFixedLength(szChoose, _encoding).TrimEnd('\0');
                var u2 = br.ReadInt32();

                // ￣ -> space
                result.Add(new Line("*" + id, question.Replace('￣', ' ')));
                result.Add(new Line("-" + u1, choose1.Replace('￣', ' ')));
                result.Add(new Line("-" + u2, choose2.Replace('￣', ' ')));
            }

            br.BaseStream.Position = 0;
            var payload = br.ReadBytes((int)br.BaseStream.Length);
            var rawTable = Convert.ToBase64String(CompressionHelper.ZlibCompress(payload, CompressionLevel.Optimal));

            result.Insert(0, new Line(rawTable, string.Empty));

            return result;
        }
#endif

        public static byte[] RepackText(List<Line> lines)
        {
            var oldMailData = CompressionHelper.ZlibUncompress(Convert.FromBase64String(lines[0].ID));

            using (var ms = new MemoryStream(oldMailData))
            using (var bw = new EndianBinaryWriter(ms, _endian))
            {
                bw.BaseStream.Position = 0x3684;
                for (int i = 1; i < lines.Count; i++) // skip first line
                {
                    bw.BaseStream.Position += 4;

                    // space -> ￣
                    var question = lines[i].English.Replace(' ', '￣');
                    var choose1 = lines[++i].English.Replace(' ', '￣');
                    var choose2 = lines[++i].English.Replace(' ', '￣');

                    bw.WriteStringFixedLength(question, szQuestion, _encoding);
                    bw.WriteStringFixedLength(choose1, szChoose, _encoding);
                    bw.BaseStream.Position += 4;
                    bw.WriteStringFixedLength(choose2, szChoose, _encoding);
                    bw.BaseStream.Position += 4;
                }

                return ms.ToArray();
            }
        }
    }
}

// file text fixed len, re-import file mới size không tăng
// MODE: Override
// Encoding Unicode (UTF16)

using BufLib.Common.IO;
using BufLib.TextFormats.DataModels;
using ExR.Format;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace BufLib.TextFormats.BinaryModels.NieRAutomata
{
    internal static class SMD
    {
        public static Encoding Encoding = null;

#if !BRIDGE_DOTNET
        public static List<Line> ExtractText(byte[] data)
        {
            using (var br = new BinaryReader(new MemoryStream(data)))
                return ExtractText(br);
        }

        public static List<Line> ExtractText(BinaryReader br)
        {
            var result = new List<Line>();

            var numLine = br.ReadInt32();
            for (int i = 0; i < numLine; i++)
            {
                var _id = br.ReadBytes(0x80);
                var unkA = br.ReadInt64();
                var _value = br.ReadBytes(0x800);
                var id = Encoding.Unicode.GetString(_id).TrimEnd('\0');
                var value = Encoding.Unicode.GetString(_value).TrimEnd('\0');

                //if (unkA % 0xa != 0)
                //    throw new Exception("SMD->unkA");

                result.Add(new Line(BIN.IREP_RECORD.mrb_sym2name(id), value));
            }

            return result;
        }
#endif

        public static byte[] RepackText(List<Line> lines, byte[] oldSMD)
        {
            using (var ms = new MemoryStream(oldSMD))
            using (var bw = new BinaryWriter(ms))
            {
                //bw.Write(lines.Count);
                //Int64 unkA = 0;
                bw.BaseStream.Position += 4;
                foreach (var line in lines)
                {
                    // line.English = line.English.Replace("\r\n", "\n"); // endline = \r\n
                    bw.BaseStream.Position += 0x88; // SkipID & unkA (80+8)
                    //var _id = Encoding.Unicode.GetBytes(line.Id).Align(0x80);
                    var _value = Encoding/*.Unicode*/.GetBytes(line.English).Align(0x800);
                    //bw.Write(_id);
                    //bw.Write(unkA);
                    //unkA += 0xa;
                    bw.Write(_value);

                }

                return ms.ToArray();
            }
        }
    }
}

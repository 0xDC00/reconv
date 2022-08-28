// file text fixed len, re-import file mới size không tăng
// MODE: Override
// Encoding Unicode (UTF16)

using BufLib.TextFormats.DataModels;
using ExR.Format;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace BufLib.TextFormats.BinaryModels.NieRAutomata
{
    internal static class TMD
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
                var idLen = br.ReadInt32();
                var _id = br.ReadBytes(idLen * 2); // utf16

                var valueLen = br.ReadInt32();
                var _value = br.ReadBytes(valueLen * 2);

                var id = Encoding.Unicode.GetString(_id).Trim('\0');
                var value = Encoding.Unicode.GetString(_value).Trim('\0');

                result.Add(new Line(id, value));
            }

            return result;
        }
#endif
        public static byte[] RepackText(List<Line> lines)
        {
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                bw.Write(lines.Count);
                foreach (var line in lines)
                {
                    line.English = line.English.Replace("\r\n", "\n"); // endline = \n
                    var _id = Encoding/*.Unicode*/.GetBytes(line.ID);
                    var _value = Encoding/*.Unicode*/.GetBytes(line.English);
                    bw.Write(line.ID.Length + 1);
                    bw.Write(_id);
                    bw.Write((short)0);
                    bw.Write(line.English.Length + 1);
                    bw.Write(_value);
                    bw.Write((short)0);
                }

                return ms.ToArray();
            }
        }
    }
}

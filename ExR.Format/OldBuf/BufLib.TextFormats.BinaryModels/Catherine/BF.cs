// Mode: Compile

using BufLib.Common.Compression.Nintendo;
using BufLib.Common.IO;
using ExR.Format;
using System.Collections.Generic;
using System.IO;

namespace BufLib.TextFormats.BinaryModels.Catherine
{
    public static partial class BF
    {
        private static Endian _endian;

        public static void Init(Platform PF)
        {
            switch (PF)
            {
                case Platform.PS3_EN:
                case Platform.PS3_JP:
                case Platform.Steam_Classis:
                    _endian = Endian.BigEndian;
                    break;
                default:
                    _endian = Endian.LittleEndian;
                    break;
            }
        }

        public static byte[] RepackText(List<Line> lines)
        {
            var payload = lines[0].ID.HexStringToByteArray();
            payload = Nintendo.Decompress(payload);

            lines.RemoveAt(0);

            /* replace space thành ￣ như ban đầu */
            // không the edit line nhưng có thể edit property của line
            //foreach (var line in lines)
            //{
            //    line.English = line.English.Replace(' ', '￣');
            //}

            var bmd = BMD.RepackText(lines);
            using (var ms = new MemoryStream())
            using (var bw = new EndianBinaryWriter(ms, _endian))
            {
                bw.Write(payload);
                bw.Write(bmd);

                // 5th section
                var lastPointer = (int)bw.BaseStream.Position;
                bw.Write(new byte[0xF0]);

                bw.BaseStream.Position = 4;
                bw.Write(lastPointer);

                // bmd section size
                bw.BaseStream.Position = 0x58; // index=3
                bw.Write(bmd.Length);

                // 5th section pointer
                bw.BaseStream.Position = 0x6C; // index=4
                bw.Write(lastPointer);


                return ms.ToArray();
            }
        }
    }
}

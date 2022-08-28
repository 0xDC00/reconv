using BufLib.Common.IO;
using BufLib.TextFormats.DataModels;
using ExR.Format;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace BufLib.TextFormats.BinaryModels.Catherine
{
    internal static partial class PAC
    {
        // pac PS3 = LE
        public static List<Line> ExtractText(EndianBinaryReader br, string baseName = "")
        {
            var result = new List<Line>();
            int index = 0;
            while (br.BaseStream.Position < br.BaseStream.Length)
            {
                // index = br.BaseStream.Position; // offset
                var fileName = br.ReadStringFixedLength(0xFC, Encoding.UTF8).TrimEnd('\0');
                var fileSize = br.ReadInt32();
                byte[] fileData = br.ReadBytes(fileSize);
                br.Align(0x40);

                var extracted = new List<Line>();
                var name = baseName + "|" + fileName + "|" + index.ToString();
                var ext = Path.GetExtension(fileName).ToLower();
                // Console.WriteLine("- " + fileName);

                switch (ext)
                {
                    case ".bmd":
                        extracted = BMD.ExtractText(fileData);
                        // sẽ test repack bên ngoài dat -> không chạy repack ở đây.
                        break;

                    case ".bf":
                        extracted = BF.ExtractText(fileData);
                        break;

                    case ".pac":
                        extracted = PAC.ExtractText(fileData, name);
                        if (extracted.Count > 0)
                        {
                            throw new Exception("No way!");
                        }
                        break;
                }

                if (extracted.Count > 0)
                {
                    result.Add(new Line(name + "|" + extracted.Count, string.Empty));
                    result.AddRange(extracted);
                }
                index++;
            }

            return result;
        }

        public static List<Line> ExtractText(byte[] data, string baseName = "")
        {
            using (var br = new EndianBinaryReader(new MemoryStream(data)))
                return ExtractText(br, baseName);
        }
    }
}

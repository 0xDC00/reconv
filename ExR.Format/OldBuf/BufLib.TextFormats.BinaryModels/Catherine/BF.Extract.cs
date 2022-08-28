// FlowScriptBinary

using BufLib.Common.Compression.Nintendo;
using BufLib.Common.IO;
using BufLib.TextFormats.DataModels;
using ExR.Format;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace BufLib.TextFormats.BinaryModels.Catherine
{
    public static partial class BF
    {
        public static List<Line> ExtractText(EndianBinaryReader br)
        {
            br.Endianness = _endian;

            var header = br.ReadStruct<Header>();
            var sections = br.ReadStructs<SectionHeader>(header.SectionCount);

            for (int i = 0; i < sections.Length; i++)
            {
                var section = sections[i];
                br.BaseStream.Position = section.FirstElementAddress;
                if (section.SectionType == SectionType.MessageScriptSection && section.ElementCount > 0)
                {
                    var bmd = br.ReadBytes(section.ElementCount);

                    /* Test */
                    if (i != 3)
                        throw new Exception("Sec: " + i);
                    var lastBlock = br.ReadBytes(0xF0);
                    var expect = new byte[lastBlock.Length];
                    if (lastBlock.SequenceEqual(expect) == false)
                        throw new Exception("Expect");
                    if (br.BaseStream.Position != br.BaseStream.Length)
                        throw new Exception("Expect");

                    var result = BMD.ExtractText(bmd);
                    if (result.Count > 0)
                    {
                        /* replace '￣' thành space để tiện edit  */
                        //foreach (var line in result)
                        //{
                        //    if (line.English.IndexOf(' ') != -1)
                        //    {
                        //        throw new Exception("Conflict!");
                        //    }
                        //    else
                        //    {
                        //        line.English = line.English.Replace('￣', ' ');
                        //    }
                        //}

                        // insert payload - compressed
                        br.BaseStream.Position = 0;
                        var payload = br.ReadBytes(section.FirstElementAddress);
                        payload = Nintendo.Compress(payload, Method.LZ11);

                        result.Insert(0, new Line(payload.ByteArrayToString(), string.Empty));
                    }

                    return result;
                }
            }

            return new List<Line>();
        }

        public static List<Line> ExtractText(byte[] data)
        {
            using (var br = new EndianBinaryReader(new MemoryStream(data)))
                return ExtractText(br);
        }
    }
}

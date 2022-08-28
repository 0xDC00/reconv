// File archive chứa BIN, MCD, TMD, SMD, ...
// MODE: Re-import

using BufLib.Common.IO;
using BufLib.TextFormats.DataModels;
using ExR.Format;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace BufLib.TextFormats.BinaryModels.NieRAutomata
{
    internal static partial class DAT
    {
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct Header
        {
            public int Magic; // 'DAT\x00'
            public int FileCount;
            public uint FileTableOffset;
            public uint ExtensionTableOffset;
            public uint NameTableOffset;
            public uint SizeTableOffset;
            public uint UnknownOffset1C;
            public uint Unknown1;   // Zero

            public Header(EndianBinaryReader br)
            {
                Magic = br.ReadInt32();
                FileCount = br.ReadInt32();
                FileTableOffset = br.ReadUInt32();
                ExtensionTableOffset = br.ReadUInt32();
                NameTableOffset = br.ReadUInt32();
                SizeTableOffset = br.ReadUInt32();
                UnknownOffset1C = br.ReadUInt32();
                Unknown1 = br.ReadUInt32();
            }
        }

        public static byte[] RepackText(List<Line> lines, byte[] oldDat)
        {
            /* replace one by one. Slow, but easy to implement */
            var info = lines[0].ID.Split('|');
            if (info[0] != string.Empty)
                throw new Exception("Only replace main directory (1 lv)");

            var index = int.Parse(info[2]);
            var numLine = int.Parse(info[3]);
            var ext = Path.GetExtension(info[1]).ToLower();
            Console.WriteLine("> " + info[1]);

            //var currentLines = lines.Skip(1).Take(numLine).ToList();
            //lines = lines.Skip(1 + numLine).ToList(); // remainLine case: many in one
            var currentLines = lines.GetRange(1, numLine);
            lines = lines.GetRange(1 + numLine, lines.Count - numLine - 1);

            byte[] result = null;
            switch (ext)
            {
                case ".smd":
                    result = SMD.RepackText(currentLines, ReadFileInDat(oldDat, index));
                    break;
                case ".tmd":
                    result = TMD.RepackText(currentLines);
                    break;
                case ".bin":
                    result = BIN.RepackText(currentLines, ReadFileInDat(oldDat, index));
                    break;
                case ".mcd":
                    result = MCD.RepackText(currentLines, ReadFileInDat(oldDat, index));
                    break;
            }

            // nếu repack thành công thì replace file trong block
            if (result != null && result.Length > 0)
                oldDat = ReplaceFile(oldDat, result, index);
            else
                Console.WriteLine("  Error");

            // Nếu còn text chưa repack thì tiếp tục repack
            if (lines.Count > 0) // remainLine
            {
                return RepackText(lines, oldDat); // remainLine, recursive: goi de quy lan nua
            }
            else
            {
                return oldDat;
            }

        }

        private static byte[] ReplaceFile(byte[] dat, byte[] file, int index)
        {
            var newFileSize = file.Length;
            var part2FileWithPadding = file.Align(0x10); // need align (pad) by 10

            using (var br = new EndianBinaryReader(new MemoryStream(dat)))
            {
#if BRIDGE_DOTNET
                var header = new Header(br);
#else
                var header = br.ReadStruct<Header>();
#endif

                /* offsets */
                var offsets = br.ReadInt32s(header.FileCount);

                /* sizes */
                br.BaseStream.Position = header.SizeTableOffset;
                var sizes = br.ReadInt32s(header.FileCount);

                var start = offsets[index];

                // nếu là replace file nằm ở cuối -. k cần fix pointer.
                if (header.FileCount == 1 || index == header.FileCount - 1)
                {
                    /* write new block at end & write new block size */
                    br.BaseStream.Position = 0;
                    var part1 = br.ReadBytes(start);
                    using (var ms = new MemoryStream(part1.Length + part2FileWithPadding.Length))
                    using (var bw = new BinaryWriter(ms))
                    {
                        bw.Write(part1);
                        bw.Write(part2FileWithPadding);

                        // fix size
                        bw.BaseStream.Position = header.SizeTableOffset + index * 4; // for first or last size;
                        bw.Write(newFileSize);

                        return ms.ToArray();
                    }
                }
                else
                {
                    /* replace 1 block & fix all pointers */
                    var end = offsets[index + 1];
                    var oldFileSizeWidthPadding = end - start;

                    /* split */
                    br.BaseStream.Position = 0;
                    var part1 = br.ReadBytes(start);
                    br.BaseStream.Position = end;
                    var part3 = br.ReadBytes((int)(br.BaseStream.Length - end));

                    /* join */
                    using (var ms = new MemoryStream(part1.Length + part2FileWithPadding.Length + part3.Length))
                    using (var bw = new BinaryWriter(ms))
                    {
                        bw.Write(part1);
                        bw.Write(part2FileWithPadding);
                        bw.Write(part3);

                        // nếu size file mới to hơn -> fix pointer.
                        var add = part2FileWithPadding.Length - oldFileSizeWidthPadding;
                        if (add != 0)
                        {
                            var seek = index * 4;
                            // fix next pointers
                            bw.BaseStream.Position = header.FileTableOffset + seek + 4;
                            for (int i = index + 1; i < header.FileCount; i++)
                            {
                                var newPointer = offsets[i] + add;
                                bw.Write(newPointer);
                            }

                            // fix size
                            bw.BaseStream.Position = header.SizeTableOffset + seek;
                            bw.Write(newFileSize);
                        }

                        return ms.ToArray();
                    }
                }

            }
        }

        private static byte[] ReadFileInDat(byte[] dat, int index)
        {
            using (var ms = new MemoryStream(dat))
            using (var br = new EndianBinaryReader(ms))
            {
#if BRIDGE_DOTNET
                var header = new Header(br);
#else
                var header = br.ReadStruct<Header>();
#endif
                /* offsets */
                var seek = index * 4;
                br.BaseStream.Position += seek;
                var offset = br.ReadInt32();

                /* sizes */
                br.BaseStream.Position = header.SizeTableOffset + seek;
                var size = br.ReadInt32();

                /* read */
                br.BaseStream.Position = offset;
                var result = br.ReadBytes(size);

                return result;
            }
        }
    }
}

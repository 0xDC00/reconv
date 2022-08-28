using BufLib.Common.IO;
using BufLib.TextFormats.BinaryModels.Souls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ExR.Format
{
    [Plugin("com.dc.souls", "Demon's Souls PS3, DARK SOULS 1, DARK SOULS 2, (.fmg)", @"Mode: ReImport
DARK SOULS: Prepare To Die
DARK SOULS II: Scholar of the First Sin

.msgbnd: https://github.com/JKAnderson/Yabber
")]
    class Souls : TextFormat
    {
        public Souls()
        {
            Extensions = new string[] { ".fmg" };
        }

        public override List<Line> ExtractText(byte[] buf)
        {
            using (var ms = new MemoryStream(buf))
            using (var br = new EndianBinaryReader(ms))
            {
                var result = new List<Line>();

                var header = br.ReadStruct<FmgHeader_1>();

                if (header.unknown2_1 == -255)
                {
                    br.Endianness = Endian.BigEndian;
                    br.BaseStream.Position = 0;
                    header = br.ReadStruct<FmgHeader_1>();
                    _Encoding = Encoding.BigEndianUnicode;
                }
                else
                {
                    _Encoding = Encoding.Unicode;
                }

                if (header.unknown1 != 0x10000)
                {
                    Console.WriteLine("[W] " + header.unknown1.ToString("X"));
                }

                var idRanges = br.ReadStructs<FmgIdRange_1>(header.idRangeCount);
                if (br.BaseStream.Position != header.stringOffsetSectionOffset)
                {
                    Console.WriteLine("[W] FmgIdRange Padding ?");
                    br.BaseStream.Position = header.stringOffsetSectionOffset;
                }
                var offsets = br.ReadInt32s(header.stringOffsetCount); // DeS, 1

                foreach (var idRange in idRanges)
                {
                    for (int i = 0; i < idRange.IdCount; i++)
                    {
                        var Id = idRange.FirstId + i;
                        var offset = offsets[idRange.OffsetIndex + i];
                        string Value;
                        if (offset > 0)
                        {
                            br.BaseStream.Position = offset;
                            Value = br.ReadTerminatedWideString(_Encoding);
                            if (Value.Length == 0)
                                Console.WriteLine("Pointer!=" + offset + ", string=Empty, ID=" + Id); // ensure only pointer=0 <-> empty
                        }
                        else
                        {
                            Value = string.Empty;
                        }
                        result.Add(new Line(Id, Value));
                    }
                }

                // check again, ensure pointer in ascending order.
                int j = 0;
                foreach (var offset in offsets)
                {
                    br.BaseStream.Position = offset;
                    var Value = br.ReadTerminatedWideString(_Encoding);
                    if (result[j++].English != Value)
                        throw new Exception("No, pointer is random sort/acess!!!");
                }

                return result;
            }
        }

        public override byte[] RepackText(List<Line> lines)
        {
            var currentFmg = ReadCurrentFileData(); // .fmg.csv -> .fmg (ReadCurrentFileData)
            using (var ms = new MemoryStream(_10MB))
            using (var bw = new EndianBinaryWriter(ms))
            using (var br = new EndianBinaryReader(ms))
            {
                bw.Write(currentFmg);
                ms.Position = 0;

                // Collected when dump text (endian, stringOffsetSectionOffset, table Offset) -> no need read again.
                var header = br.ReadStruct<FmgHeader_1>();

                if (header.unknown2_1 == -255)
                {
                    br.Endianness = Endian.BigEndian;
                    bw.Endianness = Endian.BigEndian;
                    br.BaseStream.Position = 0;
                    header = br.ReadStruct<FmgHeader_1>();
                    _Encoding = Encoding.BigEndianUnicode;
                }
                else
                {
                    _Encoding = Encoding.Unicode;
                }

                if (header.stringOffsetCount != lines.Count)
                    throw new Exception("Num line not match.");

                // get string offset
                bw.BaseStream.Position = header.stringOffsetSectionOffset;
                bw.BaseStream.Position += (lines.Count * 4); // 32bit

                // write to offset
                var offsets = new long[lines.Count];
                for (int i = 0; i < lines.Count; i++)
                {
                    // still replace if old offset is zero || not zero, maybe english version miss some text.
                    if (lines[i].English.Length != 0)
                    {
                        offsets[i] = bw.BaseStream.Position;
                        bw.Write(_Encoding.GetBytes(lines[i].English));
                        bw.Write((short)0);
                    }
                    else
                    {
                        offsets[i] = 0; // safe
                    }
                }

                // set new file length
                bw.Align(4); // pad 0x10 (DeS, DS1 = 4)
                ms.SetLength(bw.BaseStream.Position);

                // new file Size
                bw.BaseStream.Position = 4;
                bw.Write((int)ms.Length);

                // new pointer table
                bw.BaseStream.Position = header.stringOffsetSectionOffset;
                for (int i = 0; i < lines.Count; i++)
                {
                    bw.Write((int)offsets[i]); // 32bit
                }

                return ms.ToArray();
            }
        }
    }
}

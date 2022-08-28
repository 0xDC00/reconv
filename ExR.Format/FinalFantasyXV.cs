// -g ffxv -extract -in bak\FinalFantasyXV-Steam -out extracted\FinalFantasyXV-Steam -csv
// -g ffxv -extract -in bak\FinalFantasyXV-Steam-J -out extracted\FinalFantasyXV-Steam-J -csv
// -g ffxv -extract -in bak\FinalFantasyXV-Steam_v129_r1252180 -out extracted\FinalFantasyXV-Steam_v129_r1252180 -csv
// -g ffxv -extract -in bak\FinalFantasyXV-Steam_jp_v129_r1252180 -out extracted\FinalFantasyXV-Steam_jp_v129_r1252180 -csv
// Mode: Re-import
// Encoding: UTF-8 (default)
#define _NUMBER

using BufLib.Common.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace ExR.Format
{
    [Plugin("com.dc.ffxv", "Final Fantasy XV (message*.msgbin)", @"
Mode: ReImport
_init_.yaml (optional)
---
table: |-
  á=a
  à=a
...
"
)]
    class FinalFantasyXV : TextFormat
    {
        [StructLayout(LayoutKind.Sequential)]
        struct PonterType1
        {
            public uint Id;
            public int Offset;
        }

        public override bool Init(Dictionary<string, object> dict)
        {
            dict.TryGetValue("table", out var tableStr);
            _Encoding = new StandardEncoding((string)tableStr, Encoding.UTF8);
            return true;
        }

        public override List<Line> ExtractText(byte[] buf)
        {
            using (var ms = new MemoryStream(buf))
            using (var br = new EndianBinaryReader(ms))
            {
                var result = new List<Line>();

                br.BaseStream.Position = 0x120;
                var numPtr = br.ReadInt32();
                for (int i = 0; i < numPtr; i++)
                {
                    var pointer = br.ReadStruct<PonterType1>();
                    if (pointer.Offset == 0)
                    {
                        result.Add(new Line(pointer.Id, string.Empty)); // some pointer is NULL  -> save as empty [*] => conflict ?
                        Console.WriteLine("[W] NULL PTR: " + pointer.Id.ToString("X8"));
                    }
                    else
                    {
                        var pos = br.BaseStream.Position;

                        br.BaseStream.Position = pointer.Offset + 0x120;
                        var posLine = br.BaseStream.Position;
                        var line = ReadString(br);

                        /* try re-encode */
                        var len = br.BaseStream.Position - posLine - 1;
                        br.BaseStream.Position = posLine;
                        var raw = br.ReadBytes((int)len);
                        var raw2 = _Encoding.GetBytes(line);
                        if (raw.SequenceEqual(raw2) == false)
                        {
                            Console.WriteLine(raw.ByteArrayToString());
                            Console.WriteLine(raw2.ByteArrayToString());
                            Console.WriteLine(line);
                            throw new Exception();
                        }


                        result.Add(new Line(pointer.Id, line));
                        br.BaseStream.Position = pos;

                        if (line.Length == 0) // [*] some pointer point to empty text
                            Console.WriteLine("[D] Pointer=" + pointer.Offset + ", string=Empty, ID=" + pointer.Id); // ensure only pointer=0 <-> empty
                    }
                }

                // Too big
                //if (result.Count > 0)
                //{
                //    br.BaseStream.Position = 0;
                //    var payloadSize = 0x120 + 4 + (numPtr * 8);
                //    var payload = Nintendo.Compress(br.ReadBytes(payloadSize), Nintendo.Method.LZ11);
                //    result.Insert(0, new Line(payload.ByteArrayToString(), string.Empty));
                //}

                //var oldDat = ReadCurrentFileData();
                //var newDat = RepackText(result);
                //if (oldDat.SequenceEqual(newDat) == false)
                //    throw new Exception();

                return result;
            }
        }

        public override byte[] RepackText(List<Line> lines)
        {
            // wrong
            //var payload = Nintendo.Decompress(lines[0].Id.HexStringToByteArray());
            //lines.RemoveAt(0);
            var payload = ReadCurrentFileData(); // ReadCurrentFileData()

            using (var ms = new MemoryStream(_10MB))
            using (var bw = new EndianBinaryWriter(ms))
            using (var br = new EndianBinaryReader(ms))
            {
                bw.Write(payload);

                /* collect old pointers -> fix conflict */
                br.BaseStream.Position = 0x120;
                var numPtr = br.ReadInt32();

                var oldPointers = br.ReadStructs<PonterType1>(numPtr);  //item1=ID, item2=Offset

                /* jump to text offset, and encode all lines */
                bw.BaseStream.Position = 0x120 + 4 + (lines.Count * 8); // header + table
                var linePointers = new List<int>(lines.Count);

                int pointerIndex = 0;
                foreach (var line in lines)
                {
                    if (line.English.Length != 0 || oldPointers[pointerIndex].Offset != 0)
                    {
                        linePointers.Add((int)(bw.BaseStream.Position - 0x120));

                        // line.English = line.English.Replace("\r\n", "\n"); // correct breakline format

#if _NUMBER
                        var id = oldPointers[pointerIndex].Id - 184000000; // 4_DEBUG
                        line.English = id.ToString() + "." + line.English;    // 4_DEBUG: id_text
#endif

                        var raw = _Encoding.GetBytes(line.English);
                        bw.Write(raw);
                        bw.Write((byte)0); // null-terminated
                    }
                    else
                    {
                        linePointers.Add(0);
                    }
                    pointerIndex++;
                }

                /* write new size */
                var blockSize = (int)(bw.BaseStream.Position - 0x100); // no metaHeader, no padding
                bw.Align(0x100);

                // write new block Size
                var fileSize = (int)bw.BaseStream.Position;
                bw.BaseStream.Position = 0xC;
                bw.Write(fileSize);
                bw.BaseStream.Position = 0x38;
                bw.Write(blockSize);
                bw.BaseStream.Position = 0x10C;
                bw.Write(blockSize);

                /* write new pointer table */
                bw.BaseStream.Position = 0x120 + 4; // header + 4byte numPtr
                var buf = new byte[4];
                foreach (var pointer in linePointers)
                {
                    bw.BaseStream.Position += 4; // skip ID

                    var oldPtr = br.ReadInt32();
                    if (oldPtr != 0)
                    {
                        bw.BaseStream.Position -= 4;
                        bw.Write(pointer);
                    }
                }

                return ms.ToArray();
            }
        }

        string ReadString(EndianBinaryReader br)
        {
            var sb = new StringBuilder();
            var xraw = new List<byte>();
            while (true)
            {
                var c = br.ReadChar();
                if (c == 0)
                {
                    break;
                }
                else if (c == 0xA)
                {
                    sb.Append("\n");
                    xraw.Add(0xA);
                }
                else if (c == 0x19)
                {
                    var byte2 = br.ReadByte();
                    br.BaseStream.Position -= 2;

                    if (byte2 == '#') // 23
                        byte2 = 3; // 19 # (02|01)
                    else if (byte2 == '*') // 2A - onlyJP
                        byte2 = 5;
                    else
                        byte2 = 4; // 19 ?? (02|01) 00 // ($%'"

                    var buf = br.ReadBytes(byte2);
                    //sb.Append("($");
                    //sb.Append(buf.ByteArrayToString());
                    //sb.Append(")");
                    sb.Append(buf.ByteArrayToStringCustom());
                    xraw.AddRange(buf);
                }
                else if (c < 0x6)
                {
                    // 3 4 5
                    var byte2 = br.ReadByte();
                    br.BaseStream.Position -= 2;

                    if (byte2 == 0xF9)
                        byte2 = 6;
                    else if (byte2 == 0)
                        byte2 = 1; // ?
                    else
                        byte2 = 2;

                    
                    var buf = br.ReadBytes(byte2);
                    //sb.Append("($");
                    //sb.Append(buf.ByteArrayToString());
                    //sb.Append(")");
                    sb.Append(buf.ByteArrayToStringCustom());
                    xraw.AddRange(buf);
                }
                else if (c < 0x20)
                {
                    br.BaseStream.Position -= 1;

                    var byte2 = 1; // 10 11 15 18
                    switch (c)
                    {
                        case (char)0x17:
                        case (char)0x1A:
                        case (char)0x1B:
                        case (char)0x1C:
                        case (char)0x1D:
                            byte2 = 2; // 17 1A 1B 1C 1D
                            break;
                    }

                    var buf = br.ReadBytes(byte2);
                    //sb.Append("($");
                    //sb.Append(buf.ByteArrayToString());
                    //sb.Append(")");
                    sb.Append(buf.ByteArrayToStringCustom());
                    xraw.AddRange(buf);
                }
                else // char
                {
                    sb.Append(c);
                    xraw.AddRange(Encoding.UTF8.GetBytes(c.ToString()));
                }

            }

            return sb.ToString();
            //return Encoding.UTF8.GetString(xraw.ToArray());
        }
    }
}

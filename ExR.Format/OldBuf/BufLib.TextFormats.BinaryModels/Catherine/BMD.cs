// Mode: Compile

using BufLib.Common.Compression.Nintendo;
using ExR.Format;
using BufLib.Common.IO;
using BufLib.TextFormats.DataModels;
using BufLib.TextFormats.Helper.Atlus;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using static BufLib.TextFormats.DataModels.Catherine;

namespace BufLib.TextFormats.BinaryModels.Catherine
{
    public static partial class BMD
    {
        private static readonly List<int> mAddressLocations;   // for generating the relocation table; không gồm offset ở main header.
        private static Encoding Encoding;
        private static Encoding EncodingLE;
        private static Encoding EncodingBE;
        private static Encoding _encoding;
        private static Endian _endian;

        static BMD()
        {
            mAddressLocations = new List<int>();
        }

        public static void Init(Platform PF)
        {
            switch (PF)
            {
                case Platform.PS3_EN:
                case Platform.PS3_JP:
                case Platform.Steam_Classis:
                    _encoding = Encoding.BigEndianUnicode;
                    _endian = Endian.BigEndian;
                    break;
                default:
                    _encoding = Encoding.Unicode;
                    _endian = Endian.LittleEndian;
                    break;
            }

            // auto replace for repack
            EncodingBE = new StandardEncoding("\\n=\\0" + "\n" + " =￣", Encoding.BigEndianUnicode);
            EncodingLE = new StandardEncoding("\\n=\\0" + "\n" + " =_", Encoding.Unicode);
        }


        public static byte[] RepackText(List<Line> lines)
        {
            mAddressLocations.Clear();
            mAddressLocations.Add(0x18); // &PointerTableOffset (not SubHeaderSize, value=10+18=28)
            mAddressLocations.Add(0x20); // &speakerPointer

            var headers = lines[0].ID.HexStringToByteArray(); // 0x28 byte
            headers = Nintendo.Decompress(headers);

            int numLine = BitConverter.ToInt32(headers, 0x1C);
            if (BitConverter.ToInt32(headers, 0) == 0x12345678)
            {
                _endian = Endian.LittleEndian;
                Encoding = EncodingLE;
            }
            else
            {
                _endian = Endian.BigEndian;
                numLine = EndiannessHelper.Reverse(numLine);
                Encoding = EncodingBE;
            }

            lines.RemoveAt(0);

            using (var ms = new MemoryStream())
            using (var bw = new EndianBinaryWriter(ms, _endian))
            {
                /* write header, subheader, pointers */
                bw.Write(headers);
                /* collect &Pointer */
                AddAddressLocations((int)bw.BaseStream.Position, numLine);

                // write empty pointer table!
                bw.Write(new byte[numLine * 4]);

                /* write MSGs */
                var pointers = new int[numLine]; int index = 0;
                for (int i = 0; i < lines.Count; i++)
                {
                    pointers[index++] = (int)bw.BaseStream.Position - Header.Size;

                    // write header
                    var sMsgHeader = lines[i].ID.FromJson<DataModels.Catherine.MSGHeaderS>();
                    
                    //bw.WriteStruct(sMsgHeader.ToMSGHeader());
                    bw.Write(sMsgHeader.Type);
                    bw.WriteStringFixedLength(sMsgHeader.Title, 0x20, Encoding.ASCII);
                    bw.Write(sMsgHeader.NumLine);
                    bw.Write(sMsgHeader.SpeakerIndex);

                    /* collect &Pointer */
                    AddAddressLocations((int)bw.BaseStream.Position, sMsgHeader.NumLine);

                    /* write pointers & texts  */
                    if (sMsgHeader.Type == (int)MSGType.Dialogue)
                    {
                        WriteDialogues(bw, lines, ref i, sMsgHeader.NumLine);
                    }
                    else if (sMsgHeader.Type == (int)MSGType.Selection)
                    {
                        WriteSelections(bw, lines, ref i, sMsgHeader.NumLine);
                    }
                    else // psvita
                    {
                        WriteDialogues(bw, lines, ref i, sMsgHeader.NumLine);
                    }
                    bw.Align(4);
                }

                /* 0. update pointer MsgTable */
                bw.BaseStream.Position = Header.Size + SubHeader.Size; // [header_18][subHeader_10][pointers]
                bw.Write(pointers);

                /* 1. update pointer speaker - SubHeader */
                bw.BaseStream.Position = 0x20;
                int pointer = (int)(bw.BaseStream.Length);
                bw.Write(pointer - Header.Size); // tương đối

                /* 2. write speaker */
                // Empty -> do nothing.

                // 2.1 If psvita => update first pointer?
                // TODO: check all bmd file

                /* 3. update pointer RelocationTable - Header*/
                var relocationTable = RelocationTableEncoding.Encode(mAddressLocations, Header.Size);

                bw.BaseStream.Position = 0x10;
                pointer = (int)(bw.BaseStream.Length);
                bw.Write(pointer); // tuyệt đối
                bw.Write(relocationTable.Length); // reloc size

                /* 4. write RelocationTable*/
                bw.BaseStream.Position = bw.BaseStream.Length;
                bw.Write(relocationTable);

                /* 5. update bmd size */
                bw.BaseStream.Position = 0xC;
                bw.Write((int)(bw.BaseStream.Length));

                return ms.ToArray();
            }
        }

        static void WriteDialogues(EndianBinaryWriter bw, List<Line> lines, ref int index, int count)
        {
            /* write empty pointers */
            var offsetOfPointerTable = bw.BaseStream.Position;
            bw.Write(new byte[count * 4]);

            var pointers = new int[count];
            for (int i = 0; i < count; i++)
            {
                pointers[i] = (int)bw.BaseStream.Position - Header.Size;
                var line = lines[++index];
                var value = Line2String(line);
                var bytes = Encoding.GetBytes(value);
                bw.Write(bytes);
            }

            /* update pointers */
            bw.BaseStream.Position = offsetOfPointerTable;
            bw.Write(pointers);

            /* back to end */
            bw.BaseStream.Position = bw.BaseStream.Length;
        }

        static void WriteSelections(EndianBinaryWriter bw, List<Line> lines, ref int index, int count)
        {
            /* write empty pointers */
            var offsetOfPointerTable = bw.BaseStream.Position;
            bw.Write(new byte[count * 4]);
            bw.Write(0);
            var begin = bw.BaseStream.Position;

            var pointers = new int[count];
            for (int i = 0; i < count; i++)
            {
                pointers[i] = (int)bw.BaseStream.Position - Header.Size;
                var line = lines[++index];
                var value = Line2String(line);
                var bytes = Encoding.GetBytes(value);
                bw.Write(bytes);
            }
            bw.Write((byte)0); // 1 byte lạ.

            /* update pointers */
            bw.BaseStream.Position = offsetOfPointerTable;
            bw.Write(pointers);
            bw.Write((int)(bw.BaseStream.Length - begin)); // text size

            /* back to end */
            bw.BaseStream.Position = bw.BaseStream.Length;
        }

        static string Line2String(Line line)
        {
            var info = line.ID.FromJson<LineInfo>();
            return info.S + line.English + info.E;
        }

        static void AddAddressLocations(int offset, int count)
        {
            for (int i = 0; i < count; i++)
            {
                mAddressLocations.Add(offset);
                offset += 4;
            }
        }
    }
}

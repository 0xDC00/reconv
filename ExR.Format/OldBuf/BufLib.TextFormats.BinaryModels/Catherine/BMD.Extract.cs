using BufLib.Common.Compression.Nintendo;
using BufLib.Common.IO;
using BufLib.TextFormats.DataModels;
using ExR.Format;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using static BufLib.TextFormats.DataModels.Catherine;

namespace BufLib.TextFormats.BinaryModels.Catherine
{
    public static partial class BMD
    {
        private static readonly Regex regexHexBegin = new Regex(@"\G@*(\(\$([0-9a-fA-F]{2})+\))+", RegexOptions.Compiled); // lấy đoạn hex mở đầu
        private static readonly Regex regexHexEnd = new Regex(@"(\(\$([0-9a-fA-F]{2})+\))*\n*(\(\$([0-9a-fA-F]{2})+\))+$", RegexOptions.Compiled); // đoạn hex cuối chuổi
        private static string SPACE_CHAR = "￣";

        public static List<Line> ExtractText(EndianBinaryReader br)
        {
            var beginBMD = br.BaseStream.Position;
            br.Endianness = Endian.LittleEndian;

            var sig = br.ReadUInt32();
            br.BaseStream.Position -= 4;
            if (sig == 0x12345678)
            {
                _endian = Endian.LittleEndian;
                _encoding = Encoding.Unicode;
                SPACE_CHAR = "_";
            }
            else
            {
                _endian = Endian.BigEndian;
                _encoding = Encoding.BigEndianUnicode;
                SPACE_CHAR = "￣";
            }
            br.Endianness = _endian;

            var result = new List<Line>();

            var header = br.ReadStruct<Header>();
            var subHeader = br.ReadStruct<SubHeader>();
            var msgPointers = br.ReadInt32s(subHeader.NumMsg);

            if (subHeader.SpeakerCount > 0)
                throw new Exception("SpeakerCount");

            /* Extract MSGs */
            for (int i = 0; i < msgPointers.Length; i++)
            {
                br.BaseStream.Position = msgPointers[i] + Header.Size; // relative pointer, without main header.
                var msgHeader = br.ReadStruct<MSGHeader>();
                var pointers = br.ReadInt32s(msgHeader.NumLine);

                // -> |type|numLine|actorIndex(-1)|title|
                var sMsgHeader = msgHeader.ToMSGHeader().ToJson();
                result.Add(new Line(sMsgHeader, string.Empty));

                if (msgHeader.Type == MSGType.Dialogue)
                {
                    ReadDialogues(br, pointers, ref result);
                }
                else if (msgHeader.Type == MSGType.Selection)
                {
                    ReadSelections(br, pointers, ref result);
                }
                else // psvita first block is pointer to 4 byte zero - [lastblock] b1 b2 b3 b4 [reloc]
                {
                    ReadDialogues(br, pointers, ref result);
                }
            }

            if (result.Count > 0)
            {
                // lưu phần header để repack (thay vì reimport)
                br.BaseStream.Position = 0;
                var payload = br.ReadBytes(Header.Size + SubHeader.Size);
                payload = Nintendo.Compress(payload, Method.LZ11);
                result.Insert(0, new Line(payload.ByteArrayToString(), string.Empty));
            }

            // cuối BMD, (eboot safe reimport, newSize nhỏ hơn hoặc bằng)
            br.BaseStream.Position = beginBMD + header.FileSize;

            return result;
        }

        static void ReadDialogues(EndianBinaryReader br, int[] pointers, ref List<Line> lines)
        {
            for (int i = 0; i < pointers.Length; i++)
            {
                var pointer = pointers[i] + Header.Size;
                _TestSkip(br, pointer);

                br.BaseStream.Position = pointer;
                string value = ReadStringWithControls(br, MSGType.Dialogue);
                lines.Add(String2Line(value));
            }
        }

        static void ReadSelections(EndianBinaryReader br, int[] pointers, ref List<Line> lines)
        {
            int textLength = br.ReadInt32();
            for (int i = 0; i < pointers.Length; i++)
            {
                var pointer = pointers[i] + Header.Size;
                _TestSkip(br, pointer);

                br.BaseStream.Position = pointer;
                string value = ReadStringWithControls(br, MSGType.Selection);
                lines.Add(String2Line(value));
            }
            var b = br.ReadByte(); // check byte lạ.
            if (b != 0)
                throw new Exception("ReadSelections.b, offset: " + br.BaseStream.Position.ToString("X"));
        }

        static void _TestSkip(EndianBinaryReader br, long pointer)
        {
            // Đảm bảo hàm đọc string không bỏ sót byte (trường hợp không có D821)
            // hoặc selection có byte lạ ở cuối.
            var sub = pointer - br.BaseStream.Position;
            if (sub > 0)
            {
                var data = br.ReadBytes((int)sub);
                foreach (var x in data)
                {
                    if (x != 0)
                        throw new Exception("[_TestSkip] ReadStringWithControls!");
                }
            }
        }

        static Line String2Line(string s)
        {
            var begin = string.Empty;
            var end = string.Empty;

            var mBe = regexHexBegin.Match(s);
            if (mBe.Success)
            {
                begin = mBe.Value.Replace(")($", string.Empty);
                var len = s.Length - mBe.Length;
                if (len > 0)
                    s = s.Substring(mBe.Length, len);
                else
                {
                    s = string.Empty;
                }
            }

            var mEn = regexHexEnd.Match(s);
            if (mEn.Success)
            {
                end = mEn.Value.Replace(")($", string.Empty);
                s = s.Remove(mEn.Index);
            }

            var info = new LineInfo()
            {
                S = begin,
                E = end
            };

            return new Line(info.ToJson(), s);
        }

        static string ReadStringWithControls(EndianBinaryReader br, MSGType type)
        {
            // Control decode: https://github.com/MrStPL-codes/CatherineBmdExport/blob/a691d90f767ddfc18298565d333516ef838287e9/main.c#L105
            // PS3:    D821 be => D821
            // PSVita: 21D8 le => D821
            // Control 2 4 6 8 byte
            // TODO: vita swap 2 byte => ps3? (encoding repack write whose block)
            // but all hex move to id column, No: lgmes, mfmes, pzl_01_01, pzl_01_01_360, pzl_02_01
            // => can't copy ps3 to psvita
            var sb = new StringBuilder();
            while (true)
            {
                var num = br.ReadUInt16();
                var b2 = num & 0xFF;
                var b1 = num >> 8;
                var sig = new byte[2] { (byte)b1, (byte)b2 };
                //var sig = br.ReadBytes(2);
                if (sig[0] == 0 && sig[1] == 0)
                {
                    if (type == MSGType.Selection)
                    {
                        sb.Append("($0000)"); // Selection luôn kết thúc = 0000
                        break;
                    }
                    else
                    {
                        // không đổi thành \n nếu nằm ở cuối.
                        // => kiểm tra 2 byte kế
                        var sig2 = br.ReadBytes(2);
                        br.BaseStream.Position -= 2;
                        if (sig2[0] == 0xD8 && sig2[1] == 0x21)
                        {
                            sb.Append("($0000)");
                            ReadHexString(br, sb, 2);
                            break;
                        }
                        else if (sig2[0] == 0 && sig2[1] == 0)
                        {
                            // trường hợp không có D821
                            // có khi nào có 2 dấu \n liên tục?
                            sb.Append("($0000)");
                            break;
                        }
                        else
                        {
                            sb.Append('\n'); // repack cần replace \n => 0000
                        }
                    }   
                }
                else
                {
                    // check control.
                    int numByte = 0;
                    switch (sig[0])
                    {
                        case 0xD8:
                            numByte = 2;
                            if (sig[1] == 0x21) // Dialogue luôn kết thúc = D821
                            {
                                if (type == MSGType.Dialogue)
                                {
                                    br.BaseStream.Position -= 2; // peak!
                                    ReadHexString(br, sb, numByte);
                                    return sb.ToString();
                                }
                            }

                            break;

                        case 0xD9:
                            numByte = 4;
                            break;

                        case 0xDA:
                            numByte = 6;
                            break;

                        case 0xDB:
                            numByte = 8;
                            break;
                    }

                    // read as control or char
                    if (numByte > 0)
                    {
                        br.BaseStream.Position -= 2; // peak!
                        ReadHexString(br, sb, numByte);
                    }
                    else
                    {
                        // Read 1 char
                        br.BaseStream.Position -= 2;
                        sig = br.ReadBytes(2); // lazy code
                        var c = _encoding.GetString(sig);
                        if (c == "\n" || c == " ")
                        {
                            // '\n'='\0'
                            throw new Exception("Conflict!");
                        }
                        else if (c == SPACE_CHAR)
                        {
                            // Space- khoảng cách bmd có khác biệt nếu nằm trong BF
                            // BMD space=' '   -> giữ nguyên
                            // BF  space='￣'  -> tự replace sang space (khi extract) và ngược lại (khi repack)
                            // throw new Exception("￣");
                            sb.Append(' '); //  repack cần replace ' ' -> '￣' => 0000
                        }
                        else
                        {
                            sb.Append(c);
                        }
                    }
                }
            }

            return sb.ToString();
        }

        static void ReadHexString(EndianBinaryReader br, StringBuilder sb, int numByte)
        {
            var block = br.ReadBytes(numByte);
            //var str = block.ByteArrayToString();
            //sb.Append("($");
            //sb.Append(str);
            //sb.Append(")");
            sb.Append(block.ByteArrayToStringCustom());
        }

        public static List<Line> ExtractText(byte[] data)
        {
            using (var br = new EndianBinaryReader(new MemoryStream(data)))
                return ExtractText(br);
        }
    }
}

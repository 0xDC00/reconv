// \data\cellphone\mail\MailData.DAT
// Mode: Re-Import

using BufLib.Common.IO;
using BufLib.TextFormats.DataModels;
using ExR.Format;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace BufLib.TextFormats.BinaryModels.Catherine
{
    public enum Platform : int
    {
        PS3_EN,
        PS3_JP,
        Steam_Classis,
        PSVita
    }

    internal static partial class DAT
    {
        static int szSendFrom;
        static int szSubject;
        static int szMessage;
        static int szJunkData;
        static int szReply;
        static Endian _endian;
        static Encoding _encoding;

        public static void Init(Platform PF)
        {
            _endian = Endian.BigEndian;
            _encoding = Encoding.BigEndianUnicode;

            switch (PF)
            {
                case Platform.PS3_EN:
                    szSendFrom = 0x28;
                    szSubject = 0x3C;
                    szMessage = 0x320;
                    szJunkData = 0x80 * 0x94;
                    szReply = 0x90;
                    break;
                case Platform.PS3_JP:
                    szSendFrom = 0x28;
                    szSubject = 0x28;
                    szMessage = 0x200;
                    szJunkData = 0x80 * 0x2C;
                    szReply = 0x28;
                    break;
                case Platform.Steam_Classis:
                    szSendFrom = 0x28;
                    szSubject = 0x4A;
                    szMessage = 0x35C;
                    szJunkData = 0x80 * 0xB0;
                    szReply = 0xAC;
                    break;
                default:
                    break;
            }
        }

#if !BRIDGE_DOTNET
        public static List<Line> ExtractText(EndianBinaryReader br)
        {
            br.Endianness = _endian;

            var numMsg = br.ReadInt32();
            var result = new List<Line>(numMsg);
            br.BaseStream.Position = 4 + 0x80;
            for (int i = 0; i < numMsg; i++)
            {
                // header
                var id = br.ReadByte();
                var day = br.ReadByte();
                var sendTo = br.ReadInt16();
                br.BaseStream.Position += 8 * 4;

                // data
                var sendFrom = br.ReadStringFixedLength(szSendFrom, Encoding.BigEndianUnicode).TrimEnd('\0');
                var subject = br.ReadStringFixedLength(szSubject, Encoding.BigEndianUnicode).TrimEnd('\0');
                var message = br.ReadStringFixedLength(szMessage, Encoding.BigEndianUnicode).TrimEnd('\0');

                result.Add(new Line("*" + id, sendFrom));
                result.Add(new Line("-" + id, subject));
                result.Add(new Line("-" + id, message));

                // br.BaseStream.Position += szJunkData;
                // đọc reply
                for(int j=0; j<128; j++)
                {
                    sbyte repId = br.ReadSByte();
                    sbyte u1 = br.ReadSByte();
                    short u2 = br.ReadInt16();
                    if(u2 == 0xFF) // nếu khác -1
                    {
                        var reply = br.ReadStringFixedLength(szReply, Encoding.BigEndianUnicode).TrimEnd('\0');
                        result.Add(new Line(j + "|" + repId + "|" + u1, reply));
                    }
                    else
                    {
                        br.BaseStream.Position += szReply;
                    }
                }
            }

            if (br.BaseStream.Position != br.BaseStream.Length)
                throw new Exception("DAT.ExtractText");

            return result;
        }
#endif
        public static byte[] RepackText(List<Line> lines, byte[] oldMailData)
        {
            using (var ms = new MemoryStream(oldMailData))
            using (var bw = new EndianBinaryWriter(ms, _endian))
            {
                ms.Position = 4 + 0x80;

                for (int i = 0; i < lines.Count; i++)
                {
                    // skip header
                    bw.BaseStream.Position += 0x24;

                    // write new data
                    var sendFrom = lines[i].English;
                    var subject = lines[++i].English;
                    var message = lines[++i].English;

                    bw.WriteStringFixedLength(sendFrom, szSendFrom, _encoding);
                    bw.WriteStringFixedLength(subject, szSubject, _encoding);
                    bw.WriteStringFixedLength(message, szMessage, _encoding);

                    var pos = bw.BaseStream.Position;

                    // Bridge.NET Buggy
                    //loop:
                    //// nếu là số -> tin nhắn có reply
                    //int nextI = i + 1;
                    //if (nextI == lines.Count)
                    //    break;

                    //var check = lines[nextI].Id[0];
                    //if (check < ('9' + 1) && check > ('0' - 1))
                    //{
                    //    var reply = lines[++i];
                    //    var index = int.Parse(reply.Id.Split(new char[] { '|' }, 2)[0]);

                    //    // nhảy đến block cần ghi, và skip 4byte header
                    //    bw.BaseStream.Position += (szReply + 4) * index + 4;
                    //    bw.WriteStringFixedLength(reply.English, szReply, Encoding.BigEndianUnicode);

                    //    // quay về đầu mảng reply
                    //    bw.BaseStream.Position = pos;
                    //    goto loop;
                    //}

                    // // nếu không còn reply -> sang tin nhắn kế
                    // bw.BaseStream.Position += szJunkData;

                    // => fix
                    // nếu là số -> tin nhắn có reply
                    while(true)
                    {
                        int nextI = i + 1;
                        if (nextI == lines.Count)
                            break;

                        var check = lines[nextI].ID[0];
                        if (check < ('9' + 1) && check > ('0' - 1))
                        {
                            var reply = lines[++i];
                            var index = int.Parse(reply.ID.Split(new char[] { '|' }, 2)[0]);

                            // nhảy đến block cần ghi, và skip 4byte header
                            bw.BaseStream.Position += (szReply + 4) * index + 4;
                            bw.WriteStringFixedLength(reply.English, szReply, _encoding);

                            // quay về đầu mảng reply
                            bw.BaseStream.Position = pos;
                        }
                        else
                        {
                            bw.BaseStream.Position += szJunkData;
                            break;
                        }
                    }

                }

                return ms.ToArray();
            }
        }
    }
}

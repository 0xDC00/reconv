using BufLib.Common.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using static BufLib.TextFormats.BinaryModels.NintendoU.MSBT;

namespace ExR.Format
{
    [Plugin("com.dc.nintendoMSBT", "# Nintendo (.MSBT)", @"
Extract\ReImport
_init_.yaml (optional)
---
table: |-
  A=a
  b=b
...

Notes:
- Fatal Frame: Maiden of Black Water (Zero: Nuregarasu no Miko)
- The Legend of Zelda: Breath of the Wild
- ...
")]
    class MSBT : TextFormat
    {
        public const string LabelFilter = @"^[a-zA-Z0-9_]+$";
        public const uint LabelHashMagic = 0x492;
        public const int LabelMaxLength = 64;

        private const long _byteOrderOffset = 0x8;
        private byte _paddingChar = 0xAB;

        public Header Header = new Header();
        private string _replaceTable;

        public override bool Init(Dictionary<string, object> dict)
        {
            dict.TryGetValue("table", out var tableStr);
            _replaceTable = (string)tableStr; // null can be string
            return true;
        }

        public override byte[] RepackText(List<Line> lines)
        {
            var target = ReadCurrentFileData();
            var br = new EndianBinaryReader(new MemoryStream(target));

            // get endian
            br.BaseStream.Position = _byteOrderOffset;
            var byteOrder = (Endian)br.ReadUInt16();
            br.Endianness = byteOrder;
            br.BaseStream.Position = 0;

#if BRIDGE_DOTNET
            Header = new Header
            {
                Magic = br.ReadInt64(),
                ByteOrder = (Endian)br.ReadUInt16(),
                Unknown1 = br.ReadUInt16(),
                EncodingByte = (EncodingByte)br.ReadByte(),
                Unknown2 = br.ReadByte(),
                NumberOfSections = br.ReadUInt16(),
                Unknown3 = br.ReadUInt16(),
                FileSize = br.ReadUInt32(),
                Padding = br.ReadBytes(0xA)
            };
#else
            Header = br.ReadStruct<Header>();
#endif
            _Encoding = Header.EncodingByte == EncodingByte.UTF8 ? new StandardEncoding(_replaceTable, Encoding.UTF8)
                : Header.ByteOrder == Endian.BigEndian ? new StandardEncoding(_replaceTable, Encoding.BigEndianUnicode)
                : new StandardEncoding(_replaceTable, Encoding.Unicode);

            var sections = new List<byte[]>(Header.NumberOfSections);

            if (Header.Magic != "MsgStdBn") throw new InvalidMSBTException("The file provided is not a valid MSBT file.");
            if (Header.FileSize != br.BaseStream.Length) throw new InvalidMSBTException("The file provided is not a valid MSBT file. Filesize mismtach.");

            // build new TXT2
            int txt2Pos = 0;
            Section txt2Sec = null;
            for (var i = 0; i < Header.NumberOfSections; i++)
            {
                var pos = br.BaseStream.Position;
#if BRIDGE_DOTNET
                var s = new Section
                {
                    Magic = br.ReadInt32(),
                    Size = br.ReadUInt32(),
                    Padding = br.ReadBytes(8)
                };
#else
                var s = br.ReadStruct<Section>();
#endif

                if (s.Magic == "TXT2")
                {
                    txt2Sec = s;
                    txt2Pos = i;
                }

                br.BaseStream.Position += s.Size;
                br.Align(0x10);
                var size = br.BaseStream.Position - pos;
                br.BaseStream.Position = pos;

                var bytes = br.ReadBytes((int)size);
                sections.Add(bytes);
            }

            // replace
            sections[txt2Pos] = RebuildTXT2(lines, txt2Sec);

            // Write all
            using (var ms = new MemoryStream(_10MB))
            using (var bw = new EndianBinaryWriter(ms, Header.ByteOrder))
            {
                bw.BaseStream.Position = Header.Size;
                foreach (var section in sections)
                    bw.Write(section);

                Header.FileSize = (uint)bw.BaseStream.Position;
                bw.BaseStream.Position = 0;
#if BRIDGE_DOTNET
                bw.Write((long)Header.Magic);
                bw.Write((ushort)Header.ByteOrder);
                bw.Write(Header.Unknown1);
                bw.Write((byte)Header.EncodingByte);
                bw.Write(Header.Unknown2);
                bw.Write(Header.NumberOfSections);
                bw.Write(Header.Unknown3);
                bw.Write(Header.FileSize);
                bw.Write(Header.Padding);
#else
                bw.WriteStruct(Header);
#endif

                return ms.ToArray();
            };
        }

#if !BRIDGE_DOTNET
        public LBL1 LBL1;
        //public NLI1 NLI1;
        //public ATO1 ATO1;
        public ATR1 ATR1;
        //public TSY1 TSY1;
        public TXT2 TXT2;
        public List<string> SectionOrder;
        public bool HasLabels;
        public bool HasAttributes;

        public override List<Line> ExtractText(byte[] buf)
        {
            using (var ms = new MemoryStream(buf))
            using (var br = new EndianBinaryReader(ms))
            {
                Header = new Header();


                // NLI1 = new NLI1();
                // ATO1 = new ATO1();
                // TSY1 = new TSY1();

                SectionOrder = new List<string>();

                br.BaseStream.Position = _byteOrderOffset;
                var endian = (Endian)br.ReadUInt16();
                br.Endianness = endian;

                br.BaseStream.Position = 0;
                Header = br.ReadStruct<Header>();
                _Encoding = Header.EncodingByte == EncodingByte.UTF8 ? Encoding.UTF8 : Header.ByteOrder == Endian.BigEndian ? Encoding.BigEndianUnicode : Encoding.Unicode;

                if (Header.Magic != "MsgStdBn") throw new InvalidMSBTException("The file provided is not a valid MSBT file.");
                if (Header.FileSize != br.BaseStream.Length) throw new InvalidMSBTException("The file provided is not a valid MSBT file. Filesize mismtach.");

                for (var i = 0; i < Header.NumberOfSections; i++)
                {
                    var magic = Encoding.ASCII.GetString(br.ReadBytes(4));
                    br.BaseStream.Position -= 4;
                    switch (magic)
                    {
                        case "LBL1":
                            LBL1 = new LBL1();
                            ReadLBL1(br);
                            SectionOrder.Add("LBL1");
                            break;
                        case "ATR1":
                            ATR1 = new ATR1();
                            ReadATR1(br);
                            SectionOrder.Add("ATR1");
                            break;
                        case "TXT2":
                            TXT2 = new TXT2();
                            ReadTXT2(br);
                            SectionOrder.Add("TXT2");
                            break;

                        default:
                            SkipSection(br);
                            break;
                    }
                }

                return ToLines();
            }
        }

        private void ReadLBL1(EndianBinaryReader br)
        {
            LBL1.Section = br.ReadStruct<Section>();
            LBL1.NumberOfGroups = br.ReadUInt32();

            LBL1.Groups = br.ReadStructs<Group>((int)LBL1.NumberOfGroups).ToList();
            foreach (Group grp in LBL1.Groups)
                for (var i = 0; i < grp.NumberOfLabels; i++)
                    LBL1.Labels.Add(new Label
                    {
                        Name = br.ReadStringFixedLength(Convert.ToInt32(br.ReadByte()), Encoding.ASCII).TrimEnd('\0'),
                        Index = br.ReadUInt32(),
                        Checksum = (uint)LBL1.Groups.IndexOf(grp)
                    });

            //// Old rename correction
            //foreach (var lbl in LBL1.Labels)
            //{
            //    var previousChecksum = lbl.Checksum;
            //    lbl.Checksum = SimpleHash.Create(lbl.Name, LabelHashMagic, LBL1.NumberOfGroups);

            //    if (previousChecksum == lbl.Checksum) continue;
            //    LBL1.Groups[(int)previousChecksum].NumberOfLabels -= 1;
            //    LBL1.Groups[(int)lbl.Checksum].NumberOfLabels += 1;
            //}

            HasLabels = LBL1.Labels.Count > 0;

            br.Align(0x10);
            //_paddingChar = br.SeekAlignment(_paddingChar);
        }

        private void ReadTXT2(EndianBinaryReader br)
        {
            TXT2.Section = br.ReadStruct<Section>();
            var startOfStrings = (int)br.BaseStream.Position;
            TXT2.NumberOfStrings = br.ReadInt32();

            var offsets = br.ReadUInt32s(TXT2.NumberOfStrings);

            for (var i = 0; i < TXT2.NumberOfStrings; i++)
            {
                var raw = br.ReadBytes((i + 1 < offsets.Length ? startOfStrings + (int)offsets[i + 1] : startOfStrings + (int)TXT2.Section.Size) - (startOfStrings + (int)offsets[i]));
                var str = GetString(raw);
                TXT2.Strings.Add(new BufLib.TextFormats.BinaryModels.NintendoU.MSBT.String
                {
                    Text = str,
                    Index = (uint)i
                });

                // test
                //var raw_new = GetBytes(str);
                //if (raw.SequenceEqual(raw_new) == false)
                //{
                //    throw new Exception("TXT2 Encoding");
                //}
            }

            // Tie in LBL1 labels
            foreach (var lbl in LBL1.Labels)
            {
                lbl.String = TXT2.Strings[(int)lbl.Index];

                // DC fix
                lbl.String.LblName = lbl.Name;
            }

            br.Align(0x10);
            //_paddingChar = br.SeekAlignment(_paddingChar);
        }

        private void ReadATR1(EndianBinaryReader br)
        {
            ATR1.Section = br.ReadStruct<Section>();
            var next = br.BaseStream.Position + ATR1.Section.Size;
            //ATR1.Unknown = br.ReadBytes((int)ATR1.Section.Size); // Read in the entire section at once since we don't know what it's for

            long baseOffset = br.BaseStream.Position;
            int numAttributes = br.ReadInt32();
            if (br.ReadInt32() == 0)
                numAttributes = 0;

            for (int i = 0; i < numAttributes; i++)
            {
                int atrOffset = br.ReadInt32();

                long returnOffset = br.BaseStream.Position;

                br.BaseStream.Position = baseOffset + atrOffset;
                LBL1.Labels[i].Attribute = GetAttribute(br);
                br.BaseStream.Position = returnOffset;
            }

            HasAttributes = numAttributes > 0;

            br.BaseStream.Position = next;
            br.Align(16);
        }

        private void SkipSection(EndianBinaryReader br)
        {
            ATR1.Section = br.ReadStruct<Section>();
            var next = br.BaseStream.Position + ATR1.Section.Size;
            br.BaseStream.Position = next;
            br.Align(16);
        }

        // Tools
        private string GetAttribute(BinaryReader reader)
        {
            // Attributes are stored as UTF-16 strings, where each char is stored in a short (two bytes).
            // We're going to read in shorts until we get a short that's just 0, which represents \n.
            // Then, we'll convert the list of shorts to a list of bytes.
            // From there we'll use the Unicode encoding to get the actual string.

            List<short> attributeChars = new List<short>();

            short testChar = reader.ReadInt16();
            while (testChar != 0)
            {
                attributeChars.Add(testChar);
                testChar = reader.ReadInt16();
            }

            byte[] byteChars = new byte[attributeChars.Count * sizeof(short)];
            Buffer.BlockCopy(attributeChars.ToArray(), 0, byteChars, 0, byteChars.Length);

            string attributeString = new string(Encoding.Unicode.GetChars(byteChars));
            return attributeString;
        }

        /// <summary>
        /// ATR1|LBL1,TXT2
        /// </summary>
        /// <returns></returns>
        public List<Line> ToLines()
        {
            var result = new List<Line>();

            // orderBy label
            //if (HasAttributes)
            //{
            //    foreach (var label in LBL1.Labels)
            //    {
            //        result.Add(new Line($"{label.Attribute}|{label.Name}", label.Text));
            //    }
            //}
            //else
            //{
            //    foreach (var label in LBL1.Labels)
            //    {
            //        result.Add(new Line($"{label.Name}", label.Text));
            //    }
            //}

            // orderBy text
            foreach (var line in TXT2.Strings)
            {
                result.Add(new Line($"{line.LblName}", line.Text));
            }

            return result;
        }

        public string GetString(byte[] bytes)
        {
            var sb = new StringBuilder();
            using (var ms = new MemoryStream(bytes))
            using (var br = new EndianBinaryReader(ms, _Encoding, Header.ByteOrder))
            {
                while (br.BaseStream.Length != br.BaseStream.Position)
                {
                    var pos = br.BaseStream.Position;
                    var c = br.ReadChar();
                    if (c == 0xE)
                    {
                        br.BaseStream.Position += 4;
                        int count = br.ReadInt16();
                        br.BaseStream.Position = pos;
                        var hex = br.ReadBytes(8 + count);
                        //sb.Append("($");
                        //sb.Append(hex.ByteArrayToString());
                        //sb.Append(")");
                        sb.Append(hex.ByteArrayToStringCustom());

                        //sb.Append((char)br.ReadInt16());
                        //sb.Append((char)br.ReadInt16());
                        //int count = br.ReadInt16();
                        //sb.Append((char)count);
                        //for (var i = 0; i < count; i++)
                        //{
                        //    sb.Append((char)br.ReadByte());
                        //}
                    }
                    else
                    {
                        sb.Append(c);
                    }
                }
            }
            return sb.Remove(sb.Length - 1, 1).ToString(); // remove \0
        }
#endif
        public byte[] GetBytes(string str)
        {
            return _Encoding.GetBytes(str + '\0');

            //var ms = new MemoryStream(1024 * 1024);
            //using(var bw = new EndianBinaryWriter(ms, Header.ByteOrder))
            //{
            //    for (var i = 0; i < str.Length; i++)
            //    {
            //        var c = str[i];
            //        bw.Write(c);
            //        if (c == 0xE)
            //        {
            //            bw.Write((short)str[++i]);
            //            bw.Write((short)str[++i]);
            //            int count = str[++i];
            //            bw.Write((short)count);
            //            for (var j = 0; j < count; j++)
            //            {
            //                bw.Write((byte)str[++i]);
            //            }
            //        }
            //    }
            //    bw.Write('\0');
            //}
            //return ms.ToArray();
        }

        private byte[] RebuildTXT2(List<Line> lines, Section section)
        {
            // from: https://github.com/IcySon55/Kuriimu/blob/master/src/text/text_msbt/MSBT.cs#L374
            var tableSize = lines.Count * 4 + 4; // sizeof(uint) + sizeof(uint)

            var offsets = new int[lines.Count];
            var current = tableSize;
            var index = 0;

            var stringBytes = lines.Select(line =>
            {
                offsets[index++] = current;
                var bytes = GetBytes(line.English);
                current += bytes.Length;
                return bytes;
            }).ToList();

            section.Size = (uint)current; // new size

            using (var ms = new MemoryStream(current))
            using (var bw = new EndianBinaryWriter(ms, Header.ByteOrder))
            {
#if BRIDGE_DOTNET
                
#else
                bw.WriteStruct(section);
#endif

                bw.Write(lines.Count);

                foreach (var offset in offsets)
                    bw.Write(offset);

                foreach (var str in stringBytes)
                    bw.Write(str);

                // bw.WriteAlignment(_paddingChar)
                var al = AlignmentHelper.GetAlignedDifference(bw.BaseStream.Position, 16);
                for (int i = 0; i < al; i++)
                {
                    bw.Write(_paddingChar);
                }

                return ms.ToArray();
            }
        }

    }
}

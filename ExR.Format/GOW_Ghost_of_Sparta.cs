using BufLib.Common.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ExR.Format
{
    [Plugin("com.dc.gow_coo_gos_ps3", "God of War: Origins Collection PS3 (.BIN)", @"Chains of Olympus & Ghost of Sparta
Extract/ReImport
_init_.yaml (optional)
---
table: |-
  á=a
  à=a
...
Note: ENGLISH/*.BIN")]
    class GOW_Chains_of_Olympus_PS3 : GOW_Ghost_of_Sparta
    {
        protected override bool IsPS3 => true;
    }

    [Plugin("com.dc.gow_coo_psp", "God of War: Chains of Olympus PSP (.BIN)", @"Extract/ReImport
_init_.yaml (optional)
---
table: |-
  á=a
  à=a
...
Note: ENGLISH/*.BIN")]
    class GOW_Chains_of_Olympus : GOW_Ghost_of_Sparta
    {
        protected override bool IsCOO => true;
    }

    [Plugin("com.dc.gow_gos_psp", "God of War: Ghost of Sparta PSP (.BIN)", @"Extract/ReImport
_init_.yaml (optional)
---
table: |-
  á=a
  à=a
...
Note: DATA/ENGLISH/*.BIN")]
    class GOW_Ghost_of_Sparta : TextFormat
    {
        public override bool Init(Dictionary<string, object> dict)
        {
            Extensions = new string[] { ".bin" };
            if (dict.TryGetValue("table", out var table))
            {
                _Encoding = new StandardEncoding((string)table, Encoding.UTF8);
            }
            else
            {
                _Encoding = RunMode == Mode.Extract ? Encoding.UTF8 : new StandardEncoding("", Encoding.UTF8);
            }

            return true;
        }

        protected virtual bool IsCOO { get; }
        protected virtual bool IsPS3 { get; }

        public override List<Line> ExtractText(byte[] buf)
        {
            using (var ms = new MemoryStream(buf))
            using (var br = new EndianBinaryReader(ms))
            {
                if (IsPS3) br.Endianness = Endian.BigEndian;

                br.BaseStream.Position = 0xC;
                var numFile = br.ReadInt32();

                // Read file pointers
                br.BaseStream.Position = 0x40;
                var Pointers = new List<SDataInfo>();
                for (int i = 0; i < numFile; i++)
                {
                    br.BaseStream.Position += 4;
                    var offset = br.ReadInt32();
                    var size = br.ReadInt32();
                    Pointers.Add(new SDataInfo(offset, size, Pointers.Count));
                    br.BaseStream.Position += 4;
                }

                Pointers.Sort((p1, p2) => { return p1.Offset.CompareTo(p2.Offset); });

                // find text block and extract
                for (int i = 0; i < numFile; i++)
                {
                    var pointer = Pointers[i];
                    br.BaseStream.Position = pointer.Offset;
                    var rawBlock = br.ReadBytes(pointer.Size);
                    var magicBlock = Encoding.ASCII.GetString(new byte[] { rawBlock[0], rawBlock[1], rawBlock[2], rawBlock[3] });
                    if (!(magicBlock == "RADI" || magicBlock == "IDAR"
                            || magicBlock == "CfCm" || magicBlock == "mCfC"))
                    {
                        if (IsPS3)
                        {
                            var stringsPs3 = BlockStringsPS3(rawBlock);
                            return stringsPs3;
                        }
                        else
                        {
                            var strings = BlockStrings(rawBlock);
                            return strings; // rawTxt
                        }
                    }
                }

                return null; // BIN have no text.
            }
        }

        public override byte[] RepackText(List<Line> lines)
        {
            var curData = ReadCurrentFileData();
            using (var oldms = new MemoryStream(curData))
            using (var br = IsPS3 ? new EndianBinaryReader(oldms, Endian.BigEndian) : new BinaryReader(oldms))
            {
                br.BaseStream.Position = 0xC;
                var numFile = br.ReadInt32();

                // Read file pointers
                br.BaseStream.Position = 0x40;
                var Pointers = new List<SDataInfo>();
                for (int i = 0; i < numFile; i++)
                {
                    br.BaseStream.Position += 4;
                    var offset = br.ReadInt32();
                    var size = br.ReadInt32();
                    Pointers.Add(new SDataInfo(offset, size, Pointers.Count));
                    br.BaseStream.Position += 4;
                }

                Pointers.Sort((p1, p2) => { return p1.Offset.CompareTo(p2.Offset); }); // order by OFFSET

                var realSize = long.MinValue;
                var lechPointer = 0;
                for (int i = 0; i < numFile; i++)
                {
                    var pointer = Pointers[i];
                    pointer.oIndex = i;
                    br.BaseStream.Position = pointer.Offset;
                    var rawBlock = br.ReadBytes(pointer.Size);
                    var magicBlock = Encoding.ASCII.GetString(new byte[] { rawBlock[0], rawBlock[1], rawBlock[2], rawBlock[3] });

                    // 1 - only fix after read
                    pointer.Offset += lechPointer;

                    if (!(magicBlock == "RADI" || magicBlock == "IDAR"
                            || magicBlock == "CfCm" || magicBlock == "mCfC"))
                    {
                        byte[] newBlock;
                        if (IsPS3)
                        {
                            newBlock = StringsBlockPS3(rawBlock, lines);
                        }
                        else
                        {
                            newBlock = StringsBlock(rawBlock, lines); // reimport text
                        }

                        // Pointers[i].Data = newBlock; // replace
                        Pointers[i].Size = newBlock.Length; // fix

                        // 0- only fix after read
                        var newpad = (0x10 - (newBlock.Length % 0x10)) % 0x10;
                        var oldPad = (0x10 - (rawBlock.Length % 0x10)) % 0x10;
                        lechPointer = (newBlock.Length + newpad) - (rawBlock.Length + oldPad);

                        // safe PADDING
                        Array.Resize(ref newBlock, newBlock.Length + newpad);
                        Pointers[i].Data = newBlock; // autoPad
                    }
                    else
                    {
                        Pointers[i].Data = rawBlock;
                    }
                    if (realSize < br.BaseStream.Position)
                        realSize = br.BaseStream.Position;
                }

                ////////////////////////
                // get header
                ////////////////////////
                br.BaseStream.Position = 0;
                var header = br.ReadBytes(Pointers[0].Offset);
                byte[] footer = null;

                ////////////////////////
                // get footer
                ////////////////////////
                br.BaseStream.Position = realSize;
                var pad = (0x10 - (br.BaseStream.Position % 0x10)) % 0x10;
                br.BaseStream.Position += pad;

                var szJunk = br.BaseStream.Length - br.BaseStream.Position;
                if (szJunk > 0)
                {
                    footer = br.ReadBytes((int)szJunk);
                }

                ////////////////////////
                // Re-Construct
                ////////////////////////
                // build new file
                var newFile = new List<byte>();
                var basePointer = header.Length;
                //Pointers.Sort((p1, p2) => { return p1.Index.CompareTo(p2.Index); }); // order by INDEX
                Pointers.Sort((p1, p2) => { return p1.Offset.CompareTo(p2.Offset); }); // fix1: order by OFFSET
                foreach (var pointer in Pointers)
                {
                    newFile.AddRange(pointer.Data);
                }
                newFile.InsertRange(0, header);
                basePointer = newFile.Count; // size without JUNK
                if (footer != null) newFile.AddRange(footer);

                Pointers.Sort((p1, p2) => { return p1.Index.CompareTo(p2.Index); }); // fix2: order by INDEX
                // fix pointer & size
                var result = newFile.ToArray();
                using (var ms = new MemoryStream(result))
                using (var bw = IsPS3 ? new EndianBinaryWriter(ms, Endian.BigEndian) : new BinaryWriter(ms))
                {
                    bw.BaseStream.Position = 0x40;
                    foreach (var pointer in Pointers)
                    {
                        bw.BaseStream.Position += 4;
                        bw.Write(pointer.Offset);
                        bw.Write(pointer.Size);
                        bw.BaseStream.Position += 4;
                    }

                    // only for GOS_PSP -  without JUNK
                    if (IsPS3 == false)
                    {
                        // gos psp = 0x28, (coo=0x24)
                        if (IsCOO == false)
                            bw.BaseStream.Position = 0x28; // GOS
                        else
                            bw.BaseStream.Position = 0x24; // COO

                        bw.Write(basePointer); // new file Size?
                    }
                    else
                    {
                        //// gos, coo ps3
                        //bw.BaseStream.Position = 0x2C; // int[6-1]
                        //bw.Write(basePointer); // new file Size?
                        //// No it pointer!
                        // TODO: test and fix
                        //if (Pointers.Count == 1)
                        //{
                        //    bw.BaseStream.Position = 0x2C;
                        //    bw.Write(basePointer); // new file Size?
                        //}
                        //else if (Pointers.Count == 3) // hack
                        //{
                        //    Pointers.Sort((p1, p2) => { return p1.Offset.CompareTo(p2.Offset); }); // fix1: order by OFFSET
                        //    bw.BaseStream.Position = 0x2C;
                        //}
                        //// HACK (untested)
                        bw.BaseStream.Position = 0x2C;
                        bw.Write(basePointer); // new file Size?
                        bw.Write(0);
                        bw.Write(0);
                        bw.Write(0);
                        bw.Write(0);
                    }
                }

                return result;
            }
        }

        List<Line> BlockStrings(byte[] rawBlock)
        {
            var result = new List<Line>();

            using (var br = new BinaryReader(new MemoryStream(rawBlock)))
            {
                br.BaseStream.Position = 0x18;
                var relOffPointerTable = br.ReadInt32(); // relative -> absolute
                var absOffPointerTable = br.BaseStream.Position + relOffPointerTable; // Save to fileName
                uint markEnd = 0;
                var OffsetSecondText = 0;
                long offsetSzPtrandText = 0;
                while (true)
                {
                    offsetSzPtrandText = br.BaseStream.Position;
                    markEnd = br.ReadUInt32();
                    if (markEnd == 0x8BAD839A) // end of pointer table
                    {
                        offsetSzPtrandText -= 4;
                        br.BaseStream.Position -= 8;
                        OffsetSecondText = br.ReadInt32(); //
                        break;
                    }
                }

                br.BaseStream.Position = absOffPointerTable;
                var Pointers = new List<STextPointer>(); // like gos
                while (true)
                {
                    br.BaseStream.Position += 0xC;
                    var tmpPtr = br.ReadInt32();
                    var tmpPointer = new STextPointer(tmpPtr, Pointers.Count);
                    if (tmpPtr != 0x1C0008) // 08001C00 vs 001C0008
                    {
                        //lstPointer.Add(tmpPtr);
                        Pointers.Add(tmpPointer);
                    }
                    else
                    {
                        break;
                    }
                }

                Pointers.Sort((p1, p2) => { return p1.Value.CompareTo(p2.Value); });

                // process text
                br.BaseStream.Position = 0x20;
                var relOffBlockText = br.ReadInt32();
                var avsOffBlockText = br.BaseStream.Position + relOffBlockText;

                // end int = 9A83AD8B

                br.BaseStream.Position = avsOffBlockText;
                var flag = br.ReadInt32();
                var txtBlockSize = br.ReadInt32();
                var txtBasePointer = br.BaseStream.Position; // min = 4?

                br.BaseStream.Position = 0;
                var rawHeader = br.ReadBytes((int)txtBasePointer); // .hdr
                var rawTexts = br.ReadBytes(txtBlockSize);

                var footerSize = br.BaseStream.Length - br.BaseStream.Position;
                var rawFooter = br.ReadBytes((int)footerSize);

                result.Add(new Line(absOffPointerTable.ToString("d8"))); // for fix pointer
                result.Add(new Line(offsetSzPtrandText.ToString("d8"))); // for write new Text+Pointer+? Size
                result.Add(new Line((avsOffBlockText + 4).ToString("d8"))); // for write new textSize

                result.Add(new Line(Pointers.Count.ToString()));
                int i = 0;
                foreach (var pointer in Pointers)
                {
                    br.BaseStream.Position = txtBasePointer + pointer.Value;
                    var line = ReadNullTerminatedStringCSV(br);
                    var id = i++.ToString("d4") + "=" + pointer.Index.ToString("d4"); // save pointer index
                    result.Add(new Line(id, line));
                }

                result.Sort((p1, p2) =>
                {
                    return SortIndex1(p1, p2);
                });
            }

            return result;
        }

        List<Line> BlockStringsPS3(byte[] rawBlock)
        {
            var result = new List<Line>();

            using (var br = new EndianBinaryReader(new MemoryStream(rawBlock), Endian.BigEndian))
            {
                br.BaseStream.Position = 0x20;
                var relOffPointerTable = br.ReadInt32(); // relative -> absolute
                var absOffPointerTable = br.BaseStream.Position + relOffPointerTable; // Save to fileName

                br.BaseStream.Position = absOffPointerTable;
                br.BaseStream.Position += 8;
                var Pointers = new List<STextPointer>(); // like gos
                while (true)
                {
                    br.BaseStream.Position += 0xC;
                    var tmpPtr = br.ReadInt32();
                    var tmpPointer = new STextPointer(tmpPtr, Pointers.Count);
                    if (tmpPtr != 0x000C0024) // 08001C00 vs 001C0008
                    {
                        //lstPointer.Add(tmpPtr);
                        Pointers.Add(tmpPointer);
                        br.BaseStream.Position += 4;
                    }
                    else
                    {
                        break;
                    }
                }

                Pointers.Sort((p1, p2) => { return p1.Value.CompareTo(p2.Value); });

                // process text
                br.BaseStream.Position = 0x28;
                var relOffBlockText = br.ReadInt32();
                var avsOffBlockText = br.BaseStream.Position + relOffBlockText;

                // end int = 9A83AD8B

                br.BaseStream.Position = avsOffBlockText;
                var flag = br.ReadInt32();
                var txtBlockSize = br.ReadInt32();
                var txtBasePointer = br.BaseStream.Position; // min = 4?

                br.BaseStream.Position = 0;
                var rawHeader = br.ReadBytes((int)txtBasePointer); // .hdr
                var rawTexts = br.ReadBytes(txtBlockSize);

                var footerSize = br.BaseStream.Length - br.BaseStream.Position;
                var rawFooter = br.ReadBytes((int)footerSize);

                result.Add(new Line(absOffPointerTable.ToString("d8"))); // for fix pointer
                // result.Add(new Line(offsetSzPtrandText.ToString("d8"))); // for write new Text+Pointer+? Size
                result.Add(new Line((avsOffBlockText + 4).ToString("d8"))); // for write new textSize

                result.Add(new Line(Pointers.Count.ToString()));
                int i = 0;
                foreach (var pointer in Pointers)
                {
                    br.BaseStream.Position = txtBasePointer + pointer.Value;
                    var line = ReadNullTerminatedStringCSV(br);
                    var id = i++.ToString("d4") + "=" + pointer.Index.ToString("d4"); // save pointer index
                    result.Add(new Line(id, line));
                }

                result.Sort((p1, p2) =>
                {
                    return SortIndex1(p1, p2);
                });
            }

            return result;
        }

        // insert text to block
        byte[] StringsBlock(byte[] rawBlock, List<Line> lines)
        {
            //////////////////////////////
            // build new text block
            //////////////////////////////
            lines.Sort((p1, p2) =>
            {
                return SortIndex0(p1, p2);
            });
            var text = lines.ToArray();

            var absOffPointerTable = int.Parse(text[0].English);
            var offsetSzPtrandText = int.Parse(text[1].English);
            var avsOffBlockText = int.Parse(text[2].English);
            var numLine = int.Parse(text[3].English) + 4;

            // encode text to binary, and save pointer
            var Pointers = new List<STextPointer>();
            var rawText = new List<byte>();
            for (int i = 4; i < numLine; i++)
            {
                var line = text[i];
                var spl = line.ID.Split(new char[] { '=' }, 2);

                var pointer = new STextPointer();
                pointer.Index = int.Parse(spl[1]);
                pointer.Value = rawText.Count;
                Pointers.Add(pointer);

                var raw = EncodeString(line.English);
                rawText.AddRange(raw);
                rawText.Add(0);
            }
            // add padding to text
            var pad = (4 - (rawText.Count % 4)) % 4;
            if (pad > 0) rawText.AddRange(new byte[pad]);

            //////////////////////////////
            // Extract header and footer from rawBlock
            //////////////////////////////
            var headerSize = avsOffBlockText + 4;
            var previousTxtBlockSize = BitConverter.ToInt32(rawBlock, avsOffBlockText);
            var footerSize = rawBlock.Length - previousTxtBlockSize - headerSize;
            byte[] header = new byte[headerSize];
            byte[] footer = new byte[footerSize];
            Array.Copy(rawBlock, header, header.Length);
            Array.Copy(rawBlock, headerSize + previousTxtBlockSize, footer, 0, footer.Length);

            //////////////////////////////
            // FIX Header
            //////////////////////////////
            using (var bw = new BinaryWriter(new MemoryStream(header), Encoding.Unicode))
            {
                // write new pointer
                foreach (var pointer in Pointers)
                {
                    bw.BaseStream.Position = absOffPointerTable + (pointer.Index * 0x10) + 0xC;
                    bw.Write(pointer.Value);
                }

                // write size text + pointer + ? -> footer
                if (offsetSzPtrandText > 0x20)
                {
                    var unkValue = header.Length + rawText.Count - offsetSzPtrandText;
                    bw.BaseStream.Position = offsetSzPtrandText;
                    bw.Write(unkValue);
                }

                // write new block text size
                bw.BaseStream.Position = avsOffBlockText;
                bw.Write(rawText.Count);

                // write new FINAL file size
                var newSize = header.Length + rawText.Count + footer.Length - 4;
                bw.BaseStream.Position = 0;
                bw.Write(newSize);
            }

            // JOIN ALL [HEADER-TEXT-FOOTER]
            rawText.AddRange(footer);
            rawText.InsertRange(0, header);

            return rawText.ToArray();
        }

        byte[] StringsBlockPS3(byte[] rawBlock, List<Line> lines)
        {
            //////////////////////////////
            // build new text block
            //////////////////////////////
            lines.Sort((p1, p2) =>
            {
                return SortIndex0(p1, p2);
            });
            var text = lines.ToArray();

            var absOffPointerTable = int.Parse(text[0].English);
            var avsOffBlockText = int.Parse(text[1].English); // 2->1
            var numLine = int.Parse(text[2].English) + 3; // 3->2 line (absOffPointerTable, offsetSzPtrandText, avsOffBlockText), we only need 2
            // 4 line infos (no only 3)

            // encode text to binary, and save pointer
            var Pointers = new List<STextPointer>();
            var rawText = new List<byte>();
            for (int i = 3; i < numLine; i++) // skip first 3 line
            {
                var line = text[i];
                var spl = line.ID.Split(new char[] { '=' }, 2);

                var pointer = new STextPointer();
                pointer.Index = int.Parse(spl[1]);
                pointer.Value = rawText.Count;
                Pointers.Add(pointer);

                var raw = EncodeString(line.English);
                rawText.AddRange(raw);
                rawText.Add(0);
            }
            // add padding to text
            var pad = (4 - (rawText.Count % 4)) % 4;
            if (pad > 0) rawText.AddRange(new byte[pad]);

            //////////////////////////////
            // Extract header and footer from rawBlock
            //////////////////////////////
            var headerSize = avsOffBlockText + 4;
            var previousTxtBlockSize = BitConverter.ToInt32(rawBlock, avsOffBlockText).Reverse();
            var footerSize = rawBlock.Length - previousTxtBlockSize - headerSize;
            byte[] header = new byte[headerSize];
            byte[] footer = new byte[footerSize];
            Array.Copy(rawBlock, header, header.Length);
            Array.Copy(rawBlock, headerSize + previousTxtBlockSize, footer, 0, footer.Length);

            //////////////////////////////
            // FIX Header
            //////////////////////////////
            using (var bw = new EndianBinaryWriter(new MemoryStream(header), Encoding.Unicode, Endian.BigEndian))
            {
                // write new pointer
                foreach (var pointer in Pointers)
                {
                    bw.BaseStream.Position = absOffPointerTable + (pointer.Index * 0x14) + 0xC + 8; // size=14, offset C, skip 8
                    bw.Write(pointer.Value);
                }

                // write new block text size
                bw.BaseStream.Position = avsOffBlockText;
                bw.Write(rawText.Count);

                // write new FINAL file size
                var newSize = header.Length + rawText.Count + footer.Length - 4;
                bw.BaseStream.Position = 0;
                bw.Write(newSize);
            }

            // JOIN ALL [HEADER-TEXT-FOOTER]
            rawText.AddRange(footer);
            rawText.InsertRange(0, header);

            return rawText.ToArray();
        }

        byte[] EncodeString(string text)
        {
            text = text.Replace("\r\n", "\n");
            return _Encoding.GetBytes(text);
        }

        string ReadNullTerminatedStringCSV(BinaryReader br)
        {
            var builder = new StringBuilder();

            char ch;
            var pos = br.BaseStream.Position;
            while ((ch = br.ReadChar()) != 0)
            {
                // encode hex
                var numByte = br.BaseStream.Position - pos;
                if (numByte == 3)
                {
                    // check ReadedByte
                    br.BaseStream.Position -= 3;
                    var sig = br.ReadBytes(3);
                    if (sig[0] == 0xEF && sig[1] == 0xBC) // japanse code
                    {
                        builder.Append(ch);
                        continue;
                    }
                    else if (sig[0] >= 0xEE)
                    {
                        builder.Append(sig.ByteArrayToStringCustom());
                        pos = br.BaseStream.Position;
                        continue;
                    }
                }

                // default
                switch (ch)
                {
                    case '\r':
                        builder.Append("[\\r]");
                        break;
                    case '\n':
                        builder.Append("\r\n");
                        break;
                    case '\t':
                        builder.Append("[\\t]");
                        break;
                    default:
                        var raw = Encoding.UTF8.GetBytes(ch.ToString());
                        var ch2 = _Encoding.GetString(raw);
                        builder.Append(ch2);
                        break;
                }
                pos = br.BaseStream.Position;
            }

            return builder.ToString();
        }

        int SortIndex0(Line p1, Line p2)
        {
            var valueP1 = p1.ID.Split(new char[] { '=' }, 2);
            var valueP2 = p2.ID.Split(new char[] { '=' }, 2);
            if (valueP1.Length == 1 && valueP2.Length == 1)
                return 0;
            if (valueP1.Length == 1)
                return -1;
            if (valueP2.Length == 1)
                return 1;
            var p1Val = int.Parse(valueP1[0]);
            var p2Val = int.Parse(valueP2[0]);

            return p1Val.CompareTo(p2Val);
        }

        int SortIndex1(Line p1, Line p2)
        {
            var valueP1 = p1.ID.Split(new char[] { '=' }, 2);
            var valueP2 = p2.ID.Split(new char[] { '=' }, 2);
            if (valueP1.Length == 1 && valueP2.Length == 1)
                return 0;
            if (valueP1.Length == 1)
                return -1;
            if (valueP2.Length == 1)
                return 1;
            var p1Val = int.Parse(valueP1[1]);
            var p2Val = int.Parse(valueP2[1]);

            return p1Val.CompareTo(p2Val);
        }

        struct STextPointer
        {
            public int Value;
            public int Index;

            public STextPointer(int value, int index)
            {
                Value = value;
                Index = index;
            }
        }

        class SDataInfo
        {
            public int Offset;
            public int Size;
            public int Index;
            public int oIndex;
            public byte[] Data;

            public SDataInfo(int offset, int size, int index)
            {
                Offset = offset;
                Size = size;
                Index = index;
                oIndex = 0;
                Data = null;
            }
        }
    }
}

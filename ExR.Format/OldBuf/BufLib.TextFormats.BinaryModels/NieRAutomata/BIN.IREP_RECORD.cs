using BufLib.Common.IO;
using BufLib.TextFormats.DataModels;
using ExR.Format;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace BufLib.TextFormats.BinaryModels.NieRAutomata
{
    internal static partial class BIN
    {
        public class IREP_RECORD
        {
            public static Encoding Encoding = null; //  ->RecordToBinary

            public int section_size;
            public short nlocals;
            public short nregs;
            public short rlen;
            public uint[] ByteCodes;
            public string[] CONST; // string, int, float
            public string[] Symbols;
            private EndianBinaryReader br;

            // Read = new->ExtractLines
            // Write = new->Compile

            public IREP_RECORD(EndianBinaryReader reader)
            {
                br = reader;
            }

            public bool Read()
            {
                section_size = br.ReadInt32();
                nlocals = br.ReadInt16();
                nregs = br.ReadInt16();
                rlen = br.ReadInt16();

                /* bytecode */
                var ilen = br.ReadInt32();
                if (ilen > 0)
                {
                    br.Align(4);
                    ByteCodes = br.ReadUInt32s(ilen);
                }

                /* const */
                var plen = br.ReadInt32();
                if (plen > 0)
                {
                    /* read all const */
                    CONST = new string[plen];
                    for (int i = 0; i < plen; i++)
                    {
                        var type = br.ReadByte();
                        var str = br.ReadStringPrefixedLength16(Encoding.UTF8); // not contain null-terminated
                        switch (type)
                        {
                            case 0:
                                CONST[i] = str;
                                break;
                            case 1:
                                // throw new Exception("[ERR]BIN->IREP_TT_FIXNUM");
                                CONST[i] = str;
                                break;
                            case 2:
                                // throw new Exception("[ERR]BIN->IREP_TT_FLOAT_");
                                CONST[i] = str;
                                break;
                        }
                    }
                }

                /*  symbol */
                var slen = br.ReadInt32();
                if (slen > 0)
                {
                    Symbols = new string[slen];
                    for (int i = 0; i < slen; i++)
                    {
                        var len = br.ReadInt16();
                        if (len > 0)
                        {
                            Symbols[i] = br.ReadStringFixedLength(len, Encoding.UTF8);
                            br.BaseStream.Position += 1; // \0
                            if (Symbols[i] == string.Empty)
                                throw new Exception("[ERR]BIN->Symbol->Len=0");
                        }
                        else if (len < 0)
                        {
                            //throw new Exception("[ERR]BIN->Symbol->Len=0");
                            Symbols[i] = string.Empty;
                        }
                        else
                        {
                            // ???
                        }
                    }
                }

                return true;
            }

#if !BRIDGE_DOTNET
            public List<Line> ExtractLines()
            {
                var result = new List<Line>();
                if (CONST == null || CONST.Length == 0)
                    return result;

                /* doc cac byte code la string -> index */
                var indexOfStrings = new List<uint>();
                var symName = new List<string>(); // tên nhân vật

                // ngon ngu luu thanh tung mang, thu dem mot mang => so ngon ngu.
                int countLang = 0;
                int numLang = -1; // 8 || 6, english after japanese
                uint lastOp = 0;
                foreach (var bytecode in ByteCodes)
                {
                    var opcode = bytecode & 0x7F;
                    if (opcode == 61) // OP_STRING
                    {
                        var arg_bx = (bytecode >> 7) & 0xffff;
                        indexOfStrings.Add(arg_bx); // index
                        countLang++;
                        lastOp = opcode;
                    }
                    else if (opcode == 55)
                    {
                        if (lastOp == 61 && numLang == -1)
                        {
                            numLang = countLang;
                        }
                    }
                    //else if(opcode == 72)
                    //{
                    //    var arg_a = (bytecode >> 23) & 0x1ff; // OP_TCLASS
                    //    Console.WriteLine(arg_a);
                    //}
                    //else if(opcode == 64)
                    //{
                    //    var arg_a = (bytecode >> 23) & 0x1ff;
                    //    var arg_b2 = (bytecode >> (7 + 2)) & ((1 << 14) - 1);
                    //    var arg_c2 = (bytecode >> 7) & ((1 << 2) - 1);
                    //    Console.WriteLine(arg_a + " " + arg_b2 + " " + arg_c2);
                    //}
                    else if (opcode == 18)
                    {
                        // https://github.com/micktu/att/blob/master/src/script.cpp#L59
                        var arg_a = (bytecode >> 23) & 0x1ff;
                        var arg_bx = (bytecode >> 7) & 0xffff;
                        if (arg_bx < Symbols.Length)
                            symName.Add(mrb_sym2name(Symbols[arg_bx]));
                        else
                            symName.Add(string.Empty);
                    }
                    else
                    {
                        lastOp = uint.MaxValue;
                    }
                }
                if (symName.Count == 0)
                {
                    // Luôn có text nhân vật, nếu không thì... không hợp lệ, lưu ra xem thử.
                    Console.WriteLine("[WARN] BIN->Sym=0, numLang=" + numLang + " -> Dump all CONST to console:");
                    foreach (var co in CONST)
                    {
                        Console.WriteLine("    " + co.Replace("\n", "[\\n]"));
                    }
                    return result;
                }

                if (indexOfStrings.Count < numLang) // it nhat 1 line x8 ngon ngu
                    return result;

                if (numLang != 8)
                {
                    Console.WriteLine("[WARN] numLang=" + numLang);
                }

                /* LẤY NHỮNG INDEX HỢP LỆ của TIẾNG NHẬT */
                /* junk revent: 8 ngon ngu, -> 8 index phai khac nhau */
                var indexOfJapaneseSrings = new Dictionary<int, string>(); //index+name

                // TODO: case 6 ngon ngu??? (vẫn hoạt động)
                // trường hợp số ngôn ngữ lộn xộn 6 8 thì lỗi.
                for (int i = 0; i < indexOfStrings.Count; i += numLang)
                {
                    var range = indexOfStrings.GetRange(i, numLang); // chunks: [0->7], [8->15], ...
                    if (range.Distinct().Count() == range.Count)     // nếu trong range không có giá trị trùng lặp.
                    {
                        var key = (int)range[0];
                        if (indexOfJapaneseSrings.ContainsKey(key)) // double->skip
                        {
                            // Do index đã được lưu, nếu có tham chiếu khác thì bỏ qua.
                            Console.WriteLine("    DoubleKey->Skip: " + key.ToString().PadRight(4) + "|" + indexOfJapaneseSrings[key].PadRight(12) + "|" + CONST[key + 1]);
                        }
                        else
                        {
                            indexOfJapaneseSrings.Add(key, symName[i / numLang]);
                        }
                    }

                }
                if (indexOfJapaneseSrings.Count == 0)
                    return result;

                /* EXTACT TEXT THEO INDEX HỢP LỆ */
                foreach (var index in indexOfJapaneseSrings)
                {
                    try
                    {
                        var enIndex = index.Key + 1;
                        var jp = CONST[index.Key];
                        var en = CONST[enIndex]; // có trường hợp không có tiếng Anh -> Catch

                        if (en.Length > 0)
                            result.Add(new Line(enIndex.ToString() + "_" + index.Value, en, string.Empty, jp));
                    }
                    catch
                    {
                        // trường hợp lạ, không có text anh (tested: 1 case)
                        Console.WriteLine("[W] CONST->en->OutOfRange");
                    }
                }
                return result;
            }

            internal static string mrb_sym2name(string symbol)
            {
                // https://github.com/mruby/mruby/blob/master/src/symbol.c#L432 -> too hard -> fake
                try
                {
                    var syms = symbol.Split('_');
                    return syms[syms.Length - 1];
                }
                catch
                {
                    return string.Empty;
                }
            }
#endif

            /// <summary>
            /// Đổi toàn bộ text trong record sang thành text mới.
            /// </summary>
            /// <param name="lines"></param>
            /// <param name="part1Size">size of first part, for calcute padding</param>
            /// <returns></returns>
            public byte[] ReplaceLines(IEnumerable<Line> lines, int part1Size)
            {
                foreach (var line in lines)
                {
                    var index = int.Parse(line.ID.Split('_')[0]);
                    CONST[index] = line.English/*.Replace("\r\n", "\n")*/; // Correct
                }

                return RecordToBinary(part1Size);
            }

            /// <summary>
            /// Compile record sang binary
            /// </summary>
            /// <param name="part1Size">size block trước đó, đùng để align</param>
            /// <returns></returns>
            private byte[] RecordToBinary(int part1Size)
            {
                using (var ms = new MemoryStream(10485760)) // 10MB
                using (var bw = new EndianBinaryWriter(ms, Endian.BigEndian))
                {
                    /* write meta */
                    bw.Write(section_size);
                    bw.Write(nlocals);
                    bw.Write(nregs);
                    bw.Write(rlen);

                    /* write bytecode */
                    bw.Write(ByteCodes.Length);
                    var pad = AlignmentHelper.GetAlignedDifference(bw.BaseStream.Position + part1Size, 4);
                    if (pad > 0) bw.Write(new byte[pad]); //  contain text <=> contain byteCode => need check padding

                    foreach (var bytecode in ByteCodes)
                        bw.Write(bytecode);

                    /* write const */
                    bw.Write(CONST.Length);
                    foreach (var c0nst in CONST)
                    {
                        bw.Write((byte)0); // Type: String - luôn như vậy, 1 loại, chưa thấy trường hợp xà bần.
                        bw.WriteStringPrefixedLength16(c0nst, Encoding); // Encoding.UTF8
                        // not contain null-terminated
                    }

                    /* write symbol */
                    bw.Write(Symbols.Length);
                    foreach (var symbol in Symbols)
                    {
                        if (symbol == string.Empty)
                        {
                            bw.Write((short)-1);
                        }
                        else
                        {
                            bw.WriteStringPrefixedLength16(symbol, Encoding.UTF8);
                            bw.Write((byte)0); // null-terminated
                        }

                    }

                    /* fix size in top */
                    bw.BaseStream.Position = 0;
                    var newSize = (int)(bw.BaseStream.Length + 4 - pad); // Tested!
                    bw.Write(newSize);

                    return ms.ToArray();
                }
            }
        }
    }
}

using BufLib.Common.IO;
using ExR.Format;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace BufLib.TextFormats.BinaryModels.NieRAutomata
{
    internal static partial class MCD
    {
        public static List<Line> ExtractText(byte[] data)
        {
            using (var br = new EndianBinaryReader(new MemoryStream(data)))
                return ExtractText(br);
        }

        public static List<Line> ExtractText(EndianBinaryReader br)
        {
            var header = br.ReadStruct<Header>();

            /* read dictionary */
            br.BaseStream.Position = header.offset_symbol_codes;
            var codes = br.ReadStructs<Code>(header.count_symbol_codes); // index -> char

            /* collect offset of string & ref of offset */
            var lineInfoDs = new List<StringSectionD>(); // lưu offset những câu text cần decode

            /* đọc mảng section A */
            br.BaseStream.Position = header.offset_string_table;
            var secAs = br.ReadStructs<StringSectionA>(header.count_string_table);

            // với từng sectionA, duyệt các section bên trong.
            // phải duyệt tuẩn tự do không biết A có tất cả bao nhiêu section B
            foreach (var secA in secAs)
            {
                /* đọc số section có trong section A -> mảng section B. */
                br.BaseStream.Position = secA.offset;
                var secBs = br.ReadStructs<StringSectionB>(secA.count);

                // với từng sectionB, duyệt các section bên trong.
                foreach (var secB in secBs)
                {
                    br.BaseStream.Position = secB.offset;
                    var secCs = br.ReadStructs<StringSectionC>(secB.count);
                    int i = 0; // line num
                    foreach (var secC in secCs)
                    {
                        if (secC.offset == -1059061760)
                        {
                            // do nothing?
                        }
                        else
                        {
                            lineInfoDs.Add(new StringSectionD(secC.offset, secC.be_length, secB.offset + i * StringSectionC.Size, secB.fontid)); // TODO: collect what ?
                        }
                        i++;
                    }
                }
            }

            /* re-ordered pointer */
            lineInfoDs.Sort((a, b) => a.pointer.CompareTo(b.pointer));

            // Test
            //for (int i = 0; i < lineInfoDs.Count - 1; i++)
            //{
            //    var next = lineInfoDs[i].size * 2 + lineInfoDs[i].pointer; // 1 field = 2byte (index or space or 0x8000)
            //    if (next != lineInfoDs[i + 1].pointer)
            //        Console.WriteLine("S:Wrong!");
            //}

            /* decode all string */
            var result = new List<Line>();

            foreach (var lineInfoD in lineInfoDs)
            {
                //Console.WriteLine(lineInfoD.pointer + "_" + lineInfoD.size);
                if (lineInfoD.size <= 0)
                {
                    continue;
                }
                else if (lineInfoD.pointer < 0)
                {
                    continue; // TODO: why pointer < 0 ?
                }

                if (lineInfoD.size % 2 != 1)
                    Console.WriteLine("Wrong! Size");

                br.BaseStream.Position = lineInfoD.pointer;
                // size luôn không chia hết cho 2, dư 1 ký tự terminated (đọc để test)
                // mỗi ký tự dùng 4 byte để lưu.
                // terminated chỉ có 2 byte.
                var chars = br.ReadStructs<EncodedChar>(lineInfoD.size / 2 + 1);
                var s = Decode(chars, codes);
                // Console.WriteLine(s);
                result.Add(new Line(lineInfoD.offsetOffPointer + "|" + lineInfoD.fontId, s));
            }

            return result;
        }

        private static string Decode(EncodedChar[] chars, Code[] codes)
        {
            var sb = new StringBuilder();

            foreach (var charidx in chars)
            {
                var idx = charidx.Index;
                // var unk = charidx.Unk;

                if (idx == 0x8001)
                {
                    // code của dấu cách
                    sb.Append(' ');
                }
                else if (idx == 0x8000)
                {
                    // terminated
                    //sb.Append('\0');
                }
                else if (idx == 0x8003)
                {
                    sb.Append("($8003)");
                }
                else if (idx == 0x8020)
                {
                    sb.Append("($8020)");
                }
                else
                {
                    sb.Append(codes[idx].ct_char);
                }
            }

            return sb.ToString();
        }
    }
}

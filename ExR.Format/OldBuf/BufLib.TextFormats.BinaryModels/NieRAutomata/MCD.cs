/*
// MODE - Re-import
// Encoding: UTF16 -> index table

Xử lý từ điển - hoặc lập từ điển mới
- Đọc table có sẵn(code_table) -> lập tự điển có sẵn.
- Thêm ký tự việt thiếu vào cuối
  + ct_fontid = ? (vd: 2, 36, 37: cần biết câu text hiện tại thuộc số bao nhiêu để ghi ký tự - string_entry.se_fontid)
  + cr_char   = ký tự việt(UTF16)
  + ct_code   = tăng dần

- Thêm symbol việt(symbol_table)
  + textid         = tương tự những symbol khác
  + i1, i2, i3, i4 = ?
  + w, h           = ?
  + z1, z2, z3     = tương tự những symbol khác.
=> ghi code_table + symbol_table mới và cập nhật các pointer + count trên header.

encode

 */

// #define DISABLE

using BufLib.Common.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ExR.Format;

namespace BufLib.TextFormats.BinaryModels.NieRAutomata
{
    internal static partial class MCD
    {
        public static Encoding Encoding = null; // Encoding.Unicode
        static Encoding controlDecoder = new StandardEncoding("", Encoding.Unicode); // decode ($800X)

        public static byte[] RepackText(List<Line> lines, byte[] oldMCD)
        {
#if DISABLE
            return new byte[0];
#else
            // TODO: symbols phải tạo từ font.
            var codes = CreateCodeTable(lines);
            var symbols = CreateSymbolTable(codes, oldMCD);

            using (var ms = new MemoryStream(oldMCD.Length * 2))
            using (var bw = new EndianBinaryWriter(ms))
            using (var msr = new MemoryStream(oldMCD))
            using (var br = new EndianBinaryReader(msr))
            {
#if BRIDGE_DOTNET
                var header = new Header();
                header.offset_string_table = br.ReadInt32();
                header.count_string_table = br.ReadInt32();
                header.offset_symbol_codes = br.ReadInt32();
                header.count_symbol_codes = br.ReadInt32();
                header.offset_symbol_table = br.ReadInt32();
                header.count_symbol_table = br.ReadInt32();
                header.offset_fonts_table = br.ReadInt32();
                header.count_fonts_table = br.ReadInt32();
                header.offset_unk2 = br.ReadInt32();
                header.count_unk2 = br.ReadInt32();
#else
                var header = br.ReadStruct<Header>();
#endif

                // lấy phần đầu của mcd
                br.BaseStream.Position = 0;
                var payloadBegin = br.ReadBytes(header.offset_symbol_codes);

                // lấy phần font_table
                br.BaseStream.Position = header.offset_fonts_table;
                var sizePayloadFontTable = header.count_fonts_table * 0x14 + 4; // 4 byte ZERO; fontable.size = 0x14
                var payloadFontTable = br.ReadBytes(sizePayloadFontTable);

                // lấy phần unk2 (phần cuối)
                var sizePayloadUnk2 = header.count_unk2 * 0x28; // chap.size = 0x28
                var payloadUnk2 = br.ReadBytes(sizePayloadUnk2);

                /* ghi payload đầu vào file mới */
                bw.Write(payloadBegin);

                /* ghi code table mới */
                // cần replace ký tự -> ghi từng code
                foreach (var code in codes)
                {
                    bw.Write(code.ct_fontid);
                    bw.Write(Encoding.GetBytes(code.ct_char.ToString()));
                    bw.Write(code.ct_code);
                }
                bw.Write(0);

                /* ghi symbol table mới */
                var offsetSymbols = (int)ms.Position;
#if BRIDGE_DOTNET
                foreach (var symbol in symbols)
                {
                    bw.Write(symbol.texid);
                    bw.Write(symbol.u1);
                    bw.Write(symbol.u2);
                    bw.Write(symbol.v1);
                    bw.Write(symbol.v2);
                    bw.Write(symbol.w);
                    bw.Write(symbol.h);

                    bw.Write(symbol.z1);
                    bw.Write(symbol.z2);
                    bw.Write(symbol.z3);
                }
#else
                bw.WriteStructs(symbols);
#endif
                bw.Write(0);

                /* ghi payload fonts + unk2s */
                var offsetFonts = (int)ms.Position; // lưu lại pointer
                bw.Write(payloadFontTable);

                var offsetUnk2 = (int)ms.Position; // lưu lại pointer
                bw.Write(payloadUnk2);

                bw.Align(0x10); // safe: fake EOF

                /* chèn text data vào cuối mcd & update các pointer */
                foreach (var line in lines)
                {
                    var infos = line.ID.Split('|');
                    var offset = int.Parse(infos[0]);
                    var fontid = infos[1];
                    var pointer = (int)bw.BaseStream.Position;

                    var encoded = Encode(line.English, int.Parse(fontid), codes);
                    bw.Write(encoded);

                    var end = bw.BaseStream.Position;

                    // ghi text pointer + size mới (trong sectionC)
                    var numChar = encoded.Length / 2; // numChar luon la so le
                    bw.BaseStream.Position = offset;
                    bw.Write(pointer);
                    bw.BaseStream.Position += 4; // skip ZERO
                    bw.Write(numChar);
                    bw.Write(numChar); // 2 lần

                    // quay về cuối
                    bw.BaseStream.Position = end;
                }
                bw.Align(0x10); // cuối file phải align

                /* Update header */
                // cập nhật số code mới (offset k đổi)
                bw.BaseStream.Position = 0xC;
                bw.Write(codes.Length);

                // cập nhật offset + số symbol mới
                bw.Write(offsetSymbols);
                bw.Write(symbols.Length);

                // cập nhật offset fonts mới
                bw.BaseStream.Position = 0x18;
                bw.Write(offsetFonts);

                bw.BaseStream.Position = 0x20;
                bw.Write(offsetUnk2);

                return ms.ToArray();
            }
#endif
        }

#if !DISABLE
        /// <summary>
        /// Tạo code table từ các ký tự.
        /// </summary>
        /// <param name="lines"></param>
        /// <returns></returns>
        static Code[] CreateCodeTable(List<Line> lines)
        {
            var dics = new Dictionary<short, Dictionary<char, int>>(); // todo: int -> symbol

            foreach (var line in lines)
            {
                // lấy fontId từ Id của line.
                var lineInfos = line.ID.Split('|');
                var fontId = short.Parse(lineInfos[1]);

                // kiểm tra và cập nhật từ điển.
                var raws = controlDecoder.GetBytes(line.English); // decode control
                line.English = Encoding.Unicode.GetString(raws); //  dùng unicode chuẩn để encode
                foreach (var c in line.English)
                {
                    if (dics.ContainsKey(fontId) == false)
                    {
                        dics.Add(fontId, new Dictionary<char, int>());
                    }

                    // không gồm ký tự space
                    if(c != ' ')
                    {
                        dics[fontId][c] = 0; // thêm vào dic, không cần kiểm tra tồn tại.
                    }
                }
            }

            // dict to list
            var codes = new List<Code>();
            foreach (var font in dics)
            {
                var font_id = font.Key;
                foreach (var code in font.Value)
                {
                    var code_ = new Code();
                    code_.ct_char = code.Key;
                    code_.ct_fontid = font_id;
                    code_.ct_code = 0; // id

                    codes.Add(code_);
                }
            }

            // - font_id xep tang dan
            // - ky tu xep tang dan theo tung font_id
            codes = codes
                .OrderBy(x => x.ct_fontid)
                .ThenBy(x => x.ct_char)
                .ToList();

            // đánh thứ tự id
            for (int i = 0; i < codes.Count; i++)
            {
                var code = codes[i];
                code.ct_code = i;
                codes[i] = code;
            }

            return codes.ToArray();
        }

        /// <summary>
        /// Tạo symbol table mới
        /// </summary>
        /// <param name="codes">code table mới</param>
        /// <param name="oldMCD">cần mcd gốc để lấy code table gốc và symbol table gốc</param>
        /// <returns></returns>
        static Symbol[] CreateSymbolTable(Code[] codes, byte[] oldMCD)
        {
            // cần code table gốc và symbol table gốc
            // chèn thêm symbol mới
            //  - Chữ ấ sẽ có cùng thông tin chữ a
            //  - nếu không có chữ a -> dùng chữ khác (ngẫu nhiên).
            //  - SAI => cần tạo symbol table từ file glyph.

            // 1. lấy code table & symbol table gốc.
            using (var ms = new MemoryStream(oldMCD))
            using (var br = new EndianBinaryReader(ms, Encoding.Unicode))
            {
                br.BaseStream.Position = 8;
                var offset_symbol_codes = br.ReadInt32();
                var count_symbol_codes = br.ReadInt32();
                var offset_symbol_table = br.ReadInt32();
                var count_symbol_table = br.ReadInt32();

                // read codes
                br.BaseStream.Position = offset_symbol_codes;
#if BRIDGE_DOTNET
                var oldCodes = new Code[count_symbol_codes];
                for (int i = 0; i < count_symbol_codes; i++)
                {
                    var oldCode = new Code();
                    oldCode.ct_fontid = br.ReadInt16();
                    oldCode.ct_char = br.ReadChar();
                    oldCode.ct_code = br.ReadInt32();

                    oldCodes[i] = oldCode;
                }
#else
                var oldCodes = br.ReadStructs<Code>(count_symbol_codes);
#endif
                // read symbols
                br.BaseStream.Position = offset_symbol_table;
#if BRIDGE_DOTNET
                var oldSymbols = new Symbol[count_symbol_table];
                for (int i = 0; i < count_symbol_codes; i++)
                {
                    var oldSymbol = new Symbol();
                    oldSymbol.texid = br.ReadInt32();
                    oldSymbol.u1 = br.ReadSingle();
                    oldSymbol.u2 = br.ReadSingle();
                    oldSymbol.v1 = br.ReadSingle();
                    oldSymbol.v2 = br.ReadSingle();
                    oldSymbol.w = br.ReadSingle();
                    oldSymbol.h = br.ReadSingle();
                    oldSymbol.z1 = br.ReadSingle();
                    oldSymbol.z2 = br.ReadSingle();
                    oldSymbol.z3 = br.ReadSingle();

                    oldSymbols[i] = oldSymbol;
                }
#else
                var oldSymbols = br.ReadStructs<Symbol>(count_symbol_codes);
#endif
                // tạo từ điển để loockup nhanh
                var dicts = new Dictionary<short, Dictionary<char, Symbol>>();
                for (int i = 0; i < count_symbol_codes; i++)
                {
                    var oldCode = oldCodes[i];
                    var oldSymbol = oldSymbols[i];
                    if (dicts.ContainsKey(oldCode.ct_fontid) == false)
                    {
                        dicts.Add(oldCode.ct_fontid, new Dictionary<char, Symbol>());
                    }

                    dicts[oldCode.ct_fontid][oldCode.ct_char] = oldSymbol;
                }

                // 2. build symbol table mới (số symbol = số code).
                var symbols = new Symbol[codes.Length];
                var rand = new Random();
                for (int i = 0; i < codes.Length; i++)
                {
                    var code = codes[i];
                    var dict = dicts[code.ct_fontid];

                    // tìm trong table gốc
                    var c = code.ct_char.XoaDauTiengViet();
                    if (dict.TryGetValue(c, out var symbol))
                    {
                        symbols[i] = symbol;
                    }
                    else
                    {
                        // không thấy -> thử up or low
                        if(char.IsLower(c))
                        {
                            c = char.ToUpper(c);

                        }
                        else
                        {
                            c = char.ToLower(c);
                        }

                        if (dict.TryGetValue(c, out symbol))
                        {
                            symbols[i] = symbol;
                        }
                        else
                        {
                            // vẫn k có? -> lấy ngẫu nhiên một symbol nào đó
                            var _dict = dict.ToArray();
                            var index = rand.Next(0, _dict.Length - 1);
                            var randSymbol = _dict[index].Value;
                            symbols[i] = randSymbol;

                            dict[c] = randSymbol; // thêm vào từ điển cho đồng bộ
                        }
                    }
                }

                return symbols;
            }
        }

        private static byte[] Encode(string s, int fontId, Code[] codes)
        {
            var result = new List<EncodedChar>();
            foreach (var c in s)
            {
                var index = GetIndex(c, fontId, codes);
                var encodedChar = new EncodedChar()
                {
                    Index = index,
                    // nếu là khoảng cách -> theo font
                    // nếu là ký tự -> thì random?
                    Spacing = index == 0x8001 ? (short)fontId : (short)random1.Next(-1, 0) // (tested: letter spacing)
                };

                result.Add(encodedChar);
            }
            return EncodedCharToBytes(result);
        }
        static Random random1 = new Random();

        private static byte[] EncodedCharToBytes(List<EncodedChar> encodedChars)
        {
            using (var ms = new MemoryStream(encodedChars.Count * 4 + 2))
            using (var bw = new BinaryWriter(ms))
            {
                foreach (var encodedChar in encodedChars)
                {
                    bw.Write(encodedChar.Index);
                    bw.Write(encodedChar.Spacing);
                }

                // add terminated
                bw.Write((ushort)0x8000);

                return ms.ToArray();
            }
        }

        private static ushort GetIndex(char c, int fontId, Code[] codes)
        {
            if (c == ' ')
                return 0x8001;
            //if (c == '|') // ($8020) // đã decode bằng controlDecoder
            //    return 0x8020;

            foreach (var code in codes)
            {
                if (code.ct_fontid == fontId &&
                    code.ct_char == c)
                {
                    return (ushort)code.ct_code;
                }
            }

            throw new Exception("MCD.GetIndex: font=" + fontId + " char=" + c);
        }
        //*/
#endif
    }
}

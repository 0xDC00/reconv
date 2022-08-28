using BufLib.Common.Compression;
using BufLib.Common.IO;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Zio;

namespace ExR.Format
{
    [Plugin("com.dc.vnPJAdv", "VN PJAdv Engine (scenario.dat, textdata.bin)", @"
Unpack\Repack
_init_.yaml (optional)
---
table: |-
  A=a
  b=b
...

Note: 2XT-SEVEN WONDER Adventure Engine?
archive?.dat - quickbms script: http://aluigi.org/bms/paradise_cleaning.bms
00000000  47414D45 44415420 50414332 06000000  GAMEDAT PAC2....
00000010  42474D2E 696E6900 00000000 00000000  BGM.ini.........
00000020  00000000 00000000 00000000 00000000  ................
00000030  66696C65 6E616D65 2E646174 00000000  filename.dat....
")]
    public class VN_PJAdv : TextFormat
    {
        public override bool Init(Dictionary<string, object> dict)
        {
            Extensions = new string[] { ".bin" };
            dict.TryGetValue("table", out var tableStr);
            _Encoding = new StandardEncoding((string)tableStr, Encoding.GetEncoding(932));

            return true;
        }

        public override List<Line> ExtractText(byte[] buf)
        {
            var dictOffsetLine = new Dictionary<long, LineX>();
            Crypt(buf);
            string headerStr;
            string numLineStr;
            using (var ms = new MemoryStream(buf))
            using (var br = new EndianBinaryReader(ms))
            {
                var headerRaw = br.ReadBytes(0xC);
                var numLine = br.ReadInt32();
                headerStr = Encoding.UTF8.GetString(headerRaw);
                numLineStr = numLine.ToString();
                //var result = new List<string>(numLine);
                for (int i = 0; i < numLine; i++)
                {
                    var offset = br.BaseStream.Position;
                    var str = ReadString(br);
                    //result.Add(str);

                    dictOffsetLine.Add(offset, new LineX()
                    {
                        Value = str,
                        Offsets = new List<long>(),
                        SceTextIndex = -1,
                        SceLineIndex = -1,
                        TextDataIndex = i,
                    });
                }

                //result.Insert(0, headerStr);
                //result.Insert(0, numLine.ToString());
            }

            var targetIndex = Path.Combine(Path.GetDirectoryName(CurrentFilePath), "scenario.dat");
            var sce = FsIn.ReadAllBytes(targetIndex);
            var decom = ReadIdx(targetIndex, dictOffsetLine);
            decom = decom.OrderBy(x => x.SceTextIndex).ToList();
            //var lst1 = dictOffsetLine.OrderBy(x => x.Value.SceTextIndex).ToList();



            var result = new List<Line>();

            // var lst1 = dictOffsetLine.ToList(); // need decom
            //foreach (var item in lst1)
            //{
            //    var val = item.Value;
            //    var id = $"{val.TextDataIndex}_{JsonExtensions.ToJson(val.Offsets)}_{val.Name}";
            //    var line = val.Value.Replace("\\n", "\n");

            //    result.Add(new Line()
            //    {
            //        Id = id,
            //        English = line
            //    });
            //}
            foreach (var val in decom)
            {
                var id = $"{val.TextDataIndex}_{JsonExtensions.ToJson(val.Offsets)}_{val.Name}";
                var line = val.Value.Replace("\\n", "\n");
                result.Add(new Line()
                {
                    ID = id,
                    English = line
                });
            }

            
            result.Insert(0, new Line()
            {
                ID = headerStr
            });

            var payload = CompressionHelper.ZlibCompress(sce, CompressionLevel.Optimal);
            PushPayloadCells(result, payload);

            return result;
        }

        public override byte[] RepackText(List<Line> lines)
        {
            //var lines = File.ReadAllLines(path);
            //var header = lines[1];
            //var numLine = int.Parse(lines[0]);
            //var _lines = lines.Skip(2);
            //var enc = Encoding.GetEncoding(932);
            ////enc = Encoding.UTF8;
            //using (var ms = new MemoryStream())
            //using (var bw = new BinaryWriter(ms))
            //{
            //    var headerRaw = Encoding.UTF8.GetBytes(header);
            //    bw.Write(headerRaw);
            //    bw.Write(numLine);
            //    foreach (var line in _lines)
            //    {
            //        WriteString(bw, enc, line);
            //    }
            //    var buf = ms.ToArray();
            //    Crypt(buf);
            //    File.WriteAllBytes(Path.ChangeExtension(path, ".bin.bin"), buf);
            //}

            var payload = CompressionHelper.ZlibUncompress(PopPayloadCells(lines));
            //FsOut.WriteAllBytes("/scenario_.dat", payload);
            var headerStr = lines[0].ID;
            lines = lines.Skip(1).OrderBy(x => int.Parse(x.ID.Split('_')[0])).ToList();

            byte[] buf = null;
            using (var ms = new MemoryStream(_10MB))
            using (var bw = new BinaryWriter(ms))
            using (var ms2 = new MemoryStream(payload))
            using (var bwIdx = new BinaryWriter(ms2))
            {
                var headerRaw = Encoding.UTF8.GetBytes(headerStr);
                bw.Write(headerRaw);
                bw.Write(lines.Count);
                foreach (var line in lines)
                {
                    var lineOffset = (int)bw.BaseStream.Position;
                    var l = line.English
                        .Replace("\r\n", "\\n")
                        .Replace("\n", "\\n");
                    WriteString(bw, l);

                    var ids = line.ID.Split('_');
                    var offsets = JsonExtensions.FromJson<int[]>(ids[1]);
                    foreach (var offset in offsets)
                    {
                        bwIdx.BaseStream.Position = offset;
                        bwIdx.Write(lineOffset);
                    }
                }
                buf = ms.ToArray();
                Crypt(buf);
            }

            ////Console.WriteLine($"OutPath: '{OutPath}'"); // empty
            ////Console.WriteLine($"InPath: '{InPath}'"); // empty
            ////Console.WriteLine($"CurrentFilePath: '{CurrentFilePath}'"); // ||textdata.bin
            //var targetIndex = Path.Combine(Path.GetDirectoryName(CurrentFilePath), "scenario.dat");
            //File.WriteAllBytes(targetIndex, payload);

            //this.WriteDataToCurrentDir("scenario.dat", payload);
            FsOut.WriteAllBytes("/scenario.dat", payload);

            return buf;
        }

        List<LineX> ReadIdx(string path, Dictionary<long, LineX> dictOffsetLine)
        {
            // https://github.com/Doddler/PrismArkTools/blob/master/PrismScriptSplit/functiondef.txt
            // https://github.com/Inori/FuckGalEngine/blob/master/PJADV/text_out.py
            var result = new List<LineX>();
            var sce = FsIn.ReadAllBytes(path);
            using (var ms = new MemoryStream(sce))
            using (var br = new BinaryReader(ms))
            {
                br.BaseStream.Position = 0x20;
                var index = 0;
                while (br.BaseStream.Position < br.BaseStream.Length)
                {
                    var block = br.ReadUInt32(); // COOO
                    //var count = block >> 24; // BE
                    var count = block & 0xFF; // numparam
                    var opcode = block >> 8;
                    //Console.WriteLine(opcode.ToString("X"));
                    switch (opcode)
                    {
                        // text
                        case 0x800003: // 0x80000307	fssiii
                        case 0x800004: // 0x80000406	issii
                            br.BaseStream.Position += 4;
                            var nameOffset = br.BaseStream.Position;
                            var nameOffsetValue = br.ReadInt32();
                            var textOffset = br.BaseStream.Position;
                            var textOffsetValue = br.ReadInt32();

                            var lineIndex = br.ReadInt32();

                            if (nameOffsetValue >= 0x10)
                            {
                                dictOffsetLine[nameOffsetValue].Offsets.Add(nameOffset);
                                if (dictOffsetLine[nameOffsetValue].SceLineIndex == -1)
                                {
                                    dictOffsetLine[nameOffsetValue].SceLineIndex = lineIndex;
                                    dictOffsetLine[nameOffsetValue].SceTextIndex = -2; // index
                                    dictOffsetLine[nameOffsetValue].Type = -1;
                                    result.Add(dictOffsetLine[nameOffsetValue]); // collect one (ref)

                                    // move to top
                                    //dictOffsetLine[nameOffsetValue].TextDataIndex = -1;
                                }

                                // set name to line
                                var splitedName = dictOffsetLine[nameOffsetValue].Value.Split('＠');
                                dictOffsetLine[textOffsetValue].Name = splitedName.Length > 1 ? splitedName[1] : splitedName[0];
                            }

                            dictOffsetLine[textOffsetValue].Offsets.Add(textOffset);
                            dictOffsetLine[textOffsetValue].SceLineIndex = lineIndex;
                            dictOffsetLine[textOffsetValue].SceTextIndex = index;
                            var txtLine = dictOffsetLine[textOffsetValue];
                            result.Add(new LineX()
                            {
                                Name = txtLine.Name,
                                Value = txtLine.Value,
                                Offsets = new List<long>() { textOffset },
                                SceLineIndex = txtLine.SceLineIndex,
                                SceTextIndex = txtLine.SceTextIndex,
                                TextDataIndex = txtLine.TextDataIndex,
                                Type = txtLine.Type
                            }); // collect line (clone)

                            count -= 4 + 1; // skip4 & block & nameOffsetValue & textOffsetValue
                            index++;
                            break;

                        // choice
                        case 0x010108:
                        case 0x810101:
                        case 0x010102: // 0x01010203	sl
                        case 0x01000D: // 0x01000d02	s    scenename
                            var optionOffset = br.BaseStream.Position;
                            var optionOffsetValue = br.ReadInt32();

                            dictOffsetLine[optionOffsetValue].Offsets.Add(optionOffset);
                            dictOffsetLine[optionOffsetValue].SceTextIndex = index;

                            var txtPromt1 = dictOffsetLine[optionOffsetValue];
                            result.Add(new LineX()
                            {
                                Name = txtPromt1.Name,
                                Value = txtPromt1.Value,
                                Offsets = new List<long>() { optionOffset },
                                SceLineIndex = txtPromt1.SceLineIndex,
                                SceTextIndex = txtPromt1.SceTextIndex,
                                TextDataIndex = txtPromt1.TextDataIndex,
                                Type = txtPromt1.Type
                            }); // collect option (clone)

                            count -= 2;
                            index++;
                            break;

                        case 0x030003: // Op03000303			0x03000303  is		//unknown... seems to delete something?
                        case 0x010030:
                        case 0x040413:
                            br.BaseStream.Position += 4;
                            var optionPromptOffset = br.BaseStream.Position;
                            var optionPromptOffsetValue = br.ReadInt32();

                            dictOffsetLine[optionPromptOffsetValue].Offsets.Add(optionPromptOffset);
                            dictOffsetLine[optionPromptOffsetValue].SceTextIndex = index;

                            var txtPromt2 = dictOffsetLine[optionPromptOffsetValue];
                            result.Add(new LineX()
                            {
                                Name = txtPromt2.Name,
                                Value = txtPromt2.Value,
                                Offsets = new List<long>() { optionPromptOffset },
                                SceLineIndex = txtPromt2.SceLineIndex,
                                SceTextIndex = txtPromt2.SceTextIndex,
                                TextDataIndex = txtPromt2.TextDataIndex,
                                Type = txtPromt2.Type
                            }); // collect promt (clone)

                            count -= 3;
                            index++;
                            break;
                        default:
                            count -= 1;
                            break;
                    }

                    br.BaseStream.Position += count * 4;
                }
            }

            return result;
        }

        string ReadString(BinaryReader br)
        {
            var pos = br.BaseStream.Position;
            var len = 0;
            while (br.ReadByte() != 0)
            {
                len++;
            }

            br.BaseStream.Position = pos;
            var result = _Encoding.GetString(br.ReadBytes(len));
            br.BaseStream.Position += 2;
            return result;
        }

        void WriteString(BinaryWriter bw, string s)
        {
            var raw = _Encoding.GetBytes(s);
            bw.Write(raw);
            bw.Write((short)0);
        }

        class LineX
        {
            public string Value { get; set; }
            public string Name { get; set; }
            public List<long> Offsets { get; set; }
            /// <summary>
            /// Theo thứ tự opcode đọc lên
            /// </summary>
            public int SceTextIndex { get; set; }
            /// <summary>
            /// Theo số thứ tự trong opcode
            /// </summary>
            public int SceLineIndex { get; set; }
            public int TextDataIndex { get; set; }
            /// <summary>
            /// -1 = Name
            /// </summary>
            public int Type { get; set; }

            public override string ToString()
            {
                if (string.IsNullOrEmpty(Name))
                    return Value;

                return Name + "___" + Value;
            }
        }

        static void Crypt(byte[] buf)
        {
            byte key = 0xC5; // Key: C5 197
            for (int i = 0; i < buf.Length; i++)
            {
                buf[i] ^= key;
                key += 0x5C; // Seek: 5C 82
            }
        }

        internal static void PushPayloadCells(List<Line> lines, byte[] data)
        {
            // Sheets 50,000 characters | Excel: 32,767 characters
            // https://support.office.com/en-us/article/excel-specifications-and-limits-1672b34d-7043-467e-8e27-269d656771c3#ID0EBABAAA=Newer_versions
            var payload = System.Convert.ToBase64String(CompressionHelper.ZlibCompress(data, CompressionLevel.Optimal));
            var payloads = payload.Split(32747); // 49984 | 32747
            int i = 0;
            foreach (var item in payloads)
            {
                lines.Insert(i, new Line(item, string.Empty));
                i++;
            }
            lines.Insert(0, new Line(i, string.Empty));
        }

        internal static byte[] PopPayloadCells(List<Line> lines)
        {
            var base64 = new StringBuilder();

            var numChunk = int.Parse(lines[0].ID);
            lines.RemoveAt(0);

            for (int i = 0; i < numChunk; i++)
            {
                base64.Append(lines[0].ID);
                lines.RemoveAt(0);
            }
            var zlibraw = System.Convert.FromBase64String(base64.ToString());
            return CompressionHelper.ZlibUncompress(zlibraw);
        }
    }

    
}

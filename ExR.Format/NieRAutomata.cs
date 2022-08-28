// https://github.com/micktu/att

using BufLib.Common.IO;
using BufLib.TextFormats.BinaryModels.NieRAutomata;
using BufLib.TextFormats.DataModels;
using System;
using System.Text;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ExR.Format
{
    [Plugin("com.dc.nierAutomata", "NieR:Automata™ Steam (.dat, .bin, .mcd, .tmd, .smd)", @"
Extract/ReImport
_init_.yaml (optional)
---
table:
  á=a
  à=a
...
")]
    class NieRAutomata : TextFormat
    {
        public override bool Init(Dictionary<string, object> dict)
        {
            dict.TryGetValue("table", out var _tbl);
            var tbl = (string)_tbl;
            // có vài code tiếng việt game không decode được nên cần remap sang ký tự khác.
            var utf16 = new StandardEncoding(tbl, Encoding.Unicode);
            //var utf16 = Encoding.Unicode; // unicode không bị lỗi xử lý tiếng Việt nên k cần custom table (khi và chi khi file bin dùng font riêng (k share với các file còn lại -> k thỏa)
            SMD.Encoding = utf16;
            TMD.Encoding = utf16;
            MCD.Encoding = new StandardEncoding("", Encoding.Unicode); ; // không việt hóa font -> không custom table.

            _Encoding = new StandardEncoding(tbl, Encoding.UTF8);
            BIN.IREP_RECORD.Encoding = _Encoding;

            return true;
        }

        public override List<Line> ExtractText(byte[] buf)
        {
            using (var ms = new MemoryStream(buf))
            using (var br = new EndianBinaryReader(ms))
            {
                var magic = BitConverter.ToInt32(buf);
                List<Line> result = null;
                switch (magic)
                {
                    case 0x45544952:
                        result = BIN.ExtractText(br);
                        //if (result.Count > 0)
                        //{
                        //    // too many file  -> test all.
                        //    var oldBin = ReadCurrentFileData();
                        //    var newBin = BIN.RepackText(result, oldBin);
                        //    if (oldBin.SequenceEqual(newBin) == false)
                        //        Console.WriteLine("[W] BIN repack fail");
                        //}
                        break;
                    case 0x544144:
                        result = DAT.ExtractText(br);
                        //if (result.Count > 0)
                        //{
                        //    var oldDat = ReadCurrentFileData();
                        //    var newDat = DAT.RepackText(result, oldDat);
                        //    if (oldDat.SequenceEqual(newDat) == false)
                        //        Console.WriteLine("[W] DAT repack fail");
                        //}
                        break;
                    default:
                        var ext = Path.GetExtension(CurrentFilePath).ToLower();
                        if (ext == ".mcd")
                        {
                            result = MCD.ExtractText(br);
                            if (result.Count == 0)
                            {
                                Console.WriteLine("MCD->NotYet");
                            }
                        }
                        break;
                }

                return result;
            }
        }

        public override byte[] RepackText(List<Line> lines)
        {
            var rawFile = ReadCurrentFileData();
            var magic = BitConverter.ToInt32(rawFile, 0);

            byte[] result = null;
            switch (magic)
            {
                case 0x45544952:
                    result = BIN.RepackText(lines, rawFile);
                    break;
                case 0x00544144:
                    result = DAT.RepackText(lines, rawFile);
                    break;
                default:
                    var ext = Path.GetExtension(CurrentFilePath).ToLower();
                    if (ext == ".mcd")
                    {
                        result = MCD.RepackText(lines, rawFile);
                    }
                    break;
            }

            return result;
        }
    }
}

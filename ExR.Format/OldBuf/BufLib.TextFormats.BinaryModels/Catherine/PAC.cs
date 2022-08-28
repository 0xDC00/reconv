// Archive
// Mode: Re-Import

using BufLib.Common.IO;
using BufLib.TextFormats.DataModels;
using ExR.Format;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace BufLib.TextFormats.BinaryModels.Catherine
{
    internal static partial class PAC
    {
        public static byte[] RepackText(List<Line> lines, byte[] oldPAC)
        {
            var info = lines[0].ID.Split('|');
            if (info[0] != string.Empty)
                throw new Exception("Only replace main directory (1 lv)");

            var index = int.Parse(info[2]);
            var numLine = int.Parse(info[3]);
            var ext = Path.GetExtension(info[1]).ToLower();
            Console.WriteLine("> " + info[1]);

            // var currentLines = lines.Skip(1).Take(numLine).ToList();
            //lines = lines.Skip(1 + numLine).ToList(); // remainLine, case: many in one
            var currentLines = lines.GetRange(1, numLine);            
            lines = lines.GetRange(1 + numLine, lines.Count - numLine - 1);


            byte[] result = null;
            switch (ext)
            {
                case ".bmd":
                    result = BMD.RepackText(currentLines);
                    break;
                case ".bf":
                    result = BF.RepackText(currentLines);
                    break;
            }

            // nếu repack thành công thì replace file trong block
            if (result != null && result.Length > 0)
                oldPAC = ReplaceFile(oldPAC, result, index);
            else
                Console.WriteLine("  Error");

            // Nếu còn text chưa repack thì tiếp tục repack
            if (lines.Count > 0)
            {
                return RepackText(lines, oldPAC); // recursive: goi de quy lan nua
            }
            else
            {
                return oldPAC;
            }
        }

        private static byte[] ReplaceFile(byte[] dat, byte[] file, int index)
        {
            // chậm.
            // [file1]      __posBegin
            // [fileIndex]          <-- replace
            // ...          ￣posEnd
            // [fileN]

            using (var br = new EndianBinaryReader(new MemoryStream(dat)))
            {
                int i = 0;
                while (br.BaseStream.Position < br.BaseStream.Length)
                {
                    br.BaseStream.Position += 0xFC; // skip file name
                    var posBegin = br.BaseStream.Position;
                    var fileSize = br.ReadInt32();
                    br.BaseStream.Position += fileSize;
                    br.Align(0x40);
                    var posEnd = br.BaseStream.Position;

                    if (i == index)
                    {
                        using (var ms = new MemoryStream())
                        using (var bw = new EndianBinaryWriter(ms))
                        {
                            // nếu có part 1 thì ghi
                            // part 1 luôn có (nhỏ nhất = 0xFC)
                            ms.Write(dat, 0, (int)posBegin);

                            bw.Write(file.Length);
                            bw.Write(file);
                            bw.Align(0x40);

                            // nếu có part cuối.
                            var lastSize = dat.Length - (int)posEnd;
                            if (lastSize > 0)
                                ms.Write(dat, (int)posEnd, lastSize);

                            return ms.ToArray();
                        }
                    }

                    i++;
                }
            }

            throw new Exception("ReplaceFile");
        }
    }
}

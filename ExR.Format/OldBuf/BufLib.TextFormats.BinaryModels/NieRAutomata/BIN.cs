// MODE - Re-import
// Encoding: UTF8

using BufLib.Common.IO;
using BufLib.TextFormats.DataModels;
using ExR.Format;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace BufLib.TextFormats.BinaryModels.NieRAutomata
{
    internal static partial class BIN
    {
        public static byte[] RepackText(List<Line> lines, byte[] mcrBin)
        {
            if (lines.Count == 0)
                return mcrBin;

            using (var ms = new MemoryStream(mcrBin))
            using (var br = new EndianBinaryReader(ms, Endian.BigEndian))
            {
                /* read bin header */
#if BRIDGE_DOTNET
                var rite = new RITE(br);
                var irep_section = new IREP_SECTION(br);
#else
                var rite = br.ReadStruct<RITE>();
                var irep_section = br.ReadStruct<IREP_SECTION>();
#endif

                if (rite.Magic != 0x52495445)
                {
                    throw new Exception("[Bin] Not RITE");
                }

                var IREP_RECORD = new IREP_RECORD(br);

                /* jump/read to record...*/
                var recordTextAt = int.Parse(lines[0].ID);

                // fast skip
                for (int i = 0; i < recordTextAt; i++)
                {
                    var sec_size = br.ReadInt32();
                    br.BaseStream.Position += 6;
                    var numCode = br.ReadInt32();
                    var pad = 0;
                    if(numCode > 0)
                    {
                        pad = AlignmentHelper.GetAlignedDifference(br.BaseStream.Position, 4);
                    }
                    sec_size = sec_size - 4 + pad; // realSize
                    br.BaseStream.Position -= 14;
                    br.BaseStream.Position += sec_size;
                }
                // đọc block có text (recordTextAt)
                var oldRecordOffStart = br.BaseStream.Position;
                IREP_RECORD.Read();
                var oldRecordOffEnd = br.BaseStream.Position;


                /* replace record in bin */
                // 1. split part1
                br.BaseStream.Position = 0;
                var part1 = br.ReadBytes((int)oldRecordOffStart);

                // 2. re-build part2 - replace text and compile/thay đổi text trong block vừa đọc
                var part2NewIrepRecord = IREP_RECORD.ReplaceLines(lines.Skip(1), part1.Length);

                // 3. split part3
                //br.BaseStream.Position = oldRecordOffEnd;
                //var part3 = br.ReadBytes((int)(br.BaseStream.Length - oldRecordOffEnd));

                // write all
                using (var msW = new MemoryStream(mcrBin.Length * 2))
                using (var bw = new EndianBinaryWriter(msW, Endian.BigEndian))
                {
                    // 2. Join: 1+2+3
                    bw.Write(part1);
                    bw.Write(part2NewIrepRecord);
                    //bw.Write(part3);

                    // re-write part3 (giữ nguyên tất cả, chỉ re-align và cập nhật size)
                    long wOffsetLVAR = 0;
                    br.BaseStream.Position = oldRecordOffEnd;
                    while (true)
                    {
                        var sec_size = br.ReadInt32(); // 4 = realSize + 4 (not include pad)
                        if (sec_size < 0x10000000) // nếu size ổn -> đọc RECORD
                        {
                            var n_r = br.ReadBytes(6);     // 6
                            var numCode_ilen = br.ReadInt32();  // 4 => 4+6+4 = 14
                            var wpad = 0;
                            var rpad = 0;
                            if (numCode_ilen > 0)
                            {
                                rpad = AlignmentHelper.GetAlignedDifference(br.BaseStream.Position, 4);
                                wpad = AlignmentHelper.GetAlignedDifference(bw.BaseStream.Position + 14, 4);
                                br.Align(4);
                            }

                            var sizeWithoutPad = sec_size - 4 - 14; // từ sau ilen về sau.
                            var remainData = br.ReadBytes(sizeWithoutPad);

                            sec_size = sec_size + rpad - wpad; // đổi số padding
                            bw.Write(sec_size);
                            bw.Write(n_r);
                            bw.Write(numCode_ilen);
                            bw.Align(4);
                            bw.Write(remainData);
                        }
                        else
                        {
                            br.BaseStream.Position -= 4; // pick
                            wOffsetLVAR = bw.BaseStream.Position; // LVAR of writer
                            var lvar_end = br.ReadBytes((int)(br.BaseStream.Length - br.BaseStream.Position));
                            bw.Write(lvar_end);
                            break;
                        }

                    }

                    // 3. fix sizes
                    bw.BaseStream.Position = 0x1A; // IREP_SECTION.Size offset: const=1A
                    var newSizeOfIREP_SECTION = (int)(wOffsetLVAR - RITE.Size);
                    bw.Write(newSizeOfIREP_SECTION);

                    bw.BaseStream.Position = 0xA; // RITE.FileSize offset: const=A
                    bw.Write((int)bw.BaseStream.Length);

                    // 4. calcute new crc
                    var data = new byte[(int)bw.BaseStream.Length - 0xA]; // A->End
                    bw.BaseStream.Position = 0xA;
                    bw.BaseStream.Read(data, 0, data.Length);
                    bw.BaseStream.Position = 0x8; // RITE.CRC offset: const=8
                    var newCRC = data.CRC16_CCITT();
                    bw.Write(newCRC);

                    // 5. Done
                    return msW.ToArray();
                }
            }
        }
    }
}

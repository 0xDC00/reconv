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
    internal static partial class DAT
    {
        public static List<Line> ExtractText(EndianBinaryReader br, string baseName = "")
        {
            var header = br.ReadStruct<Header>();
            var result = new List<Line>();

            if (header.FileCount == 0)
                return result;

            /* offsets */
            var offsets = br.ReadInt32s(header.FileCount);

            /* exts */
            var exts = br.ReadInt32s(header.FileCount);

            /* names */
            br.BaseStream.Position = header.NameTableOffset;
            var names = new string[header.FileCount];
            var nameLen = br.ReadInt32();
            for (int i = 0; i < names.Length; i++)
            {
                var name = br.ReadBytes(nameLen);
                names[i] = Encoding.UTF8.GetString(name).TrimEnd('\0');
            }

            /* sizes */
            br.BaseStream.Position = header.SizeTableOffset;
            var sizes = br.ReadInt32s(header.FileCount);

            /* process all files in DAT */
            int textCount = 0;
            List<Line> extracted = new List<Line>();
            for (int i = 0; i < header.FileCount; i++)
            {
                br.BaseStream.Position = offsets[i];
                var magic = br.ReadInt32(); br.BaseStream.Position -= 4; // Peak 4 byte
                var fileData = br.ReadBytes(sizes[i]);

                string name = baseName + "|" + names[i] + "|" + i.ToString();
                /* detect by magic byte */
                switch (magic)
                {
                    case 0x45544952: // RITE
                        extracted = BIN.ExtractText(fileData);
                        //if (extracted.Count > 0)
                        //{
                        //    /* test Repack */
                        //    var newBin = BIN.RepackText(extracted, fileData);
                        //    if (fileData.SequenceEqual(newBin) == false)
                        //        Console.WriteLine("[E] BIN repack fail");
                        //}
                        break;
                    case 0x544144: // DAT\0
                        extracted = DAT.ExtractText(fileData, name);
                        if (extracted.Count > 0)
                        {
                            /* no way! */
                            throw new Exception("[Dat.Extract] Dat in Dat!, No way!");
                        }
                        break;
                    default:
                        /* detect by file extention */
                        var ext = exts[i];
                        if (ext == 0x646D74) // tmd\0
                        {
                            extracted = TMD.ExtractText(fileData);
                            //if (extracted.Count > 0)
                            //{
                            //    /* test Repack */
                            //    var newTmd = TMD.RepackText(extracted);
                            //    if (fileData.SequenceEqual(newTmd) == false)
                            //        Console.WriteLine("[E] TMD repack fail");
                            //}
                        }
                        else if (ext == 0x646D73) // smd\0
                        {
                            extracted = SMD.ExtractText(fileData);
                            //if (extracted.Count > 0)
                            //{
                            //    /* test Repack */
                            //    var newSmd = SMD.RepackText(extracted, fileData);
                            //    if (fileData.SequenceEqual(newSmd) == false)
                            //        Console.WriteLine("[E] SMD repack fail");
                            //}
                        }
                        else if (ext == 0x64636D) // "mcd\0"
                        {
                            textCount++;
                            extracted = MCD.ExtractText(fileData);
                            //if (extracted.Count > 0)
                            //{
                            //    /* test Repack */
                            //    var newMCD = MCD.RepackText(extracted, fileData);
                            //    var newExtracted = MCD.ExtractText(newMCD);
                            //    for(int j=0; j<newExtracted.Count; j++)
                            //    {
                            //        if(newExtracted[j].English != extracted[j].English)
                            //        {
                            //            Console.WriteLine("[E] MCD repack fail");
                            //            break;
                            //        }
                            //    }
                            //}
                        }
                        break;
                }

                if (extracted.Count > 0)
                {
                    textCount++;
                    result.Add(new Line(name + "|" + extracted.Count, string.Empty));
                    result.AddRange(extracted);
                    extracted.Clear();
                }
            }

            // test
            //if (textCount > 1)
            //    Console.WriteLine("DAT->[many in 1] - " + textCount.ToString()); // TESTED -> only 1 file -> Wrong! -> 1 TH dac biet
            //if (result.Count > 0)
            //    Console.WriteLine(result[0].Id);  // TESTED -> only first file in dat -> Wrong!

            return result;
        }

        public static List<Line> ExtractText(byte[] data, string baseName = "")
        {
            using (var br = new EndianBinaryReader(new MemoryStream(data)))
                return ExtractText(br, baseName);
        }
    }
}

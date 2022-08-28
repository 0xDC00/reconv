#define DC_PUBLIC

#if DC_PUBLIC
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using BufLib.Common.IO;
using BufLib.TextFormats.Helper.Ubisoft;

namespace ExR.Format
{
    [Plugin("com.dc.ac2", "Assassin's Creed II+ (LocalizationPackage_English)", "Extract & Repack, No _init_.yaml needed.\n b5 89 83 d2")]
    class AssassinsCreed2 : TextFormat
    {
        static int SearchLocaz(byte[] buf)
        {
            // 0xD28389B5
            // b5 89 83 d2 [len]
            var n = buf.Length - 1;
            for (int i = 0; i < n; i++)
            {
                if (buf[i] == 0x83)
                {
                    if (buf[i + 1] == 0xD2)
                    {
                        return i + 2;
                    }
                }
            }

            return -1;
        }

        public override List<Line> ExtractText(byte[] buf)
        {
            using (var ms = new MemoryStream(buf))
            using (var br = new BinaryReader(ms))
            {
                //try
                //{
                //    var b1 = br.ReadByte();
                //    while (true)
                //    {
                //        if (b1 != 0x83)
                //        {
                //            b1 = br.ReadByte();
                //        }
                //        else
                //        {
                //            var b2 = br.ReadByte();
                //            if (b2 != 0xD2)
                //            {
                //                b1 = b2;
                //            }
                //            else
                //            {
                //                break;
                //            }
                //        }
                //    }
                //}
                //catch // not found
                //{
                //    return null;
                //}
                var off = SearchLocaz(buf);
                if (off != -1)
                {
                    ms.Position = off;
                    // uint B58983D2
                    // int  _dataLength
                    int _dataLength = br.ReadInt32();
                    if (_dataLength != 0 && _dataLength < br.BaseStream.Length)
                    {
                        var pos = (int)br.BaseStream.Position;
                        br.BaseStream.Position = 0;
                        var junk = br.ReadBytes(pos);
                        var raw = br.ReadBytes(_dataLength);

                        if (br.BaseStream.Position != br.BaseStream.Length) //  - 1
                        {
                            Console.WriteLine("!EOF: \n" + br.ReadBytes((int)(br.BaseStream.Length - br.BaseStream.Position)).HexDump());
                        }

                        var locdata = new CompressedLocalizationData(new MemoryStream(raw));
                        var result = locdata.GetLines();

                        if (result != null)
                        {
                            result.Insert(0, new Line(junk.ByteArrayToString(), string.Empty));
                        }

                        return result;
                    }
                }
                else
                {
                    var locdata = new CompressedLocalizationData(new MemoryStream(buf));
                    var result = locdata.GetLines();

                    if (result != null)
                    {
                        result.Insert(0, new Line("RAW", string.Empty));
                    }

                    return result;
                }

                

                return null;
            }
        }

        public override byte[] RepackText(List<Line> lines)
        {
            byte[] junk;
            if (lines[0].ID == "RAW")
            {
                junk = null;
            }
            else
            {
                junk = lines[0].ID.HexStringToByteArray();
            }
            //var midChar = "_";
            //if(CurrentFilePath.Contains("English_Subtitles"))
            //{
            //    midChar = "\n";
            //}

            /* Collect line and id */
            var sb = new StringBuilder();
            var lstStrInfo = new List<StringInfo>();
            foreach (var line in lines.Skip(1))
            {
                var info = new StringInfo();
                info.Id = uint.Parse(line.ID, System.Globalization.NumberStyles.HexNumber);
                info.String = line.English;
                //info.String = line.Id + midChar + line.English; // DEBUG_MODE

                lstStrInfo.Add(info);
                sb.Append(info.String);
            }

            /* create encoder */
            var rnd = new Random();
            var lstChar = sb.ToString().Distinct().OrderBy(x => rnd.Next()).ToArray(); // create random table
            var acEncoder = CreateEncoding(lstChar);

            /* encode  and mask index */
            int tableSizeCounter = 0;
            ushort numIndexTable = 1;
            var lstIndexed = new List<List<StringInfo>>();
            var curIndexed = new List<StringInfo>();
            for (int i = 0; i < lstStrInfo.Count; i++)
            {
                var info = lstStrInfo[i];

                // create new indexing
                var data = EncodeString(acEncoder, info.String);

                tableSizeCounter += data.Length;
                if (tableSizeCounter > 0xF000)
                {
                    if (tableSizeCounter == data.Length)
                    {
                        throw new Exception("Out of range: " + info.String);
                    }
                    numIndexTable++;
                    tableSizeCounter = 0;
                    lstIndexed.Add(new List<StringInfo>(curIndexed));
                    curIndexed.Clear();
                }
                else if (curIndexed.Count > 0)
                {
                    var last = curIndexed[curIndexed.Count - 1];
                    if (lstStrInfo[i].Id - last.Id > 0x1000)
                    {
                        numIndexTable++;
                        tableSizeCounter = 0;
                        lstIndexed.Add(new List<StringInfo>(curIndexed));
                        curIndexed.Clear();
                    }
                }

                lstStrInfo[i].Encoded = data;
                lstStrInfo[i].Index = numIndexTable;
                curIndexed.Add(lstStrInfo[i]);
            }
            if (curIndexed.Count > 0)
            {
                lstIndexed.Add(curIndexed);
            }

            /* Build binary */
            ushort numStringFrags = (ushort)(lstChar.Length + 1); // frag in table
            //ushort maxStringFragmentIndex = (ushort)(numStringFrags > 254 ? 254 : numStringFrags); // const! 254, 240?
            ushort maxStringFragmentIndex = 0xFC; // only important if use ubisoft compression

            ushort mask = 0;

            using (var ms = new MemoryStream(_10MB))
            using (var bw = new EndianBinaryWriter(ms, Endian.BigEndian))
            {
                bw.Write(maxStringFragmentIndex);
                bw.Write(numStringFrags);

                bw.Write(mask); // 0000
                bw.Write(mask); // 0000

                // write compress table
                foreach (var enc in acEncoder)
                {
                    //Console.WriteLine(enc.Value + "=" + enc.Key);
                    var eChar = Encoding.BigEndianUnicode.GetBytes(enc.Key.ToString());
                    bw.Write(eChar);
                    bw.Write(mask);
                }
                numIndexTable = (ushort)lstIndexed.Count;
                bw.Write(numIndexTable);

                var pos = bw.BaseStream.Position;
                int baseoffset = (int)pos + numIndexTable * 0xC;

                var baseDataSize = new int[numIndexTable];
                var baseDataPtrOff = new int[numIndexTable];
                var lstEncoded = new List<byte[]>();
                int i = 0;
                foreach (var colect in lstIndexed)
                {
                    var firstID = colect[0].Id;
                    var numChildEntry = colect.Count - 1;
                    // write entry info
                    bw.Write(firstID); // FirstEntryId
                    baseDataPtrOff[i] = (int)bw.BaseStream.Position;
                    bw.Write(0); // EntriesDataOffset
                    bw.Write(baseoffset); // EntryHeadersOffset


                    var pos2 = bw.BaseStream.Position;
                    // write entry pointer info
                    bw.BaseStream.Seek(baseoffset, SeekOrigin.Begin);


                    var rawstr = colect[0].Encoded; //done
                    lstEncoded.Add(rawstr);
                    baseDataSize[i] = rawstr.Length;

                    bw.Write((ushort)numChildEntry);
                    bw.Write((ushort)baseDataSize[i]); // end offset of first string - relative

                    for (int j = 1; j < colect.Count; j++)
                    {
                        var nextId = colect[j].Id - firstID;

                        rawstr = colect[j].Encoded;
                        lstEncoded.Add(rawstr);
                        baseDataSize[i] += rawstr.Length;

                        bw.Write((ushort)nextId);
                        bw.Write((ushort)baseDataSize[i]);  // end offset of next string - relative
                    }

                    // write next entry
                    baseoffset += (numChildEntry * 4 + 4);
                    bw.BaseStream.Seek(pos2, SeekOrigin.Begin);
                    i++;
                }

                // update EntriesDataOffset
                var dataoff = baseoffset;
                for (i = 0; i < numIndexTable; i++)
                {
                    bw.BaseStream.Seek(baseDataPtrOff[i], SeekOrigin.Begin);
                    bw.Write(dataoff);
                    dataoff += baseDataSize[i];
                }

                // write encoded data
                bw.BaseStream.Seek(baseoffset, SeekOrigin.Begin);
                var rawBlock = lstEncoded.SelectMany(x => x).ToArray();
                bw.Write(rawBlock);

                var result = ms.ToArray();
                if (junk !=null && junk.Length > 0)
                {
                    using (var ms2 = new MemoryStream(result.Length + junk.Length))
                    using (var bw2 = new BinaryWriter(ms2))
                    {
                        bw2.Write(junk);
                        bw2.BaseStream.Position -= 4;
                        bw2.Write(result.Length);
                        bw2.Write(result);

                        result = ms2.ToArray();
                    }
                }
                

                return result;
            }
        }

        static Dictionary<char, byte> CreateEncoding(char[] input)
        {
            var dict = new Dictionary<char, byte>();
            for (byte i = 0; i < input.Length;)
            {
                dict.Add(input[i], ++i);
            }

            return dict;
        }

        static byte[] EncodeString(Dictionary<char, byte> dict, string input)
        {
            var result = new byte[input.Length];
            for (int i = 0; i < input.Length; i++)
            {
                var b = dict[input[i]];
                b--;
                result[i] = b;
            }
            return result;
        }

        class StringInfo
        {
            public uint Id { get; set; }
            public string String { get; set; }
            public byte[] Encoded { get; set; }
            public int Index;
        }
    }
}
#endif
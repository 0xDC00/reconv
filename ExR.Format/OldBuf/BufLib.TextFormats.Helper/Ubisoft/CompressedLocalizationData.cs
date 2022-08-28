using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BufLib.Common.IO;
using BufLib.TextFormats.DataModels;
using ExR.Format;

namespace BufLib.TextFormats.Helper.Ubisoft
{
    public class CompressedLocalizationData
    {
        private ushort maxStringFragmentIndex;
        private int stringFragmentIndexMask; // computed: maxStringFragmentIndex * 255
        private ushort numStringFrags;

        public StringTableEntryCollection[] StringTables { get; private set; }

        public CompressedLocalizationData(Stream input)
        {
            read(input);
        }

        private void read(Stream input)
        {
            var r = new EndianBinaryReader(input, Endian.BigEndian);

            #region read linked list structure of chars & string fragments
            maxStringFragmentIndex = r.ReadUInt16();

            case3b = maxStringFragmentIndex;
            if (case3b > 254 || case3b < 69)
            {
                throw new Exception("Invalid loc");
            }
            // used for indexing (2 byte index)
            // explanation: 
            //  - fragments are indexed by byte values. 
            //  - if an index > maxStringFragmentIndex => read next byte, interpret as ushort, substract mask and you have your index...
            //  - special case: index == 255 => read next to bytes and interpret as ushort index
            stringFragmentIndexMask = maxStringFragmentIndex * 255;
            numStringFrags = r.ReadUInt16();

            // read list
            CompressedStringFragment[] stringFragments = new CompressedStringFragment[numStringFrags];
            for (int i = 0; i < numStringFrags; i++)
            {
                stringFragments[i] = new CompressedStringFragment(r);
            }

            // resolve list
            for (int i = 0; i < numStringFrags; i++)
            {
                stringFragments[i].Resolve(stringFragments);
            }

            // dump
            //var lstStringFragment = new List<string>();
            //for (int i = 0; i < numStringFrags; i++)
            //{
            //    var aaa = string.Empty;
            //    if (stringFragments[i].leftIndex != 0)
            //        aaa = "[Link]";
            //    lstStringFragment.Add(aaa + stringFragments[i].Value.Replace("\r", "[\\r]").Replace("\n", "[\\n]").Replace(" ", "($20)").Replace("\t", "($09)"));
            //}
            //File.WriteAllLines("stringFragments.txt", lstStringFragment);
            #endregion

            ushort tablesCount = r.ReadUInt16();
            StringTables = new StringTableEntryCollection[tablesCount];
            for (int i = 0; i < tablesCount; i++)
            {
                StringTables[i] = new StringTableEntryCollection(r);
            }

            for (int i = 0; i < tablesCount; i++)
            {
                r.BaseStream.Position = StringTables[i].EntriesDataOffset; // it's header, not data!

                ushort entryCount = r.ReadUInt16();

                StringTables[i].Entries = new StringTableEntry[entryCount + 1];

                //StringTables[i].Entries[0] = new StringTableEntry(StringTables[i].FirstEntryId, r.ReadUInt16());
                StringTables[i].Entries[0] = new StringTableEntry(StringTables[i].FirstEntryId, r.ReadUInt16(), r.BaseStream.Position - 2, StringTables[i].EntryAddress); // backup address for test

                for (int j = 1; j < entryCount + 1; j++)
                {
                    uint id = (StringTables[i].FirstEntryId + r.ReadUInt16());
                    //StringTables[i].Entries[j] = new StringTableEntry(id, r.ReadUInt16());
                    StringTables[i].Entries[j] = new StringTableEntry(id, r.ReadUInt16(), r.BaseStream.Position - 2, r.BaseStream.Position - 4); // backup address for test
                }

                r.BaseStream.Position = StringTables[i].EntryHeadersOffset; // Data (wrong name)

                //int numConsumedCodes = 0;
                //var splitCheck = -1; // FIX ACO
                //for (int j = 0; j < StringTables[i].Entries.Length; j++)
                //{
                //    int endOffset = StringTables[i].Entries[j].Offset;
                //    // debug count
                //    // var byteUsed = endOffset - numConsumedCodes;
                //    if(splitCheck != -1) // merge to previous line
                //    {
                //        StringTables[i].Entries[splitCheck].DecodedString += decodeString(r, stringFragments, endOffset, ref numConsumedCodes);
                //        var check = endOffset - numConsumedCodes;
                //        if (check == 0)
                //        {
                //            splitCheck = -1;
                //        }
                //        else
                //        {
                //            StringTables[i].Entries[j].DecodedString = string.Empty;
                //        }
                //    }
                //    else
                //    {
                //        StringTables[i].Entries[j].DecodedString = decodeString(r, stringFragments, endOffset, ref numConsumedCodes);
                //        var check = endOffset - numConsumedCodes;
                //        if (check != 0)
                //        {
                //            StringTables[i].Entries[j - 1].DecodedString += StringTables[i].Entries[j].DecodedString; // merge to previous
                //            StringTables[i].Entries[j].DecodedString = string.Empty; // clear
                //            splitCheck = j-1; // set pos for next
                //        }
                //        else
                //        {
                //            StringTables[i].Entries[j].DecodedString = check + "_" +  StringTables[i].Entries[j].DecodedString;
                //        }
                //    }


                //    Console.WriteLine("-");
                //    Console.WriteLine(StringTables[i].Entries[j].DecodedString);
                //}

                // Backup for old version
                int numConsumedCodes = 0;
                for (int j = 0; j < StringTables[i].Entries.Length; j++)
                {
                    int endOffset = StringTables[i].Entries[j].Offset;
                    //if (StringTables[i].Entries[j].Id >= 0x001082e8)
                    //{
                    //    // 15
                    //    // E6B4
                    //    //endOffset = 0x0000e6b5;
                    //    //numConsumedCodes = 0xE6B4;
                    //    numConsumedCodes += 0x14;
                    //}
                    StringTables[i].Entries[j].DecodedString = decodeString(r, stringFragments, endOffset, ref numConsumedCodes);


                    // DC_FIX
                    //Console.WriteLine("-");
                    //Console.WriteLine(StringTables[i].Entries[j].DecodedString);
                }
            }
            string text = string.Empty;
            var l = r.BaseStream.Length - r.BaseStream.Position;
            if (l > 0)
            {
                try
                {
                    int numConsumedCodes = 0;
                    text = decodeString(r, stringFragments, (int)l, ref numConsumedCodes);
#if !BLAZOR
                    Console.ForegroundColor = ConsoleColor.DarkRed;
                    Console.WriteLine(text);
                    Console.ResetColor();
                    Console.ReadKey();
#endif
                }
                catch { }
            }
            if (r.BaseStream.Position != r.BaseStream.Length)
            {
                Console.WriteLine("Foo: \n" + r.ReadBytes((int)(r.BaseStream.Length - r.BaseStream.Position)).HexDump());
            }
            //Console.WriteLine(r.BaseStream.Position.ToString("X"));
            //Console.WriteLine("maxSFI   " + maxStringFragmentIndex);
            //Console.WriteLine("case2min " + ++case2min);
            //Console.WriteLine("case2max " + ++case2max);
            //Console.WriteLine("case3b   " + case3b);
            //Console.WriteLine("case3min " + ++case3min);
            //Console.WriteLine("case3max " + ++case3max);
            //Console.WriteLine(count1);
            //Console.WriteLine(count2);
            //Console.WriteLine(count3);

        }

        public void WriteToFile(string filename)
        {
            int i = 5604;
            if (StringTables == null || StringTables.Length == 0) return;
            using (StreamWriter w = new StreamWriter(filename))
            {
                foreach (var t in StringTables)
                {
                    foreach (var e in t.Entries)
                    {
                        //w.WriteLine(string.Format("[0x{0:x8}]", e.Id));
                        //w.WriteLine(e.DecodedString);

                        // use for repack
                        w.WriteLine(string.Format("[{0:x8}],{1,5}_{2}", e.Id, i++, e.DecodedString.Replace("\r", "[\\r]").Replace("\n", "[\\n]"))); // 1:D4 1,4

                        //w.WriteLine(string.Format("[0x{0:x8}] {1}", e.Id, e.DecodedString));
                        //w.WriteLine();

                        // use for review
                        //try
                        //{
                        //    w.WriteLine(e.DecodedString.Replace("\r", "[\\r]").Replace("\n", "[\\n]"));
                        //}
                        //catch (Exception ex)
                        //{
                        //    Console.WriteLine(ex.ToString());
                        //}



                        //w.WriteLine(string.Format("ID: [0x{0:x8}] Offset: [0x{1:x8}] IdAddress: [0x{2:x8}] OffAddress: [0x{3:x8}] ", e.Id, e.Offset, e.IdAddress, e.OffAddress));
                        //w.WriteLine(e.DecodedString);

                        //w.WriteLine(string.Format("ID: [0x{0:x8}] Offset: [0x{1:x8}] IdAddress: [0x{2:x8}] OffAddress: [0x{3:x8}] {4}", e.Id, e.Offset, e.IdAddress, e.OffAddress, e.DecodedString).Replace("\r", "[\\r]").Replace("\n", "[\\n]"));

                    }
                    //w.WriteLine("[/DC/]");
                }
            }
        }

        public List<Line> GetLines()
        {
            if (StringTables == null || StringTables.Length == 0) return null;

            var result = new List<Line>();
            foreach (var t in StringTables)
            {
                foreach (var e in t.Entries)
                {
                    result.Add(new Line(e.Id.ToString("X8"), e.DecodedString));

                }
            }
            return result;
        }

        // anlysis index
        int case2min = int.MaxValue;
        int case2max = int.MinValue;
        int case3min = int.MaxValue;
        int case3max = int.MinValue;
        int case3b = 254; // 254
        int count1 = 0;
        int count2 = 0;
        int count3 = 0;
        private string decodeString(BinaryReader r, CompressedStringFragment[] stringFragments, int endOffset, ref int numConsumedCodes)
        {

            StringBuilder sb = new StringBuilder();
#region index reading magic...            
            while (numConsumedCodes < endOffset)
            {

                byte b = r.ReadByte();
                numConsumedCodes++;
                if (b < maxStringFragmentIndex)
                {
                    count1++;
                    sb.Append(stringFragments[b + 1].ToString());
                }
                else if (b == 255)
                //else if (b == case3b+1) // fix for ACO
                {
                    count2++;
                    int decIndex = r.ReadInt16();
                    sb.Append(stringFragments[decIndex + 1].ToString());
                    numConsumedCodes += 2;

                    if (decIndex < case2min)
                        case2min = decIndex;
                    if (decIndex > case2max)
                        case2max = decIndex;
                }
                else
                {
                    count3++;
                    if (b != case3b)
                        case3b = b;

                    int decIndex = b << 8;
                    b = r.ReadByte();
                    decIndex |= b;
                    numConsumedCodes++;

                    decIndex -= stringFragmentIndexMask;

                    if (decIndex < case3min && decIndex != 254)
                        case3min = decIndex;
                    if (decIndex > case3max)
                        case3max = decIndex;

                    sb.Append(stringFragments[decIndex + 1].ToString());
                }
            }
#endregion

            return sb.ToString();
        }

        private class CompressedStringFragment
        {
            private ushort rightIndexOrChar; // if LeftIndex == 0  => value is char, else value is index
            public ushort leftIndex;

            private string rightValue = null;
            private string leftValue = null;

            public string Value { get; private set; }

            public CompressedStringFragment()
            {

            }

            public CompressedStringFragment(BinaryReader r)
            {
                Read(r);
            }

            public void Read(BinaryReader r)
            {
                rightIndexOrChar = r.ReadUInt16();
                leftIndex = r.ReadUInt16();
            }

            public void Resolve(CompressedStringFragment[] list)
            {
                if (Value == null)
                    Value = resolveString(list);
            }

            private string resolveString(CompressedStringFragment[] list)
            {
                // linked list index == 0
                if (leftIndex == 0 && rightIndexOrChar == 0) return "";

                // is character
                if (leftIndex == 0) return Encoding.Unicode.GetString(BitConverter.GetBytes(rightIndexOrChar));

                // is linked list element
                if (rightValue == null && rightIndexOrChar >= 0 && rightIndexOrChar < list.Length)
                {
                    list[rightIndexOrChar].Resolve(list);
                    rightValue = list[rightIndexOrChar].Value;
                }
                if (leftValue == null && leftIndex >= 0 && leftIndex < list.Length)
                {
                    list[leftIndex].Resolve(list);
                    leftValue = list[leftIndex].Value;
                }

                return leftValue + rightValue;
            }

            public override string ToString()
            {
                return Value;
            }
        }

        public class StringTableEntry
        {
            // for debug
            public long OffAddress { get; private set; }
            public long IdAddress { get; private set; }
            // end

            public uint Id { get; private set; }
            public ushort Offset { get; private set; }

            public string DecodedString { get; set; }

            public StringTableEntry(uint id, ushort offset)
            {
                Id = id;
                Offset = offset;
            }

            public StringTableEntry(uint id, ushort offset, long offAddress, long idAddress)
            {
                Id = id;
                Offset = offset;
                OffAddress = offAddress;
                IdAddress = idAddress;
            }

            public override string ToString()
            {
                return string.Format("[0x{0:x8}] {1}", Id, DecodedString);
            }
        }

        public class StringTableEntryCollection
        {
            public long EntryAddress { get; private set; }

            public uint FirstEntryId { get; private set; }
            public uint EntryHeadersOffset { get; private set; }
            public uint EntriesDataOffset { get; private set; }

            public StringTableEntry[] Entries { get; set; }

            public StringTableEntryCollection()
            {

            }

            public StringTableEntryCollection(BinaryReader r)
            {
                Read(r);
            }

            public void Read(BinaryReader r)
            {
                EntryAddress = r.BaseStream.Position;
                FirstEntryId = r.ReadUInt32();
                EntryHeadersOffset = r.ReadUInt32();
                EntriesDataOffset = r.ReadUInt32();
            }

            public override string ToString()
            {
                return string.Format("StartID: 0x{0:x8}, ({1:0000} Entries)", FirstEntryId, Entries.Length);
            }
        }
    }
}

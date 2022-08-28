/*
UINT32 - XORed by 0xFADC4688 decompressed PTR size
UINT32 - version
UINT32 - splited to 4 parts and XORed first 32768 ~ 0x8000 bytes (32 KB) PKR MD5 (without header)
UINT32 - splited to 4 parts and XORed decompressed PTR MD5 (without header)
11111111 ^ 22222222 ^ 33333333 ^ 44444444
// http://forum.xentax.com/viewtopic.php?f=35&t=17407
     
MD5(0x8000 byte raw PKR) -> 1111222233334444 -> xor 4 part
MD5(decompressed PTR)  -> 1111222233334444 -> xor 4 part
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using BufLib.Common.IO;
using Zio;

namespace ExR.Format
{
    [Plugin("com.dc.tew2ptrpkr", "[Archive] The Evil Within 2 (.ptr, .pkr)", @"Extract & Repack, No _init_.yaml needed.
Another tool: https://zenhax.com/viewtopic.php?t=5088&start=20")]
    class A_Archive_TheEvilWithin2_ptr_pkr : TextFormat
    {
        public override bool Init(Dictionary<string, object> dict)
        {
            Extensions = new string[] { ".ptr" };
            return true;
        }

        public override List<Line> ExtractText(byte[] bytes)
        {
            var outFolder = (UPath)Path.ChangeExtension(CurrentFilePath, null);
            

            var pathToPKR = Path.ChangeExtension(CurrentFilePath, ".pkr");
            var ptr = bytes.Skip(16).ToArray(); // DecompressedSize, Version=1, PkrChecksum, PtrChecksum
            try
            {
                ptr = ptr.DeflateUncompress(); // for PS4
            }
            catch { }

            using (var ms = new MemoryStream(ptr))
            using (var br = new EndianBinaryReader(ms))
            {
                var header = br.ReadStruct<PtrHeader>();
                if (header.numFile == 0)
                {
                    Console.WriteLine("Files: 0");
                    return null;
                }
                br.BaseStream.Position += header.numFile * 4 + header.count2 * 4;
                var pathSectionLength = br.ReadInt32();
                var pathCount = br.ReadInt32();

                var path = br.ReadTerminatedStrings(pathCount, Encoding.UTF8);

                var offset = br.BaseStream.Position;
                var fileInfos = br.ReadStructs<BlockInfo>(header.numFile);
                using (var fs = FsIn.OpenFile(pathToPKR, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var br2 = new BinaryReader(fs))
                {
                    //var fimd = new FileInfoModel[header.numFile];
                    //var fimd = new IList<FileInfoModel>;
                    var fimd = new MyClass
                    {
                        SizePKR = br.BaseStream.Length,
                        Offset = offset,
                        LazyDictionary = new Dictionary<string, int>()
                    };
                    for (int i = 0; i < header.numFile; i++)
                    {
                        var fi = fileInfos[i];
                        var relPah = path[fi.pathIndex];
                        Console.WriteLine(relPah);

                        br2.BaseStream.Position = fi.pkrOffset;
                        var data = br2.ReadBytes(fi.sizeZ);
                        //bool isCom = false;
                        if (fi.sizeZ != fi.size)
                        {
                            //isCom = true;
                            data = data.DeflateUncompress();
                            if (data.Length != fi.size)
                                throw new Exception("Deflate decompress error!");
                        }

                        var outFile = outFolder / relPah;
                        var f = outFile.GetDirectory();
                        FsOut.CreateDirectory(f);
                        FsOut.WriteAllBytes(outFile, data);

                        //fimd[i] = new FileInfoModel(i, relPah);
                        //fimd.Add(new FileInfoModel(i, relPah));
                        fimd.LazyDictionary.Add(relPah.Replace('/', '\\'), i);
                    }

                    var lines = new List<Line>()
                    {
                        new Line(fimd.ToJson())
                    };
                    _PushEnd(lines, bytes);

                    return lines;
                }
            }
        }

        public override byte[] RepackText(List<Line> lines)
        {
            var PKR_SIG = 0xFCBA1183;
            //var pathToPtr = CurrentFilePath;
            var pathToPKR = (UPath)Path.ChangeExtension(CurrentFilePath, ".pkr");
            var inFolder = (UPath)Path.ChangeExtension(CurrentFilePath, null);
            var oriPtr = _PopEnd(lines);
            var rawPtr = oriPtr.Skip(16).ToArray(); // DecompressedSize, Version=1, PkrChecksum, PtrChecksum
            Array.Resize(ref oriPtr, 16); // header only

            var fimd = lines[0].English.FromJson<MyClass>();
            try
            {
                rawPtr = rawPtr.DeflateUncompress(); // for PS4
            }
            catch { }

            BlockInfo[] fileInfos;
            using (var ms = new MemoryStream(rawPtr))
            using (var br = new EndianBinaryReader(ms))
            {
                var numFile = br.ReadInt32();
                br.BaseStream.Position = fimd.Offset;
                fileInfos = br.ReadStructs<BlockInfo>(numFile);
            }

            using (var ms = new MemoryStream(_10MB))
            using (var bw = new BinaryWriter(ms))
            {
                bw.Write(PKR_SIG);
                bw.Write(new byte[0xC]);
                int i = 0;
                foreach (var fi in fimd.LazyDictionary)
                {
                    Console.WriteLine(fi.Key);
                    fileInfos[i].pkrOffset = (int)bw.BaseStream.Position; // set pointer of file

                    var path = inFolder / fi.Key;
                    CurrentFilePath = path.FullName;
                    var file = ReadCurrentFileData();
                    var size = file.Length;
                    int sizez;

                    // bypass checksum
                    {
                        sizez = size;
                        bw.Write(file);
                    }

                    fileInfos[i].size = size;
                    fileInfos[i].sizeZ = sizez;

                    i++;
                }

                // SAVE PKR
                FsOut.WriteAllBytes(pathToPKR, ms.ToArray());
            }

            // CREATE new PTR
            using (var ms = new MemoryStream(rawPtr))
            using (var bw = new EndianBinaryWriter(ms))
            {
                bw.BaseStream.Position = fimd.Offset;
                bw.WriteStructs(fileInfos);
            }
            var newPtr = rawPtr.DeflateCompress(System.IO.Compression.CompressionLevel.NoCompression).ToList();
            newPtr.InsertRange(0, oriPtr);
            var newPtr_ = newPtr.ToArray();
            for (int i = 8; i < 0x10; i++)
                newPtr_[i] = 0; // bypass checksum

            return newPtr.ToArray();
        }

        public class MyClass
        {
            public virtual long Offset { get; set; }

            public virtual long SizePKR { get; set; }

            public virtual IDictionary<string, int> LazyDictionary { get; set; } // attention: duplicate name
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        struct PtrHeader
        {
            public int numFile;
            public int u2;
            public int u3;
            public int u4; // =numFile
            public int count2;
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        struct BlockInfo
        {
            public int pathIndex;
            public int pkrOffset;
            public int sizeZ;
            public int size;
        }
    }
}

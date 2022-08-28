// PF6, PF8 auto detect
// Artemis Engine
// TODO: PF2: https://github.com/morkt/GARbro/blob/master/ArcFormats/Artemis/ArcPFS.cs

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using BufLib.Common.IO;
using Zio;

namespace ExR.Format
{
    [Plugin("com.dc.ArtemisPFS", "[Archive] Artemis Engine (.pfs - magic: pf2, pf6, pf8)", @"Extract/Repack/Create
_init_.yaml (optional)
---
encoding: utf_8 # default: Shift_JIS (file name encoding)
...

Note:
- root.pfs.??? (??? <=> num <=> priority [999 > 000])
- Create:
  + File root.pfs.999.csv (metadata)
ID,English,Vietnamese,Note
1_pf8,root_pfs_999,,

  + Folder root_pfs_999 with any content.
  + Zip (csv + folder) and Repack.")]
    class A_Archive_pfs : TextFormat
    {
        public override bool Init(Dictionary<string, object> dict)
        {
            if (dict.TryGetValue("encoding", out var encodingName))
            {
                _Encoding = Encoding.GetEncoding((string)encodingName);
            }
            else
            {
                _Encoding = Encoding.GetEncoding(932); // Shift_JIS
            }
            
            return true;
        }

        public override void End()
        {
            base.End();
        }

        List<Line> extractPf2(UPath outFolder, EndianBinaryReader br, List<Line> result)
        {
            var indexSize = br.ReadInt32(); // toc_size
            var zero = br.ReadInt32();
            var numFile = br.ReadInt32(); // num_file

            Console.WriteLine("TotalFile: " + numFile);
            //var chunkInfos = new List<ChunkInfo>(numFile);
            for (int i = 0; i < numFile; i++)
            {
                var p = br.ReadStringPrefixedLengt32(_Encoding);
                var u1 = br.ReadInt32();
                var u2 = br.ReadInt32();
                var u3 = br.ReadInt32();
                var off = br.ReadInt32();
                var sz = br.ReadInt32();

                if (u1 != 0x10) Console.WriteLine("u1 " + u1);
                if (u2 != 0) Console.WriteLine("u2 " + u1);
                if (u3 != 0) Console.WriteLine("u3 " + u1);

                //chunkInfos.Add(new ChunkInfo()
                //{
                //    Path = p,
                //    ZERO = u1,
                //    Offset = off,
                //    Size = sz,
                //});

                var pos = br.BaseStream.Position;
                br.BaseStream.Position = off;
                var data = br.ReadBytes(sz);
                br.BaseStream.Position = pos;

                Console.WriteLine(p);
                var outPath = outFolder / /*chunkInfo.Path*/ p;
                var basePath = Path.GetDirectoryName(outPath.FullName);
                if (basePath != UPath.Root)
                    FsOut.CreateDirectory(basePath);
                FsOut.WriteAllBytes(outPath, data);
            }

            return result;
        }

        byte[] repackPf2(EndianBinaryWriter bw, UPath[] files, IFileSystem fsys, UPath uInFolder)
        {
            // write
            byte[] header = { 0x70, 0x66, 0x32, 0xB9, 0x2D, 0x09, 0x00, 0x00, 0x00, 0x00, 0x00, 0x09, 0x2E, 0x00, 0x00 };
            bw.Write(header);
            bw.BaseStream.Position = 0xB;
            bw.Write(files.Length);

            // write
            var offsets = new List<long>();
            foreach (var file in files)
            {
                var rel = Path.GetRelativePath(uInFolder.FullName, file.FullName).Replace('/', '\\'); // window style
                bw.WriteStringPrefixedLengt32(rel, _Encoding);

                bw.Write(0x10);
                bw.Write(0);
                bw.Write(0);
                offsets.Add(bw.BaseStream.Position);
                bw.Write(0);
                bw.Write(0);
            }

            // fix first pointer (indexSize)
            var firstFilePointer = (int)bw.BaseStream.Position;
            bw.BaseStream.Position = 3;
            bw.Write(firstFilePointer - 7);

            // write offset & size
            bw.BaseStream.Position = firstFilePointer;
            var baseOffSet = (int)bw.BaseStream.Position;
            for (int i = 0; i < files.Length; i++)
            {
                var file = files[i];
                bw.BaseStream.Position = offsets[i];
                bw.Write(baseOffSet);
                var size = (int)(fsys.GetFileEntry(file).Length);
                bw.Write(size);
                baseOffSet += size;
            }

            foreach (var file in files)
            {
                var rel = Path.GetRelativePath(uInFolder.FullName, file.FullName).Replace('/', '\\'); // window style
                Console.WriteLine(rel);
                var raw = fsys.ReadAllBytes(file);

                bw.Write(raw);
            }


            return ((MemoryStream)bw.BaseStream).ToArray();
        }

        public override List<Line> ExtractText(byte[] bytes)
        {
            var result = new List<Line>();
            var nameFolder = Path.GetFileName(CurrentFilePath).Replace('.', '_');
            var outFolder = ((UPath)nameFolder).ToAbsolute(); // name => /name
            FsOut.CreateDirectory(outFolder);

            using (var ms = new MemoryStream(bytes))
            using (var br = new EndianBinaryReader(ms))
            {
                //var magic = br.ReadBytes(2); // pf
                //var version = br.ReadByte(); // 8
                var magicVer = br.ReadBytes(3);
                result.Add(new Line(Encoding.ASCII.GetString(magicVer), nameFolder));
                Console.WriteLine("Magic: " + result[0].ID);

                var version = magicVer[2];
                if (version == '2') return extractPf2(outFolder, br, result);

                var indexSize = br.ReadInt32(); // toc_size
                var numFile = br.ReadInt32(); // num_file

                Console.WriteLine("TotalFile: " + numFile);
                var chunkInfos = new List<ChunkInfo>(numFile);
                for (int i = 0; i < numFile; i++)
                {
                    chunkInfos.Add(new ChunkInfo()
                    {
                        Path = br.ReadStringPrefixedLengt32(_Encoding),
                        ZERO = br.ReadInt32(),
                        Offset = br.ReadInt32(),
                        Size = br.ReadInt32(),
                    });
                }

                // Calc key
                br.BaseStream.Position = 7;
                var toc = br.ReadBytes(indexSize);

                byte[] key = null;
                if (version == '8')
                {
                    using (SHA1 sha = SHA1.Create())
                    {
                        key = sha.ComputeHash(toc);
                    }
                    Console.WriteLine("XorKey: " + key.ByteArrayToString());
                }
                
                // extract
                foreach (var chunkInfo in chunkInfos)
                {
                    Console.WriteLine(chunkInfo.Path);
                    br.BaseStream.Position = chunkInfo.Offset;
                    var data = br.ReadBytes(chunkInfo.Size);

                    if (version == '8')
                    {
                        if (IsMovie(chunkInfo.Path) == false)
                            Xor(data, key);
                    }

                    var outPath = outFolder / chunkInfo.Path;
                    var basePath = Path.GetDirectoryName(outPath.FullName);
                    if (basePath != UPath.Root)
                        FsOut.CreateDirectory(basePath);
                    FsOut.WriteAllBytes(outPath, data);
                }
            }

            return result;
        }

        public override byte[] RepackText(List<Line> lines)
        {
            var line = lines[0];
            var magicVer = Encoding.ASCII.GetBytes(line.ID);
            var version = magicVer[2];
            var inFolder = line.English;
            var uInFolder = ((UPath)inFolder).ToAbsolute();

            // use CurrentFilePath
            //var uFileName = ((UPath)inFolder.Replace('_', '.')).ToAbsolute();

            var fsys = FsIn;
            var files = fsys.EnumerateFiles(uInFolder, "*", SearchOption.AllDirectories).ToArray();
            if (files.Length == 0)
            {
                fsys = FsOut;
                files = fsys.EnumerateFiles(uInFolder, "*", SearchOption.AllDirectories).ToArray();
            }

            using (var fs = new MemoryStream(_10MB))
            using (var bw = new EndianBinaryWriter(fs))
            {
                if (version == '2') return repackPf2(bw, files, fsys, uInFolder);

                var header = new byte[] { 0x70, 0x66, version, 0x50, 0x00, 0x00, 0x00 };
                bw.Write(header);
                bw.Write(files.Length);

                var offsets = new List<long>();
                foreach (var file in files)
                {
                    var rel = Path.GetRelativePath(uInFolder.FullName, file.FullName).Replace('/', '\\'); // window style
                    bw.WriteStringPrefixedLengt32(rel, _Encoding);
                    offsets.Add(bw.BaseStream.Position - header.Length);
                    bw.Write(0);
                    bw.Write(0);
                    bw.Write(0);
                }


                // write toc offset table
                var offsetOfNumOffset = (int)bw.BaseStream.Position - 7;
                bw.Write(files.Length + 1);
                for (int i = 0; i < files.Length; i++)
                {
                    bw.Write(offsets[i]);
                }
                bw.Write((long)0);
                bw.Write(offsetOfNumOffset);
                var indexSize = (int)bw.BaseStream.Position - 7;

                // write offset & size
                var baseOffSet = (int)bw.BaseStream.Position;
                for (int i = 0; i < files.Length; i++)
                {
                    var file = files[i];
                    bw.BaseStream.Position = offsets[i] + 7 + 4;
                    bw.Write(baseOffSet);
                    var size = (int)(fsys.GetFileEntry(file).Length);
                    bw.Write(size);
                    baseOffSet += size;
                }

                // get encrypt key
                byte[] key = null;
                if (version == 8)
                {
                    bw.BaseStream.Position = 7;
                    var toc = new byte[indexSize];
                    bw.BaseStream.Read(toc, 0, indexSize);
                    using (SHA1 sha = SHA1.Create())
                    {
                        key = sha.ComputeHash(toc);
                    }
                    Console.WriteLine("XorKey: " + key.ByteArrayToString());
                }
                bw.BaseStream.Position = bw.BaseStream.Length;

                foreach (var file in files)
                {
                    var rel = Path.GetRelativePath(uInFolder.FullName, file.FullName).Replace('/', '\\'); // window style
                    Console.WriteLine(rel);
                    var raw = fsys.ReadAllBytes(file);
                    if (version == '8')
                    {
                        if (IsMovie(file.FullName) == false)
                            Xor(raw, key);
                    }
                    else
                    {
                        // PF6 not encrypt
                    }

                    bw.Write(raw);
                }

                // write index size
                bw.BaseStream.Position = 3;
                bw.Write(indexSize);

                return fs.ToArray();
            }
        }

        static bool IsMovie(string path)
        {
            path = path.ToLower();
            if (path.EndsWith(".mp4") || path.EndsWith(".mpeg"))
            {
                return true;
            }

            return false;
        }

        static void Xor(byte[] data, byte[] key)
        {
            int j = 0;
            for (int i = 0; i < data.Length; i++)
            {
                data[i] ^= key[j++];
                if (j == key.Length)
                    j = 0;
            }
        }

        class ChunkInfo
        {
            public int PathLenght { get; set; }
            public string Path { get; set; }
            public int ZERO { get; set; }
            public int Offset { get; set; }
            public int Size { get; set; }
        }
    }
}

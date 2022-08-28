// SharpYaml vs YamlDotNet

using ExR.Format;
using ExR.OutputProviders;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using Zio;
using Zio.FileSystems;

namespace ExR
{
    public partial class TextConv
    {
        public TextFormat Convert { get; set; }
        public Logger Log { get; set; }
        private ITextIOProvider _textIO = null;
        
        public TextConv()
        {
            Log = new Logger(nameof(ExR));
            _textIO = new CsvTextIOProvider();
            //if (OperatingSystem.IsBrowser())
        }

        #region Extract
#if !BLAZOR
        public async Task Extract(string inPath, string outPath)
        {
            var pyIn = new PhysicalFileSystem();
            var _inPath = pyIn.ConvertPathFromInternal(inPath);
            var fsIn = new SubFileSystem(pyIn, _inPath);

            await Extract(fsIn, outPath);
        }

        public async Task Extract(IFileSystem fsIn, string outPath)
        {
            var pyOut = new PhysicalFileSystem();
            var _outPath = pyOut.ConvertPathFromInternal(outPath);
            if (_outPath != UPath.Root)
                pyOut.CreateDirectory(_outPath);
            var fsOut = new SubFileSystem(pyOut, _outPath);
            await Extract(fsIn, fsOut);
        }

        public async Task Extract(Stream inZip, string outPath)
        {
            var memIn = new MemoryFileSystem();
            memIn.ImportZip(inZip);

            var pyOut = new PhysicalFileSystem();
            var _outPath = pyOut.ConvertPathFromInternal(outPath);
            if (_outPath != UPath.Root)
                pyOut.CreateDirectory(_outPath);
            var fsOut = new SubFileSystem(pyOut, _outPath);

            await Extract(memIn, fsOut);
        }
#endif
        public async Task Extract(Stream zipIn, Stream zipOut)
        {
            var memIn = new MemoryFileSystem();
            memIn.ImportZip(zipIn);

            var memOut = new MemoryFileSystem();

            await Extract(memIn, memOut);

            memOut.ExportZip(zipOut);
        }

        public async Task<MemoryStream> Extract(Stream zipIn)
        {
            var zipOut = new MemoryStream();
            await Extract(zipIn, zipOut);
            zipOut.Position = 0;
            return zipOut;
        }
#endregion

#region Repack
        public async Task<MemoryStream> Repack(Stream zipIn, Stream zipOut_Payload)
        {
            var memIn = new MemoryFileSystem();
            memIn.ImportZip(zipIn);

            var memOut = new MemoryFileSystem();
            if (zipOut_Payload != null && zipOut_Payload.Length > 0)
            {
                memOut.ImportZip(zipOut_Payload); // importPayload
            }
            await Repack(memIn, memOut);

            var ms = new MemoryStream();
            memOut.ExportZip(ms);
            ms.Position = 0;
            return ms;
        }

#if !BLAZOR
        public async Task Repack(Stream inZip, string outPath)
        {
            var memIn = new MemoryFileSystem();
            memIn.ImportZip(inZip);

            var pyOut = new PhysicalFileSystem();
            var _outPath = pyOut.ConvertPathFromInternal(outPath);
            if (_outPath != UPath.Root)
                pyOut.CreateDirectory(_outPath);
            var fsOut = new SubFileSystem(pyOut, _outPath);

            await Repack(memIn, fsOut);

        }

        public async Task Repack(string inPath, string outPath)
        {
            var pyIn = new PhysicalFileSystem();
            var _inPath = pyIn.ConvertPathFromInternal(inPath);
            var fsIn = new SubFileSystem(pyIn, _inPath);

            var pyOut = new PhysicalFileSystem();
            var _outPath = pyOut.ConvertPathFromInternal(outPath);
            if (_outPath != UPath.Root)
                pyOut.CreateDirectory(_outPath);
            var fsOut = new SubFileSystem(pyOut, _outPath);
            await Repack(fsIn, fsOut);
        }

        public async Task Repack(IFileSystem inPath, string outPath)
        {
            var fsIn = inPath;

            var pyOut = new PhysicalFileSystem();
            var _outPath = pyOut.ConvertPathFromInternal(outPath);
            if (_outPath != UPath.Root)
                pyOut.CreateDirectory(_outPath);
            var fsOut = new SubFileSystem(pyOut, _outPath);
            await Repack(fsIn, fsOut);
        }
#endif
        #endregion
    }

    public static class IFileSysteamExts
    {
        public static bool IsZip(this Stream stream)
        {
            if (stream.Length > 4)
            {
                stream.Position = 0;
                var bytes = new byte[4];
                stream.Read(bytes, 0, 4);
                stream.Position = 0;

                // https://users.cs.jmu.edu/buchhofp/forensics/formats/pkzip.html
                // https://www.filesignatures.net/index.php?search=ZIP&mode=EXT
                if (bytes[0] == 0x50
                    && bytes[1] == 0x4b
                    && (bytes[2] == 0x03 && bytes[3] == 0x04
                        || bytes[2] == 0x05 && bytes[3] == 0x06
                        || bytes[2] == 0x07 && bytes[3] == 0x08
                    ))
                {
                    return true;
                }
            }

            return false;
        }

        public static MemoryStream ToZip(this Stream stream, string fileName, CompressionLevel lv = CompressionLevel.NoCompression)
        {
            var outStream = new MemoryStream();
            using (var zip = new ZipArchive(outStream, ZipArchiveMode.Create, true))
            {
                stream.Position = 0;
                var entry = zip.CreateEntry(fileName, lv);
                using (var write = entry.Open())
                {
                    stream.CopyTo(write);
                }
            }
            stream.Position = 0;
            outStream.Position = 0;
            return outStream;
        }

        public static IFileSystem ImportZip(this IFileSystem fs, Stream stream)
        {
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Read, true))
            {
                foreach (var entry in zip.Entries)
                {
                    if (entry.Length > 0)
                    {
                        var path = ((UPath)entry.FullName).ToRelative().ToAbsolute();
                        var pathDir = path.GetDirectory();
                        if (pathDir != UPath.Root)
                            fs.CreateDirectory(pathDir);
                        using (var h = fs.CreateFile(path))
                        {
                            entry.Open().CopyTo(h);
                            //if (htextStream.Capacity != htextStream.Length)
                            //{
                            //    throw new Exception("Zip size!");
                            //}
                        }
                    }
                }
            }

            return fs;
        }

        public static byte[] ExportZip(this IFileSystem fs)
        {
            using(var ms = new MemoryStream())
            {
                ExportZip(fs, ms);

                return ms.ToArray();
            }
        }

        public static void ExportZip(this IFileSystem fs, Stream stream)
        {
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
            {
                foreach (var path in fs.EnumerateFiles(UPath.Root, "*", SearchOption.AllDirectories))
                {
                    var bytes = fs.ReadAllBytes(path);
                    var entry = zip.CreateEntry(path.ToRelative().FullName, CompressionLevel.NoCompression);
                    using (var write = entry.Open())
                    {
                        write.Write(bytes);
                    }
                }
            }
            stream.Position = 0;
        }
    }
}

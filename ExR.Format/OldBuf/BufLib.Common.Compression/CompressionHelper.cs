// TODO:
// https://zlib.net/ (1.2.8.1 -> 1.2.11)
//      https://github.com/madler/zlib
//      https://www.nuget.org/packages?q=zlib-msvc
//          https://github.com/ied206/ZLibWrapper/
//
// https://www.nuget.org/packages/zlib.net/
//
// https://www.nuget.org/packages/lz4net/
// https://docs.microsoft.com/en-us/dotnet/api/system.io.compression.zlibstream?view=net-6.0

using System.IO;
using ExR.Format;
using System.IO.Compression;

namespace BufLib.Common.Compression
{
    public static class CompressionHelper
    {
        // 78DA
        public static byte[] ZlibCompress(byte[] buffer, CompressionLevel level)
        {
            using (var msOut = new MemoryStream(buffer.Length))
            {
                using (var stream = new ZLibStream(msOut, level, true))
                {
                    stream.Write(buffer, 0, buffer.Length);
                }
                return msOut.ToArray();
            }
        }

        public static byte[] ZlibUncompress(byte[] buffer)
        {
            using (var msIn = new MemoryStream(buffer))
            using (var msOut = new MemoryStream(buffer.Length * 2))
            using (var stream = new ZLibStream(msIn, CompressionMode.Decompress))
            {
                stream.CopyTo(msOut);
                stream.Flush();
                return msOut.ToArray();
            }
        }

        public static byte[] DeflateCompress(byte[] buffer, CompressionLevel level)
        {
            return buffer.DeflateCompress(level);
        }

        public static byte[] DeflateUncompress(byte[] buffer)
        {
            return buffer.DeflateUncompress();
        }

        public static byte[] NintendoCompress(byte[] buffer, Nintendo.Method method)
        {
            return Nintendo.Nintendo.Compress(buffer, method);
        }

        public static byte[] NintendoUnCompress(byte[] buffer)
        {
            return Nintendo.Nintendo.Decompress(buffer);
        }
    }
}

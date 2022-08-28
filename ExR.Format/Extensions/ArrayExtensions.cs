using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Runtime.InteropServices;
using System.IO.Compression;
using System.Linq;

namespace ExR.Format
{
    public static class StringExtensions
    {
        public static IEnumerable<string> Split(this string s, int maxChunkSize)
        {
            for (int i = 0; i < s.Length; i += maxChunkSize)
                yield return s.Substring(i, Math.Min(maxChunkSize, s.Length - i));
        }

        public static unsafe string XoaDauTiengViet(this string s)
        {
            fixed (char* p = s)
                for (int i = 0; i < s.Length; i++)
                {
                    switch (p[i])
                    {
                        case 'đ':
                            p[i] = 'd';
                            break;
                        case 'á':
                        case 'à':
                        case 'ả':
                        case 'ã':
                        case 'ạ':
                        case 'ắ':
                        case 'ằ':
                        case 'ẳ':
                        case 'ẵ':
                        case 'ặ':
                        case 'ă':
                        case 'ấ':
                        case 'ầ':
                        case 'ẩ':
                        case 'ẫ':
                        case 'ậ':
                        case 'â':
                            p[i] = 'a';
                            break;
                        case 'é':
                        case 'è':
                        case 'ẻ':
                        case 'ẽ':
                        case 'ẹ':
                        case 'ế':
                        case 'ề':
                        case 'ể':
                        case 'ễ':
                        case 'ệ':
                        case 'ê':
                            p[i] = 'e';
                            break;
                        case 'í':
                        case 'ì':
                        case 'ỉ':
                        case 'ĩ':
                        case 'ị':
                            p[i] = 'i';
                            break;
                        case 'ó':
                        case 'ò':
                        case 'ỏ':
                        case 'õ':
                        case 'ọ':
                        case 'ố':
                        case 'ồ':
                        case 'ổ':
                        case 'ỗ':
                        case 'ộ':
                        case 'ô':
                        case 'ớ':
                        case 'ờ':
                        case 'ở':
                        case 'ỡ':
                        case 'ợ':
                        case 'ơ':
                            p[i] = 'o';
                            break;
                        case 'ú':
                        case 'ù':
                        case 'ủ':
                        case 'ũ':
                        case 'ụ':
                        case 'ứ':
                        case 'ừ':
                        case 'ử':
                        case 'ữ':
                        case 'ự':
                        case 'ư':
                            p[i] = 'u';
                            break;
                        case 'ý':
                        case 'ỳ':
                        case 'ỷ':
                        case 'ỹ':
                        case 'ỵ':
                            p[i] = 'y';
                            break;
                        case 'Ð':
                        case 'Đ':
                            p[i] = 'D';
                            break;
                        case 'Á':
                        case 'À':
                        case 'Ả':
                        case 'Ã':
                        case 'Ạ':
                        case 'Ắ':
                        case 'Ằ':
                        case 'Ẳ':
                        case 'Ẵ':
                        case 'Ặ':
                        case 'Ă':
                        case 'Ấ':
                        case 'Ầ':
                        case 'Ẩ':
                        case 'Ẫ':
                        case 'Ậ':
                        case 'Â':
                            p[i] = 'A';
                            break;
                        case 'É':
                        case 'È':
                        case 'Ẻ':
                        case 'Ẽ':
                        case 'Ẹ':
                        case 'Ế':
                        case 'Ề':
                        case 'Ể':
                        case 'Ễ':
                        case 'Ệ':
                        case 'Ê':
                            p[i] = 'E';
                            break;
                        case 'Í':
                        case 'Ì':
                        case 'Ỉ':
                        case 'Ĩ':
                        case 'Ị':
                            p[i] = 'I';
                            break;
                        case 'Ó':
                        case 'Ò':
                        case 'Ỏ':
                        case 'Õ':
                        case 'Ọ':
                        case 'Ố':
                        case 'Ồ':
                        case 'Ổ':
                        case 'Ỗ':
                        case 'Ộ':
                        case 'Ô':
                        case 'Ớ':
                        case 'Ờ':
                        case 'Ở':
                        case 'Ỡ':
                        case 'Ợ':
                        case 'Ơ':
                            p[i] = 'O';
                            break;
                        case 'Ú':
                        case 'Ù':
                        case 'Ủ':
                        case 'Ũ':
                        case 'Ụ':
                        case 'Ứ':
                        case 'Ừ':
                        case 'Ử':
                        case 'Ữ':
                        case 'Ự':
                        case 'Ư':
                            p[i] = 'U';
                            break;
                        case 'Ý':
                        case 'Ỳ':
                        case 'Ỷ':
                        case 'Ỹ':
                        case 'Ỵ':
                            p[i] = 'Y';
                            break;
                    }
                }

            return s;
        }

        public static char XoaDauTiengViet(this char c)
        {
            switch (c)
            {
                case 'đ':
                    return 'd';
                case 'á':
                case 'à':
                case 'ả':
                case 'ã':
                case 'ạ':
                case 'ắ':
                case 'ằ':
                case 'ẳ':
                case 'ẵ':
                case 'ặ':
                case 'ă':
                case 'ấ':
                case 'ầ':
                case 'ẩ':
                case 'ẫ':
                case 'ậ':
                case 'â':
                    return 'a';
                case 'é':
                case 'è':
                case 'ẻ':
                case 'ẽ':
                case 'ẹ':
                case 'ế':
                case 'ề':
                case 'ể':
                case 'ễ':
                case 'ệ':
                case 'ê':
                    return 'e';
                case 'í':
                case 'ì':
                case 'ỉ':
                case 'ĩ':
                case 'ị':
                    return 'i';
                case 'ó':
                case 'ò':
                case 'ỏ':
                case 'õ':
                case 'ọ':
                case 'ố':
                case 'ồ':
                case 'ổ':
                case 'ỗ':
                case 'ộ':
                case 'ô':
                case 'ớ':
                case 'ờ':
                case 'ở':
                case 'ỡ':
                case 'ợ':
                case 'ơ':
                    return 'o';
                case 'ú':
                case 'ù':
                case 'ủ':
                case 'ũ':
                case 'ụ':
                case 'ứ':
                case 'ừ':
                case 'ử':
                case 'ữ':
                case 'ự':
                case 'ư':
                    return 'u';
                case 'ý':
                case 'ỳ':
                case 'ỷ':
                case 'ỹ':
                case 'ỵ':
                    return 'y';
                case 'Ð':
                case 'Đ':
                    return 'D';
                case 'Á':
                case 'À':
                case 'Ả':
                case 'Ã':
                case 'Ạ':
                case 'Ắ':
                case 'Ằ':
                case 'Ẳ':
                case 'Ẵ':
                case 'Ặ':
                case 'Ă':
                case 'Ấ':
                case 'Ầ':
                case 'Ẩ':
                case 'Ẫ':
                case 'Ậ':
                case 'Â':
                    return 'A';
                case 'É':
                case 'È':
                case 'Ẻ':
                case 'Ẽ':
                case 'Ẹ':
                case 'Ế':
                case 'Ề':
                case 'Ể':
                case 'Ễ':
                case 'Ệ':
                case 'Ê':
                    return 'E';
                case 'Í':
                case 'Ì':
                case 'Ỉ':
                case 'Ĩ':
                case 'Ị':
                    return 'I';
                case 'Ó':
                case 'Ò':
                case 'Ỏ':
                case 'Õ':
                case 'Ọ':
                case 'Ố':
                case 'Ồ':
                case 'Ổ':
                case 'Ỗ':
                case 'Ộ':
                case 'Ô':
                case 'Ớ':
                case 'Ờ':
                case 'Ở':
                case 'Ỡ':
                case 'Ợ':
                case 'Ơ':
                    return 'O';
                case 'Ú':
                case 'Ù':
                case 'Ủ':
                case 'Ũ':
                case 'Ụ':
                case 'Ứ':
                case 'Ừ':
                case 'Ử':
                case 'Ữ':
                case 'Ự':
                case 'Ư':
                    return 'U';
                case 'Ý':
                case 'Ỳ':
                case 'Ỷ':
                case 'Ỹ':
                case 'Ỵ':
                    return 'Y';
            }
            return c;
        }
    }

    public static class ByteArrayExtension
    {
        // https://ndportmann.com/breaking-records-with-core-3-0/
        // https://github.com/dotnet/runtime/issues/17837
        // Convert.FromHexString(string s);
        // Convert.ToHexString(byte[] inArray, int offset, int length);
        public static byte[] Concat(this byte[] part1, byte[] part2)
        {
            //Marshal.Copy()
            var result = new byte[part1.Length + part2.Length];
            Buffer.BlockCopy(part1, 0, result, 0, part1.Length);
            Buffer.BlockCopy(part2, 0, result, part1.Length, part2.Length);
            return result;
        }

        public static byte[] Concat(this byte[] part1, byte[] part2, byte[] part3)
        {
            var result = new byte[part1.Length + part2.Length + part3.Length];

            Buffer.BlockCopy(part1, 0, result, 0, part1.Length);
            Buffer.BlockCopy(part2, 0, result, part1.Length, part2.Length);
            Buffer.BlockCopy(part3, 0, result, part1.Length + part2.Length, part3.Length);
            
            return result;
        }

        //public static byte[] ConcatArrays(params byte[][] p)
        //{
        //    var position = 0;
        //    var outputArray = new byte[p.Sum(a => a.Length)];
        //    foreach (var curr in p)
        //    {
        //        Buffer.BlockCopy(curr, 0, outputArray, position, curr.Length);
        //        position += curr.Length;
        //    }
        //    return outputArray;
        //}

        public static string ReadAllText(this byte[] bytes)
        {
            return ReadAllText(bytes, Encoding.UTF8);
        }

        public static string ReadAllText(this byte[] bytes, Encoding encoding)
        {
            using (var ms = new MemoryStream(bytes))
            using (StreamReader sr = new StreamReader(ms, encoding, true))
            {
                return sr.ReadToEnd();
            }
        }

        public static byte[] DeflateCompress(this byte[] buffer)
        {
            return DeflateCompress(buffer, CompressionLevel.Optimal);
        }

        public static byte[] DeflateCompress(this byte[] buffer, CompressionLevel level)
        {
            using (var originalStream = new MemoryStream(buffer))
            using (var compressedStream = new MemoryStream(buffer.Length))
            using (var compressionStream = new DeflateStream(compressedStream, level))
            {
                originalStream.CopyTo(compressionStream);
                compressionStream.Flush();
                return compressedStream.ToArray();
            }
        }

        public static byte[] DeflateUncompress(this byte[] buffer)
        {
            using (var originalStream = new MemoryStream(buffer))
            using (var decompressed = new MemoryStream(buffer.Length * 2))
            using (var decompressionStream = new DeflateStream(originalStream, CompressionMode.Decompress))
            {
                decompressionStream.CopyTo(decompressed);
                decompressionStream.Flush();
                return decompressed.ToArray();
            }
        }

        public static ushort CRC16_CCITT(this byte[] data0, uint initital_crc = 0)
        {
            const uint CRC_16_CCITT = 0x11021;  // x^16+x^12+x^5+1
            const uint CRC_XOR_PATTERN = CRC_16_CCITT << 8;
            const uint CRC_CARRY_BIT = 0x01000000;
            const uint CHAR_BIT = 8;

            uint crcwk = initital_crc << 8;
            for (int i = 0; i < data0.Length; i++)
            {
                crcwk |= data0[i];
                for (int x = 0; x < CHAR_BIT; x++)
                {
                    crcwk <<= 1;
                    if ((crcwk & CRC_CARRY_BIT) != 0)
                        crcwk ^= CRC_XOR_PATTERN;
                }
            }
            return (ushort)(crcwk >> 8);
        }

        //public static byte[] BrotliCompress(this byte[] buffer)
        //{
        //    return BrotliCompress(buffer, CompressionLevel.Optimal);
        //}

        //public static byte[] BrotliCompress(this byte[] buffer, CompressionLevel level)
        //{
        //    using (var outMemory = new MemoryStream(buffer.Length))
        //    using (var inMemory = new MemoryStream(buffer))
        //    using (var zlib = new BrotliStream(inMemory, level))
        //    {
        //        zlib.CopyTo(outMemory);
        //        return outMemory.ToArray();
        //    }
        //}

        //public static byte[] BrotliUncompress(this byte[] buffer)
        //{
        //    using (var outMemory = new MemoryStream())
        //    using (var inMemory = new MemoryStream(buffer))
        //    using (var zlib = new BrotliStream(inMemory, CompressionMode.Decompress))
        //    {
        //        zlib.CopyTo(outMemory);
        //        return outMemory.ToArray();
        //    }
        //}

        // .net6 | https://github.com/ebiggers/libdeflate
        public static byte[] ZlibUncompress(this byte[] buffer)
        {
            return BufLib.Common.Compression.CompressionHelper.ZlibUncompress(buffer);
        }

        public static byte[] ZlibCompress(this byte[] buffer)
        {
            return BufLib.Common.Compression.CompressionHelper.ZlibCompress(buffer, CompressionLevel.Optimal);
        }
    }

    public static class IListExtensions
    {
        public static T GetLast<T>(this IList<T> list)
        {
            return list[list.Count - 1];
        }
    }
}

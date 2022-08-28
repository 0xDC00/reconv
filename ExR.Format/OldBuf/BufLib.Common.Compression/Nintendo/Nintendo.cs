// TODO:
//  - Refactor: https://github.com/IcySon55/Kuriimu/tree/master/src/Kontract/Compression
//  - cpp - https://gbatemp.net/threads/nintendo-ds-gba-compressors.313278/ / http://www.romhacking.net/utilities/826/
//  - cs  - https://github.com/AdmiralCurtiss/WfcPatcher/blob/master/blz.cs
//          https://github.com/SciresM/DLCTool/blob/master/DLCTool/3DS%20Builder/BLZ.cs
//  - ...   https://github.com/xdanieldzd/Scarlet/tree/master/Scarlet.IO.CompressionFormats
//  - ...   https://github.com/pleonex/tinke#compression - barubary/DSDecmp
//  - Yaz0  https://github.com/Gericom/EveryFileExplorer/blob/master/CommonCompressors/YAZ0.cs

using System;
using System.IO;
using System.Linq;

namespace BufLib.Common.Compression.Nintendo
{
    public enum Method : byte
    {
        LZ10 = 0x10,
        LZ11 = 0x11,
        Huff4 = 0x24,
        Huff8 = 0x28,
        RLE = 0x30,
        // LZ60 = 0x60
    }

    public class Nintendo
    {
        public static byte[] Compress(byte[] bytes, Method method)
        {
            // LZ10 compression can only handle files smaller than 16MB
            if (bytes.Length > 0xffffff)
                throw new Exception("File too big to be compressed with Nintendo compression!");

            switch (method)
            {
                case Method.LZ11:
                    return LZ11.Compress(bytes);
                case Method.LZ10:
                    return LZ10.Compress(bytes);
                case Method.RLE:
                    return InsertHeader(RLE.Compress(new MemoryStream(bytes)), method, bytes.Length);
                case Method.Huff4:
                    return InsertHeader(Huffman.Compress(new MemoryStream(bytes), 4, IO.Endian.BigEndian), method, bytes.Length);
                case Method.Huff8:
                    return InsertHeader(Huffman.Compress(new MemoryStream(bytes), 8), method, bytes.Length);
            }

            throw new Exception("method?");
        }

        public static byte[] Decompress(byte[] source)
        {
            var method = (Method)source[0];
            // int decomSize = source[3] << 16 | source[2] << 8 | source[1];

            switch (method)
            {
                case Method.LZ11:
                    return LZ11.Decompress(source);
                case Method.LZ10:
                    return LZ10.Decompress(source);
                case Method.RLE:
                    return RLE.Decompress(GetDataStream(source), GetDecompressedSize(source));
                case Method.Huff4:
                    return Huffman.Decompress(GetDataStream(source), 4, GetDecompressedSize(source), IO.Endian.BigEndian);
                case Method.Huff8:
                    return Huffman.Decompress(GetDataStream(source), 8, GetDecompressedSize(source));
            }

            throw new Exception("method?");
        }

        static MemoryStream GetDataStream(byte[] bytes)
        {
            var ms = new MemoryStream(bytes);
            ms.Position = 4;
            return ms;
        }

        static int GetDecompressedSize(byte[] source)
        {
            return source[3] << 16 | source[2] << 8 | source[1];
        }

        static byte[] InsertHeader(byte[] data, Method method, int inLength)
        {
            //var bytes = data.ToList();
            //bytes.Insert(0, (byte)((inLength >> 16) & 0xFF));
            //bytes.Insert(0, (byte)((inLength >> 8) & 0xFF));
            //bytes.Insert(0, (byte)((inLength) & 0xFF));
            //bytes.Insert(0, (byte)(method));
            //return bytes.ToArray();

            Array.Resize(ref data, data.Length + 4);
            data[0] = (byte)(method);
            data[1] = (byte)((inLength) & 0xFF);
            data[2] = (byte)((inLength >> 8) & 0xFF);
            data[3] = (byte)((inLength >> 16) & 0xFF);
            return data;
        }
    }
}

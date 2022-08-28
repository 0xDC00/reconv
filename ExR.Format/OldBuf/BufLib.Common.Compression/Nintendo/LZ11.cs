// LZSS-0x11
// header 4byte 1byte CODE, 3byte SIZE
// same? https://github.com/kwsch/pk3DS/blob/master/pk3DS.Core/CTR/LZSS.cs

using System;
using System.IO;

namespace BufLib.Common.Compression.Nintendo
{
    internal static class LZ11
    {
        public static byte[] Decompress(byte[] source)
        {
            // byte type = source[0]; // 0x11
            int decompSize = source[3] << 16 | source[2] << 8 | source[1];

            byte[] result = new byte[decompSize];
            int dstoffset = 0;
            int scroffset = 4;

            while (true)
            {
                byte header = source[scroffset++];
                for (int i = 0; i < 8; i++)
                {
                    if ((header & 0x80) == 0) result[dstoffset++] = source[scroffset++];
                    else
                    {
                        byte a = source[scroffset++];
                        int offset;
                        int length2;
                        if ((a >> 4) == 0)
                        {
                            byte b = source[scroffset++];
                            byte c = source[scroffset++];
                            length2 = (((a & 0xF) << 4) | (b >> 4)) + 0x11;
                            offset = (((b & 0xF) << 8) | c) + 1;
                        }
                        else if ((a >> 4) == 1)
                        {
                            byte b = source[scroffset++];
                            byte c = source[scroffset++];
                            byte d = source[scroffset++];
                            length2 = (((a & 0xF) << 12) | (b << 4) | (c >> 4)) + 0x111;
                            offset = (((c & 0xF) << 8) | d) + 1;
                        }
                        else
                        {
                            byte b = source[scroffset++];
                            length2 = (a >> 4) + 1;
                            offset = (((a & 0xF) << 8) | b) + 1;
                        }

                        for (int j = 0; j < length2; j++)
                        {
                            result[dstoffset] = result[dstoffset - offset];
                            dstoffset++;
                        }
                    }

                    if (dstoffset >= decompSize) return result;
                    header <<= 1;
                }
            }
        }

        public static byte[] Decompress(Stream stream)
        {
            using (var br = new BinaryReader(stream))
            {
                var flag = br.ReadUInt32();
                // var method = (flag & 0xff);
                int decompressedSize = (int)((flag & 0xffffff00) >> 8);

                byte[] result = new byte[decompressedSize];
                int dstoffset = 0;

                while (true)
                {
                    byte header = br.ReadByte();
                    for (int i = 0; i < 8; i++)
                    {
                        if ((header & 0x80) == 0) result[dstoffset++] = br.ReadByte();
                        else
                        {
                            byte a = br.ReadByte();
                            int offset;
                            int length2;
                            if ((a >> 4) == 0)
                            {
                                byte b = br.ReadByte();
                                byte c = br.ReadByte();
                                length2 = (((a & 0xF) << 4) | (b >> 4)) + 0x11;
                                offset = (((b & 0xF) << 8) | c) + 1;
                            }
                            else if ((a >> 4) == 1)
                            {
                                byte b = br.ReadByte();
                                byte c = br.ReadByte();
                                byte d = br.ReadByte();
                                length2 = (((a & 0xF) << 12) | (b << 4) | (c >> 4)) + 0x111;
                                offset = (((c & 0xF) << 8) | d) + 1;
                            }
                            else
                            {
                                byte b = br.ReadByte();
                                length2 = (a >> 4) + 1;
                                offset = (((a & 0xF) << 8) | b) + 1;
                            }

                            for (int j = 0; j < length2; j++)
                            {
                                result[dstoffset] = result[dstoffset - offset];
                                dstoffset++;
                            }
                        }

                        if (dstoffset >= decompressedSize) return result;
                        header <<= 1;
                    }
                }
            }
        }

        public static byte[] Compress(byte[] indata)
        {
            var ms = new MemoryStream();
            Compress(indata, ms);
            return ms.ToArray();
        }

        public static void Compress(Stream instream, Stream destination)
        {
            // Get the source length
            int sourceLength = (int)(instream.Length - instream.Position);

            // Read the source data into an array
            byte[] sourceArray = new byte[sourceLength];
            instream.Read(sourceArray, 0, sourceLength);

            Compress(sourceArray, destination);
        }

        // https://github.com/Barubary/dsdecmp/blob/master/CSharp/DSDecmp/Formats/Nitro/LZ11.cs#L292
        // It's CompressWithLA
        public unsafe static void Compress(byte[] indata, Stream outstream)
        {
            // make sure the decompressed size fits in 3 bytes.
            // There should be room for four bytes, however I'm not 100% sure if that can be used
            // in every game, as it may not be a built-in function.
            int inLength = indata.Length;
            if (inLength > 0xFFFFFF)
                throw new Exception("InputTooLargeException");

            // save the input data in an array to prevent having to go back and forth in a file
            // int compressedLength = 0;

            // Write out the header
            // Magic code & decompressed length
            outstream.WriteByte(0x11);
            outstream.WriteByte((byte)(inLength & 0xFF));
            outstream.WriteByte((byte)((inLength >> 8) & 0xFF));
            outstream.WriteByte((byte)((inLength >> 16) & 0xFF));

            fixed (byte* instart = &indata[0])
            {
                // we do need to buffer the output, as the first byte indicates which blocks are compressed.
                // this version does not use a look-ahead, so we do not need to buffer more than 8 blocks at a time.
                // (a block is at most 4 bytes long)
                byte[] outbuffer = new byte[8 * 4 + 1];
                outbuffer[0] = 0;
                int bufferlength = 1, bufferedBlocks = 0;
                int readBytes = 0;
                while (readBytes < inLength)
                {
        #region If 8 blocks are bufferd, write them and reset the buffer
                    // we can only buffer 8 blocks at a time.
                    if (bufferedBlocks == 8)
                    {
                        outstream.Write(outbuffer, 0, bufferlength);
                        // compressedLength += bufferlength;
                        // reset the buffer
                        outbuffer[0] = 0;
                        bufferlength = 1;
                        bufferedBlocks = 0;
                    }
        #endregion

                    // determine if we're dealing with a compressed or raw block.
                    // it is a compressed block when the next 3 or more bytes can be copied from
                    // somewhere in the set of already compressed bytes.
                    int disp;
                    int oldLength = Math.Min(readBytes, 0x1000);
                    int length = LZ10.GetOccurrenceLength(instart + readBytes, (int)Math.Min(inLength - readBytes, 0x10110),
                                                          instart + readBytes - oldLength, oldLength, out disp);

                    // length not 3 or more? next byte is raw data
                    if (length < 3)
                    {
                        outbuffer[bufferlength++] = *(instart + (readBytes++));
                    }
                    else
                    {
                        // 3 or more bytes can be copied? next (length) bytes will be compressed into 2 bytes
                        readBytes += length;

                        // mark the next block as compressed
                        outbuffer[0] |= (byte)(1 << (7 - bufferedBlocks));

                        if (length > 0x110)
                        {
                            // case 1: 1(B CD E)(F GH) + (0x111)(0x1) = (LEN)(DISP)
                            outbuffer[bufferlength] = 0x10;
                            outbuffer[bufferlength] |= (byte)(((length - 0x111) >> 12) & 0x0F);
                            bufferlength++;
                            outbuffer[bufferlength] = (byte)(((length - 0x111) >> 4) & 0xFF);
                            bufferlength++;
                            outbuffer[bufferlength] = (byte)(((length - 0x111) << 4) & 0xF0);
                        }
                        else if (length > 0x10)
                        {
                            // case 0; 0(B C)(D EF) + (0x11)(0x1) = (LEN)(DISP)
                            outbuffer[bufferlength] = 0x00;
                            outbuffer[bufferlength] |= (byte)(((length - 0x111) >> 4) & 0x0F);
                            bufferlength++;
                            outbuffer[bufferlength] = (byte)(((length - 0x111) << 4) & 0xF0);
                        }
                        else
                        {
                            // case > 1: (A)(B CD) + (0x1)(0x1) = (LEN)(DISP)
                            outbuffer[bufferlength] = (byte)(((length - 1) << 4) & 0xF0);
                        }
                        // the last 1.5 bytes are always the disp
                        outbuffer[bufferlength] |= (byte)(((disp - 1) >> 8) & 0x0F);
                        bufferlength++;
                        outbuffer[bufferlength] = (byte)((disp - 1) & 0xFF);
                        bufferlength++;
                    }
                    bufferedBlocks++;
                }

                // copy the remaining blocks to the output
                if (bufferedBlocks > 0)
                {
                    outstream.Write(outbuffer, 0, bufferlength);
                    // compressedLength += bufferlength;
                    /*/ make the compressed file 4-byte aligned.
                    while ((compressedLength % 4) != 0)
                    {
                        outstream.WriteByte(0);
                        compressedLength++;
                    }/**/
                }
            }
        }

    }
}

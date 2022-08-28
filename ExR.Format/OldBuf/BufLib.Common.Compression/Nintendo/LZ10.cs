// LZSS-0x10
// LZSS (LZ77): https://tcrf.net/Help:Contents/Finding_Content/Compression_Algorithms
// code from: https://github.com/Barubary/dsdecmp/blob/master/CSharp/DSDecmp/Formats/Nitro/LZ10.cs#L83
// Devil Survivor Overclocked, cmp, nhieu block (file) LZ10 nam canh nhau, align 4byte.

using System;
using System.IO;

namespace BufLib.Common.Compression.Nintendo
{
    public static class LZ10
    {
        public static byte[] Decompress(byte[] source)
        {
            int index = 0;
            return Decompress(source, ref index);
        }

        public static byte[] Decompress(byte[] source, ref int inOffset)
        {
            #region format definition from GBATEK/NDSTEK
            /*  Data header (32bit)
                  Bit 0-3   Reserved
                  Bit 4-7   Compressed type (must be 1 for LZ77)
                  Bit 8-31  Size of decompressed data
                Repeat below. Each Flag Byte followed by eight Blocks.
                Flag data (8bit)
                  Bit 0-7   Type Flags for next 8 Blocks, MSB first
                Block Type 0 - Uncompressed - Copy 1 Byte from Source to Dest
                  Bit 0-7   One data byte to be copied to dest
                Block Type 1 - Compressed - Copy N+3 Bytes from Dest-Disp-1 to Dest
                  Bit 0-3   Disp MSBs
                  Bit 4-7   Number of bytes to copy (minus 3)
                  Bit 8-15  Disp LSBs
             */
            #endregion

            // var method = header[0]; // 0x10
            int decompressedSize = source[inOffset + 3] << 16 | source[inOffset + 2] << 8 | source[inOffset + 1];

            int inLength = source.Length;
            inOffset = inOffset + 4; // 0

            // MemoryStream outstream = new MemoryStream(decompressedSize);
            byte[] result = new byte[decompressedSize];
            int dstoffset = 0;

            // the maximum 'DISP-1' is 0xFFF.
            const int bufferLength = 0x1000;
            byte[] buffer = new byte[bufferLength];
            int bufferOffset = 0;


            int currentOutSize = 0;
            int flags = 0, mask = 1;
            while (currentOutSize < decompressedSize)
            {
                // (throws when requested new flags byte is not available)
                #region Update the mask. If all flag bits have been read, get a new set.
                // the current mask is the mask used in the previous run. So if it masks the
                // last flag bit, get a new flags byte.
                if (mask == 1)
                {
                    if (inOffset >= inLength)
                        throw new Exception("Not enough data: " + currentOutSize.ToString() + ", " + decompressedSize.ToString());
                    flags = source[inOffset++];
                    if (flags < 0)
                        throw new Exception("Stream too short!");
                    mask = 0x80;
                }
                else
                {
                    mask >>= 1;
                }
                #endregion

                // bit = 1 <=> compressed.
                if ((flags & mask) > 0)
                {
                    // (throws when < 2 bytes are available)
                    #region Get length and displacement('disp') values from next 2 bytes
                    // there are < 2 bytes available when the end is at most 1 byte away
                    if (inOffset + 1 >= inLength)
                    {
                        // make sure the stream is at the end
                        if (inOffset < inLength)
                        {
                            inOffset++;
                        }
                        throw new Exception("Not enough data: " + currentOutSize.ToString() + ", " + decompressedSize.ToString());
                    }
                    byte byte1 = source[inOffset++];
                    byte byte2 = source[inOffset++];
                    if (byte2 < 0)
                        throw new Exception("Stream too short!");

                    // the number of bytes to copy
                    int length = byte1 >> 4;
                    length += 3;

                    // from where the bytes should be copied (relatively)
                    int disp = ((byte1 & 0x0F) << 8) | byte2;
                    disp += 1;

                    if (disp > currentOutSize)
                        throw new Exception("Cannot go back more than already written. "
                                + "DISP = 0x" + disp.ToString("X") + ", #written bytes = 0x" + currentOutSize.ToString("X")
                                + " at 0x" + (inOffset - 2).ToString("X"));
                    #endregion

                    int bufIdx = bufferOffset + bufferLength - disp;
                    for (int i = 0; i < length; i++)
                    {
                        byte next = buffer[bufIdx % bufferLength];
                        bufIdx++;
                        result[dstoffset++] = next; // outstream.WriteByte(next);
                        buffer[bufferOffset] = next;
                        bufferOffset = (bufferOffset + 1) % bufferLength;
                    }
                    currentOutSize += length;
                }
                else
                {
                    if (inOffset >= inLength)
                        throw new Exception("Not enough data: " + currentOutSize.ToString() + ", " + decompressedSize.ToString());
                    byte next = source[inOffset++];
                    if (next < 0)
                        throw new Exception("Stream too short!");

                    currentOutSize++;
                    result[dstoffset++] = next;
                    buffer[bufferOffset] = next;
                    bufferOffset = (bufferOffset + 1) % bufferLength;
                }
            }

            return result;
        }

        public static byte[] Decompress(Stream instream)
        {
            #region format definition from GBATEK/NDSTEK
            /*  Data header (32bit)
                  Bit 0-3   Reserved
                  Bit 4-7   Compressed type (must be 1 for LZ77)
                  Bit 8-31  Size of decompressed data
                Repeat below. Each Flag Byte followed by eight Blocks.
                Flag data (8bit)
                  Bit 0-7   Type Flags for next 8 Blocks, MSB first
                Block Type 0 - Uncompressed - Copy 1 Byte from Source to Dest
                  Bit 0-7   One data byte to be copied to dest
                Block Type 1 - Compressed - Copy N+3 Bytes from Dest-Disp-1 to Dest
                  Bit 0-3   Disp MSBs
                  Bit 4-7   Number of bytes to copy (minus 3)
                  Bit 8-15  Disp LSBs
             */
            #endregion

            byte[] header = new byte[4];
            instream.Read(header, 0, 4);
            // var method = header[0]; // 0x10
            int decompressedSize = header[3] << 16 | header[2] << 8 | header[1];

            long inLength = instream.Length;
            long inOffset = instream.Position; // 0

            // MemoryStream outstream = new MemoryStream(decompressedSize);
            byte[] result = new byte[decompressedSize];
            int dstoffset = 0;


            // the maximum 'DISP-1' is 0xFFF.
            const int bufferLength = 0x1000;
            byte[] buffer = new byte[bufferLength];
            int bufferOffset = 0;


            int currentOutSize = 0;
            int flags = 0, mask = 1;
            while (currentOutSize < decompressedSize)
            {
                // (throws when requested new flags byte is not available)
                #region Update the mask. If all flag bits have been read, get a new set.
                // the current mask is the mask used in the previous run. So if it masks the
                // last flag bit, get a new flags byte.
                if (mask == 1)
                {
                    if (inOffset >= inLength)
                        throw new Exception("Not enough data: " + currentOutSize.ToString() + ", " + decompressedSize.ToString());
                    flags = instream.ReadByte(); inOffset++;
                    if (flags < 0)
                        throw new Exception("Stream too short!");
                    mask = 0x80;
                }
                else
                {
                    mask >>= 1;
                }
                #endregion

                // bit = 1 <=> compressed.
                if ((flags & mask) > 0)
                {
                    // (throws when < 2 bytes are available)
                    #region Get length and displacement('disp') values from next 2 bytes
                    // there are < 2 bytes available when the end is at most 1 byte away
                    if (inOffset + 1 >= inLength)
                    {
                        // make sure the stream is at the end
                        if (inOffset < inLength)
                        {
                            instream.ReadByte(); inOffset++;
                        }
                        throw new Exception("Not enough data: " + currentOutSize.ToString() + ", " + decompressedSize.ToString());
                    }
                    int byte1 = instream.ReadByte(); inOffset++;
                    int byte2 = instream.ReadByte(); inOffset++;
                    if (byte2 < 0)
                        throw new Exception("Stream too short!");

                    // the number of bytes to copy
                    int length = byte1 >> 4;
                    length += 3;

                    // from where the bytes should be copied (relatively)
                    int disp = ((byte1 & 0x0F) << 8) | byte2;
                    disp += 1;

                    if (disp > currentOutSize)
                        throw new Exception("Cannot go back more than already written. "
                                + "DISP = 0x" + disp.ToString("X") + ", #written bytes = 0x" + currentOutSize.ToString("X")
                                + " at 0x" + (instream.Position - 2).ToString("X"));
                    #endregion

                    int bufIdx = bufferOffset + bufferLength - disp;
                    for (int i = 0; i < length; i++)
                    {
                        byte next = buffer[bufIdx % bufferLength];
                        bufIdx++;
                        result[dstoffset++] = next; // outstream.WriteByte(next);
                        buffer[bufferOffset] = next;
                        bufferOffset = (bufferOffset + 1) % bufferLength;
                    }
                    currentOutSize += length;
                }
                else
                {
                    if (inOffset >= inLength)
                        throw new Exception("Not enough data: " + currentOutSize.ToString() + ", " + decompressedSize.ToString());
                    byte next = (byte)instream.ReadByte(); inOffset++;
                    if (next < 0)
                        throw new Exception("Stream too short!");

                    currentOutSize++;
                    result[dstoffset++] = next; // outstream.WriteByte((byte)next);
                    buffer[bufferOffset] = next;
                    bufferOffset = (bufferOffset + 1) % bufferLength;
                }
            }

            return result;
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

#if BRIDGE_DOTNET
        // code from: https://github.com/nickworonekin/puyotools/blob/master/PuyoTools.Modules/Compression/Formats/Lz10Compression.cs#L97
        // nén thấp hơn của DSDecmp
        public static void Compress(byte[] sourceArray, Stream destination)
        {
            // Get the source length
            int sourceLength = sourceArray.Length;

            // LZ10 compression can only handle files smaller than 16MB
            //if (sourceLength > 0xFFFFFF)
            //{
            //    throw new Exception("Source is too large. LZ10 compression can only compress files smaller than 16MB.");
            //}

            // Set the source and destination pointers
            int sourcePointer = 0x0;
            // int destinationPointer = 0x4;

            // Initalize the LZ dictionary
            LzWindowDictionary dictionary = new LzWindowDictionary();
            dictionary.SetWindowSize(0x1000);
            dictionary.SetMaxMatchAmount(0xF + 3);

            // Write out the header
            // Magic code & decompressed length
            // // PTStream.WriteInt32(destination, 0x10 | (sourceLength << 8));
            // https://github.com/Barubary/dsdecmp/blob/master/CSharp/DSDecmp/Formats/Nitro/LZ10.cs#L341
            destination.WriteByte(0x10);
            destination.WriteByte((byte)(sourceLength & 0xFF));
            destination.WriteByte((byte)((sourceLength >> 8) & 0xFF));
            destination.WriteByte((byte)((sourceLength >> 16) & 0xFF));

            // Start compression
            while (sourcePointer < sourceLength)
            {
                using (MemoryStream buffer = new MemoryStream())
                {
                    byte flag = 0;

                    for (int i = 7; i >= 0; i--)
                    {
                        // Search for a match
                        int[] match = dictionary.Search(sourceArray, (uint)sourcePointer, (uint)sourceLength);

                        if (match[1] > 0) // There is a match
                        {
                            flag |= (byte)(1 << i);

                            buffer.WriteByte((byte)((((match[1] - 3) & 0xF) << 4) | (((match[0] - 1) & 0xFFF) >> 8)));
                            buffer.WriteByte((byte)((match[0] - 1) & 0xFF));

                            dictionary.AddEntryRange(sourceArray, sourcePointer, match[1]);
                            dictionary.SlideWindow(match[1]);

                            sourcePointer += match[1];
                        }
                        else // There is not a match
                        {
                            buffer.WriteByte(sourceArray[sourcePointer]);

                            dictionary.AddEntry(sourceArray, sourcePointer);
                            dictionary.SlideWindow(1);

                            sourcePointer++;
                        }

                        // Check to see if we reached the end of the file
                        if (sourcePointer >= sourceLength)
                            break;
                    }

                    // Flush the buffer and write it to the destination stream
                    destination.WriteByte(flag);

                    buffer.Position = 0;
                    while (buffer.Position < buffer.Length)
                    {
                        byte value = (byte)buffer.ReadByte();
                        destination.WriteByte(value);
                    }

                    // destinationPointer += (int)buffer.Length + 1;
                }
            }
        }
#else
        public static unsafe void Compress(byte[] indata, Stream outStream)
        {
            // make sure the decompressed size fits in 3 bytes.
            // There should be room for four bytes, however I'm not 100% sure if that can be used
            // in every game, as it may not be a built-in function.
            long inLength = indata.Length;

            // save the input data in an array to prevent having to go back and forth in a file
            //int compressedLength = 0;

            // Write out the header
            // Magic code & decompressed length
            outStream.WriteByte(0x10);
            outStream.WriteByte((byte)(inLength & 0xFF));
            outStream.WriteByte((byte)((inLength >> 8) & 0xFF));
            outStream.WriteByte((byte)((inLength >> 16) & 0xFF));

            fixed (byte* instart = &indata[0])
            {
                // we do need to buffer the output, as the first byte indicates which blocks are compressed.
                // this version does not use a look-ahead, so we do not need to buffer more than 8 blocks at a time.
                byte[] outbuffer = new byte[8 * 2 + 1];
                outbuffer[0] = 0;
                int bufferlength = 1, bufferedBlocks = 0;
                int readBytes = 0;
                while (readBytes < inLength)
                {
                    #region If 8 blocks are bufferd, write them and reset the buffer
                    // we can only buffer 8 blocks at a time.
                    if (bufferedBlocks == 8)
                    {
                        outStream.Write(outbuffer, 0, bufferlength);
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
                    int length = GetOccurrenceLength(instart + readBytes, (int)Math.Min(inLength - readBytes, 0x12),
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

                        outbuffer[bufferlength] = (byte)(((length - 3) << 4) & 0xF0);
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
                    outStream.Write(outbuffer, 0, bufferlength);
                    // compressedLength += bufferlength;
                }
            }
        }

        public static unsafe int GetOccurrenceLength(byte* newPtr, int newLength, byte* oldPtr, int oldLength, out int disp, int minDisp = 1)
        {
            disp = 0;
            if (newLength == 0)
                return 0;
            int maxLength = 0;
            // try every possible 'disp' value (disp = oldLength - i)
            for (int i = 0; i < oldLength - minDisp; i++)
            {
                // work from the start of the old data to the end, to mimic the original implementation's behaviour
                // (and going from start to end or from end to start does not influence the compression ratio anyway)
                byte* currentOldStart = oldPtr + i;
                int currentLength = 0;
                // determine the length we can copy if we go back (oldLength - i) bytes
                // always check the next 'newLength' bytes, and not just the available 'old' bytes,
                // as the copied data can also originate from what we're currently trying to compress.
                for (int j = 0; j < newLength; j++)
                {
                    // stop when the bytes are no longer the same
                    if (*(currentOldStart + j) != *(newPtr + j))
                        break;
                    currentLength++;
                }

                // update the optimal value
                if (currentLength > maxLength)
                {
                    maxLength = currentLength;
                    disp = oldLength - i;

                    // if we cannot do better anyway, stop trying.
                    if (maxLength == newLength)
                        break;
                }
            }
            return maxLength;
        }
#endif
    }
}

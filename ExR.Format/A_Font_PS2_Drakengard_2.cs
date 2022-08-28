using BufLib.Common.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Zio;

namespace ExR.Format
{
    [Plugin("com.dc.Drakengard2_Font", "[Font] Drakengard2 (from ELF)", @"Extract/Repack
DDS: 8bit Alpha

INCOMPLETE

00000000  36010100 E0001800 10040000 20010000")]
    class A_Font_PS2_Drakengard_2 : TextFormat
    {
        public override List<Line> ExtractText(byte[] bytes)
        {
            using (var ms = new MemoryStream(bytes))
            using (var br = new EndianBinaryReader(ms))
            {
                var header = br.ReadStruct<Header>();
                br.BaseStream.Position += 0x80; // 0000FFFF
                br.BaseStream.Position += 0x80; // 00 -> 19
                var glyphs = br.ReadStructs<Glyph>(header.NumGlyph);

                br.BaseStream.Position = header.PixelDataOffset;

                /* read all tiles to canvas */
                var numColumn = header.TileWidthMax;
                var numRow = header.NumGlyph / numColumn;
                if (header.NumGlyph % numColumn != 0)
                    numRow += 1;
                var canvasWidth = numColumn * header.TileWidthMax;
                var canvasHeight = numRow * header.TileWidthMax; /*TileHeightMax*/

                /* Create canvas */
                var dds = new DDS(canvasWidth, canvasHeight, DDS.PixelFormat.DXGI_FORMAT_A8_UNORM);
                var canvasPixels = dds.Pixels;
                int curentRow = 0, currentColumn = 0;
                //var tileSize = (header.TileWidthMax * header.TileWidthMax) / 2; // 4ppp
                var tileSize = header.tileByteCount;
                var sizeOfRow = tileSize * numColumn;

                var numGlyph = (bytes.Length - br.BaseStream.Position) / tileSize; // 70 * 2 = E0
                var hw = header.TileWidthMax / 2;
                for (int i = 0; i < numGlyph; i++)
                {
                    // draw one
                    var tilePixels = br.ReadBytes(tileSize);
                    var pixelSeek = canvasWidth - header.TileWidthMax;
                    var destOffset = currentColumn * header.TileWidthMax + curentRow * sizeOfRow;
                    int srcOffset = 0;
                    for (int y = 0; y < header.TileWidthMax /*Height*/ ; y++)
                    {
                        // write line
                        for (int x = 0; x < hw; x++)
                        {
                            //int srcOffset = (int)((x * 1) + (y * header.TileWidthMax * 1)); // 1byte = 8bit.
                            var pix = tilePixels[srcOffset++];
                            //pix = (byte)ReverseBits(pix, 8);
                            var b1 = pix & 0x0F;
                            var b2 = pix >> 4;

                            //b1 = (byte)ReverseBits(b1, 4);
                            //b2 = (byte)ReverseBits(b2, 4);

                            //if (b1 < 4)
                            //    b1 = 0;
                            //if (b2 < 4)
                            //    b2 = 0;


                            if (b1 != 0)
                                b1 += 0xD0;
                            if (b2 != 0)
                                b2 += 0xD0;


                            // 2 pixels
                            canvasPixels[destOffset++] = (byte)b1; // 4bit
                            canvasPixels[destOffset++] = (byte)b2; // 4bit
                            //destOffset++;

                            //Console.WriteLine(destOffset);
                        }
                        //Console.WriteLine();
                        destOffset = destOffset + (pixelSeek * 1);
                    }

                    // move next
                    currentColumn++;
                    if (currentColumn == numColumn)
                    {
                        currentColumn = 0;
                        curentRow++;
                        break;
                    }
                    //if (i == 2) break;
                }

                /* write tga image */
                var path = Path.ChangeExtension(CurrentFilePath, null);
                var tgaData = dds.Build();
                var output = path + ".DDS";
                FsOut.WriteAllBytes(output, tgaData);

                /* write glyph info */
                return null;
            }
        }

        private static long ReverseBits(long value, int bitCount)
        {
            long result = 0;

            for (var i = 0; i < bitCount; i++)
            {
                result <<= 1;
                result |= (byte)(value & 1);
                value >>= 1;
            }

            return result;
        }

        public override byte[] RepackText(List<Line> lines)
        {
            throw new NotImplementedException();
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct Header
        {
            public short Magic; // FU
            public short unk0;
            public short NumGlyph;
            public short TileWidthMax;
            public int PixelDataOffset;
            public int tileByteCount;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct Glyph
        {
            public short xAdv;
            public char Char;
        }
    }
}

using BufLib.Common.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Zio;

namespace ExR.Format
{

    [Plugin("com.dc.IdeaFactoryFFU", "[Font] Idea Factory (.ffu - magic: UF)", @"Extract/Repack
DDS: 8bit Alpha

00000000  55462F02 67720000 03242401 01002401  UF/.gr...$$...$.")]
    class A_Font_FFU : TextFormat
    {
        public override bool Init(Dictionary<string, object> dict)
        {
            Extensions = new string[] { ".ffu" };
            return true;
        }

        public override List<Line> ExtractText(byte[] bytes)
        {
            using (var ms = new MemoryStream(bytes))
            using (var br = new EndianBinaryReader(ms))
            {
                /* read ffu header */
                var header = br.ReadStruct<Header>();

                br.BaseStream.Position = header.OffsetCodeRange;
                var ranges = br.ReadStructs<CodeRange>(header.NumRange);

                br.BaseStream.Position = header.OffsetGlyphInfo;
                var glyphs = br.ReadStructs<Glyph>(header.NumGlyph);

                // tile Rectangle -> Square (for Photoshop grid)
                var realW = header.TileWidthMax;
                var realH = header.TileHeightMax;
                if (header.TileWidthMax > header.TileHeightMax)
                    header.TileHeightMax = header.TileWidthMax;
                else
                    header.TileWidthMax = header.TileHeightMax;

                /* read all tiles to canvas */
                var numColumn = header.TileWidthMax;
                var numRow = header.NumGlyph / numColumn;
                if (header.NumGlyph % numColumn != 0)
                    numRow += 1;
                var canvasWidth = numColumn * header.TileWidthMax;
                var canvasHeight = numRow * header.TileHeightMax;

                var dds = new DDS(canvasWidth, canvasHeight, DDS.PixelFormat.DXGI_FORMAT_A8_UNORM);
                //var canvasPixels = new byte[canvasWidth * canvasHeight];
                var canvasPixels = dds.Pixels;

                int curentRow = 0, currentColumn = 0;
                var sizeOfRow = header.TileWidthMax * header.TileHeightMax * numColumn;
                var odds = new List<string>(); // unk pixel data.
                foreach (var glyph in glyphs)
                {
                    // test
                    var tileOffset = glyph.RelOffset + header.OffsetImageData;
                    if (tileOffset != br.BaseStream.Position)
                        Console.WriteLine("tileOffset-Why???");

                    var tilePixels = br.ReadBytes(glyph.TileSize);
                    var tileWidth = glyph.TileSize / glyph.Height;

                    /* write tile_data to canvas */
                    // ------------[line1]sssssssssssEEK
                    // ssssssssssss[line2]
                    var pixelSeek = canvasWidth - tileWidth;
                    var destOffset = currentColumn * header.TileWidthMax + curentRow * sizeOfRow;
                    for (int y = 0; y < glyph.Height; y++)
                    {
                        // write line
                        for (int x = 0; x < tileWidth; x++)
                        {
                            int srcOffset = (x * 1) + (y * tileWidth * 1); // 1pixel = 1byte = 8bit.
                            canvasPixels[destOffset++] = tilePixels[srcOffset];
                        }

                        // set width_mark - WHITE
                        if (tileWidth < header.TileWidthMax)
                            canvasPixels[destOffset] = 0xFF; // current = width+1
                        else
                            Console.WriteLine("width_mark???");

                        destOffset = destOffset + (pixelSeek * 1); // 1pixel = 1byte = 8bit; seek to next line
                    }

                    // test
                    var odd = glyph.TileSize % glyph.Height;
                    if (odd != 0)
                    {
                        // why??? odd=2???, value=random!
                        //var at = curentRow * numColumn + currentColumn;
                        //Console.WriteLine(at + ". odd: " + glyph.TileSize % glyph.Height);
                        br.BaseStream.Position -= odd;
                        var oddVal = br.ReadBytes(odd);
                        odds.Add(oddVal.ByteArrayToString());
                    }
                    else
                        odds.Add(string.Empty);

                    // move next
                    currentColumn++;
                    if (currentColumn == numColumn)
                    {
                        currentColumn = 0;
                        curentRow++;
                    }
                }

                var lines = new List<Line>();
                lines.Add(new Line(glyphs.Length.ToString()));
                var path = Path.ChangeExtension(CurrentFilePath, null);

                /* write tga image */
                var tgaData = dds.Build();
                var output = path + ".DDS";
                FsOut.WriteAllBytes(output, tgaData);

                /* write glyph info */
                var txtFnt = CreateMiniFnt(header, ranges, glyphs, odds, realW, realH);
                output = path + ".txt";
                FsOut.WriteAllText(output, txtFnt);

                br.BaseStream.Position = 0;
                var headerAndData = br.ReadBytes(header.OffsetImageData);

                br.BaseStream.Position = header.OffsetUnk;
                var lastBlock = br.ReadBytes((int)(br.BaseStream.Length - header.OffsetUnk));
                _PushEnd(lines, headerAndData);
                _PushEnd(lines, lastBlock);

                return lines;
            }
        }

        public override byte[] RepackText(List<Line> lines)
        {
            using (var ms = new MemoryStream(_10MB))
            using (var br = new EndianBinaryReader(ms))
            using (var bw = new EndianBinaryWriter(ms))
            {
                var lastBlock = _PopEnd(lines);
                var headerAndData = _PopEnd(lines);

                /* 1 */
                /* write header+ */
                bw.Write(headerAndData);

                ms.Position = 0;
                var header = br.ReadStruct<Header>();

                /* 2 */
                /* update glyph info */
                var glyphs = new List<GlyphInfo>();
                var currentRelativeOffset = 0;
                bw.BaseStream.Position = header.OffsetGlyphInfo;
                CurrentFilePath = Path.ChangeExtension(CurrentFilePath, ".txt");
                using (var sr = new StreamReader(new MemoryStream(ReadCurrentFileData()), Encoding.UTF8, true))
                {
                    var info = sr.ReadToEnd().Split('\r', '\n');

                    foreach (var line in info.Skip(1)) // firstLine is info
                    {
                        if (line.Length > 0)
                        {
                            var glyph = new GlyphInfo(line);
                            glyph.RelOffset = currentRelativeOffset;
                            glyphs.Add(glyph);

                            bw.Write(glyph.xAdv); // byte
                            bw.Write(glyph.Height); // byte
                            bw.Write(glyph.TileSize); // short
                            bw.Write(glyph.RelOffset); // int

                            currentRelativeOffset += glyph.TileSize;
                        }
                    }

                    /* test */
                    if (glyphs.Count != header.NumGlyph)
                    {
                        Console.WriteLine("NewGlyph???");
                        return null;
                    }

                    /* 3 */
                    /* loop again - read all tiles */
                    // after GlyphInfo = RawPixelData
                    if (header.TileWidthMax > header.TileHeightMax) // Hack: for PS
                        header.TileHeightMax = header.TileWidthMax;
                    else
                        header.TileWidthMax = header.TileHeightMax;

                    CurrentFilePath = Path.ChangeExtension(CurrentFilePath, ".DDS");
                    var ddsRaw = ReadCurrentFileData();
                    var dds = new DDS(ddsRaw);

                    if (dds.Format != DDS.PixelFormat.DXGI_FORMAT_A8_UNORM)
                        throw new Exception("[DDS] DXGI_FORMAT_A8_UNORM expected!");

                    var tgaPixels = dds.Pixels;

                    var numColumn = header.TileWidthMax;
                    var canvasWidth = numColumn * header.TileWidthMax;

                    int curentRow = 0, currentColumn = 0;
                    var sizeOfRow = header.TileWidthMax * header.TileHeightMax * numColumn;
                    using (var canvas = new BinaryReader(new MemoryStream(tgaPixels)))
                    {
                        foreach (var glyph in glyphs)
                        {
                            /* read tile data */
                            var destOffset = currentColumn * header.TileWidthMax + curentRow * sizeOfRow;
                            for (int y = 0; y < glyph.Height; y++)
                            {
                                canvas.BaseStream.Position = destOffset;
                                var OneLinePixels = canvas.ReadBytes(glyph.Width);
                                //tiles.AddRange(line);
                                bw.Write(OneLinePixels);

                                destOffset += canvasWidth; // next line
                            }

                            if (glyph.Odd.Length > 0)
                                bw.Write(glyph.Odd); // safe

                            currentColumn++;
                            if (currentColumn == numColumn)
                            {
                                currentColumn = 0;
                                curentRow++;
                            }
                        }
                    }

                    /* 4 */
                    // after RawPixelData = unk
                    var lastBlockOffset = (int)bw.BaseStream.Position;
                    bw.Write(lastBlock);
                    bw.BaseStream.Position = 0x10;
                    bw.Write(lastBlockOffset);
                }

                return ms.ToArray();
            }
        }

        static string CreateMiniFnt(Header header, CodeRange[] ranges, Glyph[] glyphs, List<string> odds, int realW, int realH)
        {
            var encoding = Encoding.UTF8;
            if (header.One == 0) // not sure.
                encoding = Encoding.GetEncoding(932);

            var sb = new StringBuilder();
            sb.AppendLine(realW + "x" + realH + " -> " + header.TileWidthMax + "x" + header.TileHeightMax);
            int index = 0;
            foreach (var range in ranges)
            {
                for (var i = range.CodeStart; i < range.CodeEnd; i++)
                {
                    var glyph = glyphs[index];
                    var tileWidth = glyph.TileSize / glyph.Height;

                    var raw = BitConverter.GetBytes(i.Reverse());
                    var ch = encoding.GetString(raw).Trim('\0');
                    if (ch.Length > 1)
                        Console.WriteLine("Char???");

                    var charOrCode = GlyphInfo.EncodeChar(ch[0]);
                    var line = string.Format("char id={0,-4} width={1,-4} height={2, -4} xAdv={3, -4} odd={4}", charOrCode, tileWidth, glyph.Height, glyph.xAdv, odds[index]);
                    sb.AppendLine(line);

                    /* test */
                    var ci = new GlyphInfo(line);
                    if (ci.Char != ch[0])
                        Console.WriteLine("CharCompare???");
                    if (ci.Width != tileWidth)
                        Console.WriteLine("WidthCompare???");
                    if (ci.Height != glyph.Height)
                        Console.WriteLine("HeightCompare???");
                    if (ci.xAdv != glyph.xAdv)
                        Console.WriteLine("xAdvCompare???");

                    index++;
                }
            }

            return sb.ToString();
        }

        #region Structure
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct Header
        {
            public short Magic; // FU
            public short NumRange;
            public int NumGlyph;
            public byte ImageType;
            public byte TileWidthMax;
            public byte TileHeightMax;
            public byte True;
            public short One;
            public byte TileHeightMax_2;
            public byte True2;
            public int OffsetUnk;
            public int OffsetCodeRange;
            public int OffsetGlyphInfo;
            public int OffsetImageData;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct CodeRange
        {
            public int CodeStart;
            public int CodeEnd;
            public int Index;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct Glyph
        {
            public byte xAdv; // NoAlpha
            public byte Height;
            public short TileSize;
            public int RelOffset;
        }

        class GlyphInfo
        {
            public byte xAdv; // NoAlpha
            public byte Height;
            public short TileSize;
            public int RelOffset;
            public char Char;
            public int Width;
            public byte[] Odd;

            public GlyphInfo(string line)
            {
                var data = line.Split(new string[] { " " }, System.StringSplitOptions.RemoveEmptyEntries);
                var spell = new char[] { '=' };
                var value = data[1].Split(spell, 2)[1];
                Char = DecodeChar(value);

                value = data[2].Split('=')[1];
                Width = int.Parse(value);

                value = data[3].Split('=')[1];
                Height = byte.Parse(value);

                value = data[4].Split('=')[1];
                xAdv = byte.Parse(value);

                value = data[5].Split('=')[1];
                Odd = value.HexStringToByteArray();

                TileSize = (short)(Width * Height + Odd.Length);
                RelOffset = 0;
            }

            internal static string EncodeChar(char Char)
            {
                // http://www.degraeve.com/reference/urlencoding.php
                var text = Char.ToString();
                if (Char == '\r')
                {
                    text = "13"; // 13 0D
                }
                else if (Char == '\n')
                {
                    text = "10"; // 10 0A
                }
                else if (Char == '\t')
                {
                    text = "09"; // 9 09
                }
                else if (Char == ' ')
                {
                    text = "32"; // 20 32
                }
                else if (Char == 0xa0)
                {
                    return "160";
                }
                return text;
            }

            internal static char DecodeChar(string value)
            {
                if (value.Length > 1)
                {
                    return (char)int.Parse(value);
                }
                else
                {
                    return value[0];
                }
            }
        }
        #endregion
    }
}

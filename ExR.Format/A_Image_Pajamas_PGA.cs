/*
https://github.com/paulvortex/RwgTex
*/
using System;
using System.Collections.Generic;
using System.IO;
using Zio;

namespace ExR.Format
{
    [Plugin("com.dc.PajamasPGA", "[Image] Pajamas Soft .EPA - magic: EP", @"Extract/Repack/Create*
Header: 00000000  45500101 02000000 B0010000 80020000  EP......°...€...

+---------------------------------------+-------------------------+
|                  EPA                  |           DDS           |
+---------------------------------------+-------------------------+
| B8G8R8 24bpp *                        | B8G8R8 24bpp unsigned   |
| palette RGB 8bpp | 256 colors         |                         |
| B5G6R5 16bpp                          |                         |
+---------------------------------------+-------------------------+
| B8G8R8A8 32bpp *                      | B8G8R8A8 32bpp unsigned |
| palette RGB 8bpp | 256 colors + Alpha |                         |
+---------------------------------------+-------------------------+

Note: https://github.com/morkt/GARbro")] // https://www.tablesgenerator.com/
    class A_Image_Pajamas_PGA : TextFormat
    {
        public override bool Init(Dictionary<string, object> dict)
        {
            Extensions = new string[] { ".epa" };
            return true;
        }

        public override List<Line> ExtractText(byte[] bytes)
        {
            var lines = new List<Line>();
            using (var ms = new MemoryStream(bytes))
            using (var br = new BinaryReader(ms))
            {
                var magic = br.ReadBytes(2); // 0 1
                br.BaseStream.Position += 1;
                var mode = br.ReadByte(); // 3, diff_flag
                var bpp = br.ReadByte(); // 4
                br.BaseStream.Position += 3; // 1
                var width = br.ReadInt32(); // 8
                var height = br.ReadInt32();

                switch (mode)
                {
                    case 1: // Usual
                        br.BaseStream.Position = 0x10;
                        break;

                    case 2: // Difference
                        br.BaseStream.Position = 0x18;
                        break;

                    default: // Unknown
                        Console.WriteLine("[Warning] mode: " + mode);
                        break;
                }
                var headerSize = (int)br.BaseStream.Position;
                br.BaseStream.Position = 0;
                var hdr = br.ReadBytes(headerSize);
                

                // Read palette
                byte[] palette_raw_bgr = null; // Bgr * 256 = 768
                bool is_alpha = false;
                int pixel_size;
                var fm = string.Empty;
                switch (bpp)
                {
                    case 0:
                        fm = "8bit Indexed";
                        Console.WriteLine("8bit Indexed (Bgr24) => Bgr24");
                        bpp = 8;
                        pixel_size = 1; // 1 byte, Indexed BGR
                        palette_raw_bgr = br.ReadBytes(768);
                        break;
                    case 1:
                        fm = "Bgr24";
                        Console.WriteLine("Bgr24");
                        bpp = 24;
                        pixel_size = 3; // 3 byte, Bgr24
                        break;
                    case 2:
                        fm = "Bgra32";
                        Console.WriteLine("Bgra32");
                        bpp = 32;
                        pixel_size = 4; // 4 byte
                        break;
                    case 3:
                        fm = "Bgr565";
                        Console.WriteLine("Bgr565 => Bgr24");
                        bpp = 16;
                        pixel_size = 2; // 2byte, Bgr565
                        break;
                    case 4:
                        fm = "8bit Indexed + Alpha";
                        Console.WriteLine("8bit Indexed (Bgr24) + Alpha => Bgra32");
                        bpp = 8;
                        pixel_size = 1; // 1 byte Alpha + indexed BGR, Bgra32
                        palette_raw_bgr = br.ReadBytes(768);
                        is_alpha = true;
                        break;
                    default: throw new NotSupportedException("Not supported EPA color depth!");
                } // => 0=>1, 4|3=>2
                lines.Add(new Line(hdr.ByteArrayToString(), fm));

                int[] offsets = new int[16] { // u32
                    0, 1, width, width + 1,
                    2, width - 1, width << 1, 3,
                    (width << 1) + 2, width + 2, (width << 1) + 1, (width << 1) - 1,
                    /*(width << 1) - 2*/ (width - 1) << 1, width - 2, width * 3, 4
                };
                // >>1 = /2   2^1
                // >>2 = /4   2^2
                // >>3 = /8   2^3
                var dst = new byte[width * height * pixel_size]; // pixel_size = (bpp >> 3)
                var src = br.ReadBytes((int)(ms.Length - br.BaseStream.Position));
                var off = EPA_Decompress(offsets, src, 0, dst, 0); // R pixels + G pixels + B + pixels

                DDS dds = null;
                if (bpp != 8)
                {
                    int src_index = 0;
                    if (bpp > 16) // argb | rgb
                    {
                        dds = new DDS(width, height, bpp == 24 ? DDS.PixelFormat.D3DFMT_R8G8B8 : DDS.PixelFormat.DXGI_FORMAT_B8G8R8A8_UNORM);
                        var bitmapData = dds.Pixels;
                        int stride = width * pixel_size;
                        
                        for (int p = 0; p < pixel_size; ++p)
                        {
                            for (int y = 0; y < height; ++y)
                            {
                                int i = y * stride + p;
                                for (int x = 0; x < width; ++x)
                                {
                                    bitmapData[i] = dst[src_index++];
                                    i += pixel_size;
                                }
                            }
                        }
                    }
                    else
                    {
                        // 565 => 888
                        dds = new DDS(width, height, DDS.PixelFormat.D3DFMT_R8G8B8);
                        var bitmap = dds.Pixels;

                        int dds_offset = 0;
                        int channel_size = width * height;
                        for (int y = 0; y < height; ++y)
                        {
                            for (int x = 0; x < width; ++x)
                            {
                                byte c1 = dst[src_index + x];
                                byte c2 = dst[src_index + x + channel_size];

                                // write 2byte
                                //bitmap[dds_offset++] = (byte)(c2 & 3 | (c1 & 7 | (c2 & 0xFC) << 1) << 2);
                                //bitmap[dds_offset++] = (byte)(c1 & 0xC0 | (c2 & 0xE3 | (c1 >> 1) & 0x1C) >> 2);

                                var p1 = (byte)(c2 & 3 | (c1 & 7 | (c2 & 0xFC) << 1) << 2);
                                var p2 = (byte)(c1 & 0xC0 | (c2 & 0xE3 | (c1 >> 1) & 0x1C) >> 2);

                                int pixel565 = (p2 << 8) | p1;
                                bitmap[dds_offset++] = (byte)(pixel565 & 0x1F); // b
                                bitmap[dds_offset++] = (byte)((pixel565 & 0x7E0) >> 5); // g
                                bitmap[dds_offset++] = (byte)((pixel565 & 0xF800) >> 11); // r
                                // WORD pixel565 = (red_value << 11) | (green_value << 5) | blue_value;
                            }
                            src_index += width;
                        }
                    }
                }
                else
                {
                    if (is_alpha)
                    {
                        // Palette = ImageFormat.ReadPalette(m_input.AsStream, 0x100, PaletteFormat.Bgr);
                        var alpha = new byte[dst.Length];
                        EPA_Decompress(offsets, src, off, alpha, 0);
                        dds = new DDS(width, height, DDS.PixelFormat.DXGI_FORMAT_B8G8R8A8_UNORM);
                        var bitmapData = dds.Pixels;
                        int i = 0;
                        for (int j = 0; j < dst.Length; ++j)
                        {
                            var colorIndex = dst[j] * 3;
                            bitmapData[i++] = palette_raw_bgr[colorIndex];     // color.B
                            bitmapData[i++] = palette_raw_bgr[colorIndex + 1]; // color.G
                            bitmapData[i++] = palette_raw_bgr[colorIndex + 2]; // color.R
                            bitmapData[i++] = alpha[j];
                        }

                    }
                    else
                    {
                        dds = new DDS(width, height, DDS.PixelFormat.D3DFMT_R8G8B8);
                        var bitmapData = dds.Pixels;
                        int i = 0;
                        for (int j = 0; j < dst.Length; ++j)
                        {
                            var colorIndex = dst[j] * 3;
                            bitmapData[i++] = palette_raw_bgr[colorIndex];     // color.B
                            bitmapData[i++] = palette_raw_bgr[colorIndex + 1]; // color.G
                            bitmapData[i++] = palette_raw_bgr[colorIndex + 2]; // color.R
                        }
                    }
                }

                var rawDDS = dds.Build();
                FsOut.WriteAllBytes(Path.ChangeExtension(CurrentFilePath, ".DDS"), rawDDS);
            }

            return lines;
        }

        public override byte[] RepackText(List<Line> lines)
        {
            CurrentFilePath = Path.ChangeExtension(CurrentFilePath, ".DDS");
            var bytes = ReadCurrentFileData();
            var image = new DDS(bytes);
            var pixel_size = image.PixelSize;
            var src = image.Pixels;

            int type;
            if (image.Format == DDS.PixelFormat.D3DFMT_R8G8B8)
            {
                type = 1;
            }
            else if (image.Format == DDS.PixelFormat.DXGI_FORMAT_B8G8R8A8_UNORM)
            {
                type = 2;
            }
            else if (image.Format == DDS.PixelFormat.DXGI_FORMAT_B8G8R8X8_UNORM)
            {
                type = 0;
                pixel_size = 3;
                // convert to 24bit
                var src24 = new byte[image.Width * image.Height * pixel_size];
                for (int i = 0; i < src.Length; i++)
                {
                    src24[type++] = src[i++];
                    src24[type++] = src[i++];
                    src24[type++] = src[i++];
                }
                type = 1;
                src = src24;
            }
            else
            {
                throw new Exception("DDS Format!");
            }

            int stride = image.Width * pixel_size;
            int dst_index = 0;
            var dst = new byte[src.Length];
            for (int p = 0; p < pixel_size; ++p)
            {
                for (int y = 0; y < image.Height; ++y)
                {
                    int i = y * stride + p;
                    for (int x = 0; x < image.Width; ++x)
                    {
                        dst[dst_index++] = src[i];
                        i += pixel_size;
                    }
                }
            }
            var com = EPA_Compress(dst);
            using (var ms = new MemoryStream(com.Length + 0x10))
            using (var bw = new BinaryWriter(ms))
            {
                bw.Write((short)0x5045);
                bw.Write((byte)1); // 2
                bw.Write((byte)1); // 3 // diff_flag = 1 => pos 10
                bw.Write(type);
                bw.Write(image.Width);
                bw.Write(image.Height);
                bw.Write(com);

                return ms.ToArray();
            }
        }

        static byte[] EPA_Compress(byte[] bytes)
        {
            using (var ms = new MemoryStream(bytes.Length * 2))
            {
                var i = 0;
                var buf = new byte[1] { 0x0F };
                while (i < bytes.Length)
                {
                    var min = bytes.Length - i;
                    if (min < 0xF)
                    {
                        buf[0] = (byte)min;
                        ms.Write(buf, 0, 1);
                        ms.Write(bytes, i, min);
                        i += min; // END
                        buf[0] = 0x0F;
                    }
                    else
                    {
                        ms.Write(buf, 0, 1);
                        ms.Write(bytes, i, 0xF);
                        i += 0x0F;
                    }
                }

                return ms.ToArray();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="offsets"></param>
        /// <param name="src"></param>
        /// <param name="srcOffset"></param>
        /// <param name="dst"></param>
        /// <param name="dstOffset"></param>
        /// <returns>Current source offset</returns>
        static int EPA_Decompress(int[] offsets, byte[] src, int srcOffset, byte[] dst, int dstOffset)
        {
            int src_ptr = srcOffset;
            int dst_ptr = dstOffset;
            var dst_size = dst.Length;

            while (dst_ptr < dst_size) // src_ptr < src.Length && dst_ptr < dst_size
            {
                var code = src[src_ptr++];
                int count;

                if ((code & 0xF0) != 0) // 11110000
                {
                    if (0 != (code & 8))
                    {
                        count = ((code & 7) << 8) + src[src_ptr++];
                    }
                    else
                    {
                        count = code & 7;
                    }

                    if (count == 0)
                        Console.WriteLine("b?");
                    else
                    {
                        code >>= 4;
                        int back_offset = dst_ptr - offsets[code];

                        if (dst_ptr + count > dst_size)
                        {
                            // Exceeds output buffer
                            count = dst_size - dst_ptr;
                            Console.WriteLine("back_count");
                        }

                        //Binary.CopyOverlapped(output, dst-m_offset_table[flag >> 4], dst, count);
                        //Buffer.BlockCopy(dst, back_offset, dst, dst_ptr, count);
                        CopyOverlapped(dst, back_offset, dst_ptr, count);

                        dst_ptr += count;
                    }
                }
                else  // if code = [1..15] 00001111 = 0F
                {
                    count = code;

                    if (count == 0)
                        Console.WriteLine("f?");
                    else
                    {
                        // uncompressed
                        if (dst_ptr + count > dst_size)
                        {
                            // Exceeds output buffer
                            count = dst_size - dst_ptr;
                            Console.WriteLine("first_count");
                        }

                        Buffer.BlockCopy(src, src_ptr, dst, dst_ptr, count);
                        src_ptr += count;
                        dst_ptr += count;
                    }
                }
            }

            return src_ptr;
        }

        // https://github.com/morkt/GARbro/blob/c5e13f6db1d24a62eb621c38c6fc31387338d857/GameRes/Utility.cs#L80
        /// <summary>
        /// Copy potentially overlapping sequence of <paramref name="count"/> bytes in array
        /// <paramref name="data"/> from <paramref name="src"/> to <paramref name="dst"/>.
        /// If destination offset resides within source region then sequence will repeat itself.
        /// Widely used in various compression techniques.
        /// </summary>
        public static void CopyOverlapped(byte[] data, int src, int dst, int count)
        {
            if (dst > src)
            {
                while (count > 0)
                {
                    int preceding = System.Math.Min(dst - src, count);
                    System.Buffer.BlockCopy(data, src, data, dst, preceding);
                    dst += preceding;
                    count -= preceding;
                }
            }
            else
            {
                System.Buffer.BlockCopy(data, src, data, dst, count);
            }
        }
    }
}

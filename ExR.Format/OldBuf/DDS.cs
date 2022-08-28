using System;

namespace ExR.Format
{
    class DDS
    {
        private int _w;
        private int _h;
        private int _bpp;
        private int _pixelSize;
        private int _headerSize = 0x80;
        private byte[] _header = {
            0x44, 0x44, 0x53, 0x20, 0x7C, 0x00, 0x00, 0x00, 0x07, 0x10, 0x08, 0x00, 0x40, 0x02, 0x00, 0x00, // 00
            0x00, 0x04, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // 10
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // 20
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // 30
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x20, 0x00, 0x00, 0x00, // 40
            0x40, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // 50
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x10, 0x00, 0x00, // 60
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00  // 70
        };
        private byte[] _body;
        private PixelFormat _format;
        private byte[] _dds = null;

        /// <summary>
        /// Create or Read
        /// </summary>
        private bool _isRead = false;

        // pfim read without decompress
        /// <summary>
        /// Read DDS
        /// </summary>
        /// <param name="dds"></param>
        /// <param name="mipmap"></param>
        public DDS(byte[] dds, bool mipmap = false)
        {
            _isRead = true;
            _dds = dds;
            _h = (dds[0xF] << 24) | (dds[0xE] << 16) | (dds[0xD] << 8) | dds[0xC];
            _w = (dds[0x13] << 24) | (dds[0x12] << 16) | (dds[0x11] << 8) | dds[0x10];

            _headerSize = 0x80;
            if (dds[0x54] != 0)
            {
                /*
typedef struct {
  DXGI_FORMAT              dxgiFormat; // fmt
  D3D10_RESOURCE_DIMENSION resourceDimension; // 3 D3D10_RESOURCE_DIMENSION_TEXTURE2D
  UINT                     miscFlag; // 0 
  UINT                     arraySize; // 1
  UINT                     miscFlags2; // 0
} DDS_HEADER_DXT10;
                 */
                switch (dds[0x57])
                {
                    case 0x30:
                        _format = PixelFormat.DDSPF_DX10;
                        _headerSize += 0x14;
                        int dxfm = (dds[0x83] << 24) | (dds[0x82] << 16) | (dds[0x81] << 8) | dds[0x80];
                        _format = (PixelFormat)dxfm;
                        break;
                    case 0x31:
                        _format = PixelFormat.D3DFMT_DXT1;
                        break;
                    case 0x33:
                        _format = PixelFormat.D3DFMT_DXT3;
                        break;
                    case 0x35:
                        _format = PixelFormat.D3DFMT_DXT5;
                        break;
                }

                _bpp = BitsPerPixel(_format);
                _pixelSize = _bpp >> 3;
            }
            else
            {
                _bpp = dds[0x58];
                _pixelSize = _bpp >> 3;
                if (_bpp == 8)
                {
                    _format = PixelFormat.DXGI_FORMAT_A8_UNORM;
                }
                else if (_bpp == 32)
                {
                    if (dds[0x6B] == 0xFF)
                    {
                        // BB GG RR AA (nvidia)    giving BGRA is understandable
                        _format = PixelFormat.DXGI_FORMAT_B8G8R8A8_UNORM;

                        if (dds[0x64] != 0xFF)
                        {
                            //  RR GG BB AA (intel, M$) => convert RGBA to BGRA
                            for (int i = _headerSize; i < dds.Length; i += _pixelSize)
                            {
                                var tmp = dds[i];
                                dds[i] = dds[i + 2];
                                dds[i + 2] = tmp;
                            }
                        }
                    }
                    else
                    {
                        _format = PixelFormat.DXGI_FORMAT_B8G8R8X8_UNORM;
                    }
                }
                else if (_bpp == 24)
                {
                    _format = PixelFormat.D3DFMT_R8G8B8;
                    if (dds[0x64] != 0xFF)
                    {
                        //  RR GG BB AA (intel, M$) => convert RGBA to BGRA
                        for (int i = _headerSize; i < dds.Length; i += PixelSize)
                        {
                            var tmp = dds[i];
                            dds[i] = dds[i + 2];
                            dds[i + 2] = tmp;
                        }
                    }
                }
                else if (_bpp == 16)
                {
                    if (dds[0x5D] == 0xF8)
                    {
                        _format = PixelFormat.DXGI_FORMAT_B5G6R5_UNORM;
                    }
                    else
                    {
                        _format = PixelFormat.DXGI_FORMAT_B5G5R5A1_UNORM;
                    }
                }
            }

            if (mipmap)
            {
                _body = new byte[dds.Length - _headerSize];
            }
            else
            {
                var len = BPP > 4 ? _w * _h * _pixelSize : (_w * _h) >> 1; // no mip map
                _body = new byte[len];
            }
            Buffer.BlockCopy(dds, _headerSize, _body, 0, _body.Length);
        }

        /// <summary>
        /// Create new DDS
        /// </summary>
        /// <param name="w"></param>
        /// <param name="h"></param>
        /// <param name="fmt"></param>
        /// <param name="isDx10"></param>
        public DDS(int w, int h, PixelFormat fmt, bool isDx10 = false)
        {
            _w = w;
            _h = h;
            _format = fmt;
            _bpp = BitsPerPixel(fmt);
            if (_bpp < 8) // 4 bit
            {
                int len = w * h;
                len >>= 1;
                _body = new byte[len];
            }
            else
            {
                _pixelSize = _bpp >> 3; // >> 3
                int len = w * h * _pixelSize;
                _body = new byte[len];
            }

            unsafe
            {
                fixed (byte* bufferRef = _header)
                {
                    var arr = (uint*)bufferRef;
                    arr[0x0C >> 2] = (uint)h;
                    arr[0x10 >> 2] = (uint)w;
                    arr[0x14 >> 2] = (uint)_body.Length; // pitchOrLinearSize, pitch = w * _pixelSize

                    if (isDx10)
                    {
                        Array.Resize(ref _header, _header.Length + 0x14);
                        arr[0x54 >> 2] = (uint)PixelFormat.DDSPF_DX10;  // DDPF_FOURCC
                        arr[0x80 >> 2] = (uint)fmt;
                        arr[0x84 >> 2] = 3; // D3D10_RESOURCE_DIMENSION_TEXTURE2D
                        arr[0x8C >> 2] = 1;
                    }
                    else
                    {
                        switch (fmt)
                        {
                            case PixelFormat.DXGI_FORMAT_B8G8R8A8_UNORM:
                                arr[0x50 >> 2] = 0x41; // DDPF_ALPHAPIXELS | DDPF_RGB
                                arr[0x58 >> 2] = 32;
                                arr[0x5C >> 2] = 0x00FF0000; // R
                                arr[0x60 >> 2] = 0x0000FF00; // G
                                arr[0x64 >> 2] = 0x000000FF; // B
                                arr[0x68 >> 2] = 0xFF000000; // A
                                //arr[0x14 >> 2] = (uint)(_w * _pixelSize); // pitch
                                break;

                            case PixelFormat.DXGI_FORMAT_R8G8B8A8_UNORM:
                                arr[0x50 >> 2] = 0x41; // DDPF_ALPHAPIXELS | DDPF_RGB
                                arr[0x58 >> 2] = 32;
                                arr[0x5C >> 2] = 0x000000FF; // R
                                arr[0x60 >> 2] = 0x0000FF00; // G
                                arr[0x64 >> 2] = 0x00FF0000; // B
                                arr[0x68 >> 2] = 0xFF000000; // A
                                //arr[0x14 >> 2] = (uint)(_w * _pixelSize); // pitch
                                break;

                            case PixelFormat.DXGI_FORMAT_B8G8R8X8_UNORM:
                                arr[0x50 >> 2] = 0x41; // DDPF_ALPHAPIXELS | DDPF_RGB
                                arr[0x58 >> 2] = 32;
                                arr[0x5C >> 2] = 0x00FF0000; // R
                                arr[0x60 >> 2] = 0x0000FF00; // G
                                arr[0x64 >> 2] = 0x000000FF; // B
                                arr[0x68 >> 2] = 0x00000000; // A
                                //arr[0x14 >> 2] = (uint)(_w * _pixelSize); // pitch
                                break;

                            case PixelFormat.D3DFMT_R8G8B8: // Directx RGB is actually taken as BGR, no D3DFMT_B8G8R8
                                arr[0x50 >> 2] = 0x40; // DDPF_RGB
                                arr[0x58 >> 2] = 24;
                                arr[0x5C >> 2] = 0x00FF0000; // R
                                arr[0x60 >> 2] = 0x0000FF00; // G
                                arr[0x64 >> 2] = 0x000000FF; // B
                                arr[0x68 >> 2] = 0x00000000; // A
                                //arr[0x14 >> 2] = (uint)(_w * _pixelSize); // pitch
                                break;

                            case PixelFormat.DXGI_FORMAT_B5G6R5_UNORM: // D3DFMT_R5G6B5 <=> BGR?
                                arr[0x50 >> 2] = 0x40; // DDPF_RGB
                                arr[0x58 >> 2] = 16;
                                arr[0x5C >> 2] = 0x0000F800; // R
                                arr[0x60 >> 2] = 0x0000E007; // G
                                arr[0x64 >> 2] = 0x0000001F; // B
                                arr[0x68 >> 2] = 0x00000000; // A
                                //arr[0x14 >> 2] = (uint)(_w * _pixelSize); // pitch
                                break;

                            case PixelFormat.DXGI_FORMAT_B5G5R5A1_UNORM:
                                arr[0x50 >> 2] = 0x40; // DDPF_RGB
                                arr[0x58 >> 2] = 16;
                                arr[0x5C >> 2] = 0x00007C00; // R
                                arr[0x60 >> 2] = 0x0000E003; // G
                                arr[0x64 >> 2] = 0x0000001F; // B
                                arr[0x68 >> 2] = 0x00000000; // A
                                //arr[0x14 >> 2] = (uint)(_w * _pixelSize); // pitch
                                break;

                            case PixelFormat.DXGI_FORMAT_A8_UNORM:
                                arr[0x50 >> 2] = 2; // DDPF_ALPHA
                                arr[0x58 >> 2] = 8;
                                arr[0x5C >> 2] = 0x00000000; // R
                                arr[0x60 >> 2] = 0x00000000; // G
                                arr[0x64 >> 2] = 0x00000000; // B
                                arr[0x68 >> 2] = 0xFF000000; // A
                                //arr[0x14 >> 2] = (uint)(_w >> 1);
                                break;

                            case PixelFormat.D3DFMT_DXT1:
                            case PixelFormat.D3DFMT_DXT3:
                            case PixelFormat.D3DFMT_DXT5:
                                arr[0x50 >> 2] = 4; // DDPF_FOURCC
                                arr[0x54 >> 2] = (uint)fmt; // DXT?
                                break;

                            default:
                                break;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Only for Create
        /// </summary>
        /// <returns></returns>
        public byte[] Build()
        {
            if (_isRead == false)
            {
                if (_dds == null)
                {
                    _dds = new byte[_header.Length + _body.Length];
                    Buffer.BlockCopy(_header, 0, _dds, 0, _header.Length);
                }
                Buffer.BlockCopy(_body, 0, _dds, _header.Length, _body.Length);
            }

            return _dds;
        }

        /// <summary>
        /// [Read] BB GG RR (AA) or compressed
        /// </summary>
        public byte[] Pixels
        {
            get
            {
                if (_isRead)
                {
                    if (_body == null)
                    {
                        _body = new byte[_dds.Length - _headerSize];
                        Buffer.BlockCopy(_dds, _headerSize, _body, 0, _body.Length);
                    }
                }

                return _body;
            }
        }

        public PixelFormat Format => _format;

        public int PixelSize => _pixelSize;

        public int BPP => _bpp;

        public int Width => _w;

        public int Height => _h;

        private int BitsPerPixel(PixelFormat fmt)
        {
            switch (fmt)
            {
                case PixelFormat.DXGI_FORMAT_B8G8R8A8_UNORM:
                case PixelFormat.DXGI_FORMAT_B8G8R8X8_UNORM:
                case PixelFormat.DXGI_FORMAT_R8G8B8A8_UNORM:
                    return 32;

                case PixelFormat.D3DFMT_R8G8B8: // no DXGI
                    return 24;

                case PixelFormat.DXGI_FORMAT_B5G6R5_UNORM:
                case PixelFormat.DXGI_FORMAT_B5G5R5A1_UNORM:
                    return 16;

                case PixelFormat.DXGI_FORMAT_A8_UNORM:
                    return 8;

                case PixelFormat.D3DFMT_DXT1:
                case PixelFormat.DXGI_FORMAT_BC1_UNORM: // <=> DXT1
                    return 4;

                case PixelFormat.D3DFMT_DXT3:
                case PixelFormat.D3DFMT_DXT5:
                case PixelFormat.DXGI_FORMAT_BC3_UNORM: // <=> DXT5
                case PixelFormat.DXGI_FORMAT_BC5_UNORM:
                case PixelFormat.DXGI_FORMAT_BC7_UNORM:
                    return 8;

                case PixelFormat.DDSPF_DX10:
                    return -1;

                default:
                    return 0;
            }
        }

        public enum PixelFormat : uint
        {
            D3DFMT_DXT1 = 0x31545844, // 4 bit, BC1
            D3DFMT_DXT3 = 0x33545844, // 8 bit, BC2
            D3DFMT_DXT5 = 0x35545844, // 8 bit, BC3
            DDSPF_DX10 = 0x30315844,  // ? bit, extracr header
            // Direct3D11 24-bit are gone
            D3DFMT_R8G8B8              = 20, // D3DFMT_R8G8B8   24bit
            DXGI_FORMAT_B8G8R8A8_UNORM = 87, // D3DFMT_A8R8G8B8 32bit
            DXGI_FORMAT_R8G8B8A8_UNORM = 28, // 
            DXGI_FORMAT_B8G8R8X8_UNORM = 88, // D3DFMT_X8R8G8B8 32bit (blank alpha)
            DXGI_FORMAT_B5G6R5_UNORM   = 85, // D3DFMT_R5G6B5 16bit
            DXGI_FORMAT_B5G5R5A1_UNORM = 24, // D3DFMT_R5G5B5 16bit
            DXGI_FORMAT_A8_UNORM       = 65, // D3DFMT_A8 8bit
            DXGI_FORMAT_BC1_UNORM = 71, // No Alpha
            DXGI_FORMAT_BC3_UNORM = 77, // Alpha
            DXGI_FORMAT_BC5_UNORM = 83,
            DXGI_FORMAT_BC7_UNORM = 98,
        }
    }
}

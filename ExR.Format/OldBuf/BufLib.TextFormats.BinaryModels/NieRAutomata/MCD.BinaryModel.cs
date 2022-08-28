using System.Runtime.InteropServices;

namespace BufLib.TextFormats.BinaryModels.NieRAutomata
{
    internal partial class MCD
    {
        #region Structure
        [StructLayout(LayoutKind.Sequential, Size = Size)]
        struct Header
        {
            public const int Size = 0x24;

            public int offset_string_table;
            public int count_string_table;
            public int offset_symbol_codes;
            public int count_symbol_codes;
            public int offset_symbol_table;
            public int count_symbol_table;
            public int offset_fonts_table;
            public int count_fonts_table;
            public int offset_unk2;
            public int count_unk2;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct Code
        {
            public short ct_fontid;
            public char ct_char;
            public int ct_code;

            public override string ToString()
            {
                return string.Format("{0} {1} {2}", ct_code, ct_char, ct_fontid);
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        struct Symbol
        {
            public int texid;
            public float u1;
            public float v1;
            public float u2;
            public float v2;
            public float w;
            public float h;
            public float z1;
            public float z2;
            public float z3;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct StringSectionA
        {
            public int offset;
            public int count;
            public int unk;
            public int Id;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct StringSectionB
        {
            public int offset;
            public int count;
            public int unk;
            public int totalLenght;
            public int fontid;
        }

        [StructLayout(LayoutKind.Sequential, Size = Size)]
        struct StringSectionC
        {
            public const int Size = 0x18;

            public int offset;
            public int be_prev_block_len;
            public int be_length;
            public int be_length2;
            public float line_height;
            public float ZERO;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct StringSectionD // model, dynamic
        {
            public int pointer;
            public int size;
            public int offsetOffPointer;
            public int fontId;

            public StringSectionD(int pointer, int size, int offsetOffPointer, int fontId)
            {
                this.pointer = pointer;
                this.size = size;
                this.offsetOffPointer = offsetOffPointer;
                this.fontId = fontId;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        struct EncodedChar
        {
            public ushort Index;
            public short Spacing;
        }
        #endregion
    }
}

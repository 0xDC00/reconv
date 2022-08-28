using System.Runtime.InteropServices;

namespace BufLib.TextFormats.BinaryModels.Catherine
{
    public static partial class BF
    {
        [StructLayout(LayoutKind.Sequential, Pack = 1, Size = SIZE)]
        struct Header
        {
            public const int SIZE = 32;

            // 0x00
            public byte FileType;

            // 0x01
            [MarshalAs(UnmanagedType.I1)] // i1 = 1byte
            public bool Compressed;

            // 0x02
            public short UserId;

            // 0x04
            public int FileSize;

            // 0x08
            public int Magic;

            // 0x0C
            public int Field0C;

            // 0x10
            public int SectionCount;

            // 0x14
            public short LocalIntVariableCount;

            // 0x16
            public short LocalFloatVariableCount;

            // 0x18
            public short Endianness;

            // 0x1A
            public short Field1A;

            // 0x1C
            public int Padding;
        }

        public enum SectionType : uint
        {
            ProcedureLabelSection,
            JumpLabelSection,
            TextSection,
            MessageScriptSection,
            StringSection,
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1, Size = SIZE)]
        public struct SectionHeader
        {
            public const int SIZE = 16;

            // 0x00
            [MarshalAs(UnmanagedType.U4)]
            public SectionType SectionType;

            // 0x04
            public int ElementSize;

            // 0x08
            public int ElementCount;

            // 0x0C
            public int FirstElementAddress;

            public override string ToString()
            {
                return $"{SectionType} {ElementSize} {ElementCount} {FirstElementAddress}";
            }
        }
    }
}

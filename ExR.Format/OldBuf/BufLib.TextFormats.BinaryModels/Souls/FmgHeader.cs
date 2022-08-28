using System.Runtime.InteropServices;

namespace BufLib.TextFormats.BinaryModels.Souls
{
    [StructLayout(LayoutKind.Sequential, Size = Size)]
    class FmgHeader_2
    {
        public int unknown1; // DS=0131072, Bb=00000200

        public int fileSize;

        public short unknown2_1; // 1, = LE, -255 = BE (2byte)

        public short unknown2_2;

        public int idRangeCount;

        public int stringOffsetCount;

        public int unknown3; // 255

        public long stringOffsetSectionOffset; // long + long ?

        public int unknown4; // 0

        public int unknown5; // 0

        public const int Size = 0x28;
    } // Len = 0x28

    [StructLayout(LayoutKind.Sequential, Pack = 4, Size = Size)]
    class FmgHeader_1
    {
        public int unknown1; // DS1=00000100, Bb=00000200  <=> 10k = 32bit, 20k = 64 bit?

        public int fileSize;

        public short unknown2_1; // 1, = LE, -255 = BE (2byte)

        public short unknown2_2;

        public int idRangeCount;

        public int stringOffsetCount;

        // public int unknown3; // 255

        public long stringOffsetSectionOffset; // int + int ?

        public const int Size = 0x1C;
    } // Len = 0x1C
}

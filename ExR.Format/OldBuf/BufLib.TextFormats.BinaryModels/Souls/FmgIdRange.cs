using System.Runtime.InteropServices;

namespace BufLib.TextFormats.BinaryModels.Souls
{
    [StructLayout(LayoutKind.Sequential)]
    class FmgIdRange_2
    {
        public int OffsetIndex { get; set; }

        public int FirstId { get; set; }

        public int LastId { get; set; }

        public int Unknown { get; set; }

        // Prop <=> Method
        public int IdCount => LastId - FirstId + 1;
    }

    [StructLayout(LayoutKind.Sequential)]
    class FmgIdRange_1
    {
        public int OffsetIndex { get; set; }

        public int FirstId { get; set; }

        public int LastId { get; set; }

        // Prop
        public int IdCount => LastId - FirstId + 1;
    }
}

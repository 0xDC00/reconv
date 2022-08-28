using System;
using System.Runtime.InteropServices;
using static BufLib.Common.IO.EndiannessHelper;

namespace BufLib.Common.IO
{
    public partial class EndianBinaryWriter
    {
        public void WriteStruct<T>(T t)
        {
            WriteStruct(t, Endianness);
        }

        public unsafe void WriteStruct<T>(T t, Endian endianness)
        {
            var buffer = new byte[Marshal.SizeOf(typeof(T))];
            fixed (byte* pBuffer = buffer)
                Marshal.StructureToPtr(t, (IntPtr)pBuffer, false);
            if(endianness != NativeEndianness)
            {
                AdjustByteOrder(typeof(T), buffer);
            }
            Write(buffer);
        }

        public void WriteStructs<T>(T[] t)
        {
            WriteStructs(t, Endianness);
        }

        public unsafe void WriteStructs<T>(T[] t, Endian endianness)
        {
            for (int i = 0; i < t.Length; i++)
            {
                WriteStruct(t[i], endianness);
            }
        }
    }
}

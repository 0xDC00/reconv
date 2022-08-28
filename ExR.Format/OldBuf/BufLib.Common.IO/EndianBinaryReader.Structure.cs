using System.Runtime.InteropServices;
using static BufLib.Common.IO.EndiannessHelper;

namespace BufLib.Common.IO
{
    public partial class EndianBinaryReader
    {
        public T ReadStruct<T>()
        {
            return ReadStruct<T>(Endianness);
        }

        public T ReadStruct<T>(Endian endianness)
        {
            var type = typeof(T);
            var byteLength = Marshal.SizeOf(type);
            var bytes = ReadBytes(byteLength);
            if(endianness != NativeEndianness)
            {
                AdjustByteOrder(type, bytes);
            }
            var pinned = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            var stt = (T)Marshal.PtrToStructure(
                pinned.AddrOfPinnedObject(),
                type);
            pinned.Free();
            return stt;
        }

        public T[] ReadStructs<T>(int count)
        {
            return ReadStructs<T>(count, Endianness);
        }

        public T[] ReadStructs<T>(int count, Endian endianness)
        {
            T[] values = new T[count];
            for (int i = 0; i < count; i++)
                values[i] = ReadStruct<T>(endianness);

            return values;
        }
    }
}
